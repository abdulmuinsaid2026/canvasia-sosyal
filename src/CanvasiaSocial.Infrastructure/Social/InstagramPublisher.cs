using System.Text.Json;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class InstagramPublisher(HttpClient httpClient, IReadOnlyDictionary<Platform, SocialProviderOptions> allOptions)
    : ISocialPublisher
{
    private readonly SocialProviderOptions options = allOptions[Platform.Instagram];
    public Platform Platform => Platform.Instagram;
    public SocialProviderConfiguration Configuration => options.ToConfiguration();

    public Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var url = $"https://www.instagram.com/oauth/authorize?client_id={Uri.EscapeDataString(options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}&response_type=code" +
            $"&scope={Uri.EscapeDataString(string.Join(',', options.Scopes))}&state={Uri.EscapeDataString(state)}";
        return Task.FromResult(new Uri(url));
    }

    public async Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var shortRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.instagram.com/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = options.RedirectUri,
                ["code"] = callback.Code
            })
        };
        using var shortResponse = await httpClient.SendAsync(shortRequest, cancellationToken);
        using var shortJson = await SocialHttp.ReadJsonAsync(shortResponse, cancellationToken);
        var tokenNode = shortJson.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray().First()
            : shortJson.RootElement;
        var shortToken = tokenNode.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Instagram erişim anahtarı döndürmedi.");

        var exchangeUrl = "https://graph.instagram.com/access_token?grant_type=ig_exchange_token" +
            $"&client_secret={Uri.EscapeDataString(options.ClientSecret)}&access_token={Uri.EscapeDataString(shortToken)}";
        using var exchangeRequest = new HttpRequestMessage(HttpMethod.Get, exchangeUrl);
        using var exchangeResponse = await SocialHttp.SendSensitiveAsync(exchangeRequest, cancellationToken);
        using var exchangeJson = await SocialHttp.ReadJsonAsync(exchangeResponse, cancellationToken);
        var accessToken = exchangeJson.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Instagram uzun süreli erişim anahtarı döndürmedi.");
        var expiresAt = ReadExpiry(exchangeJson.RootElement);

        using var meRequest = SocialHttp.Bearer(HttpMethod.Get,
            new Uri(new Uri(options.ApiBaseUrl), "me?fields=id,user_id,username,name,profile_picture_url").ToString(), accessToken);
        using var meResponse = await httpClient.SendAsync(meRequest, cancellationToken);
        using var meJson = await SocialHttp.ReadJsonAsync(meResponse, cancellationToken);
        var root = meJson.RootElement;
        var id = ReadString(root, "user_id") ?? ReadString(root, "id")
            ?? throw new InvalidOperationException("Instagram hesap kimliği döndürmedi.");
        var username = ReadString(root, "username");
        return new SocialConnection(id, ReadString(root, "name") ?? username ?? $"Instagram {id}", username,
            ReadString(root, "profile_picture_url"), accessToken, null, expiresAt, options.Scopes);
    }

    public async Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default)
    {
        using var request = SocialHttp.Bearer(HttpMethod.Get,
            new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(account.ExternalAccountId)}?fields=id,username").ToString(), account.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return new SocialValidationResult(true);
        var error = SocialHttp.CreateException(response, await response.Content.ReadAsStringAsync(cancellationToken));
        if (error.Kind is SocialPublishFailureKind.Unauthorized or SocialPublishFailureKind.Forbidden)
            return new SocialValidationResult(false, error.Message);
        throw error;
    }

    public Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(post.Caption)) return Task.FromResult(new SocialValidationResult(false, "Instagram açıklaması boş olamaz."));
        if (post.Caption.Length > 2200) return Task.FromResult(new SocialValidationResult(false, "Instagram açıklaması 2200 karakteri aşamaz."));
        return Task.FromResult(new SocialValidationResult(true));
    }

    public async Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePostAsync(post, cancellationToken);
        if (!validation.IsValid) throw new SocialPublisherException(validation.Error!, SocialPublishFailureKind.InvalidContent);
        var accountPath = Uri.EscapeDataString(account.ExternalAccountId);
        using var createRequest = SocialHttp.Bearer(HttpMethod.Post,
            new Uri(new Uri(options.ApiBaseUrl), $"{accountPath}/media").ToString(), account.AccessToken,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["image_url"] = post.ImageUrl.ToString(), ["caption"] = post.Caption }));
        using var createResponse = await httpClient.SendAsync(createRequest, cancellationToken);
        using var createJson = await SocialHttp.ReadJsonAsync(createResponse, cancellationToken);
        var creationId = createJson.RootElement.GetProperty("id").GetString()
            ?? throw new SocialPublisherException("Instagram medya kapsayıcısı oluşturulamadı.", SocialPublishFailureKind.Transient);

        await WaitUntilReadyAsync(creationId, account.AccessToken, cancellationToken);

        using var publishRequest = SocialHttp.Bearer(HttpMethod.Post,
            new Uri(new Uri(options.ApiBaseUrl), $"{accountPath}/media_publish").ToString(), account.AccessToken,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["creation_id"] = creationId }));
        using var publishResponse = await httpClient.SendAsync(publishRequest, cancellationToken);
        using var publishJson = await SocialHttp.ReadJsonAsync(publishResponse, cancellationToken);
        var postId = publishJson.RootElement.GetProperty("id").GetString()
            ?? throw new SocialPublisherException("Instagram yayın kimliği döndürmedi.", SocialPublishFailureKind.Transient);
        var permalink = await TryGetPermalinkAsync(postId, account.AccessToken, cancellationToken);
        return new SocialPublishResult(postId, permalink, JsonSerializer.Serialize(new { id = postId }));
    }

    public async Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default)
    {
        var refreshUrl = "https://graph.instagram.com/refresh_access_token?grant_type=ig_refresh_token" +
            $"&access_token={Uri.EscapeDataString(account.AccessToken)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, refreshUrl);
        using var response = await SocialHttp.SendSensitiveAsync(request, cancellationToken);
        using var json = await SocialHttp.ReadJsonAsync(response, cancellationToken);
        var token = json.RootElement.GetProperty("access_token").GetString() ?? account.AccessToken;
        return new SocialRefreshResult(token, null, ReadExpiry(json.RootElement), account.Scopes);
    }

    public async Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default)
    {
        using var request = SocialHttp.Bearer(HttpMethod.Delete,
            new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(account.ExternalAccountId)}/permissions").ToString(), account.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw SocialHttp.CreateException(response, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private async Task<string?> TryGetPermalinkAsync(string id, string token, CancellationToken cancellationToken)
    {
        using var request = SocialHttp.Bearer(HttpMethod.Get,
            new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(id)}?fields=permalink").ToString(), token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return ReadString(json.RootElement, "permalink");
    }

    private async Task WaitUntilReadyAsync(string creationId, string token, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var request = SocialHttp.Bearer(HttpMethod.Get,
                new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(creationId)}?fields=status_code,status").ToString(), token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            using var json = await SocialHttp.ReadJsonAsync(response, cancellationToken);
            var status = ReadString(json.RootElement, "status_code") ?? ReadString(json.RootElement, "status");
            if (status is "FINISHED") return;
            if (status is "ERROR" or "EXPIRED")
                throw new SocialPublisherException("Instagram görseli işleyemedi.", SocialPublishFailureKind.InvalidContent, providerErrorCode: status);
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
        throw new SocialPublisherException("Instagram görsel işleme süresi aşıldı.", SocialPublishFailureKind.Transient);
    }

    private void EnsureConfigured()
    {
        if (!options.IsConfigured) throw new InvalidOperationException("Instagram provider yapılandırılmamış.");
    }
    private static string? ReadString(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static DateTime? ReadExpiry(JsonElement root) => root.TryGetProperty("expires_in", out var value) && value.TryGetInt32(out var seconds)
        ? DateTime.UtcNow.AddSeconds(seconds) : null;
}

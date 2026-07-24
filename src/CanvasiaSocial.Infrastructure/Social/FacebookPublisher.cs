using System.Text.Json;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class FacebookPublisher(HttpClient httpClient, IReadOnlyDictionary<Platform, SocialProviderOptions> allOptions)
    : ISocialPublisher
{
    private readonly SocialProviderOptions options = allOptions[Platform.Facebook];
    public Platform Platform => Platform.Facebook;
    public SocialProviderConfiguration Configuration => options.ToConfiguration();

    public Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var url = $"https://www.facebook.com/v25.0/dialog/oauth?client_id={Uri.EscapeDataString(options.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}&response_type=code" +
            $"&scope={Uri.EscapeDataString(string.Join(',', options.Scopes))}&state={Uri.EscapeDataString(state)}";
        return Task.FromResult(new Uri(url));
    }

    public async Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var shortToken = await ExchangeAsync(new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["redirect_uri"] = options.RedirectUri,
            ["code"] = callback.Code
        }, cancellationToken);
        var longToken = await ExchangeAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "fb_exchange_token",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["fb_exchange_token"] = shortToken.AccessToken
        }, cancellationToken);

        using var pagesRequest = SocialHttp.Bearer(HttpMethod.Get,
            new Uri(new Uri(options.ApiBaseUrl), "me/accounts?fields=id,name,username,picture,access_token,tasks").ToString(), longToken.AccessToken);
        using var pagesResponse = await httpClient.SendAsync(pagesRequest, cancellationToken);
        using var pagesJson = await SocialHttp.ReadJsonAsync(pagesResponse, cancellationToken);
        var pages = pagesJson.RootElement.GetProperty("data").EnumerateArray().ToArray();
        var page = pages.FirstOrDefault(HasCreateContentTask);
        if (page.ValueKind == JsonValueKind.Undefined)
        {
            throw new SocialPublisherException("Yayın oluşturma yetkisine sahip bir Facebook Sayfası bulunamadı.", SocialPublishFailureKind.Forbidden);
        }
        var pageId = page.GetProperty("id").GetString()!;
        var pageToken = page.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Facebook Sayfa erişim anahtarı döndürmedi.");
        var image = page.TryGetProperty("picture", out var picture) && picture.TryGetProperty("data", out var pictureData) &&
                    pictureData.TryGetProperty("url", out var pictureUrl) ? pictureUrl.GetString() : null;
        return new SocialConnection(pageId, page.GetProperty("name").GetString() ?? $"Facebook {pageId}",
            page.TryGetProperty("username", out var username) ? username.GetString() : null, image,
            pageToken, null, null, options.Scopes);
    }

    public async Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default)
    {
        using var request = SocialHttp.Bearer(HttpMethod.Get,
            new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(account.ExternalAccountId)}?fields=id,name").ToString(), account.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return new SocialValidationResult(true);
        var error = SocialHttp.CreateException(response, await response.Content.ReadAsStringAsync(cancellationToken));
        if (error.Kind is SocialPublishFailureKind.Unauthorized or SocialPublishFailureKind.Forbidden)
            return new SocialValidationResult(false, error.Message);
        throw error;
    }

    public Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(post.Caption)) return Task.FromResult(new SocialValidationResult(false, "Facebook açıklaması boş olamaz."));
        if (post.Caption.Length > 63206) return Task.FromResult(new SocialValidationResult(false, "Facebook açıklaması desteklenen uzunluğu aşıyor."));
        return Task.FromResult(new SocialValidationResult(true));
    }

    public async Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default)
    {
        var validation = await ValidatePostAsync(post, cancellationToken);
        if (!validation.IsValid) throw new SocialPublisherException(validation.Error!, SocialPublishFailureKind.InvalidContent);
        using var request = SocialHttp.Bearer(HttpMethod.Post,
            new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(account.ExternalAccountId)}/photos").ToString(), account.AccessToken,
            new FormUrlEncodedContent(new Dictionary<string, string> { ["url"] = post.ImageUrl.ToString(), ["message"] = post.Caption }));
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var json = await SocialHttp.ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var id = root.TryGetProperty("post_id", out var postId) ? postId.GetString() : root.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new SocialPublisherException("Facebook yayın kimliği döndürmedi.", SocialPublishFailureKind.Transient);
        var permalink = await TryGetPermalinkAsync(id, account.AccessToken, cancellationToken);
        return new SocialPublishResult(id, permalink, JsonSerializer.Serialize(new { id }));
    }

    public async Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAccountAsync(account, cancellationToken);
        if (!validation.IsValid)
        {
            throw new SocialPublisherException("Facebook Sayfa erişimi yenilenemedi; hesabı yeniden bağlayın.", SocialPublishFailureKind.Unauthorized);
        }
        return new SocialRefreshResult(account.AccessToken, null, account.TokenExpiresAtUtc, account.Scopes);
    }

    public Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private async Task<(string AccessToken, DateTime? ExpiresAtUtc)> ExchangeAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        var query = string.Join('&', form.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(options.ApiBaseUrl), $"oauth/access_token?{query}"));
        using var response = await SocialHttp.SendSensitiveAsync(request, cancellationToken);
        using var json = await SocialHttp.ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;
        var token = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Facebook erişim anahtarı döndürmedi.");
        DateTime? expiry = root.TryGetProperty("expires_in", out var expires) && expires.TryGetInt32(out var seconds)
            ? DateTime.UtcNow.AddSeconds(seconds) : null;
        return (token, expiry);
    }

    private async Task<string?> TryGetPermalinkAsync(string id, string token, CancellationToken cancellationToken)
    {
        using var request = SocialHttp.Bearer(HttpMethod.Get,
            new Uri(new Uri(options.ApiBaseUrl), $"{Uri.EscapeDataString(id)}?fields=permalink_url").ToString(), token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return json.RootElement.TryGetProperty("permalink_url", out var value) ? value.GetString() : null;
    }

    private static bool HasCreateContentTask(JsonElement page) =>
        page.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array &&
        tasks.EnumerateArray().Any(x => string.Equals(x.GetString(), "CREATE_CONTENT", StringComparison.Ordinal));

    private void EnsureConfigured()
    {
        if (!options.IsConfigured) throw new InvalidOperationException("Facebook provider yapılandırılmamış.");
    }
}

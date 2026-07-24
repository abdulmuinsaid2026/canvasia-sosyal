using System.Net;
using System.Text.Json;
using CanvasiaSocial.Application.Social;

namespace CanvasiaSocial.Infrastructure.Social;

internal static class SocialHttp
{
    private static readonly HttpClient SensitiveQueryClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None
    }) { Timeout = TimeSpan.FromSeconds(120) };

    public static Task<HttpResponseMessage> SendSensitiveAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SensitiveQueryClient.SendAsync(request, cancellationToken);

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException(response, body);
        }
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new SocialPublisherException("Sosyal medya servisi geçersiz yanıt döndürdü.",
                SocialPublishFailureKind.Transient, (int)response.StatusCode, innerException: exception);
        }
    }

    public static SocialPublisherException CreateException(HttpResponseMessage response, string body)
    {
        string? code = null;
        string? safeMessage = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object)
                {
                    code = error.TryGetProperty("code", out var errorCode) ? errorCode.ToString() : null;
                    safeMessage = error.TryGetProperty("error_user_msg", out var userMessage)
                        ? userMessage.GetString()
                        : error.TryGetProperty("message", out var message) ? message.GetString() : null;
                }
                else if (error.ValueKind == JsonValueKind.String)
                {
                    code = error.GetString();
                    safeMessage = root.TryGetProperty("error_description", out var description) ? description.GetString() : null;
                }
            }
        }
        catch (JsonException)
        {
            // Provider HTML and proxy errors are intentionally not persisted.
        }

        var status = response.StatusCode;
        var graphCode = int.TryParse(code, out var parsed) ? parsed : 0;
        var kind = status switch
        {
            HttpStatusCode.Unauthorized => SocialPublishFailureKind.Unauthorized,
            HttpStatusCode.Forbidden => SocialPublishFailureKind.Forbidden,
            (HttpStatusCode)429 => SocialPublishFailureKind.RateLimited,
            _ when (int)status >= 500 => SocialPublishFailureKind.Transient,
            _ when graphCode == 190 => SocialPublishFailureKind.Unauthorized,
            _ when graphCode is 4 or 17 or 32 or 613 => SocialPublishFailureKind.RateLimited,
            _ when graphCode is 1 or 2 => SocialPublishFailureKind.Transient,
            _ => SocialPublishFailureKind.Permanent
        };
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var messageText = string.IsNullOrWhiteSpace(safeMessage)
            ? "Sosyal medya API isteği başarısız oldu."
            : safeMessage.Length > 1000 ? safeMessage[..1000] : safeMessage;
        return new SocialPublisherException(messageText, kind, (int)status, code, retryAfter,
            JsonSerializer.Serialize(new { status = (int)status, code }));
    }

    public static HttpRequestMessage Bearer(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

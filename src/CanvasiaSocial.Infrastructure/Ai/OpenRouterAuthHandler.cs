using System.Net.Http.Headers;

namespace CanvasiaSocial.Infrastructure.Ai;

public sealed class OpenRouterAuthHandler(OpenRouterOptions options) : DelegatingHandler
{
    public const string AuthorizationHeader = "Authorization";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("OPENROUTER_API_KEY yapılandırılmamış.");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://canvasia.social");
        request.Headers.TryAddWithoutValidation("X-Title", "CanvasiaSocial");
        return base.SendAsync(request, cancellationToken);
    }
}

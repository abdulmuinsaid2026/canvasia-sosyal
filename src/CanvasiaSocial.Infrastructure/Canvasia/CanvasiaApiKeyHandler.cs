namespace CanvasiaSocial.Infrastructure.Canvasia;

public sealed class CanvasiaApiKeyHandler(CanvasiaOptions options) : DelegatingHandler
{
    public const string HeaderName = "X-Canvasia-Social-Key";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (options.IsApiKeyConfigured)
        {
            request.Headers.Remove(HeaderName);
            request.Headers.TryAddWithoutValidation(HeaderName, options.ApiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

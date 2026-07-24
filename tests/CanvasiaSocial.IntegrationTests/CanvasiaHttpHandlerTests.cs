using System.Net;
using CanvasiaSocial.Infrastructure.Canvasia;

namespace CanvasiaSocial.IntegrationTests;

public sealed class CanvasiaHttpHandlerTests
{
    [Fact]
    public async Task Api_key_handler_sends_configured_header()
    {
        const string secret = "test-secret-not-for-logs";
        string? receivedKey = null;
        var capture = new StubHandler((request, _) =>
        {
            receivedKey = request.Headers.GetValues(CanvasiaApiKeyHandler.HeaderName).Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        var handler = new CanvasiaApiKeyHandler(new CanvasiaOptions { ApiKey = secret }) { InnerHandler = capture };

        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("https://canvasia.test/api/canvasia-social/products");

        Assert.Equal(secret, receivedKey);
    }

    [Fact]
    public async Task Resilience_handler_retries_transient_response_and_recovers()
    {
        var calls = 0;
        var stub = new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(
                calls < 3 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));
        });
        var handler = new CanvasiaResilienceHandler(new CanvasiaOptions()) { InnerHandler = stub };

        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("https://canvasia.test/api/canvasia-social/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, calls);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}

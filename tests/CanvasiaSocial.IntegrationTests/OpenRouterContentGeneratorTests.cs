using System.Net;
using System.Net.Http.Headers;
using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Ai;

namespace CanvasiaSocial.IntegrationTests;

public sealed class OpenRouterContentGeneratorTests
{
    [Fact]
    public async Task Rate_limit_uses_retry_after_before_retrying()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) =>
        {
            calls++;
            if (calls == 1)
            {
                var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return Task.FromResult(limited);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"choices":[{"message":{"content":"{\"caption\":\"Hazır içerik\",\"hashtags\":[\"canvasia\"]}"}}]}
                    """)
            });
        });

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://openrouter.test/") };
        var generator = new OpenRouterContentGenerator(client, new OpenRouterOptions());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var result = await generator.GenerateAsync(new AiContentRequest(
            Guid.NewGuid(), "Ürün", null, 100, null, null, "https://canvasia.test/urun",
            Platform.Instagram, true, true), timeout.Token);

        Assert.Equal("Hazır içerik", result.Caption);
        Assert.Equal(2, calls);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}

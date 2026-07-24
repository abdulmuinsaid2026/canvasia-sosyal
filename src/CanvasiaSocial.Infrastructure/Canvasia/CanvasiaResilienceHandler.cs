using System.Net;

namespace CanvasiaSocial.Infrastructure.Canvasia;

public sealed class CanvasiaResilienceHandler(CanvasiaOptions options) : DelegatingHandler
{
    private const int RetryCount = 2;
    private const int CircuitFailureThreshold = 5;
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(30);
    private readonly object stateLock = new();
    private int consecutiveFailures;
    private DateTime circuitOpenUntilUtc;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ThrowIfCircuitOpen();

        for (var attempt = 0; attempt <= RetryCount; attempt++)
        {
            using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            attemptCancellation.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
            try
            {
                using var attemptRequest = await CloneAsync(request, attemptCancellation.Token);
                var response = await base.SendAsync(attemptRequest, attemptCancellation.Token);
                if (!IsTransient(response.StatusCode))
                {
                    ResetCircuit();
                    return response;
                }

                if (attempt == RetryCount)
                {
                    RegisterFailure();
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < RetryCount)
            {
                // Retry only transport failures; caller cancellation is never retried.
            }
            catch (HttpRequestException)
            {
                RegisterFailure();
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < RetryCount)
            {
                // HttpClient timeout is transient; explicit caller cancellation is not.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                RegisterFailure();
                throw;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)), cancellationToken);
        }

        throw new InvalidOperationException("Canvasia retry akışı beklenmeyen biçimde sonlandı.");
    }

    private void ThrowIfCircuitOpen()
    {
        lock (stateLock)
        {
            if (circuitOpenUntilUtc > DateTime.UtcNow)
            {
                throw new HttpRequestException("Canvasia API devre kesicisi geçici olarak açık.");
            }

            if (circuitOpenUntilUtc != default)
            {
                circuitOpenUntilUtc = default;
                consecutiveFailures = 0;
            }
        }
    }

    private void RegisterFailure()
    {
        lock (stateLock)
        {
            consecutiveFailures++;
            if (consecutiveFailures >= CircuitFailureThreshold)
            {
                circuitOpenUntilUtc = DateTime.UtcNow.Add(CircuitBreakDuration);
            }
        }
    }

    private void ResetCircuit()
    {
        lock (stateLock)
        {
            consecutiveFailures = 0;
            circuitOpenUntilUtc = default;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage source,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };

        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            var content = new MemoryStream();
            await source.Content.CopyToAsync(content, cancellationToken);
            content.Position = 0;
            clone.Content = new StreamContent(content);
            foreach (var header in source.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}

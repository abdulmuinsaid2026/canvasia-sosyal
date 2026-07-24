using CanvasiaSocial.Application.Canvasia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CanvasiaSocial.Infrastructure.Health;

public sealed class CanvasiaApiHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<ICanvasiaApiClient>();
        var result = await client.TestConnectionAsync(cancellationToken);
        return result.IsHealthy
            ? HealthCheckResult.Healthy(result.Message)
            : HealthCheckResult.Unhealthy(result.Message);
    }
}

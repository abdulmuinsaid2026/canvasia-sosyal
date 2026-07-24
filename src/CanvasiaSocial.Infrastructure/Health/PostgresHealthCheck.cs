using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CanvasiaSocial.Infrastructure.Health;

public sealed class PostgresHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL erişilebilir.")
                : HealthCheckResult.Unhealthy("PostgreSQL erişilemiyor.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("PostgreSQL sağlık kontrolü başarısız.");
        }
    }
}

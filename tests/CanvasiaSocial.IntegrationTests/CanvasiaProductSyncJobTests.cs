using System.Reflection;
using CanvasiaSocial.Infrastructure.Synchronization;
using Hangfire;

namespace CanvasiaSocial.IntegrationTests;

public sealed class CanvasiaProductSyncJobTests
{
    [Fact]
    public void Full_sync_job_disables_concurrent_execution()
    {
        var method = typeof(CanvasiaProductSyncJob).GetMethod(nameof(CanvasiaProductSyncJob.ExecuteAsync));

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<DisableConcurrentExecutionAttribute>());
        var retry = method.GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(retry);
        Assert.Equal(0, retry.Attempts);
    }
}

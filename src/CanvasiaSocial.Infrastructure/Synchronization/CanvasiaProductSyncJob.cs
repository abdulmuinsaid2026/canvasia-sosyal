using CanvasiaSocial.Application.Synchronization;
using Hangfire;

namespace CanvasiaSocial.Infrastructure.Synchronization;

public sealed class CanvasiaProductSyncJob(ICanvasiaProductSyncService syncService)
{
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await syncService.SynchronizeAsync(cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Error ?? "Canvasia ürün senkronizasyonu başarısız.");
        }
    }
}

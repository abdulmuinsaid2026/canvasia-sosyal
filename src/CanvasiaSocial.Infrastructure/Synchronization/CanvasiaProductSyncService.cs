using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Application.Synchronization;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Infrastructure.Canvasia;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanvasiaSocial.Infrastructure.Synchronization;

internal sealed class CanvasiaProductSyncService(
    ICanvasiaApiClient apiClient,
    ICanvasiaProductMapper mapper,
    IProductCacheService productCacheService,
    ApplicationDbContext dbContext,
    CanvasiaOptions options,
    ILogger<CanvasiaProductSyncService> logger) : ICanvasiaProductSyncService
{
    public async Task<CanvasiaSyncResult> SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        await UpdateStateAsync("Running", startedAtUtc, null, 0, null, null, cancellationToken);
        var processed = 0;
        int? sourceCount = null;

        try
        {
            for (var page = 1; ; page++)
            {
                var response = await apiClient.GetProductsAsync(page, options.PageSize, cancellationToken: cancellationToken);
                sourceCount = response.TotalItems;
                var mapped = response.Items.Select(mapper.Map).ToArray();
                await productCacheService.UpsertBatchAsync(mapped, cancellationToken);
                processed += mapped.Length;
                await UpdateStateAsync("Running", startedAtUtc, null, processed, sourceCount, null, cancellationToken);
                if (mapped.Length == 0 || page >= response.TotalPages) break;
            }

            var completedAtUtc = DateTime.UtcNow;
            await UpdateStateAsync("Succeeded", startedAtUtc, completedAtUtc, processed, sourceCount, null, cancellationToken, completedAtUtc);
            logger.LogInformation(
                "Canvasia ürün senkronizasyonu tamamlandı. İşlenen ürün: {ProcessedProductCount}, kaynak toplamı: {SourceProductCount}",
                processed, sourceCount);
            return new CanvasiaSyncResult(true, processed, sourceCount, startedAtUtc, completedAtUtc, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var completedAtUtc = DateTime.UtcNow;
            var safeError = SanitizeError(exception.Message);
            await UpdateStateAsync("Failed", startedAtUtc, completedAtUtc, processed, sourceCount, safeError, CancellationToken.None);
            logger.LogError(
                "Canvasia ürün senkronizasyonu başarısız. İşlenen ürün: {ProcessedProductCount}, hata: {SafeError}",
                processed,
                safeError);
            return new CanvasiaSyncResult(false, processed, sourceCount, startedAtUtc, completedAtUtc, safeError);
        }
    }

    public async Task<CanvasiaSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var state = await dbContext.CanvasiaSyncStates.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        return state is null
            ? new CanvasiaSyncStatus(null, null, null, "NeverRun", 0, null, null)
            : new CanvasiaSyncStatus(state.LastStartedAtUtc, state.LastCompletedAtUtc, state.LastSuccessfulAtUtc,
                state.Status, state.ProcessedProductCount, state.SourceProductCount, state.LastError);
    }

    private async Task UpdateStateAsync(
        string status, DateTime startedAtUtc, DateTime? completedAtUtc, int processed,
        int? sourceCount, string? error, CancellationToken cancellationToken, DateTime? successfulAtUtc = null)
    {
        var state = await dbContext.CanvasiaSyncStates.OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            state = new CanvasiaSyncState();
            dbContext.CanvasiaSyncStates.Add(state);
        }
        state.Status = status;
        state.LastStartedAtUtc = startedAtUtc;
        state.LastCompletedAtUtc = completedAtUtc;
        state.ProcessedProductCount = processed;
        state.SourceProductCount = sourceCount;
        state.LastError = error;
        if (successfulAtUtc.HasValue) state.LastSuccessfulAtUtc = successfulAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string SanitizeError(string error)
    {
        var sanitized = options.IsApiKeyConfigured
            ? error.Replace(options.ApiKey, "[REDACTED]", StringComparison.Ordinal)
            : error;
        return sanitized.Length <= 4000 ? sanitized : sanitized[..4000];
    }
}

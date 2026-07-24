namespace CanvasiaSocial.Application.Synchronization;

public sealed record CanvasiaSyncResult(
    bool Succeeded,
    int ProcessedProductCount,
    int? SourceProductCount,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string? Error);

public sealed record CanvasiaSyncStatus(
    DateTime? LastStartedAtUtc,
    DateTime? LastCompletedAtUtc,
    DateTime? LastSuccessfulAtUtc,
    string Status,
    int ProcessedProductCount,
    int? SourceProductCount,
    string? LastError);

public interface ICanvasiaProductSyncService
{
    Task<CanvasiaSyncResult> SynchronizeAsync(CancellationToken cancellationToken = default);
    Task<CanvasiaSyncStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

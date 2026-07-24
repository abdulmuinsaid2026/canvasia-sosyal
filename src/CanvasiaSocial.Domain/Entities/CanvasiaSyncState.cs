using CanvasiaSocial.Domain.Common;

namespace CanvasiaSocial.Domain.Entities;

public sealed class CanvasiaSyncState : Entity
{
    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastCompletedAtUtc { get; set; }
    public DateTime? LastSuccessfulAtUtc { get; set; }
    public string Status { get; set; } = "NeverRun";
    public int ProcessedProductCount { get; set; }
    public int? SourceProductCount { get; set; }
    public string? LastError { get; set; }
}

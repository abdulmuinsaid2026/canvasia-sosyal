using CanvasiaSocial.Domain.Common;

namespace CanvasiaSocial.Domain.Entities;

public sealed class PublishAttempt : Entity
{
    public Guid ScheduledPostId { get; set; }
    public ScheduledPost ScheduledPost { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool Success { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? PlatformErrorCode { get; set; }
    public string? SanitizedRequest { get; set; }
    public string? SanitizedResponse { get; set; }
    public string? ErrorMessage { get; set; }
}

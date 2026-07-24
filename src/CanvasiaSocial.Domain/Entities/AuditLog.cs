using CanvasiaSocial.Domain.Common;

namespace CanvasiaSocial.Domain.Entities;

public sealed class AuditLog : Entity
{
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? SanitizedDetails { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class OAuthState : Entity
{
    public string StateHash { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? EncryptedCodeVerifier { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

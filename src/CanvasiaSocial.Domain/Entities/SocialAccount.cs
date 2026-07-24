using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class SocialAccount : Entity
{
    public Platform Platform { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ExternalAccountId { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string? EncryptedRefreshToken { get; set; }
    public DateTime? TokenExpiresAtUtc { get; set; }
    public string? Scopes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastValidatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

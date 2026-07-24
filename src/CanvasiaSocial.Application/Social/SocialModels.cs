using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Application.Social;

public sealed record SocialProviderConfiguration(
    Platform Platform,
    bool Enabled,
    bool IsConfigured,
    string RedirectUri,
    IReadOnlyList<string> Scopes);

public sealed record SocialOAuthCallback(string Code, string? CodeVerifier);

public sealed record SocialConnection(
    string ExternalAccountId,
    string DisplayName,
    string? Username,
    string? ProfileImageUrl,
    string AccessToken,
    string? RefreshToken,
    DateTime? TokenExpiresAtUtc,
    IReadOnlyList<string> Scopes);

public sealed record SocialAccountCredentials(
    string ExternalAccountId,
    string AccessToken,
    string? RefreshToken,
    DateTime? TokenExpiresAtUtc,
    IReadOnlyList<string> Scopes);

public sealed record SocialPostRequest(
    string Caption,
    Uri ImageUrl,
    string IdempotencyKey);

public sealed record SocialValidationResult(bool IsValid, string? Error = null);

public sealed record SocialPublishResult(string ExternalPostId, string? ExternalPostUrl, string SanitizedResponse);

public sealed record SocialRefreshResult(
    string AccessToken,
    string? RefreshToken,
    DateTime? TokenExpiresAtUtc,
    IReadOnlyList<string> Scopes);

public enum SocialPublishFailureKind
{
    InvalidContent,
    Unauthorized,
    Forbidden,
    RateLimited,
    Transient,
    Permanent
}

public sealed class SocialPublisherException(
    string message,
    SocialPublishFailureKind kind,
    int? httpStatusCode = null,
    string? providerErrorCode = null,
    TimeSpan? retryAfter = null,
    string? sanitizedResponse = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public SocialPublishFailureKind Kind { get; } = kind;
    public int? HttpStatusCode { get; } = httpStatusCode;
    public string? ProviderErrorCode { get; } = providerErrorCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public string? SanitizedResponse { get; } = sanitizedResponse;
}

public sealed record SocialAccountSummary(
    Guid Id,
    Platform Platform,
    string DisplayName,
    string? Username,
    string Status,
    DateTime? TokenExpiresAtUtc,
    IReadOnlyList<string> Scopes,
    DateTime? LastValidatedAtUtc);

public sealed record SocialPlatformCard(
    Platform Platform,
    bool Enabled,
    bool IsConfigured,
    string RedirectUri,
    IReadOnlyList<string> RequiredScopes,
    IReadOnlyList<SocialAccountSummary> Accounts);

public sealed record OAuthStartResult(Uri AuthorizationUrl);

public sealed record SocialOperationResult(bool Succeeded, string Message);

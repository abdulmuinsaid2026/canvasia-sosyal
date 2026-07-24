using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Application.Social;

public interface ISocialPublisher
{
    Platform Platform { get; }
    SocialProviderConfiguration Configuration { get; }
    Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default);
    Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default);
    Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default);
    Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default);
    Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default);
    Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default);
    Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default);
}

public interface ISocialAccountService
{
    Task<IReadOnlyList<SocialPlatformCard>> GetCardsAsync(CancellationToken cancellationToken = default);
    Task<OAuthStartResult> BeginAuthorizationAsync(Platform platform, string userId, CancellationToken cancellationToken = default);
    Task<SocialOperationResult> CompleteAuthorizationAsync(Platform platform, string state, string? code, string? error, string userId, CancellationToken cancellationToken = default);
    Task<SocialOperationResult> ValidateAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<SocialOperationResult> RefreshAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<SocialOperationResult> DisconnectAsync(Guid accountId, CancellationToken cancellationToken = default);
}

public interface ISocialTokenProtector
{
    string Protect(string token);
    string Unprotect(string protectedToken);
}

public interface ISecureImageService
{
    Task<Uri> ValidateAndPrepareAsync(string imageUrl, CancellationToken cancellationToken = default);
}

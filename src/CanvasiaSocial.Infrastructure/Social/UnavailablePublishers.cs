using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Infrastructure.Social;

public abstract class UnavailableSocialPublisher(
    Platform platform,
    IReadOnlyDictionary<Platform, SocialProviderOptions> allOptions) : ISocialPublisher
{
    private readonly SocialProviderOptions options = allOptions[platform];
    public Platform Platform { get; } = platform;
    public SocialProviderConfiguration Configuration => options.ToConfiguration();

    public Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default) =>
        throw NotAvailable();
    public Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default) =>
        throw NotAvailable();
    public Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SocialValidationResult(false, $"{Platform} provider henüz etkin değil."));
    public Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SocialValidationResult(false, $"{Platform} yayın desteği henüz etkin değil."));
    public Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default) =>
        throw NotAvailable();
    public Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) =>
        throw NotAvailable();
    public Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private InvalidOperationException NotAvailable() => new($"{Platform} provider yapılandırılmamış veya yayın için etkin değil.");
}

public sealed class TikTokPublisher(IReadOnlyDictionary<Platform, SocialProviderOptions> options)
    : UnavailableSocialPublisher(Platform.TikTok, options);

public sealed class PinterestPublisher(IReadOnlyDictionary<Platform, SocialProviderOptions> options)
    : UnavailableSocialPublisher(Platform.Pinterest, options);

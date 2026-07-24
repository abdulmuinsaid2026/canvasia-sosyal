using System.Text.Json;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using CanvasiaSocial.Infrastructure.Social;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.IntegrationTests;

public sealed class SocialAccountSecurityTests
{
    [Fact]
    public async Task OAuth_state_is_single_use_and_tokens_are_encrypted_and_masked()
    {
        await using var db = Context();
        var protector = Protector();
        var provider = new OAuthFakePublisher(Platform.Instagram, configured: true);
        var service = new SocialAccountService(db, Providers(provider), protector);

        var start = await service.BeginAuthorizationAsync(Platform.Instagram, "admin");
        var state = QueryValue(start.AuthorizationUrl, "state");
        var completed = await service.CompleteAuthorizationAsync(Platform.Instagram, state, "valid-code", null, "admin");

        Assert.True(completed.Succeeded);
        var account = Assert.Single(await db.SocialAccounts.ToListAsync());
        Assert.NotEqual(provider.AccessToken, account.EncryptedAccessToken);
        Assert.Equal(provider.AccessToken, protector.Unprotect(account.EncryptedAccessToken));
        var cardsJson = JsonSerializer.Serialize(await service.GetCardsAsync());
        Assert.DoesNotContain(provider.AccessToken, cardsJson, StringComparison.Ordinal);
        var replay = await service.CompleteAuthorizationAsync(Platform.Instagram, state, "valid-code", null, "admin");
        Assert.False(replay.Succeeded);
    }

    [Fact]
    public async Task Invalid_callback_and_disabled_provider_fail_safely()
    {
        await using var db = Context();
        var service = new SocialAccountService(db, Providers(new OAuthFakePublisher(Platform.Instagram, true)), Protector());

        var invalid = await service.CompleteAuthorizationAsync(Platform.Instagram, "invalid", null, "access_denied", "admin");

        Assert.False(invalid.Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BeginAuthorizationAsync(Platform.TikTok, "admin"));
    }

    private static IEnumerable<ISocialPublisher> Providers(ISocialPublisher instagram) =>
    [
        instagram,
        new OAuthFakePublisher(Platform.Facebook, false),
        new OAuthFakePublisher(Platform.TikTok, false),
        new OAuthFakePublisher(Platform.Pinterest, false)
    ];

    private static string QueryValue(Uri uri, string name) => uri.Query.TrimStart('?').Split('&')
        .Select(x => x.Split('=', 2)).Where(x => x.Length == 2)
        .Where(x => Uri.UnescapeDataString(x[0]) == name).Select(x => Uri.UnescapeDataString(x[1])).Single();
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ISocialTokenProtector Protector() => new SocialTokenProtector(
        DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Path.GetTempPath(), $"canvasia-test-{Guid.NewGuid():N}"))));

    private sealed class OAuthFakePublisher(Platform platform, bool configured) : ISocialPublisher
    {
        public string AccessToken { get; } = $"secret-{Guid.NewGuid():N}";
        public Platform Platform { get; } = platform;
        public SocialProviderConfiguration Configuration { get; } = new(platform, configured, configured,
            "https://example.test/callback", ["scope"]);
        public Task<Uri> CreateAuthorizationUrlAsync(string state, string? codeChallenge, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Uri($"https://example.test/oauth?state={Uri.EscapeDataString(state)}"));
        public Task<SocialConnection> HandleCallbackAsync(SocialOAuthCallback callback, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SocialConnection("external-1", "Test Account", "test", null, AccessToken, "refresh-secret",
                DateTime.UtcNow.AddHours(1), ["scope"]));
        public Task<SocialValidationResult> ValidateAccountAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.FromResult(new SocialValidationResult(true));
        public Task<SocialValidationResult> ValidatePostAsync(SocialPostRequest post, CancellationToken cancellationToken = default) => Task.FromResult(new SocialValidationResult(true));
        public Task<SocialPublishResult> PublishAsync(SocialAccountCredentials account, SocialPostRequest post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SocialRefreshResult> RefreshTokenAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DisconnectAsync(SocialAccountCredentials account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

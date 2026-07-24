using System.Security.Cryptography;
using System.Text;
using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class SocialAccountService(
    ApplicationDbContext dbContext,
    IEnumerable<ISocialPublisher> publishers,
    ISocialTokenProtector tokenProtector) : ISocialAccountService
{
    private readonly IReadOnlyDictionary<Platform, ISocialPublisher> providers = publishers.ToDictionary(x => x.Platform);

    public async Task<IReadOnlyList<SocialPlatformCard>> GetCardsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.SocialAccounts.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);
        return Enum.GetValues<Platform>().Select(platform =>
        {
            var provider = providers[platform];
            var configuration = provider.Configuration;
            var platformAccounts = accounts.Where(x => x.Platform == platform).Select(x => new SocialAccountSummary(
                x.Id, x.Platform, x.DisplayName, x.Username, x.Status, x.TokenExpiresAtUtc,
                SplitScopes(x.Scopes), x.LastValidatedAtUtc)).ToArray();
            return new SocialPlatformCard(platform, configuration.Enabled, configuration.IsConfigured,
                configuration.RedirectUri, configuration.Scopes, platformAccounts);
        }).ToArray();
    }

    public async Task<OAuthStartResult> BeginAuthorizationAsync(Platform platform, string userId, CancellationToken cancellationToken = default)
    {
        var provider = providers[platform];
        if (!provider.Configuration.IsConfigured) throw new InvalidOperationException($"{platform} provider yapılandırılmamış.");

        var state = Base64Url(RandomNumberGenerator.GetBytes(32));
        dbContext.OAuthStates.Add(new OAuthState
        {
            StateHash = Hash(state),
            Platform = platform,
            UserId = userId,
            EncryptedCodeVerifier = null,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new OAuthStartResult(await provider.CreateAuthorizationUrlAsync(state, null, cancellationToken));
    }

    public async Task<SocialOperationResult> CompleteAuthorizationAsync(
        Platform platform,
        string state,
        string? code,
        string? error,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(state)) return Failure("OAuth state doğrulanamadı. Bağlantıyı yeniden başlatın.");
        var stateHash = Hash(state);
        var oauthState = await dbContext.OAuthStates.AsNoTracking().FirstOrDefaultAsync(x => x.StateHash == stateHash, cancellationToken);
        if (oauthState is null || oauthState.Platform != platform || oauthState.UserId != userId ||
            oauthState.ConsumedAtUtc.HasValue || oauthState.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Failure("OAuth isteği geçersiz, süresi dolmuş veya daha önce kullanılmış.");
        }
        var consumedAt = DateTime.UtcNow;
        if (dbContext.Database.IsRelational())
        {
            var affected = await dbContext.OAuthStates.Where(x => x.Id == oauthState.Id && !x.ConsumedAtUtc.HasValue && x.ExpiresAtUtc > consumedAt)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.ConsumedAtUtc, consumedAt), cancellationToken);
            if (affected != 1) return Failure("OAuth isteği geçersiz, süresi dolmuş veya daha önce kullanılmış.");
        }
        else
        {
            var tracked = await dbContext.OAuthStates.FirstAsync(x => x.Id == oauthState.Id, cancellationToken);
            if (tracked.ConsumedAtUtc.HasValue) return Failure("OAuth isteği geçersiz, süresi dolmuş veya daha önce kullanılmış.");
            tracked.ConsumedAtUtc = consumedAt;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(error)) return Failure("Sosyal medya yetkilendirmesi kullanıcı veya sağlayıcı tarafından iptal edildi.");
        if (string.IsNullOrWhiteSpace(code)) return Failure("Sosyal medya sağlayıcısı yetkilendirme kodu döndürmedi.");

        try
        {
            var verifier = string.IsNullOrWhiteSpace(oauthState.EncryptedCodeVerifier)
                ? null : tokenProtector.Unprotect(oauthState.EncryptedCodeVerifier);
            var connection = await providers[platform].HandleCallbackAsync(new SocialOAuthCallback(code, verifier), cancellationToken);
            var account = await dbContext.SocialAccounts.FirstOrDefaultAsync(
                x => x.Platform == platform && x.ExternalAccountId == connection.ExternalAccountId, cancellationToken);
            if (account is null)
            {
                account = new SocialAccount { Platform = platform, ExternalAccountId = connection.ExternalAccountId };
                dbContext.SocialAccounts.Add(account);
            }
            account.DisplayName = connection.DisplayName;
            account.Username = connection.Username;
            account.ProfileImageUrl = connection.ProfileImageUrl;
            account.EncryptedAccessToken = tokenProtector.Protect(connection.AccessToken);
            account.EncryptedRefreshToken = connection.RefreshToken is null ? null : tokenProtector.Protect(connection.RefreshToken);
            account.TokenExpiresAtUtc = connection.TokenExpiresAtUtc;
            account.Scopes = string.Join(' ', connection.Scopes.Distinct(StringComparer.Ordinal));
            account.Status = "Active";
            account.LastValidatedAtUtc = DateTime.UtcNow;
            account.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Success($"{platform} hesabı güvenli biçimde bağlandı.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(SafeProviderMessage(platform, exception));
        }
    }

    public async Task<SocialOperationResult> ValidateAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await FindAsync(accountId, cancellationToken);
        try
        {
            var result = await providers[account.Platform].ValidateAccountAsync(Credentials(account), cancellationToken);
            account.Status = result.IsValid ? "Active" : "ReconnectRequired";
            account.LastValidatedAtUtc = DateTime.UtcNow;
            account.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return result.IsValid ? Success("Hesap bağlantısı geçerli.") : Failure(result.Error ?? "Hesap bağlantısı geçersiz.");
        }
        catch (Exception exception) when (exception is HttpRequestException or SocialPublisherException or InvalidOperationException)
        {
            account.Status = "ReconnectRequired";
            account.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Failure(SafeProviderMessage(account.Platform, exception));
        }
    }

    public async Task<SocialOperationResult> RefreshAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await FindAsync(accountId, cancellationToken);
        try
        {
            var refreshed = await providers[account.Platform].RefreshTokenAsync(Credentials(account), cancellationToken);
            account.EncryptedAccessToken = tokenProtector.Protect(refreshed.AccessToken);
            account.EncryptedRefreshToken = refreshed.RefreshToken is null ? account.EncryptedRefreshToken : tokenProtector.Protect(refreshed.RefreshToken);
            account.TokenExpiresAtUtc = refreshed.TokenExpiresAtUtc;
            account.Scopes = string.Join(' ', refreshed.Scopes);
            account.Status = "Active";
            account.LastValidatedAtUtc = DateTime.UtcNow;
            account.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Success("Bağlantı anahtarı yenilendi.");
        }
        catch (Exception exception) when (exception is HttpRequestException or SocialPublisherException or InvalidOperationException)
        {
            account.Status = "ReconnectRequired";
            account.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Failure(SafeProviderMessage(account.Platform, exception));
        }
    }

    public async Task<SocialOperationResult> DisconnectAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await FindAsync(accountId, cancellationToken);
        try
        {
            await providers[account.Platform].DisconnectAsync(Credentials(account), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or SocialPublisherException or InvalidOperationException)
        {
            // Local token removal must still succeed when provider revocation is unavailable.
        }
        account.EncryptedAccessToken = string.Empty;
        account.EncryptedRefreshToken = null;
        account.TokenExpiresAtUtc = null;
        account.Status = "Disconnected";
        account.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success("Sosyal medya bağlantısı kaldırıldı ve yerel tokenlar silindi.");
    }

    private async Task<SocialAccount> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.SocialAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("Sosyal medya hesabı bulunamadı.");

    private SocialAccountCredentials Credentials(SocialAccount account) => new(account.ExternalAccountId,
        tokenProtector.Unprotect(account.EncryptedAccessToken),
        account.EncryptedRefreshToken is null ? null : tokenProtector.Unprotect(account.EncryptedRefreshToken),
        account.TokenExpiresAtUtc, SplitScopes(account.Scopes));

    private static IReadOnlyList<string> SplitScopes(string? scopes) => string.IsNullOrWhiteSpace(scopes)
        ? [] : scopes.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static SocialOperationResult Success(string message) => new(true, message);
    private static SocialOperationResult Failure(string message) => new(false, message);
    private static string SafeProviderMessage(Platform platform, Exception exception) => exception is SocialPublisherException social
        ? $"{platform} bağlantısı tamamlanamadı: {social.Message}"
        : $"{platform} bağlantısı tamamlanamadı. Yapılandırmayı ve sağlayıcı izinlerini kontrol edin.";
}

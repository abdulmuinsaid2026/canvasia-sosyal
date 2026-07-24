using System.Globalization;
using Microsoft.AspNetCore.DataProtection;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class SocialImageTokenService(IDataProtectionProvider dataProtectionProvider) : Application.Social.ISocialImageTokenService
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("CanvasiaSocial.InstagramImage.v1");

    public Uri CreateInstagramJpegUrl(Guid scheduledPostId, string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var callback))
            throw new InvalidOperationException("Instagram geri dönüş adresi geçersiz.");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var token = protector.Protect($"{scheduledPostId:N}|{expiresAt.ToString(CultureInfo.InvariantCulture)}");
        var path = $"/SocialMedia/Images/{scheduledPostId:N}.jpg";
        return new UriBuilder(callback.Scheme, callback.Host, callback.IsDefaultPort ? -1 : callback.Port, path)
        {
            Query = "token=" + Uri.EscapeDataString(token)
        }.Uri;
    }

    public bool IsValidInstagramJpegToken(Guid scheduledPostId, string token)
    {
        try
        {
            var parts = protector.Unprotect(token).Split('|');
            return parts.Length == 2 && parts[0] == scheduledPostId.ToString("N") &&
                   long.TryParse(parts[1], CultureInfo.InvariantCulture, out var expiresAt) &&
                   expiresAt >= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}

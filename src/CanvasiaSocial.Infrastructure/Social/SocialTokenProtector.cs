using CanvasiaSocial.Application.Social;
using Microsoft.AspNetCore.DataProtection;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class SocialTokenProtector(IDataProtectionProvider provider) : ISocialTokenProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("CanvasiaSocial.SocialTokens.v1");
    public string Protect(string token) => protector.Protect(token);
    public string Unprotect(string protectedToken) => protector.Unprotect(protectedToken);
}

using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class SocialProviderOptions
{
    public required Platform Platform { get; init; }
    public bool Enabled { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = [];
    public required string ApiBaseUrl { get; init; }
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) && IsSafeRedirect(RedirectUri) && IsOfficialApiHost();

    public SocialProviderConfiguration ToConfiguration() =>
        new(Platform, Enabled, IsConfigured, RedirectUri, Scopes);

    public static IReadOnlyDictionary<Platform, SocialProviderOptions> FromConfiguration(IConfiguration configuration) =>
        Enum.GetValues<Platform>().ToDictionary(platform => platform, platform => Create(configuration, platform));

    private static SocialProviderOptions Create(IConfiguration configuration, Platform platform)
    {
        var prefix = platform.ToString().ToUpperInvariant();
        var defaults = platform switch
        {
            Platform.Instagram => ("https://graph.instagram.com/v25.0/", "instagram_business_basic,instagram_business_content_publish"),
            Platform.Facebook => ("https://graph.facebook.com/v25.0/", "pages_show_list,pages_read_engagement,pages_manage_posts"),
            Platform.TikTok => ("https://open.tiktokapis.com/v2/", "user.info.basic,video.publish"),
            Platform.Pinterest => ("https://api.pinterest.com/v5/", "user_accounts:read,boards:read,pins:read,pins:write"),
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        return new SocialProviderOptions
        {
            Platform = platform,
            Enabled = bool.TryParse(configuration[$"{prefix}_ENABLED"], out var enabled) && enabled,
            ClientId = configuration[$"{prefix}_CLIENT_ID"] ?? string.Empty,
            ClientSecret = configuration[$"{prefix}_CLIENT_SECRET"] ?? string.Empty,
            RedirectUri = configuration[$"{prefix}_REDIRECT_URI"] ?? string.Empty,
            Scopes = (configuration[$"{prefix}_SCOPES"] ?? defaults.Item2)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ApiBaseUrl = configuration[$"{prefix}_API_BASE_URL"] ?? defaults.Item1
        };
    }

    private bool IsOfficialApiHost()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        var expected = Platform switch
        {
            Platform.Instagram => "graph.instagram.com",
            Platform.Facebook => "graph.facebook.com",
            Platform.TikTok => "open.tiktokapis.com",
            Platform.Pinterest => "api.pinterest.com",
            _ => string.Empty
        };
        return uri.Host.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeRedirect(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
    }
}

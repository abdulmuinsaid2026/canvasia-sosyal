using CanvasiaSocial.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace CanvasiaSocial.Infrastructure.Campaigns;

public sealed class CampaignOptions
{
    public int BatchMaxProducts { get; init; } = 100;
    public int AiBatchConcurrency { get; init; } = 1;
    public int DefaultPostIntervalMinutes { get; init; } = 60;
    public int InstagramDailyLimit { get; init; } = 10;
    public int FacebookDailyLimit { get; init; } = 10;
    public int TikTokDailyLimit { get; init; } = 5;
    public int PinterestDailyLimit { get; init; } = 20;
    public bool RequireApproval { get; init; } = true;
    public string DefaultTimeZone { get; init; } = "Europe/Istanbul";
    public bool AutoPublishEnabled { get; init; }
    public int PublishMaxRetryCount { get; init; } = 3;

    public int GetPlatformDailyLimit(Platform platform) => platform switch
    {
        Platform.Instagram => InstagramDailyLimit,
        Platform.Facebook => FacebookDailyLimit,
        Platform.TikTok => TikTokDailyLimit,
        Platform.Pinterest => PinterestDailyLimit,
        _ => 1
    };

    public static CampaignOptions FromConfiguration(IConfiguration configuration) => new()
    {
        BatchMaxProducts = ReadInt(configuration, "CANVASIA_BATCH_MAX_PRODUCTS", 100, 1, 100),
        AiBatchConcurrency = ReadInt(configuration, "CANVASIA_AI_BATCH_CONCURRENCY", 1, 1, 10),
        DefaultPostIntervalMinutes = ReadInt(configuration, "CANVASIA_DEFAULT_POST_INTERVAL_MINUTES", 60, 1, 1440),
        InstagramDailyLimit = ReadInt(configuration, "CANVASIA_INSTAGRAM_DAILY_LIMIT", 10, 1, 100),
        FacebookDailyLimit = ReadInt(configuration, "CANVASIA_FACEBOOK_DAILY_LIMIT", 10, 1, 100),
        TikTokDailyLimit = ReadInt(configuration, "CANVASIA_TIKTOK_DAILY_LIMIT", 5, 1, 100),
        PinterestDailyLimit = ReadInt(configuration, "CANVASIA_PINTEREST_DAILY_LIMIT", 20, 1, 100),
        RequireApproval = ReadBool(configuration, "CANVASIA_REQUIRE_APPROVAL", true),
        DefaultTimeZone = configuration["DEFAULT_TIME_ZONE"] ?? "Europe/Istanbul",
        AutoPublishEnabled = ReadBool(configuration, "AUTO_PUBLISH_ENABLED", false),
        PublishMaxRetryCount = ReadInt(configuration, "PUBLISH_MAX_RETRY_COUNT", 3, 0, 10)
    };

    private static int ReadInt(IConfiguration configuration, string key, int fallback, int minimum, int maximum) =>
        int.TryParse(configuration[key], out var value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static bool ReadBool(IConfiguration configuration, string key, bool fallback) =>
        bool.TryParse(configuration[key], out var value) ? value : fallback;
}

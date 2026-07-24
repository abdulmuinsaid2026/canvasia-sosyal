using Microsoft.Extensions.Configuration;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class SecureImageOptions
{
    public string[] AllowedHosts { get; init; } = ["canvasia.com.tr"];
    public long MaxBytes { get; init; } = 10 * 1024 * 1024;
    public int TimeoutSeconds { get; init; } = 20;
    public int MaxRedirects { get; init; } = 3;

    public static SecureImageOptions FromConfiguration(IConfiguration configuration) => new()
    {
        AllowedHosts = (configuration["CANVASIA_ALLOWED_IMAGE_HOSTS"] ?? "canvasia.com.tr")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.TrimEnd('.').ToLowerInvariant()).Distinct().ToArray(),
        MaxBytes = int.TryParse(configuration["SOCIAL_IMAGE_MAX_BYTES"], out var bytes)
            ? Math.Clamp(bytes, 1024, 25 * 1024 * 1024) : 10 * 1024 * 1024,
        TimeoutSeconds = int.TryParse(configuration["SOCIAL_IMAGE_TIMEOUT_SECONDS"], out var timeout)
            ? Math.Clamp(timeout, 1, 120) : 20
    };
}

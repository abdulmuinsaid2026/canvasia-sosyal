using Microsoft.Extensions.Configuration;

namespace CanvasiaSocial.Infrastructure.Canvasia;

public sealed class CanvasiaOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public int PageSize { get; init; } = 100;
    public string SyncCron { get; init; } = "0 */6 * * *";
    public int RequestTimeoutSeconds { get; init; } = 30;

    public bool HasValidBaseUrl =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public bool IsApiKeyConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public static CanvasiaOptions FromConfiguration(IConfiguration configuration)
    {
        return new CanvasiaOptions
        {
            BaseUrl = Read(configuration, "CANVASIA_API_BASE_URL", "Canvasia:BaseUrl"),
            ApiKey = Read(configuration, "CANVASIA_API_KEY", "Canvasia:ApiKey"),
            PageSize = ReadInt(configuration, "CANVASIA_PRODUCTS_PAGE_SIZE", "Canvasia:ProductsPageSize", 100, 1, 100),
            SyncCron = Read(configuration, "CANVASIA_SYNC_CRON", "Canvasia:SyncCron", "0 */6 * * *"),
            RequestTimeoutSeconds = ReadInt(configuration, "CANVASIA_REQUEST_TIMEOUT_SECONDS", "Canvasia:RequestTimeoutSeconds", 30, 1, 300)
        };
    }

    private static string Read(IConfiguration configuration, string environmentName, string sectionName, string fallback = "") =>
        configuration[environmentName] ?? configuration[sectionName] ?? fallback;

    private static int ReadInt(
        IConfiguration configuration,
        string environmentName,
        string sectionName,
        int fallback,
        int minimum,
        int maximum) =>
        int.TryParse(configuration[environmentName] ?? configuration[sectionName], out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
}

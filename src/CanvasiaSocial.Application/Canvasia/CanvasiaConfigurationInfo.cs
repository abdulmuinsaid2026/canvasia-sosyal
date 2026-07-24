namespace CanvasiaSocial.Application.Canvasia;

public sealed record CanvasiaConfigurationInfo(
    string BaseUrl,
    bool IsApiKeyConfigured,
    int PageSize,
    string SyncCron,
    int RequestTimeoutSeconds);

public interface ICanvasiaConfigurationService
{
    CanvasiaConfigurationInfo GetInfo();
}

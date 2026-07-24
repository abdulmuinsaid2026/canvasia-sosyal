using CanvasiaSocial.Application.Canvasia;

namespace CanvasiaSocial.Infrastructure.Canvasia;

internal sealed class CanvasiaConfigurationService(CanvasiaOptions options) : ICanvasiaConfigurationService
{
    public CanvasiaConfigurationInfo GetInfo() => new(
        options.BaseUrl,
        options.IsApiKeyConfigured,
        options.PageSize,
        options.SyncCron,
        options.RequestTimeoutSeconds);
}

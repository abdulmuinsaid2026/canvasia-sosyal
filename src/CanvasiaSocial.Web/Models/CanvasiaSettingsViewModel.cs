using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Application.Synchronization;

namespace CanvasiaSocial.Web.Models;

public sealed record CanvasiaSettingsViewModel(
    CanvasiaConfigurationInfo Configuration,
    CanvasiaSyncStatus SyncStatus,
    int CachedProductCount,
    string? ResultMessage,
    bool? ResultSucceeded);

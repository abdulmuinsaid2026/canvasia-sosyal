using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Application.Synchronization;
using CanvasiaSocial.Infrastructure.Synchronization;
using CanvasiaSocial.Web.Models;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
[Route("Settings/Canvasia")]
public sealed class CanvasiaSettingsController(
    ICanvasiaConfigurationService configurationService,
    ICanvasiaApiClient apiClient,
    ICanvasiaProductSyncService syncService,
    IProductCacheService productCacheService,
    IBackgroundJobClient backgroundJobs) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new CanvasiaSettingsViewModel(
            configurationService.GetInfo(),
            await syncService.GetStatusAsync(cancellationToken),
            await productCacheService.CountAsync(cancellationToken),
            TempData["ResultMessage"] as string,
            TempData["ResultSucceeded"] as bool?);
        return View(model);
    }

    [HttpPost("TestConnection")]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var result = await apiClient.TestConnectionAsync(cancellationToken);
        SetResult(result.Message, result.IsHealthy);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("TestSample")]
    public async Task<IActionResult> TestSample(CancellationToken cancellationToken)
    {
        try
        {
            var products = await apiClient.GetSampleProductsAsync(cancellationToken);
            SetResult($"Örnek ürün testi başarılı. {products.Count} ürün alındı.", true);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            SetResult("Örnek ürün testi başarısız.", false);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Sync")]
    public IActionResult Sync()
    {
        backgroundJobs.Enqueue<CanvasiaProductSyncJob>(job => job.ExecuteAsync(CancellationToken.None));
        SetResult("Ürün senkronizasyonu kuyruğa alındı.", true);
        return RedirectToAction(nameof(Index));
    }

    private void SetResult(string message, bool succeeded)
    {
        TempData["ResultMessage"] = message;
        TempData["ResultSucceeded"] = succeeded;
    }
}

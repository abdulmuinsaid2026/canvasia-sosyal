using CanvasiaSocial.Application.Common.Security;
using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanvasiaSocial.Web.Controllers;

[Authorize(Policy = ApplicationPolicies.ViewDashboard)]
[Route("Products")]
public sealed class ProductsController(IProductCacheService productCacheService, ISingleContentService singleContentService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 24,
        string? search = null,
        string? category = null,
        bool? inStock = null,
        bool? isDiscounted = null,
        Platform? platform = null,
        bool? hasAiContent = null,
        ContentStatus? contentStatus = null,
        bool? isPublished = null,
        DateTime? preparedFrom = null,
        DateTime? preparedTo = null,
        DateTime? publishedFrom = null,
        DateTime? publishedTo = null,
        ProductSort sort = ProductSort.Title,
        CancellationToken cancellationToken = default)
    {
        var filter = new ProductSearch(page, pageSize, search, category, inStock, isDiscounted, platform,
            hasAiContent, contentStatus, isPublished, StartOfDayUtc(preparedFrom), EndOfDayUtc(preparedTo),
            StartOfDayUtc(publishedFrom), EndOfDayUtc(publishedTo), sort);
        ViewBag.Filter = filter;
        ViewBag.PreparedFrom = preparedFrom;
        ViewBag.PreparedTo = preparedTo;
        ViewBag.PublishedFrom = publishedFrom;
        ViewBag.PublishedTo = publishedTo;
        return View(await productCacheService.GetPageAsync(filter, cancellationToken));
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var product = await productCacheService.GetDetailsAsync(id, cancellationToken);
        if (product is null) return NotFound();
        return View(new ProductDetailsPageViewModel(product, await singleContentService.GetForProductAsync(id, cancellationToken)));
    }

    [HttpPost("Details/{id:guid}/Generate")]
    [Authorize(Policy = ApplicationPolicies.ManageContent)]
    public async Task<IActionResult> Generate(Guid id, Platform platform, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
        await singleContentService.GenerateAsync(id, platform, userId, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private static DateTime? StartOfDayUtc(DateTime? value) => ConvertIstanbulDayToUtc(value, addDay: false);

    private static DateTime? EndOfDayUtc(DateTime? value) => ConvertIstanbulDayToUtc(value, addDay: true);

    private static DateTime? ConvertIstanbulDayToUtc(DateTime? value, bool addDay)
    {
        if (!value.HasValue) return null;
        var local = DateTime.SpecifyKind(value.Value.Date.AddDays(addDay ? 1 : 0), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
    }
}

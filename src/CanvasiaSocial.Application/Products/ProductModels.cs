using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Application.Products;

public enum ProductSort
{
    Title,
    PriceAscending,
    PriceDescending,
    RecentlySynced,
    RecentlyPrepared,
    RecentlyPublished
}

public sealed record ProductSearch(
    int Page = 1,
    int PageSize = 24,
    string? Search = null,
    string? Category = null,
    bool? InStock = null,
    bool? IsDiscounted = null,
    Platform? Platform = null,
    bool? HasAiContent = null,
    ContentStatus? ContentStatus = null,
    bool? IsPublished = null,
    DateTime? PreparedFromUtc = null,
    DateTime? PreparedToUtc = null,
    DateTime? PublishedFromUtc = null,
    DateTime? PublishedToUtc = null,
    ProductSort Sort = ProductSort.Title);

public sealed record ProductPlatformActivity(
    Platform Platform,
    ContentStatus? LatestContentStatus,
    string? ModelName,
    DateTime? ContentUpdatedAtUtc,
    bool IsPublished,
    DateTime? PublishedAtUtc,
    string? ExternalPostUrl);

public sealed record ProductListItem(
    Guid Id,
    int CanvasiaProductId,
    string Title,
    string? CategoryName,
    decimal Price,
    bool IsDiscounted,
    bool InStock,
    string ProductUrl,
    string? PrimaryImageUrl,
    IReadOnlyList<ProductPlatformActivity> PlatformActivities);

public sealed record ProductImageView(string Url, bool IsPrimary, int SortOrder);

public sealed record ProductDetails(
    Guid Id,
    int CanvasiaProductId,
    string Title,
    string Slug,
    string? CategoryName,
    decimal Price,
    bool IsDiscounted,
    bool InStock,
    string ProductUrl,
    string? Description,
    string? PromptSummary,
    DateTime SyncedAtUtc,
    IReadOnlyList<ProductImageView> Images,
    bool HasGeneratedContent);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

public sealed record MappedCanvasiaProduct(
    int CanvasiaProductId,
    string Title,
    string Slug,
    string? CategoryName,
    decimal Price,
    bool IsDiscounted,
    bool InStock,
    string ProductUrl,
    string? Description,
    string? PromptSummary,
    string RawJson,
    IReadOnlyList<MappedCanvasiaProductImage> Images);

public sealed record MappedCanvasiaProductImage(string Url, bool IsPrimary, int SortOrder);

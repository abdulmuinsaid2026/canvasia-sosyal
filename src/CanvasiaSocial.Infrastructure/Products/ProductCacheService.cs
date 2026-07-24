using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Products;

public sealed class ProductCacheService(ApplicationDbContext dbContext) : IProductCacheService
{
    public async Task UpsertBatchAsync(IReadOnlyCollection<MappedCanvasiaProduct> products, CancellationToken cancellationToken = default)
    {
        if (products.Count == 0) return;

        var sourceIds = products.Select(x => x.CanvasiaProductId).Distinct().ToArray();
        var existing = await dbContext.ProductCaches.Include(x => x.Images)
            .Where(x => sourceIds.Contains(x.CanvasiaProductId))
            .ToDictionaryAsync(x => x.CanvasiaProductId, cancellationToken);
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var syncedAtUtc = DateTime.UtcNow;
        foreach (var source in products)
        {
            if (!existing.TryGetValue(source.CanvasiaProductId, out var target))
            {
                target = new ProductCache { CanvasiaProductId = source.CanvasiaProductId };
                dbContext.ProductCaches.Add(target);
                existing[source.CanvasiaProductId] = target;
            }

            target.Title = source.Title;
            target.Slug = source.Slug;
            target.CategoryName = source.CategoryName;
            target.Price = source.Price;
            target.DiscountedPrice = source.IsDiscounted ? source.Price : null;
            target.IsDiscounted = source.IsDiscounted;
            target.InStock = source.InStock;
            target.ProductUrl = source.ProductUrl;
            target.Description = source.Description;
            target.PromptSummary = source.PromptSummary;
            target.RawJson = source.RawJson;
            target.SyncedAtUtc = syncedAtUtc;

            if (target.Images.Count > 0)
            {
                dbContext.ProductImages.RemoveRange(target.Images.ToArray());
            }
            foreach (var image in source.Images)
            {
                dbContext.ProductImages.Add(new ProductImage
                {
                    ProductCacheId = target.Id,
                    Url = image.Url,
                    IsPrimary = image.IsPrimary,
                    SortOrder = image.SortOrder
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        // A full catalog sync must retain only the current page in EF's tracker.
        dbContext.ChangeTracker.Clear();
    }

    public async Task<PagedResult<ProductListItem>> GetPageAsync(ProductSearch search, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, 100);
        var query = dbContext.ProductCaches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var value = search.Search.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(value) || x.Slug.ToLower().Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            var value = search.Category.Trim().ToLower();
            query = query.Where(x => x.CategoryName != null && x.CategoryName.ToLower().Contains(value));
        }
        if (search.InStock.HasValue) query = query.Where(x => x.InStock == search.InStock.Value);
        if (search.IsDiscounted.HasValue) query = query.Where(x => x.IsDiscounted == search.IsDiscounted.Value);
        var hasScopedActivityFilter = search.HasAiContent.HasValue || search.ContentStatus.HasValue || search.IsPublished.HasValue ||
                                      search.PreparedFromUtc.HasValue || search.PreparedToUtc.HasValue ||
                                      search.PublishedFromUtc.HasValue || search.PublishedToUtc.HasValue;
        if (search.Platform.HasValue && !hasScopedActivityFilter)
        {
            var platform = search.Platform.Value;
            query = query.Where(x => dbContext.GeneratedContents.Any(content => content.ProductCacheId == x.Id && content.Platform == platform) ||
                                     dbContext.ProductPublicationHistories.Any(history => history.ProductCacheId == x.Id && history.Platform == platform));
        }
        if (search.HasAiContent.HasValue)
        {
            query = search.HasAiContent.Value
                ? query.Where(x => dbContext.GeneratedContents.Any(content => content.ProductCacheId == x.Id &&
                    (!search.Platform.HasValue || content.Platform == search.Platform.Value)))
                : query.Where(x => !dbContext.GeneratedContents.Any(content => content.ProductCacheId == x.Id &&
                    (!search.Platform.HasValue || content.Platform == search.Platform.Value)));
        }
        if (search.ContentStatus.HasValue)
        {
            query = query.Where(x => dbContext.GeneratedContents.Any(content => content.ProductCacheId == x.Id &&
                content.Status == search.ContentStatus.Value && (!search.Platform.HasValue || content.Platform == search.Platform.Value)));
        }
        if (search.IsPublished.HasValue)
        {
            query = search.IsPublished.Value
                ? query.Where(x => dbContext.ProductPublicationHistories.Any(history => history.ProductCacheId == x.Id &&
                    (!search.Platform.HasValue || history.Platform == search.Platform.Value)))
                : query.Where(x => !dbContext.ProductPublicationHistories.Any(history => history.ProductCacheId == x.Id &&
                    (!search.Platform.HasValue || history.Platform == search.Platform.Value)));
        }
        if (search.PreparedFromUtc.HasValue) query = query.Where(x => dbContext.GeneratedContents.Any(content =>
            content.ProductCacheId == x.Id && content.CreatedAtUtc >= search.PreparedFromUtc.Value &&
            (!search.Platform.HasValue || content.Platform == search.Platform.Value)));
        if (search.PreparedToUtc.HasValue) query = query.Where(x => dbContext.GeneratedContents.Any(content =>
            content.ProductCacheId == x.Id && content.CreatedAtUtc < search.PreparedToUtc.Value &&
            (!search.Platform.HasValue || content.Platform == search.Platform.Value)));
        if (search.PublishedFromUtc.HasValue) query = query.Where(x => dbContext.ProductPublicationHistories.Any(history =>
            history.ProductCacheId == x.Id && history.PublishedAtUtc >= search.PublishedFromUtc.Value &&
            (!search.Platform.HasValue || history.Platform == search.Platform.Value)));
        if (search.PublishedToUtc.HasValue) query = query.Where(x => dbContext.ProductPublicationHistories.Any(history =>
            history.ProductCacheId == x.Id && history.PublishedAtUtc < search.PublishedToUtc.Value &&
            (!search.Platform.HasValue || history.Platform == search.Platform.Value)));

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(page, totalPages);
        var ordered = search.Sort switch
        {
            ProductSort.PriceAscending => query.OrderBy(x => x.Price).ThenBy(x => x.Title),
            ProductSort.PriceDescending => query.OrderByDescending(x => x.Price).ThenBy(x => x.Title),
            ProductSort.RecentlySynced => query.OrderByDescending(x => x.SyncedAtUtc).ThenBy(x => x.Title),
            ProductSort.RecentlyPrepared => query.OrderByDescending(x => dbContext.GeneratedContents
                .Where(content => content.ProductCacheId == x.Id && (!search.Platform.HasValue || content.Platform == search.Platform.Value))
                .Max(content => (DateTime?)content.UpdatedAtUtc)).ThenBy(x => x.Title),
            ProductSort.RecentlyPublished => query.OrderByDescending(x => dbContext.ProductPublicationHistories
                .Where(history => history.ProductCacheId == x.Id && (!search.Platform.HasValue || history.Platform == search.Platform.Value))
                .Max(history => (DateTime?)history.PublishedAtUtc)).ThenBy(x => x.Title),
            _ => query.OrderBy(x => x.Title).ThenBy(x => x.CanvasiaProductId)
        };
        var items = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProductListItem(
                x.Id, x.CanvasiaProductId, x.Title, x.CategoryName, x.Price,
                x.IsDiscounted, x.InStock, x.ProductUrl,
                x.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
                    .Select(image => image.Url).FirstOrDefault(), null!))
            .ToListAsync(cancellationToken);

        items = await AddPlatformActivitiesAsync(items, cancellationToken);

        return new PagedResult<ProductListItem>(items, page, pageSize, totalItems, totalPages);
    }

    public Task<ProductDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.ProductCaches.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ProductDetails(
                x.Id, x.CanvasiaProductId, x.Title, x.Slug, x.CategoryName, x.Price,
                x.IsDiscounted, x.InStock, x.ProductUrl, x.Description, x.PromptSummary, x.SyncedAtUtc,
                x.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
                    .Select(image => new ProductImageView(image.Url, image.IsPrimary, image.SortOrder)).ToList(),
                dbContext.GeneratedContents.Any(content => content.ProductCacheId == x.Id)))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        dbContext.ProductCaches.CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductListItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var distinctIds = ids.Distinct().ToArray();
        var items = await dbContext.ProductCaches.AsNoTracking().Where(x => distinctIds.Contains(x.Id))
            .OrderBy(x => x.Title)
            .Select(x => new ProductListItem(x.Id, x.CanvasiaProductId, x.Title, x.CategoryName, x.Price,
                x.IsDiscounted, x.InStock, x.ProductUrl,
                x.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
                    .Select(image => image.Url).FirstOrDefault(), null!))
            .ToListAsync(cancellationToken);
        return await AddPlatformActivitiesAsync(items, cancellationToken);
    }

    private async Task<List<ProductListItem>> AddPlatformActivitiesAsync(
        IReadOnlyList<ProductListItem> products,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0) return [];
        var ids = products.Select(x => x.Id).ToArray();
        var contents = await dbContext.GeneratedContents.AsNoTracking()
            .Where(x => ids.Contains(x.ProductCacheId))
            .Select(x => new { x.ProductCacheId, x.Platform, x.Status, x.ModelName, x.UpdatedAtUtc })
            .ToListAsync(cancellationToken);
        var publications = await dbContext.ProductPublicationHistories.AsNoTracking()
            .Where(x => ids.Contains(x.ProductCacheId))
            .Select(x => new { x.ProductCacheId, x.Platform, x.PublishedAtUtc, x.ScheduledPost.ExternalPostUrl })
            .ToListAsync(cancellationToken);

        return products.Select(product =>
        {
            var productContents = contents.Where(x => x.ProductCacheId == product.Id).ToArray();
            var productPublications = publications.Where(x => x.ProductCacheId == product.Id).ToArray();
            var platforms = productContents.Select(x => x.Platform).Concat(productPublications.Select(x => x.Platform)).Distinct().OrderBy(x => x);
            var activities = platforms.Select(platform =>
            {
                var content = productContents.Where(x => x.Platform == platform).OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault();
                var publication = productPublications.Where(x => x.Platform == platform).OrderByDescending(x => x.PublishedAtUtc).FirstOrDefault();
                return new ProductPlatformActivity(platform, content?.Status, content?.ModelName, content?.UpdatedAtUtc,
                    publication is not null, publication?.PublishedAtUtc, publication?.ExternalPostUrl);
            }).ToArray();
            return product with { PlatformActivities = activities };
        }).ToList();
    }
}

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

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
        page = Math.Min(page, totalPages);
        var items = await query.OrderBy(x => x.Title).ThenBy(x => x.CanvasiaProductId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProductListItem(
                x.Id, x.CanvasiaProductId, x.Title, x.CategoryName, x.Price,
                x.IsDiscounted, x.InStock, x.ProductUrl,
                x.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
                    .Select(image => image.Url).FirstOrDefault()))
            .ToListAsync(cancellationToken);

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
        return await dbContext.ProductCaches.AsNoTracking().Where(x => distinctIds.Contains(x.Id))
            .OrderBy(x => x.Title)
            .Select(x => new ProductListItem(x.Id, x.CanvasiaProductId, x.Title, x.CategoryName, x.Price,
                x.IsDiscounted, x.InStock, x.ProductUrl,
                x.Images.OrderByDescending(image => image.IsPrimary).ThenBy(image => image.SortOrder)
                    .Select(image => image.Url).FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }
}

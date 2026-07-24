using CanvasiaSocial.Application.Canvasia;

namespace CanvasiaSocial.Application.Products;

public interface ICanvasiaProductMapper
{
    MappedCanvasiaProduct Map(CanvasiaProductDto source);
}

public interface IProductCacheService
{
    Task UpsertBatchAsync(IReadOnlyCollection<MappedCanvasiaProduct> products, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductListItem>> GetPageAsync(ProductSearch search, CancellationToken cancellationToken = default);
    Task<ProductDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductListItem>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}

using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Infrastructure.Persistence;
using CanvasiaSocial.Infrastructure.Products;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.IntegrationTests;

public sealed class ProductCacheServiceTests
{
    [Fact]
    public async Task Upsert_updates_same_canvasia_product_without_duplicate()
    {
        await using var dbContext = CreateContext();
        var service = new ProductCacheService(dbContext);

        await service.UpsertBatchAsync([CreateProduct(7, "İlk başlık")]);
        await service.UpsertBatchAsync([CreateProduct(7, "Güncel başlık")]);

        var products = await dbContext.ProductCaches.AsNoTracking().Include(x => x.Images).ToListAsync();
        Assert.Single(products);
        Assert.Equal("Güncel başlık", products[0].Title);
        Assert.Single(products[0].Images);
    }

    [Fact]
    public async Task GetPage_uses_server_side_page_boundaries()
    {
        await using var dbContext = CreateContext();
        dbContext.ProductCaches.AddRange(Enumerable.Range(1, 25).Select(index => new ProductCache
        {
            CanvasiaProductId = index,
            Title = $"Ürün {index:D2}",
            Slug = $"urun-{index}",
            Price = index,
            ProductUrl = $"https://canvasia.test/{index}",
            RawJson = "{}"
        }));
        await dbContext.SaveChangesAsync();

        var result = await new ProductCacheService(dbContext).GetPageAsync(new ProductSearch(Page: 2, PageSize: 10));

        Assert.Equal(25, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task Cached_products_remain_queryable_without_api_client()
    {
        await using var dbContext = CreateContext();
        dbContext.ProductCaches.Add(new ProductCache
        {
            CanvasiaProductId = 99,
            Title = "Cache ürünü",
            Slug = "cache-urunu",
            Price = 10,
            ProductUrl = "https://canvasia.test/99",
            RawJson = "{}"
        });
        await dbContext.SaveChangesAsync();

        var result = await new ProductCacheService(dbContext).GetPageAsync(new ProductSearch());

        Assert.Single(result.Items);
        Assert.Equal("Cache ürünü", result.Items[0].Title);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static MappedCanvasiaProduct CreateProduct(int id, string title) => new(
        id, title, $"urun-{id}", "Dekor", 100, true, true,
        $"https://canvasia.test/{id}", "Açıklama", "Prompt", "{}",
        [new MappedCanvasiaProductImage("https://canvasia.test/image.jpg", true, 0)]);
}

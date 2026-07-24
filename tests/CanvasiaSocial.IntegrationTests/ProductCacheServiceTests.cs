using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
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

    [Fact]
    public async Task GetPage_filters_and_reports_ai_and_publication_activity_by_platform()
    {
        await using var dbContext = CreateContext();
        var draftProduct = Product(1, "Instagram taslağı");
        var publishedProduct = Product(2, "Facebook yayını");
        var draftContent = Content(draftProduct, Platform.Instagram, ContentStatus.Draft, "gemma-test");
        var publishedContent = Content(publishedProduct, Platform.Facebook, ContentStatus.Published, "gemma-publish");
        var account = new SocialAccount
        {
            Platform = Platform.Facebook, DisplayName = "Facebook", ExternalAccountId = "fb-1",
            EncryptedAccessToken = "encrypted", Status = "Active"
        };
        var post = new ScheduledPost
        {
            SocialAccount = account, SocialAccountId = account.Id, GeneratedContent = publishedContent,
            GeneratedContentId = publishedContent.Id, Platform = Platform.Facebook, Status = ContentStatus.Published,
            ScheduledAtUtc = DateTime.UtcNow.AddDays(-1), PublishedAtUtc = DateTime.UtcNow,
            ExternalPostUrl = "https://facebook.test/post/1", IdempotencyKey = "published-product", CreatedByUserId = "tester"
        };
        var history = new ProductPublicationHistory
        {
            ProductCache = publishedProduct, ProductCacheId = publishedProduct.Id, Platform = Platform.Facebook,
            SocialAccount = account, SocialAccountId = account.Id, ScheduledPost = post, ScheduledPostId = post.Id,
            PublishedAtUtc = DateTime.UtcNow
        };
        dbContext.AddRange(draftProduct, publishedProduct, draftContent, publishedContent, account, post, history);
        await dbContext.SaveChangesAsync();
        var service = new ProductCacheService(dbContext);

        var instagramDrafts = await service.GetPageAsync(new ProductSearch(
            Platform: Platform.Instagram, HasAiContent: true, ContentStatus: ContentStatus.Draft, IsPublished: false));
        var facebookPublications = await service.GetPageAsync(new ProductSearch(
            Platform: Platform.Facebook, IsPublished: true, Sort: ProductSort.RecentlyPublished));
        var notPreparedForInstagram = await service.GetPageAsync(new ProductSearch(
            Platform: Platform.Instagram, HasAiContent: false));

        var draft = Assert.Single(instagramDrafts.Items);
        var draftActivity = Assert.Single(draft.PlatformActivities);
        Assert.Equal(Platform.Instagram, draftActivity.Platform);
        Assert.Equal(ContentStatus.Draft, draftActivity.LatestContentStatus);
        Assert.False(draftActivity.IsPublished);
        var published = Assert.Single(facebookPublications.Items);
        var publishedActivity = Assert.Single(published.PlatformActivities);
        Assert.True(publishedActivity.IsPublished);
        Assert.Equal("https://facebook.test/post/1", publishedActivity.ExternalPostUrl);
        Assert.Equal("gemma-publish", publishedActivity.ModelName);
        Assert.Equal(publishedProduct.Id, Assert.Single(notPreparedForInstagram.Items).Id);
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

    private static ProductCache Product(int id, string title) => new()
    {
        CanvasiaProductId = id, Title = title, Slug = $"urun-{id}", Price = 100,
        ProductUrl = $"https://canvasia.test/{id}", RawJson = "{}"
    };

    private static GeneratedContent Content(ProductCache product, Platform platform, ContentStatus status, string model) => new()
    {
        ProductCache = product, ProductCacheId = product.Id, Platform = platform, Caption = "İçerik",
        HashtagsJson = "[]", Language = "tr", Tone = "test", ModelName = model,
        PromptVersion = "v1", PromptHash = Guid.NewGuid().ToString("N"), Status = status, CreatedByUserId = "tester"
    };
}

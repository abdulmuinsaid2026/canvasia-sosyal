using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using CanvasiaSocial.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CanvasiaSocial.IntegrationTests;

public sealed class SocialMediaImagesControllerTests
{
    [Fact]
    public async Task Converts_product_webp_to_instagram_compatible_jpeg()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var product = new ProductCache
        {
            CanvasiaProductId = 1, Title = "WebP ürün", Slug = "webp-urun", Price = 100,
            ProductUrl = "https://www.canvasia.com.tr/urun", RawJson = "{}"
        };
        product.Images.Add(new ProductImage
        {
            ProductCache = product, ProductCacheId = product.Id,
            Url = "https://www.canvasia.com.tr/image.webp", IsPrimary = true
        });
        var content = new GeneratedContent
        {
            ProductCache = product, ProductCacheId = product.Id, Platform = Platform.Instagram,
            Caption = "İçerik", HashtagsJson = "[]", Language = "tr", Tone = "test",
            ModelName = "test", PromptVersion = "v1", PromptHash = "hash", CreatedByUserId = "tester"
        };
        var post = new ScheduledPost
        {
            GeneratedContent = content, GeneratedContentId = content.Id, Platform = Platform.Instagram,
            Status = ContentStatus.Scheduled, ScheduledAtUtc = DateTime.UtcNow,
            IdempotencyKey = "image-test", CreatedByUserId = "tester"
        };
        db.AddRange(product, content, post);
        await db.SaveChangesAsync();
        var controller = new SocialMediaImagesController(db, new AcceptAllTokens(), new WebpImageService());

        var result = Assert.IsType<FileContentResult>(await controller.InstagramJpeg(post.Id, "valid", CancellationToken.None));

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.True(result.FileContents.Length > 3);
        Assert.Equal([0xFF, 0xD8, 0xFF], result.FileContents[..3]);
    }

    private sealed class AcceptAllTokens : ISocialImageTokenService
    {
        public Uri CreateInstagramJpegUrl(Guid scheduledPostId, string redirectUri) => new("https://example.test/image.jpg");
        public bool IsValidInstagramJpegToken(Guid scheduledPostId, string token) => true;
    }

    private sealed class WebpImageService : ISecureImageService
    {
        public Task<Uri> ValidateAndPrepareAsync(string imageUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Uri(imageUrl));

        public async Task<ValidatedImage> DownloadAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            using var image = new Image<Rgba32>(1000, 1000, Color.DarkGreen);
            await using var output = new MemoryStream();
            await image.SaveAsWebpAsync(output, cancellationToken);
            return new ValidatedImage(new Uri(imageUrl), "image/webp", output.ToArray());
        }
    }
}

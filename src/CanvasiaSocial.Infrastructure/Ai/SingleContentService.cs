using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.Infrastructure.Ai;

public sealed class SingleContentService(
    ApplicationDbContext dbContext,
    IAiContentGenerator generator) : ISingleContentService
{
    public async Task<GeneratedContentView> GenerateAsync(Guid productId, Platform platform, string userId, CancellationToken cancellationToken = default)
    {
        var product = await dbContext.ProductCaches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException("Ürün bulunamadı.");
        var result = await generator.GenerateAsync(new AiContentRequest(
            product.Id, product.Title, product.CategoryName, product.Price, product.Description,
            product.PromptSummary, product.ProductUrl, platform, true, true), cancellationToken);

        var content = CreateEntity(product, platform, userId, result, ContentStatus.Draft);
        dbContext.GeneratedContents.Add(content);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToView(content);
    }

    public async Task<IReadOnlyList<GeneratedContentView>> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.GeneratedContents.AsNoTracking()
            .Where(x => x.ProductCacheId == productId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        return rows.Select(ToView).ToArray();
    }

    internal static GeneratedContent CreateEntity(ProductCache product, Platform platform, string userId, AiContentResult result, ContentStatus status)
    {
        var promptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(product.PromptSummary ?? product.Title)));
        return new GeneratedContent
        {
            ProductCacheId = product.Id,
            Platform = platform,
            Caption = result.Caption,
            StoryText = result.StoryText,
            CallToAction = result.CallToAction,
            HashtagsJson = JsonSerializer.Serialize(result.Hashtags),
            Language = "tr",
            Tone = "samimi-satış-odaklı",
            ModelName = result.ModelName,
            PromptVersion = "v1",
            PromptHash = promptHash,
            RawAiResponse = result.RawResponse,
            Status = status,
            CreatedByUserId = userId
        };
    }

    private static GeneratedContentView ToView(GeneratedContent content) => new(
        content.Id, content.ProductCacheId, content.Platform, content.Caption, content.StoryText,
        content.CallToAction, JsonSerializer.Deserialize<string[]>(content.HashtagsJson) ?? [], content.Status, content.CreatedAtUtc);
}

using CanvasiaSocial.Domain.Common;

namespace CanvasiaSocial.Domain.Entities;

public sealed class ProductCache : Entity
{
    public int CanvasiaProductId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountedPrice { get; set; }
    public bool IsDiscounted { get; set; }
    public bool InStock { get; set; }
    public string ProductUrl { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PromptSummary { get; set; }
    public string RawJson { get; set; } = "{}";
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ProductImage> Images { get; set; } = [];
}

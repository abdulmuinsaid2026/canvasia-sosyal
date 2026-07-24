using CanvasiaSocial.Domain.Common;

namespace CanvasiaSocial.Domain.Entities;

public sealed class ProductImage : Entity
{
    public Guid ProductCacheId { get; set; }
    public ProductCache ProductCache { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
}

using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class ProductPublicationHistory : Entity
{
    public Guid ProductCacheId { get; set; }
    public ProductCache ProductCache { get; set; } = null!;
    public Platform Platform { get; set; }
    public Guid SocialAccountId { get; set; }
    public SocialAccount SocialAccount { get; set; } = null!;
    public Guid ScheduledPostId { get; set; }
    public ScheduledPost ScheduledPost { get; set; } = null!;
    public DateTime PublishedAtUtc { get; set; }
}

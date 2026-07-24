using CanvasiaSocial.Domain.Common;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Domain.Entities;

public sealed class GeneratedContent : Entity
{
    public Guid ProductCacheId { get; set; }
    public ProductCache ProductCache { get; set; } = null!;
    public Platform Platform { get; set; }
    public string Caption { get; set; } = string.Empty;
    public string? StoryText { get; set; }
    public string? CallToAction { get; set; }
    public string HashtagsJson { get; set; } = "[]";
    public string Language { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public string PromptHash { get; set; } = string.Empty;
    public string? RawAiResponse { get; set; }
    public ContentStatus Status { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

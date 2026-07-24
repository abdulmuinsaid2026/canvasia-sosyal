using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Application.Ai;

public sealed record AiContentRequest(
    Guid ProductId,
    string Title,
    string? Category,
    decimal Price,
    string? Description,
    string? PromptSummary,
    string ProductUrl,
    Platform Platform,
    bool IncludePrice,
    bool IncludeProductLink);

public sealed record AiContentResult(
    string Caption,
    string? StoryText,
    string? CallToAction,
    IReadOnlyList<string> Hashtags,
    string ModelName,
    string RawResponse);

public sealed record GeneratedContentView(
    Guid Id,
    Guid ProductId,
    Platform Platform,
    string Caption,
    string? StoryText,
    string? CallToAction,
    IReadOnlyList<string> Hashtags,
    ContentStatus Status,
    DateTime CreatedAtUtc);

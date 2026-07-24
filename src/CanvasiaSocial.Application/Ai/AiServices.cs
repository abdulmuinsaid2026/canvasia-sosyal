using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Application.Ai;

public interface IAiContentGenerator
{
    Task<AiContentResult> GenerateAsync(AiContentRequest request, CancellationToken cancellationToken = default);
}

public interface ISingleContentService
{
    Task<GeneratedContentView> GenerateAsync(
        Guid productId,
        Platform platform,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedContentView>> GetForProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}

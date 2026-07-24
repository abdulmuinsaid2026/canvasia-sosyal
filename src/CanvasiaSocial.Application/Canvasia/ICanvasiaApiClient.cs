namespace CanvasiaSocial.Application.Canvasia;

public interface ICanvasiaApiClient
{
    Task<CanvasiaProductPageDto> GetProductsAsync(
        int page,
        int pageSize,
        string? category = null,
        string? search = null,
        bool onlyDiscounted = false,
        bool onlyInStock = false,
        CancellationToken cancellationToken = default);

    Task<CanvasiaProductDto?> GetProductAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CanvasiaProductDto>> GetSampleProductsAsync(CancellationToken cancellationToken = default);
    Task<CanvasiaConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed record CanvasiaConnectionResult(bool IsHealthy, string Message);

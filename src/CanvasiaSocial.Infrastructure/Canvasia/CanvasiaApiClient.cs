using System.Net;
using System.Net.Http.Json;
using CanvasiaSocial.Application.Canvasia;

namespace CanvasiaSocial.Infrastructure.Canvasia;

internal sealed class CanvasiaApiClient(HttpClient httpClient, CanvasiaOptions options) : ICanvasiaApiClient
{
    public async Task<CanvasiaProductPageDto> GetProductsAsync(
        int page,
        int pageSize,
        string? category = null,
        string? search = null,
        bool onlyDiscounted = false,
        bool onlyInStock = false,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}"
        };

        AddQuery(query, "category", category);
        AddQuery(query, "search", search);
        if (onlyDiscounted) query.Add("onlyDiscounted=true");
        if (onlyInStock) query.Add("onlyInStock=true");

        var result = await httpClient.GetFromJsonAsync<CanvasiaProductPageDto>(
            $"api/canvasia-social/products?{string.Join('&', query)}",
            cancellationToken);

        return result ?? throw new HttpRequestException("Canvasia API boş ürün listesi yanıtı döndürdü.");
    }

    public async Task<CanvasiaProductDto?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var response = await httpClient.GetAsync($"api/canvasia-social/products/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CanvasiaProductDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<CanvasiaProductDto>> GetSampleProductsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        return await httpClient.GetFromJsonAsync<List<CanvasiaProductDto>>(
            "api/canvasia-social/products/sample",
            cancellationToken) ?? [];
    }

    public async Task<CanvasiaConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!options.HasValidBaseUrl)
        {
            return new CanvasiaConnectionResult(false, "Canvasia API base URL yapılandırılmamış veya geçersiz.");
        }

        if (!options.IsApiKeyConfigured)
        {
            return new CanvasiaConnectionResult(false, "Canvasia API anahtarı yapılandırılmamış.");
        }

        try
        {
            await GetProductsAsync(1, 1, cancellationToken: cancellationToken);
            return new CanvasiaConnectionResult(true, "Canvasia API bağlantısı başarılı.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new CanvasiaConnectionResult(false, "Canvasia API bağlantısı kurulamadı.");
        }
    }

    private void EnsureConfigured()
    {
        if (!options.HasValidBaseUrl || httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException("Canvasia API base URL yapılandırılmamış veya geçersiz.");
        }

        if (!options.IsApiKeyConfigured)
        {
            throw new InvalidOperationException("Canvasia API anahtarı yapılandırılmamış.");
        }
    }

    private static void AddQuery(ICollection<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}

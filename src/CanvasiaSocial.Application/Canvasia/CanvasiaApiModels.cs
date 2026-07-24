using System.Text.Json.Serialization;

namespace CanvasiaSocial.Application.Canvasia;

public sealed class CanvasiaProductDto
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("baslik")]
    public string Baslik { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("aciklama")]
    public string Aciklama { get; init; } = string.Empty;

    [JsonPropertyName("kisaAciklama")]
    public string KisaAciklama { get; init; } = string.Empty;

    [JsonPropertyName("kategoriAdi")]
    public string KategoriAdi { get; init; } = string.Empty;

    [JsonPropertyName("etkinFiyat")]
    public decimal EtkinFiyat { get; init; }

    [JsonPropertyName("indirimVarMi")]
    public bool IndirimVarMi { get; init; }

    [JsonPropertyName("stoktaVarMi")]
    public bool StoktaVarMi { get; init; }

    [JsonPropertyName("urunUrl")]
    public string UrunUrl { get; init; } = string.Empty;

    [JsonPropertyName("resimler")]
    public IReadOnlyList<CanvasiaProductImageDto> Resimler { get; init; } = [];

    [JsonPropertyName("secenekler")]
    public IReadOnlyList<CanvasiaProductOptionDto> Secenekler { get; init; } = [];

    [JsonPropertyName("sosyalMedyaPromptOzeti")]
    public string SosyalMedyaPromptOzeti { get; init; } = string.Empty;
}

public sealed class CanvasiaProductImageDto
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("alt")]
    public string Alt { get; init; } = string.Empty;

    [JsonPropertyName("sira")]
    public int Sira { get; init; }

    [JsonPropertyName("anaGorselMi")]
    public bool AnaGorselMi { get; init; }
}

public sealed class CanvasiaProductOptionDto
{
    [JsonPropertyName("ad")]
    public string Ad { get; init; } = string.Empty;

    [JsonPropertyName("fiyat")]
    public decimal Fiyat { get; init; }

    [JsonPropertyName("stoktaVarMi")]
    public bool StoktaVarMi { get; init; }
}

public sealed class CanvasiaProductPageDto
{
    [JsonPropertyName("page")]
    public int Page { get; init; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("items")]
    public IReadOnlyList<CanvasiaProductDto> Items { get; init; } = [];
}

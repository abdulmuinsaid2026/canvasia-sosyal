using System.Text.Json;
using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Infrastructure.Canvasia;

namespace CanvasiaSocial.IntegrationTests;

public sealed class CanvasiaProductMapperTests
{
    [Fact]
    public void Api_contract_deserializes_and_maps_without_domain_coupling()
    {
        const string json = """
            {
              "id": 42,
              "baslik": "Kanvas Tablo",
              "slug": "kanvas-tablo",
              "aciklama": "<p>Duvar dekoru</p>",
              "kisaAciklama": "Dekor",
              "kategoriAdi": "Tablolar",
              "etkinFiyat": 349.90,
              "indirimVarMi": true,
              "stoktaVarMi": true,
              "urunUrl": "https://canvasia.test/Urun/Detay/kanvas-tablo-42",
              "resimler": [{"url":"https://canvasia.test/image.jpg","alt":"Tablo","sira":1,"anaGorselMi":true}],
              "secenekler": [{"ad":"Standart","fiyat":349.90,"stoktaVarMi":true}],
              "sosyalMedyaPromptOzeti": "Satış odaklı içerik üret."
            }
            """;

        var dto = JsonSerializer.Deserialize<CanvasiaProductDto>(json)!;
        var mapped = new CanvasiaProductMapper().Map(dto);

        Assert.Equal(42, mapped.CanvasiaProductId);
        Assert.Equal("Kanvas Tablo", mapped.Title);
        Assert.Equal(349.90m, mapped.Price);
        Assert.True(mapped.IsDiscounted);
        Assert.Single(mapped.Images);
        Assert.Contains("secenekler", mapped.RawJson);
    }

    [Fact]
    public void Mapper_rejects_non_http_product_and_image_urls()
    {
        var dto = new CanvasiaProductDto
        {
            Id = 1,
            Baslik = "Ürün",
            UrunUrl = "javascript:alert(1)",
            Resimler = [new CanvasiaProductImageDto { Url = "file:///secret", AnaGorselMi = true }]
        };

        var mapped = new CanvasiaProductMapper().Map(dto);

        Assert.Empty(mapped.ProductUrl);
        Assert.Empty(mapped.Images);
    }
}

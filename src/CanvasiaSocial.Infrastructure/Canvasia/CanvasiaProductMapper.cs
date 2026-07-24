using System.Text.Json;
using CanvasiaSocial.Application.Canvasia;
using CanvasiaSocial.Application.Products;

namespace CanvasiaSocial.Infrastructure.Canvasia;

public sealed class CanvasiaProductMapper : ICanvasiaProductMapper
{
    public MappedCanvasiaProduct Map(CanvasiaProductDto source)
    {
        return new MappedCanvasiaProduct(
            source.Id,
            source.Baslik.Trim(),
            source.Slug.Trim(),
            NullIfEmpty(source.KategoriAdi),
            source.EtkinFiyat,
            source.IndirimVarMi,
            source.StoktaVarMi,
            SanitizeHttpUrl(source.UrunUrl),
            NullIfEmpty(!string.IsNullOrWhiteSpace(source.Aciklama) ? source.Aciklama : source.KisaAciklama),
            NullIfEmpty(source.SosyalMedyaPromptOzeti),
            JsonSerializer.Serialize(source),
            source.Resimler
                .Select(x => new MappedCanvasiaProductImage(
                    SanitizeHttpUrl(x.Url),
                    x.AnaGorselMi,
                    x.Sira))
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.SortOrder)
                .ToArray());
    }

    private static string SanitizeHttpUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return string.Empty;
        }

        return uri.ToString();
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

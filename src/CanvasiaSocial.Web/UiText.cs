using CanvasiaSocial.Domain.Enums;
using CanvasiaSocial.Application.Products;

namespace CanvasiaSocial.Web;

public static class UiText
{
    public static string Turkish(this ContentStatus status) => status switch
    {
        ContentStatus.Generating => "Üretiliyor",
        ContentStatus.Draft => "Taslak",
        ContentStatus.AwaitingApproval => "Onay bekliyor",
        ContentStatus.Approved => "Onaylandı",
        ContentStatus.Rejected => "Reddedildi",
        ContentStatus.Scheduled => "Planlandı",
        ContentStatus.Publishing => "Yayımlanıyor",
        ContentStatus.Published => "Yayımlandı",
        ContentStatus.Failed => "Başarısız",
        ContentStatus.Cancelled => "İptal edildi",
        _ => status.ToString()
    };

    public static string Turkish(this CampaignStatus status) => status switch
    {
        CampaignStatus.Draft => "Taslak",
        CampaignStatus.Preparing => "Hazırlanıyor",
        CampaignStatus.Paused => "Duraklatıldı",
        CampaignStatus.Ready => "Hazır",
        CampaignStatus.Active => "Etkin",
        CampaignStatus.Completed => "Tamamlandı",
        CampaignStatus.PartiallyFailed => "Kısmen başarısız",
        CampaignStatus.Failed => "Başarısız",
        CampaignStatus.Cancelled => "İptal edildi",
        _ => status.ToString()
    };

    public static string Turkish(this CampaignMode mode) => mode switch
    {
        CampaignMode.DraftOnly => "Yalnızca taslak oluştur",
        CampaignMode.RequireApproval => "Onaydan sonra planla",
        CampaignMode.AutoSchedule => "Otomatik planla",
        _ => mode.ToString()
    };

    public static string Turkish(this ProductSort sort) => sort switch
    {
        ProductSort.Title => "Ürün adına göre",
        ProductSort.PriceAscending => "Fiyat: düşükten yükseğe",
        ProductSort.PriceDescending => "Fiyat: yüksekten düşüğe",
        ProductSort.RecentlySynced => "Son eşitlenenler",
        ProductSort.RecentlyPrepared => "Son AI hazırlananlar",
        ProductSort.RecentlyPublished => "Son yayımlananlar",
        _ => sort.ToString()
    };

    public static string TurkishSystemStatus(string? status) => status switch
    {
        "Active" => "Etkin",
        "ReconnectRequired" => "Yeniden bağlantı gerekiyor",
        "Disconnected" => "Bağlantı kesildi",
        "Running" => "Çalışıyor",
        "Succeeded" => "Başarılı",
        "Failed" => "Başarısız",
        "NeverRun" => "Henüz çalışmadı",
        null or "" => "Bilinmiyor",
        _ => status
    };

    public static string PublishErrorAdvice(string? code, string? message)
    {
        if (code is "ERROR" or "EXPIRED")
            return "Instagram görseli işleyemedi. Görsel artık otomatik olarak JPEG'e dönüştürülecek; yeniden yayınlamayı deneyebilirsiniz.";
        if (code == "190" || message?.Contains("token", StringComparison.OrdinalIgnoreCase) == true)
            return "Instagram bağlantısının süresi dolmuş olabilir. Sosyal hesaplar ekranından hesabı yeniden bağlayın.";
        if (code is "Forbidden" or "403")
            return "Instagram yayın izni reddedildi. Hesap yetkilerini ve Meta uygulama erişimini kontrol edin.";
        if (code == "OutcomeUnknown")
            return "Çift gönderiyi önlemek için önce Instagram profilini kontrol edin. Gönderi yoksa yeniden yayınlayın.";
        if (code is "Transient" or "1" or "2")
            return "Geçici bağlantı veya işleme sorunu oluştu. Yeniden yayınlamayı deneyebilirsiniz.";
        return "Hata giderildikten sonra bu gönderiyi yeniden yayınlayabilirsiniz.";
    }
}

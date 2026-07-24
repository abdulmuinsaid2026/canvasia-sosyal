using System.Net;
using System.Net.Sockets;
using CanvasiaSocial.Application.Social;

namespace CanvasiaSocial.Infrastructure.Social;

public sealed class SecureImageService(SecureImageOptions options) : ISecureImageService
{
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    public async Task<Uri> ValidateAndPrepareAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        cancellationToken = timeout.Token;
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var current) || current.Scheme is not ("http" or "https"))
        {
            throw Invalid("Görsel adresi yalnızca http veya https olabilir.");
        }

        for (var redirect = 0; redirect <= options.MaxRedirects; redirect++)
        {
            var addresses = await ValidateUriAsync(current, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var handler = CreatePinnedHandler(addresses);
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect == options.MaxRedirects || response.Headers.Location is null)
                    throw Invalid("Görsel yönlendirme sınırı aşıldı.");
                current = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
                continue;
            }
            if (!response.IsSuccessStatusCode) throw Invalid($"Görsel indirilemedi (HTTP {(int)response.StatusCode}).");
            var mime = response.Content.Headers.ContentType?.MediaType;
            if (mime is null || !AllowedMimeTypes.Contains(mime)) throw Invalid("Görsel MIME türü desteklenmiyor.");
            if (response.Content.Headers.ContentLength > options.MaxBytes) throw Invalid("Görsel dosyası boyut sınırını aşıyor.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var header = new byte[12];
            var read = 0;
            while (read < header.Length)
            {
                var count = await stream.ReadAsync(header.AsMemory(read, header.Length - read), cancellationToken);
                if (count == 0) break;
                read += count;
            }
            if (!MatchesMime(header.AsSpan(0, read), mime)) throw Invalid("Görsel içeriği bildirilen MIME türüyle eşleşmiyor.");
            long total = read;
            var buffer = new byte[81920];
            while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > options.MaxBytes) throw Invalid("Görsel dosyası boyut sınırını aşıyor.");
            }
            return current;
        }
        throw Invalid("Görsel doğrulanamadı.");
    }

    private async Task<IPAddress[]> ValidateUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(uri.Host) ||
            !options.AllowedHosts.Any(x => uri.Host.Equals(x, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith('.' + x, StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid("Görsel alan adı izinli Canvasia domain listesinde değil.");
        }
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw Invalid("Görsel alan adı çözümlenemedi.", exception);
        }
        if (addresses.Length == 0 || addresses.Any(IsPrivate)) throw Invalid("Görsel adresi private veya local bir IP'ye yönleniyor.");
        return addresses;
    }

    private static SocketsHttpHandler CreatePinnedHandler(IReadOnlyList<IPAddress> addresses) => new()
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseProxy = false,
        ConnectCallback = async (context, cancellationToken) =>
        {
            Exception? lastError = null;
            foreach (var address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (Exception exception) when (exception is SocketException or OperationCanceledException)
                {
                    socket.Dispose();
                    lastError = exception;
                    if (exception is OperationCanceledException) throw;
                }
            }
            throw new HttpRequestException("Doğrulanmış görsel adresine bağlanılamadı.", lastError);
        }
    };

    private static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return true;
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast ||
                   (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        }
        var bytes = address.GetAddressBytes();
        return bytes[0] is 0 or 10 or 127 ||
               bytes[0] == 169 && bytes[1] == 254 ||
               bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
               bytes[0] == 192 && bytes[1] == 168 ||
               bytes[0] == 100 && bytes[1] is >= 64 and <= 127 ||
               bytes[0] == 198 && bytes[1] is 18 or 19 ||
               bytes[0] == 192 && bytes[1] == 0 ||
               bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2 ||
               bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100 ||
               bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113 ||
               bytes[0] >= 224;
    }

    private static bool MatchesMime(ReadOnlySpan<byte> bytes, string mime) => mime.ToLowerInvariant() switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        _ => false
    };
    private static bool IsRedirect(HttpStatusCode status) => status is HttpStatusCode.Moved or HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    private static SocialPublisherException Invalid(string message, Exception? inner = null) =>
        new(message, SocialPublishFailureKind.InvalidContent, innerException: inner);
}

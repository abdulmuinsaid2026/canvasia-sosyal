using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Infrastructure.Social;

namespace CanvasiaSocial.IntegrationTests;

public sealed class SecureImageServiceTests
{
    [Theory]
    [InlineData("file:///etc/passwd", "canvasia.com.tr")]
    [InlineData("https://example.com/image.jpg", "canvasia.com.tr")]
    [InlineData("http://127.0.0.1/image.jpg", "127.0.0.1")]
    [InlineData("http://localhost/image.jpg", "localhost")]
    public async Task Rejects_unsafe_image_locations(string url, string allowedHost)
    {
        var service = new SecureImageService(new SecureImageOptions { AllowedHosts = [allowedHost] });

        var exception = await Assert.ThrowsAsync<SocialPublisherException>(() => service.ValidateAndPrepareAsync(url));

        Assert.Equal(SocialPublishFailureKind.InvalidContent, exception.Kind);
    }
}

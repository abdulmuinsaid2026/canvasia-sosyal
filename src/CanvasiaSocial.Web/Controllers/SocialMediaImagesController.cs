using CanvasiaSocial.Application.Social;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace CanvasiaSocial.Web.Controllers;

[ApiController]
[AllowAnonymous]
[Route("SocialMedia/Images")]
public sealed class SocialMediaImagesController(
    ApplicationDbContext dbContext,
    ISocialImageTokenService tokens,
    ISecureImageService imageService) : ControllerBase
{
    [HttpGet("{id:guid}.jpg")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> InstagramJpeg(Guid id, string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || !tokens.IsValidInstagramJpegToken(id, token)) return NotFound();
        var imageUrl = await dbContext.ScheduledPosts.AsNoTracking().Where(x => x.Id == id)
            .SelectMany(x => x.GeneratedContent.ProductCache.Images)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SortOrder)
            .Select(x => x.Url).FirstOrDefaultAsync(cancellationToken);
        if (imageUrl is null) return NotFound();

        var source = await imageService.DownloadAsync(imageUrl, cancellationToken);
        var imageInfo = Image.Identify(source.Content);
        if (imageInfo is null || (long)imageInfo.Width * imageInfo.Height > 25_000_000) return UnprocessableEntity();
        using var image = Image.Load(source.Content);
        image.Mutate(context =>
        {
            context.AutoOrient();
            var ratio = (double)image.Width / image.Height;
            if (ratio < 0.8)
            {
                var height = (int)Math.Round(image.Width / 0.8);
                context.Crop(new Rectangle(0, Math.Max(0, (image.Height - height) / 2), image.Width, height));
            }
            else if (ratio > 1.91)
            {
                var width = (int)Math.Round(image.Height * 1.91);
                context.Crop(new Rectangle(Math.Max(0, (image.Width - width) / 2), 0, width, image.Height));
            }
            if (image.Width is < 320 or > 1440)
            {
                var width = Math.Clamp(image.Width, 320, 1440);
                context.Resize(width, 0);
            }
        });
        await using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 90 }, cancellationToken);
        return File(output.ToArray(), "image/jpeg");
    }
}

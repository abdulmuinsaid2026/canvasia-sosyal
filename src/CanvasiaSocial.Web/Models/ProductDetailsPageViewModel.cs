using CanvasiaSocial.Application.Ai;
using CanvasiaSocial.Application.Products;
using CanvasiaSocial.Domain.Enums;

namespace CanvasiaSocial.Web.Models;

public sealed record ProductDetailsPageViewModel(ProductDetails Product, IReadOnlyList<GeneratedContentView> Contents)
{
    public Platform Platform { get; init; } = Platform.Instagram;
}

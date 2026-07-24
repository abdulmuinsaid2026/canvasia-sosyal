using CanvasiaSocial.Domain.Entities;
using CanvasiaSocial.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanvasiaSocial.IntegrationTests;

public sealed class ApplicationDbContextModelTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=postgres")
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public void ProductCache_CanvasiaProductId_HasUniqueIndex()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ProductCache));

        var index = Assert.Single(entity!.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([nameof(ProductCache.CanvasiaProductId)]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void ScheduledPost_IdempotencyKey_HasUniqueIndex()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ScheduledPost));

        var index = Assert.Single(entity!.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([nameof(ScheduledPost.IdempotencyKey)]));

        Assert.True(index.IsUnique);
    }
}

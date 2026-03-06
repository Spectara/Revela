using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.IntegrationTests;

/// <summary>
/// Integration tests for <see cref="IContentService"/> scanning real
/// project directories created by <see cref="TestProject"/>.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ContentServiceScanTests
{
    private static void AddServices(IServiceCollection services)
    {
        services.AddRevelaCommands();

        // Override IImageSizesProvider since we don't have a real theme installed
        services.AddSingleton<IImageSizesProvider>(new TestImageSizesProvider());
    }

    [TestMethod]
    public async Task ScanAsync_EmptySource_ReturnsSuccessWithZeroCounts()
    {
        // Arrange
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var result = await contentService.ScanAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.GalleryCount);
        Assert.AreEqual(0, result.ImageCount);
    }

    [TestMethod]
    public async Task ScanAsync_SingleGalleryWithImages_FindsGallery()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .AddGallery("Landscapes", g => g
                .AddImages(3)
                .WithMarkdown("# Landscapes\nBeautiful scenery")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var result = await contentService.ScanAsync();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.GalleryCount);
        Assert.AreEqual(3, result.ImageCount);
    }

    [TestMethod]
    public async Task ScanAsync_MultipleGalleries_FindsAll()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .AddGallery("Landscapes", g => g.AddImages(2))
            .AddGallery("Portraits", g => g.AddImages(1))
            .AddGallery("Street", g => g.AddImages(4)));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var result = await contentService.ScanAsync();

        // Assert
        Assert.IsTrue(result.Success);
        Assert.AreEqual(3, result.GalleryCount);
        Assert.AreEqual(7, result.ImageCount);
    }

    [TestMethod]
    public async Task ScanAsync_EmptyGalleryNoImages_IsExcluded()
    {
        // Arrange: Gallery with markdown but no images
        using var project = TestProject.Create(p => p
            .AddGallery("Empty", g => g.WithMarkdown("# Empty")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var result = await contentService.ScanAsync();

        // Assert: Empty galleries (no images) should be excluded
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.GalleryCount);
    }

    [TestMethod]
    public async Task ScanAsync_CreatesNavigationItems()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .AddGallery("Nature", g => g.AddImage("tree.jpg"))
            .AddGallery("Urban", g => g.AddImage("city.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var result = await contentService.ScanAsync();

        // Assert: Each gallery should contribute to navigation
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.NavigationItemCount > 0,
            "Expected navigation items to be created for galleries");
    }

    /// <summary>
    /// Minimal IImageSizesProvider for tests without a real theme.
    /// </summary>
    private sealed class TestImageSizesProvider : IImageSizesProvider
    {
        private static readonly IReadOnlyList<int> DefaultSizes = [320, 640, 1280, 1920];

        public IReadOnlyList<int> GetSizes() => DefaultSizes;

        public string GetResizeMode() => "longest";
    }
}

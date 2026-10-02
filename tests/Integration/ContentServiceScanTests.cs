using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

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
        services.AddGenerateFeature();

        // Register a base theme so the scan step's theme pre-check resolves.
        services.AddSingleton<ITheme>(new LuminaTheme());

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
    public async Task ScanAsync_RootWithoutTitle_UsesSiteTitleInsteadOfEnglishHome()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Mein Portfolio" })
            .AddGallery("Photos", g => g.AddImage("photo.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var result = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual("Mein Portfolio", host.Services.GetRequiredService<IManifestRepository>().Root?.Text);
    }

    [TestMethod]
    public async Task ScanAsync_RootWithoutTitleAndSiteTitle_UsesProjectFolderName()
    {
        using var project = TestProject.Create(p => p.AddGallery("Photos", g => g.AddImage("photo.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var result = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(
            Path.GetFileName(project.RootPath.TrimEnd(Path.DirectorySeparatorChar)),
            host.Services.GetRequiredService<IManifestRepository>().Root?.Text);
    }

    [TestMethod]
    public async Task ScanAsync_FolderSortedByFieldSomePhotosLack_PutsThemLast()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g
                .AddRealImage("a-no-iso.jpg", 64, 48)
                .AddRealImage("low.jpg", 64, 48, exif => exif.WithIso(100))
                .AddRealImage("high.jpg", 64, 48, exif => exif.WithIso(3200))));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Photos", "_index.revela"),
            "+++\nsort = \"exif.iso:desc\"\n+++\n");
        using var host = RevelaTestHost.Build(project.RootPath, AddServices);

        var result = await host.Services.GetRequiredService<IContentService>().ScanAsync();

        Assert.IsTrue(result.Success, result.ErrorMessage);
        var photos = host.Services.GetRequiredService<IManifestRepository>().Root!.Children.Single();
        Assert.AreEqual("high.jpg,low.jpg,a-no-iso.jpg", string.Join(',', photos.Content.Select(content => content.Filename)));
    }

    [TestMethod]
    public async Task ScanAsync_ManifestFromOlderMetadataVersion_RereadsImageMetadata()
    {
        // A manifest written by an older Revela carries metadata computed by an older
        // pipeline (e.g. un-rotated dimensions before #98, non-sRGB placeholders).
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 800, 600)));
        using (var host = RevelaTestHost.Build(project.RootPath, AddServices))
        {
            var first = await host.Services.GetRequiredService<IContentService>().ScanAsync();
            Assert.IsTrue(first.Success, first.ErrorMessage);
        }

        var manifestPath = Path.Combine(project.RootPath, ".cache", "manifest.json");
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!.AsObject();
        manifest["_meta"]!["scanConfigHash"] = LegacyScanConfigHash("CssHash", 0, 0);
        var image = manifest["root"]!["children"]![0]!["content"]!.AsArray().Single(c => (string?)c!["filename"] == "photo.jpg")!;
        image["width"] = 600;
        image["height"] = 800;
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());

        using (var host = RevelaTestHost.Build(project.RootPath, AddServices))
        {
            var second = await host.Services.GetRequiredService<IContentService>().ScanAsync();
            Assert.IsTrue(second.Success, second.ErrorMessage);

            var rescanned = host.Services.GetRequiredService<IManifestRepository>().Images.Values.Single();
            Assert.AreEqual(800, rescanned.Width, "Stale cached metadata must be re-read from the source file.");
            Assert.AreEqual(600, rescanned.Height);
        }
    }

    /// <summary>
    /// Scan cache key as written by Revela up to v0.0.1-beta.20 (no metadata version).
    /// </summary>
    private static string LegacyScanConfigHash(string placeholderStrategy, int minWidth, int minHeight)
    {
        var input = $"placeholder:{placeholderStrategy}|minWidth:{minWidth}|minHeight:{minHeight}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12];
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


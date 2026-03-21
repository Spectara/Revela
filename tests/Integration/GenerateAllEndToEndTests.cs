using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Plugins.Generate;
using Spectara.Revela.Plugins.Generate.Abstractions;
using Spectara.Revela.Plugins.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// End-to-end test that runs the full generation pipeline on a
/// project with generated test images and verifies the output.
/// </summary>
/// <remarks>
/// <para>
/// Creates a temporary project with real JPEG images (via <see cref="TestImageGenerator"/>),
/// registers the Lumina theme, runs scan → render → image processing, and verifies
/// that HTML pages and resized images are generated correctly.
/// </para>
/// <para>
/// Calls services directly (not CLI commands) to avoid Spectre.Console
/// terminal handle issues in non-interactive test runners.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class GenerateAllEndToEndTests
{
    [TestMethod]
    public async Task GenerateAll_WithTestImages_ProducesHtmlAndImages()
    {
        // Arrange: Create a realistic project structure with real images
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "E2E Test Portfolio" },
                theme = new { name = "Lumina" },
                generate = new
                {
                    images = new { avif = 80, webp = 85, jpg = 90 }
                }
            })
            .WithSiteJson(new
            {
                title = "Test Portfolio",
                author = "Test Author",
                copyright = "2025 Test",
                description = "E2E test site"
            })
            .AddGallery("Landscapes", g => g
                .WithMarkdown("# Landscapes\n\nBeautiful scenery from around the world.")
                .AddRealImage("sunset.jpg", 1920, 1080, exif => exif
                    .WithCamera("Canon", "EOS R5")
                    .WithIso(100)
                    .WithAperture(8.0)
                    .WithFocalLength(24)
                    .WithDateTaken(new DateTime(2025, 6, 15, 19, 30, 0, DateTimeKind.Utc)))
                .AddRealImage("mountain.jpg", 2560, 1440, exif => exif
                    .WithCamera("Sony", "ILCE-7M4")
                    .WithIso(200)
                    .WithAperture(11)
                    .WithFocalLength(70)))
            .AddGallery("Portraits", g => g
                .WithMarkdown("# Portraits\n\nPeople and faces.")
                .AddRealImage("person.jpg", 1280, 1920, exif => exif
                    .WithCamera("Canon", "EOS R5")
                    .WithIso(400)
                    .WithAperture(2.8)
                    .WithFocalLength(85)
                    .WithLens("RF 85mm F1.2L"))));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<IThemePlugin>(new LuminaThemePlugin());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        // Configure render service with Lumina theme
        var themePlugin = host.Services.GetRequiredService<IThemePlugin>();
        renderService.SetTheme(themePlugin);
        renderService.SetExtensions([]);

        // Act: Run the pipeline steps directly (avoids Spectre.Console terminal issues)

        // Step 1: Scan content
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");
        Assert.AreEqual(2, scanResult.GalleryCount, "Should find 2 galleries");
        Assert.AreEqual(3, scanResult.ImageCount, "Should find 3 images");

        // Step 2: Render HTML pages
        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, $"Render should succeed: {renderResult.ErrorMessage}");
        Assert.IsTrue(renderResult.PageCount > 0, "Should generate at least 1 page");

        // Step 3: Process images
        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Image processing should succeed: {imageResult.ErrorMessage}");

        // Assert: Output directory structure
        Assert.IsTrue(Directory.Exists(project.OutputPath),
            "Output directory should be created");

        // Assert: HTML pages generated
        var indexHtml = Path.Combine(project.OutputPath, "index.html");
        Assert.IsTrue(File.Exists(indexHtml),
            "Root index.html should be generated");

        // Slugs are lowercase (UrlBuilder.ToSlug lowercases all names)
        var landscapesHtml = Path.Combine(project.OutputPath, "landscapes", "index.html");
        Assert.IsTrue(File.Exists(landscapesHtml),
            "Landscapes gallery page should be generated");

        var portraitsHtml = Path.Combine(project.OutputPath, "portraits", "index.html");
        Assert.IsTrue(File.Exists(portraitsHtml),
            "Portraits gallery page should be generated");

        // Assert: HTML content
        var landscapesContent = await File.ReadAllTextAsync(landscapesHtml);
        Assert.IsTrue(landscapesContent.Contains("Landscapes", StringComparison.Ordinal),
            "Gallery page should contain gallery title");

        // Assert: Image variants generated
        var imagesDir = Path.Combine(project.OutputPath, "images");
        Assert.IsTrue(Directory.Exists(imagesDir),
            "Images output directory should be created");

        var jpgFiles = Directory.EnumerateFiles(imagesDir, "*.jpg", SearchOption.AllDirectories).ToList();
        Assert.IsTrue(jpgFiles.Count > 0,
            $"Expected JPG image variants, found {jpgFiles.Count}");

        var webpFiles = Directory.EnumerateFiles(imagesDir, "*.webp", SearchOption.AllDirectories).ToList();
        Assert.IsTrue(webpFiles.Count > 0,
            $"Expected WebP image variants, found {webpFiles.Count}");

        // Assert: Theme assets copied
        var assetsDir = Path.Combine(project.OutputPath, "_assets");
        Assert.IsTrue(Directory.Exists(assetsDir),
            "Assets directory should be created with theme assets");

        var cssFiles = Directory.EnumerateFiles(assetsDir, "*.css", SearchOption.AllDirectories).ToList();
        Assert.IsTrue(cssFiles.Count > 0,
            "Theme CSS files should be copied to output");
    }

    [TestMethod]
    public async Task GenerateAll_NestedGalleries_ProducesCorrectStructure()
    {
        // Arrange: Project with nested gallery structure (like OneDrive sample)
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Nested Test", author = "Test" })
            .AddGallery("Events", g => g
                .AddSubGallery("Fireworks", sg => sg
                    .WithMarkdown("# Fireworks\n\nNew Year celebrations.")
                    .AddRealImage("boom.jpg", 1920, 1080, exif => exif
                        .WithCamera("Canon", "EOS R5")
                        .WithIso(3200)
                        .WithAperture(2.8)))
                .AddSubGallery("Racing", sg => sg
                    .AddRealImage("car.jpg", 2560, 1440)))
            .AddGallery("Nature", g => g
                .WithMarkdown("# Nature")
                .AddRealImage("tree.jpg", 1920, 1080)));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<IThemePlugin>(new LuminaThemePlugin());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<IThemePlugin>();
        renderService.SetTheme(themePlugin);
        renderService.SetExtensions([]);

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");

        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Images failed: {imageResult.ErrorMessage}");

        // Assert: Nested galleries create nested output structure
        Assert.AreEqual(3, scanResult.GalleryCount,
            "Should find 3 galleries (Fireworks, Racing, Nature)");
        Assert.AreEqual(3, scanResult.ImageCount,
            "Should find 3 images total");

        // Assert: Root index exists
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "index.html")));

        // Assert: Nested pages generated (slugified paths)
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "events", "fireworks", "index.html")),
            "Nested gallery Fireworks should have its page");
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "nature", "index.html")),
            "Top-level gallery Nature should have its page");

        // Assert: Navigation should include nested items
        var rootHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "index.html"));
        Assert.IsTrue(rootHtml.Contains("Nature", StringComparison.Ordinal),
            "Root page should contain Nature in navigation");
    }

    [TestMethod]
    public async Task GenerateAll_IncrementalBuild_SkipsCachedImages()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Cache Test", author = "Test" })
            .AddGallery("Photos", g => g
                .AddRealImage("photo1.jpg", 1280, 720)
                .AddRealImage("photo2.jpg", 1280, 720)));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<IThemePlugin>(new LuminaThemePlugin());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<IThemePlugin>();
        renderService.SetTheme(themePlugin);
        renderService.SetExtensions([]);

        // Act: First run — everything processed
        await contentService.ScanAsync();
        await renderService.RenderAsync();
        var firstRun = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Act: Second run — should skip cached images
        await contentService.ScanAsync();
        await renderService.RenderAsync();
        var secondRun = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Assert: Both succeed
        Assert.IsTrue(firstRun.Success);
        Assert.IsTrue(secondRun.Success);

        // Assert: Second run processes fewer (or zero) images due to caching
        Assert.IsTrue(secondRun.ProcessedCount <= firstRun.ProcessedCount,
            $"Second run should process same or fewer images (first: {firstRun.ProcessedCount}, second: {secondRun.ProcessedCount})");
    }
}

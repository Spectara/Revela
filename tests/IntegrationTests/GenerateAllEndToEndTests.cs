using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Theme.Lumina;

namespace Spectara.Revela.IntegrationTests;

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
}

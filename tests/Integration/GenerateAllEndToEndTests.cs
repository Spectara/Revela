using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
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
    public async Task GenerateAll_ImagelessSite_ImageStepIsSuccessfulNoOp()
    {
        // Arrange: a photo-less site (e.g. calendar-only) with no galleries, no images,
        // and no image formats configured. The image step must be a successful no-op
        // rather than failing the build on the "no formats configured" guard.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Imageless" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Imageless", author = "Test" }));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();
        var themePlugin = host.Services.GetRequiredService<ITheme>();
        renderService.SetTheme(themePlugin);
        renderService.SetExtensions([]);

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");
        await renderService.RenderAsync();
        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Assert: no images to process → success with zero processed, no error
        Assert.IsTrue(imageResult.Success,
            $"Image step must succeed on an image-less site, not fail: {imageResult.ErrorMessage}");
        Assert.AreEqual(0, imageResult.ProcessedCount, "No images means nothing processed");
        Assert.IsNull(imageResult.ErrorMessage, "Image-less site must not surface an error");
    }

    [TestMethod]
    public async Task GenerateAll_RootOnlySite_CountsIndexExactlyOnce()
    {
        // Arrange: a site with no galleries — only the root index page (#99 regression)
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Root Only" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Root Only", author = "Test" }));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var themePlugin = host.Services.GetRequiredService<ITheme>();
        renderService.SetTheme(themePlugin);
        renderService.SetExtensions([]);

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");

        var renderResult = await renderService.RenderAsync();

        // Assert: the root index page contributes exactly one to the count
        Assert.IsTrue(renderResult.Success, $"Render should succeed: {renderResult.ErrorMessage}");
        Assert.AreEqual(1, renderResult.PageCount,
            "A root-only site renders exactly one page (the index, counted once — see #99)");
    }

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
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        // Configure render service with Lumina theme
        var themePlugin = host.Services.GetRequiredService<ITheme>();
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
        Assert.AreEqual(6, renderResult.PageCount,
            "Root index + 2 galleries + 3 photo pages = 6 (index counted once, see #99/#77)");

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

        // Assert: photo pages (#77) — one canonical page per published source image.
        var sunsetPhoto = Path.Combine(project.OutputPath, "photo", "landscapes", "sunset", "index.html");
        Assert.IsTrue(File.Exists(sunsetPhoto), "Photo page for sunset.jpg should be generated");

        var sunsetPhotoContent = await File.ReadAllTextAsync(sunsetPhoto);
        Assert.Contains("/photo/landscapes/sunset/", sunsetPhotoContent);
        Assert.Contains("rel=\"canonical\"", sunsetPhotoContent);
        // up returns to the originating gallery occurrence via the #photo-* anchor.
        Assert.Contains("#photo-landscapes-sunset", sunsetPhotoContent);
        // no wraparound: the first image in the gallery has a next but no previous link.
        Assert.Contains("photo/landscapes/mountain/", sunsetPhotoContent);

        // Assert: the Lumina gallery no longer emits an inline lightbox figure (#77).
        Assert.IsFalse(landscapesContent.Contains("<figure", StringComparison.Ordinal),
            "Gallery markup must not contain the removed inline lightbox figure");
        Assert.Contains("/photo/landscapes/sunset/", landscapesContent);
    }

    [TestMethod]
    public async Task GenerateAll_CollidingGalleryFolders_ScanFailsAndWritesNoOutput()
    {
        // Arrange: "01 Events" and "02 Events" both normalize to the slug "events", and each
        // holds a "photo.jpg" — so both the gallery page and the image variants would target the
        // same output paths. The scan must reject this before anything is written (#97).
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Colliding" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Colliding Site", author = "Test" })
            .AddGallery("01 Events", g => g.AddImage("photo.jpg"))
            .AddGallery("02 Events", g => g.AddImage("photo.jpg")));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act: the scan step is the gate — it must fail before rendering.
        var scanResult = await contentService.ScanAsync();

        // Assert: scan failed and the error names the slug plus every conflicting source path.
        Assert.IsFalse(scanResult.Success, "Scan must fail when two galleries collide to one slug.");
        Assert.IsNotNull(scanResult.ErrorMessage);
        Assert.Contains("events", scanResult.ErrorMessage);
        Assert.Contains("01 Events", scanResult.ErrorMessage);
        Assert.Contains("02 Events", scanResult.ErrorMessage);

        // Assert: nothing was written — no gallery page could silently overwrite another.
        var writtenHtml = Directory.Exists(project.OutputPath)
            ? Directory.EnumerateFiles(project.OutputPath, "*.html", SearchOption.AllDirectories).ToList()
            : [];
        Assert.IsEmpty(writtenHtml, "A failed scan must not write any output.");
    }

    [TestMethod]
    public async Task GenerateAll_GalleryNameNormalizingToEmptySlug_ScanFails()
    {
        // Arrange: a folder whose name consists only of removed characters → empty slug,
        // which would collide with the site root (#97).
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Empty Slug" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Empty Slug Site", author = "Test" })
            .AddGallery("!!!", g => g.AddImage("photo.jpg")));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var scanResult = await contentService.ScanAsync();

        // Assert
        Assert.IsFalse(scanResult.Success, "Scan must fail on an empty gallery slug.");
        Assert.IsNotNull(scanResult.ErrorMessage);
        Assert.Contains("!!!", scanResult.ErrorMessage);

        var writtenHtml = Directory.Exists(project.OutputPath)
            ? Directory.EnumerateFiles(project.OutputPath, "*.html", SearchOption.AllDirectories).ToList()
            : [];
        Assert.IsEmpty(writtenHtml, "A failed scan must not write any output.");
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
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<ITheme>();
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
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<ITheme>();
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

    [TestMethod]
    public async Task GenerateAll_DuplicateFilenames_CreatesDistinctOutputPaths()
    {
        // Arrange: Two galleries with the same image filename (e.g., camera numbering "001.jpg")
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Duplicate Test", author = "Test" })
            .AddGallery("Gallery A", g => g
                .AddRealImage("001.jpg", 1920, 1080, exif => exif
                    .WithCamera("Canon", "EOS R5")))
            .AddGallery("Gallery B", g => g
                .AddRealImage("001.jpg", 2560, 1440, exif => exif
                    .WithCamera("Sony", "A7R V"))));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<ITheme>();
        renderService.SetTheme(themePlugin);
        renderService.SetExtensions([]);

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");

        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Images failed: {imageResult.ErrorMessage}");

        // Assert: Both images should be processed (not overwritten)
        Assert.AreEqual(2, scanResult.ImageCount,
            "Should find 2 images (one per gallery)");

        // Assert: Distinct output directories for each image
        var imagesDir = Path.Combine(project.OutputPath, "images");
        var galleryADir = Path.Combine(imagesDir, "gallery-a", "001");
        var galleryBDir = Path.Combine(imagesDir, "gallery-b", "001");

        Assert.IsTrue(Directory.Exists(galleryADir),
            $"Gallery A image directory should exist at: gallery-a/001");
        Assert.IsTrue(Directory.Exists(galleryBDir),
            $"Gallery B image directory should exist at: gallery-b/001");

        // Assert: Both directories contain image variants
        var galleryAFiles = Directory.GetFiles(galleryADir).Length;
        var galleryBFiles = Directory.GetFiles(galleryBDir).Length;

        Assert.IsTrue(galleryAFiles > 0,
            "Gallery A should have image variants");
        Assert.IsTrue(galleryBFiles > 0,
            "Gallery B should have image variants");
    }

    [TestMethod]
    public async Task GeneratePages_InlineGallery_RendersMidContentAndSuppressesTrailingGrid()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Inline Gallery Test", author = "Test" })
            .AddGallery("Inline Gallery", g => g
                .AddRealImage("first.jpg", 1920, 1080)
                .AddRealImage("second.jpg", 1920, 1080))
            .AddGallery("Default Gallery", g => g
                .AddRealImage("default.jpg", 1920, 1080)));

        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Inline Gallery", "_index.revela"),
            "Before inline gallery.\n\n[[gallery]]\n\nAfter inline gallery.");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Default Gallery", "_index.revela"),
            "Default gallery body.");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var theme = host.Services.GetRequiredService<ITheme>();
        renderService.SetTheme(theme);
        renderService.SetExtensions([]);

        // Act
        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var inlineHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "inline-gallery", "index.html"));
        var beforePosition = inlineHtml.IndexOf("Before inline gallery.", StringComparison.Ordinal);
        var gridPosition = inlineHtml.IndexOf("<section class=\"gallery\">", StringComparison.Ordinal);
        var afterPosition = inlineHtml.IndexOf("After inline gallery.", StringComparison.Ordinal);

        Assert.IsTrue(beforePosition < gridPosition, "Inline grid should render after preceding content.");
        Assert.IsTrue(gridPosition < afterPosition, "Inline grid should render before following content.");
        Assert.AreEqual(1, CountOccurrences(inlineHtml, "<section class=\"gallery\">"),
            "Inline page must not render the automatic trailing grid.");
        Assert.DoesNotContain("[[gallery]]", inlineHtml);

        var defaultHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "default-gallery", "index.html"));
        Assert.AreEqual(1, CountOccurrences(defaultHtml, "<section class=\"gallery\">"),
            "A page without a token must retain its automatic trailing grid.");
    }

    [TestMethod]
    public async Task GeneratePages_ThemeWithoutGalleryGrid_OnlyFailsWhenTokenIsPresent()
    {
        // Arrange: first prove a no-token page remains unaffected.
        using var noTokenProject = TestProject.Create(p => p
            .WithSiteJson(new { title = "No Token", author = "Test" })
            .AddGallery("Gallery", g => g.AddRealImage("photo.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(noTokenProject.SourcePath, "Gallery", "_index.revela"),
            "Body without an inline gallery.");

        using var noTokenHost = RevelaTestHost.Build(noTokenProject.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new ThemeWithoutGalleryGrid());
        });
        var noTokenContentService = noTokenHost.Services.GetRequiredService<IContentService>();
        var noTokenRenderService = noTokenHost.Services.GetRequiredService<IRenderService>();
        await noTokenContentService.ScanAsync();

        // Act
        var noTokenResult = await noTokenRenderService.RenderAsync();

        // Assert
        Assert.IsTrue(noTokenResult.Success, noTokenResult.ErrorMessage);

        // Arrange: the same theme with an inline token must require the missing partial.
        using var tokenProject = TestProject.Create(p => p
            .WithSiteJson(new { title = "With Token", author = "Test" })
            .AddGallery("Gallery", g => g.AddRealImage("photo.jpg", 1920, 1080)));
        var sourcePath = Path.Combine(tokenProject.SourcePath, "Gallery", "_index.revela");
        await File.WriteAllTextAsync(sourcePath, "Before.\n\n[[gallery]]");

        using var tokenHost = RevelaTestHost.Build(tokenProject.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new ThemeWithoutGalleryGrid());
        });
        var tokenContentService = tokenHost.Services.GetRequiredService<IContentService>();
        var tokenRenderService = tokenHost.Services.GetRequiredService<IRenderService>();
        await tokenContentService.ScanAsync();

        // Act
        var tokenResult = await tokenRenderService.RenderAsync();

        // Assert
        Assert.IsFalse(tokenResult.Success);
        Assert.IsNotNull(tokenResult.ErrorMessage);
        Assert.Contains($"{sourcePath}:3:", tokenResult.ErrorMessage);
        Assert.Contains("Partials/GalleryGrid.revela", tokenResult.ErrorMessage);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(search, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += search.Length;
        }

        return count;
    }

    private sealed class ThemeWithoutGalleryGrid : ITheme
    {
        private const string GalleryGridPath = "Partials/GalleryGrid.revela";
        private readonly LuminaTheme inner = new();

        public PackageMetadata Metadata => inner.Metadata;

        public string? Prefix => inner.Prefix;

        public string? TargetTheme => inner.TargetTheme;

        public ThemeManifest Manifest => inner.Manifest;

        public Stream? GetFile(string relativePath) =>
            IsGalleryGrid(relativePath) ? null : inner.GetFile(relativePath);

        public IEnumerable<string> GetAllFiles() =>
            inner.GetAllFiles().Where(file => !IsGalleryGrid(file));

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            inner.ExtractToAsync(targetDirectory, cancellationToken);

        public Stream? GetSiteTemplate() => inner.GetSiteTemplate();

        public Stream? GetImagesTemplate() => inner.GetImagesTemplate();

        public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey) =>
            inner.GetTemplateDataDefaults(templateKey);

        private static bool IsGalleryGrid(string path) =>
            path.Replace('\\', '/').Equals(GalleryGridPath, StringComparison.OrdinalIgnoreCase);
    }
}

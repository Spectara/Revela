using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Parsing;
using Spectara.Revela.Commands.Generate.Scanning;
using Spectara.Revela.Commands.Generate.Services;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Commands.Tests.Generate.Services;

/// <summary>
/// Integration tests verifying that shared <c>_images/</c> images are correctly
/// included in the manifest tree and available for processing.
/// </summary>
/// <remarks>
/// These tests use real <see cref="ContentScanner"/> and <see cref="NavigationBuilder"/>
/// against a temporary file system, with only <see cref="IImageProcessor"/> mocked.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public sealed class SharedImagesTests : IDisposable
{
    private string tempDir = null!;
    private string sourceDir = null!;

    [TestInitialize]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "revela-test-" + Guid.NewGuid().ToString("N")[..8]);
        sourceDir = Path.Combine(tempDir, "source");
        Directory.CreateDirectory(sourceDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    public void Dispose() => Cleanup();

    [TestMethod]
    public async Task ScanAsync_SharedImagesInImagesFolder_AppearInManifestTree()
    {
        // Arrange: Create source structure with _images/
        // source/
        //   _index.revela
        //   _images/
        //     photo-a.jpg
        //     screenshots/
        //       screenshot-b.jpg
        CreateRevelaIndex(sourceDir, "Home");
        var imagesDir = Path.Combine(sourceDir, "_images");
        Directory.CreateDirectory(imagesDir);
        CreateDummyJpeg(Path.Combine(imagesDir, "photo-a.jpg"));
        var screenshotsDir = Path.Combine(imagesDir, "screenshots");
        Directory.CreateDirectory(screenshotsDir);
        CreateDummyJpeg(Path.Combine(screenshotsDir, "screenshot-b.jpg"));

        var (service, manifestRepo) = CreateContentService();

        // Act
        var result = await service.ScanAsync();

        // Assert: scan succeeded with 2 images
        Assert.IsTrue(result.Success, $"Scan failed: {result.ErrorMessage}");
        Assert.AreEqual(2, result.ImageCount);

        // Verify _images node exists in tree as hidden child
        var root = manifestRepo.Root;
        Assert.IsNotNull(root);

        var imagesNode = root.Children.FirstOrDefault(c => c.Path == ProjectPaths.SharedImages);
        Assert.IsNotNull(imagesNode, "Expected a hidden _images node in root.Children");
        Assert.IsTrue(imagesNode.Hidden, "_images node should be hidden");
        Assert.IsNull(imagesNode.Slug, "_images node should have no slug (not a gallery)");

        // Verify both images are in the _images node content
        var imageFilenames = imagesNode.Content
            .OfType<ImageContent>()
            .Select(img => img.Filename)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        Assert.HasCount(2, imageFilenames);
        Assert.AreEqual("photo-a.jpg", imageFilenames[0]);
        Assert.AreEqual("screenshot-b.jpg", imageFilenames[1]);
    }

    [TestMethod]
    public async Task ScanAsync_SharedImages_HaveCorrectSourcePaths()
    {
        // Arrange
        CreateRevelaIndex(sourceDir, "Home");
        var screenshotsDir = Path.Combine(sourceDir, "_images", "screenshots");
        Directory.CreateDirectory(screenshotsDir);
        CreateDummyJpeg(Path.Combine(screenshotsDir, "wizard.jpg"));

        var (service, manifestRepo) = CreateContentService();

        // Act
        await service.ScanAsync();

        // Assert: SourcePath should include the full relative path from source root
        var root = manifestRepo.Root!;
        var imagesNode = root.Children.First(c => c.Path == ProjectPaths.SharedImages);
        var image = imagesNode.Content.OfType<ImageContent>().Single();

        Assert.AreEqual("wizard.jpg", image.Filename);
        Assert.AreEqual("_images/screenshots/wizard.jpg", image.SourcePath);
    }

    [TestMethod]
    public async Task ScanAsync_SharedImages_AvailableViaManifestRepositoryImages()
    {
        // Arrange
        CreateRevelaIndex(sourceDir, "Home");
        var imagesDir = Path.Combine(sourceDir, "_images");
        Directory.CreateDirectory(imagesDir);
        CreateDummyJpeg(Path.Combine(imagesDir, "shared-photo.jpg"));

        var (service, manifestRepo) = CreateContentService();

        // Act
        await service.ScanAsync();

        // Assert: manifestRepository.Images should contain the shared image
        var images = manifestRepo.Images;
        Assert.IsNotEmpty(images);
        Assert.IsTrue(
            images.ContainsKey("_images/shared-photo.jpg"),
            $"Expected key '_images/shared-photo.jpg' in Images. Found: {string.Join(", ", images.Keys)}");
    }

    [TestMethod]
    public async Task ScanAsync_NoSharedImages_NoImagesNodeInTree()
    {
        // Arrange: source with only a gallery, no _images/
        CreateRevelaIndex(sourceDir, "Home");
        var galleryDir = Path.Combine(sourceDir, "landscapes");
        Directory.CreateDirectory(galleryDir);
        CreateRevelaIndex(galleryDir, "Landscapes");
        CreateDummyJpeg(Path.Combine(galleryDir, "sunset.jpg"));

        var (service, manifestRepo) = CreateContentService();

        // Act
        await service.ScanAsync();

        // Assert: no _images node
        var root = manifestRepo.Root!;
        var imagesNode = root.Children.FirstOrDefault(c => c.Path == ProjectPaths.SharedImages);
        Assert.IsNull(imagesNode, "Should not have _images node when no shared images exist");
    }

    [TestMethod]
    public async Task ScanAsync_SharedImagesAndGalleryImages_BothIncluded()
    {
        // Arrange: source with both gallery images and shared images
        // source/
        //   _index.revela
        //   _images/
        //     shared.jpg
        //   landscapes/
        //     _index.revela
        //     sunset.jpg
        CreateRevelaIndex(sourceDir, "Home");

        var imagesDir = Path.Combine(sourceDir, "_images");
        Directory.CreateDirectory(imagesDir);
        CreateDummyJpeg(Path.Combine(imagesDir, "shared.jpg"));

        var galleryDir = Path.Combine(sourceDir, "landscapes");
        Directory.CreateDirectory(galleryDir);
        CreateRevelaIndex(galleryDir, "Landscapes");
        CreateDummyJpeg(Path.Combine(galleryDir, "sunset.jpg"));

        var (service, manifestRepo) = CreateContentService();

        // Act
        var result = await service.ScanAsync();

        // Assert: both images counted
        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.ImageCount);

        var root = manifestRepo.Root!;

        // Gallery images are in the gallery node
        var landscapesNode = root.Children.FirstOrDefault(c => c.Slug == "landscapes/");
        Assert.IsNotNull(landscapesNode, "Landscapes gallery should exist");
        var galleryImages = landscapesNode.Content.OfType<ImageContent>().ToList();
        Assert.HasCount(1, galleryImages);
        Assert.AreEqual("sunset.jpg", galleryImages[0].Filename);

        // Shared images are in the hidden _images node
        var imagesNode = root.Children.FirstOrDefault(c => c.Path == ProjectPaths.SharedImages);
        Assert.IsNotNull(imagesNode, "Shared _images node should exist");
        var sharedImages = imagesNode.Content.OfType<ImageContent>().ToList();
        Assert.HasCount(1, sharedImages);
        Assert.AreEqual("shared.jpg", sharedImages[0].Filename);
    }

    [TestMethod]
    public async Task ScanAsync_SharedImagesNotInGalleryImagesByPath()
    {
        // Arrange: Verify shared images don't leak into gallery content
        CreateRevelaIndex(sourceDir, "Home");
        var imagesDir = Path.Combine(sourceDir, "_images");
        Directory.CreateDirectory(imagesDir);
        CreateDummyJpeg(Path.Combine(imagesDir, "leaked.jpg"));

        var (service, manifestRepo) = CreateContentService();

        // Act
        await service.ScanAsync();

        // Assert: Root content should NOT contain _images images
        var root = manifestRepo.Root!;
        var rootImages = root.Content.OfType<ImageContent>().ToList();
        Assert.IsEmpty(rootImages);
    }

    #region Helpers

    /// <summary>
    /// Create a minimal _index.revela file.
    /// </summary>
    private static void CreateRevelaIndex(string directory, string title) =>
        File.WriteAllText(
            Path.Combine(directory, "_index.revela"),
            $"""
            +++
            title = "{title}"
            +++
            """);

    /// <summary>
    /// Create a minimal valid JPEG file (smallest possible — 2×2 grey).
    /// </summary>
    /// <remarks>
    /// This is a minimal JFIF-compliant JPEG that image libraries can read.
    /// </remarks>
    private static void CreateDummyJpeg(string path)
    {
        // Minimal valid JPEG: SOI + APP0 (JFIF) + DQT + SOF0 + DHT + SOS + image data + EOI
        // This is a standard 2×2 pixel grey JPEG
        byte[] minimalJpeg =
        [
            0xFF, 0xD8, // SOI
            0xFF, 0xE0, 0x00, 0x10, // APP0 marker + length
            0x4A, 0x46, 0x49, 0x46, 0x00, // "JFIF\0"
            0x01, 0x01, // version 1.1
            0x00, // pixel aspect ratio
            0x00, 0x01, 0x00, 0x01, // 1×1 pixel density
            0x00, 0x00, // no thumbnail
            0xFF, 0xDB, 0x00, 0x43, 0x00, // DQT marker + length + table 0
            0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07,
            0x07, 0x07, 0x09, 0x09, 0x08, 0x0A, 0x0C, 0x14,
            0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12, 0x13,
            0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A,
            0x1C, 0x1C, 0x20, 0x24, 0x2E, 0x27, 0x20, 0x22,
            0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29, 0x2C,
            0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39,
            0x3D, 0x38, 0x32, 0x3C, 0x2E, 0x33, 0x34, 0x32,
            0xFF, 0xC0, 0x00, 0x0B, 0x08, // SOF0 + length + precision
            0x00, 0x02, 0x00, 0x02, // 2×2 pixels
            0x01, // 1 component (greyscale)
            0x01, 0x11, 0x00, // component: id=1, sampling=1×1, quant-table=0
            0xFF, 0xC4, 0x00, 0x1F, 0x00, // DHT marker + length + DC table 0
            0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B,
            0xFF, 0xC4, 0x00, 0xB5, 0x10, // DHT + AC table 0
            0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04, 0x03,
            0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D,
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
            0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
            0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
            0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
            0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16,
            0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
            0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
            0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
            0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
            0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
            0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
            0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
            0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
            0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
            0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
            0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
            0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4,
            0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
            0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA,
            0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
            0xF9, 0xFA,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, // SOS
            0x3F, 0x00, 0x7B, 0x40, 0x1B, 0x9E, 0x59, 0xE7, // scan data
            0xFF, 0xD9 // EOI
        ];
        File.WriteAllBytes(path, minimalJpeg);
    }

    /// <summary>
    /// Creates a fully wired <see cref="ContentService"/> with real scanner and mocked processor.
    /// </summary>
    private (ContentService Service, IManifestRepository ManifestRepo) CreateContentService()
    {
        // Real components
        var revelaParser = new RevelaParser(NullLogger<RevelaParser>.Instance);
        var scanner = new ContentScanner(NullLogger<ContentScanner>.Instance, revelaParser);
        var navBuilder = new NavigationBuilder(NullLogger<NavigationBuilder>.Instance);

        // ManifestService as real (in-memory, no file I/O since cache dir won't exist)
        var projectEnv = new ProjectEnvironment { Path = tempDir };
        var manifestRepo = new ManifestService(
            NullLogger<ManifestService>.Instance,
            Options.Create(projectEnv));

        // Mock: IImageProcessor returns dummy metadata for any image
        var imageProcessor = Substitute.For<IImageProcessor>();
        imageProcessor
            .ReadMetadataAsync(Arg.Any<string>(), Arg.Any<PlaceholderConfig?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(new ImageMetadata
            {
                Width = 1920,
                Height = 1080,
                FileSize = new FileInfo(callInfo.ArgAt<string>(0)).Length
            }));

        // Mock: IImageSizesProvider returns standard sizes
        var sizesProvider = Substitute.For<IImageSizesProvider>();
        sizesProvider.GetSizes().Returns([320, 640, 1280, 1920]);

        // Mock: IPathResolver returns our temp source directory
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(sourceDir);
        pathResolver.OutputPath.Returns(Path.Combine(tempDir, "output"));

        // Default GenerateConfig
        var generateConfig = new GenerateConfig();
        var optionsMonitor = Substitute.For<IOptionsMonitor<GenerateConfig>>();
        optionsMonitor.CurrentValue.Returns(generateConfig);

        var service = new ContentService(
            scanner,
            navBuilder,
            manifestRepo,
            imageProcessor,
            sizesProvider,
            pathResolver,
            optionsMonitor,
            NullLogger<ContentService>.Instance);

        return (service, manifestRepo);
    }

    #endregion
}

using Spectara.Revela.Tests.Shared.Fixtures;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Tests for <see cref="TestImageGenerator"/> to verify it creates valid
/// JPEG images with correct dimensions and EXIF metadata.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class TestImageGeneratorTests : IDisposable
{
    private readonly string tempDir = Path.Combine(
        Path.GetTempPath(),
        "revela-imgtest-" + Guid.NewGuid().ToString("N")[..8]);

    public TestImageGeneratorTests() => Directory.CreateDirectory(tempDir);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup
        }
    }

    [TestMethod]
    public void CreateJpeg_DefaultSize_CreatesValidImage()
    {
        var path = Path.Combine(tempDir, "default.jpg");

        TestImageGenerator.CreateJpeg(path);

        Assert.IsTrue(File.Exists(path));
        using var image = Image.NewFromFile(path);
        Assert.AreEqual(1920, image.Width);
        Assert.AreEqual(1080, image.Height);
        Assert.AreEqual(3, image.Bands); // RGB
    }

    [TestMethod]
    public void CreateJpeg_CustomSize_MatchesDimensions()
    {
        var path = Path.Combine(tempDir, "custom.jpg");

        TestImageGenerator.CreateJpeg(path, width: 800, height: 600);

        using var image = Image.NewFromFile(path);
        Assert.AreEqual(800, image.Width);
        Assert.AreEqual(600, image.Height);
    }

    [TestMethod]
    public void CreateJpeg_PortraitOrientation_TallerThanWide()
    {
        var path = Path.Combine(tempDir, "portrait.jpg");

        TestImageGenerator.CreateJpeg(path, width: 1080, height: 1920);

        using var image = Image.NewFromFile(path);
        Assert.AreEqual(1080, image.Width);
        Assert.AreEqual(1920, image.Height);
    }

    [TestMethod]
    public void CreateJpeg_WithExif_EmbedsCameraInfo()
    {
        var path = Path.Combine(tempDir, "exif.jpg");
        var exif = ExifOptions.Create()
            .WithCamera("Canon", "EOS R5")
            .WithIso(400);

        TestImageGenerator.CreateJpeg(path, exif: exif);

        using var image = Image.NewFromFile(path);

        // Verify EXIF fields are readable
        var make = (string)image.Get("exif-ifd0-Make");
        var model = (string)image.Get("exif-ifd0-Model");
        Assert.IsTrue(make.StartsWith("Canon", StringComparison.Ordinal));
        Assert.IsTrue(model.StartsWith("EOS R5", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateJpeg_WithExif_EmbedsExposureSettings()
    {
        var path = Path.Combine(tempDir, "exposure.jpg");
        var exif = ExifOptions.Create()
            .WithAperture(2.8)
            .WithShutterSpeed(1.0 / 125)
            .WithFocalLength(50);

        TestImageGenerator.CreateJpeg(path, exif: exif);

        using var image = Image.NewFromFile(path);

        // Verify EXIF fields exist (values may be rational format)
        var fNumber = image.Get("exif-ifd2-FNumber");
        var focalLength = image.Get("exif-ifd2-FocalLength");
        Assert.IsNotNull(fNumber);
        Assert.IsNotNull(focalLength);
    }

    [TestMethod]
    public void CreateJpeg_WithDateTaken_EmbedsDateTimeOriginal()
    {
        var path = Path.Combine(tempDir, "dated.jpg");
        var dateTaken = new DateTime(2025, 8, 15, 14, 30, 0, DateTimeKind.Utc);
        var exif = ExifOptions.Create()
            .WithDateTaken(dateTaken);

        TestImageGenerator.CreateJpeg(path, exif: exif);

        using var image = Image.NewFromFile(path);
        var dateStr = (string)image.Get("exif-ifd2-DateTimeOriginal");
        Assert.IsTrue(dateStr.StartsWith("2025:08:15", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateJpeg_FileSizeIsReasonable()
    {
        var path = Path.Combine(tempDir, "size.jpg");

        TestImageGenerator.CreateJpeg(path, width: 1920, height: 1080);

        var fileInfo = new FileInfo(path);
        // A 1920x1080 JPEG gradient should be between 10KB and 1MB
        Assert.IsTrue(fileInfo.Length > 10_000, $"File too small: {fileInfo.Length} bytes");
        Assert.IsTrue(fileInfo.Length < 1_000_000, $"File too large: {fileInfo.Length} bytes");
    }

    [TestMethod]
    public void TestProject_AddRealImage_CreatesFileOnDisk()
    {
        using var project = TestProject.Create(p => p
            .AddGallery("Photos", g => g
                .AddRealImage("sunset.jpg", 1280, 720, exif => exif
                    .WithCamera("Sony", "ILCE-7M4")
                    .WithIso(200)
                    .WithAperture(5.6)
                    .WithFocalLength(85))));

        var imagePath = Path.Combine(project.SourcePath, "Photos", "sunset.jpg");
        Assert.IsTrue(File.Exists(imagePath));

        using var image = Image.NewFromFile(imagePath);
        Assert.AreEqual(1280, image.Width);
        Assert.AreEqual(720, image.Height);
    }
}

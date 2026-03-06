using System.Globalization;
using NetVips;
using Image = NetVips.Image;

namespace Spectara.Revela.Tests.Shared.Fixtures;

/// <summary>
/// Generates real JPEG images with EXIF metadata for integration testing.
/// </summary>
/// <remarks>
/// <para>
/// Uses NetVips (already a project dependency) to create actual images with
/// pixels, gradients, and embedded EXIF data. This is self-contained and
/// cross-platform — no ExifTool or System.Drawing required.
/// </para>
/// <para>
/// For scan-only tests that don't need real pixels, use the minimal JPEG
/// stubs created by <see cref="TestProject.GalleryBuilder.AddImage"/>.
/// Use this generator when testing image processing, EXIF extraction,
/// or format conversion.
/// </para>
/// </remarks>
public static class TestImageGenerator
{
    /// <summary>
    /// Creates a real JPEG image with optional EXIF metadata.
    /// </summary>
    /// <param name="path">Output file path.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="exif">Optional EXIF configuration.</param>
    /// <param name="quality">JPEG quality (1-100, default 90).</param>
    public static void CreateJpeg(
        string path,
        int width = 1920,
        int height = 1080,
        ExifOptions? exif = null,
        int quality = 90)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Create a visually distinct gradient image
        using var image = CreateGradientImage(width, height);

        // Embed EXIF if requested
        if (exif is not null)
        {
            using var withExif = EmbedExif(image, exif);
            withExif.Jpegsave(path, q: quality);
            return;
        }

        // Save as JPEG
        image.Jpegsave(path, q: quality);
    }

    /// <summary>
    /// Creates a visually distinct test image with a colour gradient.
    /// </summary>
    private static Image CreateGradientImage(int width, int height)
    {
        // Create a 2-band coordinate image (x, y values at each pixel)
        var xy = Image.Xyz(width, height);

        // Extract x and y bands, normalize to 0-255 range
        var x = xy[0] * 255.0 / Math.Max(width, 1);
        var y = xy[1] * 255.0 / Math.Max(height, 1);

        // Build warm-toned RGB from gradients:
        // R = horizontal gradient (warm left-to-right fade)
        // G = vertical gradient (top-to-bottom)
        // B = diagonal mix
        var r = ((x * 0.7) + 80).Cast(Enums.BandFormat.Uchar);
        var g = ((y * 0.6) + 60).Cast(Enums.BandFormat.Uchar);
        var b = (((x + y) * 0.3) + 40).Cast(Enums.BandFormat.Uchar);

        return r.Bandjoin(g, b);
    }

    /// <summary>
    /// Embeds EXIF metadata into the image via a round-trip through JPEG buffer.
    /// </summary>
    /// <remarks>
    /// NetVips preserves EXIF when saving JPEG, but to SET new EXIF fields we need
    /// to use the underlying VipsImage metadata system. The fields must match the
    /// naming convention used by libvips: "exif-ifd0-*", "exif-ifd2-*".
    /// </remarks>
    private static Image EmbedExif(Image image, ExifOptions exif)
    {
        // Set EXIF fields using libvips metadata system
        if (!string.IsNullOrEmpty(exif.CameraMake))
        {
            image = image.Mutate(m => m.Set(GValue.GStrType, "exif-ifd0-Make", exif.CameraMake));
        }

        if (!string.IsNullOrEmpty(exif.CameraModel))
        {
            image = image.Mutate(m => m.Set(GValue.GStrType, "exif-ifd0-Model", exif.CameraModel));
        }

        if (exif.Iso.HasValue)
        {
            image = image.Mutate(m =>
                m.Set(GValue.GStrType, "exif-ifd2-ISOSpeedRatings", exif.Iso.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (exif.FNumber.HasValue)
        {
            image = image.Mutate(m =>
                m.Set(GValue.GStrType, "exif-ifd2-FNumber",
                    FormatExifRational(exif.FNumber.Value)));
        }

        if (exif.ExposureTime.HasValue)
        {
            image = image.Mutate(m =>
                m.Set(GValue.GStrType, "exif-ifd2-ExposureTime",
                    FormatExifRational(exif.ExposureTime.Value)));
        }

        if (exif.FocalLength.HasValue)
        {
            image = image.Mutate(m =>
                m.Set(GValue.GStrType, "exif-ifd2-FocalLength",
                    FormatExifRational(exif.FocalLength.Value)));
        }

        if (!string.IsNullOrEmpty(exif.LensModel))
        {
            image = image.Mutate(m =>
                m.Set(GValue.GStrType, "exif-ifd2-LensModel", exif.LensModel));
        }

        if (exif.DateTaken.HasValue)
        {
            var dateStr = exif.DateTaken.Value.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
            image = image.Mutate(m =>
                m.Set(GValue.GStrType, "exif-ifd2-DateTimeOriginal", dateStr));
        }

        return image;
    }

    /// <summary>
    /// Formats a double value as EXIF rational (e.g., 2.8 → "28/10").
    /// </summary>
    private static string FormatExifRational(double value)
    {
        // Convert to rational with denominator 10 for reasonable precision
        var numerator = (int)(value * 10);
        return $"{numerator}/10";
    }
}

/// <summary>
/// EXIF metadata options for test image generation.
/// </summary>
/// <remarks>
/// Use the fluent builder pattern via <see cref="Create"/>:
/// <code>
/// var exif = ExifOptions.Create()
///     .WithCamera("Canon", "EOS R5")
///     .WithIso(400)
///     .WithAperture(5.6)
///     .WithShutterSpeed(1.0 / 125)
///     .WithFocalLength(50)
///     .WithLens("RF 50mm F1.2L")
///     .WithDateTaken(new DateTime(2025, 8, 15, 14, 30, 0));
/// </code>
/// </remarks>
public sealed class ExifOptions
{
    /// <summary>Camera manufacturer (e.g., "Canon", "Sony", "Nikon").</summary>
    public string? CameraMake { get; private set; }

    /// <summary>Camera model (e.g., "EOS R5", "ILCE-7M4").</summary>
    public string? CameraModel { get; private set; }

    /// <summary>ISO sensitivity (e.g., 100, 400, 3200).</summary>
    public int? Iso { get; private set; }

    /// <summary>Aperture f-number (e.g., 2.8, 5.6, 8.0).</summary>
    public double? FNumber { get; private set; }

    /// <summary>Exposure time in seconds (e.g., 1.0/125 for 1/125s).</summary>
    public double? ExposureTime { get; private set; }

    /// <summary>Focal length in mm (e.g., 50, 85, 200).</summary>
    public double? FocalLength { get; private set; }

    /// <summary>Lens model name.</summary>
    public string? LensModel { get; private set; }

    /// <summary>Date and time the photo was taken.</summary>
    public DateTime? DateTaken { get; private set; }

    /// <summary>Creates a new empty ExifOptions builder.</summary>
    public static ExifOptions Create() => new();

    /// <summary>Sets camera make and model.</summary>
    public ExifOptions WithCamera(string make, string model)
    {
        CameraMake = make;
        CameraModel = model;
        return this;
    }

    /// <summary>Sets ISO sensitivity.</summary>
    public ExifOptions WithIso(int iso)
    {
        Iso = iso;
        return this;
    }

    /// <summary>Sets aperture f-number.</summary>
    public ExifOptions WithAperture(double fNumber)
    {
        FNumber = fNumber;
        return this;
    }

    /// <summary>Sets shutter speed (exposure time in seconds).</summary>
    public ExifOptions WithShutterSpeed(double seconds)
    {
        ExposureTime = seconds;
        return this;
    }

    /// <summary>Sets focal length in mm.</summary>
    public ExifOptions WithFocalLength(double mm)
    {
        FocalLength = mm;
        return this;
    }

    /// <summary>Sets lens model name.</summary>
    public ExifOptions WithLens(string lensModel)
    {
        LensModel = lensModel;
        return this;
    }

    /// <summary>Sets the date/time the photo was taken.</summary>
    public ExifOptions WithDateTaken(DateTime dateTaken)
    {
        DateTaken = dateTaken;
        return this;
    }
}

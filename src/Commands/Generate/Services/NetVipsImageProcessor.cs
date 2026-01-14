using System.Collections.Concurrent;
using NetVips;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Mapping;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Sdk.Models;
using Image = NetVips.Image;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Image processor using NetVips for high-performance image processing
/// </summary>
/// <remarks>
/// <para>
/// NetVips is 3-5× faster than ImageSharp and handles large images efficiently.
/// Supports:
/// </para>
/// <list type="bullet">
///   <item><description>Multiple output formats (WebP, JPG, AVIF)</description></item>
///   <item><description>Multiple sizes (responsive images)</description></item>
///   <item><description>EXIF extraction (cached in ImageManifest)</description></item>
///   <item><description>Camera model normalization (Sony ILCE → α series)</description></item>
///   <item><description>Quality control</description></item>
///   <item><description>Streaming (low memory usage)</description></item>
/// </list>
/// <para>
/// Thread Safety: Each image is processed independently. LibVips is thread-safe
/// for reading different images in parallel. We disable the libvips cache and
/// set internal concurrency to 1 to avoid contention when processing multiple
/// images in parallel from ImageService.
/// </para>
/// </remarks>
public sealed partial class NetVipsImageProcessor(
    ILogger<NetVipsImageProcessor> logger,
    CameraModelMapper cameraModelMapper) : IImageProcessor
{
    /// <summary>
    /// Flag to ensure NetVips is initialized only once
    /// </summary>
    private static bool netVipsInitialized;
    private static readonly Lock InitLock = new();

    /// <summary>
    /// Collected warnings during processing (thread-safe)
    /// </summary>
    private static readonly ConcurrentBag<string> CollectedWarnings = [];

    /// <summary>
    /// Initialize NetVips settings for parallel processing
    /// </summary>
    private static void EnsureNetVipsInitialized()
    {
        if (netVipsInitialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (netVipsInitialized)
            {
                return;
            }

            // Disable cache - each image is unique, no benefit from caching
            // Also prevents memory accumulation during batch processing
            Cache.Max = 0;

            // Redirect libvips warnings to our collection instead of stderr
            // This prevents warnings like "large XMP not saved" from interrupting
            // the progress display in the console
            Log.SetLogHandler("VIPS", Enums.LogLevelFlags.Warning, (_, _, message) =>
            {
                // Collect unique warnings (many images may have the same issue)
                if (!string.IsNullOrEmpty(message))
                {
                    CollectedWarnings.Add(message);
                }
            });

            // Let libvips use its default concurrency (ProcessorCount)
            // Each image is processed independently, libvips handles internal threading

            netVipsInitialized = true;
        }
    }

    /// <summary>
    /// Get and clear collected warnings
    /// </summary>
    public static IReadOnlyList<string> GetAndClearWarnings()
    {
        var warnings = CollectedWarnings.Distinct().ToList();
        CollectedWarnings.Clear();
        return warnings;
    }

    /// <summary>
    /// Process a single image: resize, convert formats, extract EXIF
    /// </summary>
    public Task<Models.Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        Action<bool>? onVariantSaved = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Image not found: {inputPath}", inputPath);
        }

        // Ensure NetVips is configured for parallel processing
        EnsureNetVipsInitialized();

        return ProcessImageInternalAsync(inputPath, options, onVariantSaved, cancellationToken);
    }

    /// <summary>
    /// Internal image processing (called under global lock)
    /// </summary>
    private async Task<Models.Image> ProcessImageInternalAsync(
        string inputPath,
        ImageProcessingOptions options,
        Action<bool>? onVariantSaved,
        CancellationToken cancellationToken)
    {
        // Suppress unused parameter warning - kept for future use and API consistency
        _ = cancellationToken;

        LogProcessingImage(logger, inputPath);

        // Load image once to get dimensions and EXIF
        int width, height;
        ExifData? exif;
        using (var original = Image.NewFromFile(inputPath, access: Enums.Access.Sequential))
        {
            // Extract EXIF data
            exif = ExtractExifData(original);

            // Get original dimensions
            width = original.Width;
            height = original.Height;
        }

        // Generate variants (different sizes and formats)
        // OPTIMIZATION: Load thumbnail ONCE per size, then save to ALL formats
        // This reduces file operations from (sizes × formats) to just (sizes)
        // Example: 6 sizes × 2 formats = 12 saves, but only 6 file reads!
        //
        // Note: We use Image.Thumbnail() for each size because JPEG shrink-on-load
        // is faster than loading the full image and resizing in memory.
        List<ImageVariant> variants = [];

        // Use sizes from options (already includes original width from scan phase)
        // Filter to only sizes <= original width (in case config changed)
        var sizesToGenerate = options.Sizes
            .Where(s => s <= width)
            .ToList();

        foreach (var size in sizesToGenerate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Load thumbnail for this size using shrink-on-load optimization
            // After loading, we strip all metadata immediately to reduce memory footprint
            // and avoid "large XMP not saved" warnings during encoding.
            // CopyMemory() is essential - it renders the image to RAM, enabling:
            // 1. Multiple format saves without re-decoding the source
            // 2. Avoids "out of order read" errors from lazy JPEG decoding
            using var thumb = Image.Thumbnail(inputPath, size, height: 10000000)
                .Mutate(mutable =>
                {
                    // Remove all metadata fields that start with known prefixes
                    // This reduces memory and prevents metadata from being encoded
                    foreach (var field in mutable.GetFields())
                    {
                        if (field.StartsWith("exif-", StringComparison.Ordinal) ||
                            field.StartsWith("xmp-", StringComparison.Ordinal) ||
                            field.StartsWith("iptc-", StringComparison.Ordinal) ||
                            field.StartsWith("icc-", StringComparison.Ordinal))
                        {
                            mutable.Remove(field);
                        }
                    }
                })
                .CopyMemory(); // Render to RAM for multi-format saves
            var thumbHeight = thumb.Height;

            // Save to ALL formats from the same thumbnail (no re-decode needed)
            foreach (var (format, quality) in options.Formats)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var variant = await SaveVariantAsync(
                    thumb,
                    inputPath,
                    options.OutputDirectory,
                    format,
                    size,
                    thumbHeight,
                    quality);

                variants.Add(variant);

                // Notify caller that a variant was saved (not skipped)
                onVariantSaved?.Invoke(false);
            }
        }

        // Collect actually generated sizes (for srcset in templates)
        var generatedSizes = variants
            .Select(v => v.Width)
            .Distinct()
            .Order()
            .ToList();

        return new Models.Image
        {
            SourcePath = inputPath,
            FileName = Path.GetFileNameWithoutExtension(inputPath),
            Width = width,
            Height = height,
            FileSize = new FileInfo(inputPath).Length,
            DateTaken = exif?.DateTaken ?? File.GetCreationTimeUtc(inputPath),
            Exif = exif,
            Variants = variants,
            Sizes = generatedSizes
        };
    }

    /// <summary>
    /// Read image metadata without processing (fast operation).
    /// </summary>
    /// <remarks>
    /// Uses Sequential access mode - only reads image header, not full decode.
    /// Much faster than full processing (~10-20ms vs 200-500ms per image).
    /// </remarks>
    public Task<ImageMetadata> ReadMetadataAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        // Suppress unused parameter warning - kept for API consistency
        _ = cancellationToken;

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Image not found: {inputPath}", inputPath);
        }

        // Ensure NetVips is configured for parallel processing
        EnsureNetVipsInitialized();

        // Sequential access = only read header, not full image decode
        using var image = Image.NewFromFile(inputPath, access: Enums.Access.Sequential);

        var width = image.Width;
        var height = image.Height;
        var fileInfo = new FileInfo(inputPath);
        var exif = ExtractExifData(image);

        return Task.FromResult(new ImageMetadata
        {
            Width = width,
            Height = height,
            FileSize = fileInfo.Length,
            Exif = exif,
            DateTaken = exif?.DateTaken ?? fileInfo.LastWriteTimeUtc
        });
    }

    /// <summary>
    /// Extract EXIF data from image
    /// </summary>
    private ExifData? ExtractExifData(Image image)
    {
        try
        {
            // Cache available metadata fields to avoid first-chance exceptions when fields are missing
            var fields = new HashSet<string>(image.GetFields(), StringComparer.Ordinal);

            // NetVips stores EXIF data in the "exif-ifd0-*" and "exif-ifd2-*" fields
            // All values come as formatted strings: "VALUE (VALUE, TYPE, N components, M bytes)"
            var rawMake = TryGetString(image, "exif-ifd0-Make", fields);
            var rawModel = TryGetString(image, "exif-ifd0-Model", fields);
            var rawLensModel = TryGetString(image, "exif-ifd2-LensModel", fields);
            var dateTimeOriginal = TryGetString(image, "exif-ifd2-DateTimeOriginal", fields);

            // Extract actual values from NetVips format
            var make = CameraModelMapper.ExtractExifValue(rawMake);
            var model = CameraModelMapper.ExtractExifValue(rawModel);
            var lensModel = CameraModelMapper.ExtractExifValue(rawLensModel);

            // Parse camera settings
            var fNumber = TryGetDouble(image, "exif-ifd2-FNumber", fields);
            var exposureTime = TryGetDouble(image, "exif-ifd2-ExposureTime", fields);
            var iso = TryGetInt(image, "exif-ifd2-ISOSpeedRatings", fields);
            var focalLength = TryGetDouble(image, "exif-ifd2-FocalLength", fields);

            // GPS coordinates (optional)
            var gpsLatitude = TryGetDouble(image, "exif-ifd3-GPSLatitude", fields);
            var gpsLongitude = TryGetDouble(image, "exif-ifd3-GPSLongitude", fields);

            // Apply camera model mappings (Sony ILCE → α series, etc.)
            var mappedMake = cameraModelMapper.MapMake(make);
            var mappedModel = cameraModelMapper.MapModel(model);
            var cleanedLens = CameraModelMapper.CleanLensModel(lensModel);

            // Extract additional useful fields
            var raw = ExtractAdditionalExifFields(image, fields);

            return new ExifData
            {
                Make = mappedMake,
                Model = mappedModel,
                LensModel = cleanedLens,
                DateTaken = ParseExifDate(dateTimeOriginal),
                FNumber = fNumber,
                ExposureTime = exposureTime,
                Iso = iso,
                FocalLength = focalLength,
                GpsLatitude = gpsLatitude,
                GpsLongitude = gpsLongitude,
                Raw = raw.Count > 0 ? raw : null
            };
        }
        catch (Exception ex)
        {
            // EXIF extraction is optional - log but don't fail
            LogExifExtractionFailed(logger, ex);
            return null;
        }
    }

    /// <summary>
    /// Additional EXIF fields that are useful for photographers.
    /// </summary>
    /// <remarks>
    /// These fields are extracted into the Raw dictionary for sorting, filtering,
    /// and display purposes. Only fields with non-empty values are included.
    /// </remarks>
    private static readonly HashSet<string> UsefulExifFields =
    [
        // Exposure and metering
        "ExposureProgram",      // 0=Unknown, 1=Manual, 2=Program, 3=Aperture Priority, etc.
        "ExposureMode",         // 0=Auto, 1=Manual, 2=Auto bracket
        "MeteringMode",         // 1=Average, 2=Center-weighted, 3=Spot, etc.
        "Flash",                // Flash status and mode
        "WhiteBalance",         // 0=Auto, 1=Manual
        "ExposureCompensation", // Exposure bias

        // Lens and focus
        "FocalLengthIn35mmFormat", // 35mm equivalent focal length
        "MaxApertureValue",     // Maximum aperture of lens
        "SubjectDistance",      // Distance to subject

        // Scene info
        "SceneCaptureType",     // 0=Standard, 1=Landscape, 2=Portrait, 3=Night
        "Contrast",             // 0=Normal, 1=Low, 2=High
        "Saturation",           // 0=Normal, 1=Low, 2=High
        "Sharpness",            // 0=Normal, 1=Soft, 2=Hard

        // Rating and metadata
        "Rating",               // Star rating (1-5)
        "RatingPercent",        // Rating as percentage
        "Copyright",            // Copyright notice
        "Artist",               // Photographer name
        "ImageDescription",     // Image title/description
        "UserComment",          // User comment

        // Windows metadata (from XP)
        "XPTitle",              // Windows title
        "XPComment",            // Windows comment
        "XPAuthor",             // Windows author
        "XPKeywords",           // Windows keywords/tags
        "XPSubject",            // Windows subject

        // Additional camera info
        "LensMake",             // Lens manufacturer
        "LensSerialNumber",     // Lens serial number
        "SerialNumber",         // Camera body serial number (BodySerialNumber)
        "CameraSerialNumber",   // Alternative camera serial number field

        // GPS (additional)
        "GPSAltitude",          // Altitude

        // Software
        "Software",             // Processing software
    ];

    /// <summary>
    /// Extract additional EXIF fields into a dictionary.
    /// Only fields with non-empty values are included.
    /// </summary>
    private static Dictionary<string, string> ExtractAdditionalExifFields(Image image, ISet<string> fields)
    {
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var fieldName in UsefulExifFields)
        {
            // Try different IFD locations
            var value = TryGetExifValue(image, fieldName, fields);
            if (!string.IsNullOrWhiteSpace(value))
            {
                raw[fieldName] = value;
            }
        }

        return raw;
    }

    /// <summary>
    /// Try to get an EXIF value by field name, checking multiple IFD locations.
    /// </summary>
    private static string? TryGetExifValue(Image image, string fieldName, ISet<string> fields)
    {
        // Try different IFD locations (IFD0, ExifIFD/IFD2, GPS/IFD3)
        string[] prefixes = ["exif-ifd0-", "exif-ifd2-", "exif-ifd3-"];

        foreach (var prefix in prefixes)
        {
            var value = TryGetString(image, prefix + fieldName, fields);
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Extract actual value from NetVips format
                return CameraModelMapper.ExtractExifValue(value);
            }
        }

        return null;
    }

    /// <summary>
    /// Try to get a string value from EXIF field
    /// </summary>
    /// <remarks>
    /// Uses image.Contains() to check field existence before Get() to avoid
    /// first-chance exceptions in the debugger. The fields set is used as an
    /// additional fast-path optimization.
    /// </remarks>
    private static string? TryGetString(Image image, string field, ISet<string>? fields = null)
    {
        // Fast path: if we have a cached field set and the field is not in it, skip
        if (fields is not null && !fields.Contains(field))
        {
            return null;
        }

        // Check with NetVips native Contains() to avoid exception on Get()
        if (!image.Contains(field))
        {
            return null;
        }

        // Field exists - safe to read (still try/catch for edge cases like corrupt data)
        try
        {
            return image.Get(field) as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Try to get a double value from EXIF field
    /// </summary>
    /// <remarks>
    /// NetVips returns EXIF values as formatted strings like:
    /// - Rational: "28/10 (f/2.8, Rational, 1 components, 8 bytes)"
    /// - Short: "100 (100, Short, 1 components, 2 bytes)"
    /// We need to parse the fraction or first number.
    /// </remarks>
    private static double? TryGetDouble(Image image, string field, ISet<string>? fields = null)
    {
        try
        {
            var value = TryGetString(image, field, fields);

            // NetVips returns EXIF as strings "value (meta)"; parse first number/fraction
            if (!string.IsNullOrEmpty(value))
            {
                return ParseExifNumericValue(value);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Try to get an int value from EXIF field
    /// </summary>
    private static int? TryGetInt(Image image, string field, ISet<string>? fields = null)
    {
        try
        {
            var value = TryGetString(image, field, fields);

            if (!string.IsNullOrEmpty(value))
            {
                var parsed = ParseExifNumericValue(value);
                return parsed.HasValue ? (int)parsed.Value : null;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse numeric value from NetVips EXIF string format
    /// </summary>
    /// <remarks>
    /// Formats:
    /// - Rational: "28/10 (f/2.8, Rational...)" → 2.8
    /// - Short/Long: "100 (100, Short...)" → 100
    /// - Direct: "1/200" → 0.005
    /// </remarks>
    private static double? ParseExifNumericValue(string exifString)
    {
        if (string.IsNullOrWhiteSpace(exifString))
        {
            return null;
        }

        // Get the part before the opening parenthesis (if any)
        var spaceIndex = exifString.IndexOf(' ', StringComparison.Ordinal);
        var valuePart = spaceIndex > 0 ? exifString[..spaceIndex] : exifString;

        // Check if it's a fraction like "28/10" or "1/200"
        var slashIndex = valuePart.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex > 0)
        {
            var numeratorStr = valuePart[..slashIndex];
            var denominatorStr = valuePart[(slashIndex + 1)..];

            if (double.TryParse(numeratorStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var numerator) &&
                double.TryParse(denominatorStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }
        }

        // Try to parse as a direct number
        if (double.TryParse(valuePart, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var directValue))
        {
            return directValue;
        }

        return null;
    }

    /// <summary>
    /// Parse EXIF date string
    /// </summary>
    /// <remarks>
    /// NetVips format: "2022:07:31 22:22:22 (2022:07:31 22:22:22, ASCII, 20 components...)"
    /// Standard EXIF format: "2024:01:20 14:30:45"
    /// We need to extract "YYYY:MM:DD HH:MM:SS" from the beginning.
    /// </remarks>
    private static DateTime? ParseExifDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
        {
            return null;
        }

        try
        {
            // NetVips adds metadata after the date, extract just the date/time part
            // Format: "2022:07:31 22:22:22 (2022:07:31..."
            // We need first 19 characters: "YYYY:MM:DD HH:MM:SS"
            var dateTimePart = dateString.Length >= 19 ? dateString[..19] : dateString;

            var parts = dateTimePart.Split(' ');
            if (parts.Length < 2)
            {
                return null;
            }

            var dateParts = parts[0].Split(':');
            var timeParts = parts[1].Split(':');

            if (dateParts.Length != 3 || timeParts.Length != 3)
            {
                return null;
            }

            return new DateTime(
                int.Parse(dateParts[0], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(dateParts[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(dateParts[2], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(timeParts[0], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(timeParts[1], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(timeParts[2], System.Globalization.CultureInfo.InvariantCulture),
                DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save image variant to disk
    /// </summary>
    /// <remarks>
    /// Output structure matches Lumina theme expectations:
    /// images/{fileName}/{width}.{format}
    /// e.g., images/photo1/640.jpg
    /// </remarks>
    private Task<ImageVariant> SaveVariantAsync(
        Image image,
        string originalPath,
        string outputDirectory,
        string format,
        int width,
        int height,
        int quality)
    {
        // Build output path: images/{fileName}/{width}.{format}
        var fileName = Path.GetFileNameWithoutExtension(originalPath);
        var imageDirectory = Path.Combine(outputDirectory, fileName);
        var outputFileName = $"{width}.{format}";
        var outputPath = Path.Combine(imageDirectory, outputFileName);

        // Ensure image-specific output directory exists
        Directory.CreateDirectory(imageDirectory);

        // Save with format-specific options
        // IMPORTANT: Do NOT use Task.Run here!
        // NetVips is NOT thread-safe - all operations on an Image must happen on the same thread
        //
        // keep: ForeignKeep.None - removes all metadata (EXIF, XMP, ICC profiles)
        // Benefits:
        // - Smaller file sizes (XMP/EXIF can add several KB per image)
        // - No "large XMP not saved" warnings from libvips
        // - Privacy: GPS coordinates etc. not leaked (already extracted to manifest)
        // - Web images don't need embedded metadata
        switch (format.ToUpperInvariant())
        {
            case "WEBP":
                image.Webpsave(outputPath, q: quality, keep: Enums.ForeignKeep.None);
                break;

            case "JPG":
            case "JPEG":
                image.Jpegsave(outputPath, q: quality, keep: Enums.ForeignKeep.None);
                break;

            case "AVIF":
                // AVIF uses AV1 compression via HEIF container
                image.Heifsave(outputPath, q: quality, compression: Enums.ForeignHeifCompression.Av1, keep: Enums.ForeignKeep.None);
                break;

            case "PNG":
                image.Pngsave(outputPath, compression: 9, keep: Enums.ForeignKeep.None);
                break;

            default:
                throw new NotSupportedException($"Image format not supported: {format}");
        }

        LogSavedVariant(logger, outputPath, width, height, format);

        var variant = new ImageVariant
        {
            Width = width,
            Height = height,
            Format = format,
            Path = outputFileName,
            Size = new FileInfo(outputPath).Length
        };

        return Task.FromResult(variant);
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing image: {Path}")]
    private static partial void LogProcessingImage(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to extract EXIF from image")]
    private static partial void LogExifExtractionFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved variant: {Path} ({Width}×{Height}, {Format})")]
    private static partial void LogSavedVariant(ILogger logger, string path, int width, int height, string format);
}

using NetVips;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Mapping;
using Spectara.Revela.Commands.Generate.Models;
using Image = NetVips.Image;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Image processor using NetVips for high-performance image processing
/// </summary>
/// <remarks>
/// NetVips is 3-5× faster than ImageSharp and handles large images efficiently.
/// Supports:
/// - Multiple output formats (WebP, JPG, AVIF)
/// - Multiple sizes (responsive images)
/// - EXIF extraction (cached in ImageManifest)
/// - Camera model normalization (Sony ILCE → α series)
/// - Quality control
/// - Streaming (low memory usage)
///
/// CRITICAL: NetVips/libvips has GLOBAL STATE that is NOT THREAD-SAFE
/// All NetVips operations must be protected by a global lock
/// </remarks>
public sealed partial class NetVipsImageProcessor(
    ILogger<NetVipsImageProcessor> logger,
    CameraModelMapper cameraModelMapper) : IImageProcessor
{
    // CRITICAL: Global lock for ALL NetVips operations
    // NetVips/libvips has global codec instances, thread pools, and caches
    // Even processing different images in parallel causes "out of order read" errors
    private static readonly SemaphoreSlim GlobalNetVipsLock = new(1, 1);

    /// <summary>
    /// Process a single image: resize, convert formats, extract EXIF
    /// </summary>
    public async Task<Models.Image> ProcessImageAsync(
        string inputPath,
        ImageProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Image not found: {inputPath}", inputPath);
        }

        // CRITICAL: Acquire global lock before ANY NetVips operations
        await GlobalNetVipsLock.WaitAsync(cancellationToken);
        try
        {
            return await ProcessImageInternalAsync(inputPath, options, cancellationToken);
        }
        finally
        {
            GlobalNetVipsLock.Release();
        }
    }

    /// <summary>
    /// Internal image processing (called under global lock)
    /// </summary>
    private async Task<Models.Image> ProcessImageInternalAsync(
        string inputPath,
        ImageProcessingOptions options,
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
        // CRITICAL: Use Image.Thumbnail() for EACH variant independently
        // This is the safest approach to avoid "out of order read" errors
        // Thumbnail() is optimized for this use case and handles EXIF rotation
        List<ImageVariant> variants = [];

        foreach (var size in options.Sizes)
        {
            // Skip if image width is smaller than target size
            if (width < size)
            {
                continue;
            }

            // Generate each format for this size
            foreach (var format in options.Formats)
            {
                // Load a fresh thumbnail for each output
                // Always resize by WIDTH to ensure consistent filenames (640.jpg, 1024.jpg, etc.)
                // Height is calculated automatically to maintain aspect ratio
                using var thumb = Image.Thumbnail(inputPath, size, height: 10000000);

                var variant = await SaveVariantAsync(
                    thumb,
                    inputPath,
                    options.OutputDirectory,
                    format,
                    size,  // Use requested size for filename, not actual thumb.Width
                    thumb.Height,
                    options.Quality);

                variants.Add(variant);
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
            AvailableSizes = generatedSizes
        };
    }

    /// <summary>
    /// Read image metadata without processing (fast operation).
    /// </summary>
    /// <remarks>
    /// Uses Sequential access mode - only reads image header, not full decode.
    /// Much faster than full processing (~10-20ms vs 200-500ms per image).
    /// </remarks>
    public async Task<ImageMetadata> ReadMetadataAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Image not found: {inputPath}", inputPath);
        }

        // CRITICAL: Acquire global lock before ANY NetVips operations
        await GlobalNetVipsLock.WaitAsync(cancellationToken);
        try
        {
            // Sequential access = only read header, not full image decode
            using var image = Image.NewFromFile(inputPath, access: Enums.Access.Sequential);

            var width = image.Width;
            var height = image.Height;
            var fileInfo = new FileInfo(inputPath);
            var exif = ExtractExifData(image);

            return new ImageMetadata
            {
                Width = width,
                Height = height,
                FileSize = fileInfo.Length,
                Exif = exif,
                DateTaken = exif?.DateTaken ?? fileInfo.LastWriteTimeUtc
            };
        }
        finally
        {
            GlobalNetVipsLock.Release();
        }
    }

    /// <summary>
    /// Extract EXIF data from image
    /// </summary>
    private ExifData? ExtractExifData(Image image)
    {
        try
        {
            // NetVips stores EXIF data in the "exif-ifd0-*" and "exif-ifd2-*" fields
            // All values come as formatted strings: "VALUE (VALUE, TYPE, N components, M bytes)"
            var rawMake = image.Get("exif-ifd0-Make") as string;
            var rawModel = image.Get("exif-ifd0-Model") as string;
            var rawLensModel = TryGetString(image, "exif-ifd2-LensModel");
            var dateTimeOriginal = image.Get("exif-ifd2-DateTimeOriginal") as string;

            // Extract actual values from NetVips format
            var make = CameraModelMapper.ExtractExifValue(rawMake);
            var model = CameraModelMapper.ExtractExifValue(rawModel);
            var lensModel = CameraModelMapper.ExtractExifValue(rawLensModel);

            // Parse camera settings
            var fNumber = TryGetDouble(image, "exif-ifd2-FNumber");
            var exposureTime = TryGetDouble(image, "exif-ifd2-ExposureTime");
            var iso = TryGetInt(image, "exif-ifd2-ISOSpeedRatings");
            var focalLength = TryGetDouble(image, "exif-ifd2-FocalLength");

            // GPS coordinates (optional)
            var gpsLatitude = TryGetDouble(image, "exif-ifd3-GPSLatitude");
            var gpsLongitude = TryGetDouble(image, "exif-ifd3-GPSLongitude");

            // Apply camera model mappings (Sony ILCE → α series, etc.)
            var mappedMake = cameraModelMapper.MapMake(make);
            var mappedModel = cameraModelMapper.MapModel(model);
            var cleanedLens = CameraModelMapper.CleanLensModel(lensModel);

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
                GpsLongitude = gpsLongitude
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
    /// Try to get a string value from EXIF field
    /// </summary>
    private static string? TryGetString(Image image, string field)
    {
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
    private static double? TryGetDouble(Image image, string field)
    {
        try
        {
            var value = image.Get(field);

            // NetVips returns all EXIF as strings
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                return ParseExifNumericValue(s);
            }

            // Fallback for other types
            return value switch
            {
                double d => d,
                int i => i,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Try to get an int value from EXIF field
    /// </summary>
    private static int? TryGetInt(Image image, string field)
    {
        try
        {
            var value = image.Get(field);

            // NetVips returns all EXIF as strings
            if (value is string s && !string.IsNullOrEmpty(s))
            {
                var parsed = ParseExifNumericValue(s);
                return parsed.HasValue ? (int)parsed.Value : null;
            }

            // Fallback for other types
            return value switch
            {
                int i => i,
                double d => (int)d,
                _ => null
            };
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
    /// Output structure matches Expose theme expectations:
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
        switch (format.ToUpperInvariant())
        {
            case "WEBP":
                image.Webpsave(outputPath, q: quality);
                break;

            case "JPG":
            case "JPEG":
                image.Jpegsave(outputPath, q: quality);
                break;

            case "AVIF":
                // AVIF support (requires libvips 8.12+)
                image.Heifsave(outputPath, q: quality);
                break;

            case "PNG":
                image.Pngsave(outputPath, compression: 9);
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

using System.Collections.Concurrent;
using System.Globalization;
using NetVips;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Mapping;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Core.Configuration;
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
internal sealed partial class NetVipsImageProcessor(
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

            // Optimize thread pool size: (CPU/2) × (CPU/2) strategy
            // - Workers (in ImageService): CPU/2 parallel image processing tasks
            // - Concurrency (here): CPU/2 libvips threads for resize/decode operations
            // This balances parallelism with thread overhead, especially important
            // because AVIF (libaom) spawns its own threads per encoder instance.
            // Benchmarks show this reduces thread count by ~30% with equal performance.
            NetVips.NetVips.Concurrency = Math.Max(1, Environment.ProcessorCount / 2);

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
        Action<bool, string>? onVariantSaved = null,
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
        Action<bool, string>? onVariantSaved,
        CancellationToken cancellationToken)
    {
        // Suppress unused parameter warning - kept for future use and API consistency
        _ = cancellationToken;

        LogProcessingImage(logger, inputPath);

        // Use dimensions from options (already read during scan phase)
        var width = options.Width;
        var height = options.Height;

        // Use existing placeholder from scan phase
        var placeholder = options.ExistingPlaceholder;

        // Generate variants (different sizes and formats)
        // OPTIMIZATION: Load thumbnail ONCE per size, then save to ALL formats
        // This reduces file operations from (sizes × formats) to just (sizes)
        // Example: 6 sizes × 2 formats = 12 saves, but only 6 file reads!
        //
        // Note: We use Image.Thumbnail() for each size because JPEG shrink-on-load
        // is faster than loading the full image and resizing in memory.
        List<ImageVariant> variants = [];

        // Use sizes from options (already includes original width from scan phase)
        // Filter to only sizes <= longest side (in case config changed)
        // Sort DESCENDING: largest first for pyramid resize optimization
        var longestSide = Math.Max(width, height);
        var sizesToGenerate = options.Sizes
            .Where(s => s <= longestSide)
            .OrderByDescending(s => s)
            .ToList();

        // Incremental mode: only generate specific variants
        // Group by size for efficient thumbnail reuse
        var variantsToGenerate = options.VariantsToGenerate;
        var isIncrementalMode = variantsToGenerate != null && variantsToGenerate.Count > 0;

        // Build lookup: which formats need to be generated for each size
        var formatsPerSize = new Dictionary<int, HashSet<string>>();
        if (isIncrementalMode)
        {
            foreach (var (size, format) in variantsToGenerate!)
            {
                if (!formatsPerSize.TryGetValue(size, out var formatSet))
                {
                    formatSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    formatsPerSize[size] = formatSet;
                }

                formatSet.Add(format);
            }
        }

        // STAR OPTIMIZATION: Load original ONCE, resize all sizes from it
        // Benchmark results (6846px original, 11 sizes, 3 formats):
        //   Strategy A (shrink-on-load per size): 32.81s
        //   Strategy B (star from thumbnail):     30.09s
        //   Strategy C (star from original):      28.90s ← Winner! 13% faster
        //
        // Since original size is ALWAYS included in sizes (for lightbox),
        // loading the full original is optimal. All smaller sizes are resized
        // from the full-resolution image in memory.
        //
        // Quality: Each resize is directly from original = maximum quality
        // No accumulated artifacts like pyramid resize
        //
        // Note: CopyMemory() is NOT needed here because:
        // - NewFromFile() uses random access by default
        // - Multiple Resize() calls from same source work fine
        // - Only Thumbnail() (sequential access) would need CopyMemory()

        // Find the largest size we need to generate (may not be first if incremental mode)
        var largestNeededSize = isIncrementalMode
            ? sizesToGenerate.Where(s => formatsPerSize.ContainsKey(s)).DefaultIfEmpty(0).Max()
            : sizesToGenerate.FirstOrDefault(); // Already sorted descending

        // If nothing to generate, just report skips
        if (largestNeededSize == 0)
        {
            foreach (var size in sizesToGenerate)
            {
                foreach (var (format, _) in options.Formats)
                {
                    onVariantSaved?.Invoke(true, format);
                }
            }
        }
        else
        {
            // Load full original ONCE - benchmarking shows this is faster than Thumbnail(originalWidth)
            using var original = Image.NewFromFile(inputPath);

            var originalWidth = original.Width;
            var originalHeight = original.Height;

            foreach (var size in sizesToGenerate)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // In incremental mode, check if this size has any missing formats
                var formatsNeededForSize = isIncrementalMode && formatsPerSize.TryGetValue(size, out var needed)
                    ? needed
                    : null;

                // If this size has no missing formats, report all as skipped and continue
                if (isIncrementalMode && formatsNeededForSize == null)
                {
                    foreach (var (format, _) in options.Formats)
                    {
                        onVariantSaved?.Invoke(true, format);
                    }

                    continue;
                }

                // Get image for this size:
                // - Original size: use loaded original directly
                // - Smaller sizes: resize from original (no additional file I/O!)
                Image thumb;
                int thumbHeight;

                if (size >= originalWidth)
                {
                    // Use the already-loaded original directly (no resize needed)
                    thumb = original;
                    thumbHeight = originalHeight;
                }
                else
                {
                    // Resize from original based on resize mode
                    thumb = ResizeImage(original, size, options.ResizeMode);
                    thumbHeight = thumb.Height;
                }

                try
                {
                    // Process each format - report saved or skipped in order
                    foreach (var (format, quality) in options.Formats)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // In incremental mode, check if this specific format needs to be generated
                        var needsGeneration = !isIncrementalMode || formatsNeededForSize!.Contains(format);

                        if (needsGeneration)
                        {
                            var variant = await SaveVariantAsync(
                                thumb,
                                inputPath,
                                options.OutputDirectory,
                                format,
                                size,
                                thumbHeight,
                                quality);

                            variants.Add(variant);
                            onVariantSaved?.Invoke(false, format);
                        }
                        else
                        {
                            onVariantSaved?.Invoke(true, format);
                        }
                    }
                }
                finally
                {
                    // Dispose resized thumbnails (but NOT the original - it's managed by using)
                    if (size < originalWidth)
                    {
                        thumb.Dispose();
                    }
                }
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
            Variants = variants,
            Sizes = generatedSizes,
            Placeholder = placeholder
        };
    }

    /// <summary>
    /// Read image metadata without processing (fast operation).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses Sequential access mode - only reads image header, not full decode.
    /// Much faster than full processing (~10-20ms vs 200-500ms per image).
    /// </para>
    /// <para>
    /// When placeholderConfig is provided with Strategy != None, the image is loaded
    /// with random access to generate the placeholder. This is slower but ensures
    /// placeholders are available when pages are generated.
    /// </para>
    /// </remarks>
    public Task<ImageMetadata> ReadMetadataAsync(
        string inputPath,
        PlaceholderConfig? placeholderConfig = null,
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

        // Determine access mode based on whether we need to generate placeholder
        var needsPlaceholder = placeholderConfig?.Strategy is PlaceholderStrategy.CssHash;
        var accessMode = needsPlaceholder ? Enums.Access.Random : Enums.Access.Sequential;

        using var image = Image.NewFromFile(inputPath, access: accessMode);

        var width = image.Width;
        var height = image.Height;
        var fileInfo = new FileInfo(inputPath);
        var exif = ExtractExifData(image);

        // Generate placeholder if configured
        string? placeholder = null;
        if (needsPlaceholder && placeholderConfig is not null)
        {
            placeholder = GenerateCssHash(image);
        }

        return Task.FromResult(new ImageMetadata
        {
            Width = width,
            Height = height,
            FileSize = fileInfo.Length,
            Exif = exif,
            DateTaken = exif?.DateTaken ?? fileInfo.LastWriteTimeUtc,
            Placeholder = placeholder
        });
    }

    /// <summary>
    /// Resize an already-loaded image based on the resize mode.
    /// </summary>
    /// <param name="source">Source image (already loaded in memory).</param>
    /// <param name="size">Target size in pixels.</param>
    /// <param name="resizeMode">Which dimension to constrain: "longest", "width", or "height".</param>
    /// <returns>Resized image (caller must dispose).</returns>
    private static Image ResizeImage(Image source, int size, string resizeMode)
    {
        // ResizeMode determines how 'size' is interpreted:
        // - "longest" (default): size = longest side
        // - "width": size = exact width
        // - "height": size = exact height
        //
        // Using ThumbnailImage instead of Resize for correct alpha channel handling.
        // See: https://github.com/libvips/libvips/issues/4588

        return resizeMode.ToUpperInvariant() switch
        {
            "WIDTH" => source.ThumbnailImage(size, height: int.MaxValue),
            "HEIGHT" => source.ThumbnailImage(int.MaxValue, height: size),
            // "LONGEST" (default) - ThumbnailImage constrains to longest side
            _ => source.ThumbnailImage(size)
        };
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

            if (double.TryParse(numeratorStr, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var numerator) &&
                double.TryParse(denominatorStr, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var denominator) &&
                denominator != 0)
            {
                return numerator / denominator;
            }
        }

        // Try to parse as a direct number
        if (double.TryParse(valuePart, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var directValue))
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
                int.Parse(dateParts[0], CultureInfo.InvariantCulture),
                int.Parse(dateParts[1], CultureInfo.InvariantCulture),
                int.Parse(dateParts[2], CultureInfo.InvariantCulture),
                int.Parse(timeParts[0], CultureInfo.InvariantCulture),
                int.Parse(timeParts[1], CultureInfo.InvariantCulture),
                int.Parse(timeParts[2], CultureInfo.InvariantCulture),
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated {Strategy} placeholder ({Bytes} bytes)")]
    private static partial void LogPlaceholderGenerated(ILogger logger, string strategy, int bytes);

    /// <summary>
    /// Generate a CSS-only LQIP hash (20-bit integer)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Based on: https://leanrada.com/notes/css-only-lqip/
    /// Encodes image as a single integer that CSS can decode and render
    /// using radial gradients. Extremely minimal markup (~6-7 characters).
    /// </para>
    /// <para>
    /// Encoding scheme (20 bits total):
    /// - Bits 0-2: Oklab b component (3 bits = 8 values)
    /// - Bits 3-5: Oklab a component (3 bits = 8 values)
    /// - Bits 6-7: Oklab L (lightness) (2 bits = 4 values)
    /// - Bits 8-19: 6 brightness values for 3×2 grid (2 bits each = 4 levels)
    /// </para>
    /// <para>
    /// Matches original leanrada.com algorithm:
    /// - Uses dominant color (histogram peak) instead of average
    /// - Applies sharpen to 3×2 grid for better contrast
    /// - Uses relative brightness values (0.5 + cellL - baseL)
    /// </para>
    /// </remarks>
    /// <param name="image">Source image (already loaded)</param>
    /// <returns>Integer hash as string (e.g., "-721311")</returns>
    private string GenerateCssHash(Image image)
    {
        // Step 1: Calculate average color in Oklab space
        // Using average instead of dominant color works better for high-contrast images
        // (e.g., white fur on black background)
        using var sampler = image.ThumbnailImage(10, height: 10, crop: Enums.Interesting.Centre);

        var sumL = 0.0;
        var sumA = 0.0;
        var sumB = 0.0;
        var pixelCount = 0;

        for (var y = 0; y < sampler.Height; y++)
        {
            for (var x = 0; x < sampler.Width; x++)
            {
                var pixel = sampler.Getpoint(x, y);
                var r = Math.Clamp(pixel[0], 0, 255) / 255.0;
                var g = Math.Clamp(pixel[1], 0, 255) / 255.0;
                var b = Math.Clamp(pixel[2], 0, 255) / 255.0;

                // Convert to Oklab for perceptually uniform averaging
                var (l, a, ob) = RgbToOklab(r, g, b);
                sumL += l;
                sumA += a;
                sumB += ob;
                pixelCount++;
            }
        }

        // Average in Oklab space
        var rawBaseL = sumL / pixelCount;
        var rawBaseA = sumA / pixelCount;
        var rawBaseB = sumB / pixelCount;

        // Step 2: Find optimal Oklab bit representation via brute-force search
        var (qL, qA, qB) = FindOklabBits(rawBaseL, rawBaseA, rawBaseB);

        // Get the actual Oklab values that will be used in CSS decoding
        var (baseL, _, _) = BitsToOklab(qL, qA, qB);

        // Step 3: Resize to 3x2 with sharpen (like original)
        var gridScaleX = 3.0 / image.Width;
        var gridScaleY = 2.0 / image.Height;
        using var gridRaw = image.Resize(gridScaleX, vscale: gridScaleY);
        using var grid = gridRaw.Sharpen(sigma: 1.0);

        // Step 4: Calculate ABSOLUTE brightness values (original algorithm)
        // The CSS uses grayscale cells (hsl(0 0% x%)) NOT relative to base color
        var brightness = new int[6];
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var pixel = grid.Getpoint(x, y);
                var r = Math.Clamp(pixel[0], 0, 255) / 255.0;
                var g = Math.Clamp(pixel[1], 0, 255) / 255.0;
                var b = Math.Clamp(pixel[2], 0, 255) / 255.0;

                // Get cell lightness in Oklab (for perceptual accuracy)
                var (cellL, _, _) = RgbToOklab(r, g, b);

                // Map lightness [0-1] to [0-3] (CSS maps 0-3 to 20%-80%)
                // cellL is typically 0-1, quantize directly
                brightness[(y * 3) + x] = (int)Math.Clamp(Math.Round(cellL * 3), 0, 3);
            }
        }

        // Step 5: Pack into 20-bit integer
        var hash = 0;
        hash |= brightness[0] << 18;
        hash |= brightness[1] << 16;
        hash |= brightness[2] << 14;
        hash |= brightness[3] << 12;
        hash |= brightness[4] << 10;
        hash |= brightness[5] << 8;
        hash |= qL << 6;
        hash |= qA << 3;
        hash |= qB;

        var signedHash = hash - 524288;

        var hashString = signedHash.ToString(CultureInfo.InvariantCulture);
        LogPlaceholderGenerated(logger, "csshash", hashString.Length);
        return hashString;
    }

    /// <summary>
    /// Find the best bit configuration that produces a color closest to target
    /// </summary>
    /// <remarks>
    /// Brute-force search through all 128 combinations (4×8×8) to find
    /// the quantized Oklab values that minimize perceptual distance.
    /// Uses chroma-aware scaling to avoid bias toward neutral colors.
    /// </remarks>
    private static (int ll, int aaa, int bbb) FindOklabBits(double targetL, double targetA, double targetB)
    {
        var targetChroma = Math.Sqrt((targetA * targetA) + (targetB * targetB));
        var scaledTargetA = ScaleComponentForDiff(targetA, targetChroma);
        var scaledTargetB = ScaleComponentForDiff(targetB, targetChroma);

        var bestBits = (ll: 0, aaa: 0, bbb: 0);
        var bestDifference = double.MaxValue;

        // Try all 128 combinations: L(4) × a(8) × b(8)
        for (var lli = 0; lli <= 3; lli++)
        {
            for (var aaai = 0; aaai <= 7; aaai++)
            {
                for (var bbbi = 0; bbbi <= 7; bbbi++)
                {
                    var (l, a, b) = BitsToOklab(lli, aaai, bbbi);
                    var chroma = Math.Sqrt((a * a) + (b * b));
                    var scaledA = ScaleComponentForDiff(a, chroma);
                    var scaledB = ScaleComponentForDiff(b, chroma);

                    // Euclidean distance in scaled Oklab space
                    var dL = l - targetL;
                    var dA = scaledA - scaledTargetA;
                    var dB = scaledB - scaledTargetB;
                    var difference = Math.Sqrt((dL * dL) + (dA * dA) + (dB * dB));

                    if (difference < bestDifference)
                    {
                        bestDifference = difference;
                        bestBits = (lli, aaai, bbbi);
                    }
                }
            }
        }

        return bestBits;
    }

    /// <summary>
    /// Scale a/b component to reduce bias toward neutral colors
    /// </summary>
    /// <remarks>
    /// Without this scaling, euclidean comparison in Oklab space would
    /// be biased toward low-chroma (gray) colors. This spreads out
    /// the comparison space for saturated colors.
    /// </remarks>
    private static double ScaleComponentForDiff(double x, double chroma) =>
        x / (1e-6 + Math.Pow(chroma, 0.5));

    /// <summary>
    /// Convert quantized bits back to Oklab values (matches CSS decoder)
    /// </summary>
    private static (double L, double a, double b) BitsToOklab(int ll, int aaa, int bbb)
    {
        // Must match CSS decoder exactly! (original formula from leanrada.com)
        // L: 2 bits -> [0.2, 0.8]
        var l = (ll / 3.0 * 0.6) + 0.2;
        // a: 3 bits -> [-0.35, 0.35]
        var a = (aaa / 8.0 * 0.7) - 0.35;
        // b: 3 bits -> [-0.35, 0.35] with +1 offset (asymmetric range)
        var b = ((bbb + 1) / 8.0 * 0.7) - 0.35;
        return (l, a, b);
    }

    /// <summary>
    /// Convert sRGB to Oklab color space
    /// </summary>
    /// <remarks>
    /// Based on Björn Ottosson's Oklab: https://bottosson.github.io/posts/oklab/
    /// </remarks>
    private static (double L, double a, double b) RgbToOklab(double r, double g, double b)
    {
        // sRGB to linear RGB
        var lr = r <= 0.04045 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        var lg = g <= 0.04045 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        var lb = b <= 0.04045 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        // Linear RGB to LMS
        var l = (0.4122214708 * lr) + (0.5363325363 * lg) + (0.0514459929 * lb);
        var m = (0.2119034982 * lr) + (0.6806995451 * lg) + (0.1073969566 * lb);
        var s = (0.0883024619 * lr) + (0.2817188376 * lg) + (0.6299787005 * lb);

        // LMS to Oklab
        var l_ = Math.Cbrt(l);
        var m_ = Math.Cbrt(m);
        var s_ = Math.Cbrt(s);

        return (
            L: (0.2104542553 * l_) + (0.7936177850 * m_) - (0.0040720468 * s_),
            a: (1.9779984951 * l_) - (2.4285922050 * m_) + (0.4505937099 * s_),
            b: (0.0259040371 * l_) + (0.7827717662 * m_) - (0.8086757660 * s_)
        );
    }
}

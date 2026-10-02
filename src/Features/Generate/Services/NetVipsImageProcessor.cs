using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Xml;
using NetVips;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Image = NetVips.Image;

namespace Spectara.Revela.Features.Generate.Services;

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
/// for reading different images in parallel. We disable the libvips cache and set the
/// per-image libvips concurrency (see <see cref="SetThreadsPerImage"/>) because ImageService
/// processes several images in parallel.
/// </para>
/// </remarks>
internal sealed partial class NetVipsImageProcessor(
    ILogger<NetVipsImageProcessor> logger,
    CameraModelMapper cameraModelMapper) : IImageProcessor
{
    /// <summary>
    /// Version of the encoded output for an unchanged source and configuration.
    /// </summary>
    /// <remarks>
    /// Part of every image's processing fingerprint. Increment whenever the written pixels
    /// or metadata change (e.g. color conversion) so existing variants are regenerated.
    /// </remarks>
    internal const int OutputVersion = 2;

    /// <summary>
    /// Version of the scanned metadata (dimensions, EXIF, placeholder) for an unchanged source.
    /// </summary>
    /// <remarks>
    /// Part of the scan cache key. Increment whenever <see cref="ReadMetadataAsync"/> computes
    /// different values for the same file (2: upright dimensions after EXIF orientation and
    /// sRGB placeholders; 3: XMP title, description, keywords, and rating; 4: placeholders from
    /// a shrink-on-load thumbnail), so manifests from older versions re-read their metadata.
    /// Images are not re-encoded: their processing state is independent of the scan.
    /// </remarks>
    internal const int MetadataVersion = 4;

    /// <summary>
    /// libvips metadata field holding the raw XMP packet.
    /// </summary>
    private const string XmpField = "xmp-data";

    /// <summary>
    /// Color space of published variants. Variants are saved without metadata, and browsers
    /// interpret untagged images as sRGB.
    /// </summary>
    private const string OutputProfile = "srgb";

    /// <summary>
    /// Largest image dimension libvips accepts (<c>VIPS_MAX_COORD</c>).
    /// </summary>
    private const int VipsMaxCoord = 10_000_000;

    /// <summary>
    /// Largest side of the thumbnail a placeholder is computed from. Its hash samples a 10×10
    /// center crop and a 3×2 grid, so a few hundred pixels keep it close to the full image.
    /// </summary>
    private const int PlaceholderSourceSize = 256;

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

            // Default libvips threads per image until ImageService applies its plan
            // (SetThreadsPerImage). The scan reads one image per core with this value. libvips
            // also passes it to libaom as the AVIF encoder's thread count
            // (see kleisauke/net-vips#272), which stops scaling past ~8 threads.
            NetVips.NetVips.Concurrency = Math.Clamp(Environment.ProcessorCount, 1, 8);

            netVipsInitialized = true;
        }
    }

    /// <summary>
    /// Sets how many libvips threads each image uses (see <see cref="ImageWorkerPlan"/>).
    /// </summary>
    internal static void SetThreadsPerImage(int threads)
    {
        EnsureNetVipsInitialized();
        NetVips.NetVips.Concurrency = Math.Max(1, threads);
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
        Action<VariantState, string>? onVariantProgress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Image not found: {inputPath}", inputPath);
        }

        // Ensure NetVips is configured for parallel processing
        EnsureNetVipsInitialized();

        return ProcessImageInternalAsync(inputPath, options, onVariantProgress, cancellationToken);
    }

    /// <summary>
    /// Internal image processing (called under global lock)
    /// </summary>
    private async Task<Models.Image> ProcessImageInternalAsync(
        string inputPath,
        ImageProcessingOptions options,
        Action<VariantState, string>? onVariantProgress,
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
        // Sort ASCENDING: smallest first so the user sees progress immediately
        // (the largest variant is also the slowest encode; running it last keeps the
        // progress display moving from the start instead of stalling on size #1).
        // The star-from-original strategy below is order-independent — see comment there.
        var longestSide = Math.Max(width, height);
        var sizesToGenerate = options.Sizes
            .Where(s => s <= longestSide)
            .OrderBy(s => s)
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
        // Since original size is included in sizes (for lightbox, unless capped by maxSize),
        // loading the full original is optimal. All smaller sizes are resized
        // from the full-resolution image in memory.
        //
        // Quality: Each resize is directly from original = maximum quality
        // No accumulated artifacts like pyramid resize
        //
        // NewFromFile() uses random access, so the original is decoded once and every resize
        // reads it from memory. Each resized size is then materialized once (see below).

        // Find the largest size we need to generate (drives the single full-res load).
        // sizesToGenerate is sorted ascending → Last() is the biggest.
        var largestNeededSize = isIncrementalMode
            ? sizesToGenerate.Where(s => formatsPerSize.ContainsKey(s)).DefaultIfEmpty(0).Max()
            : sizesToGenerate.LastOrDefault();

        // If nothing to generate, just report skips
        if (largestNeededSize == 0)
        {
            foreach (var size in sizesToGenerate)
            {
                foreach (var (format, _) in options.Formats)
                {
                    onVariantProgress?.Invoke(VariantState.Skipped, format);
                }
            }
        }
        else
        {
            // Load full original ONCE - benchmarking shows this is faster than Thumbnail(originalWidth)
            using var loaded = Image.NewFromFile(inputPath);

            // Normalize EXIF orientation once at the single load point that feeds BOTH the
            // direct largest-size save path and every resized variant (see #98). After Autorot
            // the pixels are physically upright and the orientation tag is removed, so all
            // downstream paths — including the original-size output saved with ForeignKeep.None —
            // are upright and no longer depend on an orientation tag. Autorot swaps width/height
            // for Orientation 6/8, matching the upright dimensions recorded during the scan.
            using var original = loaded.Autorot();

            var originalWidth = original.Width;

            // Above the configured cap, the cap replaces the full resolution: every size is a resize.
            var capped = options.MaxSize > 0
                && GetResizeExtent(original.Width, original.Height, options.ResizeMode) > options.MaxSize;

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
                        onVariantProgress?.Invoke(VariantState.Skipped, format);
                    }

                    continue;
                }

                // Get image for this size:
                // - Original size: use loaded original directly (already decoded in memory),
                //   unless a maxSize cap applies
                // - Smaller sizes: resize from original (no additional file I/O!), computed once
                //   into memory because every format and the sRGB check below read it. The lazy
                //   pipeline would otherwise redo the resize for each of them.
                using var resized = size >= originalWidth && !capped ? null : ResizeImage(original, size, options.ResizeMode);
                using var frame = resized?.CopyMemory();
                var source = frame ?? original;
                var thumbHeight = source.Height;

                // Convert after resizing: converting the original once would be recomputed
                // for every size and format by the lazy pipeline (~60% slower overall).
                using var converted = ConvertToOutputColorSpace(source, keepGrey: true);

                // Materialize the conversion when several formats encode it (one extra frame per
                // worker); a single encode streams it.
                var encodeCount = isIncrementalMode ? formatsNeededForSize!.Count : options.Formats.Count;
                using var convertedFrame = converted is not null && encodeCount > 1 ? converted.CopyMemory() : null;
                var publishable = convertedFrame ?? converted ?? source;

                // Process each format - report saved or skipped in order
                foreach (var (format, quality) in options.Formats)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // In incremental mode, check if this specific format needs to be generated
                    var needsGeneration = !isIncrementalMode || formatsNeededForSize!.Contains(format);

                    if (needsGeneration)
                    {
                        // Announce work-in-progress before the (potentially slow) encode/write
                        onVariantProgress?.Invoke(VariantState.Started, format);

                        var variant = await SaveVariantAsync(
                            publishable,
                            options.ImageSlug,
                            options.OutputDirectory,
                            format,
                            size,
                            thumbHeight,
                            quality,
                            options.Efforts.TryGetValue(format, out var effort) ? effort : null);

                        variants.Add(variant);
                        onVariantProgress?.Invoke(VariantState.Done, format);
                    }
                    else
                    {
                        onVariantProgress?.Invoke(VariantState.Skipped, format);
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
            Slug = options.ImageSlug,
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
    /// Dimensions, EXIF and XMP come from the header (sequential access, no pixel decode).
    /// </para>
    /// <para>
    /// When placeholderConfig is provided with Strategy != None, the placeholder is computed
    /// from a small shrink-on-load thumbnail: a JPEG is decoded at 1/2–1/8 scale instead of at
    /// full resolution, which was 90% of the scan's cost.
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

        using var loaded = Image.NewFromFile(inputPath, access: Enums.Access.Sequential);

        // Normalize EXIF orientation once so the scanned dimensions (and any placeholder)
        // describe the visually UPRIGHT image, consistent with the variants produced later
        // (see #98). Autorot swaps width/height for Orientation 6/8 and removes the tag.
        using var image = loaded.Autorot();

        var width = image.Width;
        var height = image.Height;
        var fileInfo = new FileInfo(inputPath);
        var exif = ExtractExifData(image);
        var xmp = ExtractXmpMetadata(image, inputPath);

        // Generate placeholder if configured
        string? placeholder = null;
        if (placeholderConfig?.Strategy is PlaceholderStrategy.CssHash)
        {
            // Thumbnail applies the EXIF orientation like Autorot and never enlarges. Its result
            // is read sequentially, so it is materialized before the hash reads it several
            // times (libvips would fail with "out of order read").
            using var thumbnail = Image.Thumbnail(inputPath, PlaceholderSourceSize, height: PlaceholderSourceSize, size: Enums.Size.Down);
            using var source = thumbnail.CopyMemory();
            placeholder = GenerateCssHash(source);
        }

        return Task.FromResult(new ImageMetadata
        {
            Width = width,
            Height = height,
            FileSize = fileInfo.Length,
            Exif = exif,
            DateTaken = exif?.DateTaken ?? fileInfo.LastWriteTimeUtc,
            // XMP is what DAMs (Capture One, Lightroom) write; EXIF tags are the fallback.
            Title = xmp.Title ?? GetRawExifText(exif, "XPTitle"),
            Description = xmp.Description ?? GetRawExifText(exif, "ImageDescription"),
            Keywords = xmp.Keywords,
            Rating = xmp.Rating,
            Placeholder = placeholder
        });
    }

    /// <summary>
    /// Reads descriptive metadata from the image's XMP packet.
    /// </summary>
    /// <remarks>
    /// Unreadable XMP never fails the scan: the image keeps its EXIF data and is
    /// treated as having no XMP.
    /// </remarks>
    private XmpMetadata ExtractXmpMetadata(Image image, string inputPath)
    {
        if (!image.Contains(XmpField))
        {
            return XmpMetadata.Empty;
        }

        try
        {
            return image.Get(XmpField) is byte[] { Length: > 0 } packet
                ? XmpMetadataParser.Parse(packet)
                : XmpMetadata.Empty;
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException or VipsException)
        {
            LogXmpIgnored(logger, inputPath, ex);
            return XmpMetadata.Empty;
        }
    }

    private static string? GetRawExifText(ExifData? exif, string field) =>
        exif?.Raw?.GetValueOrDefault(field)?.Trim() is { Length: > 0 } value ? value : null;

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
        //
        // The unconstrained side must stay within VIPS_MAX_COORD: libvips rejects larger
        // values (e.g. int.MaxValue) and silently falls back to a square bounding box.

        return resizeMode.ToUpperInvariant() switch
        {
            "WIDTH" => source.ThumbnailImage(size, height: VipsMaxCoord),
            "HEIGHT" => source.ThumbnailImage(VipsMaxCoord, height: size),
            // "LONGEST" (default) - ThumbnailImage constrains to longest side
            _ => source.ThumbnailImage(size)
        };
    }

    /// <summary>
    /// The dimension a size constrains under <paramref name="resizeMode"/> (see <see cref="ResizeImage"/>).
    /// </summary>
    internal static int GetResizeExtent(int width, int height, string resizeMode) =>
        resizeMode.ToUpperInvariant() switch
        {
            "WIDTH" => width,
            "HEIGHT" => height,
            _ => Math.Max(width, height)
        };

    /// <summary>
    /// Converts an image to 8-bit sRGB for publishing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Variants are saved without metadata, so the pixels must already be sRGB. An embedded
    /// ICC profile (Display P3, Adobe RGB, CMYK, …) is applied with the perceptual intent;
    /// untagged images outside 8-bit RGB (CMYK, 16-bit, Lab, …) use libvips' default profiles.
    /// Colors outside sRGB are mapped into it — the source file is never modified.
    /// </para>
    /// <para>
    /// ThumbnailImage/Resize keep the pixel values and the attached profile unchanged,
    /// so this can run after resizing.
    /// </para>
    /// </remarks>
    /// <param name="image">Image to convert (not disposed).</param>
    /// <param name="keepGrey">Keep untagged 8-bit greyscale images single-band.</param>
    /// <returns>The converted image (caller disposes), or <c>null</c> if <paramref name="image"/> is already publishable.</returns>
    private Image? ConvertToOutputColorSpace(Image image, bool keepGrey)
    {
        if (image.Contains(IccTransformShortcut.ProfileField))
        {
            if (IccTransformShortcut.IsNoOp(image, TransformToOutputProfile))
            {
                // Already sRGB pixel for pixel: the conversion would write the same values.
                return null;
            }

            try
            {
                return TransformToOutputProfile(image);
            }
            catch (VipsException ex)
            {
                // A broken embedded profile must not fail the build: fall back to the
                // interpretation-based conversion below (untagged sRGB stays unchanged).
                LogIccTransformFailed(logger, ex);
            }
        }

        var alreadyPublishable = image.Format == Enums.BandFormat.Uchar
            && (image.Interpretation is Enums.Interpretation.Srgb or Enums.Interpretation.Rgb or Enums.Interpretation.Multiband
                || (keepGrey && image.Interpretation == Enums.Interpretation.Bw));

        return alreadyPublishable ? null : image.Colourspace(Enums.Interpretation.Srgb);
    }

    /// <summary>
    /// Converts an image with an embedded ICC profile to sRGB, the profile of published variants.
    /// </summary>
    internal static Image TransformToOutputProfile(Image image) =>
        image.IccTransform(OutputProfile, embedded: true, intent: Enums.Intent.Perceptual);

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
    private static readonly FrozenSet<string> UsefulExifFields = new string[]
    {
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
    }.ToFrozenSet();

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
    /// Output structure: images/{imageSlug}/{width}.{format}
    /// e.g., images/events/fireworks/029081/640.jpg
    /// The imageSlug includes the gallery path to avoid collisions between images with the same
    /// filename in different galleries. A <c>null</c> <paramref name="effort"/> leaves the
    /// encoder's default (the option is not passed at all).
    /// </remarks>
    private Task<ImageVariant> SaveVariantAsync(
        Image image,
        string imageSlug,
        string outputDirectory,
        string format,
        int width,
        int height,
        int quality,
        int? effort)
    {
        // Build output path: images/{imageSlug}/{width}.{format}
        var imageDirectory = Path.Combine(outputDirectory, imageSlug.Replace('/', Path.DirectorySeparatorChar));
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
                image.Webpsave(outputPath, q: quality, effort: effort, keep: Enums.ForeignKeep.None);
                break;

            case "JPG":
            case "JPEG":
                image.Jpegsave(outputPath, q: quality, keep: Enums.ForeignKeep.None);
                break;

            case "AVIF":
                // AVIF uses AV1 compression via HEIF container
                image.Heifsave(outputPath, q: quality, compression: Enums.ForeignHeifCompression.Av1, effort: effort, keep: Enums.ForeignKeep.None);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ignoring unreadable XMP metadata in {Path}")]
    private static partial void LogXmpIgnored(ILogger logger, string path, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved variant: {Path} ({Width}×{Height}, {Format})")]
    private static partial void LogSavedVariant(ILogger logger, string path, int width, int height, string format);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated {Strategy} placeholder ({Bytes} bytes)")]
    private static partial void LogPlaceholderGenerated(ILogger logger, string strategy, int bytes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Embedded ICC profile could not be applied, converting without it")]
    private static partial void LogIccTransformFailed(ILogger logger, Exception exception);

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
        // Materialized: the sRGB check and the per-pixel reads below would otherwise each
        // recompute the shrink from the source image.
        using var samplerShrunk = image.ThumbnailImage(10, height: 10, crop: Enums.Interesting.Centre);
        using var samplerRaw = samplerShrunk.CopyMemory();
        using var samplerConverted = ConvertToOutputColorSpace(samplerRaw, keepGrey: false);
        var sampler = samplerConverted ?? samplerRaw;

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
        using var gridShrunk = image.Resize(gridScaleX, vscale: gridScaleY);
        using var gridRaw = gridShrunk.CopyMemory();
        using var gridConverted = ConvertToOutputColorSpace(gridRaw, keepGrey: false);
        using var grid = (gridConverted ?? gridRaw).Sharpen(sigma: 1.0);

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


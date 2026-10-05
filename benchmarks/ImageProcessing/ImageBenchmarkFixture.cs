using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetVips;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Configuration;
using Image = NetVips.Image;

namespace Spectara.Revela.Benchmarks.ImageProcessing;

/// <summary>
/// Shared setup that drives the production image pipeline the way <c>ImageService</c> does.
/// </summary>
internal static class ImageBenchmarkFixture
{
    /// <summary>
    /// Sizes of the bundled Lumina theme (<c>src/Themes/Lumina/Configuration/images.json</c>).
    /// </summary>
    public static readonly IReadOnlyList<int> LuminaSizes = [160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560];

    /// <summary>
    /// Qualities written by <c>revela config image</c> (jpg:90, webp:85, avif:75).
    /// </summary>
    private static readonly Dictionary<string, int> DefaultQualities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = 90,
        ["webp"] = 85,
        ["avif"] = 75
    };

    public static NetVipsImageProcessor CreateProcessor() => new(
        NullLogger<NetVipsImageProcessor>.Instance,
        new CameraModelMapper(new FixedOptionsMonitor<GenerateConfig>(new GenerateConfig())));

    /// <summary>
    /// Parses a format list such as <c>"jpg,webp"</c> into format → default quality.
    /// </summary>
    public static IReadOnlyDictionary<string, int> ParseFormats(string formats) => formats
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToDictionary(format => format, format => DefaultQualities[format], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Encoder efforts as <c>ImageService</c> passes them for the default <see cref="ImageConfig"/>:
    /// only values that differ from the libvips default (AVIF effort 2).
    /// </summary>
    public static IReadOnlyDictionary<string, int> DefaultEfforts(IReadOnlyDictionary<string, int> formats) =>
        formats.ContainsKey("avif")
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["avif"] = ImageConfig.DefaultAvifEffort }
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public static ImageProcessingOptions CreateOptions(
        IReadOnlyDictionary<string, int> formats,
        string outputDirectory,
        string imageSlug,
        int width,
        int height) => new()
        {
            Formats = formats,
            Efforts = DefaultEfforts(formats),
            Sizes = [.. LuminaSizes, Math.Max(width, height)],
            OutputDirectory = outputDirectory,
            ImageSlug = imageSlug,
            Width = width,
            Height = height
        };

    /// <summary>
    /// Applies the production worker plan and returns the number of images to process in parallel.
    /// </summary>
    public static int ApplyWorkerPlan(int imageCount, IReadOnlyDictionary<string, int> formats)
    {
        var plan = ImageWorkerPlan.Create(
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            configuredWorkers: null,
            imageCount,
            encodesAvif: formats.ContainsKey("avif"));
        NetVipsImageProcessor.SetThreadsPerImage(plan.ThreadsPerImage);
        return plan.Workers;
    }

    /// <summary>
    /// Returns a cached, deterministic photo-like JPEG (smoothed noise, camera-style dimensions).
    /// </summary>
    public static string GetTestImage(int width, int height)
    {
        var directory = Path.Combine(Path.GetTempPath(), "revela-benchmarks");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"source-{width}x{height}.jpg"));
        if (File.Exists(path))
        {
            return path;
        }

        using var noise = Image.Gaussnoise(width, height, mean: 128, sigma: 40, seed: 42);
        using var smooth = noise.Gaussblur(1.5);
        using var rgb = smooth.Bandjoin(smooth.Rot180(), smooth.Flip(Enums.Direction.Horizontal));
        using var srgb = rgb.Cast(Enums.BandFormat.Uchar).Copy(interpretation: Enums.Interpretation.Srgb);
        var temporaryPath = path + ".tmp.jpg";
        srgb.Jpegsave(temporaryPath, q: 95);
        File.Move(temporaryPath, path, overwrite: true);
        return path;
    }

    public static string CreateOutputDirectory(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "revela-benchmarks", $"{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static void ClearDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.GetDirectories(directory))
        {
            Directory.Delete(child, recursive: true);
        }
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

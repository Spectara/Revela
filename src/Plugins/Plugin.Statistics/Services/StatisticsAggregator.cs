using System.Globalization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models.Manifest;
using Spectara.Revela.Plugin.Statistics.Configuration;
using Spectara.Revela.Plugin.Statistics.Models;

namespace Spectara.Revela.Plugin.Statistics.Services;

/// <summary>
/// Aggregates EXIF data from manifest into statistics
/// </summary>
/// <remarks>
/// Uses non-generic ILogger to allow instantiation with any logger type.
/// This enables lazy instantiation from StatsCommand without DI.
/// </remarks>
public sealed partial class StatisticsAggregator(
    IManifestRepository manifestRepository,
    IOptionsMonitor<StatisticsPluginConfig> config,
    ILogger logger)
{
    #region Bucket Definitions

    /// <summary>
    /// Aperture ranges (f-stop buckets)
    /// </summary>
    private static readonly (double Min, double Max, string Label)[] ApertureBuckets =
    [
        (1.0, 1.4, "f/1.0-1.4"),
        (1.4, 2.0, "f/1.4-2.0"),
        (2.0, 2.8, "f/2.0-2.8"),
        (2.8, 4.0, "f/2.8-4.0"),
        (4.0, 5.6, "f/4.0-5.6"),
        (5.6, 8.0, "f/5.6-8.0"),
        (8.0, 11.0, "f/8.0-11.0"),
        (11.0, 16.0, "f/11.0-16.0"),
        (16.0, 22.0, "f/16.0-22.0"),
        (22.0, 64.0, "f/22.0+")
    ];

    /// <summary>
    /// Focal length ranges (photography categories)
    /// </summary>
    private static readonly (double Min, double Max, string Label)[] FocalLengthBuckets =
    [
        (0, 18, "10-18mm"),
        (18, 35, "18-35mm"),
        (35, 70, "35-70mm"),
        (70, 135, "70-135mm"),
        (135, 300, "135-300mm"),
        (300, 10000, "300mm+")
    ];

    /// <summary>
    /// ISO sensitivity ranges
    /// </summary>
    private static readonly (int Min, int Max, string Label)[] IsoBuckets =
    [
        (0, 100, "ISO 50-100"),
        (100, 400, "ISO 100-400"),
        (400, 800, "ISO 400-800"),
        (800, 1600, "ISO 800-1600"),
        (1600, 3200, "ISO 1600-3200"),
        (3200, 6400, "ISO 3200-6400"),
        (6400, int.MaxValue, "ISO 6400+")
    ];

    #endregion

    /// <summary>
    /// Aggregate statistics from manifest
    /// </summary>
    public SiteStatistics Aggregate()
    {
        var images = manifestRepository.Images.Values.ToList();
        var imagesWithExif = images.Where(i => i.Exif is not null).ToList();
        var galleries = CountGalleries(manifestRepository.Root);

        LogAggregating(logger, images.Count, imagesWithExif.Count);

        var settings = config.CurrentValue;

        return new SiteStatistics
        {
            TotalImages = images.Count,
            ImagesWithExif = imagesWithExif.Count,
            TotalGalleries = galleries,
            CameraModels = AggregateExact(
                imagesWithExif,
                i => FormatCameraModel(i.Exif!.Make, i.Exif!.Model),
                settings),
            LensModels = AggregateExact(
                imagesWithExif,
                i => i.Exif!.LensModel,
                settings),
            FocalLengths = AggregateBucketed(
                imagesWithExif,
                i => i.Exif!.FocalLength,
                FocalLengthBuckets,
                settings),
            Apertures = AggregateBucketed(
                imagesWithExif,
                i => i.Exif!.FNumber,
                ApertureBuckets,
                settings),
            IsoValues = AggregateIsoBucketed(imagesWithExif, settings),
            ShutterSpeeds = AggregateExact(
                imagesWithExif,
                i => FormatShutterSpeed(i.Exif!.ExposureTime),
                settings),
            ImagesByYear = AggregateByYear(imagesWithExif),
            GeneratedAt = DateTime.UtcNow
        };
    }

    #region Aggregation Methods

    private static List<StatisticsEntry> AggregateExact(
        IEnumerable<ImageContent> images,
        Func<ImageContent, string?> selector,
        StatisticsPluginConfig settings)
    {
        var counts = images
            .Select(selector)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!)
            .Select(g => (Label: g.Key, Count: g.Count()))
            .ToList();

        return ApplySettingsAndCalculatePercent(counts, settings);
    }

    private static List<StatisticsEntry> AggregateBucketed(
        IEnumerable<ImageContent> images,
        Func<ImageContent, double?> selector,
        (double Min, double Max, string Label)[] buckets,
        StatisticsPluginConfig settings)
    {
        var values = images
            .Select(selector)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        var counts = buckets
            .Select(b => (b.Label, Count: values.Count(v => v >= b.Min && v < b.Max)))
            .Where(c => c.Count > 0)
            .ToList();

        return ApplySettingsAndCalculatePercent(counts, settings);
    }

    private static List<StatisticsEntry> AggregateIsoBucketed(
        IEnumerable<ImageContent> images,
        StatisticsPluginConfig settings)
    {
        var values = images
            .Where(i => i.Exif?.Iso.HasValue == true)
            .Select(i => i.Exif!.Iso!.Value)
            .ToList();

        var counts = IsoBuckets
            .Select(b => (b.Label, Count: values.Count(v => v >= b.Min && v < b.Max)))
            .Where(c => c.Count > 0)
            .ToList();

        return ApplySettingsAndCalculatePercent(counts, settings);
    }

    private static List<StatisticsEntry> AggregateByYear(IEnumerable<ImageContent> images)
    {
        var counts = images
            .Where(i => i.DateTaken.HasValue)
            .GroupBy(i => i.DateTaken!.Value.Year.ToString(CultureInfo.InvariantCulture))
            .Select(g => (Label: g.Key, Count: g.Count()))
            .ToList();

        // Always sort years descending (most recent first)
        var sorted = counts.OrderByDescending(c => c.Label).ToList();

        return CalculatePercent(sorted);
    }

    private static List<StatisticsEntry> ApplySettingsAndCalculatePercent(
        List<(string Label, int Count)> counts,
        StatisticsPluginConfig settings)
    {
        // Sort
        List<(string Label, int Count)> sorted = settings.SortByFrequency
            ? [.. counts.OrderByDescending(c => c.Count)]
            : [.. counts.OrderBy(c => c.Label)];

        // Limit entries if configured
        if (settings.MaxEntriesPerCategory > 0 && sorted.Count > settings.MaxEntriesPerCategory)
        {
            var top = sorted.Take(settings.MaxEntriesPerCategory).ToList();
            var otherCount = sorted.Skip(settings.MaxEntriesPerCategory).Sum(c => c.Count);
            if (otherCount > 0)
            {
                top.Add(("Other", otherCount));
            }

            sorted = top;
        }

        return CalculatePercent(sorted);
    }

    private static List<StatisticsEntry> CalculatePercent(List<(string Label, int Count)> sorted)
    {
        if (sorted.Count == 0)
        {
            return [];
        }

        var maxCount = sorted.Max(c => c.Count);

        return
        [
            .. sorted.Select(c => new StatisticsEntry
            {
                Label = c.Label,
                Count = c.Count,
                Percent = maxCount > 0 ? (int)Math.Round(c.Count * 100.0 / maxCount) : 0
            })
        ];
    }

    #endregion

    #region Helper Methods

    private static int CountGalleries(ManifestEntry? root)
    {
        if (root is null)
        {
            return 0;
        }

        var count = 0;
        var queue = new Queue<ManifestEntry>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var entry = queue.Dequeue();

            // A gallery is an entry with image content
            if (entry.Content.OfType<ImageContent>().Any())
            {
                count++;
            }

            foreach (var child in entry.Children)
            {
                queue.Enqueue(child);
            }
        }

        return count;
    }

    private static string? FormatCameraModel(string? make, string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        // If model already contains make, don't duplicate
        if (!string.IsNullOrWhiteSpace(make) &&
            !model.Contains(make, StringComparison.OrdinalIgnoreCase))
        {
            return $"{make} {model}";
        }

        return model;
    }

    private static string? FormatShutterSpeed(double? exposureTime)
    {
        if (!exposureTime.HasValue || exposureTime.Value <= 0)
        {
            return null;
        }

        var value = exposureTime.Value;

        // Format as fraction for fast shutter speeds
        if (value < 1)
        {
            var denominator = (int)Math.Round(1.0 / value);
            return string.Create(CultureInfo.InvariantCulture, $"1/{denominator}");
        }

        // Format as seconds for long exposures
        return string.Create(CultureInfo.InvariantCulture, $"{value:0.#}s");
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Aggregating statistics from {TotalImages} images ({ImagesWithExif} with EXIF)")]
    private static partial void LogAggregating(ILogger logger, int totalImages, int imagesWithExif);

    #endregion
}

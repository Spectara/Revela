using Spectara.Revela.Plugin.Statistics.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Plugin.Statistics.Pipeline;

/// <summary>
/// Pipeline step for statistics generation.
/// </summary>
/// <remarks>
/// Generates statistics JSON from EXIF data in manifest.
/// Runs at order 200 (after scan, before pages).
/// </remarks>
public sealed class StatisticsPipelineStep(
    ILogger<StatisticsPipelineStep> logger,
    IManifestRepository manifestRepository,
    StatisticsAggregator aggregator) : IGeneratePipelineStep
{
    private const string ManifestPath = ".cache/manifest.json";

    /// <inheritdoc />
    public string Name => "statistics";

    /// <inheritdoc />
    public string Description => "Generate statistics from EXIF data";

    /// <inheritdoc />
    public int Order => PipelineOrder.Statistics;

    /// <inheritdoc />
    public async Task<PipelineStepResult> ExecuteAsync(
        IProgress<PipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if manifest exists
            var manifestFile = Path.Combine(Directory.GetCurrentDirectory(), ManifestPath);
            if (!File.Exists(manifestFile))
            {
                return PipelineStepResult.Fail("No manifest found. Run scan first.");
            }

            // Load manifest
            logger.LoadingManifest();
            await manifestRepository.LoadAsync(cancellationToken);

            if (manifestRepository.Images.Count == 0)
            {
                return PipelineStepResult.Skipped("No images in manifest");
            }

            // Find all pages that need statistics
            var root = manifestRepository.Root
                ?? throw new InvalidOperationException("Manifest root is null after loading");

            var statsPages = FindStatisticsPages(root);

            if (statsPages.Count == 0)
            {
                return PipelineStepResult.Skipped("No statistics pages configured");
            }

            logger.GeneratingStats(statsPages.Count);
            progress?.Report(new PipelineProgress(0, statsPages.Count, "Generating statistics..."));

            var generatedCount = 0;
            foreach (var page in statsPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Aggregate statistics
                var stats = aggregator.Aggregate();

                // Calculate output path in .cache/{page.Path}/
                var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), ".cache", page.Path);
                var jsonPath = Path.Combine(cacheDir, "statistics.json");

                // Write JSON data file
                Directory.CreateDirectory(cacheDir);
                await JsonWriter.WriteAsync(jsonPath, stats, cancellationToken);

                logger.GeneratedJsonFile(page.Path, stats.TotalImages);
                generatedCount++;

                progress?.Report(new PipelineProgress(generatedCount, statsPages.Count, page.Path));
            }

            return PipelineStepResult.Ok(
                $"{generatedCount} pages, {manifestRepository.Images.Count} images",
                generatedCount);
        }
        catch (OperationCanceledException)
        {
            return PipelineStepResult.Fail("Cancelled");
        }
        catch (Exception ex)
        {
            logger.GenerationFailed(ex);
            return PipelineStepResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Recursively find all pages with statistics data source.
    /// </summary>
    private static List<(string Path, string Slug)> FindStatisticsPages(ManifestEntry root)
    {
        var results = new List<(string Path, string Slug)>();
        FindRecursive(root, results);
        return results;

        static void FindRecursive(ManifestEntry node, List<(string Path, string Slug)> results)
        {
            if (node.DataSources.ContainsKey("statistics"))
            {
                results.Add((node.Path, node.Slug ?? string.Empty));
            }

            foreach (var child in node.Children)
            {
                FindRecursive(child, results);
            }
        }
    }
}

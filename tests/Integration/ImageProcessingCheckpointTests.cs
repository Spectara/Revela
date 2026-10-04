using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Regression tests for persisting image processing progress during long runs.
/// </summary>
/// <remarks>
/// Processed images are only skipped when their fingerprint was saved. Without checkpoints an
/// interrupted run (Ctrl+C, crash, closed SSH session) lost all progress and the next run
/// re-encoded every image — costly on the upgrade that re-encodes a whole library anyway.
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class ImageProcessingCheckpointTests
{
    private const string GalleryName = "Photos";

    [TestMethod]
    [DataRow(false, DisplayName = "failure")]
    [DataRow(true, DisplayName = "cancellation")]
    public async Task ProcessAsync_InterruptedRun_NextRunProcessesOnlyRemainingImages(bool cancel)
    {
        using var project = CreateProject(imageCount: 3);
        using var cancellation = new CancellationTokenSource();
        var interrupting = new ScriptedProcessor(call =>
        {
            if (call < 3)
            {
                return;
            }

            if (cancel)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }

            throw new IOException("Simulated failure while encoding.");
        });

        using (var host = BuildHost(project, interrupting))
        {
            await ScanAsync(host);
            var imageService = host.Services.GetRequiredService<IImageService>();
            if (cancel)
            {
                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => imageService.ProcessAsync(new ProcessImagesOptions(), cancellationToken: cancellation.Token));
            }
            else
            {
                var failed = await imageService.ProcessAsync(new ProcessImagesOptions());
                Assert.IsFalse(failed.Success);
            }
        }

        using (var host = BuildHost(project, new ScriptedProcessor(_ => { })))
        {
            await ScanAsync(host);
            var rerun = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());

            Assert.IsTrue(rerun.Success, rerun.ErrorMessage);
            Assert.AreEqual(1, rerun.ProcessedCount, "Only the interrupted image must be processed again.");
            Assert.AreEqual(2, rerun.SkippedCount);
        }
    }

    [TestMethod]
    public async Task ProcessAsync_LongRun_PersistsProgressBeforeItEnds()
    {
        const int imageCount = 27;
        using var project = CreateProject(imageCount);
        var statePath = Path.Combine(project.RootPath, ".revela", "core", "images.json");
        var savedBeforeLastImage = -1;
        var observing = new ScriptedProcessor(call =>
        {
            if (call == imageCount)
            {
                savedBeforeLastImage = CountProcessedImages(statePath);
            }
        });

        using var host = BuildHost(project, observing);
        await ScanAsync(host);
        var result = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsGreaterThanOrEqualTo(25, savedBeforeLastImage, "Progress must be checkpointed during the run, not only at the end.");
    }

    [TestMethod]
    public async Task ProcessAsync_QualityChangeInterrupted_NextRunProcessesOnlyRemainingImages()
    {
        using var project = CreateProject(imageCount: 3);
        using (var host = BuildHost(project, new ScriptedProcessor(_ => { })))
        {
            await ScanAsync(host);
            var initial = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());
            Assert.AreEqual(3, initial.ProcessedCount);
        }

        WriteProjectJson(project, jpgQuality: 60);
        var failing = new ScriptedProcessor(call =>
        {
            if (call == 3)
            {
                throw new IOException("Simulated failure while encoding.");
            }
        });
        using (var host = BuildHost(project, failing))
        {
            await ScanAsync(host);
            var failed = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());
            Assert.IsFalse(failed.Success);
        }

        using (var host = BuildHost(project, new ScriptedProcessor(_ => { })))
        {
            await ScanAsync(host);
            var rerun = await host.Services.GetRequiredService<IImageService>().ProcessAsync(new ProcessImagesOptions());

            Assert.IsTrue(rerun.Success, rerun.ErrorMessage);
            Assert.AreEqual(1, rerun.ProcessedCount, "Images re-encoded with the new quality before the failure are current.");
        }
    }

    private static TestProject CreateProject(int imageCount)
    {
        var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Checkpoint", author = "Test" })
            .AddGallery(GalleryName, g =>
            {
                for (var i = 1; i <= imageCount; i++)
                {
                    g.AddRealImage($"photo{i:D2}.jpg", 200, 150);
                }
            }));
        WriteProjectJson(project, jpgQuality: 80);
        return project;
    }

    private static void WriteProjectJson(TestProject project, int jpgQuality) =>
        File.WriteAllText(Path.Combine(project.RootPath, "project.json"), new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "Checkpoint" },
            ["theme"] = new JsonObject { ["name"] = "Lumina" },
            ["generate"] = new JsonObject
            {
                ["images"] = new JsonObject { ["jpg"] = jpgQuality, ["maxDegreeOfParallelism"] = 1 }
            },
        }.ToJsonString());

    private static IHost BuildHost(TestProject project, ScriptedProcessor processor) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IImageSizesProvider>(new FixedSizesProvider([160]));
            services.AddSingleton<NetVipsImageProcessor>();
            services.AddSingleton<IImageProcessor>(sp =>
            {
                processor.Inner = sp.GetRequiredService<NetVipsImageProcessor>();
                return processor;
            });
        });

    private static async Task ScanAsync(IHost host)
    {
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scan.Success, scan.ErrorMessage);
    }

    private static int CountProcessedImages(string statePath)
    {
        if (!File.Exists(statePath))
        {
            return 0;
        }

        var state = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
        return state["images"] is JsonObject processed ? processed.Count : 0;
    }

    /// <summary>
    /// Delegates to the real processor and runs a hook before each image (1-based call number).
    /// </summary>
    private sealed class ScriptedProcessor(Action<int> beforeImage) : IImageProcessor
    {
        private int calls;

        public IImageProcessor? Inner { get; set; }

        public Task<Image> ProcessImageAsync(
            string inputPath,
            ImageProcessingOptions options,
            Action<VariantState, string>? onVariantProgress = null,
            CancellationToken cancellationToken = default)
        {
            beforeImage(Interlocked.Increment(ref calls));
            return Inner!.ProcessImageAsync(inputPath, options, onVariantProgress, cancellationToken);
        }

        public Task<ImageMetadata> ReadMetadataAsync(
            string inputPath,
            CancellationToken cancellationToken = default) =>
            Inner!.ReadMetadataAsync(inputPath, cancellationToken);
    }

    private sealed class FixedSizesProvider(IReadOnlyList<int> sizes) : IImageSizesProvider
    {
        public IReadOnlyList<int> GetSizes() => sizes;

        public string GetResizeMode() => "longest";
    }
}

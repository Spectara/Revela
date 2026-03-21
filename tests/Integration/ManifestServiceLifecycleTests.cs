using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Plugins.Generate;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IManifestRepository"/> load/save lifecycle
/// with real filesystem operations.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ManifestServiceLifecycleTests
{
    [TestMethod]
    public async Task LoadAsync_NoManifest_StartsEmpty()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });

        var manifest = host.Services.GetRequiredService<IManifestRepository>();

        await manifest.LoadAsync();

        Assert.IsNull(manifest.Root);
        Assert.IsEmpty(manifest.Images);
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });

        var manifest = host.Services.GetRequiredService<IManifestRepository>();

        // Build a manifest tree
        var root = new ManifestEntry
        {
            Text = "Test Site",
            Path = "",
            Children =
            [
                new ManifestEntry
                {
                    Text = "Gallery 1",
                    Path = "gallery-1",
                    Content =
                    [
                        new ImageContent
                        {
                            Filename = "photo.jpg",
                            Width = 1920,
                            Height = 1080,
                            Sizes = [320, 640, 1920],
                            FileSize = 500_000,
                            LastModified = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc)
                        }
                    ]
                }
            ]
        };

        manifest.SetRoot(root);
        manifest.ConfigHash = "ABC123";
        manifest.ScanConfigHash = "SCAN456";
        await manifest.SaveAsync();

        // Create a fresh host to simulate restart
        using var host2 = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });
        var manifest2 = host2.Services.GetRequiredService<IManifestRepository>();

        await manifest2.LoadAsync();

        // Verify round-trip
        Assert.IsNotNull(manifest2.Root);
        Assert.AreEqual("Test Site", manifest2.Root.Text);
        Assert.HasCount(1, manifest2.Root.Children);
        Assert.AreEqual("Gallery 1", manifest2.Root.Children[0].Text);

        // Verify image data preserved
        Assert.AreEqual(1, manifest2.Images.Count);
        var image = manifest2.GetImage("gallery-1/photo.jpg");
        Assert.IsNotNull(image);
        Assert.AreEqual(1920, image.Width);
        Assert.AreEqual(1080, image.Height);
        Assert.HasCount(3, image.Sizes);

        // Verify metadata preserved
        Assert.AreEqual("ABC123", manifest2.ConfigHash);
        Assert.AreEqual("SCAN456", manifest2.ScanConfigHash);
    }

    [TestMethod]
    public async Task SetImage_UpdatesExistingEntry()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });

        var manifest = host.Services.GetRequiredService<IManifestRepository>();

        var root = new ManifestEntry
        {
            Text = "Site",
            Path = "",
            Children =
            [
                new ManifestEntry
                {
                    Text = "Photos",
                    Path = "photos",
                    Content =
                    [
                        new ImageContent
                        {
                            Filename = "test.jpg",
                            Width = 1920,
                            Height = 1080,
                            Sizes = [1920]
                        }
                    ]
                }
            ]
        };

        manifest.SetRoot(root);

        // Update the image with new data
        var updated = new ImageContent
        {
            Filename = "test.jpg",
            Width = 1920,
            Height = 1080,
            Sizes = [320, 640, 1920],
            FileSize = 750_000
        };

        manifest.SetImage("photos/test.jpg", updated);

        // Verify update
        var retrieved = manifest.GetImage("photos/test.jpg");
        Assert.IsNotNull(retrieved);
        Assert.HasCount(3, retrieved.Sizes);
        Assert.AreEqual(750_000, retrieved.FileSize);
    }

    [TestMethod]
    public async Task RemoveImage_RemovesFromManifest()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });

        var manifest = host.Services.GetRequiredService<IManifestRepository>();

        var root = new ManifestEntry
        {
            Text = "Site",
            Path = "",
            Children =
            [
                new ManifestEntry
                {
                    Text = "Photos",
                    Path = "photos",
                    Content =
                    [
                        new ImageContent
                        {
                            Filename = "keep.jpg",
                            Width = 1920,
                            Height = 1080,
                            Sizes = [1920]
                        },
                        new ImageContent
                        {
                            Filename = "remove.jpg",
                            Width = 1920,
                            Height = 1080,
                            Sizes = [1920]
                        }
                    ]
                }
            ]
        };

        manifest.SetRoot(root);
        Assert.AreEqual(2, manifest.Images.Count);

        var removed = manifest.RemoveImage("photos/remove.jpg");

        Assert.IsTrue(removed);
        Assert.AreEqual(1, manifest.Images.Count);
        Assert.IsNull(manifest.GetImage("photos/remove.jpg"));
        Assert.IsNotNull(manifest.GetImage("photos/keep.jpg"));
    }

    [TestMethod]
    public async Task FormatQualities_SavedAndLoaded()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });

        var manifest = host.Services.GetRequiredService<IManifestRepository>();

        manifest.SetRoot(new ManifestEntry { Text = "Site", Path = "" });
        manifest.SetFormatQualities(new Dictionary<string, int>
        {
            ["avif"] = 80,
            ["webp"] = 85,
            ["jpg"] = 90
        });

        await manifest.SaveAsync();

        // Reload
        using var host2 = RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });
        var manifest2 = host2.Services.GetRequiredService<IManifestRepository>();
        await manifest2.LoadAsync();

        Assert.AreEqual(3, manifest2.FormatQualities.Count);
        Assert.AreEqual(80, manifest2.FormatQualities["avif"]);
        Assert.AreEqual(85, manifest2.FormatQualities["webp"]);
        Assert.AreEqual(90, manifest2.FormatQualities["jpg"]);
    }
}

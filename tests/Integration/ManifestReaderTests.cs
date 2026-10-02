using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for the host's <see cref="IManifestReader"/> with real files.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ManifestReaderTests
{
    [TestMethod]
    public async Task TryLoadAsync_NoManifest_ReturnsNullWithoutCreatingFiles()
    {
        using var project = TestProject.Create();
        using var host = BuildHost(project);

        var snapshot = await host.Services.GetRequiredService<IManifestReader>().TryLoadAsync();

        Assert.IsNull(snapshot);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("core"))));
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{}")]
    [DataRow(/*lang=json,strict*/ """{"_meta":{"version":1},"root":{"text":"Old","path":""}}""")]
    public async Task TryLoadAsync_UnusableManifest_ReturnsNull(string json)
    {
        using var project = TestProject.Create();
        WriteManifest(project, json);
        using var host = BuildHost(project);

        var snapshot = await host.Services.GetRequiredService<IManifestReader>().TryLoadAsync();

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public async Task TryLoadAsync_AfterScanSave_ReturnsTreeAndImagesKeyedBySourcePath()
    {
        using var project = TestProject.Create();
        using (var writerHost = BuildHost(project))
        {
            var repository = writerHost.Services.GetRequiredService<IManifestRepository>();
            repository.SetRoot(new ManifestEntry
            {
                Text = "Home",
                Path = "",
                Content = [CreateImage("landscapes/shared.jpg")],
                Children =
                [
                    new ManifestEntry
                    {
                        Text = "Landscapes",
                        Path = "landscapes",
                        Content = [CreateImage(sourcePath: "", filename: "shared.jpg"), CreateImage(sourcePath: "", filename: "lake.jpg")]
                    }
                ]
            });
            await repository.SaveAsync();
        }

        using var host = BuildHost(project);
        var snapshot = await host.Services.GetRequiredService<IManifestReader>().TryLoadAsync();

        Assert.IsNotNull(snapshot);
        Assert.AreEqual("Home", snapshot.Root.Text);
        Assert.HasCount(1, snapshot.Root.Children);
        Assert.HasCount(2, snapshot.Images);
        Assert.IsTrue(snapshot.Images.ContainsKey("landscapes/shared.jpg"));
        Assert.IsTrue(snapshot.Images.ContainsKey("landscapes/lake.jpg"));
    }

    private static IHost BuildHost(TestProject project) =>
        RevelaTestHost.Build(project.RootPath, s => { s.AddRevelaCommands(); s.AddGenerateFeature(); });

    private static void WriteManifest(TestProject project, string json)
    {
        var cache = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("core"));
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(cache, "manifest.json"), json);
    }

    private static ImageContent CreateImage(string sourcePath, string? filename = null) => new()
    {
        Filename = filename ?? Path.GetFileName(sourcePath),
        SourcePath = sourcePath,
        Width = 800,
        Height = 600,
        Sizes = [640],
        FileSize = 1_000,
        LastModified = new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc)
    };
}

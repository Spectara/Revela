using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Generate;

/// <summary>
/// <c>clean cache</c> removes reproducible data only; the output state in <c>.revela/state</c>
/// (which image variants exist and with which settings) would cost a full re-encode.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class CleanCacheCommandTests
{
    [TestMethod]
    [DataRow(true, DisplayName = "CLI command")]
    [DataRow(false, DisplayName = "pipeline step")]
    public async Task Clean_CacheAndStateExist_DeletesCacheKeepsState(bool viaCommand)
    {
        using var project = TestProject.Create();
        var manifest = CreateFile(Path.Combine(project.RootPath, ".revela", "cache"), "manifest.json");
        var pageData = CreateFile(Path.Combine(project.RootPath, ".revela", "cache", "stats"), "statistics.json");
        var imageState = CreateFile(Path.Combine(project.RootPath, ".revela", "state"), "images.json");
        var compressState = CreateFile(Path.Combine(project.RootPath, ".revela", "state"), "compress.json");
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddTransient<CleanCacheCommand>());
        var command = host.Services.GetRequiredService<CleanCacheCommand>();

        var succeeded = viaCommand
            ? await command.ExecuteAsync(CancellationToken.None) == 0
            : (await ((IPipelineStep)command).ExecuteAsync(CancellationToken.None)).Success;

        Assert.IsTrue(succeeded);
        Assert.IsFalse(File.Exists(manifest));
        Assert.IsFalse(File.Exists(pageData));
        Assert.IsTrue(File.Exists(imageState), "clean cache must keep the image state.");
        Assert.IsTrue(File.Exists(compressState), "clean cache must keep the compression record.");
    }

    private static string CreateFile(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, "{}");
        return path;
    }
}

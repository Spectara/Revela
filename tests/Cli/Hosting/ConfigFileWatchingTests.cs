using Microsoft.Extensions.Configuration;

using Spectara.Revela.Cli.Hosting;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Cli.Hosting;

/// <summary>
/// Guards against file watching on configuration sources. A reload-on-change file source
/// watches its directory recursively; on Linux that means one inotify watch per sub-directory
/// of the project (source photos, output, cache), which made even <c>revela --version</c>
/// hang on large or network-mounted projects.
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class ConfigFileWatchingTests
{
    [TestMethod]
    public void CreateBuilder_ConfigurationFileSources_DoNotWatchForChanges()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Watch" } }));

        var builder = HostBootstrap.CreateBuilder([], new EmptyPackageSource(), project.RootPath);

        var fileSources = builder.Configuration.Sources
            .OfType<FileConfigurationSource>()
            .ToList();
        var watching = fileSources
            .Where(source => source.ReloadOnChange)
            .Select(source => source.Path)
            .ToList();

        Assert.HasCount(4, fileSources, "Only revela.json, project.json, site.json and logging.json are file sources (no appsettings*.json).");
        Assert.IsEmpty(watching, $"Watching sources: {string.Join(", ", watching)}");
    }

    private sealed class EmptyPackageSource : IPackageSource
    {
        public IReadOnlyList<LoadedPluginInfo> LoadPlugins() => [];

        public IReadOnlyList<LoadedThemeInfo> LoadThemes() => [];
    }
}

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Core;

/// <summary>
/// Loads plugins and themes from disk directories.
/// </summary>
/// <remarks>
/// <para>
/// This is the default package source for the standard CLI.
/// Discovers plugins from:
/// </para>
/// <list type="bullet">
/// <item>Application directory (ProjectReference in development, bundled in release)</item>
/// <item>User plugin directory (~/.revela/plugins or %APPDATA%/Revela/plugins)</item>
/// </list>
/// </remarks>
public sealed class DiskPackageSource : IPackageSource
{
    private readonly PackageOptions options;
    private IReadOnlyList<LoadedPluginInfo>? plugins;
    private IReadOnlyList<LoadedThemeInfo>? themes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiskPackageSource"/> class.
    /// </summary>
    /// <param name="options">Package loading options, or null for defaults.</param>
    public DiskPackageSource(PackageOptions? options = null) => this.options = options ?? new PackageOptions();

    /// <inheritdoc />
    public IReadOnlyList<LoadedPluginInfo> LoadPlugins()
    {
        EnsureLoaded();
        return plugins!;
    }

    /// <inheritdoc />
    public IReadOnlyList<LoadedThemeInfo> LoadThemes()
    {
        EnsureLoaded();
        return themes!;
    }

    private void EnsureLoaded()
    {
        if (plugins is not null)
        {
            return;
        }

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var logger = loggerFactory.CreateLogger<PackageLoader>();
        var loader = new PackageLoader(options, logger);
        loader.Load();
        plugins = loader.GetLoadedPlugins();
        themes = loader.GetLoadedThemes();
    }
}

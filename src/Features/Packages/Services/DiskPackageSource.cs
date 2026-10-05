using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Features.Packages.Services;

/// <summary>
/// Loads plugins and themes from disk directories.
/// </summary>
/// <remarks>
/// <para>
/// This is the default package source for the standard CLI.
/// Discovers plugins from:
/// </para>
/// <list type="bullet">
/// <item>User plugin directory (~/.revela/plugins or %APPDATA%/Revela/plugins)</item>
/// <item>Application directory, in Development only (ProjectReference builds, see <see cref="PackageOptions.SearchApplicationDirectory"/>)</item>
/// </list>
/// </remarks>
/// <param name="options">Where to look for packages.</param>
/// <param name="loggerFactory">Bootstrap logger factory; only used while packages are loaded.</param>
internal sealed class DiskPackageSource(PackageOptions options, ILoggerFactory loggerFactory) : IPackageSource
{
    private IReadOnlyList<LoadedPluginInfo>? plugins;
    private IReadOnlyList<LoadedThemeInfo>? themes;

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

        var loader = new PackageLoader(options, loggerFactory.CreateLogger<PackageLoader>());
        loader.Load();
        plugins = loader.GetLoadedPlugins();
        themes = loader.GetLoadedThemes();
    }
}

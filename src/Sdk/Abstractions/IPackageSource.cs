namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Abstraction for loading plugins and themes.
/// </summary>
/// <remarks>
/// <para>
/// Two implementations exist:
/// </para>
/// <list type="bullet">
/// <item><b>DiskPackageSource</b> — discovers plugins and themes from disk (default CLI behavior)</item>
/// <item><b>EmbeddedPackageSource</b> — returns statically referenced plugins and themes (AOT-compatible)</item>
/// </list>
/// </remarks>
public interface IPackageSource
{
    /// <summary>
    /// Gets all available plugins.
    /// </summary>
    IReadOnlyList<LoadedPluginInfo> LoadPlugins();

    /// <summary>
    /// Gets all available themes.
    /// </summary>
    IReadOnlyList<LoadedThemeInfo> LoadThemes();
}

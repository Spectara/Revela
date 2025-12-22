namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Indicates where a plugin was loaded from.
/// </summary>
public enum PluginSource
{
    /// <summary>
    /// Plugin bundled with the application (built-in themes, ProjectReference in development).
    /// </summary>
    Bundled,

    /// <summary>
    /// Plugin loaded from local plugins folder (next to executable or working directory).
    /// </summary>
    Local,

    /// <summary>
    /// Plugin loaded from global user directory (AppData/Revela/plugins).
    /// </summary>
    Global
}

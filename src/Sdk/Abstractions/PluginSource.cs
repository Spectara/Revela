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
    /// Plugin installed in the plugins directory.
    /// Location depends on installation type:
    /// - Standalone: {exe-dir}/plugins
    /// - dotnet tool: %APPDATA%/Revela/plugins
    /// </summary>
    Local
}

using System.Collections.ObjectModel;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Options for plugin loading behavior.
/// </summary>
/// <remarks>
/// Configure via AddPlugins() method:
/// <code>
/// services.AddPlugins(configuration, options =&gt; {
///     options.SearchApplicationDirectory = true;
///     options.AdditionalSearchPaths.Add("./dev-plugins");
/// });
/// </code>
/// </remarks>
public sealed class PluginOptions
{
    /// <summary>
    /// Whether to search for plugins in the application directory.
    /// </summary>
    /// <remarks>
    /// When true, plugins built alongside the application (e.g., via ProjectReference)
    /// will be discovered automatically. This enables seamless debugging without
    /// requiring plugin installation.
    /// Default: true
    /// </remarks>
    public bool SearchApplicationDirectory { get; set; } = true;

    /// <summary>
    /// Whether to search for plugins in the user's plugin directory (AppData/Revela/plugins).
    /// </summary>
    /// <remarks>
    /// This is where plugins installed via 'revela plugin install' are stored.
    /// Default: true
    /// </remarks>
    public bool SearchUserPluginDirectory { get; set; } = true;

    /// <summary>
    /// Additional directories to search for plugins.
    /// </summary>
    /// <remarks>
    /// Paths can be absolute or relative to the current working directory.
    /// Useful for development scenarios or custom plugin locations.
    /// </remarks>
    public Collection<string> AdditionalSearchPaths { get; } = [];

    /// <summary>
    /// Whether to fail if no plugins are found.
    /// </summary>
    /// <remarks>
    /// If true, throws exception when no plugins are discovered.
    /// If false, continues execution with core commands only.
    /// Default: false
    /// </remarks>
    public bool RequirePlugins { get; set; }

    /// <summary>
    /// Whether to enable detailed logging during plugin loading.
    /// </summary>
    public bool EnableVerboseLogging { get; set; }

    /// <summary>
    /// Plugin assembly patterns to exclude (e.g., "*.Tests.dll").
    /// </summary>
    public Collection<string> ExcludePatterns { get; } = [];
}

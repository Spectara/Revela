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

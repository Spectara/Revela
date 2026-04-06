using System.Collections.ObjectModel;

namespace Spectara.Revela.Sdk.Configuration;

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
    /// Plugin assembly filename patterns to exclude during discovery.
    /// </summary>
    /// <remarks>
    /// Supports leading wildcard patterns only:
    /// <list type="bullet">
    /// <item><c>*.Tests.dll</c> — excludes test assemblies</item>
    /// <item><c>*.Mock.dll</c> — excludes mock assemblies</item>
    /// </list>
    /// Complex patterns like <c>*.Tests.*.dll</c> or trailing wildcards are not supported.
    /// </remarks>
    public Collection<string> ExcludePatterns { get; } = [];
}

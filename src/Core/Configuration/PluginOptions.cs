namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Options for plugin loading behavior
/// </summary>
/// <remarks>
/// Configure via AddPlugins() method:
/// services.AddPlugins(configuration, options => {
///     options.PluginsDirectory = "custom-plugins";
///     options.RequirePlugins = true;
/// });
/// </remarks>
public sealed class PluginOptions
{
    /// <summary>
    /// Directory to load plugins from (relative to application directory)
    /// </summary>
    public string PluginsDirectory { get; set; } = "plugins";

    /// <summary>
    /// Whether to fail if no plugins are found
    /// </summary>
    /// <remarks>
    /// If true, throws exception when plugins directory is empty or doesn't exist.
    /// If false, continues execution with core commands only.
    /// </remarks>
    public bool RequirePlugins { get; set; }

    /// <summary>
    /// Whether to enable detailed logging during plugin loading
    /// </summary>
    public bool EnableVerboseLogging { get; set; }

    /// <summary>
    /// Plugin assembly patterns to exclude (e.g., "*.Test.dll", "*.Experimental.dll")
    /// </summary>
    public System.Collections.ObjectModel.Collection<string> ExcludePatterns { get; } = [];
}

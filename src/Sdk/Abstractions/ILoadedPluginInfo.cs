namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Information about a loaded plugin including its source location.
/// </summary>
public interface ILoadedPluginInfo
{
    /// <summary>
    /// The loaded plugin instance.
    /// </summary>
    IPlugin Plugin { get; }

    /// <summary>
    /// Where the plugin was loaded from.
    /// </summary>
    PluginSource Source { get; }
}

/// <summary>
/// Default implementation of <see cref="ILoadedPluginInfo"/>.
/// </summary>
/// <param name="Plugin">The loaded plugin instance.</param>
/// <param name="Source">Where the plugin was loaded from.</param>
public sealed record LoadedPluginInfo(IPlugin Plugin, PluginSource Source) : ILoadedPluginInfo;

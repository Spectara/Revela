namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Information about a loaded plugin including its source location.
/// </summary>
/// <param name="Plugin">The loaded plugin instance.</param>
/// <param name="Source">Where the plugin was loaded from.</param>
public sealed record LoadedPluginInfo(IPlugin Plugin, PluginSource Source);

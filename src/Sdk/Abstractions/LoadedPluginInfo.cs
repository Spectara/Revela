namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Information about a loaded plugin including its source location.
/// </summary>
/// <param name="Plugin">The loaded plugin instance.</param>
/// <param name="Source">Where the plugin was loaded from.</param>
public sealed record LoadedPluginInfo(IPlugin Plugin, PackageSource Source);

/// <summary>
/// Information about a loaded theme provider including its source location.
/// </summary>
/// <param name="Theme">The loaded theme provider instance.</param>
/// <param name="Source">Where the theme was loaded from.</param>
public sealed record LoadedThemeInfo(ITheme Theme, PackageSource Source);

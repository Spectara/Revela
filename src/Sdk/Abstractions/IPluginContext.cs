using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Context for managing loaded plugins
/// </summary>
/// <remarks>
/// Returned by AddPlugins() extension method to allow plugin initialization
/// and command registration after ServiceProvider is built.
/// </remarks>
public interface IPluginContext
{
    /// <summary>
    /// All loaded plugins
    /// </summary>
    IReadOnlyList<IPlugin> Plugins { get; }

    /// <summary>
    /// Initialize all plugins with the built ServiceProvider
    /// </summary>
    /// <remarks>
    /// Must be called after host.Build() so ServiceProvider is available.
    /// Calls Initialize() on each plugin with error handling.
    /// </remarks>
    /// <param name="serviceProvider">Built service provider from host</param>
    void Initialize(IServiceProvider serviceProvider);

    /// <summary>
    /// Register all plugin commands with the root command
    /// </summary>
    /// <remarks>
    /// Handles parent command creation and duplicate detection.
    /// Should be called after Initialize().
    /// </remarks>
    /// <param name="rootCommand">Root command to register plugin commands under</param>
    /// <param name="onCommandRegistered">Optional callback when a command is registered, receives command, order, and optional group name.</param>
    void RegisterCommands(RootCommand rootCommand, Action<Command, int, string?>? onCommandRegistered = null);
}

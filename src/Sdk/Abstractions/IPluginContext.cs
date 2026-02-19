using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Context for managing loaded plugins after host is built.
/// </summary>
/// <remarks>
/// Resolved from DI after <c>host.Build()</c>.
/// Provides access to loaded plugins and handles command registration.
/// </remarks>
public interface IPluginContext
{
    /// <summary>
    /// All loaded plugins with their source information.
    /// </summary>
    IReadOnlyList<ILoadedPluginInfo> Plugins { get; }

    /// <summary>
    /// Register all plugin commands with the root command.
    /// </summary>
    /// <remarks>
    /// Calls <see cref="IPlugin.GetCommands"/> on each plugin with the built service provider.
    /// Handles parent command creation and duplicate detection.
    /// </remarks>
    /// <param name="rootCommand">Root command to register plugin commands under.</param>
    /// <param name="services">Built service provider for resolving commands from DI.</param>
    /// <param name="onCommandRegistered">Optional callback when a command is registered.</param>
    void RegisterCommands(RootCommand rootCommand, IServiceProvider services, Action<Command, int, string?, bool, bool>? onCommandRegistered = null);
}

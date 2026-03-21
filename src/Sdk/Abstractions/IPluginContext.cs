using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Callback invoked when a command is registered during plugin command registration.
/// </summary>
/// <param name="command">The registered command.</param>
/// <param name="order">Display order for interactive menu.</param>
/// <param name="group">Optional group name for categorization.</param>
/// <param name="requiresProject">Whether the command requires a project context.</param>
/// <param name="hideWhenProjectExists">Whether to hide the command when a project exists.</param>
/// <param name="isSequentialStep">Whether this command is a sequential pipeline step (included in "all").</param>
public delegate void CommandRegisteredCallback(Command command, int order, string? group, bool requiresProject, bool hideWhenProjectExists, bool isSequentialStep);

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
    IReadOnlyList<LoadedPluginInfo> Plugins { get; }

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
    void RegisterCommands(RootCommand rootCommand, IServiceProvider services, CommandRegisteredCallback? onCommandRegistered = null);
}

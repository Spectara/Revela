using System.CommandLine;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Callback invoked when a command is registered during plugin command registration.
/// </summary>
public delegate void CommandRegisteredCallback(Command command, CommandDescriptor descriptor);

/// <summary>
/// Context for managing loaded plugins and themes after host is built.
/// </summary>
public interface IPackageContext
{
    /// <summary>
    /// All loaded plugins with their source information.
    /// </summary>
    IReadOnlyList<LoadedPluginInfo> Plugins { get; }

    /// <summary>
    /// All loaded theme providers with their source information.
    /// </summary>
    IReadOnlyList<LoadedThemeInfo> Themes { get; }

    /// <summary>
    /// Register all plugin commands with the root command.
    /// </summary>
    void RegisterCommands(RootCommand rootCommand, IServiceProvider services, CommandRegisteredCallback? onCommandRegistered = null);
}

using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Provides host-owned command descriptors for unified registration.
/// Plugin commands (generate, clean, create, theme, projects) are registered
/// by their respective plugins via <see cref="IPlugin.GetCommands"/>.
/// </summary>
internal sealed class CoreCommandProvider : ICommandProvider
{
    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Setup group: config (10)
        var configCommand = services.GetRequiredService<ConfigCommand>();
        yield return new CommandDescriptor(
            configCommand.Create(),
            ParentCommand: null,
            Order: 10,
            Group: CommandGroups.Setup,
            RequiresProject: false);

        // Addons group: restore (5), plugin (20), packages (30)
        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        yield return new CommandDescriptor(
            restoreCommand.Create(),
            ParentCommand: null,
            Order: 5,
            Group: CommandGroups.Addons,
            RequiresProject: true);

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        yield return new CommandDescriptor(
            pluginCommand.Create(),
            ParentCommand: null,
            Order: 20,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        var packagesCommand = services.GetRequiredService<PackagesCommand>();
        yield return new CommandDescriptor(
            packagesCommand.Create(),
            ParentCommand: null,
            Order: 30,
            Group: CommandGroups.Addons,
            RequiresProject: false);
    }
}

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands.Clean.Commands;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Create;
using Spectara.Revela.Commands.Generate.Commands;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Projects;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Commands.Theme;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Provides all core command descriptors for unified registration.
/// </summary>
/// <remarks>
/// Core commands use the same CommandDescriptor pattern as plugins,
/// enabling unified registration logic in HostExtensions.
/// </remarks>
internal sealed class CoreCommandProvider : ICommandProvider
{
    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // Build group: generate (10), clean (20) - require project
        var generateCommand = services.GetRequiredService<GenerateCommand>();
        yield return new CommandDescriptor(
            generateCommand.Create(),
            ParentCommand: null,
            Order: 10,
            Group: CommandGroups.Build,
            RequiresProject: true);

        var cleanCommand = services.GetRequiredService<CleanCommand>();
        yield return new CommandDescriptor(
            cleanCommand.Create(),
            ParentCommand: null,
            Order: 20,
            Group: CommandGroups.Build,
            RequiresProject: true);

        // Content group: create (10) - requires project (creates pages in project)
        var createCommand = services.GetRequiredService<CreateCommand>();
        yield return new CommandDescriptor(
            createCommand.Create(),
            ParentCommand: null,
            Order: 10,
            Group: CommandGroups.Content,
            RequiresProject: true);

        // Setup group: config (10)
        // config has mixed requirements (subcommands registered separately)
        var configCommand = services.GetRequiredService<ConfigCommand>();
        yield return new CommandDescriptor(
            configCommand.Create(),
            ParentCommand: null,
            Order: 10,
            Group: CommandGroups.Setup,
            RequiresProject: false);  // Parent doesn't require, subcommands vary

        // Addons group: restore (5), theme (10), plugin (20), packages (30)
        // restore requires project (restores theme for current project)
        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        yield return new CommandDescriptor(
            restoreCommand.Create(),
            ParentCommand: null,
            Order: 5,
            Group: CommandGroups.Addons,
            RequiresProject: true);

        // theme doesn't require project (install/list work globally)
        var themeCommand = services.GetRequiredService<ThemeCommand>();
        yield return new CommandDescriptor(
            themeCommand.Create(),
            ParentCommand: null,
            Order: 10,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        // plugin doesn't require project (install/list work globally)
        var pluginCommand = services.GetRequiredService<PluginCommand>();
        yield return new CommandDescriptor(
            pluginCommand.Create(),
            ParentCommand: null,
            Order: 20,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        // packages doesn't require project (refresh/search work globally)
        var packagesCommand = services.GetRequiredService<PackagesCommand>();
        yield return new CommandDescriptor(
            packagesCommand.Create(),
            ParentCommand: null,
            Order: 30,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        // Projects command: only in standalone mode
        if (ConfigPathResolver.IsStandaloneMode)
        {
            var projectsCommand = services.GetRequiredService<ProjectsCommand>();
            yield return new CommandDescriptor(
                projectsCommand.Create(),
                ParentCommand: null,
                Order: 5,  // Before config in Setup group
                Group: CommandGroups.Setup,
                RequiresProject: false);
        }
    }
}

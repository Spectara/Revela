using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Features.Projects.Commands;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Provides all host-owned command descriptors for unified registration.
/// Includes core features (Generate, Theme, Projects) and management commands.
/// External plugins register commands via <see cref="IPlugin.GetCommands"/>.
/// </summary>
internal sealed class CoreCommandProvider : ICommandProvider
{
    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // ── Build group ──
        yield return new CommandDescriptor(
            GenerateCommand.Create(),
            Order: 10,
            Group: CommandGroups.Build,
            RequiresProject: true);

        var allCommand = services.GetRequiredService<AllCommand>();
        yield return new CommandDescriptor(
            allCommand.Create(),
            ParentCommand: "generate",
            Order: 0);

        var scanCommand = services.GetRequiredService<ScanCommand>();
        yield return new CommandDescriptor(
            scanCommand.Create(),
            ParentCommand: "generate",
            Order: 10,
            IsSequentialStep: true);

        var pagesCommand = services.GetRequiredService<PagesCommand>();
        yield return new CommandDescriptor(
            pagesCommand.Create(),
            ParentCommand: "generate",
            Order: 30,
            IsSequentialStep: true);

        var imagesCommand = services.GetRequiredService<ImagesCommand>();
        yield return new CommandDescriptor(
            imagesCommand.Create(),
            ParentCommand: "generate",
            Order: 40,
            IsSequentialStep: true);

        yield return new CommandDescriptor(
            CleanCommand.Create(),
            Order: 20,
            Group: CommandGroups.Build,
            RequiresProject: true);

        var cleanAllCommand = services.GetRequiredService<CleanAllCommand>();
        yield return new CommandDescriptor(
            cleanAllCommand.Create(),
            ParentCommand: "clean",
            Order: 0);

        var cleanOutputCommand = services.GetRequiredService<CleanOutputCommand>();
        yield return new CommandDescriptor(
            cleanOutputCommand.Create(),
            ParentCommand: "clean",
            Order: 10,
            IsSequentialStep: true);

        var cleanImagesCommand = services.GetRequiredService<CleanImagesCommand>();
        yield return new CommandDescriptor(
            cleanImagesCommand.Create(),
            ParentCommand: "clean",
            Order: 15,
            IsSequentialStep: true);

        var cleanCacheCommand = services.GetRequiredService<CleanCacheCommand>();
        yield return new CommandDescriptor(
            cleanCacheCommand.Create(),
            ParentCommand: "clean",
            Order: 20,
            IsSequentialStep: true);

        // ── Content group ──
        var createCommand = services.GetRequiredService<CreateCommand>();
        yield return new CommandDescriptor(
            createCommand.Create(),
            Order: 10,
            Group: CommandGroups.Content,
            RequiresProject: true);

        // ── Setup group ──
        var configCommand = services.GetRequiredService<ConfigCommand>();
        yield return new CommandDescriptor(
            configCommand.Create(),
            Order: 10,
            Group: CommandGroups.Setup,
            RequiresProject: false);

        // Projects (standalone mode only)
        if (Core.Services.ConfigPathResolver.IsStandaloneMode)
        {
            var projectsCommand = services.GetRequiredService<ProjectsCommand>();
            yield return new CommandDescriptor(
                projectsCommand.Create(),
                Order: 5,
                Group: CommandGroups.Setup,
                RequiresProject: false);
        }

        // ── Addons group ──
        var themeCommand = services.GetRequiredService<ThemeCommand>();
        yield return new CommandDescriptor(
            themeCommand.Create(),
            Order: 10,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        yield return new CommandDescriptor(
            restoreCommand.Create(),
            Order: 5,
            Group: CommandGroups.Addons,
            RequiresProject: true);

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        yield return new CommandDescriptor(
            pluginCommand.Create(),
            Order: 20,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        var packagesCommand = services.GetRequiredService<PackagesCommand>();
        yield return new CommandDescriptor(
            packagesCommand.Create(),
            Order: 30,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        // ── Config subcommands (from Generate + Theme) ──
        var configImageCommand = services.GetRequiredService<ConfigImageCommand>();
        yield return new CommandDescriptor(
            configImageCommand.Create(),
            ParentCommand: "config",
            Order: 40,
            Group: "Project");

        var configSortingCommand = services.GetRequiredService<ConfigSortingCommand>();
        yield return new CommandDescriptor(
            configSortingCommand.Create(),
            ParentCommand: "config",
            Order: 50,
            Group: "Project");

        var configPathsCommand = services.GetRequiredService<ConfigPathsCommand>();
        yield return new CommandDescriptor(
            configPathsCommand.Create(),
            ParentCommand: "config",
            Order: 30,
            Group: "Project");

        var configThemeCommand = services.GetRequiredService<ConfigThemeCommand>();
        yield return new CommandDescriptor(
            configThemeCommand.Create(),
            ParentCommand: "config",
            Order: 20,
            Group: "Project");
    }
}


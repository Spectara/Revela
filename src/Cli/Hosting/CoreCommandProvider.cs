using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Config;
using Spectara.Revela.Commands.Info;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Features.Generate.Commands;
using Spectara.Revela.Features.Theme.Commands;
using Spectara.Revela.Sdk.Abstractions;
namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Provides all host-owned command descriptors for unified registration.
/// Includes core features (Generate, Theme) and management commands.
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

        var scanCommand = services.GetRequiredService<ScanCommand>();
        yield return new CommandDescriptor(
            scanCommand.Create(),
            ParentCommand: "generate",
            Order: PipelineOrder.Scan,
            IsSequentialStep: true);

        var pagesCommand = services.GetRequiredService<PagesCommand>();
        yield return new CommandDescriptor(
            pagesCommand.Create(),
            ParentCommand: "generate",
            Order: PipelineOrder.Pages,
            IsSequentialStep: true);

        var imagesCommand = services.GetRequiredService<ImagesCommand>();
        yield return new CommandDescriptor(
            imagesCommand.Create(),
            ParentCommand: "generate",
            Order: PipelineOrder.Images,
            IsSequentialStep: true);

        yield return new CommandDescriptor(
            CleanCommand.Create(),
            Order: 20,
            Group: CommandGroups.Build,
            RequiresProject: true);

        var cleanOutputCommand = services.GetRequiredService<CleanOutputCommand>();
        yield return new CommandDescriptor(
            cleanOutputCommand.Create(),
            ParentCommand: "clean",
            Order: CleanPipelineOrder.Output,
            IsSequentialStep: true);

        var cleanImagesCommand = services.GetRequiredService<CleanImagesCommand>();
        yield return new CommandDescriptor(
            cleanImagesCommand.Create(),
            ParentCommand: "clean",
            Order: CleanPipelineOrder.Images,
            IsSequentialStep: true);

        var cleanCacheCommand = services.GetRequiredService<CleanCacheCommand>();
        yield return new CommandDescriptor(
            cleanCacheCommand.Create(),
            ParentCommand: "clean",
            Order: CleanPipelineOrder.Cache,
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

        // `check` command group (structural validation without generating), next to config.
        var checkCommand = services.GetRequiredService<CheckCommand>();
        yield return new CommandDescriptor(
            checkCommand.CreateParent(),
            Order: 20,
            Group: CommandGroups.Setup,
            RequiresProject: true);

        // Bespoke collect-all `check all` (unified report, aggregate exit 2). Registered
        // explicitly so the host does NOT auto-generate a fail-fast `all` for this group.
        yield return new CommandDescriptor(
            checkCommand.CreateAll(),
            ParentCommand: "check",
            Order: 0,
            RequiresProject: true);

        // Host-wrap every registered ICheck (host + plugin) as `check <name>`. Plugins do
        // not hand-write these — they only register the ICheck. Each is a sequential step so
        // it shows the `●` "included in all" marker in the interactive menu.
        foreach (var check in services.GetServices<ICheck>())
        {
            yield return new CommandDescriptor(
                checkCommand.CreateUnit(check),
                ParentCommand: "check",
                Order: CheckOrder(check.Name),
                IsSequentialStep: true,
                RequiresProject: true);
        }

        // ── Addons group ──
        var themeCommand = services.GetRequiredService<ThemeCommand>();
        yield return new CommandDescriptor(
            themeCommand.Create(),
            Order: 10,
            Group: CommandGroups.Addons,
            RequiresProject: false);

        // ── Info group (TUI rendered inline as "Revela") ──
        var infoCommand = services.GetRequiredService<InfoCommand>();
        yield return new CommandDescriptor(
            infoCommand.Create(),
            Order: 10,
            Group: CommandGroups.Info,
            RequiresProject: false,
            InlineInMenu: true,
            InlineDefaultActionLabel: "Revela");

        // Restore, Plugin, and Packages commands are provided by PackagesCommandProvider
        // (only available in Cli, not in Cli.Embedded)

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

    /// <summary>
    /// Maps a check name to its interactive-menu order. Unknown (plugin) checks fall back
    /// to <see cref="CheckPipelineOrder.Plugin"/>.
    /// </summary>
    private static int CheckOrder(string name) => name switch
    {
        "config" => CheckPipelineOrder.Config,
        "structure" => CheckPipelineOrder.Structure,
        "theme" => CheckPipelineOrder.Theme,
        "content" => CheckPipelineOrder.Content,
        "slugs" => CheckPipelineOrder.Slugs,
        _ => CheckPipelineOrder.Plugin,
    };
}


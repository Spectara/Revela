using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands.Config.Feed;
using Spectara.Revela.Commands.Packages;
using Spectara.Revela.Commands.Plugins;
using Spectara.Revela.Commands.Restore;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Features.Packages;

/// <summary>
/// Provides command descriptors for NuGet-based package management commands.
/// </summary>
/// <remarks>
/// Registered only in <c>Cli</c> (dynamic plugin loading).
/// <c>Cli.Embedded</c> does not load this provider — all plugins are statically linked.
/// </remarks>
internal sealed class PackagesCommandProvider : ICommandProvider
{
    /// <inheritdoc />
    public IEnumerable<CommandDescriptor> GetCommands(IServiceProvider services)
    {
        // ── Config subcommands (feed management) ──
        var feedCommand = services.GetRequiredService<FeedCommand>();
        yield return new CommandDescriptor(
            feedCommand.Create(),
            ParentCommand: "config",
            Order: 10,
            Group: "Packages",
            RequiresProject: false);

        // ── Addons group ──
        var restoreCommand = services.GetRequiredService<RestoreCommand>();
        yield return new CommandDescriptor(
            restoreCommand.Create(),
            Order: 5,
            Group: "Addons",
            RequiresProject: true);

        var pluginCommand = services.GetRequiredService<PluginCommand>();
        yield return new CommandDescriptor(
            pluginCommand.Create(),
            Order: 20,
            Group: "Addons",
            RequiresProject: false);

        var packagesCommand = services.GetRequiredService<PackagesCommand>();
        yield return new CommandDescriptor(
            packagesCommand.Create(),
            Order: 30,
            Group: "Addons",
            RequiresProject: false);
    }
}

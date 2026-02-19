using System.CommandLine;

namespace Spectara.Revela.Commands.Clean.Commands;

/// <summary>
/// Parent command for clean operations.
/// </summary>
/// <remarks>
/// <para>
/// Sub-commands for cleaning generated files:
/// </para>
/// <list type="bullet">
///   <item><description>revela clean all - Clean output and cache</description></item>
///   <item><description>revela clean output - Clean only output directory</description></item>
///   <item><description>revela clean images - Clean unused images (smart cleanup)</description></item>
///   <item><description>revela clean cache - Clean only cache directory</description></item>
/// </list>
/// <para>
/// Safety: NEVER deletes source files (source/, config/, *.json configs).
/// </para>
/// </remarks>
internal sealed class CleanCommand(
    CleanAllCommand allCommand,
    CleanOutputCommand outputCommand,
    CleanImagesCommand imagesCommand,
    CleanCacheCommand cacheCommand)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("clean", "Clean generated files");

        // No direct handler - use subcommands

        // Add sub-commands in order: all (0), output (10), images (15), cache (20)
        command.Subcommands.Add(allCommand.Create());
        command.Subcommands.Add(outputCommand.Create());
        command.Subcommands.Add(imagesCommand.Create());
        command.Subcommands.Add(cacheCommand.Create());

        return command;
    }
}

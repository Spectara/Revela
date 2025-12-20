using System.CommandLine;

namespace Spectara.Revela.Commands.Generate.Commands;

/// <summary>
/// Command to generate static site from content.
/// </summary>
/// <remarks>
/// <para>
/// Sub-commands for site generation:
/// </para>
/// <list type="bullet">
///   <item><description>revela generate all - Execute full pipeline</description></item>
///   <item><description>revela generate scan - Scan content only</description></item>
///   <item><description>revela generate images - Process images only</description></item>
///   <item><description>revela generate pages - Render pages only</description></item>
/// </list>
/// <para>
/// To clean output/cache before generating, use: revela clean all
/// </para>
/// </remarks>
public sealed class GenerateCommand(
    ScanCommand scanCommand,
    ImagesCommand imagesCommand,
    PagesCommand pagesCommand,
    AllCommand allCommand)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("generate", "Generate static site from content");

        // No direct handler - use 'generate all' for full pipeline

        // Add sub-commands in execution order (stats is added by plugin)
        // all (Order 0), scan (10), [stats by plugin (20)], pages (30), images (40)
        command.Subcommands.Add(allCommand.Create());
        command.Subcommands.Add(scanCommand.Create());
        command.Subcommands.Add(pagesCommand.Create());
        command.Subcommands.Add(imagesCommand.Create());

        return command;
    }
}

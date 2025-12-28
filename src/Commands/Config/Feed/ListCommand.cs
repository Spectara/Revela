using System.CommandLine;

using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Feed;

/// <summary>
/// Command to list all NuGet feeds.
/// </summary>
public sealed partial class ListCommand(
    ILogger<ListCommand> logger,
    INuGetSourceManager nugetSourceManager)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List all NuGet feeds");

        command.SetAction(async (_, cancellationToken) =>
            await ShowFeedsAsync(cancellationToken));

        return command;
    }

    /// <summary>
    /// Shows the list of configured NuGet feeds.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ShowFeedsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            LogListingFeeds(logger);

            var sources = await nugetSourceManager.GetAllSourcesWithLocationAsync(cancellationToken);

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Name")
                .AddColumn("URL")
                .AddColumn("Type");

            foreach (var (source, location) in sources)
            {
                var typeStyle = location switch
                {
                    "bundled" => "[magenta]bundled[/]",
                    "built-in" => "[blue]built-in[/]",
                    "local" => "[green]local[/]",
                    _ => "[dim]remote[/]"
                };

                table.AddRow(
                    $"[cyan]{source.Name}[/]",
                    $"[dim]{source.Url}[/]",
                    typeStyle);
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Total: [cyan]{sources.Count}[/] feed(s)");

            // Show bundled packages info if exists
            var bundledDir = PluginManager.BundledPackagesDirectory;
            if (Directory.Exists(bundledDir))
            {
                var nupkgCount = Directory.GetFiles(bundledDir, "*.nupkg").Length;
                if (nupkgCount > 0)
                {
                    AnsiConsole.MarkupLine($"Bundled: [cyan]{nupkgCount}[/] package(s) in [dim]{bundledDir}[/]");
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Config: [dim]{GlobalConfigManager.ConfigFilePath}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            LogListFailed(logger, ex);
            ErrorPanels.ShowException(ex, "Failed to list feeds.");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing NuGet feeds")]
    private static partial void LogListingFeeds(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list feeds")]
    private static partial void LogListFailed(ILogger logger, Exception exception);
}

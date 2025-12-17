using System.CommandLine;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins.Source;

/// <summary>
/// Command to list all NuGet sources
/// </summary>
public sealed partial class PluginSourceListCommand(
    ILogger<PluginSourceListCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var command = new Command("list", "List all NuGet package sources");

        command.SetAction(async (_, cancellationToken) =>
        {
            return await ExecuteAsync(cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogListingSources(logger);
            var sources = await NuGetSourceManager.GetAllSourcesAsync(cancellationToken);

            if (sources.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No sources configured[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Name")
                .AddColumn("URL")
                .AddColumn("Status");

            foreach (var source in sources)
            {
                var name = source.Name == "nuget.org" ? $"[cyan]{source.Name}[/] [dim](built-in)[/]" : $"[cyan]{source.Name}[/]";
                var url = $"[dim]{source.Url}[/]";
                var status = source.Enabled ? "[green]enabled[/]" : "[red]disabled[/]";
                table.AddRow(name, url, status);
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Total: [cyan]{sources.Count}[/] source(s)");

            return 0;
        }
        catch (Exception ex)
        {
            LogListFailed(logger, ex);
            AnsiConsole.MarkupLine($"[red]ERROR[/] Failed to list sources: {ex.Message}");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing NuGet sources")]
    private static partial void LogListingSources(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to list sources")]
    private static partial void LogListFailed(ILogger logger, Exception exception);
}

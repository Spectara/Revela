using System.CommandLine;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Plugins.Source;

/// <summary>
/// Command to add a new NuGet source
/// </summary>
public sealed partial class PluginSourceAddCommand(
    ILogger<PluginSourceAddCommand> logger)
{
    /// <summary>
    /// Creates the CLI command
    /// </summary>
    public Command Create()
    {
        var nameOption = new Option<string>("--name", "-n")
        {
            Description = "Name for the source (e.g., 'github', 'my-feed')",
            Required = true
        };

        var urlOption = new Option<string>("--url", "-u")
        {
            Description = "NuGet v3 API URL",
            Required = true
        };

        var command = new Command("add", "Add a new NuGet package source");
        command.Options.Add(nameOption);
        command.Options.Add(urlOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption)!;
            var url = parseResult.GetValue(urlOption)!;

            return await ExecuteAsync(name, url, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(string name, string url, CancellationToken cancellationToken)
    {
        try
        {
            // Validate URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                AnsiConsole.MarkupLine($"[red]ERROR[/] Invalid URL: {url}");
                return 1;
            }

            LogAddingSource(logger, name, url);
            await NuGetSourceManager.AddSourceAsync(name, url, cancellationToken);

            AnsiConsole.MarkupLine($"[green]✓[/] Added source [cyan]{name}[/]");
            AnsiConsole.MarkupLine($"  URL: [dim]{url}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"Use [blue]revela plugins install <package> --source {name}[/] to install from this source.");

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            LogAddFailed(logger, ex, name);
            AnsiConsole.MarkupLine($"[red]ERROR[/] {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            LogAddFailed(logger, ex, name);
            AnsiConsole.MarkupLine($"[red]ERROR[/] Failed to add source: {ex.Message}");
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Adding NuGet source: {name} ({url})")]
    private static partial void LogAddingSource(ILogger logger, string name, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to add source: {name}")]
    private static partial void LogAddFailed(ILogger logger, Exception exception, string name);
}

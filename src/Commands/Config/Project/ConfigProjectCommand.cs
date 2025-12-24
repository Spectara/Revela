using System.CommandLine;
using System.Text.Json.Nodes;
using Spectara.Revela.Commands.Config.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Project;

/// <summary>
/// Command to configure project settings.
/// </summary>
/// <remarks>
/// Creates project.json if it doesn't exist, otherwise updates.
/// Configures name and url in project.json.
/// Theme is configured separately via 'config theme'.
/// Uses ConfigService for both create and update (no template needed).
/// </remarks>
public sealed partial class ConfigProjectCommand(
    ILogger<ConfigProjectCommand> logger,
    IConfigService configService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("project", "Configure project settings (name, url)");

        var nameOption = new Option<string?>("--name", "-n")
        {
            Description = "Project name"
        };
        var urlOption = new Option<string?>("--url", "-u")
        {
            Description = "Base URL for the generated site"
        };

        command.Options.Add(nameOption);
        command.Options.Add(urlOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var name = parseResult.GetValue(nameOption);
            var url = parseResult.GetValue(urlOption);

            return await ExecuteAsync(name, url, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(
        string? nameArg,
        string? urlArg,
        CancellationToken cancellationToken)
    {
        var isFirstTime = !configService.IsProjectInitialized();

        // Get current config (or empty for new project)
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);

        // Determine if interactive mode (no arguments provided)
        var isInteractive = nameArg is null && urlArg is null;

        string name, url;

        if (isInteractive)
        {
            var action = isFirstTime ? "Initialize" : "Configure";
            AnsiConsole.MarkupLine($"[cyan]{action} project settings[/]\n");

            // Default name from current directory
            var defaultName = current?["name"]?.GetValue<string>()
                ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            name = AnsiConsole.Prompt(
                new TextPrompt<string>("Project name:")
                    .DefaultValue(defaultName));

            url = AnsiConsole.Prompt(
                new TextPrompt<string>("Base URL:")
                    .DefaultValue(current?["url"]?.GetValue<string>() ?? string.Empty)
                    .AllowEmpty());
        }
        else
        {
            // Use provided arguments, fall back to current values or defaults
            name = nameArg ?? current?["name"]?.GetValue<string>()
                ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            url = urlArg ?? current?["url"]?.GetValue<string>() ?? "";
        }

        // Create or update project configuration using ConfigService
        var update = new JsonObject
        {
            ["name"] = name,
            ["url"] = url
        };

        // For new projects, log initialization
        if (isFirstTime)
        {
            LogInitializingProject(name);
        }

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        if (isFirstTime)
        {
            // Create directories
            Directory.CreateDirectory("source");
            Directory.CreateDirectory("output");

            AnsiConsole.MarkupLine($"\n[green]✓[/] Project '{name}' initialized");
            AnsiConsole.MarkupLine("[dim]Created: project.json, source/, output/[/]");
            AnsiConsole.MarkupLine("\n[yellow]Next:[/] Run [cyan]revela config theme[/] to select a theme");
        }
        else
        {
            LogProjectConfigured(name);
            AnsiConsole.MarkupLine("[green]✓[/] Project settings updated");
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}'")]
    private partial void LogInitializingProject(string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project configured: name='{Name}'")]
    private partial void LogProjectConfigured(string name);
}

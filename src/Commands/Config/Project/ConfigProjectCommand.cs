using System.CommandLine;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;

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
    IOptions<ProjectEnvironment> projectEnvironment,
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

    /// <summary>
    /// Executes the project configuration.
    /// </summary>
    /// <param name="nameArg">Optional project name.</param>
    /// <param name="urlArg">Optional base URL (string for CLI input, may be empty).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
#pragma warning disable CA1054 // URI-like parameters should not be strings - CLI input may be empty or invalid
    public async Task<int> ExecuteAsync(
        string? nameArg,
        string? urlArg,
        CancellationToken cancellationToken)
#pragma warning restore CA1054
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
            var defaultName = current?["project"]?["name"]?.GetValue<string>()
                ?? projectEnvironment.Value.FolderName;
            name = AnsiConsole.Prompt(
                new TextPrompt<string>("Project name:")
                    .DefaultValue(defaultName));

            AnsiConsole.MarkupLine("[dim]Your website address, e.g. https://photos.example.com (leave empty if unknown)[/]");
            url = AnsiConsole.Prompt(
                new TextPrompt<string>("Base URL:")
                    .DefaultValue(current?["project"]?["url"]?.GetValue<string>() ?? string.Empty)
                    .AllowEmpty());
        }
        else
        {
            // Use provided arguments, fall back to current values or defaults
            name = nameArg ?? current?["project"]?["name"]?.GetValue<string>()
                ?? projectEnvironment.Value.FolderName;
            url = urlArg ?? current?["project"]?["url"]?.GetValue<string>() ?? "";
        }

        // Create or update project configuration using ConfigService
        var update = new JsonObject
        {
            ["project"] = new JsonObject
            {
                ["name"] = name,
                ["url"] = url
            }
        };

        // For new projects, log initialization
        if (isFirstTime)
        {
            LogInitializingProject(name);
        }

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        if (isFirstTime)
        {
            AnsiConsole.MarkupLine($"\n{OutputMarkers.Success} Project '{name}' initialized");
            AnsiConsole.MarkupLine("[dim]Created: project.json[/]");

            AnsiConsole.MarkupLine("\n[yellow]Next:[/] Run [cyan]revela config theme[/] to select a theme");
        }
        else
        {
            LogProjectConfigured(name);
            AnsiConsole.MarkupLine($"{OutputMarkers.Success} Project settings updated");
        }

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}'")]
    private partial void LogInitializingProject(string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Project configured: name='{Name}'")]
    private partial void LogProjectConfigured(string name);
}

using System.CommandLine;
using System.Text.Json;

using Spectara.Revela.Plugin.Source.OneDrive.Commands.Logging;
using Spectara.Revela.Sdk;

using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to initialize OneDrive source configuration
/// </summary>
/// <remarks>
/// Uses Dependency Injection with Primary Constructor (C# 12).
/// Logger is injected for potential error logging.
/// </remarks>
public sealed class OneDriveInitCommand(ILogger<OneDriveInitCommand> logger)
{
    private const string PluginsFolderName = "plugins";
    private const string PluginPackageId = "Spectara.Revela.Plugin.Source.OneDrive";
    private const string DefaultConfigFileName = $"{PluginPackageId}.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Command Create()
    {
        var command = new Command("onedrive", "Initialize OneDrive source configuration");

        // Option to specify share URL non-interactively
        var shareUrlOption = new Option<string?>("--share-url", "-u")
        {
            Description = "OneDrive shared folder URL (skip interactive prompt)"
        };

        // Option to specify custom config filename
        var nameOption = new Option<string?>("--name", "-n")
        {
            Description = $"Config filename (default: {DefaultConfigFileName})"
        };

        command.Options.Add(shareUrlOption);
        command.Options.Add(nameOption);

        command.SetAction(parseResult =>
        {
            var shareUrl = parseResult.GetValue(shareUrlOption);
            var configName = parseResult.GetValue(nameOption);
            Execute(shareUrl, configName);
            return 0;
        });

        return command;
    }

    private void Execute(string? shareUrl, string? configName)
    {
        try
        {
            // Determine config filename (default: Package-ID.json)
            var fileName = string.IsNullOrWhiteSpace(configName)
                ? DefaultConfigFileName
                : configName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? configName
                    : $"{configName}.json";

            // Ensure plugins folder exists
            Directory.CreateDirectory(PluginsFolderName);
            var configPath = Path.Combine(PluginsFolderName, fileName);

            // Check if already initialized
            if (File.Exists(configPath))
            {
                if (!AnsiConsole.Confirm($"[yellow]{configPath} already exists. Overwrite?[/]"))
                {
                    AnsiConsole.MarkupLine("[dim]Configuration unchanged.[/]");
                    return;
                }
            }

            AnsiConsole.MarkupLine("[blue]Initializing OneDrive source...[/]\n");

            // Get share URL (interactive or from parameter)
            shareUrl ??= AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]OneDrive share URL:[/]")
                    .PromptStyle("green")
                    .ValidationErrorMessage("[red]Please enter a valid OneDrive share URL[/]")
                    .Validate(url =>
                    {
                        return string.IsNullOrWhiteSpace(url)
                            ? ValidationResult.Error("[red]URL cannot be empty[/]")
                            : !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                            ? ValidationResult.Error("[red]URL must start with https://[/]")
                            : !url.Contains("1drv.ms", StringComparison.OrdinalIgnoreCase) &&
                            !url.Contains("onedrive.live.com", StringComparison.OrdinalIgnoreCase)
                            ? ValidationResult.Error("[red]Must be a valid OneDrive share URL[/]")
                            : ValidationResult.Success();
                    })
            );

            // Create configuration with plugin package ID as root key (no Plugins wrapper)
            var configObject = new Dictionary<string, object?>
            {
                [PluginPackageId] = new Dictionary<string, object?>
                {
                    ["ShareUrl"] = shareUrl
                    // IncludePatterns and ExcludePatterns not set - will use smart defaults
                }
            };

            // Save to plugins folder
            var json = JsonSerializer.Serialize(configObject, JsonOptions);
            File.WriteAllText(configPath, json);

            // Create source directory
            Directory.CreateDirectory("source");

            // Success message
            var panel = new Panel(
                $"[green]OneDrive source configured![/]\n\n" +
                $"[bold]Configuration:[/] [cyan]{configPath}[/]\n" +
                $"[bold]Share URL:[/] [dim]{shareUrl}[/]\n" +
                $"[bold]Download to:[/] [cyan]./source/[/]\n\n" +
                $"[dim]Downloads all images (via MIME type) and markdown files by default.[/]\n" +
                $"[dim]To customize, edit {configPath} and add includePatterns/excludePatterns.[/]\n\n" +
                $"[bold]Next steps:[/]\n" +
                $"1. Run [cyan]revela source onedrive download[/] to fetch files\n" +
                $"2. Run [cyan]revela generate[/] to build your site"
            )
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            ErrorPanels.ShowException(ex);
            logger.InitFailed(ex);
        }
    }
}


using System.CommandLine;
using System.Text.Json;
using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectre.Console;

namespace Spectara.Revela.Plugin.Source.OneDrive.Commands;

/// <summary>
/// Command to initialize OneDrive source configuration
/// </summary>
public static class OneDriveInitCommand
{
    private const string ConfigFileName = "onedrive.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Command Create()
    {
        var command = new Command("init", "Initialize OneDrive source configuration");

        // Option to specify share URL non-interactively
        var shareUrlOption = new Option<string?>("--share-url", "-u")
        {
            Description = "OneDrive shared folder URL (skip interactive prompt)"
        };

        command.Options.Add(shareUrlOption);

        command.SetAction(parseResult =>
        {
            var shareUrl = parseResult.GetValue(shareUrlOption);
            Execute(shareUrl);
            return 0;
        });

        return command;
    }

    private static void Execute(string? shareUrl)
    {
        try
        {
            // Check if already initialized
            if (File.Exists(ConfigFileName))
            {
                if (!AnsiConsole.Confirm($"[yellow]{ConfigFileName} already exists. Overwrite?[/]"))
                {
                    AnsiConsole.MarkupLine("[dim]Configuration unchanged.[/]");
                    return;
                }
            }

            AnsiConsole.MarkupLine("[blue]🔧 Initializing OneDrive source...[/]\n");

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

            // Create minimal configuration (patterns use smart defaults)
            var config = new OneDriveConfig
            {
                ShareUrl = shareUrl
                // IncludePatterns and ExcludePatterns not set - will use smart defaults
            };

            // Save to JSON
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFileName, json);

            // Create source directory
            Directory.CreateDirectory("source");

            // Success message
            var panel = new Panel(
                $"[green]✨ OneDrive source configured![/]\n\n" +
                $"[bold]Configuration:[/] [cyan]{ConfigFileName}[/]\n" +
                $"[bold]Share URL:[/] [dim]{shareUrl}[/]\n" +
                $"[bold]Download to:[/] [cyan]./source/[/]\n\n" +
                $"[dim]Downloads all images (via MIME type) and markdown files by default.[/]\n" +
                $"[dim]To customize, edit {ConfigFileName} and add includePatterns/excludePatterns.[/]\n\n" +
                $"[bold]Next steps:[/]\n" +
                $"1. Run [cyan]revela source onedrive download[/] to fetch files\n" +
                $"2. Run [cyan]revela generate[/] to build your site"
            )
            {
                Header = new PanelHeader("[bold green]Success[/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        }
    }
}

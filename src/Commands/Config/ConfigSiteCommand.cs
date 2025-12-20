using System.CommandLine;
using Spectara.Revela.Commands.Config.Models;
using Spectara.Revela.Commands.Config.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config;

/// <summary>
/// Command to configure site metadata.
/// </summary>
/// <remarks>
/// Configures title, author, description, copyright in site.json.
/// </remarks>
public sealed partial class ConfigSiteCommand(
    ILogger<ConfigSiteCommand> logger,
    IConfigService configService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("site", "Configure site metadata");

        var titleOption = new Option<string?>("--title")
        {
            Description = "Site title"
        };
        var authorOption = new Option<string?>("--author")
        {
            Description = "Author name"
        };
        var descriptionOption = new Option<string?>("--description")
        {
            Description = "Site description"
        };
        var copyrightOption = new Option<string?>("--copyright")
        {
            Description = "Copyright notice"
        };

        command.Options.Add(titleOption);
        command.Options.Add(authorOption);
        command.Options.Add(descriptionOption);
        command.Options.Add(copyrightOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var title = parseResult.GetValue(titleOption);
            var author = parseResult.GetValue(authorOption);
            var description = parseResult.GetValue(descriptionOption);
            var copyright = parseResult.GetValue(copyrightOption);

            return await ExecuteAsync(title, author, description, copyright, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the site configuration.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? titleArg,
        string? authorArg,
        string? descriptionArg,
        string? copyrightArg,
        CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Not a Revela project. Run [cyan]revela init project[/] first.");
            return 1;
        }

        // Read current values
        var current = await configService.ReadSiteConfigAsync(cancellationToken).ConfigureAwait(false)
            ?? new SiteConfigDto();

        // Determine if interactive mode (no arguments provided)
        var isInteractive = titleArg is null && authorArg is null && descriptionArg is null && copyrightArg is null;

        string title, author, description, copyright;

        if (isInteractive)
        {
            AnsiConsole.MarkupLine("[cyan]Configure site metadata[/]\n");

            title = AnsiConsole.Prompt(
                new TextPrompt<string>("Title:")
                    .DefaultValue(current.Title ?? "")
                    .AllowEmpty());

            author = AnsiConsole.Prompt(
                new TextPrompt<string>("Author:")
                    .DefaultValue(current.Author ?? "")
                    .AllowEmpty());

            description = AnsiConsole.Prompt(
                new TextPrompt<string>("Description:")
                    .DefaultValue(current.Description ?? "")
                    .AllowEmpty());

            // Generate default copyright if not set
            var defaultCopyright = string.IsNullOrEmpty(current.Copyright)
                ? $"© {DateTime.Now.Year} {author}. All rights reserved."
                : current.Copyright;

            copyright = AnsiConsole.Prompt(
                new TextPrompt<string>("Copyright:")
                    .DefaultValue(defaultCopyright)
                    .AllowEmpty());
        }
        else
        {
            // Use provided arguments, fall back to current values
            title = titleArg ?? current.Title ?? "";
            author = authorArg ?? current.Author ?? "";
            description = descriptionArg ?? current.Description ?? "";
            copyright = copyrightArg ?? current.Copyright ?? "";
        }

        // Update config using DTO
        var update = new SiteConfigDto
        {
            Title = title,
            Author = author,
            Description = description,
            Copyright = copyright
        };

        await configService.UpdateSiteConfigAsync(update, cancellationToken).ConfigureAwait(false);

        LogSiteConfigured(title, author);
        AnsiConsole.MarkupLine("[green]✓[/] Site metadata updated");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Site configured: title='{Title}', author='{Author}'")]
    private partial void LogSiteConfigured(string title, string author);
}

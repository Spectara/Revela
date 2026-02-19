using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;

using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Sorting;

/// <summary>
/// Command to configure sorting settings for galleries and images.
/// </summary>
/// <remarks>
/// <para>
/// Configures how galleries and images are sorted in the generated site.
/// Settings are stored in project.json under generate.sorting.
/// </para>
/// <para>
/// Images can be sorted by any property path including EXIF data:
/// </para>
/// <list type="bullet">
///   <item><c>filename</c> - File name</item>
///   <item><c>dateTaken</c> - EXIF date taken</item>
///   <item><c>exif.focalLength</c> - Focal length</item>
///   <item><c>exif.iso</c> - ISO sensitivity</item>
///   <item><c>exif.raw.Rating</c> - Star rating (1-5)</item>
/// </list>
/// </remarks>
internal sealed class ConfigSortingCommand(IConfigService configService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("sorting", "Configure sorting settings");

        var galleriesOption = new Option<string?>("--galleries", "-g")
        {
            Description = "Gallery sort direction: asc or desc"
        };
        var fieldOption = new Option<string?>("--field", "-f")
        {
            Description = "Image sort field (e.g., dateTaken, filename, exif.iso, exif.raw.Rating)"
        };
        var directionOption = new Option<string?>("--direction", "-d")
        {
            Description = "Image sort direction: asc or desc"
        };
        var fallbackOption = new Option<string?>("--fallback")
        {
            Description = "Fallback field when primary is null (default: filename)"
        };

        command.Options.Add(galleriesOption);
        command.Options.Add(fieldOption);
        command.Options.Add(directionOption);
        command.Options.Add(fallbackOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var galleries = parseResult.GetValue(galleriesOption);
            var field = parseResult.GetValue(fieldOption);
            var direction = parseResult.GetValue(directionOption);
            var fallback = parseResult.GetValue(fallbackOption);

            return await ExecuteAsync(galleries, field, direction, fallback, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the sorting configuration.
    /// </summary>
    public async Task<int> ExecuteAsync(
        string? galleriesArg,
        string? fieldArg,
        string? directionArg,
        string? fallbackArg,
        CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        // Non-interactive mode if any argument provided
        if (!string.IsNullOrEmpty(galleriesArg) ||
            !string.IsNullOrEmpty(fieldArg) ||
            !string.IsNullOrEmpty(directionArg) ||
            !string.IsNullOrEmpty(fallbackArg))
        {
            return await ExecuteNonInteractiveAsync(galleriesArg, fieldArg, directionArg, fallbackArg, cancellationToken)
                .ConfigureAwait(false);
        }

        // Interactive mode
        return await ExecuteInteractiveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes non-interactive configuration via CLI arguments.
    /// </summary>
    private async Task<int> ExecuteNonInteractiveAsync(
        string? galleriesArg,
        string? fieldArg,
        string? directionArg,
        string? fallbackArg,
        CancellationToken cancellationToken)
    {
        var updates = new JsonObject();
        var sorting = new JsonObject();
        var hasChanges = false;

        // Update galleries direction
        if (!string.IsNullOrEmpty(galleriesArg))
        {
            var direction = ParseDirection(galleriesArg);
            if (direction is null)
            {
                AnsiConsole.MarkupLine($"{OutputMarkers.Error} Invalid gallery direction: {galleriesArg}. Use 'asc' or 'desc'.");
                return 1;
            }

            sorting["galleries"] = direction;
            hasChanges = true;
        }

        // Update images settings
        if (!string.IsNullOrEmpty(fieldArg) || !string.IsNullOrEmpty(directionArg) || !string.IsNullOrEmpty(fallbackArg))
        {
            var images = new JsonObject();

            if (!string.IsNullOrEmpty(fieldArg))
            {
                images["field"] = fieldArg;
            }

            if (!string.IsNullOrEmpty(directionArg))
            {
                var direction = ParseDirection(directionArg);
                if (direction is null)
                {
                    AnsiConsole.MarkupLine($"{OutputMarkers.Error} Invalid image direction: {directionArg}. Use 'asc' or 'desc'.");
                    return 1;
                }

                images["direction"] = direction;
            }

            if (!string.IsNullOrEmpty(fallbackArg))
            {
                images["fallback"] = fallbackArg;
            }

            sorting["images"] = images;
            hasChanges = true;
        }

        if (!hasChanges)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} No changes specified");
            return 0;
        }

        updates["generate"] = new JsonObject { ["sorting"] = sorting };
        await configService.UpdateProjectConfigAsync(updates, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Sorting configuration updated");

        return 0;
    }

    /// <summary>
    /// Executes interactive configuration wizard.
    /// </summary>
    private async Task<int> ExecuteInteractiveAsync(CancellationToken cancellationToken)
    {
        var projectConfig = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);

        // Read current values
        var generate = projectConfig?["generate"]?.AsObject();
        var sorting = generate?["sorting"]?.AsObject();
        var currentGalleries = sorting?["galleries"]?.GetValue<string>() ?? "asc";
        var imagesObj = sorting?["images"]?.AsObject();
        var currentField = imagesObj?["field"]?.GetValue<string>() ?? "dateTaken";
        var currentDirection = imagesObj?["direction"]?.GetValue<string>() ?? "desc";
        var currentFallback = imagesObj?["fallback"]?.GetValue<string>() ?? "filename";

        AnsiConsole.Write(new Rule("[cyan]Sorting Configuration[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Show current config
        ShowCurrentConfig(currentGalleries, currentField, currentDirection, currentFallback);

        // Gallery sort direction
        var galleriesDirection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Gallery sort direction[/] [dim](affects navigation order)[/]")
                .AddChoices("asc", "desc")
                .UseConverter(d => d == "asc"
                    ? "Ascending (A → Z, 1 → 9)"
                    : "Descending (Z → A, 9 → 1)"));

        // Image sort field - using array with named tuples
        var fieldChoices = new (string Value, string Display)[]
        {
            ("dateTaken", "Date Taken [dim](EXIF capture date)[/]"),
            ("filename", "Filename [dim](alphabetical)[/]"),
            ("exif.focalLength", "Focal Length [dim](wide to tele)[/]"),
            ("exif.iso", "ISO [dim](sensitivity)[/]"),
            ("exif.fNumber", "Aperture [dim](f-number)[/]"),
            ("exif.exposureTime", "Shutter Speed [dim](exposure time)[/]"),
            ("exif.raw.Rating", "Rating [dim](star rating 1-5)[/]"),
            ("custom", "[dim]Custom field...[/]")
        };

        var (imageField, _) = AnsiConsole.Prompt(
            new SelectionPrompt<(string Value, string Display)>()
                .Title("[cyan]Sort images by[/]")
                .AddChoices(fieldChoices)
                .UseConverter(c => c.Display));

        if (imageField == "custom")
        {
            imageField = AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Enter field path[/] [dim](e.g., exif.raw.Copyright)[/]:")
                    .DefaultValue(currentField));
        }

        // Image sort direction
        var imageDirection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Image sort direction[/]")
                .AddChoices("desc", "asc")
                .UseConverter(d => d == "asc"
                    ? "Ascending (oldest/smallest first)"
                    : "Descending (newest/largest first)"));

        // Fallback field
        var useFallback = await AnsiConsole.ConfirmAsync(
            $"[cyan]Configure fallback field?[/] [dim](used when {imageField} is empty)[/]",
            defaultValue: false,
            cancellationToken).ConfigureAwait(false);

        var fallbackField = currentFallback;
        if (useFallback)
        {
            fallbackField = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Fallback sort field[/]")
                    .AddChoices("filename", "dateTaken"));
        }

        // Build update object
        var newSorting = new JsonObject
        {
            ["galleries"] = galleriesDirection,
            ["images"] = new JsonObject
            {
                ["field"] = imageField,
                ["direction"] = imageDirection,
                ["fallback"] = fallbackField
            }
        };

        // Preview
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[cyan]Preview[/]").RuleStyle("grey"));

        var previewJson = newSorting.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var panel = new Panel(previewJson)
            .WithHeader("[bold]generate.sorting[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        var confirmed = await AnsiConsole.ConfirmAsync("[cyan]Save this configuration?[/]", defaultValue: true, cancellationToken).ConfigureAwait(false);
        if (!confirmed)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Configuration not saved");
            return 0;
        }

        var updates = new JsonObject
        {
            ["generate"] = new JsonObject { ["sorting"] = newSorting }
        };

        await configService.UpdateProjectConfigAsync(updates, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Sorting configuration saved");

        return 0;
    }

    /// <summary>
    /// Shows the current sorting configuration.
    /// </summary>
    private static void ShowCurrentConfig(string galleries, string field, string direction, string fallback)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Current Value");

        table.AddRow("[cyan]Galleries[/]", galleries == "desc" ? "Descending (Z → A)" : "Ascending (A → Z)");
        table.AddRow("[cyan]Image Field[/]", field);
        table.AddRow("[cyan]Image Direction[/]", direction == "desc" ? "Descending" : "Ascending");
        table.AddRow("[cyan]Fallback[/]", fallback);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Parses a direction string to normalized form.
    /// </summary>
    private static string? ParseDirection(string input)
    {
        return input.ToUpperInvariant() switch
        {
            "ASC" or "ASCENDING" => "asc",
            "DESC" or "DESCENDING" => "desc",
            _ => null
        };
    }
}

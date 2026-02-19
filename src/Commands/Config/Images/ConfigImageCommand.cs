using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Images;

/// <summary>
/// Command to configure image processing settings.
/// </summary>
/// <remarks>
/// <para>
/// Configures output formats and quality settings in project.json.
/// Sizes are defined by the theme (via images.json) and cannot be changed here.
/// </para>
/// <para>
/// Format configuration uses flat properties:
/// <code>
/// "generate": {
///   "images": {
///     "webp": 85,
///     "jpg": 90,
///     "avif": 0   // 0 = disabled
///   }
/// }
/// </code>
/// </para>
/// </remarks>
internal sealed partial class ConfigImageCommand(
    ILogger<ConfigImageCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IThemeResolver themeResolver,
    IConfigService configService)
{
    private static readonly Dictionary<string, int> DefaultFormatQualities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = 90,
        ["webp"] = 85,
        ["avif"] = 80
    };
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("image", "Configure image processing settings");

        var formatsOption = new Option<string?>("--formats", "-f")
        {
            Description = "Output formats with optional quality (e.g., avif:80,webp:85,jpg or just avif,webp,jpg). Set quality to 0 to disable."
        };

        command.Options.Add(formatsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var formats = parseResult.GetValue(formatsOption);

            return await ExecuteAsync(formats, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the image configuration.
    /// </summary>
    /// <param name="formatsArg">Formats with optional quality (format:quality or just format).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(
        string? formatsArg,
        CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        // Theme must be selected - only theme knows which image sizes make sense
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrWhiteSpace(themeName))
        {
            ErrorPanels.ShowError(
                "No Theme Selected",
                "[yellow]Image configuration depends on the selected theme.[/]\n" +
                "[dim]Only the theme knows which image sizes are optimal for its layout.[/]\n\n" +
                "[bold]Run first:[/] [cyan]revela config theme[/]");
            return 1;
        }

        // Load theme defaults for format quality defaults
        var themeDefaults = LoadThemeDefaults(themeName);

        // Non-interactive mode if argument provided
        if (!string.IsNullOrEmpty(formatsArg))
        {
            return await ExecuteNonInteractiveAsync(formatsArg, themeDefaults, cancellationToken)
                .ConfigureAwait(false);
        }

        // Interactive mode
        return await ExecuteInteractiveAsync(themeDefaults, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads image defaults from theme's images.json (for quality defaults).
    /// </summary>
    private ImageDefaults? LoadThemeDefaults(string themeName)
    {
        var projectPath = projectEnvironment.Value.Path;
        var theme = themeResolver.Resolve(themeName, projectPath);

        if (theme is null)
        {
            return null;
        }

        using var stream = theme.GetImagesTemplate();
        if (stream is null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Deserialize<JsonObject>(stream);
            if (json is null)
            {
                return null;
            }

            var formats = new Dictionary<string, int>();
            if (json["formats"]?.AsObject() is { } formatsObj)
            {
                foreach (var (key, value) in formatsObj)
                {
                    if (value?.GetValue<int>() is { } quality)
                    {
                        formats[key] = quality;
                    }
                }
            }

            var sizes = json["sizes"]?.AsArray()
                ?.Where(s => s is not null)
                .Select(s => s!.GetValue<int>())
                .ToArray() ?? [];

            if (formats.Count > 0 || sizes.Length > 0)
            {
                return new ImageDefaults(formats, sizes);
            }
        }
        catch (JsonException)
        {
            // Invalid JSON - ignore and return null
        }

        return null;
    }

    private async Task<int> ExecuteNonInteractiveAsync(
        string formatsArg,
        ImageDefaults? themeDefaults,
        CancellationToken cancellationToken)
    {
        // Read current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentFormats = GetCurrentFormats(current);

        // Determine defaults: current > theme > fallback
        var defaultFormats = currentFormats
            ?? themeDefaults?.Formats
            ?? DefaultFormatQualities;

        // Parse formats (format or format:quality or format:0 to disable)
        var validFormats = new HashSet<string>(["avif", "webp", "jpg"], StringComparer.OrdinalIgnoreCase);
        var formats = new Dictionary<string, int>();

        var entries = formatsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsedCount = 0;

        foreach (var entry in entries)
        {
            var parts = entry.Split(':', StringSplitOptions.TrimEntries);
            var formatName = parts[0];

            if (!validFormats.Contains(formatName))
            {
                continue;
            }

            // Normalize to lowercase for JSON keys
#pragma warning disable CA1308
            var normalizedFormat = formatName.ToLowerInvariant();
#pragma warning restore CA1308

            // Check for quality (format:quality or format:0 to disable)
            if (parts.Length == 2 && int.TryParse(parts[1], out var quality) && quality is >= 0 and <= 100)
            {
                formats[normalizedFormat] = quality;
            }
            else
            {
                // No quality specified, use default from theme or fallback
                formats[normalizedFormat] = defaultFormats.GetValueOrDefault(normalizedFormat, DefaultFormatQualities.GetValueOrDefault(normalizedFormat, 90));
            }

            parsedCount++;
        }

        if (parsedCount == 0)
        {
            ErrorPanels.ShowValidationError(
                "No valid formats specified.",
                "  avif, webp, jpg (optionally with :quality, e.g. webp:85, or :0 to disable)");
            return 1;
        }

        // Build and save config using flat format properties
        var imagesObj = new JsonObject();
        foreach (var (format, quality) in formats)
        {
            imagesObj[format] = quality;
        }

        var update = new JsonObject
        {
            ["generate"] = new JsonObject
            {
                ["images"] = imagesObj
            }
        };

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        var formatList = string.Join(", ", formats.Select(f => f.Value > 0 ? $"{f.Key}:{f.Value}" : $"{f.Key}:disabled"));
        LogImageConfigured(formatList);
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Image settings updated");

        return 0;
    }

    private async Task<int> ExecuteInteractiveAsync(ImageDefaults? themeDefaults, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[cyan]Configure image processing[/]\n");

        // Read current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentFormats = GetCurrentFormats(current);

        // Determine defaults: current > theme
        var defaultFormats = currentFormats ?? themeDefaults?.Formats ?? DefaultFormatQualities;

        // Format selection
        var formatChoices = new[] { "jpg", "webp", "avif" };
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select output formats (deselect to disable):")
            .PageSize(5)
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
            .AddChoices(formatChoices);

        // Pre-select from defaults (if available)
        if (defaultFormats is not null)
        {
            foreach (var format in defaultFormats.Keys)
            {
                prompt.Select(format);
            }
        }

        var selectedFormats = AnsiConsole.Prompt(prompt);

        if (selectedFormats.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] At least one format required. Using JPG.");
            selectedFormats = ["jpg"];
        }

        // Quality per format - use current config value or recommended defaults
        var formats = new Dictionary<string, int>();
        foreach (var format in selectedFormats)
        {
            // Priority: current config (if > 0) > recommended defaults
            var configuredQuality = currentFormats?.GetValueOrDefault(format) ?? 0;
            var defaultQuality = configuredQuality > 0
                ? configuredQuality
                : DefaultFormatQualities.GetValueOrDefault(format, 85);

            var quality = AnsiConsole.Prompt(
                new TextPrompt<int>($"Quality for [green]{format.ToUpperInvariant()}[/] (1-100):")
                    .DefaultValue(defaultQuality)
                    .Validate(q => q is >= 1 and <= 100
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Quality must be between 1 and 100[/]")));

            formats[format] = quality;
        }

        // Build update with only selected formats (no C# defaults to override)
        // Deselected formats are removed via null value in DeepMerge
        var imagesObj = new JsonObject();
        foreach (var knownFormat in formatChoices)
        {
            if (formats.TryGetValue(knownFormat, out var quality))
            {
                imagesObj[knownFormat] = quality;
            }
            else
            {
                // Set to null to remove from config (DeepMerge handles this)
                imagesObj[knownFormat] = null;
            }
        }

        var update = new JsonObject
        {
            ["generate"] = new JsonObject
            {
                ["images"] = imagesObj
            }
        };

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        var formatList = string.Join(", ", selectedFormats);
        LogImageConfigured(formatList);
        AnsiConsole.MarkupLine($"\n{OutputMarkers.Success} Image settings updated");

        // Show summary
        var summary = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Format")
            .AddColumn("Quality");

        foreach (var (format, quality) in formats)
        {
            summary.AddRow(format.ToUpperInvariant(), quality.ToString(CultureInfo.InvariantCulture));
        }

        AnsiConsole.Write(summary);

        // Show info about sizes
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Note: Image sizes are defined by the theme.[/]");
        AnsiConsole.MarkupLine("[dim]To customize sizes, create theme/images.json in your project.[/]");

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Image configured: formats=[{Formats}]")]
    private partial void LogImageConfigured(string formats);

    /// <summary>
    /// Extracts formats from flat properties in JsonObject config.
    /// </summary>
    /// <remarks>
    /// New format: generate.images.webp: 85, generate.images.jpg: 90
    /// Legacy format: generate.images.formats.webp: 85 (still supported for reading)
    /// </remarks>
    private static Dictionary<string, int>? GetCurrentFormats(JsonObject? config)
    {
        var imagesNode = config?["generate"]?["images"];
        if (imagesNode is null)
        {
            return null;
        }

        var result = new Dictionary<string, int>();

        // Try new flat format first (webp, jpg, avif as direct properties)
        var validFormats = new HashSet<string>(["avif", "webp", "jpg"], StringComparer.OrdinalIgnoreCase);

        if (imagesNode is JsonObject imagesObj)
        {
            foreach (var (key, value) in imagesObj)
            {
                if (validFormats.Contains(key) && value?.GetValue<int>() is { } quality)
                {
                    result[key] = quality;
                }
            }
        }

        // If no flat formats found, try legacy nested format
        if (result.Count == 0)
        {
            var legacyFormats = imagesNode["formats"]?.AsObject();
            if (legacyFormats is not null)
            {
                foreach (var (key, value) in legacyFormats)
                {
                    if (value?.GetValue<int>() is { } quality)
                    {
                        result[key] = quality;
                    }
                }
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Image defaults loaded from theme's images.json.
    /// </summary>
    /// <remarks>
    /// Only formats are used as defaults for quality values.
    /// Sizes are informational only (managed by theme).
    /// </remarks>
    private sealed record ImageDefaults(Dictionary<string, int> Formats, int[] Sizes);
}

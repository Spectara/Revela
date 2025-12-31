using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Images;

/// <summary>
/// Command to configure image processing settings.
/// </summary>
/// <remarks>
/// Configures output formats, quality, and sizes in project.json.
/// Uses theme's images.template.json for recommended defaults.
/// </remarks>
public sealed partial class ConfigImageCommand(
    ILogger<ConfigImageCommand> logger,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IThemeResolver themeResolver,
    IConfigService configService)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("image", "Configure image processing settings");

        var formatsOption = new Option<string?>("--formats", "-f")
        {
            Description = "Output formats with optional quality (e.g., avif:80,webp:85,jpg or just avif,webp,jpg)"
        };
        var sizesOption = new Option<string?>("--sizes", "-s")
        {
            Description = "Output sizes in pixels (comma-separated: 640,1280,1920)"
        };

        command.Options.Add(formatsOption);
        command.Options.Add(sizesOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var formats = parseResult.GetValue(formatsOption);
            var sizes = parseResult.GetValue(sizesOption);

            return await ExecuteAsync(formats, sizes, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes the image configuration.
    /// </summary>
    /// <param name="formatsArg">Formats with optional quality (format:quality or just format).</param>
    /// <param name="sizesArg">Sizes argument (comma-separated).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(
        string? formatsArg,
        string? sizesArg,
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

        // Load theme defaults from images.template.json
        var themeDefaults = LoadThemeDefaults(themeName);

        // Non-interactive mode if any argument provided
        if (!string.IsNullOrEmpty(formatsArg) || !string.IsNullOrEmpty(sizesArg))
        {
            return await ExecuteNonInteractiveAsync(formatsArg, sizesArg, themeDefaults, cancellationToken)
                .ConfigureAwait(false);
        }

        // Interactive mode
        return await ExecuteInteractiveAsync(themeDefaults, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Loads image defaults from theme's images.template.json.
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
        string? formatsArg,
        string? sizesArg,
        ImageDefaults? themeDefaults,
        CancellationToken cancellationToken)
    {
        // Read current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentFormats = GetCurrentFormats(current);
        var currentSizes = GetCurrentSizes(current);

        // Determine defaults: current > theme > fallback
        var defaultFormats = currentFormats
            ?? themeDefaults?.Formats
            ?? new Dictionary<string, int> { ["jpg"] = 90 };
        var defaultSizes = currentSizes
            ?? themeDefaults?.Sizes
            ?? [640, 1280, 1920];

        // Parse formats (format or format:quality)
        var validFormats = new HashSet<string>(["avif", "webp", "jpg"], StringComparer.OrdinalIgnoreCase);
        var formats = new Dictionary<string, int>();

        if (!string.IsNullOrEmpty(formatsArg))
        {
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

                // Check for quality (format:quality)
                if (parts.Length == 2 && int.TryParse(parts[1], out var quality) && quality is >= 1 and <= 100)
                {
                    formats[normalizedFormat] = quality;
                }
                else
                {
                    // No quality specified, use default from theme or fallback
                    formats[normalizedFormat] = defaultFormats.GetValueOrDefault(normalizedFormat, 90);
                }

                parsedCount++;
            }

            if (parsedCount == 0)
            {
                ErrorPanels.ShowValidationError(
                    "No valid formats specified.",
                    "  avif, webp, jpg (optionally with :quality, e.g. webp:85)");
                return 1;
            }
        }
        else
        {
            // Keep current/default formats
            foreach (var (format, quality) in defaultFormats)
            {
                formats[format] = quality;
            }
        }

        // Parse sizes
        var sizes = defaultSizes;
        if (!string.IsNullOrEmpty(sizesArg))
        {
            sizes = [.. sizesArg
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .Where(n => n > 0)
                .OrderBy(n => n)];

            if (sizes.Length == 0)
            {
                ErrorPanels.ShowValidationError(
                    "No valid sizes specified.",
                    "  Comma-separated widths in pixels (e.g. 640,1280,1920)");
                return 1;
            }
        }

        // Build and save config using JsonObject
        var update = new JsonObject
        {
            ["generate"] = new JsonObject
            {
                ["images"] = new JsonObject
                {
                    ["formats"] = CreateFormatsObject(formats),
                    ["sizes"] = new JsonArray(sizes.Select(s => JsonValue.Create(s)).ToArray())
                }
            }
        };

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        var formatList = string.Join(", ", formats.Select(f => $"{f.Key}:{f.Value}"));
        LogImageConfigured(formatList, string.Join(", ", sizes));
        AnsiConsole.MarkupLine("[green]✓[/] Image settings updated");

        return 0;
    }

    private async Task<int> ExecuteInteractiveAsync(ImageDefaults? themeDefaults, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[cyan]Configure image processing[/]\n");

        // Read current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentFormats = GetCurrentFormats(current);
        var currentSizes = GetCurrentSizes(current);

        // Determine defaults: current > theme
        var defaultFormats = currentFormats ?? themeDefaults?.Formats;
        var defaultSizes = currentSizes ?? themeDefaults?.Sizes;

        // Format selection
        var formatChoices = new[] { "avif", "webp", "jpg" };
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select output formats:")
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

        // Quality per format
        var formats = new Dictionary<string, int>();
        foreach (var format in selectedFormats)
        {
            var defaultQuality = defaultFormats?.GetValueOrDefault(format, 85) ?? 85;
            var quality = AnsiConsole.Prompt(
                new TextPrompt<int>($"Quality for [green]{format.ToUpperInvariant()}[/] (1-100):")
                    .DefaultValue(defaultQuality)
                    .Validate(q => q is >= 1 and <= 100
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Quality must be between 1 and 100[/]")));

            formats[format] = quality;
        }

        // Size input - use theme defaults or prompt for custom
        AnsiConsole.WriteLine();
        int[] sizes;

        if (defaultSizes is { Length: > 0 })
        {
            // Theme provides defaults - show them and allow editing
            var defaultSizesStr = string.Join(", ", defaultSizes);
            AnsiConsole.MarkupLine($"[dim]Theme recommends:[/] {defaultSizesStr}px");

            var useDefaults = await AnsiConsole.ConfirmAsync("Use recommended sizes?", defaultValue: true, cancellationToken)
                .ConfigureAwait(false);
            if (useDefaults)
            {
                sizes = defaultSizes;
            }
            else
            {
                sizes = PromptCustomSizes(defaultSizes);
            }
        }
        else
        {
            // No theme defaults - user must enter sizes manually
            AnsiConsole.MarkupLine("[yellow]Theme doesn't provide size recommendations.[/]");
            sizes = PromptCustomSizes([640, 1280, 1920]);
        }

        // Build update using JsonObject
        var update = new JsonObject
        {
            ["generate"] = new JsonObject
            {
                ["images"] = new JsonObject
                {
                    ["formats"] = CreateFormatsObject(formats),
                    ["sizes"] = new JsonArray(sizes.Select(s => JsonValue.Create(s)).ToArray())
                }
            }
        };

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        LogImageConfigured(string.Join(", ", selectedFormats), string.Join(", ", sizes));
        AnsiConsole.MarkupLine("\n[green]✓[/] Image settings updated");

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
        AnsiConsole.MarkupLine($"\n[dim]Sizes:[/] {string.Join(", ", sizes)}px");

        return 0;
    }

    private static int[] PromptCustomSizes(int[] defaultSizes)
    {
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter sizes (comma-separated, e.g., 640,1280,1920):")
                .DefaultValue(string.Join(",", defaultSizes)));

        var sizes = input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .OrderBy(n => n)
            .ToArray();

        if (sizes.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Invalid sizes. Using defaults.");
            return defaultSizes;
        }

        return sizes;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Image configured: formats=[{Formats}], sizes=[{Sizes}]")]
    private partial void LogImageConfigured(string formats, string sizes);

    /// <summary>
    /// Extracts formats dictionary from JsonObject config.
    /// </summary>
    private static Dictionary<string, int>? GetCurrentFormats(JsonObject? config)
    {
        var formats = config?["generate"]?["images"]?["formats"]?.AsObject();
        if (formats is null)
        {
            return null;
        }

        var result = new Dictionary<string, int>();
        foreach (var (key, value) in formats)
        {
            if (value?.GetValue<int>() is { } quality)
            {
                result[key] = quality;
            }
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Extracts sizes array from JsonObject config.
    /// </summary>
    private static int[]? GetCurrentSizes(JsonObject? config)
    {
        var sizes = config?["generate"]?["images"]?["sizes"]?.AsArray();
        if (sizes is null)
        {
            return null;
        }

        var result = sizes
            .Where(s => s is not null)
            .Select(s => s!.GetValue<int>())
            .ToArray();

        return result.Length > 0 ? result : null;
    }

    /// <summary>
    /// Creates a JsonObject from formats dictionary.
    /// </summary>
    private static JsonObject CreateFormatsObject(Dictionary<string, int> formats)
    {
        var obj = new JsonObject();
        foreach (var (format, quality) in formats)
        {
            obj[format] = quality;
        }

        return obj;
    }

    /// <summary>
    /// Image defaults loaded from theme's images.template.json.
    /// </summary>
    private sealed record ImageDefaults(Dictionary<string, int> Formats, int[] Sizes);
}

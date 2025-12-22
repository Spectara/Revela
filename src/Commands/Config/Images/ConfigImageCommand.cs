using System.CommandLine;
using System.Globalization;
using Spectara.Revela.Commands.Config.Models;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Images;

/// <summary>
/// Command to configure image processing settings.
/// </summary>
/// <remarks>
/// Configures output formats, quality, and sizes in project.json.
/// </remarks>
public sealed partial class ConfigImageCommand(
    ILogger<ConfigImageCommand> logger,
    IConfigService configService)
{
    private static readonly Dictionary<string, int> DefaultQualities = new()
    {
        ["avif"] = 80,
        ["webp"] = 85,
        ["jpg"] = 90
    };

    private static readonly int[] PresetSizes = [640, 1024, 1280, 1920, 2560];

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

        // Non-interactive mode if any argument provided
        if (!string.IsNullOrEmpty(formatsArg) || !string.IsNullOrEmpty(sizesArg))
        {
            return await ExecuteNonInteractiveAsync(formatsArg, sizesArg, cancellationToken)
                .ConfigureAwait(false);
        }

        // Interactive mode
        return await ExecuteInteractiveAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ExecuteNonInteractiveAsync(
        string? formatsArg,
        string? sizesArg,
        CancellationToken cancellationToken)
    {
        // Read current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentFormats = current?.Generate?.Images?.Formats ?? new Dictionary<string, int> { ["jpg"] = 90 };
        var currentSizes = current?.Generate?.Images?.Sizes ?? PresetSizes;

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
                    // No quality specified, use current or default
                    formats[normalizedFormat] = currentFormats.GetValueOrDefault(normalizedFormat, DefaultQualities[normalizedFormat]);
                }

                parsedCount++;
            }

            if (parsedCount == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No valid formats specified. Use: avif, webp, jpg (optionally with :quality)");
                return 1;
            }
        }
        else
        {
            // Keep current formats
            foreach (var (format, quality) in currentFormats)
            {
                formats[format] = quality;
            }
        }

        // Parse sizes
        var sizes = currentSizes;
        if (!string.IsNullOrEmpty(sizesArg))
        {
            sizes = [.. sizesArg
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .Where(n => n > 0)
                .OrderBy(n => n)];

            if (sizes.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No valid sizes specified.");
                return 1;
            }
        }

        // Build and save config using DTO
        var update = new ProjectConfigDto
        {
            Generate = new GenerateConfigDto
            {
                Images = new ImageConfigDto
                {
                    Formats = formats,
                    Sizes = sizes
                }
            }
        };

        await configService.UpdateProjectConfigAsync(update, cancellationToken).ConfigureAwait(false);

        var formatList = string.Join(", ", formats.Select(f => $"{f.Key}:{f.Value}"));
        LogImageConfigured(formatList, string.Join(", ", sizes));
        AnsiConsole.MarkupLine("[green]✓[/] Image settings updated");

        return 0;
    }

    private async Task<int> ExecuteInteractiveAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[cyan]Configure image processing[/]\n");

        // Read current config
        var current = await configService.ReadProjectConfigAsync(cancellationToken).ConfigureAwait(false);
        var currentFormats = current?.Generate?.Images?.Formats ?? new Dictionary<string, int> { ["jpg"] = 90 };
        var currentSizes = current?.Generate?.Images?.Sizes ?? [640, 1280, 1920];

        // Format selection
        var formatChoices = new[] { "avif", "webp", "jpg" };
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select output formats:")
            .PageSize(5)
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to accept)[/]")
            .AddChoices(formatChoices);

        // Pre-select current formats
        foreach (var format in currentFormats.Keys)
        {
            prompt.Select(format);
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
            var currentQuality = currentFormats.GetValueOrDefault(format, DefaultQualities[format]);
            var quality = AnsiConsole.Prompt(
                new TextPrompt<int>($"Quality for [green]{format.ToUpperInvariant()}[/] (1-100):")
                    .DefaultValue(currentQuality)
                    .Validate(q => q is >= 1 and <= 100
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Quality must be between 1 and 100[/]")));

            formats[format] = quality;
        }

        // Size selection
        AnsiConsole.WriteLine();
        var sizeChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Image sizes:")
                .AddChoices(
                    "Standard (640, 1024, 1280, 1920, 2560)",
                    "Minimal (640, 1280, 1920)",
                    "High-res (1280, 1920, 2560, 3840)",
                    "Custom"));

        // Explicit type required for collection expressions with mixed branches
#pragma warning disable IDE0007 // Use implicit type
        int[] sizes = sizeChoice switch
        {
            "Standard (640, 1024, 1280, 1920, 2560)" => [640, 1024, 1280, 1920, 2560],
            "Minimal (640, 1280, 1920)" => [640, 1280, 1920],
            "High-res (1280, 1920, 2560, 3840)" => [1280, 1920, 2560, 3840],
            _ => PromptCustomSizes(currentSizes)
        };
#pragma warning restore IDE0007

        // Build update using DTO
        var update = new ProjectConfigDto
        {
            Generate = new GenerateConfigDto
            {
                Images = new ImageConfigDto
                {
                    Formats = formats,
                    Sizes = sizes
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

    private static int[] PromptCustomSizes(int[] currentSizes)
    {
        var input = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter sizes (comma-separated, e.g., 640,1280,1920):")
                .DefaultValue(string.Join(",", currentSizes)));

        var sizes = input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .OrderBy(n => n)
            .ToArray();

        if (sizes.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Invalid sizes. Using defaults.");
            return PresetSizes;
        }

        return sizes;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Image configured: formats=[{Formats}], sizes=[{Sizes}]")]
    private partial void LogImageConfigured(string formats, string sizes);
}

using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Command to list all theme files with source information.
/// </summary>
/// <remarks>
/// Shows all templates and assets from:
/// - Base theme (embedded resources)
/// - Theme extensions
/// - Local overrides
///
/// The paths shown can be used directly with <c>revela theme extract --file</c>.
/// </remarks>
public sealed partial class ThemeFilesCommand(
    IThemeResolver themeResolver,
    ITemplateResolver templateResolver,
    IAssetResolver assetResolver,
    IConfiguration configuration,
    ILogger<ThemeFilesCommand> logger)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    /// <returns>The configured files command.</returns>
    public Command Create()
    {
        var themeOption = new Option<string?>("--theme", "-t")
        {
            Description = "Theme name (defaults to theme from project.json)"
        };

        var command = new Command("files", "List all theme files with source information");
        command.Options.Add(themeOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var themeName = parseResult.GetValue(themeOption);
            await ExecuteAsync(themeName, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    // Color palette for visual distinction
    private const string ThemeColor = "grey";
    private static readonly string[] ExtensionColors = ["blue", "magenta", "darkcyan", "darkorange", "mediumpurple"];

    private Task ExecuteAsync(string? themeNameOverride, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectPath = Environment.CurrentDirectory;

        // Get theme name from option or config
        var themeName = themeNameOverride ?? configuration["theme"] ?? "default";

        // Resolve theme
        var theme = themeResolver.Resolve(themeName, projectPath);
        if (theme is null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Theme [yellow]'{EscapeMarkup(themeName)}'[/] not found.");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Run [cyan]revela theme list[/] to see available themes.");
            return Task.CompletedTask;
        }

        // Get extensions
        var extensions = themeResolver.GetExtensions(themeName);

        // Build extension color map
        var extensionColorMap = extensions
            .Select((ext, index) => (ext.Metadata.Name, Color: ExtensionColors[index % ExtensionColors.Length]))
            .ToDictionary(x => x.Name, x => x.Color, StringComparer.OrdinalIgnoreCase);

        // Initialize resolvers
        templateResolver.Initialize(theme, extensions, projectPath);
        assetResolver.Initialize(theme, extensions, projectPath);

        LogInitialized(themeName, extensions.Count);

        // Get all entries
        var templateEntries = templateResolver.GetAllEntries();
        var assetEntries = assetResolver.GetAllEntries();

        // Build templates table
        var templatesTable = new Table
        {
            Border = TableBorder.Rounded
        };
        templatesTable.AddColumn("[bold]Path[/]");
        templatesTable.AddColumn("[bold]Source[/]");

        foreach (var entry in templateEntries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceDisplay = FormatSource(entry, themeName, extensionColorMap);
            templatesTable.AddRow(
                $"[cyan]{EscapeMarkup(entry.Key)}.revela[/]",
                sourceDisplay);
        }

        // Build assets table
        var assetsTable = new Table
        {
            Border = TableBorder.Rounded
        };
        assetsTable.AddColumn("[bold]Path[/]");
        assetsTable.AddColumn("[bold]Source[/]");

        foreach (var entry in assetEntries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceDisplay = FormatSource(entry, themeName, extensionColorMap);
            assetsTable.AddRow(
                $"[cyan]{EscapeMarkup(entry.Key)}[/]",
                sourceDisplay);
        }

        // Panel header with extension names (colored)
        var extensionNames = extensions.Select(e => e.Metadata.Name).ToList();
        var coloredExtensions = extensionNames
            .Select(name => $"[{extensionColorMap[name]}]{EscapeMarkup(name)}[/]");
        var extensionInfo = extensionNames.Count > 0
            ? " + " + string.Join(", ", coloredExtensions)
            : string.Empty;

        // Create grid to hold both tables with left-aligned titles
        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Markup("[blue]Templates[/]"));
        grid.AddRow(templatesTable);
        grid.AddEmptyRow();
        grid.AddRow(new Markup("[yellow]Assets[/]"));
        grid.AddRow(assetsTable);

        var panel = new Panel(grid)
            .WithHeader($"[bold]Theme Files[/] [dim]([/][{ThemeColor}]{EscapeMarkup(themeName)}[/]{extensionInfo}[dim])[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine($"\n[dim]Total:[/] {templateEntries.Count} templates, {assetEntries.Count} assets");

        // Build legend with colored extension names
        var legendParts = new List<string> { $"[{ThemeColor}]{EscapeMarkup(themeName)}[/] = Theme" };
        foreach (var name in extensionNames)
        {
            legendParts.Add($"[{extensionColorMap[name]}]{EscapeMarkup(name)}[/] = Extension");
        }
        legendParts.Add("[green]Local[/] = Override");
        AnsiConsole.MarkupLine($"[dim]Source:[/] {string.Join(", ", legendParts)}");

        AnsiConsole.MarkupLine("");
        AnsiConsole.MarkupLine("[dim]Tip:[/] Use [cyan]revela theme extract --file <path>[/] to extract specific files for customization");

        return Task.CompletedTask;
    }

    private static string FormatSource(
        ResolvedFileInfo entry,
        string themeName,
        Dictionary<string, string> extensionColorMap)
    {
        return entry.Source switch
        {
            FileSourceType.Theme => $"[{ThemeColor}]{EscapeMarkup(themeName)}[/]",
            FileSourceType.Extension when entry.ExtensionName is not null &&
                extensionColorMap.TryGetValue(entry.ExtensionName, out var color)
                => $"[{color}]{EscapeMarkup(entry.ExtensionName)}[/]",
            FileSourceType.Extension => $"[blue]{EscapeMarkup(entry.ExtensionName ?? "Extension")}[/]",
            FileSourceType.Local => "[green]Local[/]",
            _ => "[dim]Unknown[/]"
        };
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing files for theme '{ThemeName}' with {ExtensionCount} extension(s)")]
    private partial void LogInitialized(string themeName, int extensionCount);
}


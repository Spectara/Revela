using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;
using Spectre.Console;

namespace Spectara.Revela.Features.Theme.Commands;

/// <summary>
/// Command to list all theme files — uses <see cref="IThemeService"/> for data,
/// renders tables with Spectre.Console.
/// </summary>
internal sealed partial class ThemeFilesCommand(
    IThemeService themeService,
    IThemeResolver themeResolver,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig,
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
            await ExecuteAsync(themeName, cancellationToken);
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
        var projectPath = projectEnvironment.Value.Path;

        // Get theme name from option or config
        var themeName = themeNameOverride ?? themeConfig.CurrentValue.Name;
        if (string.IsNullOrWhiteSpace(themeName))
        {
            themeName = "Lumina";
        }

        // Use ThemeService for file data
        var filesResult = themeService.GetFiles(themeName);

        if (filesResult.Templates.Count == 0 && filesResult.Assets.Count == 0)
        {
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{Markup.Escape(themeName)}' not found.[/]\n\n" +
                "Run [cyan]revela theme list[/] to see available themes.");
            return Task.CompletedTask;
        }

        // Get extensions for color map and config entries
        var extensions = themeResolver.GetExtensions(themeName);
        var theme = themeResolver.Resolve(themeName, projectPath);

        // Build extension color map
        var extensionColorMap = extensions
            .Select((ext, index) => (ext.Metadata.Name, Color: ExtensionColors[index % ExtensionColors.Length]))
            .ToDictionary(x => x.Name, x => x.Color, StringComparer.OrdinalIgnoreCase);

        LogInitialized(themeName, extensions.Count);

        // Get entries from service result
        var templateEntries = filesResult.Templates;
        var assetEntries = filesResult.Assets;
        var configEntries = theme is not null
            ? GetConfigurationEntries(theme, extensions, projectPath, themeName, extensionColorMap)
            : [];

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
                $"[cyan]{Markup.Escape(entry.Key)}.revela[/]",
                sourceDisplay);
        }

        // Build configuration table
        var configTable = new Table
        {
            Border = TableBorder.Rounded
        };
        configTable.AddColumn("[bold]Path[/]");
        configTable.AddColumn("[bold]Source[/]");

        foreach (var (path, source) in configEntries.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            configTable.AddRow(
                $"[cyan]{Markup.Escape(path)}[/]",
                source);
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
                $"[cyan]{Markup.Escape(entry.Key)}[/]",
                sourceDisplay);
        }

        // Panel header with extension names (colored)
        var extensionNames = extensions.Select(e => e.Metadata.Name).ToList();
        var coloredExtensions = extensionNames
            .Select(name => $"[{extensionColorMap[name]}]{Markup.Escape(name)}[/]");
        var extensionInfo = extensionNames.Count > 0
            ? " + " + string.Join(", ", coloredExtensions)
            : string.Empty;

        // Create grid to hold tables with left-aligned titles
        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Markup("[blue]Templates[/]"));
        grid.AddRow(templatesTable);
        grid.AddEmptyRow();
        grid.AddRow(new Markup("[green]Configuration[/]"));
        grid.AddRow(configTable);
        grid.AddEmptyRow();
        grid.AddRow(new Markup("[yellow]Assets[/]"));
        grid.AddRow(assetsTable);

        var panel = new Panel(grid)
            .WithHeader($"[bold]Theme Files[/] [dim]([/][{ThemeColor}]{Markup.Escape(themeName)}[/]{extensionInfo}[dim])[/]")
            .WithInfoStyle();
        panel.Padding = new Padding(1, 0, 1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine($"\n[dim]Total:[/] {templateEntries.Count} templates, {configEntries.Count} config files, {assetEntries.Count} assets");

        // Build legend with colored extension names
        var legendParts = new List<string> { $"[{ThemeColor}]{Markup.Escape(themeName)}[/] = Theme" };
        foreach (var name in extensionNames)
        {
            legendParts.Add($"[{extensionColorMap[name]}]{Markup.Escape(name)}[/] = Extension");
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
            FileSourceType.Theme => $"[{ThemeColor}]{Markup.Escape(themeName)}[/]",
            FileSourceType.Extension when entry.ExtensionName is not null &&
                extensionColorMap.TryGetValue(entry.ExtensionName, out var color)
                => $"[{color}]{Markup.Escape(entry.ExtensionName)}[/]",
            FileSourceType.Extension => $"[blue]{Markup.Escape(entry.ExtensionName ?? "Extension")}[/]",
            FileSourceType.Local => "[green]Local[/]",
            _ => "[dim]Unknown[/]"
        };
    }

    /// <summary>
    /// Gets configuration files from theme and extensions with source information.
    /// </summary>
    private static List<(string Path, string Source)> GetConfigurationEntries(
        ITheme theme,
        IReadOnlyList<ITheme> extensions,
        string projectPath,
        string themeName,
        Dictionary<string, string> extensionColorMap)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Get from base theme (Configuration/*.json and manifest.json)
        foreach (var file in theme.GetAllFiles())
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.StartsWith("Configuration/", StringComparison.OrdinalIgnoreCase))
            {
                // Use lowercase for consistency
                var key = "configuration/" + normalized["Configuration/".Length..];
                entries[key] = $"[{ThemeColor}]{Markup.Escape(themeName)}[/]";
            }
            else if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                entries["manifest.json"] = $"[{ThemeColor}]{Markup.Escape(themeName)}[/]";
            }
        }

        // Get from extensions (can override)
        foreach (var ext in extensions)
        {
            var extName = ext.Metadata.Name;
            var color = extensionColorMap.GetValueOrDefault(extName, "blue");

            foreach (var file in ext.GetAllFiles())
            {
                var normalized = file.Replace('\\', '/');
                if (normalized.StartsWith("Configuration/", StringComparison.OrdinalIgnoreCase))
                {
                    var key = "configuration/" + normalized["Configuration/".Length..];
                    entries[key] = $"[{color}]{Markup.Escape(extName)}[/]";
                }
                else if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    entries["manifest.json"] = $"[{color}]{Markup.Escape(extName)}[/]";
                }
            }
        }

        // Check for local overrides in theme/configuration/ folder (lowercase)
        var localConfigPath = Path.Combine(projectPath, "theme", "configuration");
        if (Directory.Exists(localConfigPath))
        {
            foreach (var file in Directory.GetFiles(localConfigPath, "*.json", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(localConfigPath, file).Replace('\\', '/');
                var configPath = "configuration/" + relativePath;

                // Only mark as local override if it matches a theme config file (case-insensitive)
                var matchingKey = entries.Keys.FirstOrDefault(k => k.Equals(configPath, StringComparison.OrdinalIgnoreCase));
                if (matchingKey is not null)
                {
                    entries[matchingKey] = "[green]Local[/]";
                }
            }
        }

        return [.. entries.Select(kvp => (kvp.Key, kvp.Value))];
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Listing files for theme '{ThemeName}' with {ExtensionCount} extension(s)")]
    private partial void LogInitialized(string themeName, int extensionCount);
}





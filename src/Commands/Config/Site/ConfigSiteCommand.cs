using System.CommandLine;
using System.Globalization;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Site;

/// <summary>
/// Command to manage site configuration.
/// </summary>
/// <remarks>
/// <para>
/// Creates or edits site.json with interactive prompts.
/// Uses JSON structure from theme template to determine available properties.
/// When editing, existing values are used as defaults.
/// </para>
/// </remarks>
public sealed partial class ConfigSiteCommand(
    ILogger<ConfigSiteCommand> logger,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IConfigService configService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Creates the command definition.
    /// </summary>
    public Command Create()
    {
        var command = new Command("site", "Configure site settings (site.json)");

        command.SetAction(async (_, cancellationToken) =>
            await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    /// <summary>
    /// Executes the site configuration (create or edit).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Exit code.</returns>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!configService.IsProjectInitialized())
        {
            ErrorPanels.ShowNotAProjectError();
            return 1;
        }

        // Get selected theme from IOptions (runtime reading)
        var themeName = themeConfig.CurrentValue.Name;

        if (string.IsNullOrWhiteSpace(themeName))
        {
            ErrorPanels.ShowError(
                "No Theme Selected",
                "[yellow]Site configuration depends on the selected theme.[/]\n\n" +
                "[bold]Run first:[/] [cyan]revela config theme[/]");
            return 1;
        }

        var siteConfigPath = Path.GetFullPath(configService.SiteConfigPath);
        var isEditMode = configService.IsSiteConfigured();

        // Get the configured theme
        var projectPath = Directory.GetCurrentDirectory();
        var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();
        var selectedTheme = availableThemes.FirstOrDefault(t =>
            t.Metadata.Name.Equals(themeName, StringComparison.OrdinalIgnoreCase));

        if (selectedTheme is null)
        {
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{themeName}' is not installed.[/]\n\n" +
                "[bold]Install it:[/] [cyan]revela plugin install Spectara.Revela.Theme.{themeName}[/]");
            return 1;
        }

        await using var siteTemplateStream = selectedTheme.GetSiteTemplate();

        if (siteTemplateStream is null)
        {
            ErrorPanels.ShowWarning(
                "No Template",
                $"[yellow]Theme '{selectedTheme.Metadata.Name}' doesn't provide a site.json template.[/]\n\n" +
                "[dim]Create site.json manually.[/]");
            return 1;
        }

        // Read template for structure
        using var reader = new StreamReader(siteTemplateStream);
        var templateJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        // Get current values (from existing file or empty from template)
        string sourceJson;
        if (isEditMode)
        {
            sourceJson = await File.ReadAllTextAsync(siteConfigPath, cancellationToken).ConfigureAwait(false);
            LogEditingExisting(logger, siteConfigPath);
        }
        else
        {
            sourceJson = templateJson;
            LogCreatingNew(logger);
        }

        // Extract properties with current values
        var properties = JsonPropertyExtractor.ExtractProperties(sourceJson);

        LogFoundProperties(logger, string.Join(", ", properties.Select(p => p.Path)));

        // Get project name for smart defaults (from directory if not set)
        var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;

        // Collect values via interactive prompts
        var values = CollectValues(properties, isEditMode, projectName);

        // Build final JSON using template structure
        var finalJson = JsonPropertyExtractor.BuildJson(templateJson, values);
        await File.WriteAllTextAsync(siteConfigPath, finalJson, cancellationToken).ConfigureAwait(false);

        LogSavedSiteConfig(logger, siteConfigPath);

        // Show success
        ShowSuccessPanel(siteConfigPath, values, selectedTheme.Metadata.Name, isEditMode);

        return 0;
    }

    /// <summary>
    /// Collects values for all properties via interactive prompts.
    /// </summary>
    private static Dictionary<string, string> CollectValues(
        IReadOnlyList<JsonProperty> properties,
        bool isEditMode,
        string projectName)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);

        var action = isEditMode ? "Edit" : "Configure";
        AnsiConsole.MarkupLine($"[cyan]{action} site settings[/]\n");

        // Sort properties by order, then alphabetically
        var sortedProperties = properties
            .OrderBy(p => GetPropertyOrder(p.Path))
            .ThenBy(p => p.Path)
            .ToList();

        foreach (var property in sortedProperties)
        {
            var (smartDefault, description) = GetPropertyDefaults(property.Path, projectName, year, values);

            // Use existing value if available, otherwise smart default
            var currentValue = !string.IsNullOrEmpty(property.Value) ? property.Value : smartDefault;

            var value = AnsiConsole.Prompt(
                new TextPrompt<string>($"{description}:")
                    .DefaultValue(currentValue)
                    .AllowEmpty());

            // Use current value if user just pressed Enter
            values[property.Path] = string.IsNullOrWhiteSpace(value) ? currentValue : value;
        }

        AnsiConsole.WriteLine();

        return values;
    }

    /// <summary>
    /// Gets the sort order for a property (controls prompt order).
    /// </summary>
    private static int GetPropertyOrder(string path) => path.ToUpperInvariant() switch
    {
        "TITLE" => 1,
        "AUTHOR" => 2,
        "DESCRIPTION" => 3,
        "COPYRIGHT" => 4,
        _ when path.StartsWith("SOCIAL.", StringComparison.OrdinalIgnoreCase) => 50,
        _ => 100
    };

    /// <summary>
    /// Gets smart default value and description for a property.
    /// </summary>
    private static (string DefaultValue, string Description) GetPropertyDefaults(
        string path,
        string projectName,
        string year,
        Dictionary<string, string> collectedValues)
    {
        // Description always from property path (e.g., "social.twitter" → "Social Twitter")
        var description = FormatPropertyName(path);

        // Smart defaults for known properties
        var defaultValue = path.ToUpperInvariant() switch
        {
            "TITLE" => projectName,
            "AUTHOR" => Environment.UserName,
            "DESCRIPTION" => "Photography portfolio",
            "COPYRIGHT" => collectedValues.TryGetValue("author", out var author) && !string.IsNullOrEmpty(author)
                ? $"© {year} {author}. All rights reserved."
                : $"© {year} {Environment.UserName}. All rights reserved.",
            _ => ""
        };

        return (defaultValue, description);
    }

    /// <summary>
    /// Formats a property path as a human-readable label.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - "social.twitter" → "Social Twitter"
    /// - "my_property" → "My Property"
    /// </remarks>
    private static string FormatPropertyName(string path)
    {
        // Replace dots and underscores with spaces, then capitalize each word
        var name = path
            .Replace(".", " ", StringComparison.Ordinal)
            .Replace("_", " ", StringComparison.Ordinal);

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Select(w =>
            string.IsNullOrEmpty(w)
                ? w
                : char.ToUpperInvariant(w[0]) + w[1..]));
    }

    private static void ShowSuccessPanel(
        string path,
        Dictionary<string, string> values,
        string themeName,
        bool isEditMode)
    {
        var action = isEditMode ? "Updated" : "Created";

        // Group values by top-level key for cleaner display
        var valuesSummary = string.Join("\n",
            values
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"  [dim]{kv.Key}:[/] {kv.Value}"));

        if (string.IsNullOrWhiteSpace(valuesSummary))
        {
            valuesSummary = "  [dim](all empty)[/]";
        }

        var panel = new Panel(
            $"[green]{action} site.json[/]\n\n" +
            $"[bold]Values:[/]\n{valuesSummary}\n\n" +
            $"[bold]File:[/]\n[link={path}]{path}[/]\n\n" +
            $"[dim]Theme: {themeName}[/]")
            .WithHeader($"[bold green]✓ Site {action}[/]")
            .WithSuccessStyle();

        AnsiConsole.Write(panel);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Editing existing site.json at {Path}")]
    private static partial void LogEditingExisting(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating new site.json from template")]
    private static partial void LogCreatingNew(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found properties: {Properties}")]
    private static partial void LogFoundProperties(ILogger logger, string properties);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved site.json at {Path}")]
    private static partial void LogSavedSiteConfig(ILogger logger, string path);
}

using System.CommandLine;
using System.Globalization;
using Spectara.Revela.Commands.Config.Services;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectre.Console;

namespace Spectara.Revela.Commands.Config.Site;

/// <summary>
/// Command to manage site configuration.
/// </summary>
/// <remarks>
/// <para>
/// Creates site.json from theme template with interactive prompts.
/// Options are generated dynamically from template placeholders.
/// </para>
/// </remarks>
public sealed partial class ConfigSiteCommand(
    ILogger<ConfigSiteCommand> logger,
    IConfigService configService,
    IScaffoldingService scaffoldingService,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Creates the command definition with dynamic options from template.
    /// </summary>
    public Command Create()
    {
        var command = new Command("site", "Configure site settings (site.json)");

        // We'll add dynamic options when we have the template
        // For now, set up the action that will handle both cases
        command.SetAction(async (parseResult, cancellationToken) =>
            await ExecuteAsync(cancellationToken).ConfigureAwait(false));

        return command;
    }

    /// <summary>
    /// Executes the site configuration.
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

        var siteConfigPath = Path.GetFullPath(configService.SiteConfigPath);

        if (configService.IsSiteConfigured())
        {
            // File exists - show clickable path
            ShowSiteConfigPath(siteConfigPath);
            return 0;
        }

        // Get theme template
        var projectPath = Directory.GetCurrentDirectory();
        var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (availableThemes.Count == 0)
        {
            ErrorPanels.ShowNothingInstalledError(
                "themes",
                "plugin install Spectara.Revela.Theme.Lumina");
            return 1;
        }

        var selectedTheme = availableThemes[0];
        await using var siteTemplateStream = selectedTheme.GetSiteTemplate();

        if (siteTemplateStream is null)
        {
            ErrorPanels.ShowWarning(
                "No Template",
                $"[yellow]Theme '{selectedTheme.Metadata.Name}' doesn't provide a site.json template.[/]\n\n" +
                "[dim]Create site.json manually.[/]");
            return 1;
        }

        // Read template and extract variables
        using var reader = new StreamReader(siteTemplateStream);
        var siteTemplate = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var variables = TemplateVariableExtractor.ExtractVariables(siteTemplate);

        LogFoundVariables(logger, string.Join(", ", variables));

        // Collect values for each variable (interactive prompts)
        var values = await CollectValuesAsync(variables, cancellationToken).ConfigureAwait(false);

        // Render template with collected values
        var siteConfig = scaffoldingService.RenderTemplateContent(siteTemplate, values);
        await File.WriteAllTextAsync(siteConfigPath, siteConfig, cancellationToken).ConfigureAwait(false);

        LogCreatedSiteConfig(logger, siteConfigPath);

        // Show success with summary
        ShowSuccessPanel(siteConfigPath, values, selectedTheme.Metadata.Name);

        return 0;
    }

    /// <summary>
    /// Collects values for template variables via interactive prompts.
    /// </summary>
    private static Task<Dictionary<string, object>> CollectValuesAsync(
        IReadOnlySet<string> variables,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken; // Reserved for future async prompts

        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
        var year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);

        AnsiConsole.MarkupLine("[cyan]Configure site settings[/]\n");

        foreach (var variable in variables.OrderBy(v => GetVariableOrder(v)))
        {
            var (defaultValue, description) = GetVariableDefaults(variable, projectName, year, values);

            var value = AnsiConsole.Prompt(
                new TextPrompt<string>($"{description}:")
                    .DefaultValue(defaultValue)
                    .AllowEmpty());

            // Use default if empty
            values[variable] = string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        AnsiConsole.WriteLine();

        return Task.FromResult(values);
    }

    /// <summary>
    /// Gets the sort order for a variable (controls prompt order).
    /// </summary>
    private static int GetVariableOrder(string variable) => variable.ToUpperInvariant() switch
    {
        "TITLE" => 1,
        "AUTHOR" => 2,
        "DESCRIPTION" => 3,
        "COPYRIGHT" => 4,
        _ => 100
    };

    /// <summary>
    /// Gets default value and description for a variable.
    /// </summary>
    private static (string DefaultValue, string Description) GetVariableDefaults(
        string variable,
        string projectName,
        string year,
        Dictionary<string, object> collectedValues)
    {
        return variable.ToUpperInvariant() switch
        {
            "TITLE" => (projectName, "Site title"),
            "AUTHOR" => (Environment.UserName, "Author name"),
            "DESCRIPTION" => ("Photography portfolio", "Site description"),
            "COPYRIGHT" => (
                collectedValues.TryGetValue("author", out var author)
                    ? $"© {year} {author}. All rights reserved."
                    : $"© {year} Your Name. All rights reserved.",
                "Copyright notice"),
            _ => (string.Empty, variable.Replace("_", " ", StringComparison.Ordinal))
        };
    }

    private static void ShowSiteConfigPath(string path)
    {
        var panel = new Panel(
            $"[bold]Site configuration:[/]\n" +
            $"[link={path}]{path}[/]\n\n" +
            $"[dim]Edit this file directly - structure depends on your theme.[/]")
            .WithHeader("[bold cyan]site.json[/]")
            .WithInfoStyle();

        AnsiConsole.Write(panel);
    }

    private static void ShowSuccessPanel(string path, Dictionary<string, object> values, string themeName)
    {
        var valuesSummary = string.Join("\n",
            values.Select(kv => $"  [dim]{kv.Key}:[/] {kv.Value}"));

        var panel = new Panel(
            $"[green]Created site.json[/]\n\n" +
            $"[bold]Values:[/]\n{valuesSummary}\n\n" +
            $"[bold]File:[/]\n[link={path}]{path}[/]\n\n" +
            $"[dim]Theme: {themeName}[/]")
            .WithHeader("[bold green]✓ Site Configured[/]")
            .WithSuccessStyle();

        AnsiConsole.Write(panel);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found template variables: {Variables}")]
    private static partial void LogFoundVariables(ILogger logger, string variables);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created site.json at {Path}")]
    private static partial void LogCreatedSiteConfig(ILogger logger, string path);
}

using System.CommandLine;
using Spectara.Revela.Commands.Init.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;
using Spectara.Revela.Sdk;
using System.Text.Json;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Initializes a complete project with all available plugin configurations.
/// </summary>
/// <remarks>
/// <para>
/// This command runs the project initialization and then initializes all
/// available plugin configurations with their default values.
/// </para>
/// <para>
/// Usage: revela init all
/// </para>
/// </remarks>
public sealed partial class InitAllCommand(
    ILogger<InitAllCommand> logger,
    IScaffoldingService scaffoldingService,
    IThemeResolver themeResolver,
    IEnumerable<IPageTemplate> templates)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the 'init all' command.
    /// </summary>
    public Command Create()
    {
        var command = new Command("all", "Initialize project with all plugin configurations");

        command.SetAction(async (_, cancellationToken) =>
        {
            return await ExecuteAsync(cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if already initialized
            if (File.Exists("project.json") || File.Exists("site.json"))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Project already initialized (project.json or site.json exists)");
                return 1;
            }

            // Check for available themes
            var projectPath = Directory.GetCurrentDirectory();
            var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

            if (availableThemes.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No themes available.\n");
                AnsiConsole.MarkupLine("Install a theme first:");
                AnsiConsole.MarkupLine("  [cyan]revela plugin install Spectara.Revela.Theme.Lumina[/]\n");
                AnsiConsole.MarkupLine("To see available themes:");
                AnsiConsole.MarkupLine("  [cyan]revela theme list --online[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[blue]>[/] Initializing complete Revela project...\n");

            // Use sensible defaults - user can customize via 'revela config'
            var projectName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            var projectAuthor = Environment.UserName;
            var defaultTheme = availableThemes[0].Metadata.Name;
            var currentYear = DateTime.Now.Year;

            LogInitializingProject(projectName);

            // Template model - use first available theme as default
            var model = new
            {
                project = new
                {
                    name = projectName,
                    url = "https://revela.website",
                    theme = defaultTheme
                },
                site = new
                {
                    title = projectName,
                    author = projectAuthor,
                    year = currentYear,
                    description = $"Photography portfolio by {projectAuthor}"
                }
            };

            // Use ScaffoldingService to render project.json template
            var projectConfig = scaffoldingService.RenderTemplate("Project.project.json", model);

            cancellationToken.ThrowIfCancellationRequested();
            await File.WriteAllTextAsync("project.json", projectConfig, cancellationToken).ConfigureAwait(false);

            // Get site.json template from theme (if theme provides one)
            var selectedTheme = availableThemes[0];
            var hasSiteJson = false;
            await using var siteTemplateStream = selectedTheme.GetSiteTemplate();
            if (siteTemplateStream is not null)
            {
                using var reader = new StreamReader(siteTemplateStream);
                var siteTemplate = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                var siteConfig = scaffoldingService.RenderTemplateContent(siteTemplate, model);
                await File.WriteAllTextAsync("site.json", siteConfig, cancellationToken).ConfigureAwait(false);
                hasSiteJson = true;
            }

            // Create directories
            Directory.CreateDirectory("source");
            Directory.CreateDirectory("output");

            AnsiConsole.MarkupLine($"[green]✓[/] Created project files with theme [cyan]{defaultTheme}[/]");

            // Initialize all plugin configurations
            var pluginTemplates = templates.Where(t => t.ConfigProperties.Count > 0).ToList();

            if (pluginTemplates.Count > 0)
            {
                AnsiConsole.MarkupLine("");
                Directory.CreateDirectory("plugins");

                foreach (var template in pluginTemplates)
                {
                    await InitializePluginConfigAsync(template, cancellationToken).ConfigureAwait(false);
                }
            }

            // Success message
            var createdFiles = hasSiteJson
                ? "project.json, site.json, source/, output/, plugins/"
                : "project.json, source/, output/, plugins/";
            AnsiConsole.MarkupLine($"\n[green]✓[/] Project '{projectName}' fully initialized");
            AnsiConsole.MarkupLine($"[dim]Created: {createdFiles}[/]\n");

            // Show next steps
            var panel = new Panel("[bold]Next steps:[/]\n" +
                                "1. Run [cyan]revela config[/] to customize settings\n" +
                                "2. Add photos to [cyan]source/[/]\n" +
                                "3. Run [cyan]revela generate[/]")
                .WithInfoStyle();
            AnsiConsole.Write(panel);

            return 0;
        }
        catch (Exception ex)
        {
            LogError(ex);
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task InitializePluginConfigAsync(IPageTemplate template, CancellationToken cancellationToken)
    {
        var filename = $"{template.ConfigSectionName}.json";
        var configPath = Path.Combine("plugins", filename);

        // Build config with defaults
        var config = BuildConfigObject(template);

        // Serialize and write
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);

        LogPluginConfigCreated(logger, configPath, template.DisplayName);
        AnsiConsole.MarkupLine($"[green]✓[/] Created [cyan]{configPath}[/] ({template.DisplayName})");
    }

    private static Dictionary<string, object> BuildConfigObject(IPageTemplate template)
    {
        var root = new Dictionary<string, object>();
        var config = new Dictionary<string, object>();

        foreach (var property in template.ConfigProperties)
        {
            if (property.DefaultValue == null || property.ConfigKey == null)
            {
                continue;
            }

            SetNestedValue(config, property.ConfigKey, property.DefaultValue);
        }

        root[template.ConfigSectionName] = config;
        return root;
    }

    private static void SetNestedValue(Dictionary<string, object> root, string key, object value)
    {
        var segments = key.Split('.');

        if (segments.Length == 1)
        {
            root[key] = value;
            return;
        }

        var current = root;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (!current.TryGetValue(segment, out var nested))
            {
                nested = new Dictionary<string, object>();
                current[segment] = nested;
            }

            if (nested is Dictionary<string, object> dict)
            {
                current = dict;
            }
            else
            {
                return;
            }
        }

        current[segments[^1]] = value;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Initializing project '{ProjectName}'")]
    private partial void LogInitializingProject(string projectName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created plugin config: {Path} ({TemplateName})")]
    private static partial void LogPluginConfigCreated(ILogger logger, string path, string templateName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to initialize project")]
    private partial void LogError(Exception exception);
}


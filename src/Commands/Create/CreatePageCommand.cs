using System.CommandLine;
using System.Globalization;
using System.Reflection;
using System.Text;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;
using Spectara.Revela.Sdk.Services;

using Spectre.Console;

namespace Spectara.Revela.Commands.Create;

/// <summary>
/// Command for creating _index.revela files from page templates.
/// </summary>
/// <remarks>
/// <para>
/// Discovers all <see cref="IPageTemplate"/> instances via DI and creates subcommands dynamically.
/// Each template becomes a subcommand under 'create page'.
/// </para>
/// <para>
/// Usage: revela create page &lt;template&gt; &lt;path&gt; [options]
/// </para>
/// <para>
/// The path argument is required. Options are dynamically generated from template properties.
/// </para>
/// </remarks>
public sealed partial class CreatePageCommand(
    ILogger<CreatePageCommand> logger,
    IPathResolver pathResolver,
    IEnumerable<IPageTemplate> templates)
{
    /// <summary>
    /// Creates the 'create page' command with template subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("page", "Create _index.revela file from template");

        foreach (var template in templates)
        {
            var templateCmd = CreateTemplateCommand(template);
            command.Subcommands.Add(templateCmd);
        }

        return command;
    }

    private Command CreateTemplateCommand(IPageTemplate template)
    {
        var command = new Command(template.Name, template.Description);

        // Path argument (optional - triggers interactive mode when missing)
        var pathArgument = new Argument<string?>("path")
        {
            Description = "Target directory for _index.revela",
            Arity = ArgumentArity.ZeroOrOne
        };
        command.Arguments.Add(pathArgument);

        var optionsMap = new Dictionary<TemplateProperty, Option>();

        // Dynamically create options from template's PageProperties (excluding path)
        foreach (var property in template.PageProperties.Where(p => p.Name != "path"))
        {
            var option = CreateOptionFromProperty(property);
            command.Options.Add(option);
            optionsMap[property] = option;
        }

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var path = parseResult.GetValue(pathArgument);

            // Interactive mode if path not provided
            if (string.IsNullOrEmpty(path))
            {
                return await ExecuteInteractiveAsync(template, cancellationToken).ConfigureAwait(false);
            }

            var values = ExtractValues(parseResult, optionsMap);
            return await GeneratePageAsync(template, path, values, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Executes interactive mode for page creation.
    /// </summary>
    private async Task<int> ExecuteInteractiveAsync(IPageTemplate template, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule($"[cyan]Create {template.DisplayName}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Prompt for path (empty = source root)
        var path = AnsiConsole.Prompt(
            new TextPrompt<string>("[cyan]Directory path[/] [dim](relative to source/, empty = source root)[/]:")
                .AllowEmpty());

        // Normalize empty path to current directory
        if (string.IsNullOrWhiteSpace(path))
        {
            path = ".";
        }

        var values = new Dictionary<string, object?>();

        // Prompt for each property
        foreach (var property in template.PageProperties)
        {
            var value = PromptForProperty(property);
            values[property.Name] = value;
        }

        // Preview
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[cyan]Preview[/]").RuleStyle("grey"));
        var frontmatter = GenerateFrontmatter(template, values);
        var panel = new Panel(frontmatter.Trim())
            .Header("[bold]_index.revela[/]")
            .BorderColor(Color.Grey);
        panel.Padding = new Padding(1, 0, 1, 0);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Confirm
        var confirmed = await AnsiConsole.ConfirmAsync("[cyan]Create this page?[/]", defaultValue: true, cancellationToken)
            .ConfigureAwait(false);
        if (!confirmed)
        {
            AnsiConsole.MarkupLine($"{OutputMarkers.Warning} Cancelled");
            return 1;
        }

        return await GeneratePageAsync(template, path, values, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Prompts for a single property value based on its type.
    /// </summary>
    private static object? PromptForProperty(TemplateProperty property)
    {
        var prompt = $"[cyan]{property.Name}[/]";
        if (!string.IsNullOrEmpty(property.Description))
        {
            prompt += $" [dim]({property.Description})[/]";
        }

        // Boolean properties
        if (property.Type == typeof(bool))
        {
            var defaultBool = property.DefaultValue is bool b && b;
            return AnsiConsole.Confirm(prompt, defaultValue: defaultBool);
        }

        // String properties with selection options (like sort)
        if (property.Name == "sort")
        {
            return PromptForSort();
        }

        // Regular string properties
        var defaultString = property.DefaultValue?.ToString() ?? "";
        var textPrompt = new TextPrompt<string>($"{prompt}:")
            .AllowEmpty();

        if (!string.IsNullOrEmpty(defaultString))
        {
            textPrompt.DefaultValue(defaultString);
        }

        var result = AnsiConsole.Prompt(textPrompt);
        return string.IsNullOrEmpty(result) ? null : result;
    }

    /// <summary>
    /// Prompts for sort field selection with common options.
    /// </summary>
    private static string? PromptForSort()
    {
        var choices = new (string Value, string Display)[]
        {
            ("", "[dim]None (use global default)[/]"),
            ("dateTaken:desc", "Date Taken (newest first)"),
            ("dateTaken:asc", "Date Taken (oldest first)"),
            ("filename:asc", "Filename (A → Z)"),
            ("filename:desc", "Filename (Z → A)"),
            ("exif.raw.Rating:desc", "Rating (highest first)"),
            ("exif.focalLength:asc", "Focal Length (wide to tele)"),
            ("custom", "[dim]Custom field...[/]")
        };

        var (selected, _) = AnsiConsole.Prompt(
            new SelectionPrompt<(string Value, string Display)>()
                .Title("[cyan]sort[/] [dim](image sort order for this gallery)[/]")
                .AddChoices(choices)
                .UseConverter(c => c.Display));

        if (selected == "custom")
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>("[cyan]Enter sort field[/] [dim](e.g., exif.iso:desc)[/]:")
                    .AllowEmpty());
        }

        return string.IsNullOrEmpty(selected) ? null : selected;
    }

    private static Option CreateOptionFromProperty(TemplateProperty property)
    {
        // Create Option<T> dynamically based on property type
        var optionType = typeof(Option<>).MakeGenericType(property.Type);

        // System.CommandLine 2.0: Constructor Option<T>(string name, params string[] aliases)
        // The name is the first alias (e.g., "--title"), additional aliases (e.g., "-t") are optional
        Option option;
        if (property.Aliases.Count == 1)
        {
            // Only name, no aliases: use single-parameter constructor
            option = (Option)Activator.CreateInstance(optionType, property.Aliases[0])!;
        }
        else
        {
            // Name + aliases: pass all remaining aliases as params array
            var aliases = property.Aliases.Skip(1).ToArray();
            option = (Option)Activator.CreateInstance(optionType, property.Aliases[0], aliases)!;
        }

        // Set Description property via reflection
        var descProperty = optionType.GetProperty("Description");
        descProperty?.SetValue(option, property.Description);

        // Set DefaultValue if provided
        if (property.DefaultValue != null)
        {
            var defaultValueProperty = optionType.GetProperty("DefaultValue");
            defaultValueProperty?.SetValue(option, property.DefaultValue);
        }

        return option;
    }

    private static Dictionary<string, object?> ExtractValues(
        ParseResult parseResult,
        Dictionary<TemplateProperty, Option> optionsMap)
    {
        var values = new Dictionary<string, object?>();

        foreach (var (property, option) in optionsMap)
        {
            // Use reflection to call generic GetValue<T>(Option<T>) method
            var getValueMethod = typeof(ParseResult)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "GetValue" && m.IsGenericMethodDefinition)
                .FirstOrDefault(m =>
                {
                    var parameters = m.GetParameters();
                    return parameters.Length == 1 &&
                           parameters[0].ParameterType.IsGenericType &&
                           parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(Option<>);
                });

            if (getValueMethod != null)
            {
                var genericMethod = getValueMethod.MakeGenericMethod(property.Type);
                var value = genericMethod.Invoke(parseResult, [option]);
                values[property.Name] = value;
            }
        }

        return values;
    }

    private async Task<int> GeneratePageAsync(
        IPageTemplate template,
        string path,
        Dictionary<string, object?> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Combine with source directory if relative
        // Pages are always created inside source/ directory
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(pathResolver.SourcePath, path);

        // Ensure directory exists
        Directory.CreateDirectory(fullPath);

        var revelaPath = Path.Combine(fullPath, "_index.revela");

        // Check if file already exists
        if (File.Exists(revelaPath))
        {
            ErrorPanels.ShowFileExistsError(revelaPath, "Use a different path or delete the existing file.");
            return 1;
        }

        // Generate frontmatter
        var frontmatter = GenerateFrontmatter(template, values);

        // Write file
        await File.WriteAllTextAsync(revelaPath, frontmatter, cancellationToken).ConfigureAwait(false);

        LogPageCreated(logger, revelaPath, template.DisplayName);
        AnsiConsole.MarkupLine($"{OutputMarkers.Success} Created [cyan]{revelaPath}[/]");

        return 0;
    }

    private static string GenerateFrontmatter(IPageTemplate template, Dictionary<string, object?> values)
    {
        var sb = new StringBuilder();
        sb.AppendLine("+++");

        // Write properties that appear in frontmatter
        foreach (var property in template.PageProperties.Where(p => p.FrontmatterKey != null))
        {
            var userValue = values.TryGetValue(property.Name, out var val) ? val : null;

            // Check if user explicitly provided a non-default value
            var hasUserValue = userValue != null && !Equals(userValue, property.DefaultValue);

            if (hasUserValue)
            {
                // User provided a value - write it
                var formattedValue = FormatValue(property, userValue);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} = {1}", property.FrontmatterKey, formattedValue));
            }
            else if (property.DefaultValue != null && ShouldWriteDefault(property))
            {
                // No user value - write default only for certain types
                var formattedDefault = FormatValue(property, property.DefaultValue);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} = {1}", property.FrontmatterKey, formattedDefault));
            }
            // Skip: null defaults (optional fields like sort, slug) and false booleans
        }

        // Template field (only if specified)
        if (!string.IsNullOrEmpty(template.TemplateName))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "template = \"{0}\"", template.TemplateName));
        }

        sb.AppendLine("+++");

        // Add default body content if template provides one
        if (!string.IsNullOrEmpty(template.DefaultBody))
        {
            sb.AppendLine(template.DefaultBody);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines if a default value should be written to frontmatter.
    /// </summary>
    /// <remarks>
    /// <para>Rules:</para>
    /// <list type="bullet">
    /// <item><c>bool</c> with default <c>false</c>: skip (e.g., hidden)</item>
    /// <item><c>string</c> with default <c>null</c>: skip (optional fields)</item>
    /// <item><c>string</c> with default value: write (e.g., title = "Gallery")</item>
    /// </list>
    /// </remarks>
    private static bool ShouldWriteDefault(TemplateProperty property)
    {
        // Skip boolean defaults that are false (hidden, featured, etc.)
        if (property.Type == typeof(bool) && Equals(property.DefaultValue, false))
        {
            return false;
        }

        // Write string defaults (like title = "Gallery")
        return true;
    }

    private static string FormatValue(TemplateProperty property, object? value)
    {
        // Use custom formatter if provided
        if (property.FormatValue != null)
        {
            return property.FormatValue(value);
        }

        // Default formatting based on type
        return value switch
        {
            null => "\"\"",
            string str => $"\"{str}\"",
#pragma warning disable CA1308 // Scriban requires lowercase boolean values (true/false)
            bool b => b.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
#pragma warning restore CA1308
            int i => i.ToString(CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "\"\""
        };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {Path} from template '{TemplateName}'")]
    private static partial void LogPageCreated(ILogger logger, string path, string templateName);
}

using System.CommandLine;
using System.Globalization;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Output;

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
    IOptions<ProjectEnvironment> projectEnvironment,
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

        // Fixed path argument (required)
        var pathArgument = new Argument<string>("path")
        {
            Description = "Target directory for _index.revela"
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
            var values = ExtractValues(parseResult, optionsMap);
            return await GeneratePageAsync(template, path!, values, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    private static Option CreateOptionFromProperty(TemplateProperty property)
    {
        // Create Option<T> dynamically based on property type
        var optionType = typeof(Option<>).MakeGenericType(property.Type);

        // System.CommandLine 2.0: Constructor takes name and optional aliases
#pragma warning disable IDE0055 // Fix formatting - editorconfig rule unclear for this construct
        var option = (Option)Activator.CreateInstance(
            optionType,
            property.Aliases[0],
            property.Aliases.Count > 1 ? property.Aliases[1] : null)!;
#pragma warning restore IDE0055

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

        // Combine with project path if relative
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(projectEnvironment.Value.Path, path);

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
            var hasUserValue = userValue != null && !Equals(userValue, property.DefaultValue);

            if (hasUserValue)
            {
                // User provided a value - write it active
                var formattedValue = FormatValue(property, userValue);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} = {1}", property.FrontmatterKey, formattedValue));
            }
            else if (property.DefaultValue != null)
            {
                // No user value - write default as active (not commented)
                var formattedDefault = FormatValue(property, property.DefaultValue);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} = {1}", property.FrontmatterKey, formattedDefault));
            }
        }

        // Template field (only if specified)
        if (!string.IsNullOrEmpty(template.TemplateName))
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "template = \"{0}\"", template.TemplateName));
        }

        // Template-specific data sources
        if (template.Name == "statistics")
        {
            sb.AppendLine("data = { statistics: \"statistics.json\" }");
        }

        sb.AppendLine("+++");
        return sb.ToString();
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

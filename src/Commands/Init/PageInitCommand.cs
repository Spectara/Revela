using System.CommandLine;
using System.Globalization;
using System.Reflection;
using System.Text;
using Spectara.Revela.Commands.Init.Abstractions;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Command for creating _index.revela files from page templates.
/// </summary>
/// <remarks>
/// Discovers all <see cref="IPageTemplate"/> instances via DI and creates subcommands dynamically.
/// Properties are exposed as CLI options with user values active and defaults commented in frontmatter.
/// </remarks>
public sealed partial class PageInitCommand(
    ILogger<PageInitCommand> logger,
    IEnumerable<IPageTemplate> templates)
{
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

        var optionsMap = new Dictionary<TemplateProperty, Option>();

        // Dynamically create options from template's PageProperties
        foreach (var property in template.PageProperties)
        {
            var option = CreateOptionFromProperty(property);
            command.Options.Add(option);
            optionsMap[property] = option;
        }

        command.SetAction(parseResult =>
        {
            var values = ExtractValues(parseResult, optionsMap);
            GeneratePage(template, values);
            return 0;
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

    private void GeneratePage(IPageTemplate template, Dictionary<string, object?> values)
    {
        // Determine output path (from --path option or property default)
        var pathProperty = template.PageProperties.FirstOrDefault(p => p.Name == "path");
        var outputPath = values.TryGetValue("path", out var pathValue) && pathValue is string strPath
            ? strPath
            : pathProperty?.DefaultValue as string ?? "source";

        // Ensure directory exists
        Directory.CreateDirectory(outputPath);

        var revelaPath = Path.Combine(outputPath, "_index.revela");

        // Generate frontmatter
        var frontmatter = GenerateFrontmatter(template, values);

        // Write file
        File.WriteAllText(revelaPath, frontmatter);

        LogPageCreated(logger, revelaPath, template.DisplayName);
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
            else
            {
                // No user value - write default as comment
                var formattedDefault = FormatValue(property, property.DefaultValue);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "# {0} = {1}", property.FrontmatterKey, formattedDefault));
            }
        }

        // Hardcoded template field
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "template = \"{0}\"", template.TemplateName));

        // Hardcoded data sources (template-specific)
        // For statistics: data = { statistics: "statistics.json" }
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

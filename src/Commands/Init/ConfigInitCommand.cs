using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using Spectara.Revela.Commands.Init.Abstractions;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Command for creating plugin configuration JSON files.
/// </summary>
/// <remarks>
/// Discovers all <see cref="IPageTemplate"/> instances and creates config initialization commands.
/// Supports dot notation for nested configuration (e.g., "Deploy.Host" → {"Deploy": {"Host": "..."}}).
/// </remarks>
public sealed partial class ConfigInitCommand(
    ILogger<ConfigInitCommand> logger,
    IEnumerable<IPageTemplate> templates)
{
    public Command Create()
    {
        var command = new Command("config", "Create plugin configuration file");

        foreach (var template in templates.Where(t => t.ConfigProperties.Count > 0))
        {
            var configCmd = CreateConfigCommand(template);
            command.Subcommands.Add(configCmd);
        }

        return command;
    }

    private Command CreateConfigCommand(IPageTemplate template)
    {
        var command = new Command(template.Name, $"Create {template.DisplayName} configuration");

        var optionsMap = new Dictionary<TemplateProperty, Option>();

        // Dynamically create options from template's ConfigProperties
        foreach (var property in template.ConfigProperties)
        {
            var option = CreateOptionFromProperty(property);
            command.Options.Add(option);
            optionsMap[property] = option;
        }

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var values = ExtractValues(parseResult, optionsMap);
            await GenerateConfigAsync(template, values, cancellationToken).ConfigureAwait(false);
            return 0;
        });

        return command;
    }

    private static Option CreateOptionFromProperty(TemplateProperty property)
    {
        // Create Option<T> dynamically based on property type
        var optionType = typeof(Option<>).MakeGenericType(property.Type);

        // System.CommandLine 2.0: Constructor takes name and optional aliases
        var option = (Option)Activator.CreateInstance(
            optionType,
            property.Aliases[0],  // First alias (--long)
            property.Aliases.Count > 1 ? property.Aliases[1] : null  // Optional short alias (-s)
        )!;

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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private async Task GenerateConfigAsync(IPageTemplate template, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Ensure plugins directory exists
        const string pluginsDir = "plugins";
        Directory.CreateDirectory(pluginsDir);

        // Auto-detect filename from ConfigSectionName
        var filename = $"{template.ConfigSectionName}.json";
        var configPath = Path.Combine(pluginsDir, filename);

        // Build configuration object
        var configObj = BuildConfigObject(template, values);

        // Serialize to JSON
        var json = JsonSerializer.Serialize(configObj, JsonOptions);

        // Write file
        await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);

        LogConfigCreated(logger, configPath, template.DisplayName);
    }

    private Dictionary<string, object> BuildConfigObject(IPageTemplate template, Dictionary<string, object?> values)
    {
        // Root object with ConfigSectionName as key
        var root = new Dictionary<string, object>();
        var config = new Dictionary<string, object>();

        foreach (var property in template.ConfigProperties)
        {
            var userValue = values.TryGetValue(property.Name, out var val) ? val : null;
            var finalValue = userValue ?? property.DefaultValue;

            if (finalValue == null)
            {
                continue;
            }

            // Support dot notation for nested objects
            if (property.ConfigKey != null)
            {
                SetNestedValue(config, property.ConfigKey, finalValue);
            }
        }

        root[template.ConfigSectionName] = config;
        return root;
    }

    private void SetNestedValue(Dictionary<string, object> root, string key, object value)
    {
        // Split by dots for nested objects (e.g., "Deploy.Host")
        var segments = key.Split('.');

        if (segments.Length == 1)
        {
            // Simple key
            root[key] = value;
            return;
        }

        // Navigate/create nested structure
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
                // Conflict: existing value is not a dictionary
                LogNestedKeyConflict(logger, key, segment);
                return;
            }
        }

        // Set final value
        current[segments[^1]] = value;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {Path} for {TemplateName}")]
    private static partial void LogConfigCreated(ILogger logger, string path, string templateName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Nested key conflict for '{Key}' at segment '{Segment}' - skipping")]
    private static partial void LogNestedKeyConflict(ILogger logger, string key, string segment);
}

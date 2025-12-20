using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

namespace Spectara.Revela.Commands.Init;

/// <summary>
/// Parent command for initialization operations.
/// </summary>
/// <remarks>
/// <para>
/// Subcommands:
/// - project: Initialize a new Revela project
/// - all: Initialize project with all plugin configurations
/// - [plugin]: Create plugin configuration file (flattened)
/// </para>
/// <para>
/// Plugin config commands are created dynamically from IPageTemplate instances
/// that have ConfigProperties defined.
/// </para>
/// <para>
/// Theme initialization has been moved to 'revela theme extract'.
/// </para>
/// </remarks>
public sealed partial class InitCommand(
    ILogger<InitCommand> logger,
    InitProjectCommand projectCommand,
    InitAllCommand allCommand,
    IEnumerable<IPageTemplate> templates)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the 'init' command with subcommands.
    /// </summary>
    public Command Create()
    {
        var command = new Command("init", "Initialize project or plugin configurations");

        // Add fixed subcommands
        command.Subcommands.Add(projectCommand.Create());
        command.Subcommands.Add(allCommand.Create());

        // Add flattened plugin config commands (was: init config <plugin>)
        foreach (var template in templates.Where(t => t.ConfigProperties.Count > 0))
        {
            var configCmd = CreatePluginConfigCommand(template);
            command.Subcommands.Add(configCmd);
        }

        return command;
    }

    private Command CreatePluginConfigCommand(IPageTemplate template)
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
        var optionType = typeof(Option<>).MakeGenericType(property.Type);

#pragma warning disable IDE0055 // Fix formatting
        var option = (Option)Activator.CreateInstance(
            optionType,
            property.Aliases[0],
            property.Aliases.Count > 1 ? property.Aliases[1] : null)!;
#pragma warning restore IDE0055

        var descProperty = optionType.GetProperty("Description");
        descProperty?.SetValue(option, property.Description);

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

    private async Task GenerateConfigAsync(
        IPageTemplate template,
        Dictionary<string, object?> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        const string pluginsDir = "plugins";
        Directory.CreateDirectory(pluginsDir);

        var filename = $"{template.ConfigSectionName}.json";
        var configPath = Path.Combine(pluginsDir, filename);

        var configObj = BuildConfigObject(template, values);
        var json = JsonSerializer.Serialize(configObj, JsonOptions);

        await File.WriteAllTextAsync(configPath, json, cancellationToken).ConfigureAwait(false);

        LogConfigCreated(logger, configPath, template.DisplayName);

        // Show hint about config command if available
        if (template.HasConfigCommand)
        {
            AnsiConsole.MarkupLine($"[dim]Tip: Use [cyan]revela config {template.Name}[/] to modify settings interactively.[/]");
        }
    }

    private static Dictionary<string, object> BuildConfigObject(
        IPageTemplate template,
        Dictionary<string, object?> values)
    {
        var root = new Dictionary<string, object>();
        var config = new Dictionary<string, object>();

        foreach (var property in template.ConfigProperties)
        {
            var userValue = values.TryGetValue(property.Name, out var val) ? val : null;
            var finalValue = userValue ?? property.DefaultValue;

            if (finalValue == null || property.ConfigKey == null)
            {
                continue;
            }

            SetNestedValue(config, property.ConfigKey, finalValue);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "Created {Path} for {TemplateName}")]
    private static partial void LogConfigCreated(ILogger logger, string path, string templateName);
}

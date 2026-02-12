using System.CommandLine;

using Spectre.Console;

namespace Spectara.Revela.Cli.Hosting;

/// <summary>
/// Builds interactive prompts for command options and arguments.
/// </summary>
internal sealed partial class CommandPromptBuilder(ILogger<CommandPromptBuilder> logger)
{
    /// <summary>
    /// Prompts the user for all arguments of a command.
    /// </summary>
    /// <param name="command">The command to prompt arguments for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping arguments to their values.</returns>
    public static Task<Dictionary<Argument, object?>> PromptForArgumentsAsync(
        Command command,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var results = new Dictionary<Argument, object?>();

        foreach (var argument in command.Arguments)
        {
            // Skip hidden arguments
            if (argument.Hidden)
            {
                continue;
            }

            // Skip optional arguments (ZeroOrOne, ZeroOrMore)
            // These commands handle their own interactive selection
            if (argument.Arity.MinimumNumberOfValues == 0)
            {
                continue;
            }

            var value = PromptForArgument(argument);
            results[argument] = value;
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// Prompts the user for boolean behavior options of a command.
    /// </summary>
    /// <remarks>
    /// Only prompts for visible bool options (behavior flags like --force, --dry-run).
    /// Config overrides (nullable types like string?, int?) are never prompted.
    /// If no boolean options exist, returns empty dictionary without prompting.
    /// </remarks>
    /// <param name="command">The command to prompt options for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping options to their values.</returns>
    public static Task<Dictionary<Option, object?>> PromptForOptionsAsync(
        Command command,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var results = new Dictionary<Option, object?>();

        // Skip options prompt if command has only optional arguments
        // These commands (like plugin install, theme install) have their own interactive flow
        var hasRequiredArguments = command.Arguments.Any(a => !a.Hidden && a.Arity.MinimumNumberOfValues > 0);
        if (!hasRequiredArguments && command.Arguments.Count > 0)
        {
            return Task.FromResult(results);
        }

        // Collect only visible bool options (behavior flags)
        var boolOptions = command.Options
            .Where(o => !o.Hidden && o.ValueType == typeof(bool))
            .ToList();

        // No bool options → execute directly without prompts
        if (boolOptions.Count == 0)
        {
            return Task.FromResult(results);
        }

        // Use multi-selection prompt for behavior flags
        var choices = boolOptions
            .Select(o => new
            {
                Option = o,
                Display = FormatOptionDisplay(o)
            })
            .ToList();

        var selected = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("[blue]Options[/] [dim](Space to toggle, Enter to confirm)[/]")
                .NotRequired()
                .InstructionsText("[dim](All options default to off)[/]")
                .AddChoices(choices.Select(c => c.Display)));

        // Map selections back to options
        foreach (var choice in choices)
        {
            var isSelected = selected.Contains(choice.Display);
            results[choice.Option] = isSelected;
        }

        return Task.FromResult(results);
    }

    /// <summary>
    /// Formats an option for display in the multi-selection prompt.
    /// </summary>
    private static string FormatOptionDisplay(Option option)
    {
        var name = option.Name;
        var description = option.Description;

        return string.IsNullOrWhiteSpace(description)
            ? name
            : $"{name} [dim]({description})[/]";
    }

    /// <summary>
    /// Builds an arguments array from collected values.
    /// </summary>
    /// <param name="commandPath">The command path (e.g., ["source", "onedrive", "download"]).</param>
    /// <param name="arguments">The collected argument values.</param>
    /// <param name="options">The collected option values.</param>
    /// <returns>The arguments array for Parse/Invoke.</returns>
    public string[] BuildArgsArray(
        IReadOnlyList<string> commandPath,
        IReadOnlyDictionary<Argument, object?> arguments,
        IReadOnlyDictionary<Option, object?> options)
    {
        var args = new List<string>(commandPath);

        // Add positional arguments first
        foreach (var (argument, value) in arguments)
        {
            if (value is null)
            {
                continue;
            }

            if (value is string[] stringArray)
            {
                args.AddRange(stringArray);
            }
            else
            {
                args.Add(value.ToString()!);
            }
        }

        // Add options
        foreach (var (option, value) in options)
        {
            if (value is null)
            {
                continue;
            }

            var optionName = option.Name;

            // Boolean flags
            if (value is bool boolValue)
            {
                if (boolValue)
                {
                    args.Add(optionName);
                }

                continue;
            }

            // String arrays (multiple values)
            if (value is string[] arrayValue)
            {
                foreach (var item in arrayValue)
                {
                    args.Add(optionName);
                    args.Add(item);
                }

                continue;
            }

            // Regular values
            args.Add(optionName);
            args.Add(value.ToString()!);
        }

        var builtArgs = string.Join(" ", args);
        LogBuiltArgs(logger, builtArgs);
        return [.. args];
    }

    private static object? PromptForArgument(Argument argument)
    {
        var name = argument.Name;
        var description = argument.Description ?? string.Empty;
        var isRequired = argument.Arity.MinimumNumberOfValues > 0;
        var valueType = argument.ValueType;

        var title = BuildPromptTitle(name, description, isRequired);

        return PromptByType(title, valueType, isRequired);
    }

    private static string BuildPromptTitle(string name, string description, bool isRequired)
    {
        var title = name;
        if (!string.IsNullOrWhiteSpace(description))
        {
            title = $"{name} [dim]({description})[/]";
        }

        if (!isRequired)
        {
            title = $"{title} [dim](optional, press Enter to skip)[/]";
        }

        return title;
    }

    private static object? PromptByType(string title, Type valueType, bool isRequired)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        // Boolean
        if (underlyingType == typeof(bool))
        {
            return AnsiConsole.Confirm(title, defaultValue: false);
        }

        // String array
        if (underlyingType == typeof(string[]))
        {
            return PromptStringArray(title);
        }

        // Integer
        if (underlyingType == typeof(int))
        {
            return PromptInt(title, isRequired);
        }

        // String (default)
        return PromptString(title, isRequired);
    }

    private static string? PromptString(string title, bool isRequired)
    {
        if (isRequired)
        {
            return AnsiConsole.Prompt(
                new TextPrompt<string>(title)
                    .ValidationErrorMessage("[red]Value is required[/]")
                    .Validate(input => !string.IsNullOrWhiteSpace(input)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("[red]Value cannot be empty[/]")));
        }

        var result = AnsiConsole.Prompt(
            new TextPrompt<string>(title)
                .AllowEmpty());

        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static int? PromptInt(string title, bool isRequired)
    {
        if (isRequired)
        {
            return AnsiConsole.Prompt(new TextPrompt<int>(title));
        }

        var result = AnsiConsole.Prompt(
            new TextPrompt<string>(title)
                .AllowEmpty());

        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        return int.TryParse(result, out var intValue) ? intValue : null;
    }

    private static string[] PromptStringArray(string title)
    {
        var values = new List<string>();

        AnsiConsole.MarkupLine($"[blue]{title}[/]");
        AnsiConsole.MarkupLine("[dim]Enter values one by one. Leave empty to finish.[/]");

        while (true)
        {
            var prompt = values.Count == 0
                ? "Value"
                : $"Value {values.Count + 1}";

            var value = AnsiConsole.Prompt(
                new TextPrompt<string>($"  {prompt}:")
                    .AllowEmpty());

            if (string.IsNullOrWhiteSpace(value))
            {
                break;
            }

            values.Add(value);

            if (!AnsiConsole.Confirm("Add another?", defaultValue: false))
            {
                break;
            }
        }

        return [.. values];
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Built args: {Args}")]
    private static partial void LogBuiltArgs(ILogger logger, string args);
}

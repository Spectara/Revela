using System.CommandLine;

using Spectre.Console;

using Spectara.Revela.Core.Services;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Command to extract a theme to the local themes folder for customization.
/// </summary>
/// <remarks>
/// Usage:
///   revela theme extract Expose           → themes/Expose/
///   revela theme extract Expose MyTheme   → themes/MyTheme/
/// </remarks>
public sealed partial class ThemeExtractCommand(
    IThemeResolver themeResolver,
    ILogger<ThemeExtractCommand> logger)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    /// <returns>The configured extract command.</returns>
    public Command Create()
    {
        var sourceArg = new Argument<string>("source")
        {
            Description = "Name of the theme to extract"
        };

        var targetArg = new Argument<string?>("target")
        {
            Description = "Name for the extracted theme (optional)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Overwrite existing theme folder"
        };

        var command = new Command("extract", "Extract a theme to themes/ folder for customization");
        command.Arguments.Add(sourceArg);
        command.Arguments.Add(targetArg);
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg)!;
            var target = parseResult.GetValue(targetArg);
            var force = parseResult.GetValue(forceOption);

            return await ExecuteAsync(source, target, force, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(
        string sourceName,
        string? targetName,
        bool force,
        CancellationToken cancellationToken)
    {
        var projectPath = Environment.CurrentDirectory;
        var themeName = targetName ?? sourceName;

        // Resolve source theme
        var sourceTheme = themeResolver.Resolve(sourceName, projectPath);
        if (sourceTheme is null)
        {
            AnsiConsole.MarkupLine($"[red]Theme '{EscapeMarkup(sourceName)}' not found.[/]");
            AnsiConsole.MarkupLine("Use [blue]revela theme list[/] to see available themes.");
            return 1;
        }

        // Determine target path
        var themesFolder = Path.Combine(projectPath, "themes");
        var targetPath = Path.Combine(themesFolder, themeName);

        // Check if target exists
        if (Directory.Exists(targetPath))
        {
            if (!force)
            {
                AnsiConsole.MarkupLine($"[red]Theme folder '{EscapeMarkup(themeName)}' already exists.[/]");
                AnsiConsole.MarkupLine("Use [blue]--force[/] to overwrite.");
                return 1;
            }

            LogOverwriting(logger, targetPath);
            Directory.Delete(targetPath, recursive: true);
        }

        // Create themes folder if needed
        Directory.CreateDirectory(themesFolder);

        // Extract theme
        LogExtracting(logger, sourceName, targetPath);

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(
                $"Extracting theme '{sourceName}'...",
                async _ => await sourceTheme.ExtractToAsync(targetPath, cancellationToken));

        // Update theme.json with new name if different
        if (targetName is not null && !targetName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
        {
            UpdateThemeName(targetPath, targetName);
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Theme extracted to [blue]themes/{EscapeMarkup(themeName)}/[/]");
        AnsiConsole.MarkupLine("\nYou can now customize the theme and use it in your project.");

        return 0;
    }

    private static void UpdateThemeName(string themePath, string newName)
    {
        var themeJsonPath = Path.Combine(themePath, "theme.json");
        if (!File.Exists(themeJsonPath))
        {
            return;
        }

        var json = File.ReadAllText(themeJsonPath);

        // Simple regex replacement for "name": "..."
        var pattern = "\"name\"\\s*:\\s*\"[^\"]*\"";
        var replacement = $"\"name\": \"{newName}\"";
        var updatedJson = System.Text.RegularExpressions.Regex.Replace(json, pattern, replacement);

        File.WriteAllText(themeJsonPath, updatedJson);
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting theme '{ThemeName}' to {TargetPath}")]
    private static partial void LogExtracting(ILogger logger, string themeName, string targetPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overwriting existing theme folder: {Path}")]
    private static partial void LogOverwriting(ILogger logger, string path);
}

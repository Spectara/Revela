using System.CommandLine;
using Spectara.Revela.Core.Services;
using Spectre.Console;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Command to extract a theme to the local themes folder for customization.
/// </summary>
/// <remarks>
/// Usage:
///   revela theme extract Lumina           → themes/Lumina/
///   revela theme extract Lumina MyTheme   → themes/MyTheme/
///   revela theme extract Lumina --extensions → also extracts Theme.Lumina.* extensions
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

        var extensionsOption = new Option<bool>("--extensions", "-e")
        {
            Description = "Also extract matching theme extensions (Theme.{Name}.* packages)"
        };

        var command = new Command("extract", "Extract a theme to themes/ folder for customization");
        command.Arguments.Add(sourceArg);
        command.Arguments.Add(targetArg);
        command.Options.Add(forceOption);
        command.Options.Add(extensionsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg)!;
            var target = parseResult.GetValue(targetArg);
            var force = parseResult.GetValue(forceOption);
            var includeExtensions = parseResult.GetValue(extensionsOption);

            return await ExecuteAsync(source, target, force, includeExtensions, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteAsync(
        string sourceName,
        string? targetName,
        bool force,
        bool includeExtensions,
        CancellationToken cancellationToken)
    {
        var projectPath = Environment.CurrentDirectory;
        var themeName = targetName ?? sourceName;

        // Determine target path first
        var themesFolder = Path.Combine(projectPath, "themes");
        var targetPath = Path.Combine(themesFolder, themeName);

        // For extract: always prefer installed theme (user wants fresh copy from original)
        // Fall back to local only if installed theme not found
        var sourceTheme = themeResolver.ResolveInstalled(sourceName)
                          ?? themeResolver.Resolve(sourceName, projectPath);

        if (sourceTheme is null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Theme [yellow]'{EscapeMarkup(sourceName)}'[/] not found.");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Run [cyan]revela theme list[/] to see available themes.");
            return 1;
        }

        // Check if target exists
        if (Directory.Exists(targetPath))
        {
            if (!force)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Theme folder [yellow]'{EscapeMarkup(themeName)}'[/] already exists.");
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("Use [cyan]--force[/] to overwrite existing theme.");
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

        // Extract matching extensions if requested
        var extractedExtensions = new List<string>();
        if (includeExtensions)
        {
            var extensions = themeResolver.GetExtensions(sourceName);
            if (extensions.Count > 0)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync(
                        "Extracting theme extensions...",
                        async _ =>
                        {
                            foreach (var extension in extensions)
                            {
                                var extensionFolder = Path.Combine(targetPath, "Extensions", extension.PartialPrefix);

                                if (Directory.Exists(extensionFolder))
                                {
                                    if (force)
                                    {
                                        Directory.Delete(extensionFolder, recursive: true);
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                }

                                Directory.CreateDirectory(extensionFolder);
                                await extension.ExtractToAsync(extensionFolder, cancellationToken);
                                extractedExtensions.Add(extension.PartialPrefix);
                                LogExtractingExtension(logger, extension.Metadata.Name, extensionFolder);
                            }
                        });
            }
        }

        // Success panel
        var extensionsInfo = extractedExtensions.Count > 0
            ? $"\n[bold]Extensions:[/] {string.Join(", ", extractedExtensions)}"
            : "";

        var panel = new Panel($"[green]✨ Theme '{EscapeMarkup(themeName)}' extracted![/]\n\n" +
                            $"[bold]Location:[/] [cyan]themes/{EscapeMarkup(themeName)}/[/]{extensionsInfo}\n\n" +
                            "[bold]Next steps:[/]\n" +
                            $"1. Edit [cyan]themes/{EscapeMarkup(themeName)}/[/] to customize\n" +
                            "2. Run [cyan]revela generate[/] to see changes\n" +
                            "3. Your local theme takes priority over installed themes")
        {
            Header = new PanelHeader("[bold green]Success[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting extension '{ExtensionName}' to {TargetPath}")]
    private static partial void LogExtractingExtension(ILogger logger, string extensionName, string targetPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Overwriting existing theme folder: {Path}")]
    private static partial void LogOverwriting(ILogger logger, string path);
}

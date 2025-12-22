using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Commands.Theme;

/// <summary>
/// Command to extract a theme to the local themes folder for customization.
/// </summary>
/// <remarks>
/// Usage:
///   revela theme extract Lumina           → themes/Lumina/ (full extraction)
///   revela theme extract Lumina MyTheme   → themes/MyTheme/ (renamed)
///   revela theme extract --file Body/Gallery.revela → themes/Lumina/Body/Gallery.revela
///   revela theme extract --file Partials/ → themes/Lumina/Partials/* (folder)
///   revela theme extract Lumina --extensions → also extracts Theme.Lumina.* extensions
/// </remarks>
public sealed partial class ThemeExtractCommand(
    IThemeResolver themeResolver,
    ITemplateResolver templateResolver,
    IAssetResolver assetResolver,
    IConfiguration configuration,
    ILogger<ThemeExtractCommand> logger)
{
    /// <summary>
    /// Creates the CLI command.
    /// </summary>
    /// <returns>The configured extract command.</returns>
    public Command Create()
    {
        var sourceArg = new Argument<string?>("source")
        {
            Description = "Name of the theme to extract (required for full extraction)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var targetArg = new Argument<string?>("target")
        {
            Description = "Name for the extracted theme (optional)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var fileOption = new Option<string?>("--file", "-F")
        {
            Description = "Extract specific file or folder (e.g., 'Body/Gallery.revela' or 'Partials/')"
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Overwrite existing files without confirmation"
        };

        var extensionsOption = new Option<bool>("--extensions", "-e")
        {
            Description = "Also extract matching theme extensions (Theme.{Name}.* packages)"
        };

        var command = new Command("extract", "Extract a theme or specific files to themes/ folder for customization");
        command.Arguments.Add(sourceArg);
        command.Arguments.Add(targetArg);
        command.Options.Add(fileOption);
        command.Options.Add(forceOption);
        command.Options.Add(extensionsOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg);
            var target = parseResult.GetValue(targetArg);
            var file = parseResult.GetValue(fileOption);
            var force = parseResult.GetValue(forceOption);
            var includeExtensions = parseResult.GetValue(extensionsOption);

            // If --file is specified, use selective extraction
            if (!string.IsNullOrEmpty(file))
            {
                return await ExecuteSelectiveExtractAsync(file, force, cancellationToken);
            }

            // Otherwise, require source argument for full extraction
            if (string.IsNullOrEmpty(source))
            {
                AnsiConsole.MarkupLine("[red]✗[/] Theme name is required for full extraction.");
                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("Usage:");
                AnsiConsole.MarkupLine("  [cyan]revela theme extract <theme>[/]         Full theme extraction");
                AnsiConsole.MarkupLine("  [cyan]revela theme extract --file <path>[/]   Extract specific file");
                return 1;
            }

            return await ExecuteFullExtractAsync(source, target, force, includeExtensions, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteSelectiveExtractAsync(
        string filePath,
        bool force,
        CancellationToken cancellationToken)
    {
        var projectPath = Environment.CurrentDirectory;

        // Get theme name from config
        var themeName = configuration["theme"] ?? "default";

        // Resolve theme
        var theme = themeResolver.Resolve(themeName, projectPath);
        if (theme is null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Theme [yellow]'{EscapeMarkup(themeName)}'[/] not found.");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Run [cyan]revela theme list[/] to see available themes.");
            return 1;
        }

        // Get extensions
        var extensions = themeResolver.GetExtensions(themeName);

        // Initialize resolvers
        templateResolver.Initialize(theme, extensions, projectPath);
        assetResolver.Initialize(theme, extensions, projectPath);

        // Normalize path
        var normalizedPath = filePath.Replace('\\', '/');
        var isFolder = normalizedPath.EndsWith('/') || !Path.HasExtension(normalizedPath);

        // Find matching entries
        var templateEntries = templateResolver.GetAllEntries();
        var assetEntries = assetResolver.GetAllEntries();

        var matchingFiles = new List<(ResolvedFileInfo Entry, bool IsAsset)>();

        if (isFolder)
        {
            // Match by prefix (case-insensitive)
            var prefix = normalizedPath.TrimEnd('/');

            foreach (var entry in templateEntries)
            {
                if (entry.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchingFiles.Add((entry, false));
                }
            }

            foreach (var entry in assetEntries)
            {
                var assetKey = "assets/" + entry.Key;
                if (assetKey.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    assetKey.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchingFiles.Add((entry, true));
                }
            }
        }
        else
        {
            // Exact match (with or without .revela)
            var keyWithoutExt = normalizedPath.EndsWith(".revela", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath[..^".revela".Length]
                : normalizedPath;

            var templateEntry = templateEntries.FirstOrDefault(e =>
                e.Key.Equals(keyWithoutExt, StringComparison.OrdinalIgnoreCase));

            if (templateEntry is not null)
            {
                matchingFiles.Add((templateEntry, false));
            }
            else
            {
                // Try as asset (Assets/xxx)
                var assetKey = normalizedPath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                    ? normalizedPath["assets/".Length..]
                    : normalizedPath;

                var assetEntry = assetEntries.FirstOrDefault(e =>
                    e.Key.Equals(assetKey, StringComparison.OrdinalIgnoreCase));

                if (assetEntry is not null)
                {
                    matchingFiles.Add((assetEntry, true));
                }
            }
        }

        if (matchingFiles.Count == 0)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] No files match [yellow]'{EscapeMarkup(filePath)}'[/]");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Run [cyan]revela theme files[/] to see available files.");
            return 1;
        }

        // Filter out files that are already local overrides (nothing to extract)
        var localFiles = matchingFiles.Where(f => f.Entry.Source == FileSourceType.Local).ToList();
        if (localFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]The following files are already local overrides:[/]");
            foreach (var (entry, _) in localFiles)
            {
                AnsiConsole.MarkupLine($"  • [cyan]{EscapeMarkup(entry.Key)}[/]");
            }
            AnsiConsole.MarkupLine("");

            matchingFiles = [.. matchingFiles.Where(f => f.Entry.Source != FileSourceType.Local)];

            if (matchingFiles.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]All requested files are already local overrides. Nothing to extract.[/]");
                return 0;
            }
        }

        // Check for existing files
        var existingFiles = new List<string>();
        var filesToExtract = new List<(ResolvedFileInfo Entry, bool IsAsset, string TargetPath)>();

        foreach (var (entry, isAsset) in matchingFiles)
        {
            var targetPath = GetTargetPath(entry, isAsset, themeName, projectPath);
            filesToExtract.Add((entry, isAsset, targetPath));

            if (File.Exists(targetPath))
            {
                existingFiles.Add(Path.GetRelativePath(projectPath, targetPath));
            }
        }

        // Handle existing files
        if (existingFiles.Count > 0 && !force)
        {
            AnsiConsole.MarkupLine("[yellow]The following files already exist:[/]");
            foreach (var file in existingFiles)
            {
                AnsiConsole.MarkupLine($"  • [cyan]{EscapeMarkup(file)}[/]");
            }
            AnsiConsole.MarkupLine("");

            if (!await AnsiConsole.ConfirmAsync("Overwrite?", defaultValue: false, cancellationToken))
            {
                AnsiConsole.MarkupLine("[dim]Cancelled.[/]");
                return 0;
            }
        }

        // Extract files
        var extractedFiles = new List<string>();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Extracting files...", async _ =>
            {
                foreach (var (entry, isAsset, targetPath) in filesToExtract)
                {
                    await ExtractFileAsync(entry, isAsset, targetPath, theme, extensions, cancellationToken);
                    extractedFiles.Add(Path.GetRelativePath(projectPath, targetPath));
                }
            });

        // Success panel
        var fileList = string.Join("\n", extractedFiles.Select(f => $"  [green]+[/] [cyan]{EscapeMarkup(f)}[/]"));
        var panel = new Panel($"{fileList}\n\n" +
                            "[dim]These files will now override the embedded theme files.[/]")
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();

        AnsiConsole.Write(panel);

        return 0;
    }

    private static string GetTargetPath(ResolvedFileInfo entry, bool isAsset, string themeName, string projectPath)
    {
        // All local overrides go to themes/{ThemeName}/
        // The key structure already matches the expected override structure
        var themesFolder = Path.Combine(projectPath, "themes", themeName);

        if (isAsset)
        {
            // Assets/main.css → themes/Lumina/Assets/main.css
            return Path.Combine(themesFolder, "Assets", entry.Key.Replace('/', Path.DirectorySeparatorChar));
        }
        else
        {
            // body/gallery → themes/Lumina/Body/Gallery.revela
            // statistics/overview → themes/Lumina/statistics/Overview.revela
            var parts = entry.Key.Split('/');
            var pascalParts = parts.Select(ToPascalCase).ToArray();
            var relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), pascalParts) + ".revela";
            return Path.Combine(themesFolder, relativePath);
        }
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return char.ToUpperInvariant(input[0]) + input[1..];
    }

    private async Task ExtractFileAsync(
        ResolvedFileInfo entry,
        bool isAsset,
        string targetPath,
        IThemePlugin theme,
        IReadOnlyList<IThemeExtension> extensions,
        CancellationToken cancellationToken)
    {
        // Ensure directory exists
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Get source stream based on entry source
        // CA2000 is a false positive - stream is disposed in finally block via DisposeAsync
#pragma warning disable CA2000
        var sourceStream = GetSourceStream(entry, isAsset, theme, extensions);
#pragma warning restore CA2000

        if (sourceStream is null)
        {
            LogFileNotFound(entry.Key, entry.OriginalPath);
            return;
        }

        try
        {
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);
            LogExtractedFile(entry.Key, targetPath);
        }
        finally
        {
            await sourceStream.DisposeAsync();
        }
    }

    private static Stream? GetSourceStream(
        ResolvedFileInfo entry,
        bool isAsset,
        IThemePlugin theme,
        IReadOnlyList<IThemeExtension> extensions)
    {
        return entry.Source switch
        {
            FileSourceType.Theme => isAsset
                ? theme.GetFile("Assets/" + entry.Key.Replace('/', Path.DirectorySeparatorChar))
                : theme.GetFile(entry.OriginalPath),
            FileSourceType.Extension => GetExtensionStream(entry, extensions),
            FileSourceType.Local when File.Exists(entry.OriginalPath) => File.OpenRead(entry.OriginalPath),
            _ => null
        };
    }

    private static Stream? GetExtensionStream(ResolvedFileInfo entry, IReadOnlyList<IThemeExtension> extensions)
    {
        if (entry.ExtensionName is null)
        {
            return null;
        }

        var extension = extensions.FirstOrDefault(e =>
            e.Metadata.Name.Equals(entry.ExtensionName, StringComparison.OrdinalIgnoreCase));

        return extension?.GetFile(entry.OriginalPath);
    }

    private async Task<int> ExecuteFullExtractAsync(
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
                                // Convert prefix to PascalCase for folder name (statistics → Statistics)
                                var folderName = char.ToUpperInvariant(extension.PartialPrefix[0]) + extension.PartialPrefix[1..];
                                var extensionFolder = Path.Combine(targetPath, "Extensions", folderName);

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
                                extractedExtensions.Add(folderName);
                                LogExtractingExtension(logger, extension.Metadata.Name, extensionFolder);
                            }
                        });
            }
        }

        // Success panel
        var extensionsInfo = extractedExtensions.Count > 0
            ? $"\n[bold]Extensions:[/] {string.Join(", ", extractedExtensions)}"
            : "";

        var panel = new Panel($"[green]Theme '{EscapeMarkup(themeName)}' extracted![/]\n\n" +
                            $"[bold]Location:[/] [cyan]themes/{EscapeMarkup(themeName)}/[/]{extensionsInfo}\n\n" +
                            "[bold]Next steps:[/]\n" +
                            $"1. Edit [cyan]themes/{EscapeMarkup(themeName)}/[/] to customize\n" +
                            "2. Run [cyan]revela generate[/] to see changes\n" +
                            "3. Your local theme takes priority over installed themes")
            .WithHeader("[bold green]Success[/]")
            .WithSuccessStyle();

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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Extracted file '{Key}' to {TargetPath}")]
    private partial void LogExtractedFile(string key, string targetPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File not found: '{Key}' at path '{Path}'")]
    private partial void LogFileNotFound(string key, string path);
}


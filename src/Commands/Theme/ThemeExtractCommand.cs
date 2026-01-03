using System.CommandLine;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectre.Console;

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
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IPluginContext pluginContext,
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

        var command = new Command("extract", "Extract a theme or specific files to themes/ folder for customization");
        command.Arguments.Add(sourceArg);
        command.Arguments.Add(targetArg);
        command.Options.Add(fileOption);
        command.Options.Add(forceOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetValue(sourceArg);
            var target = parseResult.GetValue(targetArg);
            var file = parseResult.GetValue(fileOption);
            var force = parseResult.GetValue(forceOption);

            // If --file is specified, use selective extraction
            if (!string.IsNullOrEmpty(file))
            {
                return await ExecuteSelectiveExtractAsync(file, force, cancellationToken);
            }

            // If no source argument, show interactive selection
            if (string.IsNullOrEmpty(source))
            {
                source = PromptForThemeSelection();
                if (source is null)
                {
                    return 1; // User cancelled or no themes available
                }

                // In interactive mode, ask what to extract
                var extractionChoice = PromptForExtractionMode();
#pragma warning disable IDE0072 // Populate switch - we explicitly want default for future enum values
                return extractionChoice switch
                {
                    ExtractionMode.Full => await ExecuteFullExtractAsync(source, target, force, cancellationToken),
                    ExtractionMode.SelectFiles => await ExecuteInteractiveFileSelectionAsync(source, target, force, cancellationToken),
                    _ => 0, // Cancel or unknown
                };
#pragma warning restore IDE0072
            }

            return await ExecuteFullExtractAsync(source, target, force, cancellationToken);
        });

        return command;
    }

    private async Task<int> ExecuteSelectiveExtractAsync(
        string filePath,
        bool force,
        CancellationToken cancellationToken)
    {
        var projectPath = projectEnvironment.Value.Path;

        // Get theme name from config
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrWhiteSpace(themeName))
        {
            themeName = "Lumina";
        }

        // Resolve theme
        var theme = themeResolver.Resolve(themeName, projectPath);
        if (theme is null)
        {
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{EscapeMarkup(themeName)}' not found.[/]\n\n" +
                "Run [cyan]revela theme list[/] to see available themes.");
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
        var configEntries = GetConfigurationEntries(theme, extensions);

        var matchingFiles = new List<(ResolvedFileInfo Entry, bool IsAsset, bool IsConfig)>();

        if (isFolder)
        {
            // Match by prefix (case-insensitive)
            var prefix = normalizedPath.TrimEnd('/');

            foreach (var entry in templateEntries)
            {
                if (entry.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchingFiles.Add((entry, false, false));
                }
            }

            foreach (var entry in assetEntries)
            {
                var assetKey = "assets/" + entry.Key;
                if (assetKey.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    assetKey.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchingFiles.Add((entry, true, false));
                }
            }

            foreach (var entry in configEntries)
            {
                if (entry.Key.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase) ||
                    entry.Key.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    matchingFiles.Add((entry, false, true));
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
                matchingFiles.Add((templateEntry, false, false));
            }
            else
            {
                // Try as configuration file
                var configEntry = configEntries.FirstOrDefault(e =>
                    e.Key.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (configEntry is not null)
                {
                    matchingFiles.Add((configEntry, false, true));
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
                        matchingFiles.Add((assetEntry, true, false));
                    }
                }
            }
        }

        if (matchingFiles.Count == 0)
        {
            ErrorPanels.ShowError(
                "No Match",
                $"[yellow]No files match '{EscapeMarkup(filePath)}'[/]\n\n" +
                "Run [cyan]revela theme files[/] to see available files.");
            return 1;
        }

        // Filter out files that are already local overrides (nothing to extract)
        var localFiles = matchingFiles.Where(f => f.Entry.Source == FileSourceType.Local).ToList();
        if (localFiles.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]The following files are already local overrides:[/]");
            foreach (var (entry, _, _) in localFiles)
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
        var filesToExtract = new List<(ResolvedFileInfo Entry, bool IsAsset, bool IsConfig, string TargetPath)>();

        foreach (var (entry, isAsset, isConfig) in matchingFiles)
        {
            var targetPath = GetTargetPath(entry, isAsset, isConfig, themeName, projectPath);
            filesToExtract.Add((entry, isAsset, isConfig, targetPath));

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
                foreach (var (entry, isAsset, isConfig, targetPath) in filesToExtract)
                {
                    await ExtractFileAsync(entry, isAsset, isConfig, targetPath, theme, extensions, cancellationToken);
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

    private static string GetTargetPath(ResolvedFileInfo entry, bool isAsset, bool isConfig, string themeName, string projectPath)
    {
        // Configuration files go to theme/configuration/ folder (mirrors theme structure, lowercase)
        if (isConfig)
        {
            // manifest.json → theme/manifest.json
            // configuration/images.json → theme/configuration/images.json
            return Path.Combine(projectPath, "theme", entry.Key.Replace('/', Path.DirectorySeparatorChar));
        }

        // Templates and assets go to themes/{ThemeName}/
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
        bool isConfig,
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
        var sourceStream = GetSourceStream(entry, isAsset, isConfig, theme, extensions);
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
        bool isConfig,
        IThemePlugin theme,
        IReadOnlyList<IThemeExtension> extensions)
    {
        if (isConfig)
        {
            // Configuration files - use OriginalPath which has correct case for embedded resource
            return entry.Source switch
            {
                FileSourceType.Theme => theme.GetFile(entry.OriginalPath),
                FileSourceType.Extension => GetExtensionStream(entry, extensions, entry.OriginalPath),
                FileSourceType.Local when File.Exists(entry.OriginalPath) => File.OpenRead(entry.OriginalPath),
                _ => null
            };
        }

        return entry.Source switch
        {
            FileSourceType.Theme => isAsset
                ? theme.GetFile("Assets/" + entry.Key.Replace('/', Path.DirectorySeparatorChar))
                : theme.GetFile(entry.OriginalPath),
            FileSourceType.Extension => GetExtensionStream(entry, extensions, entry.OriginalPath),
            FileSourceType.Local when File.Exists(entry.OriginalPath) => File.OpenRead(entry.OriginalPath),
            _ => null
        };
    }

    private static Stream? GetExtensionStream(ResolvedFileInfo entry, IReadOnlyList<IThemeExtension> extensions, string path)
    {
        if (entry.ExtensionName is null)
        {
            return null;
        }

        var extension = extensions.FirstOrDefault(e =>
            e.Metadata.Name.Equals(entry.ExtensionName, StringComparison.OrdinalIgnoreCase));

        return extension?.GetFile(path);
    }

    /// <summary>
    /// Gets configuration file entries from theme and extensions.
    /// Includes manifest.json and Configuration/*.json files.
    /// </summary>
    private static List<ResolvedFileInfo> GetConfigurationEntries(
        IThemePlugin theme,
        IReadOnlyList<IThemeExtension> extensions)
    {
        var entries = new Dictionary<string, ResolvedFileInfo>(StringComparer.OrdinalIgnoreCase);

        // Get from base theme (theme.json and Configuration/*.json)
        foreach (var file in theme.GetAllFiles())
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.StartsWith("Configuration/", StringComparison.OrdinalIgnoreCase))
            {
                // Use lowercase for consistency
                var key = "configuration/" + normalized["Configuration/".Length..];
                entries[key] = new ResolvedFileInfo(
                    key,
                    normalized,
                    FileSourceType.Theme,
                    null);
            }
            else if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                entries["manifest.json"] = new ResolvedFileInfo(
                    "manifest.json",
                    normalized,
                    FileSourceType.Theme,
                    null);
            }
        }

        // Get from extensions (can override)
        foreach (var ext in extensions)
        {
            foreach (var file in ext.GetAllFiles())
            {
                var normalized = file.Replace('\\', '/');
                if (normalized.StartsWith("Configuration/", StringComparison.OrdinalIgnoreCase))
                {
                    var key = "configuration/" + normalized["Configuration/".Length..];
                    entries[key] = new ResolvedFileInfo(
                        key,
                        normalized,
                        FileSourceType.Extension,
                        ext.Metadata.Name);
                }
                else if (normalized.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                {
                    entries["manifest.json"] = new ResolvedFileInfo(
                        "manifest.json",
                        normalized,
                        FileSourceType.Extension,
                        ext.Metadata.Name);
                }
            }
        }

        return [.. entries.Values];
    }

    private async Task<int> ExecuteFullExtractAsync(
        string sourceName,
        string? targetName,
        bool force,
        CancellationToken cancellationToken)
    {
        var projectPath = projectEnvironment.Value.Path;
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
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{EscapeMarkup(sourceName)}' not found.[/]\n\n" +
                "Run [cyan]revela theme list[/] to see available themes.");
            return 1;
        }

        // Check if target exists
        if (Directory.Exists(targetPath))
        {
            if (!force)
            {
                ErrorPanels.ShowFileExistsError(
                    $"themes/{themeName}/",
                    "Use [cyan]--force[/] to overwrite existing theme.");
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

        // Extract extensions into category subfolders (Partials/<ExtName>/, Assets/<ExtName>/)
        var extractedExtensions = new List<string>();
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

                            // Extract each file into the appropriate category subfolder
                            foreach (var file in extension.GetAllFiles())
                            {
                                // file is like "Partials/Statistics.revela" or "Assets/statistics.css"
                                // We want to insert the extension name: "Partials/Statistics/Statistics.revela"
                                var parts = file.Split('/', 2);
                                string targetFile;
                                if (parts.Length == 2)
                                {
                                    // Has category: Category/ExtName/FileName
                                    targetFile = Path.Combine(targetPath, parts[0], folderName, parts[1]);
                                }
                                else
                                {
                                    // No category (root file): ExtName/FileName
                                    targetFile = Path.Combine(targetPath, folderName, file);
                                }

                                // Ensure directory exists
                                var targetDir = Path.GetDirectoryName(targetFile);
                                if (!string.IsNullOrEmpty(targetDir))
                                {
                                    Directory.CreateDirectory(targetDir);
                                }

                                // Handle overwrite
                                if (File.Exists(targetFile) && !force)
                                {
                                    continue;
                                }

                                // Copy file
                                using var stream = extension.GetFile(file);
                                if (stream is not null)
                                {
                                    await using var fileStream = File.Create(targetFile);
                                    await stream.CopyToAsync(fileStream, cancellationToken);
                                }
                            }

                            extractedExtensions.Add(folderName);
                            LogExtractingExtension(logger, extension.Metadata.Name, $"{targetPath}/*/'{folderName}/");
                        }
                    });
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
        var manifestPath = Path.Combine(themePath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var json = File.ReadAllText(manifestPath);

        // Simple regex replacement for "name": "..."
        var pattern = "\"name\"\\s*:\\s*\"[^\"]*\"";
        var replacement = $"\"name\": \"{newName}\"";
        var updatedJson = System.Text.RegularExpressions.Regex.Replace(json, pattern, replacement);

        File.WriteAllText(manifestPath, updatedJson);
    }

    private static string EscapeMarkup(string text)
    {
        return text
            .Replace("[", "[[", StringComparison.Ordinal)
            .Replace("]", "]]", StringComparison.Ordinal);
    }

    /// <summary>
    /// Prompts the user to select a theme for extraction.
    /// </summary>
    /// <returns>The selected theme name, or null if cancelled or no themes available.</returns>
    private string? PromptForThemeSelection()
    {
        var projectPath = projectEnvironment.Value.Path;
        var availableThemes = themeResolver.GetAvailableThemes(projectPath).ToList();

        if (availableThemes.Count == 0)
        {
            ErrorPanels.ShowError(
                "No Themes Available",
                "[yellow]No themes are installed.[/]\n\n" +
                "Install a theme first:\n" +
                "  [cyan]revela theme install Lumina[/]");
            return null;
        }

        var choices = availableThemes
            .Select(t => new ThemeChoice(t.Metadata.Name, GetThemeSource(t)))
            .ToList();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<ThemeChoice>()
                .Title("[cyan]Select theme to extract[/]")
                .PageSize(10)
                .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                .UseConverter(c => c.Display)
                .AddChoices(choices));

        return selection.Name;
    }

    private string GetThemeSource(IThemePlugin theme)
    {
        // Look up source from plugin context
        var pluginInfo = pluginContext.Plugins
            .FirstOrDefault(p => p.Plugin.Metadata.Name.Equals(theme.Metadata.Name, StringComparison.OrdinalIgnoreCase));

        return pluginInfo?.Source switch
        {
            PluginSource.Bundled => "bundled",
            PluginSource.Local => "installed",
            _ => "installed"
        };
    }

    /// <summary>
    /// Prompts the user to select extraction mode.
    /// </summary>
    private static ExtractionMode PromptForExtractionMode()
    {
        var choices = new List<ExtractionChoice>
        {
            new("Extract entire theme", ExtractionMode.Full),
            new("Select specific files", ExtractionMode.SelectFiles),
            new("Cancel", ExtractionMode.Cancel)
        };

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<ExtractionChoice>()
                .Title("[cyan]What would you like to extract?[/]")
                .HighlightStyle(new Style(Color.Cyan1, decoration: Decoration.Bold))
                .UseConverter(c => c.Display)
                .AddChoices(choices));

        return selection.Mode;
    }

    /// <summary>
    /// Executes interactive file selection and extraction.
    /// </summary>
    private async Task<int> ExecuteInteractiveFileSelectionAsync(
        string sourceName,
        string? targetName,
        bool force,
        CancellationToken cancellationToken)
    {
        var projectPath = projectEnvironment.Value.Path;

        // Resolve theme
        var sourceTheme = themeResolver.ResolveInstalled(sourceName)
                          ?? themeResolver.Resolve(sourceName, projectPath);

        if (sourceTheme is null)
        {
            ErrorPanels.ShowError(
                "Theme Not Found",
                $"[yellow]Theme '{EscapeMarkup(sourceName)}' not found.[/]\n\n" +
                "Run [cyan]revela theme list[/] to see available themes.");
            return 1;
        }

        // Get extensions for this theme
        var extensions = themeResolver.GetExtensions(sourceName);

        // Get all available files from theme and extensions
        var allFiles = GetThemeAndExtensionFiles(sourceTheme, extensions)
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Path)
            .ToList();

        if (allFiles.Count == 0)
        {
            ErrorPanels.ShowError(
                "No Files Found",
                $"[yellow]Theme '{EscapeMarkup(sourceName)}' has no extractable files.[/]");
            return 1;
        }

        // Group files by category for display (include SourceType and ExtensionIndex)
        var templateFiles = allFiles.Where(f => f.Category == "Templates")
            .Select(f => new FileChoice(f.Path, f.Category, f.SourceType, f.ExtensionIndex)).ToList();
        var assetFiles = allFiles.Where(f => f.Category == "Assets")
            .Select(f => new FileChoice(f.Path, f.Category, f.SourceType, f.ExtensionIndex)).ToList();
        var configFiles = allFiles.Where(f => f.Category == "Configuration")
            .Select(f => new FileChoice(f.Path, f.Category, f.SourceType, f.ExtensionIndex)).ToList();
        var otherFiles = allFiles.Where(f => f.Category == "Other")
            .Select(f => new FileChoice(f.Path, f.Category, f.SourceType, f.ExtensionIndex)).ToList();

        var prompt = new MultiSelectionPrompt<FileChoice>()
            .Title("[cyan]Select files to extract[/] [dim](Space to toggle, Enter to confirm)[/]")
            .PageSize(15)
            .HighlightStyle(new Style(Color.Cyan1))
            .InstructionsText("[dim](↑↓ navigate, Space toggle, Enter confirm)[/]")
            .UseConverter(f => f.Display);

        if (templateFiles.Count > 0)
        {
            prompt.AddChoiceGroup(new FileChoice("Templates", "group"), templateFiles);
        }

        if (assetFiles.Count > 0)
        {
            prompt.AddChoiceGroup(new FileChoice("Assets", "group"), assetFiles);
        }

        if (configFiles.Count > 0)
        {
            prompt.AddChoiceGroup(new FileChoice("Configuration", "group"), configFiles);
        }

        if (otherFiles.Count > 0)
        {
            prompt.AddChoiceGroup(new FileChoice("Other", "group"), otherFiles);
        }

        var selectedFiles = AnsiConsole.Prompt(prompt);

        if (selectedFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No files selected. Extraction cancelled.[/]");
            return 0;
        }

        // Determine target directory
        var targetThemeName = targetName ?? sourceName;
        var themesDir = Path.Combine(projectPath, "themes", targetThemeName);

        // Check if target exists
        if (Directory.Exists(themesDir) && !force)
        {
            var existingFiles = selectedFiles
                .Where(f => f.Category != "group" && File.Exists(Path.Combine(themesDir, f.Path)))
                .ToList();

            if (existingFiles.Count > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Some files already exist in themes/{EscapeMarkup(targetThemeName)}/[/]");

                var overwrite = await AnsiConsole.ConfirmAsync("Overwrite existing files?", defaultValue: false, cancellationToken).ConfigureAwait(false);
                if (!overwrite)
                {
                    AnsiConsole.MarkupLine("[dim]Extraction cancelled.[/]");
                    return 0;
                }
            }
        }

        // Extract selected files using appropriate source (theme or extension)
        var extractedCount = 0;
        foreach (var file in selectedFiles.Where(f => f.Category != "group"))
        {
            var targetPath = Path.Combine(themesDir, file.Path);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            try
            {
                // Get file from correct source (theme or extension)
                Stream? sourceStream;
                if (file.SourceType == FileSourceType.Extension && file.ExtensionIndex >= 0 && file.ExtensionIndex < extensions.Count)
                {
                    // For extensions, file.Path is the target path with extension folder inserted
                    // e.g., "Partials/Statistics/Statistics.revela" - we need to extract "Partials/Statistics.revela"
                    var extension = extensions[file.ExtensionIndex];
                    var folderName = char.ToUpperInvariant(extension.PartialPrefix[0]) + extension.PartialPrefix[1..];
                    var originalPath = GetExtensionOriginalPath(file.Path, folderName);
                    sourceStream = extension.GetFile(originalPath);
                }
                else
                {
                    sourceStream = sourceTheme.GetFile(file.Path);
                }

                if (sourceStream is null)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] File not found: {EscapeMarkup(file.Path)}");
                    continue;
                }

                using (sourceStream)
                {
                    using var targetStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(targetStream, cancellationToken).ConfigureAwait(false);
                }

                extractedCount++;
                LogExtractedFile(file.Path, targetPath);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Failed to extract {EscapeMarkup(file.Path)}: {EscapeMarkup(ex.Message)}");
            }
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] Extracted [cyan]{extractedCount}[/] file(s) to [cyan]themes/{EscapeMarkup(targetThemeName)}/[/]");

        return 0;
    }

    /// <summary>
    /// Gets all files from a theme and its extensions for selection, categorized by type.
    /// Extension files are shown with their target path: Category/{ExtName}/file
    /// </summary>
    private static List<(string Path, string Category, FileSourceType SourceType, int ExtensionIndex)> GetThemeAndExtensionFiles(
        IThemePlugin theme,
        IReadOnlyList<IThemeExtension> extensions)
    {
        var files = new List<(string Path, string Category, FileSourceType SourceType, int ExtensionIndex)>();

        // Add files from base theme
        foreach (var file in theme.GetAllFiles())
        {
            var normalized = file.Replace('\\', '/');
            var category = GetFileCategory(normalized);
            files.Add((normalized, category, FileSourceType.Theme, -1));
        }

        // Add files from extensions - insert extension name into path
        // e.g., "Partials/Statistics.revela" → "Partials/Statistics/Statistics.revela"
        for (var i = 0; i < extensions.Count; i++)
        {
            var extension = extensions[i];
            // Convert prefix to PascalCase for folder name (statistics → Statistics)
            var folderName = char.ToUpperInvariant(extension.PartialPrefix[0]) + extension.PartialPrefix[1..];

            foreach (var file in extension.GetAllFiles())
            {
                var normalized = file.Replace('\\', '/');
                var targetPath = InsertExtensionFolder(normalized, folderName);
                var category = GetFileCategory(normalized);
                files.Add((targetPath, category, FileSourceType.Extension, i));
            }
        }

        return files;
    }

    /// <summary>
    /// Inserts extension folder name into the path.
    /// "Partials/Statistics.revela" + "Statistics" → "Partials/Statistics/Statistics.revela"
    /// "Assets/styles.css" + "Statistics" → "Assets/Statistics/styles.css"
    /// </summary>
    private static string InsertExtensionFolder(string path, string folderName)
    {
        var parts = path.Split('/', 2);
        if (parts.Length == 2)
        {
            // Has category: Category/ExtName/rest
            return $"{parts[0]}/{folderName}/{parts[1]}";
        }

        // Root file: ExtName/file
        return $"{folderName}/{path}";
    }

    /// <summary>
    /// Determines the category of a file based on its path.
    /// </summary>
    private static string GetFileCategory(string path)
    {
        if (path.EndsWith(".revela", StringComparison.OrdinalIgnoreCase))
        {
            return "Templates";
        }

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return "Assets";
        }

        if (path.StartsWith("Configuration/", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            return "Configuration";
        }

        return "Other";
    }

    /// <summary>
    /// Extracts the original extension file path from the target path.
    /// E.g., "Partials/Statistics/Statistics.revela" → "Partials/Statistics.revela"
    /// </summary>
    private static string GetExtensionOriginalPath(string targetPath, string folderName)
    {
        // Format: Category/ExtName/file → Category/file
        var parts = targetPath.Split('/', 3);
        if (parts.Length == 3 && parts[1].Equals(folderName, StringComparison.OrdinalIgnoreCase))
        {
            // Category/ExtName/rest → Category/rest
            return $"{parts[0]}/{parts[2]}";
        }

        if (parts.Length == 2 && parts[0].Equals(folderName, StringComparison.OrdinalIgnoreCase))
        {
            // ExtName/file → file (root level extension file)
            return parts[1];
        }

        return targetPath;
    }

    /// <summary>
    /// Extraction mode selection.
    /// </summary>
    private enum ExtractionMode
    {
        Full,
        SelectFiles,
        Cancel
    }

    /// <summary>
    /// Represents an extraction mode choice.
    /// </summary>
    private sealed record ExtractionChoice(string Display, ExtractionMode Mode);

    /// <summary>
    /// Represents a theme choice in the selection prompt.
    /// </summary>
    private sealed record ThemeChoice(string Name, string Source)
    {
        public string Display => $"{Name} [dim]({Source})[/]";
    }

    /// <summary>
    /// Represents a file choice in the selection prompt.
    /// </summary>
    /// <param name="Path">Relative path of the file.</param>
    /// <param name="Category">Category (Templates, Assets, Configuration, Other).</param>
    /// <param name="SourceType">Source type (Theme or Extension).</param>
    /// <param name="ExtensionIndex">Index of the extension in the extensions list (-1 for theme).</param>
    private sealed record FileChoice(string Path, string Category, FileSourceType SourceType = FileSourceType.Theme, int ExtensionIndex = -1)
    {
        public string Display => Category == "group"
            ? $"[bold]{Path}[/]"
            : SourceType == FileSourceType.Extension
                ? $"  {Path} [dim](extension)[/]"
                : $"  {Path}";
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

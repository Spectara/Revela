using Microsoft.Extensions.Options;
using Spectara.Revela.Core;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Theme.Services;

/// <summary>
/// Default implementation of <see cref="IThemeService"/>.
/// UI-free — all formatting is done by the consuming command or MCP tool.
/// </summary>
internal sealed partial class ThemeService(
    IThemeRegistry themeRegistry,
    ITemplateResolver templateResolver,
    IAssetResolver assetResolver,
    IPackageContext packageContext,
    PackageManager packageManager,
    IPackageIndexService packageIndexService,
    IConfigService configService,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig,
    ILogger<ThemeService> logger) : IThemeService
{
    private const string ThemePackagePrefix = "Spectara.Revela.Themes.";

    private string ProjectPath => projectEnvironment.Value.Path;

    /// <inheritdoc />
    public async Task<ThemeListResult> ListAsync(
        bool includeOnline = false,
        CancellationToken cancellationToken = default)
    {
        var themes = themeRegistry.GetAvailableThemes(ProjectPath).ToList();
        var themeSources = BuildSourceLookup();

        var installed = themes.Select(t => ToThemeInfo(t, themeSources)).ToList();

        var online = new List<OnlineThemeInfo>();
        if (includeOnline)
        {
            var installedNames = themes
                .Select(t => t.Metadata.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var searchResults = await packageIndexService.SearchByTypeAsync("RevelaTheme", cancellationToken);
            online = [.. searchResults.Select(r => new OnlineThemeInfo
            {
                Id = r.Id,
                Name = ExtractThemeName(r.Id),
                Version = r.Version,
                Description = r.Description,
                IsInstalled = installedNames.Contains(ExtractThemeName(r.Id))
            })];
        }

        return new ThemeListResult
        {
            Installed = installed,
            Online = online
        };
    }

    /// <inheritdoc />
    public ThemeInfoResult GetCurrentTheme()
    {
        var themeName = themeConfig.CurrentValue.Name ?? "Lumina";
        var theme = themeRegistry.Resolve(themeName, ProjectPath);
        var source = GetThemeSource(theme);
        var extensions = theme is not null
            ? themeRegistry.GetExtensions(themeName)
                .Select(e => ToExtensionInfo(e))
                .ToList()
            : [];

        return new ThemeInfoResult
        {
            ThemeName = themeName,
            Theme = theme,
            Source = source,
            Extensions = extensions
        };
    }

    /// <inheritdoc />
    public async Task<bool> InstallAsync(
        string name,
        string? version = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var packageId = EnsureFullPackageId(name);
        LogInstalling(logger, packageId);
        return await packageManager.InstallAsync(packageId, version, source, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UninstallAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var packageId = EnsureFullPackageId(name);
        LogUninstalling(logger, packageId);
        return await packageManager.UninstallPluginAsync(packageId, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public ThemeFilesResult GetFiles(string? themeName = null)
    {
        var name = themeName ?? themeConfig.CurrentValue.Name ?? "Lumina";
        var theme = themeRegistry.Resolve(name, ProjectPath);

        if (theme is null)
        {
            return new ThemeFilesResult
            {
                ThemeName = name,
                Templates = [],
                Assets = []
            };
        }

        var extensions = themeRegistry.GetExtensions(name);
        templateResolver.Initialize(theme, extensions, ProjectPath);
        assetResolver.Initialize(theme, extensions, ProjectPath);

        return new ThemeFilesResult
        {
            ThemeName = name,
            Templates = templateResolver.GetAllEntries(),
            Assets = assetResolver.GetAllEntries()
        };
    }

    /// <inheritdoc />
    public async Task SetActiveThemeAsync(
        string themeName,
        CancellationToken cancellationToken = default)
    {
        var update = new System.Text.Json.Nodes.JsonObject
        {
            ["theme"] = new System.Text.Json.Nodes.JsonObject { ["name"] = themeName }
        };
        await configService.UpdateProjectConfigAsync(update, cancellationToken);
        LogThemeChanged(logger, themeName);
    }

    /// <inheritdoc />
    public async Task<ThemeExtractResult> ExtractAsync(
        string sourceName,
        string? targetName = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var themeName = targetName ?? sourceName;
        var themesFolder = Path.Combine(ProjectPath, ProjectPaths.Themes);
        var targetPath = Path.Combine(themesFolder, themeName);

        // Prefer installed theme (user wants a fresh copy from original)
        var sourceTheme = themeRegistry.ResolveInstalled(sourceName)
                          ?? themeRegistry.Resolve(sourceName, ProjectPath);

        if (sourceTheme is null)
        {
            return new ThemeExtractResult
            {
                Success = false,
                ErrorMessage = $"Theme '{sourceName}' not found."
            };
        }

        // Check if target exists
        if (Directory.Exists(targetPath) && !force)
        {
            return new ThemeExtractResult
            {
                Success = false,
                ErrorMessage = $"Target 'themes/{themeName}/' already exists. Use force to overwrite."
            };
        }

        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, recursive: true);
        }

        Directory.CreateDirectory(themesFolder);

        // Extract base theme
        LogExtracting(logger, sourceName, targetPath);
        await sourceTheme.ExtractToAsync(targetPath, cancellationToken);

        // Update theme.json name if renamed
        if (targetName is not null && !targetName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
        {
            UpdateThemeName(targetPath, targetName);
        }

        // Extract extensions
        var extractedExtensions = new List<string>();
        var extensions = themeRegistry.GetExtensions(sourceName);

        foreach (var extension in extensions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefix = extension.Prefix ?? string.Empty;
            var folderName = prefix.Length > 0
                ? char.ToUpperInvariant(prefix[0]) + prefix[1..]
                : "Extension";

            foreach (var file in extension.GetAllFiles())
            {
                var parts = file.Split('/', 2);
                var targetFile = parts.Length == 2
                    ? Path.Combine(targetPath, parts[0], folderName, parts[1])
                    : Path.Combine(targetPath, folderName, file);

                var targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                if (File.Exists(targetFile) && !force)
                {
                    continue;
                }

                using var stream = extension.GetFile(file);
                if (stream is not null)
                {
                    await using var fileStream = File.Create(targetFile);
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }
            }

            extractedExtensions.Add(folderName);
        }

        return new ThemeExtractResult
        {
            Success = true,
            TargetPath = targetPath,
            ThemeName = themeName,
            ExtractedExtensions = extractedExtensions
        };
    }

    /// <inheritdoc />
    public async Task<ThemeExtractResult> ExtractFilesAsync(
        string filePattern,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var currentThemeName = themeConfig.CurrentValue.Name ?? "Lumina";
        var theme = themeRegistry.Resolve(currentThemeName, ProjectPath);

        if (theme is null)
        {
            return new ThemeExtractResult
            {
                Success = false,
                ErrorMessage = $"Theme '{currentThemeName}' not found."
            };
        }

        var extensions = themeRegistry.GetExtensions(currentThemeName);
        templateResolver.Initialize(theme, extensions, ProjectPath);
        assetResolver.Initialize(theme, extensions, ProjectPath);

        // Collect matching files
        var allEntries = new List<ResolvedFileInfo>();
        allEntries.AddRange(templateResolver.GetAllEntries());
        allEntries.AddRange(assetResolver.GetAllEntries());

        var isFolder = filePattern.EndsWith('/') || filePattern.EndsWith('\\');
        var normalizedPattern = filePattern.TrimEnd('/', '\\').Replace('\\', '/');

        List<ResolvedFileInfo> matchingEntries;
        if (isFolder)
        {
            matchingEntries = [.. allEntries.Where(e => e.Key.StartsWith(normalizedPattern, StringComparison.OrdinalIgnoreCase))];
        }
        else
        {
            matchingEntries = [.. allEntries.Where(e => e.Key.Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase)
                || e.OriginalPath.Replace('\\', '/').Equals(normalizedPattern, StringComparison.OrdinalIgnoreCase))];
        }

        if (matchingEntries.Count == 0)
        {
            return new ThemeExtractResult
            {
                Success = false,
                ErrorMessage = $"No files matching '{filePattern}' found in theme '{currentThemeName}'."
            };
        }

        var extractedFiles = new List<string>();
        var themesFolder = Path.Combine(ProjectPath, ProjectPaths.Themes, currentThemeName);

        foreach (var entry in matchingEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isAsset = assetResolver.GetAllEntries().Any(a => a.Key.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
            var targetPath = isAsset
                ? Path.Combine(themesFolder, "Assets", entry.Key.Replace('/', Path.DirectorySeparatorChar))
                : Path.Combine(themesFolder, ToPascalCasePath(entry.Key) + ".revela");

            if (File.Exists(targetPath) && !force)
            {
                continue;
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

#pragma warning disable CA2000 // Stream ownership transferred to CopyToAsync + disposed in finally
            var sourceStream = GetSourceStream(entry, isAsset, theme, extensions);
#pragma warning restore CA2000
            if (sourceStream is not null)
            {
                try
                {
                    await using var targetStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(targetStream, cancellationToken);
                    extractedFiles.Add(Path.GetRelativePath(ProjectPath, targetPath));
                }
                finally
                {
                    await sourceStream.DisposeAsync();
                }
            }
        }

        return new ThemeExtractResult
        {
            Success = true,
            ThemeName = currentThemeName,
            ExtractedFiles = extractedFiles
        };
    }

    private static Stream? GetSourceStream(
        ResolvedFileInfo entry,
        bool isAsset,
        ITheme theme,
        IReadOnlyList<ITheme> extensions)
    {
        return entry.Source switch
        {
            FileSourceType.Theme => isAsset
                ? theme.GetFile("Assets/" + entry.Key.Replace('/', Path.DirectorySeparatorChar))
                : theme.GetFile(entry.OriginalPath),
            FileSourceType.Extension when entry.ExtensionName is not null =>
                extensions.FirstOrDefault(e => e.Metadata.Name.Equals(entry.ExtensionName, StringComparison.OrdinalIgnoreCase))
                    ?.GetFile(entry.OriginalPath),
            FileSourceType.Local when File.Exists(entry.OriginalPath) => File.OpenRead(entry.OriginalPath),
            _ => null
        };
    }

    private static string ToPascalCasePath(string key)
    {
        var parts = key.Split('/');
        return string.Join(Path.DirectorySeparatorChar.ToString(),
            parts.Select(p => p.Length > 0 ? char.ToUpperInvariant(p[0]) + p[1..] : p));
    }

    private static void UpdateThemeName(string themePath, string newName)
    {
        var manifestPath = Path.Combine(themePath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(
            File.ReadAllText(manifestPath));

        if (json is not null)
        {
            json["name"] = newName;
            File.WriteAllText(manifestPath, json.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Extracting theme '{SourceName}' to {TargetPath}")]
    private static partial void LogExtracting(ILogger logger, string sourceName, string targetPath);

    private Dictionary<string, PackageSource> BuildSourceLookup() =>
        packageContext.Themes
            .ToDictionary(
                t => t.Theme.Metadata.Name,
                t => t.Source,
                StringComparer.OrdinalIgnoreCase);

    private ThemeInfo ToThemeInfo(ITheme theme, Dictionary<string, PackageSource> sources)
    {
        var isLocal = !sources.ContainsKey(theme.Metadata.Name);
        var extensions = themeRegistry.GetExtensions(theme.Metadata.Name)
            .Select(e => ToExtensionInfo(e))
            .ToList();

        return new ThemeInfo
        {
            Metadata = theme.Metadata,
            IsLocal = isLocal,
            Source = isLocal ? null : sources.GetValueOrDefault(theme.Metadata.Name),
            Extensions = extensions
        };
    }

    private ThemeExtensionInfo ToExtensionInfo(ITheme extension)
    {
        var sources = BuildSourceLookup();
        return new ThemeExtensionInfo
        {
            Metadata = extension.Metadata,
            Source = sources.GetValueOrDefault(extension.Metadata.Name)
        };
    }

    private string GetThemeSource(ITheme? theme)
    {
        if (theme is null)
        {
            return "not found";
        }

        var themeInfo = packageContext.Themes
            .FirstOrDefault(t => t.Theme.Metadata.Name.Equals(theme.Metadata.Name, StringComparison.OrdinalIgnoreCase));

        if (themeInfo is null)
        {
            return "local";
        }

        return themeInfo.Source switch
        {
            PackageSource.Bundled => "bundled",
            PackageSource.Local => "installed",
            _ => "installed"
        };
    }

    private static string EnsureFullPackageId(string name)
    {
        if (name.StartsWith(ThemePackagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return ThemePackagePrefix + name;
    }

    private static string ExtractThemeName(string packageId) =>
        packageId.StartsWith(ThemePackagePrefix, StringComparison.OrdinalIgnoreCase)
            ? packageId[ThemePackagePrefix.Length..]
            : packageId;

    [LoggerMessage(Level = LogLevel.Information, Message = "Installing theme: {PackageId}")]
    private static partial void LogInstalling(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Uninstalling theme: {PackageId}")]
    private static partial void LogUninstalling(ILogger logger, string packageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Active theme changed to: {ThemeName}")]
    private static partial void LogThemeChanged(ILogger logger, string themeName);
}

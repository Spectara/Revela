using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Resolves assets by scanning theme, extensions, and local overrides.
/// </summary>
/// <remarks>
/// <para>
/// Scans all sources once at initialization and builds merged asset lists.
/// Local overrides take priority over extensions, which take priority over theme.
/// </para>
/// <para>
/// Output conventions:
/// - Theme assets: _assets/{path}
/// - Extension assets: _assets/{partialPrefix}/{path}
/// - Local assets: override by same name, or append new
/// </para>
/// </remarks>
public sealed partial class AssetResolver(ILogger<AssetResolver> logger) : IAssetResolver
{
    private const string AssetsFolderName = "Assets";
    private const string OutputAssetsFolderName = "_assets";

    private static readonly HashSet<string> CssExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css"
    };

    private static readonly HashSet<string> JsExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs"
    };

    private readonly Dictionary<string, AssetEntry> assets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> styleSheetOrder = [];
    private readonly List<string> scriptOrder = [];

    private IThemePlugin? theme;
    private string? localThemePath;
    private bool isInitialized;

    /// <inheritdoc />
    public void Initialize(IThemePlugin theme, IReadOnlyList<IThemeExtension> extensions, string projectPath)
    {
        this.theme = theme;
        assets.Clear();
        styleSheetOrder.Clear();
        scriptOrder.Clear();

        var themeName = theme.Metadata.Name;
        localThemePath = Path.Combine(projectPath, ProjectPaths.Themes, themeName, AssetsFolderName);

        LogInitializing(themeName);

        // 1. Scan base theme (lowest priority)
        ScanTheme(theme);

        // 2. Scan extensions (medium priority) - assets go under {prefix}/
        foreach (var extension in extensions)
        {
            ScanExtension(extension);
        }

        // 3. Scan local overrides (highest priority)
        if (Directory.Exists(localThemePath))
        {
            ScanLocalOverrides(localThemePath);
        }

        isInitialized = true;
        LogInitialized(assets.Count, styleSheetOrder.Count, scriptOrder.Count);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetStyleSheets()
    {
        EnsureInitialized();
        return styleSheetOrder.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetScripts()
    {
        EnsureInitialized();
        return scriptOrder.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetOtherAssets()
    {
        EnsureInitialized();
        return assets
            .Where(kvp => !IsCss(kvp.Key) && !IsJs(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task CopyToOutputAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var assetsOutputDir = Path.Combine(outputDirectory, OutputAssetsFolderName);
        Directory.CreateDirectory(assetsOutputDir);

        LogCopyingAssets(assets.Count, assetsOutputDir);

        foreach (var (key, entry) in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = Path.Combine(assetsOutputDir, key.Replace('/', Path.DirectorySeparatorChar));
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            await CopyAssetAsync(key, entry, targetPath, cancellationToken);
        }
    }

    private async Task CopyAssetAsync(
        string key,
        AssetEntry entry,
        string targetPath,
        CancellationToken cancellationToken)
    {
        using var sourceStream = GetAssetStream(entry);
        if (sourceStream is null)
        {
            LogAssetNotFound(key, entry.Path);
            return;
        }

        await using var targetStream = File.Create(targetPath);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            var sourceTypeName = entry.SourceType.ToString();
            LogCopiedAsset(key, sourceTypeName);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetResolvedAssets()
    {
        EnsureInitialized();

        return assets.ToDictionary(
            kvp => kvp.Key,
            kvp => $"{kvp.Value.SourceType}: {kvp.Value.Path}");
    }

    private void ScanTheme(IThemePlugin theme)
    {
        var count = 0;

        foreach (var file in theme.GetAllFiles())
        {
            // Only scan Assets/ folder
            if (!IsInAssetsFolder(file))
            {
                continue;
            }

            var key = DeriveKeyFromPath(file);
            assets[key] = new AssetEntry(AssetSourceType.Theme, file, null);
            TrackOrderedAsset(key);
            count++;
        }

        LogScannedTheme(theme.Metadata.Name, count);
    }

    private void ScanExtension(IThemeExtension extension)
    {
        var prefix = extension.PartialPrefix;
        var count = 0;

        foreach (var file in extension.GetAllFiles())
        {
            // Only scan Assets/ folder
            if (!IsInAssetsFolder(file))
            {
                continue;
            }

            // Extension key = prefix + relative path within Assets/
            var key = DeriveExtensionKey(prefix, file);
            assets[key] = new AssetEntry(AssetSourceType.Extension, file, extension);
            TrackOrderedAsset(key);
            count++;
        }

        LogScannedExtension(extension.Metadata.Name, prefix, count);
    }

    private void ScanLocalOverrides(string localPath)
    {
        var overrideCount = 0;
        var newCount = 0;

        foreach (var file in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(localPath, file);
            var key = DeriveKeyFromLocalPath(relativePath);
            var isOverride = assets.ContainsKey(key);

            assets[key] = new AssetEntry(AssetSourceType.Local, file, null);

            if (isOverride)
            {
                LogLocalOverride(key, file);
                overrideCount++;
                // Override keeps original position in order lists
            }
            else
            {
                LogLocalNew(key, file);
                TrackOrderedAsset(key);
                newCount++;
            }
        }

        if (overrideCount > 0 || newCount > 0)
        {
            LogScannedLocal(overrideCount, newCount);
        }
    }

    private void TrackOrderedAsset(string key)
    {
        if (IsCss(key) && !styleSheetOrder.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            styleSheetOrder.Add(key);
        }
        else if (IsJs(key) && !scriptOrder.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            scriptOrder.Add(key);
        }
    }

    private Stream? GetAssetStream(AssetEntry entry)
    {
        return entry.SourceType switch
        {
            AssetSourceType.Local => File.Exists(entry.Path) ? File.OpenRead(entry.Path) : null,
            AssetSourceType.Theme => theme?.GetFile(entry.Path),
            AssetSourceType.Extension => entry.Extension?.GetFile(entry.Path),
            _ => null
        };
    }

    /// <summary>
    /// Derives asset key from theme file path.
    /// Assets/main.css → main.css
    /// Assets/fonts/inter.woff2 → fonts/inter.woff2
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Asset keys use lowercase by convention for web URLs")]
    private static string DeriveKeyFromPath(string path)
    {
        // Remove Assets/ prefix
        var relativePath = path;
        var assetsPrefix = AssetsFolderName + "/";
        var assetsPrefixBackslash = AssetsFolderName + "\\";

        if (relativePath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[assetsPrefix.Length..];
        }
        else if (relativePath.StartsWith(assetsPrefixBackslash, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[assetsPrefixBackslash.Length..];
        }

        // Normalize separators and lowercase
        return relativePath
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    /// <summary>
    /// Derives asset key for extension files.
    /// Assets/statistics.css + prefix "statistics" → statistics/statistics.css
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Asset keys use lowercase by convention for web URLs")]
    private static string DeriveExtensionKey(string prefix, string path)
    {
        // Get relative path within Assets/
        var relativePath = DeriveKeyFromPath(path);

        // Combine prefix with relative path
        return $"{prefix}/{relativePath}".ToLowerInvariant();
    }

    /// <summary>
    /// Derives asset key from local file path.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Asset keys use lowercase by convention for web URLs")]
    private static string DeriveKeyFromLocalPath(string relativePath)
    {
        // Normalize separators and lowercase
        return relativePath
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    private static bool IsInAssetsFolder(string path)
    {
        return path.StartsWith(AssetsFolderName + "/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(AssetsFolderName + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCss(string path) =>
        CssExtensions.Contains(Path.GetExtension(path));

    private static bool IsJs(string path) =>
        JsExtensions.Contains(Path.GetExtension(path));

    private void EnsureInitialized()
    {
        if (!isInitialized)
        {
            throw new InvalidOperationException(
                "AssetResolver not initialized. Call Initialize() before accessing assets.");
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initializing asset resolver for theme '{ThemeName}'")]
    private partial void LogInitializing(string themeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Asset resolver initialized: {TotalCount} assets ({CssCount} CSS, {JsCount} JS)")]
    private partial void LogInitialized(int totalCount, int cssCount, int jsCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned theme '{ThemeName}': {Count} assets")]
    private partial void LogScannedTheme(string themeName, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned extension '{ExtensionName}' (prefix: {Prefix}): {Count} assets")]
    private partial void LogScannedExtension(string extensionName, string prefix, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned local overrides: {OverrideCount} overrides, {NewCount} new")]
    private partial void LogScannedLocal(int overrideCount, int newCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local asset override: '{Key}' → {Path}")]
    private partial void LogLocalOverride(string key, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local asset: '{Key}' → {Path}")]
    private partial void LogLocalNew(string key, string path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copying {Count} assets to '{OutputDir}'")]
    private partial void LogCopyingAssets(int count, string outputDir);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copied asset '{Key}' from {Source}")]
    private partial void LogCopiedAsset(string key, string source);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Asset not found: '{Key}' at path '{Path}'")]
    private partial void LogAssetNotFound(string key, string path);

    #endregion

    /// <inheritdoc />
    public IReadOnlyList<ResolvedFileInfo> GetAllEntries()
    {
        EnsureInitialized();

        return [.. assets.Select(kvp => new ResolvedFileInfo(
            kvp.Key,
            kvp.Value.Path,
            kvp.Value.SourceType switch
            {
                AssetSourceType.Theme => FileSourceType.Theme,
                AssetSourceType.Extension => FileSourceType.Extension,
                AssetSourceType.Local => FileSourceType.Local,
                _ => FileSourceType.Theme
            },
            kvp.Value.Extension?.Metadata.Name))];
    }
}

/// <summary>
/// Internal entry for resolved assets.
/// </summary>
internal sealed record AssetEntry(
    AssetSourceType SourceType,
    string Path,
    IThemeExtension? Extension);

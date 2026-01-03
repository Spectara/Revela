using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Resolves templates by scanning theme, extensions, and local overrides.
/// </summary>
/// <remarks>
/// <para>
/// Scans all sources once at initialization and builds a merged template dictionary.
/// Local overrides take priority over extensions, which take priority over theme.
/// </para>
/// <para>
/// Key derivation conventions:
/// - Theme: path relative to theme root, lowercase, no extension
/// - Extension: partialPrefix + "/" + filename (Body/Partials folders stripped)
/// - Local: path relative to themes/{ThemeName}/, lowercase, no extension
/// </para>
/// </remarks>
public sealed partial class TemplateResolver(ILogger<TemplateResolver> logger) : ITemplateResolver
{
    private const string LayoutFileName = "Layout.revela";
    private const string RevelaExtension = ".revela";

    private readonly Dictionary<string, TemplateEntry> templates = new(StringComparer.OrdinalIgnoreCase);
    private IThemePlugin? theme;
    private string? localThemePath;
    private bool isInitialized;

    /// <inheritdoc />
    public void Initialize(IThemePlugin theme, IReadOnlyList<IThemeExtension> extensions, string projectPath)
    {
        this.theme = theme;
        templates.Clear();

        var themeName = theme.Metadata.Name;
        localThemePath = Path.Combine(projectPath, ProjectPaths.Themes, themeName);

        LogInitializing(themeName);

        // 1. Scan base theme (lowest priority)
        ScanTheme(theme);

        // 2. Scan extensions (medium priority)
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
        LogInitialized(templates.Count);
    }

    /// <inheritdoc />
    public Stream? GetTemplate(string key)
    {
        EnsureInitialized();

        var normalizedKey = NormalizeKey(key);

        if (!templates.TryGetValue(normalizedKey, out var entry))
        {
            LogTemplateNotFound(normalizedKey);
            return null;
        }

        return entry.SourceType switch
        {
            TemplateSourceType.Local => File.OpenRead(entry.Path),
            TemplateSourceType.Theme => theme!.GetFile(entry.Path),
            TemplateSourceType.Extension => entry.Extension!.GetFile(entry.Path),
            _ => null
        };
    }

    /// <inheritdoc />
    public string GetLayoutPath() => LayoutFileName;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetResolvedTemplates()
    {
        EnsureInitialized();

        return templates.ToDictionary(
            kvp => kvp.Key,
            kvp => $"{kvp.Value.SourceType}: {kvp.Value.Path}");
    }

    /// <inheritdoc />
    public bool HasTemplate(string key)
    {
        EnsureInitialized();
        return templates.ContainsKey(NormalizeKey(key));
    }

    private void ScanTheme(IThemePlugin theme)
    {
        var count = 0;

        foreach (var file in theme.GetAllFiles())
        {
            if (!IsRevelaFile(file))
            {
                continue;
            }

            var key = DeriveKeyFromPath(file);
            LogScannedTemplate(key, file);
            templates[key] = new TemplateEntry(TemplateSourceType.Theme, file, null);
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
            if (!IsRevelaFile(file))
            {
                continue;
            }

            // Extension key = prefix + filename (strip Body/Partials folders)
            var key = DeriveExtensionKey(prefix, file);
            templates[key] = new TemplateEntry(TemplateSourceType.Extension, file, extension);
            count++;
        }

        LogScannedExtension(extension.Metadata.Name, prefix, count);
    }

    private void ScanLocalOverrides(string localPath)
    {
        var overrideCount = 0;
        var newCount = 0;

        foreach (var file in Directory.EnumerateFiles(localPath, $"*{RevelaExtension}", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(localPath, file);

            // Skip layout (handled separately)
            if (IsLayoutFile(relativePath))
            {
                continue;
            }

            var key = DeriveKeyFromPath(relativePath);
            var isOverride = templates.ContainsKey(key);

            templates[key] = new TemplateEntry(TemplateSourceType.Local, file, null);

            if (isOverride)
            {
                LogLocalOverride(key, file);
                overrideCount++;
            }
            else
            {
                LogLocalNew(key, file);
                newCount++;
            }
        }

        if (overrideCount > 0 || newCount > 0)
        {
            LogScannedLocal(overrideCount, newCount);
        }
    }

    /// <summary>
    /// Derives template key from file path.
    /// </summary>
    /// <remarks>
    /// Body/Gallery.revela → body/gallery
    /// Partials/Navigation.revela → partials/navigation
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Template keys use lowercase by convention - this is format conversion, not normalization")]
    private static string DeriveKeyFromPath(string path)
    {
        // Remove .revela extension
        var withoutExtension = Path.ChangeExtension(path, null);

        // Normalize separators and lowercase
        return withoutExtension
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    /// <summary>
    /// Derives template key for extension files.
    /// </summary>
    /// <remarks>
    /// Body/Overview.revela + prefix "statistics" → statistics/overview
    /// Partials/Cameras.revela + prefix "statistics" → statistics/cameras
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Template keys use lowercase by convention - this is format conversion, not normalization")]
    private static string DeriveExtensionKey(string prefix, string path)
    {
        // Get filename without extension
        var fileName = Path.GetFileNameWithoutExtension(path);

        // Combine prefix with filename (lowercase)
        return $"{prefix}/{fileName}".ToLowerInvariant();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Template keys use lowercase by convention - this is format conversion, not normalization")]
    private static string NormalizeKey(string key)
    {
        // Remove .revela extension if present
        if (key.EndsWith(RevelaExtension, StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^RevelaExtension.Length];
        }

        return key.Replace('\\', '/').ToLowerInvariant();
    }

    private static bool IsRevelaFile(string path) =>
        path.EndsWith(RevelaExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsLayoutFile(string path) =>
        Path.GetFileName(path).Equals(LayoutFileName, StringComparison.OrdinalIgnoreCase);

    private void EnsureInitialized()
    {
        if (!isInitialized)
        {
            throw new InvalidOperationException(
                "TemplateResolver not initialized. Call Initialize() before accessing templates.");
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Debug, Message = "Initializing template resolver for theme '{ThemeName}'")]
    private partial void LogInitializing(string themeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Template resolver initialized with {Count} templates")]
    private partial void LogInitialized(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned theme '{ThemeName}': {Count} templates")]
    private partial void LogScannedTheme(string themeName, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned template key '{Key}' from file '{FilePath}'")]
    private partial void LogScannedTemplate(string key, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned extension '{ExtensionName}' (prefix: {Prefix}): {Count} templates")]
    private partial void LogScannedExtension(string extensionName, string prefix, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Scanned local overrides: {OverrideCount} overrides, {NewCount} new")]
    private partial void LogScannedLocal(int overrideCount, int newCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local override: '{Key}' → {Path}")]
    private partial void LogLocalOverride(string key, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Local template: '{Key}' → {Path}")]
    private partial void LogLocalNew(string key, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Template not found: '{Key}' (check if required theme extension is installed)")]
    private partial void LogTemplateNotFound(string key);

    #endregion

    /// <inheritdoc />
    public IReadOnlyList<ResolvedFileInfo> GetAllEntries()
    {
        EnsureInitialized();

        return [.. templates.Select(kvp => new ResolvedFileInfo(
            kvp.Key,
            kvp.Value.Path,
            kvp.Value.SourceType switch
            {
                TemplateSourceType.Theme => FileSourceType.Theme,
                TemplateSourceType.Extension => FileSourceType.Extension,
                TemplateSourceType.Local => FileSourceType.Local,
                _ => FileSourceType.Theme
            },
            kvp.Value.Extension?.Metadata.Name))];
    }
}

/// <summary>
/// Internal entry for resolved templates.
/// </summary>
internal sealed record TemplateEntry(
    TemplateSourceType SourceType,
    string Path,
    IThemeExtension? Extension);

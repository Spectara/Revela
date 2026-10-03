using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text.Json;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Json;

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

    private static readonly FrozenSet<string> CssExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".css"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> JsExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ResolvedEntry> assets = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> styleSheetOrder = [];
    private readonly List<string> scriptOrder = [];

    // Asset key -> declared scope tokens (lowercased). Assets without a declaration
    // remain available for copying and inspection but are not linked on scoped pages.
    private readonly Dictionary<string, string[]> assetScopes = new(StringComparer.OrdinalIgnoreCase);

    private const string AllScope = "all";

    // Hex characters of the SHA-256 kept in versioned asset URLs: short, yet a collision
    // between two versions of the same file is a one-in-four-billion event.
    private const int FingerprintLength = 8;

    // Asset key -> fingerprint (null for unknown paths). Pages render in parallel, so the
    // cache is concurrent; Initialize resets it so every render run hashes current bytes.
    private readonly ConcurrentDictionary<string, string?> fingerprints = new(StringComparer.OrdinalIgnoreCase);

    private string? localThemePath;
    private bool isInitialized;

    /// <inheritdoc />
    public void Initialize(ITheme theme, IReadOnlyList<ITheme> extensions, string projectPath)
    {
        assets.Clear();
        styleSheetOrder.Clear();
        scriptOrder.Clear();
        assetScopes.Clear();
        fingerprints.Clear();

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

        RegisterProjectAssetScopes(projectPath);

        isInitialized = true;
        LogInitialized(assets.Count, styleSheetOrder.Count, scriptOrder.Count);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetStyleSheets() => GetScopedAssets(styleSheetOrder, scope: null);

    /// <inheritdoc />
    public IReadOnlyList<string> GetStyleSheets(string? scope) => GetScopedAssets(styleSheetOrder, scope);

    /// <inheritdoc />
    public IReadOnlyList<string> GetScripts() => GetScopedAssets(scriptOrder, scope: null);

    /// <inheritdoc />
    public IReadOnlyList<string> GetScripts(string? scope) => GetScopedAssets(scriptOrder, scope);

    private IReadOnlyList<string> GetScopedAssets(List<string> assetOrder, string? scope)
    {
        EnsureInitialized();

        if (string.IsNullOrEmpty(scope))
        {
            return assetOrder.AsReadOnly();
        }

        var result = new List<string>(assetOrder.Count);
        foreach (var asset in assetOrder)
        {
            // Scoped page rendering is explicit: undeclared assets are not linked.
            if (!assetScopes.TryGetValue(asset, out var scopes))
            {
                continue;
            }

            foreach (var declared in scopes)
            {
                if (declared.Equals(AllScope, StringComparison.OrdinalIgnoreCase)
                    || declared.Equals(scope, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(asset);
                    break;
                }
            }
        }

        return result.AsReadOnly();
    }

    /// <inheritdoc />
    public string? GetFingerprint(string path)
    {
        EnsureInitialized();

        return fingerprints.GetOrAdd(DeriveKeyFromLocalPath(path.Trim('/', '\\')), ComputeFingerprint);
    }

    private string? ComputeFingerprint(string key)
    {
        using var stream = assets.TryGetValue(key, out var entry) ? GetAssetStream(entry) : null;
        if (stream is null)
        {
            LogFingerprintUnavailable(key);
            return null;
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(stream, hash);
        return Convert.ToHexStringLower(hash[..(FingerprintLength / 2)]);
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
        ResolvedEntry entry,
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

    private void ScanTheme(ITheme theme)
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
            assets[key] = new ResolvedEntry(FileSourceType.Theme, file, null, () => theme.GetFile(file));
            TrackOrderedAsset(key);
            count++;
        }

        RegisterAssetScopes(theme, prefix: null);

        LogScannedTheme(theme.Metadata.Name, count);
    }

    private void ScanExtension(ITheme extension)
    {
        var prefix = extension.Prefix ?? string.Empty;
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
            assets[key] = new ResolvedEntry(FileSourceType.Extension, file, extension, () => extension.GetFile(file));
            TrackOrderedAsset(key);
            count++;
        }

        RegisterAssetScopes(extension, prefix);

        LogScannedExtension(extension.Metadata.Name, prefix, count);
    }

    /// <summary>
    /// Records page-type scope declarations from a theme/extension manifest.
    /// The declared asset path is resolved to the same asset key produced by
    /// <see cref="DeriveKeyFromPath"/> / <see cref="DeriveExtensionKey"/> so it lines
    /// up with the corresponding asset order. A declaration with no scope tokens loads
    /// everywhere (stored as the <c>all</c> token).
    /// </summary>
    private void RegisterAssetScopes(ITheme theme, string? prefix)
    {
        RegisterAssetScopes(theme.Manifest.Stylesheets, prefix);
        RegisterAssetScopes(theme.Manifest.Scripts, prefix);
    }

    private void RegisterAssetScopes(
        IReadOnlyList<AssetDeclaration>? declarations,
        string? prefix)
    {
        if (declarations is null)
        {
            return;
        }

        foreach (var declaration in declarations)
        {
            if (string.IsNullOrWhiteSpace(declaration.Path))
            {
                continue;
            }

            var relativeKey = DeriveKeyFromPath(declaration.Path);
            var key = string.IsNullOrEmpty(prefix) ? relativeKey : $"{prefix}/{relativeKey}".ToLowerInvariant();

            var scopes = declaration.Scope is { Count: > 0 }
                ? declaration.Scope.Select(s => s.Trim()).Where(s => s.Length > 0).ToArray()
                : [AllScope];

            assetScopes[key] = scopes.Length > 0 ? scopes : [AllScope];
        }
    }

    private void RegisterProjectAssetScopes(string projectPath)
    {
        var siteJsonPath = Path.Combine(projectPath, "site.json");
        if (!File.Exists(siteJsonPath))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(siteJsonPath),
                RevelaJsonOptions.LenientDocument);
            RegisterProjectAssetScopes(document.RootElement, "stylesheets");
            RegisterProjectAssetScopes(document.RootElement, "scripts");
        }
        catch (JsonException)
        {
            // site.json validation is owned by the render pipeline.
        }
    }

    private void RegisterProjectAssetScopes(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var entries)
            || entries.ValueKind is not JsonValueKind.Array)
        {
            return;
        }

        var declarations = new List<AssetDeclaration>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind is JsonValueKind.String)
            {
                declarations.Add(new AssetDeclaration { Path = entry.GetString() });
                continue;
            }

            if (entry.ValueKind is not JsonValueKind.Object
                || !TryGetProperty(entry, "path", out var pathElement)
                || pathElement.ValueKind is not JsonValueKind.String)
            {
                continue;
            }

            IReadOnlyList<string>? scopes = null;
            if (TryGetProperty(entry, "scope", out var scopeElement)
                && scopeElement.ValueKind is JsonValueKind.Array)
            {
                scopes =
                [
                    .. scopeElement.EnumerateArray()
                        .Where(value => value.ValueKind is JsonValueKind.String)
                        .Select(value => value.GetString())
                        .OfType<string>()
                ];
            }

            declarations.Add(new AssetDeclaration
            {
                Path = pathElement.GetString(),
                Scope = scopes
            });
        }

        RegisterAssetScopes(declarations, prefix: null);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
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

            assets[key] = new ResolvedEntry(
                FileSourceType.Local, file, null,
                () => File.Exists(file) ? File.OpenRead(file) : null);

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

    private static Stream? GetAssetStream(ResolvedEntry entry) => entry.StreamFactory();

    /// <summary>
    /// Derives asset key from theme file path.
    /// Assets/main.css → main.css
    /// Assets/fonts/inter.woff2 → fonts/inter.woff2
    /// </summary>
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "No asset '{Key}' to fingerprint; its URL stays unversioned")]
    private partial void LogFingerprintUnavailable(string key);

    #endregion

    /// <inheritdoc />
    public IReadOnlyList<ResolvedFileInfo> GetAllEntries()
    {
        EnsureInitialized();

        return [.. assets.Select(kvp => new ResolvedFileInfo(
            kvp.Key,
            kvp.Value.Path,
            kvp.Value.SourceType,
            kvp.Value.Extension?.Metadata.Name))];
    }
}

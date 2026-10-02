using System.Globalization;
using System.Text.Json;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Json;

namespace Spectara.Revela.Core.Themes;

/// <summary>
/// Loads theme UI strings from <c>Locales/&lt;language&gt;.json</c> files.
/// </summary>
/// <remarks>
/// <para>
/// Each theme and theme extension may ship flat <c>{ "key": "text" }</c> files in
/// <c>Locales/</c>. Files are merged key by key; later layers win:
/// base theme → extensions → project-local <c>themes/&lt;Theme&gt;/Locales/&lt;lang&gt;.json</c>
/// → project-local <c>themes/&lt;Theme&gt;/Locales/&lt;Prefix&gt;/&lt;lang&gt;.json</c>
/// (the paths <c>revela theme extract</c> writes for theme and extension files).
/// </para>
/// <para>
/// Language resolution tries the exact site language, then its parents, then
/// <see cref="FallbackLanguage"/> (<c>de-CH → de → en</c>).
/// </para>
/// </remarks>
public static class ThemeLocales
{
    /// <summary>Folder holding locale files inside a theme.</summary>
    public const string Folder = "Locales";

    /// <summary>Language every theme must provide and every lookup falls back to.</summary>
    public const string FallbackLanguage = "en";

    private const string KeyPrefix = "locales/";
    private const string FileExtension = ".json";

    /// <summary>
    /// Builds the lookup order for a site language: exact, parent cultures, then <c>en</c>.
    /// </summary>
    /// <example><c>"de-CH"</c> → <c>["de-CH", "de", "en"]</c></example>
    public static IReadOnlyList<string> GetLanguageChain(string? language)
    {
        var chain = new List<string>();
        var current = language?.Trim().Replace('_', '-');

        while (!string.IsNullOrEmpty(current))
        {
            if (!chain.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                chain.Add(current);
            }

            var separator = current.LastIndexOf('-');
            current = separator > 0 ? current[..separator] : null;
        }

        if (!chain.Contains(FallbackLanguage, StringComparer.OrdinalIgnoreCase))
        {
            chain.Add(FallbackLanguage);
        }

        return chain;
    }

    /// <summary>
    /// Resolves the formatting culture for a site language, or the invariant culture when unknown.
    /// </summary>
    public static CultureInfo ResolveCulture(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language.Trim().Replace('_', '-'), predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// Loads and merges the theme strings for <paramref name="language"/>.
    /// </summary>
    /// <param name="theme">Base theme (lowest priority).</param>
    /// <param name="extensions">Theme extensions (override the base theme).</param>
    /// <param name="projectPath">Project root; local overrides live in <c>themes/&lt;Theme&gt;/Locales/</c>.</param>
    /// <param name="language">Site language (e.g. <c>de-CH</c>); blank means <c>en</c>.</param>
    /// <param name="logger">Logger for missing-key diagnostics.</param>
    /// <exception cref="InvalidOperationException">A locale file is not a flat JSON object of strings.</exception>
    public static ThemeStrings Load(
        ITheme theme,
        IReadOnlyList<ITheme> extensions,
        string projectPath,
        string? language,
        ILogger logger)
    {
        var requested = string.IsNullOrWhiteSpace(language) ? FallbackLanguage : language.Trim();
        var localFolder = Path.Combine(projectPath, ProjectPaths.Themes, theme.Metadata.Name, Folder);
        var themeFiles = ListLocaleFiles(theme);
        var extensionFiles = extensions.Select(extension => (extension, Files: ListLocaleFiles(extension))).ToList();

        var chain = new List<(string Language, IReadOnlyDictionary<string, string> Strings)>();
        foreach (var chainLanguage in GetLanguageChain(requested))
        {
            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            var fileName = chainLanguage + FileExtension;

            ReadThemeFile(strings, theme, themeFiles, fileName);
            foreach (var (extension, files) in extensionFiles)
            {
                ReadThemeFile(strings, extension, files, fileName);
            }

            ReadLocalFile(strings, localFolder, fileName);
            foreach (var extension in extensions.Where(e => !string.IsNullOrEmpty(e.Prefix)))
            {
                ReadLocalFile(strings, FindDirectory(localFolder, extension.Prefix!), fileName);
            }

            if (strings.Count > 0)
            {
                chain.Add((chainLanguage, strings));
            }
        }

        return new ThemeStrings(requested, chain, ResolveCulture(requested), logger);
    }

    /// <summary>
    /// Lists the locale files shipped by a theme and its extensions for <c>theme files</c>/<c>theme extract</c>.
    /// </summary>
    /// <remarks>
    /// Keys are <c>locales/&lt;file&gt;</c> for the base theme and <c>locales/&lt;prefix&gt;/&lt;file&gt;</c>
    /// for extensions, so equally named files never collide. <see cref="ResolvedFileInfo.OriginalPath"/>
    /// is the path inside the owning theme; <see cref="GetLocalPath"/> maps a key to its override path.
    /// </remarks>
    public static IReadOnlyList<ResolvedFileInfo> GetEntries(ITheme theme, IReadOnlyList<ITheme> extensions)
    {
        var entries = new List<ResolvedFileInfo>();

        foreach (var path in ListLocaleFiles(theme))
        {
            entries.Add(new ResolvedFileInfo(KeyPrefix + Path.GetFileName(path), path, FileSourceType.Theme, null));
        }

        foreach (var extension in extensions)
        {
            var prefix = extension.Prefix ?? string.Empty;
            foreach (var path in ListLocaleFiles(extension))
            {
                var key = prefix.Length > 0
                    ? $"{KeyPrefix}{prefix}/{Path.GetFileName(path)}"
                    : KeyPrefix + Path.GetFileName(path);
                entries.Add(new ResolvedFileInfo(key, path, FileSourceType.Extension, extension.Metadata.Name));
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns whether <paramref name="key"/> names a locale entry from <see cref="GetEntries"/>.
    /// </summary>
    public static bool IsLocaleKey(string key) =>
        key.Replace('\\', '/').StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a locale key to its project-local override path relative to <c>themes/&lt;Theme&gt;/</c>.
    /// </summary>
    /// <example>
    /// <c>locales/de.json</c> → <c>Locales/de.json</c>;
    /// <c>locales/statistics/de.json</c> → <c>Locales/Statistics/de.json</c>
    /// </example>
    public static string GetLocalPath(string key)
    {
        var segments = key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string> { Folder };
        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            parts.Add(i < segments.Length - 1 && segment.Length > 0
                ? char.ToUpperInvariant(segment[0]) + segment[1..]
                : segment);
        }

        return Path.Combine([.. parts]);
    }

    private static List<string> ListLocaleFiles(ITheme theme) =>
        [.. theme.GetAllFiles()
            .Select(file => file.Replace('\\', '/'))
            .Where(file => file.StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase)
                && file.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
                && file.IndexOf('/', Folder.Length + 1) < 0)];

    private static void ReadThemeFile(Dictionary<string, string> strings, ITheme theme, List<string> files, string fileName)
    {
        var path = files.FirstOrDefault(file => Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (path is null)
        {
            return;
        }

        using var stream = theme.GetFile(path);
        if (stream is not null)
        {
            Merge(strings, stream, $"{theme.Metadata.Name}/{path}");
        }
    }

    private static void ReadLocalFile(Dictionary<string, string> strings, string? folder, string fileName)
    {
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }

        var path = Directory.EnumerateFiles(folder, "*" + FileExtension)
            .FirstOrDefault(file => Path.GetFileName(file).Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (path is null)
        {
            return;
        }

        using var stream = File.OpenRead(path);
        Merge(strings, stream, path);
    }

    private static string? FindDirectory(string parent, string name) =>
        Directory.Exists(parent)
            ? Directory.EnumerateDirectories(parent)
                .FirstOrDefault(dir => Path.GetFileName(dir).Equals(name, StringComparison.OrdinalIgnoreCase))
            : null;

    private static void Merge(Dictionary<string, string> strings, Stream stream, string source)
    {
        try
        {
            using var document = JsonDocument.Parse(stream, RevelaJsonOptions.LenientDocument);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Locale file '{source}' must contain a JSON object of \"key\": \"text\" pairs.");
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException($"Locale file '{source}': value of '{property.Name}' must be a string.");
                }

                strings[property.Name] = property.Value.GetString()!;
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Locale file '{source}' is not valid JSON: {exception.Message}", exception);
        }
    }
}

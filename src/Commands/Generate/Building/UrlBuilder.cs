using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Spectara.Revela.Commands.Generate.Building;

/// <summary>
/// Provides URL and slug generation utilities for gallery paths
/// </summary>
/// <remarks>
/// <para>
/// Creates URL-safe slugs from folder and file names:
/// </para>
/// <list type="bullet">
///   <item><description>Removes sort prefixes ("01 Events" → "events")</description></item>
///   <item><description>Converts to lowercase</description></item>
///   <item><description>Replaces spaces and special chars with hyphens</description></item>
///   <item><description>Removes consecutive hyphens</description></item>
/// </list>
/// </remarks>
internal static partial class UrlBuilder
{
    /// <summary>
    /// Characters to replace with hyphens in slugs
    /// </summary>
    [GeneratedRegex(@"[\s_]+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Characters not allowed in URL slugs
    /// </summary>
    [GeneratedRegex(@"[^a-z0-9\-]", RegexOptions.Compiled)]
    private static partial Regex InvalidCharsRegex();

    /// <summary>
    /// Multiple consecutive hyphens
    /// </summary>
    [GeneratedRegex(@"\-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleHyphensRegex();

    /// <summary>
    /// Converts a folder or file name to a URL-safe slug
    /// </summary>
    /// <param name="name">Original name (e.g., "01 Events")</param>
    /// <returns>URL-safe slug (e.g., "events")</returns>
    /// <example>
    /// <code>
    /// ToSlug("01 Events")           // → "events"
    /// ToSlug("2024 Summer Trip")    // → "2024-summer-trip"
    /// ToSlug("Wedding or Party")    // → "wedding-or-party"
    /// ToSlug("Café Photos")         // → "cafe-photos"
    /// </code>
    /// </example>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "URL slugs must be lowercase per web standards")]
    public static string ToSlug(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // 1. Extract display name (remove sort prefix)
        var displayName = GallerySorter.ExtractDisplayName(name);

        // 2. Normalize unicode and convert to lowercase
        var normalized = displayName
            .Normalize(NormalizationForm.FormD)
            .ToLowerInvariant();

        // 3. Remove diacritics (accents)
        var withoutDiacritics = RemoveDiacritics(normalized);

        // 4. Replace whitespace and underscores with hyphens
        var withHyphens = WhitespaceRegex().Replace(withoutDiacritics, "-");

        // 5. Remove invalid characters
        var cleaned = InvalidCharsRegex().Replace(withHyphens, string.Empty);

        // 6. Remove consecutive hyphens
        var final = MultipleHyphensRegex().Replace(cleaned, "-");

        // 7. Trim leading/trailing hyphens
        return final.Trim('-');
    }

    /// <summary>
    /// Builds a relative URL path from path segments
    /// </summary>
    /// <param name="segments">Path segments (will be slugified)</param>
    /// <returns>Relative URL path with trailing slash</returns>
    /// <example>
    /// <code>
    /// BuildPath("01 Events", "2024 Wedding")  // → "events/2024-wedding/"
    /// BuildPath("Photos")                      // → "photos/"
    /// BuildPath()                              // → ""
    /// </code>
    /// </example>
    public static string BuildPath(params string[] segments)
    {
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var slugs = segments
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(ToSlug);

        var path = string.Join("/", slugs);
        return string.IsNullOrEmpty(path) ? string.Empty : path + "/";
    }

    /// <summary>
    /// Calculates the basepath (relative root) for a given path depth
    /// </summary>
    /// <param name="path">Current page path (e.g., "events/2024/")</param>
    /// <returns>Relative path to root (e.g., "../../")</returns>
    /// <remarks>
    /// Used in templates to reference root-relative assets:
    /// <code>
    /// &lt;link href="{{ basepath }}css/style.css"&gt;
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// CalculateBasePath("events/")           // → "../"
    /// CalculateBasePath("events/2024/")      // → "../../"
    /// CalculateBasePath("")                  // → ""
    /// CalculateBasePath("index.html")        // → ""
    /// </code>
    /// </example>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1054:URI-like parameters should not be strings",
        Justification = "path is a relative path segment used in templates, not a full URI")]
    public static string CalculateBasePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        // Count directory depth (number of slashes, excluding trailing)
        var trimmed = path.TrimEnd('/');
        var depth = trimmed.Count(c => c == '/') + (trimmed.Length > 0 ? 1 : 0);

        if (depth == 0)
        {
            return string.Empty;
        }

        return string.Concat(Enumerable.Repeat("../", depth));
    }

    /// <summary>
    /// Removes diacritical marks (accents) from characters
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        var sb = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Converts a folder name to a display title
    /// </summary>
    /// <param name="name">Original folder name (e.g., "01 Events")</param>
    /// <returns>Human-readable title (e.g., "Events")</returns>
    /// <example>
    /// <code>
    /// ToTitle("01 Events")           // → "Events"
    /// ToTitle("2024 Summer Trip")    // → "2024 Summer Trip"
    /// ToTitle("About")               // → "About"
    /// </code>
    /// </example>
    public static string ToTitle(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return GallerySorter.ExtractDisplayName(name);
    }
}

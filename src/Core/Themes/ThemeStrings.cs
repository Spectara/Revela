using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace Spectara.Revela.Core.Themes;

/// <summary>
/// Theme UI strings for one site language, merged from all theme layers.
/// </summary>
/// <remarks>
/// <para>
/// Lookup walks the language chain built by <see cref="ThemeLocales.GetLanguageChain"/>
/// (e.g. <c>de-CH → de → en</c>); a key missing everywhere renders as the key itself so
/// gaps stay visible. Placeholders <c>{0}</c>, <c>{1}</c>, … are replaced with the
/// culture-invariant text of the arguments.
/// </para>
/// <para>
/// The returned text is plain text, not trusted HTML — templates must escape it.
/// </para>
/// </remarks>
public sealed partial class ThemeStrings
{
    private readonly IReadOnlyList<(string Language, IReadOnlyDictionary<string, string> Strings)> chain;
    private readonly ILogger logger;
    private readonly ConcurrentDictionary<string, byte> reportedKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates theme strings from per-language dictionaries in lookup order.
    /// </summary>
    /// <param name="language">The requested site language.</param>
    /// <param name="chain">Language dictionaries in lookup order (most specific first, <c>en</c> last).</param>
    /// <param name="culture">Culture used for date/number formatting.</param>
    /// <param name="logger">Logger for missing-key diagnostics.</param>
    public ThemeStrings(
        string language,
        IReadOnlyList<(string Language, IReadOnlyDictionary<string, string> Strings)> chain,
        CultureInfo culture,
        ILogger logger)
    {
        Language = language;
        this.chain = chain;
        Culture = culture;
        this.logger = logger;
    }

    /// <summary>
    /// Strings without any locale files: every key renders as itself, formatting is invariant.
    /// </summary>
    public static ThemeStrings Empty { get; } = new(
        ThemeLocales.FallbackLanguage,
        [],
        CultureInfo.InvariantCulture,
        NullLogger.Instance);

    /// <summary>
    /// The requested site language (e.g. <c>de-CH</c>).
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Culture for date and number formatting (invariant when the language is unknown).
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Returns the text for <paramref name="key"/> with <c>{n}</c> placeholders replaced.
    /// </summary>
    /// <param name="key">Namespaced key, e.g. <c>photo.close</c>.</param>
    /// <param name="args">Positional placeholder values.</param>
    public string Translate(string key, IReadOnlyList<object?> args)
    {
        var text = Lookup(key);
        return args.Count == 0 ? text : Substitute(text, args);
    }

    private string Lookup(string key)
    {
        foreach (var (language, strings) in chain)
        {
            if (!strings.TryGetValue(key, out var text))
            {
                continue;
            }

            if (language.Equals(ThemeLocales.FallbackLanguage, StringComparison.OrdinalIgnoreCase)
                && !Language.Equals(ThemeLocales.FallbackLanguage, StringComparison.OrdinalIgnoreCase)
                && reportedKeys.TryAdd(key, 0))
            {
                LogFallbackKey(logger, key, Language);
            }

            return text;
        }

        if (reportedKeys.TryAdd(key, 0))
        {
            LogMissingKey(logger, key, Language);
        }

        return key;
    }

    /// <summary>
    /// Single-pass placeholder replacement: argument values are never re-scanned,
    /// and malformed or out-of-range placeholders stay literal instead of throwing.
    /// </summary>
    private static string Substitute(string text, IReadOnlyList<object?> args)
    {
        var builder = new StringBuilder(text.Length + 32);
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf('{', index);
            if (open < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            var close = text.IndexOf('}', open + 1);
            if (close > open + 1
                && int.TryParse(text.AsSpan(open + 1, close - open - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var position)
                && position < args.Count)
            {
                builder.Append(text, index, open - index);
                builder.Append(Convert.ToString(args[position], CultureInfo.InvariantCulture));
                index = close + 1;
            }
            else
            {
                builder.Append(text, index, open - index + 1);
                index = open + 1;
            }
        }

        return builder.ToString();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme string '{Key}' is not translated for language '{Language}'; using 'en'")]
    private static partial void LogFallbackKey(ILogger logger, string key, string language);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme string '{Key}' is missing for language '{Language}'; rendering the key")]
    private static partial void LogMissingKey(ILogger logger, string key, string language);
}

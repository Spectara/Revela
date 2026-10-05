using System.Diagnostics.CodeAnalysis;

namespace Spectara.Revela.Core.Helpers;

/// <summary>
/// Produces log-safe forms of package sources and feed URLs.
/// </summary>
/// <remarks>
/// Feed URLs can carry credentials as user info (<c>https://user:token@host/</c>) or as signed
/// query strings (<c>?sig=…</c>). Only scheme, host, port and path are kept. Values that are not
/// network URLs (feed names, package IDs, local or UNC paths) are returned unchanged.
/// </remarks>
public static class UrlRedaction
{
    private const string SchemeSeparator = "://";
    private const string RedactedPlaceholder = "<redacted>";

    /// <summary>
    /// Returns <paramref name="value"/> without user info, query and fragment when it is a network URL.
    /// </summary>
    /// <param name="value">A feed URL, package URL, local path or feed name.</param>
    /// <returns>
    /// The redacted URL; <paramref name="value"/> itself when there is nothing to redact; or
    /// <c>scheme://&lt;redacted&gt;</c> when it looks like a URL but cannot be parsed.
    /// </returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile || (uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0))
            {
                return value;
            }

            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
        }

        var separator = value.IndexOf(SchemeSeparator, StringComparison.Ordinal);
        return separator > 0
            ? string.Concat(value.AsSpan(0, separator + SchemeSeparator.Length), RedactedPlaceholder)
            : value;
    }
}

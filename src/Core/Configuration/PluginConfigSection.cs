using System.Diagnostics.CodeAnalysis;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Rules for the host-owned <c>plugins</c> configuration node that holds all plugin settings.
/// </summary>
/// <remarks>
/// <para>
/// Every plugin stores its settings under <c>plugins:&lt;key&gt;</c> and declares that
/// section on its config class (<c>[RevelaConfig("plugins:serve")]</c> plus
/// <c>public const string Section = "plugins:serve";</c>). The key must match
/// <c>^[a-z][a-zA-Z0-9]*$</c> — camelCase letters and digits, no <c>.</c>, <c>:</c>,
/// <c>/</c> or <c>_</c> — so it maps cleanly to environment variables
/// (<c>SPECTARA__REVELA__PLUGINS__SERVE__PORT</c>). The SDK source generator enforces
/// this at compile time for plugin and theme assemblies (REVELA001); the host uses these
/// rules to resolve ownership and to write a plugin's own settings.
/// </para>
/// </remarks>
public static class PluginConfigSection
{
    /// <summary>Name of the host-owned root node for plugin settings.</summary>
    public const string Root = "plugins";

    /// <summary>Section prefix every plugin section starts with.</summary>
    public const string Prefix = Root + ":";

    /// <summary>
    /// Checks whether <paramref name="key"/> is a valid plugin key (<c>^[a-z][a-zA-Z0-9]*$</c>).
    /// </summary>
    /// <param name="key">The candidate key.</param>
    /// <returns><see langword="true"/> when the key is valid.</returns>
    public static bool IsValidKey([NotNullWhen(true)] string? key)
    {
        if (string.IsNullOrEmpty(key) || !char.IsAsciiLetterLower(key[0]))
        {
            return false;
        }

        foreach (var c in key.AsSpan(1))
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extracts the plugin key from a section of the form <c>plugins:&lt;key&gt;</c>.
    /// </summary>
    /// <param name="section">The configuration section (e.g. <c>plugins:serve</c>).</param>
    /// <param name="key">The plugin key when the section is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="section"/> is a valid plugin section.</returns>
    public static bool TryGetKey(string? section, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (section is null || !section.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = section[Prefix.Length..];
        if (!IsValidKey(candidate))
        {
            return false;
        }

        key = candidate;
        return true;
    }
}

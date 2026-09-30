using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;

namespace Spectara.Revela.Sdk.Configuration;

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
/// this at compile time for plugin and theme assemblies.
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

    /// <summary>
    /// Wraps plugin settings into a <c>project.json</c> update object
    /// (<c>{ "plugins": { "&lt;key&gt;": settings } }</c>) for <see cref="Abstractions.IConfigService.UpdateProjectConfigAsync"/>.
    /// </summary>
    /// <param name="section">The plugin section (e.g. <c>plugins:serve</c>).</param>
    /// <param name="settings">The settings to write below the plugin key.</param>
    /// <returns>The nested update object.</returns>
    /// <exception cref="ArgumentException"><paramref name="section"/> is not a valid plugin section.</exception>
    public static JsonObject CreateUpdate(string section, JsonObject settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!TryGetKey(section, out var key))
        {
            throw new ArgumentException(
                $"'{section}' is not a plugin configuration section (expected '{Prefix}<key>').",
                nameof(section));
        }

        return new JsonObject { [Root] = new JsonObject { [key] = settings } };
    }
}

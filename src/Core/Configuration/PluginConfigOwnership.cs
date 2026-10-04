using System.Buffers;
using System.Collections.Frozen;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Records which loaded package owns which key below the host-owned <c>plugins</c> node.
/// </summary>
/// <remarks>
/// <para>
/// Claims come from the <see cref="RevelaPluginConfigKeyAttribute"/> that the SDK source
/// generator emits into every plugin/theme assembly declaring a <c>plugins:&lt;key&gt;</c>
/// section. They are read before any plugin configures services, so a duplicate claim
/// stops plugin loading instead of letting two packages bind the same settings.
/// </para>
/// <para>
/// A claim also records the claiming assembly, so the plugin settings writer can check that a
/// plugin only writes the key its own assembly claims. Reading another plugin's node is
/// prevented at compile time (REVELA003 and the SDK's banned configuration APIs).
/// Keys are compared case-insensitively because <see cref="IConfiguration"/> is.
/// </para>
/// </remarks>
public sealed class PluginConfigOwnership
{
    private static readonly SearchValues<char> KeyCharacters =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

    private PluginConfigOwnership(FrozenDictionary<string, PluginConfigClaim> owners) => Owners = owners;

    /// <summary>
    /// Claimed keys mapped to the owning claim (package ID and assembly).
    /// </summary>
    public FrozenDictionary<string, PluginConfigClaim> Owners { get; }

    /// <summary>
    /// Collects the claims of all loaded packages.
    /// </summary>
    /// <param name="packages">Loaded plugins and themes.</param>
    /// <returns>The resolved ownership.</returns>
    /// <exception cref="PluginConfigConflictException">Two packages claim the same key.</exception>
    public static PluginConfigOwnership FromPackages(IEnumerable<IPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var claims = new List<PluginConfigClaim>();
        var seenAssemblies = new HashSet<Assembly>();

        foreach (var package in packages)
        {
            // Claims belong to the assembly; count each assembly once.
            var assembly = package.GetType().Assembly;
            if (!seenAssemblies.Add(assembly))
            {
                continue;
            }

            foreach (var attribute in assembly.GetCustomAttributes<RevelaPluginConfigKeyAttribute>())
            {
                claims.Add(new PluginConfigClaim(attribute.Key, package.Metadata.Id, assembly));
            }
        }

        return FromClaims(claims);
    }

    /// <summary>
    /// Resolves ownership from explicit claims.
    /// </summary>
    /// <param name="claims">Key claims per package.</param>
    /// <returns>The resolved ownership.</returns>
    /// <exception cref="PluginConfigConflictException">Two packages claim the same key.</exception>
    public static PluginConfigOwnership FromClaims(IEnumerable<PluginConfigClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var owners = new Dictionary<string, PluginConfigClaim>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in claims)
        {
            if (owners.TryGetValue(claim.Key, out var existing))
            {
                if (!string.Equals(existing.PackageId, claim.PackageId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new PluginConfigConflictException(claim.Key, existing.PackageId, claim.PackageId);
                }

                continue;
            }

            owners[claim.Key] = claim;
        }

        return new PluginConfigOwnership(owners.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the claim on <paramref name="key"/>, if any package claims it.
    /// </summary>
    /// <param name="key">The plugin key (e.g. <c>serve</c>), compared case-insensitively.</param>
    /// <returns>The claim, or <see langword="null"/> when no loaded package claims the key.</returns>
    public PluginConfigClaim? FindClaim(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Owners.GetValueOrDefault(key);
    }

    /// <summary>
    /// Finds keys below <c>plugins</c> that no loaded package claims.
    /// </summary>
    /// <remarks>
    /// Only key-shaped children (ASCII letters and digits, starting with a letter) are
    /// considered. Anything else — for example package IDs such as
    /// <c>Spectara.Revela.Plugins.Serve</c> that share the <c>plugins</c> node with the
    /// dependency map — can never be a plugin key and is ignored. The first-letter case is
    /// not enforced because environment variables arrive upper-cased.
    /// </remarks>
    /// <param name="configuration">The merged configuration.</param>
    /// <returns>Unclaimed keys with the closest claimed key, if reasonably close.</returns>
    public IReadOnlyList<UnclaimedPluginConfigKey> FindUnclaimedKeys(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var unclaimed = new List<UnclaimedPluginConfigKey>();
        foreach (var child in configuration.GetSection(PluginConfigSection.Root).GetChildren())
        {
            if (!IsKeyShaped(child.Key) || Owners.ContainsKey(child.Key))
            {
                continue;
            }

            unclaimed.Add(new UnclaimedPluginConfigKey(child.Key, FindClosestKey(child.Key)));
        }

        return unclaimed;
    }

    private static bool IsKeyShaped(string key) =>
        key.Length > 0 &&
        char.IsAsciiLetter(key[0]) &&
        key.AsSpan(1).IndexOfAnyExcept(KeyCharacters) < 0;

    private string? FindClosestKey(string key)
    {
        string? best = null;
        var bestDistance = int.MaxValue;

        foreach (var candidate in Owners.Keys)
        {
            var distance = EditDistance(key, candidate);
            var threshold = Math.Clamp(candidate.Length / 3, 1, 2);
            if (distance <= threshold && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int EditDistance(string source, string target)
    {
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];

        for (var j = 0; j <= target.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = char.ToUpperInvariant(source[i - 1]) == char.ToUpperInvariant(target[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }
}

/// <summary>
/// A package's claim on a key below <c>plugins</c>.
/// </summary>
/// <param name="Key">The claimed key (e.g. <c>serve</c>).</param>
/// <param name="PackageId">The claiming package ID.</param>
/// <param name="Assembly">The assembly that carries the claim (declares the <c>[RevelaConfig]</c> type).</param>
public sealed record PluginConfigClaim(string Key, string PackageId, Assembly Assembly);

/// <summary>
/// A key below <c>plugins</c> that no loaded package claims.
/// </summary>
/// <param name="Key">The configured key.</param>
/// <param name="Suggestion">The closest claimed key, or <see langword="null"/> when none is close.</param>
public sealed record UnclaimedPluginConfigKey(string Key, string? Suggestion);

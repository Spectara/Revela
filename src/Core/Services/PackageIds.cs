using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Core.Services;

/// <summary>
/// Naming rules for Revela package IDs and NuGet package types.
/// </summary>
public static class PackageIds
{
    /// <summary>
    /// NuGet package type declared by theme packages (base themes and theme extensions).
    /// </summary>
    public const string ThemePackageType = InstalledPackage.ThemePackageType;

    /// <summary>
    /// NuGet package type declared by plugin packages.
    /// </summary>
    public const string PluginPackageType = "RevelaPlugin";

    /// <summary>
    /// ID prefix of official theme packages.
    /// </summary>
    public const string OfficialThemePrefix = PackageTrustPolicy.OfficialPackagePrefix + "Themes.";

    /// <summary>
    /// ID prefix of official plugin packages.
    /// </summary>
    public const string OfficialPluginPrefix = PackageTrustPolicy.OfficialPackagePrefix + "Plugins.";

    /// <summary>
    /// Expands a theme short name to its official package ID.
    /// </summary>
    /// <remarks>
    /// <c>Lumina</c> becomes <c>Spectara.Revela.Themes.Lumina</c>; IDs that already start with
    /// <c>Spectara.Revela.</c> are returned unchanged.
    /// </remarks>
    /// <param name="name">Short name or full package ID.</param>
    /// <returns>The package ID.</returns>
    public static string FromThemeName(string name) => Expand(name, OfficialThemePrefix);

    /// <summary>
    /// Expands a plugin short name to its official package ID.
    /// </summary>
    /// <remarks>
    /// <c>Source.OneDrive</c> becomes <c>Spectara.Revela.Plugins.Source.OneDrive</c>; IDs that already
    /// start with <c>Spectara.Revela.</c> are returned unchanged.
    /// </remarks>
    /// <param name="name">Short name or full package ID.</param>
    /// <returns>The package ID.</returns>
    public static string FromPluginName(string name) => Expand(name, OfficialPluginPrefix);

    /// <summary>
    /// Removes the official theme or plugin prefix for display (<c>Spectara.Revela.Themes.Lumina</c> → <c>Lumina</c>).
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <returns>The short name, or <paramref name="packageId"/> when it has no official prefix.</returns>
    public static string ToShortName(string packageId)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        foreach (var prefix in (ReadOnlySpan<string>)[OfficialThemePrefix, OfficialPluginPrefix])
        {
            if (packageId.Length > prefix.Length && packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return packageId[prefix.Length..];
            }
        }

        return packageId;
    }

    /// <summary>
    /// Infers package types from the ID when a feed does not report them.
    /// </summary>
    /// <remarks>
    /// Only a fallback for index entries: installation always validates the types declared in the nuspec.
    /// Both the official plural segment (<c>.Themes.</c>, <c>.Plugins.</c>) and a singular
    /// community segment (<c>.Theme.</c>, <c>.Plugin.</c>) are recognized.
    /// </remarks>
    /// <param name="packageId">The package ID.</param>
    /// <returns>The inferred package types; empty when the ID follows neither convention.</returns>
    public static IReadOnlyList<string> InferPackageTypes(string packageId)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        List<string> types = [];

        if (packageId.Contains(".Themes.", StringComparison.OrdinalIgnoreCase)
            || packageId.Contains(".Theme.", StringComparison.OrdinalIgnoreCase))
        {
            types.Add(ThemePackageType);
        }

        if (packageId.Contains(".Plugins.", StringComparison.OrdinalIgnoreCase)
            || packageId.Contains(".Plugin.", StringComparison.OrdinalIgnoreCase))
        {
            types.Add(PluginPackageType);
        }

        return types;
    }

    private static string Expand(string name, string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return name.StartsWith(PackageTrustPolicy.OfficialPackagePrefix, StringComparison.OrdinalIgnoreCase)
            ? name
            : prefix + name;
    }
}

using Spectara.Revela.Sdk.Abstractions;

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
}

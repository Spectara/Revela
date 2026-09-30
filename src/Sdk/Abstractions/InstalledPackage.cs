namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// A package that was installed into the local package directory.
/// </summary>
/// <param name="Id">Package ID as declared in the package's nuspec.</param>
/// <param name="Version">Exact installed version (normalized NuGet version string).</param>
/// <param name="PackageTypes">Package types declared in the nuspec (e.g. <c>RevelaPlugin</c>, <c>RevelaTheme</c>).</param>
public sealed record InstalledPackage(string Id, string Version, IReadOnlyList<string> PackageTypes)
{
    /// <summary>
    /// NuGet package type declared by theme packages.
    /// </summary>
    public const string ThemePackageType = "RevelaTheme";

    /// <summary>
    /// Whether the package declares the <see cref="ThemePackageType"/> package type.
    /// </summary>
    public bool IsTheme => PackageTypes.Contains(ThemePackageType, StringComparer.OrdinalIgnoreCase);
}

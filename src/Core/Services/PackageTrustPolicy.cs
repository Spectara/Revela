namespace Spectara.Revela.Core.Services;

/// <summary>
/// Trust rules for package discovery and package sources.
/// </summary>
/// <remarks>
/// The <c>Spectara.*</c> NuGet ID prefix is reserved on nuget.org, so the official package index
/// only offers IDs under <see cref="OfficialPackagePrefix"/>. See <c>docs/security-model.md</c>.
/// </remarks>
public static class PackageTrustPolicy
{
    /// <summary>
    /// ID prefix of official Revela packages.
    /// </summary>
    public const string OfficialPackagePrefix = "Spectara.Revela.";

    /// <summary>
    /// Returns whether <paramref name="packageId"/> belongs to the official Revela namespace.
    /// </summary>
    /// <remarks>NuGet package IDs are case-insensitive.</remarks>
    public static bool IsOfficialPackageId(string? packageId) =>
        packageId is not null &&
        packageId.Length > OfficialPackagePrefix.Length &&
        packageId.StartsWith(OfficialPackagePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns whether a package source (feed URL, package URL, or local path) may be used.
    /// </summary>
    /// <remarks>
    /// Plain <c>http://</c> is rejected because packages contain executable code and would be
    /// exposed to tampering in transit. Loopback <c>http://</c> and local folders remain allowed.
    /// </remarks>
    public static bool IsAllowedSource(string source) =>
        !Uri.TryCreate(source, UriKind.Absolute, out var uri) || IsAllowedSource(uri);

    /// <inheritdoc cref="IsAllowedSource(string)"/>
    public static bool IsAllowedSource(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Scheme != Uri.UriSchemeHttp || source.IsLoopback;
    }
}

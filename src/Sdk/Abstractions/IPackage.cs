namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Base interface for all installable packages — plugins and themes.
/// </summary>
public interface IPackage
{
    /// <summary>
    /// Gets the package metadata (name, version, description, author).
    /// </summary>
    PackageMetadata Metadata { get; }
}

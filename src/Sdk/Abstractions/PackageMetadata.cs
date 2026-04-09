namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Base metadata record for all packages (plugins and themes).
/// </summary>
/// <remarks>
/// Shared by <see cref="IPlugin"/> and <see cref="ITheme"/>.
/// All fields have sensible defaults — only <see cref="Id"/>, <see cref="Name"/>,
/// <see cref="Version"/>, and <see cref="Description"/> are required.
/// </remarks>
public record PackageMetadata
{
    /// <summary>Fully qualified package ID (e.g., "Spectara.Revela.Plugins.Serve" or "Spectara.Revela.Themes.Lumina").</summary>
    public required string Id { get; init; }

    /// <summary>Package display name (short, human-readable).</summary>
    public required string Name { get; init; }

    /// <summary>Package version (semver).</summary>
    public required string Version { get; init; }

    /// <summary>Brief description of the package.</summary>
    public required string Description { get; init; }

    /// <summary>Package author or organization.</summary>
    public string Author { get; init; } = "Unknown";

    /// <summary>
    /// Fully qualified package IDs that MUST be installed for this package to work.
    /// </summary>
    public IReadOnlyList<string> RequiredPackages { get; init; } = [];

    /// <summary>
    /// Fully qualified package IDs that this package optionally extends.
    /// Extension commands are only registered if the target is present.
    /// </summary>
    public IReadOnlyList<string> ExtendsPackages { get; init; } = [];

    /// <summary>URL to preview image (primarily used by themes).</summary>
    public Uri? PreviewImageUri { get; init; }

    /// <summary>Tags for discovery (e.g., "photography", "dark", "gallery").</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

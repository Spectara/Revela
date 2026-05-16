namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme manifest describing available templates and assets.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Main layout template path.</summary>
    public required string LayoutTemplate { get; init; }
}

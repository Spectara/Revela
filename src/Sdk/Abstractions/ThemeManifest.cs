namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme manifest describing available templates and assets.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Main layout template path.</summary>
    public required string LayoutTemplate { get; init; }

    /// <summary>Theme variables with default values.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } =
        new Dictionary<string, string>();
}

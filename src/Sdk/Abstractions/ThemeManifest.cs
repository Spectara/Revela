namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme manifest describing available templates and assets.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Main layout template path.</summary>
    public required string LayoutTemplate { get; init; }

    /// <summary>
    /// Optional per-stylesheet page-type scope declarations.
    /// </summary>
    /// <remarks>
    /// When a theme or extension declares stylesheet scopes, a stylesheet is only
    /// linked on pages whose scope token matches (or when it declares the
    /// <c>all</c> token / declares no scope). Stylesheets with no declaration at
    /// all default to loading everywhere, preserving backward compatibility.
    /// </remarks>
    public IReadOnlyList<StylesheetDeclaration>? Stylesheets { get; init; }
}

/// <summary>
/// Declares a stylesheet and the page-type scopes on which it should load.
/// </summary>
/// <remarks>
/// Scope tokens are plain strings so any plugin can scope its CSS to its own
/// template prefix without a change to the core. Well-known tokens are
/// <c>all</c>, <c>index</c>, <c>gallery</c>, and <c>photo</c>; plugin pages use
/// the plugin prefix (e.g. <c>statistics</c>, <c>calendar</c>).
/// </remarks>
public sealed class StylesheetDeclaration
{
    /// <summary>
    /// Stylesheet path relative to the theme's or extension's <c>Assets/</c> folder
    /// (e.g. <c>photo.css</c>, or <c>main.css</c> for an extension under its prefix).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Scope tokens on which this stylesheet loads. When null or empty the
    /// stylesheet loads on every page (equivalent to declaring <c>all</c>).
    /// </summary>
    public IReadOnlyList<string>? Scope { get; set; }
}

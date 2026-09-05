using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme manifest describing available templates and assets.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Main layout template path.</summary>
    public required string LayoutTemplate { get; init; }

    /// <summary>
    /// Photo viewer capabilities declared by a base theme, or <see langword="null"/>
    /// for a theme extension.
    /// </summary>
    public PhotoViewerCapabilities? PhotoViewer { get; init; }

    /// <summary>
    /// Optional per-stylesheet page-type scope declarations.
    /// </summary>
    /// <remarks>
    /// When a theme or extension declares stylesheet scopes, a stylesheet is only
    /// linked on pages whose scope token matches (or when it declares the
    /// <c>all</c> token / declares no scope). Undeclared stylesheets are copied
    /// to the output but are not linked on scoped pages.
    /// </remarks>
    public IReadOnlyList<AssetDeclaration>? Stylesheets { get; init; }

    /// <summary>
    /// Optional per-script page-type scope declarations.
    /// </summary>
    /// <remarks>
    /// Scripts use the same scope semantics and backward-compatible defaults as
    /// <see cref="Stylesheets"/>.
    /// </remarks>
    public IReadOnlyList<AssetDeclaration>? Scripts { get; init; }
}

/// <summary>
/// Declares the photo viewer modes supported by a base theme and its default mode.
/// </summary>
public sealed class PhotoViewerCapabilities
{
    /// <summary>Photo viewer modes supported by the theme.</summary>
    public required IReadOnlyList<PhotoViewerMode> Supported
    {
        get;
        init => field = Array.AsReadOnly([.. value]);
    }

    /// <summary>Default photo viewer mode for the theme.</summary>
    public required PhotoViewerMode Default { get; init; }
}

/// <summary>
/// Declares an asset and the page-type scopes on which it should load.
/// </summary>
/// <remarks>
/// Scope tokens are plain strings so any plugin can scope its CSS to its own
/// template prefix without a change to the core. Well-known tokens are
/// <c>all</c>, <c>index</c>, <c>gallery</c>, and <c>photo</c>; plugin pages use
/// the plugin prefix (e.g. <c>statistics</c>, <c>calendar</c>).
/// </remarks>
public sealed class AssetDeclaration
{
    /// <summary>
    /// Asset path relative to the theme's or extension's <c>Assets/</c> folder
    /// (e.g. <c>photo.css</c>, <c>lightbox.js</c>, or an extension asset under its prefix).
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Scope tokens on which this asset loads. When null or empty the asset loads
    /// on every page (equivalent to declaring <c>all</c>).
    /// </summary>
    public IReadOnlyList<string>? Scope { get; set; }
}

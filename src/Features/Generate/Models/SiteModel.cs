namespace Spectara.Revela.Features.Generate.Models;

/// <summary>
/// Site-wide data of one render run, shared by all pages.
/// </summary>
internal sealed class SiteModel
{
    /// <summary>
    /// Site settings from site.json, converted once into a read-only Scriban object
    /// (<c>site</c> in templates). Supports arbitrary properties defined by the theme.
    /// </summary>
    public object? Site { get; init; }

    /// <summary>
    /// All galleries in the site, the home page first.
    /// </summary>
    public required IReadOnlyList<Gallery> Galleries { get; init; }

    /// <summary>
    /// All images of all galleries.
    /// </summary>
    public required IReadOnlyList<Image> Images { get; init; }

    /// <summary>
    /// Navigation tree for site navigation
    /// </summary>
    public required IReadOnlyList<NavigationItem> Navigation { get; init; }
}

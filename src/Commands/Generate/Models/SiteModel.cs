using System.Text.Json;

namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Site model for template rendering
/// </summary>
/// <remarks>
/// This is the complete model passed to Scriban templates.
/// Contains all data needed to render the site.
/// </remarks>
internal sealed class SiteModel
{
    /// <summary>
    /// Site settings loaded dynamically from site.json.
    /// Supports arbitrary properties defined by the theme.
    /// </summary>
    public JsonElement? Site { get; init; }

    /// <summary>
    /// Project settings (name, baseUrl, basePath, imageBasePath)
    /// </summary>
    public required RenderProjectSettings Project { get; init; }

    /// <summary>
    /// All galleries in the site
    /// </summary>
    public required IReadOnlyList<Gallery> Galleries { get; init; }

    /// <summary>
    /// All processed images
    /// </summary>
    public required IReadOnlyList<Image> Images { get; init; }

    /// <summary>
    /// Navigation tree for site navigation
    /// </summary>
    public required IReadOnlyList<NavigationItem> Navigation { get; init; }

    /// <summary>
    /// Build timestamp
    /// </summary>
    public DateTime BuildDate { get; init; }
}

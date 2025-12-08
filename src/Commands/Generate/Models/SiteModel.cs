using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Generate.Models;

/// <summary>
/// Site model for template rendering
/// </summary>
/// <remarks>
/// This is the complete model passed to Scriban templates.
/// Contains all data needed to render the site.
/// </remarks>
public sealed class SiteModel
{
    /// <summary>
    /// Site settings (title, author, description, copyright)
    /// </summary>
    public required SiteSettings Site { get; init; }

    /// <summary>
    /// Project settings (name, baseUrl, basePath, imageBasePath)
    /// </summary>
    public required ProjectSettings Project { get; init; }

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

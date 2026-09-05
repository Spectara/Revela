namespace Spectara.Revela.Sdk.Models;

/// <summary>
/// Defines how a theme can present an individual photo.
/// </summary>
public enum PhotoViewerMode
{
    /// <summary>Open the photo on its dedicated page.</summary>
    Page,

    /// <summary>Open the photo in an in-page lightbox.</summary>
    Lightbox,

    /// <summary>Do not provide an individual photo viewer.</summary>
    None
}

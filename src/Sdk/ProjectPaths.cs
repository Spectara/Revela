namespace Spectara.Revela.Sdk;

/// <summary>
/// Well-known directory names used in Revela projects.
/// </summary>
/// <remarks>
/// These constants define the standard folder structure for Revela projects.
/// All paths are relative to the project root directory.
/// </remarks>
public static class ProjectPaths
{
    /// <summary>
    /// Source directory for input images (photos to process).
    /// </summary>
    public const string Source = "source";

    /// <summary>
    /// Output directory for generated site files.
    /// </summary>
    public const string Output = "output";

    /// <summary>
    /// Cache directory for intermediate files (manifest, processed images).
    /// </summary>
    public const string Cache = ".cache";

    /// <summary>
    /// Themes directory for local/extracted themes.
    /// </summary>
    public const string Themes = "themes";

    /// <summary>
    /// Plugins configuration directory.
    /// </summary>
    public const string Plugins = "plugins";

    /// <summary>
    /// Shared images directory for images available to filter galleries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Images in <c>source/_images/</c> (including subdirectories) are scanned
    /// and available for filter expressions, but do not create their own galleries.
    /// </para>
    /// <para>
    /// Use this for images that should be accessible via filters but not displayed
    /// in a dedicated gallery (e.g., curated collections, featured images).
    /// </para>
    /// </remarks>
    public const string SharedImages = "_images";
}

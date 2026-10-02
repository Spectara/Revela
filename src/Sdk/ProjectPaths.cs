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
    /// Revela's own folder in the project. Holds <see cref="Cache"/> and <see cref="State"/>;
    /// nothing in it is ever published.
    /// </summary>
    public const string Revela = ".revela";

    /// <summary>
    /// Reproducible data (scan manifest, plugin data files such as <c>statistics.json</c>).
    /// </summary>
    /// <remarks>
    /// Deleted by <c>revela clean cache</c>; losing it costs at most a rescan.
    /// <c>.revela/cache</c>, composed with the platform's directory separator.
    /// </remarks>
    public static readonly string Cache = Path.Combine(Revela, "cache");

    /// <summary>
    /// State of the output: what Revela produced there and with which settings
    /// (image variants, pre-compressed sidecars).
    /// </summary>
    /// <remarks>
    /// Belongs to the output directory: <c>revela clean output</c> deletes both, <c>revela clean cache</c>
    /// keeps it. Losing it while the output stays means re-encoding every image.
    /// <c>.revela/state</c>, composed with the platform's directory separator.
    /// </remarks>
    public static readonly string State = Path.Combine(Revela, "state");

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

    /// <summary>
    /// Static files directory for files copied directly to output root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Files in <c>source/_static/</c> are copied 1:1 to the output root.
    /// Use this for favicon files, robots.txt, CNAME, or other static assets.
    /// </para>
    /// <para>
    /// Example: <c>source/_static/favicon/favicon.ico</c> → <c>output/favicon/favicon.ico</c>
    /// </para>
    /// </remarks>
    public const string Static = "_static";
}

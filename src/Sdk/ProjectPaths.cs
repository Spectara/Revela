namespace Spectara.Revela.Sdk;

/// <summary>
/// Well-known directory names used in Revela projects.
/// </summary>
/// <remarks>
/// <para>
/// These constants define the standard folder structure for Revela projects.
/// All paths are relative to the project root directory; combine them with
/// <see cref="ProjectEnvironment.Path"/>.
/// </para>
/// <para>
/// Where a plugin keeps a file depends on what losing it means: data that can be rebuilt
/// from the source goes to <see cref="Cache"/>, a record of what the plugin produced in the
/// output goes to <see cref="State"/>, and only files that belong to the published site go
/// to the output (<see cref="Services.IPathResolver.OutputPath"/>).
/// </para>
/// </remarks>
public static class ProjectPaths
{
    /// <summary>
    /// Revela's own folder in the project (<c>.revela</c>). Holds <see cref="Cache"/> and
    /// <see cref="State"/>; nothing in it is ever published.
    /// </summary>
    public const string Revela = ".revela";

    /// <summary>
    /// Cache (<c>.revela/cache</c>): data that is reproducible from the source, such as the scan
    /// manifest and plugin data files (<c>&lt;page&gt;/statistics.json</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// May be deleted at any time (<c>revela clean cache</c>, <c>clean all</c>, or by hand);
    /// losing it only costs time, because the next build recreates it. Never put anything here
    /// that the next build cannot rebuild from the source and configuration.
    /// </para>
    /// <para>Composed with the platform's directory separator.</para>
    /// </remarks>
    public static readonly string Cache = Path.Combine(Revela, "cache");

    /// <summary>
    /// State (<c>.revela/state</c>): records that describe what is in the output, such as which
    /// image variants exist and with which settings, or which <c>.gz</c>/<c>.br</c> sidecars
    /// Revela created.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Belongs to the output: it is deleted together with the output (<c>revela clean output</c>,
    /// <c>clean all</c>) and kept by <c>clean cache</c>. It lives outside the output, so it is never
    /// published. Losing it while the output stays means redoing that work (re-encoding every
    /// image) or no longer knowing which files in the output are yours.
    /// </para>
    /// <para>Composed with the platform's directory separator.</para>
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

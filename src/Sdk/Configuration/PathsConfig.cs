namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Configuration for source and output directory paths.
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the "paths" section of project.json.
/// Allows configuring custom locations for source images and generated output.
/// </para>
/// <para>
/// Paths can be:
/// <list type="bullet">
/// <item>Relative to project root (e.g., "source", "dist", "../output")</item>
/// <item>Absolute paths (e.g., "D:\OneDrive\Photos", "/var/www/html")</item>
/// </list>
/// </para>
/// <para>
/// If not specified, defaults to standard Revela project structure.
/// </para>
/// <example>
/// <code>
/// // project.json - Default (can be omitted entirely)
/// {
///   "paths": {
///     "source": "source",
///     "output": "output"
///   }
/// }
/// 
/// // OneDrive source with local output
/// {
///   "paths": {
///     "source": "D:\\OneDrive\\Photos\\Portfolio",
///     "output": "output"
///   }
/// }
/// 
/// // Direct server deployment
/// {
///   "paths": {
///     "source": "source",
///     "output": "/var/www/html/photos"
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class PathsConfig
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "paths";

    /// <summary>
    /// Source directory containing images to process.
    /// </summary>
    /// <remarks>
    /// Can be relative to project root or an absolute path.
    /// Use absolute paths for cloud-synced folders (OneDrive, Dropbox, iCloud).
    /// </remarks>
    public string Source { get; init; } = "source";

    /// <summary>
    /// Output directory for generated site files.
    /// </summary>
    /// <remarks>
    /// Can be relative to project root or an absolute path.
    /// Use absolute paths for direct webserver deployment.
    /// </remarks>
    public string Output { get; init; } = "output";
}

using System.ComponentModel.DataAnnotations;

namespace Spectara.Revela.Plugin.Source.OneDrive.Configuration;

/// <summary>
/// OneDrive plugin configuration
/// </summary>
/// <remarks>
/// Default values are defined in the property initializers (OutputDirectory = "source", etc.).
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Command-line arguments (--share-url, --output, --concurrency, etc.)
/// 2. Environment variables (ONEDRIVE__* or REVELA__PLUGINS__ONEDRIVE__*)
/// 3. User config file (onedrive.json) - created by "revela source onedrive init"
/// 
/// Example onedrive.json (user-specific, in working directory):
/// {
///   "Plugins": {
///     "OneDrive": {
///       "ShareUrl": "https://1drv.ms/...",
///       "IncludePatterns": ["*.jpg", "*.png", "*.md"],
///       "ExcludePatterns": ["*.tmp"]
///     }
///   }
/// }
/// 
/// Example Environment Variables:
/// ONEDRIVE_SHAREURL=https://1drv.ms/...
/// ONEDRIVE_OUTPUTDIRECTORY=downloads
/// ONEDRIVE_DEFAULTCONCURRENCY=16
/// 
/// Or with REVELA__ prefix:
/// REVELA__PLUGINS__ONEDRIVE__SHAREURL=https://1drv.ms/...
/// </remarks>
public sealed class OneDrivePluginConfig
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Plugins:OneDrive";

    /// <summary>
    /// OneDrive shared folder URL
    /// </summary>
    /// <remarks>
    /// CA1056 suppressed: ShareUrl must remain string for compatibility with configuration binding.
    /// OneDrive URLs often include share tokens that don't parse as valid URIs.
    /// </remarks>
    [Required(ErrorMessage = "ShareUrl is required. Set via config file, environment variable, or --share-url parameter.")]
    [Url(ErrorMessage = "ShareUrl must be a valid URL")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI properties should not be strings", Justification = "Share URLs with tokens don't parse as System.Uri")]
    public string ShareUrl { get; init; } = string.Empty;

    /// <summary>
    /// Output directory for downloaded files (relative to current directory)
    /// </summary>
    public string OutputDirectory { get; init; } = "source";

    /// <summary>
    /// Default number of parallel downloads (auto-detected based on CPU cores if not specified)
    /// </summary>
    [Range(1, 100, ErrorMessage = "DefaultConcurrency must be between 1 and 100")]
    public int? DefaultConcurrency { get; init; }

    /// <summary>
    /// File patterns to include (e.g., "*.jpg", "*.png")
    /// If null or empty, smart defaults are used: all images (via MIME type) and markdown files
    /// </summary>
    public IReadOnlyList<string>? IncludePatterns { get; init; }

    /// <summary>
    /// File patterns to exclude (e.g., "*.tmp", "*.bak")
    /// </summary>
    public IReadOnlyList<string>? ExcludePatterns { get; init; }
}

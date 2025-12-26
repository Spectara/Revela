using System.ComponentModel.DataAnnotations;

namespace Spectara.Revela.Plugin.Source.OneDrive.Configuration;

/// <summary>
/// OneDrive plugin configuration
/// </summary>
/// <remarks>
/// Default values are defined in the property initializers (OutputDirectory = "source", etc.).
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Command-line arguments (--share-url, --output, --concurrency, etc.)
/// 2. Environment variables (SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__*)
/// 3. User config file (config/Spectara.Revela.Plugin.Source.OneDrive.json)
///
/// Example config/Spectara.Revela.Plugin.Source.OneDrive.json:
/// {
///   "Spectara.Revela.Plugin.Source.OneDrive": {
///     "ShareUrl": "https://1drv.ms/...",
///     "IncludePatterns": ["*.jpg", "*.png", "*.md"],
///     "ExcludePatterns": ["*.tmp"]
///   }
/// }
///
/// Example Environment Variables:
/// SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL=https://1drv.ms/...
/// SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__OUTPUTDIRECTORY=downloads
/// </remarks>
public sealed class OneDrivePluginConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    /// <remarks>
    /// Uses full package ID as key directly (no Plugins: prefix).
    /// This allows direct mapping from JSON root key and ENV variables.
    /// </remarks>
    public const string SectionName = "Spectara.Revela.Plugin.Source.OneDrive";

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

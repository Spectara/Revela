using System.ComponentModel.DataAnnotations;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Source.OneDrive.Configuration;

/// <summary>
/// OneDrive plugin configuration
/// </summary>
/// <remarks>
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Command-line arguments (--share-url, etc.)
/// 2. Environment variables (SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__*)
/// 3. Project config file (project.json)
///
/// Example project.json:
/// {
///   "Spectara.Revela.Plugins.Source.OneDrive": {
///     "ShareUrl": "https://1drv.ms/...",
///     "IncludePatterns": ["*.jpg", "*.png", "*.md"],
///     "ExcludePatterns": ["*.tmp"]
///   }
/// }
///
/// Example Environment Variables:
/// SPECTARA__REVELA__PLUGIN__SOURCE__ONEDRIVE__SHAREURL=https://1drv.ms/...
///
/// Downloaded files are saved to the project's source directory (paths.source config).
/// </remarks>
[RevelaConfig("Spectara.Revela.Plugins.Source.OneDrive")]
internal sealed class OneDrivePluginConfig
{
    /// <summary>
    /// Configuration section name in project.json.
    /// </summary>
    public static string SectionName => "Spectara.Revela.Plugins.Source.OneDrive";

    /// <summary>
    /// OneDrive shared folder URL
    /// </summary>
    /// <remarks>
    /// OneDrive URLs often include share tokens that don't parse as valid <see cref="Uri"/>,
    /// so this is kept as <see cref="string"/> for compatibility with configuration binding.
    /// </remarks>
    [Required(ErrorMessage = "ShareUrl is required. Set via config file, environment variable, or --share-url parameter.")]
    [Url(ErrorMessage = "ShareUrl must be a valid URL")]
    public string ShareUrl { get; init; } = string.Empty;

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

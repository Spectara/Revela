using System.ComponentModel.DataAnnotations;

namespace Spectara.Revela.Plugin.Source.OneDrive.Models;

/// <summary>
/// Configuration for OneDrive shared folder source
/// </summary>
/// <remarks>
/// Stored in onedrive.json in project directory.
/// 
/// Minimal example (uses defaults):
/// <code>
/// {
///   "shareUrl": "https://1drv.ms/f/s!xxxxx"
/// }
/// </code>
/// 
/// Advanced example (custom patterns):
/// <code>
/// {
///   "shareUrl": "https://1drv.ms/f/s!xxxxx",
///   "includePatterns": ["*.jpg", "*.webp"],
///   "excludePatterns": ["thumbnail_*"]
/// }
/// </code>
/// 
/// Default behavior (when patterns not specified):
/// - Downloads all images (detected via MIME type: image/*)
/// - Downloads all markdown files (*.md)
/// </remarks>
public sealed class OneDriveConfig
{
    /// <summary>
    /// The OneDrive shared folder URL
    /// </summary>
    [Required(ErrorMessage = "ShareUrl is required")]
    [Url(ErrorMessage = "ShareUrl must be a valid URL")]
    [RegularExpression(@"^https://(1drv\.ms|onedrive\.live\.com)/.*",
        ErrorMessage = "Must be a valid OneDrive share URL (1drv.ms or onedrive.live.com)")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Used for CLI parameter and JSON serialization")]
    public required string ShareUrl { get; init; }

    /// <summary>
    /// Optional file patterns to include (e.g., "*.jpg", "*.png", "*.md")
    /// If null or empty, defaults to all images (via MIME type) and markdown files
    /// </summary>
    public IReadOnlyList<string>? IncludePatterns { get; init; }

    /// <summary>
    /// Optional file patterns to exclude
    /// </summary>
    public IReadOnlyList<string>? ExcludePatterns { get; init; }
}

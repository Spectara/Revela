using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Source.OneDrive.Configuration;

/// <summary>
/// OneDrive plugin configuration
/// </summary>
/// <remarks>
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Command-line arguments (--share-url, etc.)
/// 2. Environment variables (SPECTARA__REVELA__PLUGINS__ONEDRIVE__*)
/// 3. Project config file (project.json)
/// 4. Global config file (revela.json)
///
/// Example project.json:
/// {
///   "plugins": {
///     "oneDrive": {
///       "shareUrl": "https://1drv.ms/...",
///       "includePatterns": ["*.jpg", "*.png", "*.md"],
///       "excludePatterns": ["*.tmp"]
///     }
///   }
/// }
///
/// Example Environment Variables:
/// SPECTARA__REVELA__PLUGINS__ONEDRIVE__SHAREURL=https://1drv.ms/...
///
/// Downloaded files are saved to the project's source directory (paths.source config).
/// </remarks>
[RevelaConfig("plugins:oneDrive")]
internal sealed class OneDrivePluginConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "plugins:oneDrive";
    /// <summary>
    /// OneDrive shared folder URL
    /// </summary>
    /// <remarks>
    /// <para>
    /// OneDrive URLs often include share tokens that don't parse as valid <see cref="Uri"/>,
    /// so this is kept as <see cref="string"/> for compatibility with configuration binding.
    /// </para>
    /// <para>
    /// Not annotated with <c>[Required]</c>/<c>[Url]</c>: the wizard and <c>ConfigOneDriveCommand</c>
    /// read the current value (via <c>IOptionsMonitor</c>) before the user sets it, which would
    /// otherwise throw <see cref="OptionsValidationException"/>.
    /// Required-and-safe validation lives at the actual call site (<c>OneDriveSourceCommand</c>
    /// and the interactive prompt's <c>UrlSafety</c> check).
    /// </para>
    /// </remarks>
    public string ShareUrl { get; set; } = string.Empty;

    /// <summary>
    /// Default number of parallel downloads (auto-detected based on CPU cores if not specified)
    /// </summary>
    [Range(1, 100, ErrorMessage = "DefaultConcurrency must be between 1 and 100")]
    public int? DefaultConcurrency { get; set; }

    /// <summary>
    /// File-name patterns to download (e.g., "*.jpg", "*.png"; <c>*</c> and <c>?</c>, case-insensitive).
    /// If null or empty, all files are downloaded; <c>--clean</c> then only considers
    /// local images (.jpg, .jpeg, .png, .webp) and markdown files.
    /// </summary>
    public IReadOnlyList<string>? IncludePatterns { get; set; }

    /// <summary>
    /// File-name patterns to skip (e.g., "*.tmp", "*.bak"). Exclusion wins over inclusion.
    /// </summary>
    public IReadOnlyList<string>? ExcludePatterns { get; set; }
}

/// <summary>
/// Trim/AOT-safe <see cref="IValidateOptions{TOptions}"/> implementation for
/// <see cref="OneDrivePluginConfig"/>. The body is emitted by the
/// <c>Microsoft.Extensions.Options</c> source generator from the
/// <c>DataAnnotations</c> on the config type.
/// </summary>
[OptionsValidator]
internal sealed partial class OneDrivePluginConfigValidator : IValidateOptions<OneDrivePluginConfig>;

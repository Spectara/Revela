using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Statistics.Configuration;

/// <summary>
/// Statistics plugin configuration
/// </summary>
/// <remarks>
/// Default values are defined in the property initializers.
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Command-line arguments (--output, etc.)
/// 2. Environment variables (SPECTARA__REVELA__PLUGINS__STATISTICS__*)
/// 3. Project config file (project.json)
/// 4. Global config file (revela.json)
///
/// Example project.json:
/// {
///   "plugins": {
///     "statistics": {
///       "maxEntriesPerCategory": 15,
///       "sortByCount": true
///     }
///   }
/// }
///
/// Example Environment Variables:
/// SPECTARA__REVELA__PLUGINS__STATISTICS__MAXENTRIESPERCATEGORY=20
/// SPECTARA__REVELA__PLUGINS__STATISTICS__SORTBYCOUNT=false
/// </remarks>
[RevelaConfig("plugins:statistics")]
internal sealed class StatisticsPluginConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "plugins:statistics";
    /// <summary>
    /// Maximum number of entries per category (e.g., top 15 apertures)
    /// </summary>
    /// <remarks>
    /// Remaining entries are aggregated into "Other".
    /// Set to 0 for unlimited entries.
    /// </remarks>
    [Range(0, 100, ErrorMessage = "MaxEntriesPerCategory must be between 0 and 100")]
    public int MaxEntriesPerCategory { get; set; } = 15;

    /// <summary>
    /// Sort entries by count (descending) instead of by value
    /// </summary>
    public bool SortByCount { get; set; } = true;
}

/// <summary>
/// Trim/AOT-safe <see cref="IValidateOptions{TOptions}"/> implementation for
/// <see cref="StatisticsPluginConfig"/>. The body is emitted by the
/// <c>Microsoft.Extensions.Options</c> source generator from the
/// <c>DataAnnotations</c> on the config type.
/// </summary>
[OptionsValidator]
internal sealed partial class StatisticsPluginConfigValidator : IValidateOptions<StatisticsPluginConfig>;

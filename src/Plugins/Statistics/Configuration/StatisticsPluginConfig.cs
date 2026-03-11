using System.ComponentModel.DataAnnotations;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Statistics.Configuration;

/// <summary>
/// Statistics plugin configuration
/// </summary>
/// <remarks>
/// Default values are defined in the property initializers.
/// These can be overridden from multiple sources (in priority order, highest to lowest):
/// 1. Command-line arguments (--output, etc.)
/// 2. Environment variables (SPECTARA__REVELA__PLUGIN__STATISTICS__*)
/// 3. Project config file (project.json)
///
/// Example project.json:
/// {
///   "Spectara.Revela.Plugins.Statistics": {
///     "MaxEntriesPerCategory": 15,
///     "SortByCount": true
///   }
/// }
///
/// Example Environment Variables:
/// SPECTARA__REVELA__PLUGIN__STATISTICS__MAXENTRIESPERCATEGORY=20
/// SPECTARA__REVELA__PLUGIN__STATISTICS__SORTBYCOUNT=false
/// </remarks>
internal sealed class StatisticsPluginConfig : IPluginConfig
{
    /// <inheritdoc />
    public static string SectionName => "Spectara.Revela.Plugins.Statistics";

    /// <summary>
    /// Maximum number of entries per category (e.g., top 15 apertures)
    /// </summary>
    /// <remarks>
    /// Remaining entries are aggregated into "Other".
    /// Set to 0 for unlimited entries.
    /// </remarks>
    [Range(0, 100, ErrorMessage = "MaxEntriesPerCategory must be between 0 and 100")]
    public int MaxEntriesPerCategory { get; init; } = 15;

    /// <summary>
    /// Sort entries by count (descending) instead of by value
    /// </summary>
    public bool SortByCount { get; init; } = true;
}

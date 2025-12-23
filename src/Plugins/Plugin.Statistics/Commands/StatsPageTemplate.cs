using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugin.Statistics.Commands;

/// <summary>
/// Page template for creating statistics pages with EXIF data aggregation.
/// </summary>
/// <remarks>
/// <para>
/// Generates _index.revela files with frontmatter for the statistics/overview template
/// and optional plugin configuration for MaxEntriesPerCategory and SortByCount settings.
/// </para>
/// <para>
/// Usage: revela create page statistics source/stats --title "My Stats"
/// </para>
/// </remarks>
public sealed class StatsPageTemplate : IPageTemplate
{
    public string Name => "statistics";

    public string DisplayName => "Photo Statistics";

    public string Description => "Create statistics page with EXIF aggregations";

    public string TemplateName => "statistics/overview";

    public string ConfigSectionName => "Spectara.Revela.Plugin.Statistics";

    /// <inheritdoc />
    /// <remarks>
    /// Statistics plugin has 'config statistics' command for interactive configuration.
    /// </remarks>
    public bool HasConfigCommand => true;

    public IReadOnlyList<TemplateProperty> PageProperties { get; } =
    [
        new()
        {
            Name = "title",
            Aliases = ["--title", "-t"],
            Type = typeof(string),
            DefaultValue = "Photo Statistics",
            Description = "Page title (example: 'Gallery Stats')",
            Required = false,
            FrontmatterKey = "title",
            ConfigKey = null
        },
        new()
        {
            Name = "description",
            Aliases = ["--description", "-d"],
            Type = typeof(string),
            DefaultValue = "EXIF statistics from your photo library",
            Description = "Page description (example: 'Statistics from 500+ photos')",
            Required = false,
            FrontmatterKey = "description",
            ConfigKey = null
        }
    ];

    /// <inheritdoc />
    /// <remarks>
    /// Empty - configuration is handled by ConfigStatisticsCommand, not dynamically generated.
    /// </remarks>
    public IReadOnlyList<TemplateProperty> ConfigProperties { get; } = [];
}

using System.ComponentModel.DataAnnotations;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Plugins.Source.Calendar.Configuration;

/// <summary>
/// Configuration for the Source.Calendar plugin.
/// </summary>
internal sealed class SourceCalendarConfig : IPluginConfig
{
    /// <inheritdoc />
    public static string SectionName => "Spectara.Revela.Plugins.Source.Calendar";

    /// <summary>
    /// Named iCal feeds to fetch. Key = feed name, Value = feed configuration.
    /// </summary>
    public Dictionary<string, FeedConfig> Feeds { get; init; } = [];
}

/// <summary>
/// Configuration for a single iCal feed.
/// </summary>
internal sealed class FeedConfig
{
    /// <summary>
    /// URL of the iCal feed.
    /// </summary>
    [Required(ErrorMessage = "Feed URL is required")]
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Output path relative to the source directory (e.g. "availability/bookings.ics").
    /// </summary>
    [Required(ErrorMessage = "Output path is required")]
    public string Output { get; init; } = string.Empty;
}

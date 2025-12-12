using System.Globalization;
using System.Text;
using Spectara.Revela.Plugin.Statistics.Models;

namespace Spectara.Revela.Plugin.Statistics.Services;

/// <summary>
/// Generates HTML content for statistics display
/// </summary>
public static class HtmlGenerator
{
    /// <summary>
    /// Begin marker for auto-generated content
    /// </summary>
    public const string BeginMarker = "<!-- STATS:BEGIN -->";

    /// <summary>
    /// End marker for auto-generated content
    /// </summary>
    public const string EndMarker = "<!-- STATS:END -->";

    /// <summary>
    /// Generate HTML for all statistics
    /// </summary>
    public static string Generate(SiteStatistics stats)
    {
        var sb = new StringBuilder();

        sb.AppendLine(BeginMarker);
        sb.AppendLine();

        // Overview section
        sb.AppendLine("<section class=\"stats-section stats-overview\">");
        sb.AppendLine("  <h2>Overview</h2>");
        sb.AppendLine("  <div class=\"stats-grid\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    <div class=\"stats-card\"><span class=\"stats-big\">{stats.TotalImages}</span><span class=\"stats-label\">Images</span></div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    <div class=\"stats-card\"><span class=\"stats-big\">{stats.TotalGalleries}</span><span class=\"stats-label\">Galleries</span></div>");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    <div class=\"stats-card\"><span class=\"stats-big\">{stats.ImagesWithExif}</span><span class=\"stats-label\">With EXIF</span></div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</section>");
        sb.AppendLine();

        // Camera & Lens
        if (stats.CameraModels.Count > 0)
        {
            GenerateBarChart(sb, "Camera Models", stats.CameraModels);
        }

        if (stats.LensModels.Count > 0)
        {
            GenerateBarChart(sb, "Lenses", stats.LensModels);
        }

        // Technical settings
        if (stats.FocalLengths.Count > 0)
        {
            GenerateBarChart(sb, "Focal Length", stats.FocalLengths);
        }

        if (stats.Apertures.Count > 0)
        {
            GenerateBarChart(sb, "Aperture", stats.Apertures);
        }

        if (stats.IsoValues.Count > 0)
        {
            GenerateBarChart(sb, "ISO", stats.IsoValues);
        }

        if (stats.ShutterSpeeds.Count > 0)
        {
            GenerateBarChart(sb, "Shutter Speed", stats.ShutterSpeeds);
        }

        // Timeline
        if (stats.ImagesByYear.Count > 0)
        {
            GenerateBarChart(sb, "Timeline", stats.ImagesByYear);
        }

        // Footer
        sb.AppendLine(CultureInfo.InvariantCulture, $"<p class=\"stats-generated\">Generated: {stats.GeneratedAt:yyyy-MM-dd HH:mm} UTC</p>");
        sb.AppendLine();
        sb.AppendLine(EndMarker);

        return sb.ToString();
    }

    private static void GenerateBarChart(StringBuilder sb, string title, IReadOnlyList<StatisticsEntry> entries)
    {
        sb.AppendLine(CultureInfo.InvariantCulture, $"<section class=\"stats-section\">");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  <h2>{EscapeHtml(title)}</h2>");
        sb.AppendLine("  <div class=\"stats-chart\">");

        foreach (var entry in entries)
        {
            sb.AppendLine("    <div class=\"stats-bar\">");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      <span class=\"stats-label\">{EscapeHtml(entry.Label)}</span>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      <div class=\"stats-track\"><div class=\"stats-fill\" style=\"--percent: {entry.Percent}%\"></div></div>");
            sb.AppendLine(CultureInfo.InvariantCulture, $"      <span class=\"stats-value\">{entry.Count}</span>");
            sb.AppendLine("    </div>");
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("</section>");
        sb.AppendLine();
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}

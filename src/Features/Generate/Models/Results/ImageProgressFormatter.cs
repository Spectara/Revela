using System.Globalization;
using System.Text;

namespace Spectara.Revela.Features.Generate.Models.Results;

/// <summary>
/// Pure, ANSI-free formatting of <see cref="ImageProgress"/> into the pieces
/// used by both the interactive live display and the non-interactive heartbeat.
/// </summary>
/// <remarks>
/// Everything here returns plain strings — no Spectre markup, no escape codes —
/// so the heartbeat output stays clean, greppable and honours <c>NO_COLOR</c>.
/// The interactive command wraps these pieces in colour markup itself.
/// </remarks>
internal static class ImageProgressFormatter
{
    private const string HeartbeatPrefix = "[generate images]";
    private const char Separator = '·';

    /// <summary>
    /// A single plain heartbeat line, e.g.
    /// <c>[generate images] 512/1197 (43%) · avif 512 webp 998 jpg 1120 · 132 img/min · ~24m left</c>.
    /// </summary>
    public static string Heartbeat(ImageProgress p)
    {
        var builder = new StringBuilder();
        builder.Append(HeartbeatPrefix)
            .Append(' ')
            .Append(Counts(p));

        var formats = Formats(p.DoneByFormat);
        if (formats.Length > 0)
        {
            builder.Append(' ').Append(Separator).Append(' ').Append(formats);
        }

        if (p.ImagesPerMinute > 0)
        {
            builder.Append(' ').Append(Separator).Append(' ').Append(Rate(p.ImagesPerMinute));
        }

        builder.Append(' ').Append(Separator).Append(' ').Append(EtaText(p.Eta));

        if (p.Skipped > 0)
        {
            builder.Append(' ').Append(Separator).Append(' ')
                .Append(p.Skipped.ToString(CultureInfo.InvariantCulture)).Append(" skipped");
        }

        return builder.ToString();
    }

    /// <summary><c>512/1197 (43%)</c>.</summary>
    public static string Counts(ImageProgress p) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1} ({2}%)",
            p.Processed,
            p.Total,
            Percent(p.Processed, p.Total));

    /// <summary>Integer completion percentage (0-100).</summary>
    public static int Percent(int processed, int total) =>
        total > 0 ? (int)(100.0 * processed / total) : 0;

    /// <summary>Space-joined per-format counts, e.g. <c>avif 512 webp 998 jpg 1120</c>.</summary>
    public static string Formats(IReadOnlyDictionary<string, int> doneByFormat)
    {
        var builder = new StringBuilder();
        foreach (var (format, count) in doneByFormat)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(format)
                .Append(' ')
                .Append(count.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary><c>132 img/min</c>.</summary>
    public static string Rate(double imagesPerMinute) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0} img/min",
            (int)Math.Round(imagesPerMinute, MidpointRounding.AwayFromZero));

    /// <summary><c>~24m left</c> when known, otherwise <c>estimating…</c>.</summary>
    public static string EtaText(TimeSpan? eta) =>
        eta is { } value
            ? string.Format(CultureInfo.InvariantCulture, "~{0} left", Duration(value))
            : "estimating…";

    /// <summary>Compact human duration, e.g. <c>45s</c>, <c>18m</c>, <c>1h 5m</c>.</summary>
    public static string Duration(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        if (value.TotalHours >= 1)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}h {1}m",
                (int)value.TotalHours,
                value.Minutes);
        }

        if (value.TotalMinutes >= 1)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}m", (int)value.TotalMinutes);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0}s", (int)value.TotalSeconds);
    }

    /// <summary>Plain progress bar of <paramref name="width"/> cells.</summary>
    public static string Bar(int processed, int total, int width, char filled, char empty)
    {
        var fraction = total > 0 ? (double)processed / total : 0;
        var filledCells = Math.Clamp((int)(fraction * width), 0, width);
        return new string(filled, filledCells) + new string(empty, width - filledCells);
    }
}

namespace Spectara.Revela.Plugin.Source.OneDrive.Formatting;

/// <summary>
/// Formats file sizes to human-readable strings
/// </summary>
internal static class FileSizeFormatter
{
    /// <summary>
    /// Formats bytes to human-readable size (B, KB, MB, GB)
    /// </summary>
    /// <param name="bytes">File size in bytes</param>
    /// <returns>Human-readable size string (e.g., "1.23 MB")</returns>
    public static string Format(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

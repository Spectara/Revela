using System.Text;

namespace Spectara.Revela.Plugin.Statistics.Services;

/// <summary>
/// Writes markdown files with statistics content, preserving user frontmatter and content outside markers
/// </summary>
public sealed partial class MarkdownWriter(ILogger<MarkdownWriter> logger)
{
    private const string DefaultFrontmatter = """
        ---
        title: Site Statistics
        description: Auto-generated photography statistics
        ---
        """;

    /// <summary>
    /// Write statistics content to a markdown file
    /// </summary>
    /// <remarks>
    /// - If file doesn't exist: creates with default frontmatter + stats content
    /// - If file exists without markers: preserves frontmatter, appends stats content
    /// - If file exists with markers: replaces content between markers only
    /// </remarks>
    public async Task WriteAsync(string filePath, string statsContent, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string result;

        if (File.Exists(filePath))
        {
            var existing = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            result = MergeContent(existing, statsContent);
            LogUpdatingFile(logger, filePath);
        }
        else
        {
            result = CreateNewFile(statsContent);
            LogCreatingFile(logger, filePath);
        }

        await File.WriteAllTextAsync(filePath, result, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Create new file with default frontmatter and stats content
    /// </summary>
    internal static string CreateNewFile(string statsContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine(DefaultFrontmatter);
        sb.AppendLine();
        sb.Append(statsContent);
        return sb.ToString();
    }

    /// <summary>
    /// Merge stats content into existing file
    /// </summary>
    internal static string MergeContent(string existing, string statsContent)
    {
        var beginIndex = existing.IndexOf(HtmlGenerator.BeginMarker, StringComparison.Ordinal);
        var endIndex = existing.IndexOf(HtmlGenerator.EndMarker, StringComparison.Ordinal);

        // Case 1: No markers found - append stats at end
        if (beginIndex < 0 || endIndex < 0 || endIndex < beginIndex)
        {
            var sb = new StringBuilder(existing.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(statsContent);
            return sb.ToString();
        }

        // Case 2: Markers found - replace content between them
        var before = existing[..beginIndex];
        var after = existing[(endIndex + HtmlGenerator.EndMarker.Length)..];

        var result = new StringBuilder();
        result.Append(before);
        result.Append(statsContent);
        result.Append(after);

        return result.ToString();
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating new statistics file: {FilePath}")]
    private static partial void LogCreatingFile(ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating existing statistics file: {FilePath}")]
    private static partial void LogUpdatingFile(ILogger logger, string filePath);

    #endregion
}

using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

using Spectara.Revela.Commands.Generate.Models;

namespace Spectara.Revela.Commands.Generate.Parsing;

/// <summary>
/// Parses _index.md files to extract directory metadata from YAML frontmatter.
/// </summary>
/// <remarks>
/// Uses Markdig's YamlFrontMatterExtension to extract the YAML block,
/// then performs simple line-by-line parsing for key: value pairs.
/// This avoids adding a full YAML parser dependency (like YamlDotNet).
/// </remarks>
public sealed partial class FrontMatterParser(ILogger<FrontMatterParser> logger)
{
    /// <summary>
    /// The standard filename for directory metadata.
    /// </summary>
    public const string IndexFileName = "_index.md";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .Build();

    /// <summary>
    /// Parses the content of an _index.md file and returns the extracted metadata.
    /// </summary>
    /// <param name="content">The raw content of the _index.md file.</param>
    /// <returns>The parsed <see cref="DirectoryMetadata"/>.</returns>
    public static DirectoryMetadata Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return DirectoryMetadata.Empty;
        }

        var document = Markdown.Parse(content, Pipeline);

        // Extract YAML frontmatter block
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        string? title = null;
        string? slug = null;
        string? description = null;
        var hidden = false;

        if (yamlBlock is not null)
        {
            var yamlText = ExtractYamlText(content, yamlBlock);
            (title, slug, description, hidden) = ParseYamlFrontMatter(yamlText);
        }

        // Render the body (everything after frontmatter)
        var body = RenderBody(content, yamlBlock);

        if (title is null && slug is null && description is null && !hidden && body is null)
        {
            return DirectoryMetadata.Empty;
        }

        return new DirectoryMetadata
        {
            Title = title,
            Slug = slug,
            Description = description,
            Hidden = hidden,
            Body = body
        };
    }

    /// <summary>
    /// Parses an _index.md file from the filesystem.
    /// </summary>
    /// <param name="filePath">The path to the _index.md file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The parsed <see cref="DirectoryMetadata"/>.</returns>
    public async Task<DirectoryMetadata> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            LogFileNotFound(logger, filePath);
            return DirectoryMetadata.Empty;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            var metadata = Parse(content);
            LogParsedMetadata(logger, filePath, metadata.HasMetadata);
            return metadata;
        }
        catch (IOException ex)
        {
            LogReadError(logger, filePath, ex);
            return DirectoryMetadata.Empty;
        }
    }

    private static string ExtractYamlText(string content, YamlFrontMatterBlock yamlBlock)
    {
        // Get the text between the --- markers
        var start = yamlBlock.Span.Start;
        var end = yamlBlock.Span.End;

        if (start >= 0 && end <= content.Length && start < end)
        {
            var rawYaml = content[start..(end + 1)];

            // Remove the --- markers
            var lines = rawYaml.Split('\n');
            var yamlLines = lines
                .Where(line => !line.TrimEnd('\r').Equals("---", StringComparison.Ordinal))
                .ToArray();

            return string.Join('\n', yamlLines);
        }

        return string.Empty;
    }

    private static (string? Title, string? Slug, string? Description, bool Hidden) ParseYamlFrontMatter(string yamlText)
    {
        string? title = null;
        string? slug = null;
        string? description = null;
        var hidden = false;

        var lines = yamlText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            var colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);

            if (colonIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim();

            // Remove surrounding quotes if present
            value = RemoveQuotes(value);

            if (key.Equals("title", StringComparison.OrdinalIgnoreCase))
            {
                title = string.IsNullOrEmpty(value) ? null : value;
            }
            else if (key.Equals("slug", StringComparison.OrdinalIgnoreCase))
            {
                slug = string.IsNullOrEmpty(value) ? null : value;
            }
            else if (key.Equals("description", StringComparison.OrdinalIgnoreCase))
            {
                description = string.IsNullOrEmpty(value) ? null : value;
            }
            else if (key.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                hidden = ParseBool(value);
            }
            // Unknown keys are ignored
        }

        return (title, slug, description, hidden);
    }

    private static string RemoveQuotes(string value)
    {
        if (value.Length >= 2)
        {
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                return value[1..^1];
            }
        }

        return value;
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.Ordinal);
    }

    private static string? RenderBody(string content, YamlFrontMatterBlock? yamlBlock)
    {
        // Get content after frontmatter
        string bodyMarkdown;

        if (yamlBlock is not null)
        {
            // Find the end of the YAML block (including closing ---)
            var afterYaml = yamlBlock.Span.End + 1;

            // Skip past any remaining --- and newlines
            while (afterYaml < content.Length &&
                   (content[afterYaml] == '-' || content[afterYaml] == '\r' || content[afterYaml] == '\n'))
            {
                afterYaml++;
            }

            bodyMarkdown = afterYaml < content.Length
                ? content[afterYaml..].TrimStart('\r', '\n')
                : string.Empty;
        }
        else
        {
            bodyMarkdown = content;
        }

        if (string.IsNullOrWhiteSpace(bodyMarkdown))
        {
            return null;
        }

        // Render markdown to HTML
        var html = Markdown.ToHtml(bodyMarkdown, Pipeline).Trim();

        return string.IsNullOrWhiteSpace(html) ? null : html;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Index file not found: {FilePath}")]
    private static partial void LogFileNotFound(ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Parsed {FilePath}, hasMetadata: {HasMetadata}")]
    private static partial void LogParsedMetadata(ILogger logger, string filePath, bool hasMetadata);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error reading index file {FilePath}")]
    private static partial void LogReadError(ILogger logger, string filePath, Exception ex);
}

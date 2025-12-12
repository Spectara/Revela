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
        string? template = null;
        Dictionary<string, string> dataSources = [];

        if (yamlBlock is not null)
        {
            var yamlText = ExtractYamlText(content, yamlBlock);
            (title, slug, description, hidden, template, dataSources) = ParseYamlFrontMatter(yamlText);
        }

        // Get raw body (before markdown processing)
        var rawBody = ExtractRawBody(content, yamlBlock);

        // Render the body (everything after frontmatter) - only if no template
        // When template is set, body processing happens later with Scriban
        var body = template is null ? RenderBody(content, yamlBlock) : null;

        if (title is null && slug is null && description is null && !hidden &&
            body is null && template is null && dataSources.Count == 0)
        {
            return DirectoryMetadata.Empty;
        }

        return new DirectoryMetadata
        {
            Title = title,
            Slug = slug,
            Description = description,
            Hidden = hidden,
            Template = template,
            DataSources = dataSources,
            RawBody = rawBody,
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

    private static (string? Title, string? Slug, string? Description, bool Hidden, string? Template, Dictionary<string, string> DataSources) ParseYamlFrontMatter(string yamlText)
    {
        string? title = null;
        string? slug = null;
        string? description = null;
        var hidden = false;
        string? template = null;
        Dictionary<string, string> dataSources = [];

        var lines = yamlText.Split('\n');
        var inDataBlock = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Check if this is an indented line (part of data: block)
            var isIndented = line.StartsWith(' ') || line.StartsWith('\t');

            if (inDataBlock && isIndented)
            {
                // Parse nested data source: "  statistics: statistics.json"
                var trimmed = line.Trim();
                var colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (colonIndex > 0)
                {
                    var key = trimmed[..colonIndex].Trim();
                    var value = RemoveQuotes(trimmed[(colonIndex + 1)..].Trim());
                    if (!string.IsNullOrEmpty(value))
                    {
                        dataSources[key] = value;
                    }
                }
                continue;
            }

            // Not indented - end data block if we were in one
            inDataBlock = false;

            var colonIndex2 = line.IndexOf(':', StringComparison.Ordinal);
            if (colonIndex2 <= 0)
            {
                continue;
            }

            var rootKey = line[..colonIndex2].Trim();
            var rootValue = line[(colonIndex2 + 1)..].Trim();

            // Remove surrounding quotes if present
            rootValue = RemoveQuotes(rootValue);

            if (rootKey.Equals("title", StringComparison.OrdinalIgnoreCase))
            {
                title = string.IsNullOrEmpty(rootValue) ? null : rootValue;
            }
            else if (rootKey.Equals("slug", StringComparison.OrdinalIgnoreCase))
            {
                slug = string.IsNullOrEmpty(rootValue) ? null : rootValue;
            }
            else if (rootKey.Equals("description", StringComparison.OrdinalIgnoreCase))
            {
                description = string.IsNullOrEmpty(rootValue) ? null : rootValue;
            }
            else if (rootKey.Equals("hidden", StringComparison.OrdinalIgnoreCase))
            {
                hidden = ParseBool(rootValue);
            }
            else if (rootKey.Equals("template", StringComparison.OrdinalIgnoreCase))
            {
                template = string.IsNullOrEmpty(rootValue) ? null : rootValue;
            }
            else if (rootKey.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(rootValue))
                {
                    // Object syntax: data:\n  key: value
                    inDataBlock = true;
                }
                else
                {
                    // Legacy single-value syntax: data: statistics.json
                    // Use "statistics" as default variable name
                    dataSources["statistics"] = rootValue;
                }
            }
            // Unknown keys are ignored
        }

        return (title, slug, description, hidden, template, dataSources);
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

    private static string? ExtractRawBody(string content, YamlFrontMatterBlock? yamlBlock)
    {
        if (yamlBlock is null)
        {
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }

        // Find the end of the YAML block (including closing ---)
        var afterYaml = yamlBlock.Span.End + 1;

        // Skip past any remaining --- and newlines
        while (afterYaml < content.Length &&
               (content[afterYaml] == '-' || content[afterYaml] == '\r' || content[afterYaml] == '\n'))
        {
            afterYaml++;
        }

        var rawBody = afterYaml < content.Length
            ? content[afterYaml..].TrimStart('\r', '\n')
            : string.Empty;

        return string.IsNullOrWhiteSpace(rawBody) ? null : rawBody;
    }

    private static string? RenderBody(string content, YamlFrontMatterBlock? yamlBlock)
    {
        var bodyMarkdown = ExtractRawBody(content, yamlBlock);

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

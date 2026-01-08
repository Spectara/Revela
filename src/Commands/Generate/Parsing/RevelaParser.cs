using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

using Spectara.Revela.Commands.Generate.Models;

namespace Spectara.Revela.Commands.Generate.Parsing;

/// <summary>
/// Parses _index.revela files using Scriban's native frontmatter support.
/// </summary>
/// <remarks>
/// <para>
/// The .revela format uses Scriban's <c>+++</c> frontmatter syntax with variable assignments:
/// </para>
/// <code>
/// +++
/// title = "My Gallery"
/// description = "Photos from 2024"
/// template = "body/page"
/// hidden = true
/// data.statistics = "statistics.json"
/// +++
/// # Markdown Content
///
/// With **formatting** and {{ scriban.expressions }}
/// </code>
/// <para>
/// Benefits over YAML frontmatter:
/// </para>
/// <list type="bullet">
/// <item>No syntax confusion (YAML uses <c>---</c>, Scriban uses <c>+++</c>)</item>
/// <item>Native Scriban expressions in frontmatter if needed</item>
/// <item>Single parser for both frontmatter and body</item>
/// <item>Type-safe: booleans are <c>true</c>/<c>false</c>, not strings</item>
/// </list>
/// </remarks>
public sealed partial class RevelaParser(ILogger<RevelaParser> logger)
{
    /// <summary>
    /// The standard filename for directory metadata.
    /// </summary>
    public const string IndexFileName = "_index.revela";

    /// <summary>
    /// Parses the content of a .revela file and returns the extracted metadata.
    /// </summary>
    /// <param name="content">The raw content of the .revela file.</param>
    /// <returns>The parsed <see cref="DirectoryMetadata"/>.</returns>
    public static DirectoryMetadata Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return DirectoryMetadata.Empty;
        }

        // Check if content starts with frontmatter marker
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("+++", StringComparison.Ordinal))
        {
            // No frontmatter - treat entire content as body
            return new DirectoryMetadata
            {
                RawBody = content
            };
        }

        // Ensure content ends with newline - Scriban requires this for frontmatter parsing
        var normalizedContent = content;
        if (!content.EndsWith('\n'))
        {
            normalizedContent = content + "\n";
        }

        // Parse with Scriban's FrontMatterAndContent mode
        var lexerOptions = new LexerOptions
        {
            Mode = ScriptMode.FrontMatterAndContent,
            FrontMatterMarker = "+++"
        };

        var template = Template.Parse(normalizedContent, lexerOptions: lexerOptions);

        if (template.HasErrors)
        {
            // Return empty on parse errors - errors will be logged by caller
            return DirectoryMetadata.Empty;
        }

        // Evaluate frontmatter to extract variables
        var context = new TemplateContext();

        if (template.Page.FrontMatter is not null)
        {
            context.Evaluate(template.Page.FrontMatter);
        }

        // Extract metadata from evaluated context using ScriptObject
        // CurrentGlobal returns IScriptObject, but it's actually ScriptObject at runtime
        var global = (ScriptObject)context.CurrentGlobal;

        var title = GetStringValue(global, "title");
        var slug = GetStringValue(global, "slug");
        var description = GetStringValue(global, "description");
        var hidden = GetBoolValue(global, "hidden");
        var templateName = GetStringValue(global, "template");
        var sort = GetStringValue(global, "sort");
        var filter = GetStringValue(global, "filter");
        var dataSources = GetDataSources(global);

        // Extract raw body (text after frontmatter, before Scriban processing)
        var rawBody = ExtractRawBody(content);

        if (title is null && slug is null && description is null && !hidden &&
            rawBody is null && templateName is null && sort is null && filter is null && dataSources.Count == 0)
        {
            return DirectoryMetadata.Empty;
        }

        return new DirectoryMetadata
        {
            Title = title,
            Slug = slug,
            Description = description,
            Hidden = hidden,
            Template = templateName,
            Sort = sort,
            Filter = filter,
            DataSources = dataSources,
            RawBody = rawBody,
            Body = null // Body is rendered later with full context
        };
    }

    /// <summary>
    /// Parses a .revela file from the filesystem.
    /// </summary>
    /// <param name="filePath">The path to the .revela file.</param>
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

    private static string? GetStringValue(ScriptObject global, string key)
    {
        if (global.TryGetValue(key, out var value) && value is string str)
        {
            return string.IsNullOrEmpty(str) ? null : str;
        }

        return null;
    }

    private static bool GetBoolValue(ScriptObject global, string key)
    {
        if (global.TryGetValue(key, out var value))
        {
            return value switch
            {
                bool b => b,
                string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        return false;
    }

    private static Dictionary<string, string> GetDataSources(ScriptObject global)
    {
        var result = new Dictionary<string, string>();

        if (!global.TryGetValue("data", out var dataValue))
        {
            return result;
        }

        // Handle object syntax: data.statistics = "file.json"
        if (dataValue is ScriptObject dataObj)
        {
            foreach (var key in dataObj.Keys)
            {
                if (dataObj.TryGetValue(key, out var val) && val is string str && !string.IsNullOrEmpty(str))
                {
                    result[key] = str;
                }
            }
        }
        // Handle legacy single value: data = "file.json"
        else if (dataValue is string singleValue && !string.IsNullOrEmpty(singleValue))
        {
            result["statistics"] = singleValue;
        }

        return result;
    }

    private static string? ExtractRawBody(string content)
    {
        // Find the closing +++ marker
        var firstMarker = content.IndexOf("+++", StringComparison.Ordinal);
        if (firstMarker < 0)
        {
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }

        var secondMarker = content.IndexOf("+++", firstMarker + 3, StringComparison.Ordinal);
        if (secondMarker < 0)
        {
            return null;
        }

        // Skip past the closing marker and any following newlines
        var bodyStart = secondMarker + 3;
        while (bodyStart < content.Length && (content[bodyStart] == '\r' || content[bodyStart] == '\n'))
        {
            bodyStart++;
        }

        if (bodyStart >= content.Length)
        {
            return null;
        }

        var rawBody = content[bodyStart..];
        return string.IsNullOrWhiteSpace(rawBody) ? null : rawBody;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Index file not found: {FilePath}")]
    private static partial void LogFileNotFound(ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Parsed {FilePath}, hasMetadata: {HasMetadata}")]
    private static partial void LogParsedMetadata(ILogger logger, string filePath, bool hasMetadata);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Error reading index file {FilePath}")]
    private static partial void LogReadError(ILogger logger, string filePath, Exception ex);
}

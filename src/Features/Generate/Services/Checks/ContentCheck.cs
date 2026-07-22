using Scriban;
using Scriban.Parsing;

using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services.Checks;

/// <summary>
/// Parses every <c>_index.revela</c> file's frontmatter syntactically and reports parse
/// errors (which the scanner would otherwise swallow silently).
/// </summary>
internal sealed class ContentCheck(IPathResolver pathResolver) : ICheck
{
    /// <inheritdoc />
    public string Name => "content";

    /// <inheritdoc />
    public string Title => "Content & metadata";

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        var source = pathResolver.SourcePath;
        if (!Directory.Exists(source))
        {
            return diagnostics;
        }

        var indexFiles = Directory.EnumerateFiles(source, RevelaParser.IndexFileName, SearchOption.AllDirectories);

        foreach (var file in indexFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch (IOException ex)
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    $"Could not read metadata file: {ex.Message}",
                    file: RelativeToSource(source, file)));
                continue;
            }

            if (!content.TrimStart().StartsWith("+++", StringComparison.Ordinal))
            {
                continue;
            }

            var normalized = content.EndsWith('\n') ? content : content + "\n";
            var template = Template.Parse(normalized, lexerOptions: new LexerOptions
            {
                Mode = ScriptMode.FrontMatterAndContent,
                FrontMatterMarker = "+++",
            });

            if (!template.HasErrors)
            {
                continue;
            }

            foreach (var message in template.Messages)
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    $"Invalid frontmatter: {message.Message}",
                    file: RelativeToSource(source, file),
                    line: message.Span.Start.Line + 1,
                    hint: "Fix the '+++' frontmatter block (title = \"...\", one assignment per line)."));
            }
        }

        return diagnostics;
    }

    private static string RelativeToSource(string source, string file)
    {
        var relative = Path.GetRelativePath(source, file);
        return relative.Replace('\\', '/');
    }
}

using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Evaluates the <c>+++</c> frontmatter of an <c>_index.revela</c> file into a Scriban object.
/// </summary>
/// <remarks>
/// <para>
/// Statements are evaluated one by one: a failing statement is skipped without hiding the
/// keys after it, and dotted assignments (<c>calendar.source = "x"</c>) get their missing
/// parent objects created first.
/// </para>
/// <para>
/// This file is the single implementation of these semantics. The Calendar plugin links it
/// (see Calendar.csproj) because the SDK intentionally has no Scriban dependency.
/// </para>
/// </remarks>
internal static class FrontMatterEvaluator
{
    /// <summary>
    /// The frontmatter delimiter used by <c>.revela</c> files.
    /// </summary>
    public const string Marker = "+++";

    /// <summary>
    /// Returns whether <paramref name="content"/> starts with a frontmatter block.
    /// </summary>
    public static bool HasFrontMatter(string content) =>
        content.TrimStart().StartsWith(Marker, StringComparison.Ordinal);

    /// <summary>
    /// Evaluates the frontmatter of <paramref name="content"/>.
    /// </summary>
    /// <param name="content">Raw <c>.revela</c> file content.</param>
    /// <returns>
    /// The evaluated global object, or <see langword="null"/> when the content has no
    /// frontmatter or cannot be parsed.
    /// </returns>
    public static ScriptObject? Evaluate(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || !HasFrontMatter(content))
        {
            return null;
        }

        // Scriban requires a trailing newline after the closing marker
        var normalizedContent = content.EndsWith('\n') ? content : content + "\n";

        var lexerOptions = new LexerOptions
        {
            Mode = ScriptMode.FrontMatterAndContent,
            FrontMatterMarker = Marker
        };

        var template = Template.Parse(normalizedContent, lexerOptions: lexerOptions);
        if (template.HasErrors)
        {
            return null;
        }

        var context = new TemplateContext();
        if (context.CurrentGlobal is not ScriptObject root)
        {
            return null;
        }

        if (template.Page?.FrontMatter is { } frontMatter)
        {
            foreach (var statement in frontMatter.Statements.Statements)
            {
                EnsureAssignmentParents(root, statement);

                try
                {
                    context.Evaluate(statement);
                }
                catch (ScriptRuntimeException)
                {
                    // Skip only this statement; later keys are still evaluated
                }
            }
        }

        return root;
    }

    /// <summary>
    /// For an assignment like <c>a.b.c = value</c>, creates the missing parent objects
    /// <c>a</c> and <c>a.b</c> so Scriban can assign into them.
    /// </summary>
    private static void EnsureAssignmentParents(ScriptObject root, ScriptStatement statement)
    {
        if (statement is ScriptExpressionStatement { Expression: ScriptAssignExpression { Target: ScriptMemberExpression { Target: { } parent } } })
        {
            EnsureObject(root, parent);
        }
    }

    private static ScriptObject? EnsureObject(ScriptObject root, ScriptExpression expression)
    {
        var (parent, name) = expression switch
        {
            ScriptVariableGlobal variable => (root, variable.Name),
            ScriptMemberExpression { Target: { } target, Member: ScriptVariable memberVariable } => (EnsureObject(root, target), memberVariable.Name),
            _ => (null, null)
        };

        if (parent is null || name is null)
        {
            return null;
        }

        if (parent.TryGetValue(name, out var existing) && existing is not null)
        {
            return existing as ScriptObject;
        }

        var created = new ScriptObject();
        parent[name] = created;
        return created;
    }
}

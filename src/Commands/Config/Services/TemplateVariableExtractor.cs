using Scriban;
using Scriban.Syntax;

namespace Spectara.Revela.Commands.Config.Services;

/// <summary>
/// Extracts variable names from Scriban templates.
/// </summary>
/// <remarks>
/// Used to dynamically generate CLI options from template placeholders.
/// For example, a template with {{ title }} and {{ author }} will return ["title", "author"].
/// </remarks>
public static class TemplateVariableExtractor
{
    /// <summary>
    /// Extracts all top-level variable names from a Scriban template.
    /// </summary>
    /// <param name="templateContent">The Scriban template content.</param>
    /// <returns>A set of variable names found in the template.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the template has parse errors.</exception>
    /// <example>
    /// <code>
    /// var template = "{ \"title\": \"{{ title }}\", \"author\": \"{{ author }}\" }";
    /// var variables = TemplateVariableExtractor.ExtractVariables(template);
    /// // Returns: ["title", "author"]
    /// </code>
    /// </example>
    public static IReadOnlySet<string> ExtractVariables(string templateContent)
    {
        var template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template parse errors: {errors}");
        }

        var visitor = new VariableVisitor();
        visitor.Visit(template.Page);

        return visitor.Variables;
    }

    /// <summary>
    /// Scriban AST visitor that collects global variable names.
    /// </summary>
    private sealed class VariableVisitor : ScriptVisitor
    {
        private readonly HashSet<string> variables = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the collected variable names.
        /// </summary>
        public IReadOnlySet<string> Variables => variables;

        /// <summary>
        /// Called for each global variable reference (e.g., {{ title }}).
        /// </summary>
        public override void Visit(ScriptVariableGlobal node)
        {
            variables.Add(node.Name);
            base.Visit(node);
        }
    }
}

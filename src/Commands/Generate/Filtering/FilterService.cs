using System.Linq.Expressions;

using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Service for filtering images using filter expressions.
/// </summary>
/// <remarks>
/// <para>
/// Filter expressions use a simple syntax for querying image metadata:
/// </para>
/// <code>
/// // Simple comparison
/// exif.make == 'Canon'
/// exif.iso >= 800
///
/// // Logical operators
/// exif.make == 'Canon' and exif.iso >= 800
/// exif.make == 'Canon' or exif.make == 'Sony'
///
/// // Functions
/// year(dateTaken) == 2024
/// contains(filename, 'portrait')
/// </code>
/// </remarks>
public sealed class FilterService
{
    /// <summary>
    /// Compiles a filter expression into a predicate.
    /// </summary>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>A compiled predicate for filtering images.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static Func<ImageContent, bool> Compile(string filterExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var expression = CompileToExpression(filterExpression);
        return expression.Compile();
    }

    /// <summary>
    /// Compiles a filter expression into a LINQ expression tree.
    /// </summary>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>A LINQ expression tree.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static Expression<Func<ImageContent, bool>> CompileToExpression(string filterExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        // Tokenize
        var lexer = new FilterLexer(filterExpression);
        var tokens = lexer.Tokenize();

        // Parse to AST
        var parser = new FilterParser(tokens, filterExpression);
        var ast = parser.Parse();

        // Build expression
        var builder = new FilterExpressionBuilder(filterExpression);
        return builder.Build(ast);
    }

    /// <summary>
    /// Filters images using the specified filter expression.
    /// </summary>
    /// <param name="images">The images to filter.</param>
    /// <param name="filterExpression">The filter expression string.</param>
    /// <returns>Images matching the filter.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static IEnumerable<ImageContent> Apply(IEnumerable<ImageContent> images, string filterExpression)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentException.ThrowIfNullOrWhiteSpace(filterExpression);

        var predicate = Compile(filterExpression);
        return images.Where(predicate);
    }

    /// <summary>
    /// Validates a filter expression without executing it.
    /// </summary>
    /// <param name="filterExpression">The filter expression to validate.</param>
    /// <returns>True if the expression is valid.</returns>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    public static bool Validate(string filterExpression)
    {
        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            return false;
        }

        // This will throw if invalid
        _ = CompileToExpression(filterExpression);
        return true;
    }

    /// <summary>
    /// Tries to validate a filter expression and returns the error if invalid.
    /// </summary>
    /// <param name="filterExpression">The filter expression to validate.</param>
    /// <param name="error">The error message if validation fails.</param>
    /// <returns>True if the expression is valid, false otherwise.</returns>
    public static bool TryValidate(string filterExpression, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(filterExpression))
        {
            error = "Filter expression cannot be empty";
            return false;
        }

        try
        {
            _ = CompileToExpression(filterExpression);
            return true;
        }
        catch (FilterParseException ex)
        {
            error = ex.GetDetailedMessage();
            return false;
        }
    }
}

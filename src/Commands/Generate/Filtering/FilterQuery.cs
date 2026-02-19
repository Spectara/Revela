using Spectara.Revela.Commands.Generate.Filtering.Ast;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Generate.Filtering;

/// <summary>
/// Represents a sort clause in a filter query.
/// </summary>
/// <param name="PropertyPath">The property path to sort by (e.g., ["dateTaken"] or ["exif", "iso"]).</param>
/// <param name="Direction">The sort direction.</param>
internal sealed record SortClause(
    IReadOnlyList<string> PropertyPath,
    SortDirection Direction)
{
    /// <summary>
    /// Gets the property path as a dot-separated string.
    /// </summary>
    public string PropertyPathString => string.Join(".", PropertyPath);
}

/// <summary>
/// Represents a complete filter query with optional sort and limit clauses.
/// </summary>
/// <remarks>
/// Syntax: <c>filter_expression [| sort property [asc|desc]] [| limit n]</c>
/// <para>
/// Examples:
/// <list type="bullet">
/// <item><c>exif.make == 'Canon'</c> - Simple filter</item>
/// <item><c>all | sort dateTaken desc</c> - All images sorted by date</item>
/// <item><c>exif.iso &gt;= 3200 | sort dateTaken desc | limit 5</c> - Filter, sort, limit</item>
/// </list>
/// </para>
/// </remarks>
/// <param name="Predicate">The filter predicate AST, or null if "all" was specified.</param>
/// <param name="Sort">Optional sort clause.</param>
/// <param name="Limit">Optional limit on number of results.</param>
internal sealed record FilterQuery(
    FilterNode? Predicate,
    SortClause? Sort,
    int? Limit)
{
    /// <summary>
    /// Gets a value indicating whether this query selects all images (no predicate).
    /// </summary>
    public bool SelectsAll => Predicate is null;

    /// <summary>
    /// Gets a value indicating whether this query has a sort clause.
    /// </summary>
    public bool HasSort => Sort is not null;

    /// <summary>
    /// Gets a value indicating whether this query has a limit clause.
    /// </summary>
    public bool HasLimit => Limit is not null;
}

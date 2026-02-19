namespace Spectara.Revela.Commands.Generate.Filtering.Ast;

/// <summary>
/// Represents a function call (name(args)).
/// </summary>
internal sealed class CallNode : FilterNode
{
    /// <summary>
    /// Gets the function name.
    /// </summary>
    public required string FunctionName { get; init; }

    /// <summary>
    /// Gets the function arguments.
    /// </summary>
    public required IReadOnlyList<FilterNode> Arguments { get; init; }

    /// <inheritdoc />
    public override TResult Accept<TResult>(IFilterNodeVisitor<TResult> visitor) => visitor.Visit(this);
}

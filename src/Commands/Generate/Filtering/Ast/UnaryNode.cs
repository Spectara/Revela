namespace Spectara.Revela.Commands.Generate.Filtering.Ast;

/// <summary>
/// Unary operators for filter expressions.
/// </summary>
public enum UnaryOperator
{
    /// <summary>Logical negation (not).</summary>
    Not
}

/// <summary>
/// Represents a unary expression (op operand).
/// </summary>
public sealed class UnaryNode : FilterNode
{
    /// <summary>
    /// Gets the operator.
    /// </summary>
    public required UnaryOperator Operator { get; init; }

    /// <summary>
    /// Gets the operand.
    /// </summary>
    public required FilterNode Operand { get; init; }

    /// <inheritdoc />
    public override TResult Accept<TResult>(IFilterNodeVisitor<TResult> visitor) => visitor.Visit(this);
}

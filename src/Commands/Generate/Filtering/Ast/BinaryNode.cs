namespace Spectara.Revela.Commands.Generate.Filtering.Ast;

/// <summary>
/// Binary operators for filter expressions.
/// </summary>
public enum BinaryOperator
{
    /// <summary>Equality comparison (==).</summary>
    Equal,

    /// <summary>Inequality comparison (!=).</summary>
    NotEqual,

    /// <summary>Less than comparison (&lt;).</summary>
    LessThan,

    /// <summary>Less than or equal comparison (&lt;=).</summary>
    LessThanOrEqual,

    /// <summary>Greater than comparison (&gt;).</summary>
    GreaterThan,

    /// <summary>Greater than or equal comparison (&gt;=).</summary>
    GreaterThanOrEqual,

    /// <summary>Logical AND.</summary>
    And,

    /// <summary>Logical OR.</summary>
    Or
}

/// <summary>
/// Represents a binary expression (left op right).
/// </summary>
internal sealed class BinaryNode : FilterNode
{
    /// <summary>
    /// Gets the left operand.
    /// </summary>
    public required FilterNode Left { get; init; }

    /// <summary>
    /// Gets the operator.
    /// </summary>
    public required BinaryOperator Operator { get; init; }

    /// <summary>
    /// Gets the right operand.
    /// </summary>
    public required FilterNode Right { get; init; }

    /// <inheritdoc />
    public override TResult Accept<TResult>(IFilterNodeVisitor<TResult> visitor) => visitor.Visit(this);
}

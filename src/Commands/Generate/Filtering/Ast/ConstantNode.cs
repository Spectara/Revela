namespace Spectara.Revela.Commands.Generate.Filtering.Ast;

/// <summary>
/// Represents a constant value (string, number, boolean, null).
/// </summary>
internal sealed class ConstantNode : FilterNode
{
    /// <summary>
    /// Gets the constant value.
    /// </summary>
    /// <remarks>
    /// The value type can be:
    /// <list type="bullet">
    /// <item><see cref="string"/> for string literals</item>
    /// <item><see cref="int"/> or <see cref="double"/> for numbers</item>
    /// <item><see cref="bool"/> for true/false</item>
    /// <item><c>null</c> for null literal</item>
    /// </list>
    /// </remarks>
    public object? Value { get; init; }

    /// <inheritdoc />
    public override TResult Accept<TResult>(IFilterNodeVisitor<TResult> visitor) => visitor.Visit(this);
}

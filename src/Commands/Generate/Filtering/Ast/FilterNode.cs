namespace Spectara.Revela.Commands.Generate.Filtering.Ast;

/// <summary>
/// Base class for all filter expression AST nodes.
/// </summary>
public abstract class FilterNode
{
    /// <summary>
    /// Gets the position in the source where this node starts.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// Accepts a visitor for the visitor pattern.
    /// </summary>
    public abstract TResult Accept<TResult>(IFilterNodeVisitor<TResult> visitor);
}

/// <summary>
/// Visitor interface for filter AST nodes.
/// </summary>
/// <typeparam name="TResult">The type returned by the visitor.</typeparam>
public interface IFilterNodeVisitor<out TResult>
{
    /// <summary>Visits a binary expression node.</summary>
    TResult Visit(BinaryNode node);

    /// <summary>Visits a unary expression node.</summary>
    TResult Visit(UnaryNode node);

    /// <summary>Visits a function call node.</summary>
    TResult Visit(CallNode node);

    /// <summary>Visits a property access node.</summary>
    TResult Visit(PropertyNode node);

    /// <summary>Visits a constant value node.</summary>
    TResult Visit(ConstantNode node);
}

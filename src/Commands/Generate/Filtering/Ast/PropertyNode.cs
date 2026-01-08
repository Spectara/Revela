namespace Spectara.Revela.Commands.Generate.Filtering.Ast;

/// <summary>
/// Represents a property access (object.property or nested object.property.subProperty).
/// </summary>
public sealed class PropertyNode : FilterNode
{
    /// <summary>
    /// Gets the property path segments.
    /// </summary>
    /// <example>
    /// "exif.make" → ["exif", "make"]
    /// "exif.raw.Rating" → ["exif", "raw", "Rating"]
    /// </example>
    public required IReadOnlyList<string> Path { get; init; }

    /// <summary>
    /// Gets the full property path as a dot-separated string.
    /// </summary>
    public string FullPath => string.Join(".", Path);

    /// <inheritdoc />
    public override TResult Accept<TResult>(IFilterNodeVisitor<TResult> visitor) => visitor.Visit(this);
}

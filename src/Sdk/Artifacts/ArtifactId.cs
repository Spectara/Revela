namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Identifies a generated artifact across package boundaries.
/// </summary>
public readonly record struct ArtifactId
{
    /// <summary>
    /// Creates an artifact identifier from a canonical, namespaced value.
    /// </summary>
    /// <param name="value">Identifier in <c>namespace/name</c> form.</param>
    /// <exception cref="ArgumentException">The value is not canonical.</exception>
    public ArtifactId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!value.Equals(value.Trim(), StringComparison.Ordinal) ||
            !value.Contains('/', StringComparison.Ordinal) ||
            value.StartsWith('/') ||
            value.EndsWith('/') ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Artifact identifiers must use canonical 'namespace/name' form.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical identifier value.</summary>
    public string Value { get; }

    /// <summary>Whether this value is the uninitialized default.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

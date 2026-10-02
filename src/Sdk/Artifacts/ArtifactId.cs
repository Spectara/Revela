namespace Spectara.Revela.Sdk.Artifacts;

/// <summary>
/// Identifies a generated artifact across package boundaries.
/// </summary>
/// <remarks>
/// The canonical form is <c>owner/name</c>. The owner names the package (or Revela core) that
/// produces the artifact and its folder <c>.revela/&lt;owner&gt;/</c>
/// (<see cref="ProjectPaths.GetOwnerDirectory"/>); it must be a valid owner name
/// (<see cref="ProjectPaths.IsValidOwner"/>), for example <c>statistics</c> or <c>acme.captions</c>.
/// </remarks>
public readonly record struct ArtifactId
{
    /// <summary>
    /// Creates an artifact identifier from a canonical, owner-qualified value.
    /// </summary>
    /// <param name="value">Identifier in <c>owner/name</c> form.</param>
    /// <exception cref="ArgumentException">The value is not canonical.</exception>
    public ArtifactId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var separator = value.IndexOf('/', StringComparison.Ordinal);
        if (separator <= 0 ||
            !ProjectPaths.IsValidOwner(value[..separator]) ||
            value.EndsWith('/') ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Artifact identifiers must use canonical 'owner/name' form with a valid owner name.",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>Gets the canonical identifier value.</summary>
    public string Value { get; }

    /// <summary>
    /// Gets the owner part (before the first <c>/</c>), which also names the owner's folder
    /// <c>.revela/&lt;owner&gt;/</c>.
    /// </summary>
    public string Owner => IsEmpty ? string.Empty : Value[..Value.IndexOf('/', StringComparison.Ordinal)];

    /// <summary>Whether this value is the uninitialized default.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

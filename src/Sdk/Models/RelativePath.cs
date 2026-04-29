namespace Spectara.Revela.Sdk.Models;

/// <summary>
/// A relative path or URL segment, used throughout the manifest, render context,
/// and templates. Distinct from <see cref="Uri"/> (which models absolute URIs)
/// and from absolute filesystem paths.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this is:</b> a slugified or sanitized path segment that other code
/// concatenates with a base path/URL to produce a final reference. Examples:
/// </para>
/// <list type="bullet">
///   <item><description><c>"events/fireworks"</c> — gallery slug used as URL fragment</description></item>
///   <item><description><c>"events/fireworks/029081"</c> — image slug used to build variant URLs</description></item>
///   <item><description><c>"hero/cover.jpg"</c> — relative cover image reference</description></item>
/// </list>
/// <para>
/// <b>Cross-platform:</b> the constructor normalizes Windows backslashes (<c>\</c>)
/// to forward slashes (<c>/</c>) so that the same value compares equal regardless
/// of the OS that produced it. This eliminates a class of CI-on-Linux failures.
/// </para>
/// <para>
/// <b>JSON:</b> serialized as a plain string (e.g. <c>"events/fireworks"</c>), not
/// as an object — see <see cref="RelativePathJsonConverter"/>.
/// </para>
/// <para>
/// <b>Why a struct:</b> zero allocation, value semantics, and the type system
/// distinguishes path segments from URLs and arbitrary strings — eliminating the
/// need for <c>CA1056</c> suppressions on properties that look URL-like but aren't.
/// </para>
/// <para>
/// <b>Templates:</b> Scriban calls <see cref="ToString"/> automatically when a
/// <see cref="RelativePath"/> is interpolated, so no template changes are needed
/// when migrating a property from <see cref="string"/> to <see cref="RelativePath"/>.
/// </para>
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(RelativePathJsonConverter))]
public readonly record struct RelativePath(string Value)
{
    /// <summary>
    /// Empty path segment.
    /// </summary>
    public static RelativePath Empty { get; }

    /// <summary>
    /// The underlying path string, with backslashes normalized to forward slashes.
    /// Always non-null — even <c>default(RelativePath).Value</c> returns <see cref="string.Empty"/>.
    /// </summary>
    public string Value
    {
        get => field ?? string.Empty;
        init => field = Normalize(value);
    } = Normalize(Value);

    /// <summary>
    /// Whether this path is empty.
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>
    /// Returns the underlying path string. Used by Scriban templates for implicit
    /// string conversion.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Implicit conversion from <see cref="string"/> for ergonomic construction
    /// (e.g. <c>RelativePath p = "events/fireworks";</c>).
    /// </summary>
    public static implicit operator RelativePath(string value) => new(value);

    /// <summary>
    /// Friendly alternate for the implicit conversion from <see cref="string"/>
    /// (required by CA2225 for language interop).
    /// </summary>
    public static RelativePath FromString(string value) => new(value);

    /// <summary>
    /// Constructs a nullable <see cref="RelativePath"/> from a nullable string.
    /// Returns <c>null</c> when the input is <c>null</c>; otherwise wraps the value.
    /// </summary>
    /// <remarks>
    /// Use this helper when converting optional string properties (e.g. from
    /// configuration or other models) to nullable <see cref="RelativePath"/>
    /// without triggering CS8604 nullability warnings.
    /// </remarks>
    public static RelativePath? FromNullable(string? value) =>
        value is null ? (RelativePath?)null : new RelativePath(value);

    /// <summary>
    /// Implicit conversion to <see cref="string"/> for use in concatenation and
    /// APIs that expect string paths.
    /// </summary>
    public static implicit operator string(RelativePath path) => path.Value;

    /// <summary>
    /// Friendly alternate for the implicit conversion to <see cref="string"/>
    /// (required by CA2225 for language interop).
    /// </summary>
    public string ToStringValue() => Value;

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', '/');
}

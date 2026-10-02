namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Defines a property for page templates that serves dual purpose as CLI option and frontmatter field.
/// </summary>
/// <remarks>
/// Each property can be used in two contexts:
/// <list type="bullet">
/// <item><description><strong>CLI Option:</strong> Generated from <see cref="Name"/> and <see cref="Aliases"/></description></item>
/// <item><description><strong>Frontmatter:</strong> Written to _index.revela if <see cref="FrontmatterKey"/> is set</description></item>
/// </list>
/// </remarks>
/// <example>
/// Page property example:
/// <code>
/// new TemplateProperty
/// {
///     Name = "title",
///     Aliases = ["--title", "-t"],
///     Type = typeof(string),
///     DefaultValue = "Photo Statistics",
///     Description = "Page title (example: 'Gallery Stats')",
///     Required = false,
///     FrontmatterKey = "title"
/// }
/// </code>
/// </example>
public sealed class TemplateProperty
{
    /// <summary>
    /// Gets the property name used for CLI options (e.g., "max-entries" for --max-entries).
    /// </summary>
    /// <remarks>
    /// Should be lowercase, kebab-case for consistency (e.g., "max-entries", "sort-by-count").
    /// </remarks>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the CLI option aliases (e.g., ["--title", "-t"]).
    /// </summary>
    /// <remarks>
    /// First alias is the long form (--title), second is optional short form (-t).
    /// At least one alias is required for CLI option generation.
    /// </remarks>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>
    /// Gets the property type (typeof(string), typeof(int), typeof(bool), etc.).
    /// </summary>
    /// <remarks>
    /// Selects the System.CommandLine option type. Supported types: string, int, bool;
    /// <c>revela create page</c> rejects any other type.
    /// </remarks>
    public required Type Type { get; init; }

    /// <summary>
    /// Gets the default value for the property (used when user doesn't provide a value).
    /// </summary>
    /// <remarks>
    /// Written as a comment in frontmatter when not provided by user (e.g., # title = "Photo Statistics").
    /// Must be compatible with <see cref="Type"/>.
    /// </remarks>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets the description shown in CLI help text, including examples.
    /// </summary>
    /// <remarks>
    /// Format: "Description (example: 'example value')"
    /// Example: "Page title (example: 'Gallery Stats')"
    /// </remarks>
    public required string Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether the property is required (user must provide a value).
    /// </summary>
    /// <remarks>
    /// <c>revela create page</c> fails without writing the page when a required value is missing
    /// or an empty string, and the interactive mode asks again until one is given. Most properties
    /// are optional with sensible defaults.
    /// </remarks>
    public bool Required { get; init; }

    /// <summary>
    /// Gets the key name used in _index.revela frontmatter (e.g., "title").
    /// </summary>
    /// <remarks>
    /// Set to <c>null</c> for CLI-only properties that shouldn't appear in frontmatter (e.g., --path).
    /// Written as: <c>title = "value"</c> or <c># title = "default"</c> (commented if not provided).
    /// </remarks>
    public string? FrontmatterKey { get; init; }

    /// <summary>
    /// Gets an optional function to format the value for frontmatter output.
    /// </summary>
    /// <remarks>
    /// Used for custom formatting (e.g., escaping strings, formatting booleans as true/false).
    /// If <c>null</c>, default formatting is used based on <see cref="Type"/>.
    /// </remarks>
    public Func<object?, string>? FormatValue { get; init; }
}

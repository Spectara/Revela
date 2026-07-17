using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Site identity configuration — the validated core of <c>site.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// Loaded from <c>site.json</c> (bound under the <c>site</c> configuration section).
/// Holds the content/identity properties that templates and plugins consume,
/// regardless of who reads them: title, description, author, copyright and language.
/// </para>
/// <para>
/// This is deliberately distinct from <see cref="ProjectConfig"/>, which steers
/// <em>how</em> the build runs (paths, base URL, hosting subpath, theme). Nothing in
/// the build pipeline reads <see cref="Language"/> — only render output and plugins do
/// (e.g. <c>&lt;html lang="…"&gt;</c>, the Calendar plugin's locale default, future RSS/JSON-LD).
/// </para>
/// <para>
/// Properties beyond this core (e.g. <c>heroImage</c>, <c>socialLinks</c>) are theme
/// territory and remain available to templates via the dynamic <c>site.json</c> tail —
/// they are intentionally not modelled here.
/// </para>
/// <example>
/// <code>
/// // site.json
/// {
///   "title": "My Portfolio",
///   "description": "Landscapes and portraits",
///   "author": "Jane Doe",
///   "copyright": "© 2026 Jane Doe",
///   "language": "en"
/// }
/// </code>
/// </example>
/// </remarks>
[RevelaConfig("site")]
public sealed class SiteCoreConfig
{
    /// <summary>
    /// Configuration section name. Matches the <c>[RevelaConfig]</c> attribute
    /// argument; passed to <c>BindConfiguration</c> at registration time.
    /// Hand-written because the .NET Configuration Binding Source Generator
    /// only intercepts call sites where the section argument is statically
    /// resolvable from user-written source (constants emitted from another
    /// source generator are invisible to it).
    /// </summary>
    public const string Section = "site";

    /// <summary>
    /// Site title. Required — a <c>site.json</c> without a title fails validation at load.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "site.json must define a 'title'.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional site description (used for meta tags, feed descriptions, …).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional site author.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Optional copyright notice.
    /// </summary>
    public string? Copyright { get; set; }

    /// <summary>
    /// Primary language code (e.g., "en", "de"). Defaults to "en".
    /// Consumed by render output and plugins (Calendar locale default, RSS, JSON-LD).
    /// </summary>
    public string Language { get; set; } = "en";
}

/// <summary>
/// Trim/AOT-safe <see cref="IValidateOptions{TOptions}"/> implementation for
/// <see cref="SiteCoreConfig"/>. The body is emitted by the
/// <c>Microsoft.Extensions.Options</c> source generator from the
/// <c>DataAnnotations</c> on the config type.
/// </summary>
[OptionsValidator]
public sealed partial class SiteCoreConfigValidator : IValidateOptions<SiteCoreConfig>;

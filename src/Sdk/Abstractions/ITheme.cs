namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme provider interface — provides templates, assets, and configuration.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the previous <c>ITheme</c> + <c>IThemeExtension</c> split.
/// A single interface handles both base themes and extensions:
/// </para>
/// <list type="bullet">
/// <item><see cref="Prefix"/> = null → base theme</item>
/// <item><see cref="Prefix"/> = "statistics" → extension scoped under that prefix</item>
/// </list>
/// <para>
/// <see cref="TargetTheme"/> indicates which theme an extension targets.
/// Base themes have <c>TargetTheme = null</c>.
/// </para>
/// </remarks>
public interface ITheme : IPackage
{
    /// <summary>
    /// Prefix for extension templates, or null for base themes.
    /// </summary>
    /// <remarks>
    /// Extensions provide templates under <c>partials/{Prefix}/</c> and assets under <c>{Prefix}/</c>.
    /// Base themes have null prefix — their files live at the root.
    /// </remarks>
    string? Prefix { get; }

    /// <summary>
    /// Name of the target theme this extends, or null for standalone themes.
    /// </summary>
    /// <remarks>
    /// Matched case-insensitively against other themes' <see cref="PackageMetadata.Name"/>.
    /// </remarks>
    string? TargetTheme { get; }

    /// <summary>
    /// Theme manifest with layout template path and variables.
    /// </summary>
    ThemeManifest Manifest { get; }

    /// <summary>
    /// Get a file from the theme as a stream.
    /// </summary>
    /// <param name="relativePath">Relative path within the theme (e.g., "Layout.revela").</param>
    /// <returns>Stream with file contents, or null if not found.</returns>
    Stream? GetFile(string relativePath);

    /// <summary>
    /// Get all file paths in the theme.
    /// </summary>
    IEnumerable<string> GetAllFiles();

    /// <summary>
    /// Extract all theme files to a directory.
    /// </summary>
    Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the site.json template for project initialization.
    /// </summary>
    /// <returns>Stream with template contents, or null if not provided.</returns>
    Stream? GetSiteTemplate() => null;

    /// <summary>
    /// Get the images configuration template for image processing setup.
    /// </summary>
    /// <returns>Stream with template contents, or null if not provided.</returns>
    Stream? GetImagesTemplate() => null;

    /// <summary>
    /// Get default data sources for a template (used by extensions).
    /// </summary>
    /// <param name="templateKey">Template key relative to extension prefix.</param>
    /// <returns>Dictionary of variable name → default filename, or empty.</returns>
    IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey) =>
        EmptyDataDefaults;

    /// <summary>
    /// Shared empty dictionary to avoid per-call allocation.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyDataDefaults =
        new Dictionary<string, string>();
}

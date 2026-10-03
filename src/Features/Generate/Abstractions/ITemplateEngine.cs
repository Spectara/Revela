using Spectara.Revela.Core.Themes;
using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Abstractions;

/// <summary>
/// Abstraction for template rendering operations
/// </summary>
/// <remarks>
/// <para>
/// One engine serves one render run: configure it once with <see cref="SetImageLookup"/>,
/// <see cref="SetStrings"/> and <see cref="SetAssetFingerprints"/>, then render every page with it. Rendering is thread-safe, and
/// templates and includes are parsed only once per engine.
/// </para>
/// <para>
/// Model values must be Scriban-native (primitives, strings, string lists, <c>ScriptObject</c>
/// or <c>ScriptArray</c>) — never raw .NET objects, which Scriban would read through reflection
/// that the trimmed Native AOT build does not support.
/// </para>
/// </remarks>
internal interface ITemplateEngine
{
    /// <summary>
    /// Set the image lookup for the <c>find_image</c> template function.
    /// </summary>
    /// <param name="imagesBySourcePath">All processed images (gallery and shared <c>_images/</c>) keyed by normalized source path</param>
    void SetImageLookup(IReadOnlyDictionary<string, Image> imagesBySourcePath);

    /// <summary>
    /// Set the theme UI strings used by the <c>t</c> function and the culture used by
    /// <c>format_date</c>/<c>format_filesize</c>.
    /// </summary>
    /// <remarks>
    /// Without strings, <c>t</c> renders keys and formatting is culture-invariant.
    /// </remarks>
    /// <param name="themeStrings">Strings resolved for the site language</param>
    void SetStrings(ThemeStrings themeStrings);

    /// <summary>
    /// Set the content fingerprint source for <c>asset_url</c>, which appends it as
    /// <c>?v=&lt;fingerprint&gt;</c> so changed assets get new URLs.
    /// </summary>
    /// <remarks>
    /// Called concurrently while pages render. Without a source, or when it returns <c>null</c>
    /// for a path, <c>asset_url</c> renders the plain unversioned URL.
    /// </remarks>
    /// <param name="fingerprint">Maps an asset path relative to <c>_assets/</c> to its fingerprint</param>
    void SetAssetFingerprints(Func<string, string?> fingerprint);

    /// <summary>
    /// Render template content with data model
    /// </summary>
    /// <param name="templateContent">Template source</param>
    /// <param name="model">Global variables for the template</param>
    /// <returns>Rendered output</returns>
    string Render(string templateContent, IReadOnlyDictionary<string, object?> model);
}

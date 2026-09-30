using System.Text.Json.Nodes;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Json;

namespace Spectara.Revela.Features.Theme.Services;

/// <summary>
/// Turns a fully extracted theme folder into a local theme by writing its <c>theme.json</c>.
/// </summary>
/// <remarks>
/// Bundled themes ship a read-only <c>manifest.json</c>, but local themes are only
/// recognised by <c>theme.json</c> (see <c>ThemeRegistry</c> / <c>LocalThemeProvider</c>).
/// Both full-extraction paths (CLI command and <see cref="ThemeService"/>) call this after
/// <see cref="ITheme.ExtractToAsync"/> so the result is usable as <c>theme.name</c>.
/// </remarks>
internal static class LocalThemeManifest
{
    internal const string FileName = "theme.json";
    private const string BundledFileName = "manifest.json";

    /// <summary>
    /// Writes <c>theme.json</c> into <paramref name="themePath"/> named <paramref name="themeName"/>,
    /// sourced from the extracted <c>manifest.json</c> (removed afterwards) or the source theme.
    /// </summary>
    public static async Task WriteAsync(ITheme source, string themePath, string themeName, CancellationToken cancellationToken = default)
    {
        var themeJsonPath = Path.Combine(themePath, FileName);
        var bundledPath = Path.Combine(themePath, BundledFileName);

        string json;
        if (File.Exists(bundledPath))
        {
            json = await File.ReadAllTextAsync(bundledPath, cancellationToken);
        }
        else if (File.Exists(themeJsonPath))
        {
            json = await File.ReadAllTextAsync(themeJsonPath, cancellationToken);
        }
        else
        {
            // Local source themes do not extract their own theme.json.
            await using var stream = source.GetFile(FileName) ?? source.GetFile(BundledFileName)
                ?? throw new InvalidOperationException($"Theme '{source.Metadata.Name}' provides no {FileName} or {BundledFileName}.");
            using var reader = new StreamReader(stream);
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        await File.WriteAllTextAsync(themeJsonPath, WithName(json, themeName), cancellationToken);

        if (File.Exists(bundledPath))
        {
            File.Delete(bundledPath);
        }
    }

    /// <summary>
    /// Sets the manifest name, keeping the original text (and comments) when it already matches.
    /// </summary>
    private static string WithName(string json, string themeName)
    {
        if (JsonNode.Parse(json, nodeOptions: null, RevelaJsonOptions.LenientDocument) is not JsonObject manifest)
        {
            throw new InvalidOperationException("Theme manifest must be a JSON object.");
        }

        if (manifest["name"] is JsonValue current
            && current.TryGetValue<string>(out var name)
            && string.Equals(name, themeName, StringComparison.Ordinal))
        {
            return json;
        }

        manifest["name"] = themeName;
        return manifest.ToJsonString(RevelaJsonOptions.Write);
    }
}

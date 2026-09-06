using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services.Checks;

/// <summary>
/// Validates the configured theme is installed and provides the required templates
/// (layout + the content-image partial the renderer hard-depends on).
/// </summary>
internal sealed class ThemeCheck(
    IThemeRegistry themeRegistry,
    ITemplateResolver templateResolver,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig) : ICheck
{
    private const string ContentImagePartialKey = "partials/contentimage.revela";
    private const string PhotoTemplateKey = "body/photo.revela";

    /// <inheritdoc />
    public string Name => "theme";

    /// <inheritdoc />
    public string Title => "Theme";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrEmpty(themeName))
        {
            themeName = "Lumina";
        }

        var projectPath = projectEnvironment.Value.Path;
        ITheme? theme;
        try
        {
            theme = themeRegistry.Resolve(themeName, projectPath);
        }
        catch (InvalidOperationException ex)
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                ex.Message,
                hint: "Fix the local theme.json. Theme stylesheets/scripts use objects such as {\"path\":\"main.css\"}, not the site.json string shorthand."));
            return new ValueTask<IReadOnlyList<ValidationDiagnostic>>(diagnostics);
        }

        if (theme is null)
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Theme '{themeName}' is not installed.",
                hint: $"Run 'revela theme install {themeName}' or pick an installed theme with 'revela config theme'."));
            return new ValueTask<IReadOnlyList<ValidationDiagnostic>>(diagnostics);
        }

        var extensions = themeRegistry.GetExtensions(themeName);
        templateResolver.Initialize(theme, extensions, projectPath);

        var layoutKey = theme.Manifest.LayoutTemplate;
        if (!TemplateExists(layoutKey))
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Theme '{themeName}' is missing its layout template ('{layoutKey}').",
                hint: "The theme package looks incomplete — try reinstalling it."));
        }

        if (!TemplateExists(ContentImagePartialKey))
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Theme '{themeName}' is missing the required partial 'Partials/ContentImage.revela'.",
                hint: "This partial renders images in Markdown body content — reinstall the theme."));
        }

        if (theme.Manifest.PhotoViewer?.Supported.Contains(PhotoViewerMode.Page) is true
            && !TemplateExists(PhotoTemplateKey))
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Theme '{themeName}' supports the 'page' photo viewer but is missing 'Body/Photo.revela'.",
                hint: "Add the photo page template or remove 'page' from the theme's supported photo viewers."));
        }

        return new ValueTask<IReadOnlyList<ValidationDiagnostic>>(diagnostics);
    }

    private bool TemplateExists(string key)
    {
        using var stream = templateResolver.GetTemplate(key);
        return stream is not null;
    }
}

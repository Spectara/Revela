using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Features.Generate.Services.Checks;

/// <summary>
/// Validates project and site configuration: surfaces options-binding failures as
/// friendly errors, requires a site title, and emits the non-blocking base-URL hint.
/// </summary>
internal sealed class ConfigCheck(
    IOptionsMonitor<ProjectConfig> projectConfig,
    IOptionsMonitor<SiteCoreConfig> siteConfig) : ICheck
{
    /// <inheritdoc />
    public string Name => "config";

    /// <inheritdoc />
    public string Title => "Configuration";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        CollectConfigFailures(diagnostics, () => _ = projectConfig.CurrentValue);
        CollectConfigFailures(diagnostics, () => _ = siteConfig.CurrentValue);

        // SiteCoreConfig.Title carries no [Required] annotation (site.json is written
        // incrementally by the wizard/CLI, so the model must not throw mid-write). The
        // required-title check therefore lives here, at the call site.
        if (string.IsNullOrWhiteSpace(siteConfig.CurrentValue.Title))
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                "site.json must define a 'title'.",
                hint: "Set \"title\" in site.json (or run 'revela config site')."));
        }

        AddBaseUrlHint(diagnostics);

        return new ValueTask<IReadOnlyList<ValidationDiagnostic>>(diagnostics);
    }

    private static void CollectConfigFailures(List<ValidationDiagnostic> diagnostics, Action access)
    {
        try
        {
            access();
        }
        catch (OptionsValidationException ex)
        {
            foreach (var failure in ex.Failures)
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    failure,
                    hint: "Fix the setting in project.json (or site.json), then run the command again."));
            }
        }
    }

    /// <summary>
    /// Adds a friendly, non-blocking hint when no absolute base URL is configured, since
    /// the renderer then skips sitemap.xml and absolute Open Graph URLs.
    /// </summary>
    private void AddBaseUrlHint(List<ValidationDiagnostic> diagnostics)
    {
        Uri? baseUrl;
        try
        {
            baseUrl = projectConfig.CurrentValue.BaseUrl;
        }
        catch (OptionsValidationException)
        {
            // Configuration is invalid; that has already been reported as an error.
            return;
        }

        if (baseUrl is null)
        {
            diagnostics.Add(ValidationDiagnostic.Hint(
                "No baseUrl is configured, so sitemap.xml and absolute Open Graph URLs will be skipped.",
                hint: "Set project.baseUrl in project.json (e.g. \"https://example.com\") when you deploy."));
        }
    }
}

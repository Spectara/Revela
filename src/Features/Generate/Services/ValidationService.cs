using Microsoft.Extensions.Options;

using Scriban;
using Scriban.Parsing;

using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Single shared implementation of <see cref="ISiteValidator"/> backing both the
/// standalone <c>revela check</c> command and Phase 0 of <c>generate all</c>.
/// </summary>
/// <remarks>
/// Performs only cheap, structural checks (no image decode, no network) and collects
/// every finding in one pass. See <see cref="ISiteValidator"/> for the contract.
/// </remarks>
internal sealed partial class ValidationService(
    IPathResolver pathResolver,
    IThemeRegistry themeRegistry,
    ITemplateResolver templateResolver,
    ContentScanner contentScanner,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IOptionsMonitor<ProjectConfig> projectConfig,
    IOptionsMonitor<SiteCoreConfig> siteConfig,
    ILogger<ValidationService> logger) : ISiteValidator
{
    private const string ContentImagePartialKey = "partials/contentimage.revela";

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        LogValidationStarted(logger);

        var diagnostics = new List<ValidationDiagnostic>();

        ValidateConfiguration(diagnostics);
        var sourceHasContent = ValidateSourceDirectory(diagnostics);
        ValidateOutputWritable(diagnostics);
        ValidateTheme(diagnostics);
        await ValidateFrontMatterAsync(diagnostics, cancellationToken);

        if (sourceHasContent)
        {
            await ValidateSlugCollisionsAsync(diagnostics, cancellationToken);
        }

        AddBaseUrlHint(diagnostics);

        LogValidationCompleted(logger, diagnostics.Count);
        return diagnostics;
    }

    /// <summary>
    /// Triggers the existing options validation (the #75 / Bundle A path) by touching the
    /// validated config sections, turning any failure into collected error diagnostics
    /// instead of an unhandled <see cref="OptionsValidationException"/>.
    /// </summary>
    private void ValidateConfiguration(List<ValidationDiagnostic> diagnostics)
    {
        CollectConfigFailures(diagnostics, () => _ = projectConfig.CurrentValue);
        CollectConfigFailures(diagnostics, () => _ = siteConfig.CurrentValue);
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
    /// Verifies the source directory exists and holds content. Returns whether the source
    /// is present and non-empty (so downstream scans can be skipped when it is not).
    /// </summary>
    private bool ValidateSourceDirectory(List<ValidationDiagnostic> diagnostics)
    {
        var source = pathResolver.SourcePath;

        if (!Directory.Exists(source))
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Source directory not found: {source}",
                hint: "Create the source/ folder and add your photos, or run 'revela config paths'."));
            return false;
        }

        var hasEntries = Directory.EnumerateFileSystemEntries(source).Any();
        if (!hasEntries)
        {
            diagnostics.Add(ValidationDiagnostic.Warning(
                $"Source directory is empty: {source}",
                hint: "Add galleries or photos before generating — the build would produce an empty site."));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verifies the output location can be written to, probing the nearest existing
    /// ancestor so the check itself never creates directories.
    /// </summary>
    private void ValidateOutputWritable(List<ValidationDiagnostic> diagnostics)
    {
        var output = pathResolver.OutputPath;
        var probeDir = NearestExistingDirectory(output);

        if (probeDir is null)
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Output location is not reachable: {output}",
                hint: "Check the 'output' path in project.json points somewhere Revela can create."));
            return;
        }

        var probeFile = Path.Combine(probeDir, $".revela-write-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probeFile, string.Empty);
            File.Delete(probeFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Output directory is not writable: {output}",
                hint: "Check folder permissions or choose a different 'output' path in project.json."));
        }
    }

    /// <summary>
    /// Verifies the configured theme is installed and provides the required templates
    /// (layout + the content-image partial that the renderer hard-depends on).
    /// </summary>
    private void ValidateTheme(List<ValidationDiagnostic> diagnostics)
    {
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrEmpty(themeName))
        {
            themeName = "Lumina";
        }

        var projectPath = projectEnvironment.Value.Path;
        var theme = themeRegistry.Resolve(themeName, projectPath);

        if (theme is null)
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Theme '{themeName}' is not installed.",
                hint: $"Run 'revela theme install {themeName}' or pick an installed theme with 'revela config theme'."));
            return;
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
    }

    private bool TemplateExists(string key)
    {
        using var stream = templateResolver.GetTemplate(key);
        return stream is not null;
    }

    /// <summary>
    /// Parses every <c>_index.revela</c> file's frontmatter syntactically and reports
    /// parse errors (which the scanner would otherwise swallow silently).
    /// </summary>
    private async ValueTask ValidateFrontMatterAsync(List<ValidationDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var source = pathResolver.SourcePath;
        if (!Directory.Exists(source))
        {
            return;
        }

        var indexFiles = Directory.EnumerateFiles(source, RevelaParser.IndexFileName, SearchOption.AllDirectories);

        foreach (var file in indexFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string content;
            try
            {
                content = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch (IOException ex)
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    $"Could not read metadata file: {ex.Message}",
                    file: RelativeToSource(source, file)));
                continue;
            }

            if (!content.TrimStart().StartsWith("+++", StringComparison.Ordinal))
            {
                continue;
            }

            var normalized = content.EndsWith('\n') ? content : content + "\n";
            var template = Template.Parse(normalized, lexerOptions: new LexerOptions
            {
                Mode = ScriptMode.FrontMatterAndContent,
                FrontMatterMarker = "+++",
            });

            if (!template.HasErrors)
            {
                continue;
            }

            foreach (var message in template.Messages)
            {
                diagnostics.Add(ValidationDiagnostic.Error(
                    $"Invalid frontmatter: {message.Message}",
                    file: RelativeToSource(source, file),
                    line: message.Span.Start.Line + 1,
                    hint: "Fix the '+++' frontmatter block (title = \"...\", one assignment per line)."));
            }
        }
    }

    /// <summary>
    /// Reuses the content scanner to compute gallery slugs and reports any two source
    /// locations that would resolve to the same URL — a collision that is not detected
    /// anywhere else in the pipeline today.
    /// </summary>
    private async ValueTask ValidateSlugCollisionsAsync(List<ValidationDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var source = pathResolver.SourcePath;
        var tree = await contentScanner.ScanAsync(source, cancellationToken);

        var collisions = tree.Galleries
            .GroupBy(g => g.Slug, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in collisions)
        {
            var url = string.IsNullOrEmpty(group.Key) ? "/" : "/" + group.Key;
            var sources = group
                .Select(g => string.IsNullOrEmpty(g.Path) ? "(site root)" : g.Path)
                .OrderBy(path => path, StringComparer.Ordinal);

            diagnostics.Add(ValidationDiagnostic.Error(
                $"Slug collision: {string.Join(", ", sources)} all resolve to the same URL '{url}'.",
                hint: "Rename one of the folders so each gallery gets a unique URL."));
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

    private static string RelativeToSource(string source, string file)
    {
        var relative = Path.GetRelativePath(source, file);
        return relative.Replace('\\', '/');
    }

    private static string? NearestExistingDirectory(string path)
    {
        var current = Path.TrimEndingDirectorySeparator(path);
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Site validation started")]
    private static partial void LogValidationStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Site validation completed with {Count} diagnostic(s)")]
    private static partial void LogValidationCompleted(ILogger logger, int count);
}

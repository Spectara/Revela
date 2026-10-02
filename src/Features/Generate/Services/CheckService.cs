using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Aggregates every registered <see cref="ICheck"/> into the single collect-all report
/// that backs the standalone <c>revela check</c> command.
/// </summary>
/// <remarks>
/// The source is scanned at most once per run: the shared <see cref="ContentTree"/> is
/// handed to every <see cref="IContentAwareCheck"/> so content-aware units do not each
/// re-scan. Checks run in registration order. Any <see cref="ValidationSeverity.Error"/>
/// makes <c>revela check</c> exit with code 2; warnings and hints are only reported.
/// <c>generate</c> does not run checks.
/// </remarks>
internal sealed partial class CheckService(
    IEnumerable<ICheck> checks,
    ContentScanner contentScanner,
    IPathResolver pathResolver,
    ILogger<CheckService> logger) : ISiteValidator
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        LogValidationStarted(logger);

        var diagnostics = new List<ValidationDiagnostic>();

        // Scan the source at most once and share the tree with every content-aware check.
        ContentTree? tree = null;
        var scanned = false;

        async ValueTask<ContentTree?> GetTreeAsync()
        {
            if (!scanned)
            {
                scanned = true;
                var source = pathResolver.SourcePath;
                tree = Directory.Exists(source)
                    ? await contentScanner.ScanAsync(source, cancellationToken)
                    : null;
            }

            return tree;
        }

        foreach (var check in checks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (check is IContentAwareCheck contentAware)
            {
                var scan = await GetTreeAsync();
                if (scan is not null)
                {
                    diagnostics.AddRange(await contentAware.ValidateAsync(scan, cancellationToken));
                }
            }
            else
            {
                diagnostics.AddRange(await check.ValidateAsync(cancellationToken));
            }
        }

        LogValidationCompleted(logger, diagnostics.Count);
        return diagnostics;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Site validation started")]
    private static partial void LogValidationStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Site validation completed with {Count} diagnostic(s)")]
    private static partial void LogValidationCompleted(ILogger logger, int count);
}

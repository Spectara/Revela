using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services.Checks;

/// <summary>
/// Reports any two source locations that would resolve to the same gallery URL — a
/// collision that is not detected anywhere else in the pipeline.
/// </summary>
/// <remarks>
/// Consumes the scanned <see cref="ContentTree"/>. During <c>check all</c> the shared
/// tree is supplied via <see cref="IContentAwareCheck"/>; run on its own the check scans
/// the source for itself.
/// </remarks>
internal sealed class SlugsCheck(
    ContentScanner contentScanner,
    IPathResolver pathResolver) : ICheck, IContentAwareCheck
{
    /// <inheritdoc />
    public string Name => "slugs";

    /// <inheritdoc />
    public string Title => "Gallery URLs";

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var source = pathResolver.SourcePath;
        if (!Directory.Exists(source))
        {
            return [];
        }

        var tree = await contentScanner.ScanAsync(source, cancellationToken);
        return Evaluate(tree);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(ContentTree tree, CancellationToken cancellationToken = default) =>
        new(Evaluate(tree));

    private static IReadOnlyList<ValidationDiagnostic> Evaluate(ContentTree tree)
    {
        var diagnostics = new List<ValidationDiagnostic>();

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

        return diagnostics;
    }
}

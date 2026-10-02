using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services.Checks;

/// <summary>
/// Validates project structure: the source directory exists and holds content, and the
/// output location can be written to.
/// </summary>
internal sealed class StructureCheck(IPathResolver pathResolver) : ICheck
{
    /// <inheritdoc />
    public string Name => "structure";

    /// <inheritdoc />
    public string Title => "Project structure";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        ValidateSourceDirectory(diagnostics);
        ValidateOutputWritable(diagnostics);

        return new ValueTask<IReadOnlyList<ValidationDiagnostic>>(diagnostics);
    }

    /// <summary>
    /// Verifies the source directory exists and holds content.
    /// </summary>
    private void ValidateSourceDirectory(List<ValidationDiagnostic> diagnostics)
    {
        var source = pathResolver.SourcePath;

        if (!Directory.Exists(source))
        {
            diagnostics.Add(ValidationDiagnostic.Error(
                $"Source directory not found: {source}",
                suggestion: "Create the source/ folder and add your photos, or run 'revela config paths'."));
            return;
        }

        var hasEntries = Directory.EnumerateFileSystemEntries(source).Any();
        if (!hasEntries)
        {
            diagnostics.Add(ValidationDiagnostic.Warning(
                $"Source directory is empty: {source}",
                suggestion: "Add galleries or photos before generating — the build would produce an empty site."));
        }
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
                suggestion: "Check the 'output' path in project.json points somewhere Revela can create."));
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
                suggestion: "Check folder permissions or choose a different 'output' path in project.json."));
        }
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
}

using NSubstitute;

using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services.Checks;

/// <summary>
/// Unit tests for <see cref="StructureCheck"/> — source presence/emptiness and output
/// writability, using a real temp directory and a substituted <see cref="IPathResolver"/>.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class StructureCheckTests
{
    [TestMethod]
    public async Task ValidateAsync_MissingSource_ReportsError()
    {
        using var temp = new TempDir();
        var missingSource = Path.Combine(temp.Path, "does-not-exist");
        var check = CreateCheck(missingSource, temp.Path);

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("Source directory not found", StringComparison.Ordinal)),
            "A missing source directory must be a blocking error.");
    }

    [TestMethod]
    public async Task ValidateAsync_EmptySource_ReportsWarningNotError()
    {
        using var temp = new TempDir();
        var source = temp.CreateSubdirectory("source");
        var check = CreateCheck(source, temp.Path);

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Warning
                && d.Message.Contains("empty", StringComparison.OrdinalIgnoreCase)),
            "An empty source directory must warn.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task ValidateAsync_SourceWithContentAndWritableOutput_ReportsNothing()
    {
        using var temp = new TempDir();
        var source = temp.CreateSubdirectory("source");
        await File.WriteAllTextAsync(Path.Combine(source, "photo.jpg"), "x");
        var check = CreateCheck(source, temp.Path);

        var diagnostics = await check.ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    private static StructureCheck CreateCheck(string sourcePath, string outputParent)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(sourcePath);
        pathResolver.OutputPath.Returns(Path.Combine(outputParent, "output"));
        return new StructureCheck(pathResolver);
    }
}

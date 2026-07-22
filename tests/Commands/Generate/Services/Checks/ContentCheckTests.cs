using NSubstitute;

using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services.Checks;

/// <summary>
/// Unit tests for <see cref="ContentCheck"/> — syntactic frontmatter validation of
/// <c>_index.revela</c> files.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ContentCheckTests
{
    [TestMethod]
    public async Task ValidateAsync_BrokenFrontmatter_ReportsError()
    {
        using var temp = new TempDir();
        var source = temp.CreateSubdirectory("source");
        var galleryDir = Path.Combine(source, "Broken");
        Directory.CreateDirectory(galleryDir);
        await File.WriteAllTextAsync(
            Path.Combine(galleryDir, "_index.revela"),
            "+++\ntitle = \"unterminated\n+++\n");

        var diagnostics = await CreateCheck(source).ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("frontmatter", StringComparison.OrdinalIgnoreCase)),
            "A broken frontmatter block must be a blocking error.");
    }

    [TestMethod]
    public async Task ValidateAsync_ValidFrontmatter_ReportsNothing()
    {
        using var temp = new TempDir();
        var source = temp.CreateSubdirectory("source");
        var galleryDir = Path.Combine(source, "Landscapes");
        Directory.CreateDirectory(galleryDir);
        await File.WriteAllTextAsync(
            Path.Combine(galleryDir, "_index.revela"),
            "+++\ntitle = \"Landscapes\"\n+++\n");

        var diagnostics = await CreateCheck(source).ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task ValidateAsync_MissingSource_ReportsNothing()
    {
        using var temp = new TempDir();
        var missing = Path.Combine(temp.Path, "nope");

        var diagnostics = await CreateCheck(missing).ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    private static ContentCheck CreateCheck(string sourcePath)
    {
        var pathResolver = Substitute.For<IPathResolver>();
        pathResolver.SourcePath.Returns(sourcePath);
        return new ContentCheck(pathResolver);
    }
}

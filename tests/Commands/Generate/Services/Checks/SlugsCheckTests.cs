using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Commands.Generate.Services.Checks;

/// <summary>
/// Unit tests for <see cref="SlugsCheck"/> collision detection via the shared-tree
/// (<see cref="IContentAwareCheck"/>) path.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class SlugsCheckTests
{
    [TestMethod]
    public async Task ValidateAsync_TwoGalleriesShareSlug_ReportsCollision()
    {
        var tree = TreeWith(
            Gallery("01 Events", "events"),
            Gallery("02 Events", "events"));

        var diagnostics = await Evaluate(tree);

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("Slug collision", StringComparison.Ordinal)),
            "Two galleries resolving to the same slug must collide.");
    }

    [TestMethod]
    public async Task ValidateAsync_DistinctSlugs_ReportsNothing()
    {
        var tree = TreeWith(
            Gallery("Landscapes", "landscapes"),
            Gallery("Portraits", "portraits"));

        var diagnostics = await Evaluate(tree);

        Assert.IsEmpty(diagnostics);
    }

    private static async Task<IReadOnlyList<ValidationDiagnostic>> Evaluate(ContentTree tree)
    {
        // The IContentAwareCheck path never touches the scanner, so it is safe to omit here.
        IContentAwareCheck check = new SlugsCheck(contentScanner: null!, pathResolver: null!);
        return await check.ValidateAsync(tree);
    }

    private static Gallery Gallery(string path, string slug) => new()
    {
        Title = path,
        Path = path,
        Slug = slug,
    };

    private static ContentTree TreeWith(params Gallery[] galleries) => new()
    {
        Images = [],
        Galleries = galleries,
    };
}

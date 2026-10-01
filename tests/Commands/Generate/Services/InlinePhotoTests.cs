using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Tests for preparing and rendering <c>[[photo: …]]</c> blocks in Markdown content.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class InlinePhotoTests
{
    private const string SourcePath = "source/story/_index.revela";

    [TestMethod]
    public void PrepareGalleryBlocks_PhotoBeforeFilteredGallery_KeepsGridNumbering()
    {
        // Arrange
        var service = new MarkdownService();
        const string withoutPhoto = "[[gallery: filename == 'a.jpg']]\n\n[[gallery: filename == 'b.jpg']]";
        const string withPhoto = "[[photo: fence.jpg]]\n\n" + withoutPhoto + "\n\n[[photo: lake.jpg | gallery]]";

        // Act
        var before = service.PrepareGalleryBlocks(withoutPhoto, SourcePath, [], _ => [], _ => null);
        var after = service.PrepareGalleryBlocks(withPhoto, SourcePath, [], _ => [], _ => null);

        // Assert
        CollectionAssert.AreEqual(
            before.Blocks.Values.Select(block => (block.FilterExpression, block.GridNumber)).ToList(),
            after.Blocks.Values.Select(block => (block.FilterExpression, block.GridNumber)).ToList());
        Assert.AreEqual(2, after.Count, "Photo blocks are not inline galleries.");
        var photos = after.Photos.Values.OrderBy(photo => photo.PhotoNumber).ToList();
        Assert.HasCount(2, photos);
        Assert.AreEqual(1, photos[0].PhotoNumber);
        Assert.IsTrue(photos[0].UsesPageContext);
        Assert.AreEqual(2, photos[1].PhotoNumber);
        Assert.IsFalse(photos[1].UsesPageContext);
    }

    [TestMethod]
    public void PrepareGalleryBlocks_PhotoToken_ResolvesPathOnce()
    {
        // Arrange
        var service = new MarkdownService();
        var fence = CreateImage("fence");
        var requestedPaths = new List<string>();

        // Act
        var prepared = service.PrepareGalleryBlocks(
            "[[photo: Years 2018/fence.jpg]]",
            SourcePath,
            [],
            _ => [],
            path =>
            {
                requestedPaths.Add(path);
                return fence;
            });

        // Assert
        Assert.HasCount(1, requestedPaths);
        Assert.AreEqual("Years 2018/fence.jpg", requestedPaths[0]);
        Assert.AreSame(fence, prepared.Photos.Values.Single().Image);
        Assert.AreEqual(0, prepared.Count);
    }

    [TestMethod]
    public void ToHtml_PhotoToken_RendersPreparedPhotoAtTokenPosition()
    {
        // Arrange
        var service = new MarkdownService();
        var fence = CreateImage("fence");
        const string markdown = "Before\n\n[[photo: fence.jpg]]\n\nAfter";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [], _ => [], _ => fence);
        var rendered = new List<(PreparedPhotoBlock Block, int Line)>();
        var context = CreateContext(prepared, (block, line) =>
        {
            rendered.Add((block, line));
            return "<figure class=\"photo-figure\"></figure>";
        });

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.HasCount(1, rendered);
        Assert.AreSame(fence, rendered[0].Block.Image);
        Assert.AreEqual(3, rendered[0].Line);
        var beforePosition = html.IndexOf("Before", StringComparison.Ordinal);
        var figurePosition = html.IndexOf("<figure class=\"photo-figure\">", StringComparison.Ordinal);
        var afterPosition = html.IndexOf("After", StringComparison.Ordinal);
        Assert.IsTrue(beforePosition < figurePosition);
        Assert.IsTrue(figurePosition < afterPosition);
        Assert.DoesNotContain("[[photo", html);
    }

    [TestMethod]
    public void ToHtml_UnresolvedPhoto_ReportsWarningAndRendersNothing()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        const string markdown = "Before\n\n[[photo: missing.jpg]]\n\nAfter";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [], _ => [], _ => null);
        var context = CreateContext(
            prepared,
            (_, _) => throw new AssertFailedException("Unresolved photos must not be rendered."),
            warnings.Add);

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.DoesNotContain("missing.jpg", html);
        Assert.Contains("After", html);
        Assert.HasCount(1, warnings);
        Assert.Contains($"{SourcePath}:3:", warnings[0]);
        Assert.Contains("'missing.jpg'", warnings[0]);
    }

    [TestMethod]
    public void ToHtml_NestedPhotoToken_RemainsLiteralAndReportsWarning()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        const string markdown = "- [[photo: fence.jpg]]";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [], _ => [], _ => CreateImage("fence"));
        var context = CreateContext(
            prepared,
            (_, _) => throw new AssertFailedException("Nested tokens must not render a photo."),
            warnings.Add);

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.Contains("[[photo: fence.jpg]]", html);
        Assert.HasCount(1, warnings);
        Assert.Contains("photo token", warnings[0]);
        Assert.Contains("only top-level blocks are recognized", warnings[0]);
    }

    private static ContentImageContext CreateContext(
        PreparedGalleryBlocks preparedBlocks,
        Func<PreparedPhotoBlock, int, string> renderPhoto,
        Action<string>? reportWarning = null)
    {
        var galleryContext = new GalleryBlockContext(
            SourcePath,
            preparedBlocks,
            _ => { },
            (_, _) => "<section class=\"gallery\"></section>",
            reportWarning ?? (_ => { }),
            renderPhoto);

        return new ContentImageContext(
            new Dictionary<string, Image>(),
            "story",
            "../images/",
            ["avif", "webp", "jpg"],
            (_, _, _) => string.Empty,
            galleryContext);
    }

    private static Image CreateImage(string name) => new()
    {
        SourcePath = $"years/{name}.jpg",
        FileName = name,
        Slug = $"years/{name}",
        Width = 1920,
        Height = 1080,
        Sizes = [320, 640]
    };
}

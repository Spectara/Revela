using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Tests for inline-gallery Markdown rendering.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class InlineGalleryTests
{
    private const string SourcePath = "source/gallery/_index.revela";

    [TestMethod]
    public void ToHtml_BareGallery_RendersPageImagesAtTokenPosition()
    {
        // Arrange
        var service = new MarkdownService();
        var pageImages = new[] { CreateImage("first"), CreateImage("second") };
        var markdown = "Before\n\n[[gallery]]\n\nAfter";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, pageImages, _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => "<section class=\"gallery\">first,second</section>");

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.AreEqual(1, prepared.Count);
        var beforePosition = html.IndexOf("Before", StringComparison.Ordinal);
        var galleryPosition = html.IndexOf("<section class=\"gallery\">", StringComparison.Ordinal);
        var afterPosition = html.IndexOf("After", StringComparison.Ordinal);
        Assert.IsTrue(beforePosition < galleryPosition);
        Assert.IsTrue(galleryPosition < afterPosition);
    }

    [TestMethod]
    public void PrepareGalleryBlocks_FilteredGallery_ResolvesOnceAndStableIdentityRendersFrozenResult()
    {
        // Arrange
        var service = new MarkdownService();
        var resolverCount = 0;
        var globalImage = CreateImage("global");
        var renderedImages = new List<Image>();
        var markdown = "Before\n\n[[gallery: filename == 'global.jpg']]\n\nAfter";
        var prepared = service.PrepareGalleryBlocks(
            markdown,
            SourcePath,
            [CreateImage("local")],
            filterExpression =>
            {
                resolverCount++;
                Assert.AreEqual("filename == 'global.jpg'", filterExpression);
                return [globalImage];
            });
        var context = CreateContext(
            prepared,
            (block, _) =>
            {
                renderedImages.AddRange(block.Images);
                Assert.AreEqual(1, block.GridNumber);
                return "<section class=\"gallery\"></section>";
            });

        // Act
        var countAfterPreparation = resolverCount;
        service.ToHtml(markdown, context);

        // Assert
        Assert.AreEqual(1, countAfterPreparation);
        Assert.AreEqual(countAfterPreparation, resolverCount);
        Assert.HasCount(1, renderedImages);
        Assert.AreSame(globalImage, renderedImages[0]);
    }

    [TestMethod]
    public void ToHtml_EmptyMatch_ReportsWarningAndEmitsNoGrid()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        var markdown = "Before\n\n[[gallery]]\n\nAfter";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [], _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => throw new AssertFailedException("Empty galleries must not be rendered."),
            warnings.Add);

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.DoesNotContain("class=\"gallery\"", html);
        Assert.HasCount(1, warnings);
        Assert.Contains($"{SourcePath}:3:", warnings[0]);
        Assert.Contains("matched 0 photos", warnings[0]);
    }

    [TestMethod]
    public void ToHtml_MultipleBareGalleries_ReportsDuplicateSetWarning()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        var markdown = "[[gallery]]\n\nText\n\n[[gallery]]";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [CreateImage("photo")], _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => "<section class=\"gallery\"></section>",
            reportWarning: warnings.Add);

        // Act
        service.ToHtml(markdown, context);

        // Assert
        Assert.HasCount(1, warnings);
        Assert.Contains("multiple bare", warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{SourcePath}:5:", warnings[0]);
    }

    [TestMethod]
    public void PrepareGalleryBlocks_MultipleBareGalleries_AssignsDistinctRenderOrdinals()
    {
        var service = new MarkdownService();
        var prepared = service.PrepareGalleryBlocks(
            "[[gallery]]\n\n[[gallery]]",
            SourcePath,
            [CreateImage("photo")],
            _ => []);

        var blocks = prepared.Blocks.Values.ToList();

        Assert.HasCount(2, blocks);
        Assert.AreEqual(1, blocks[0].BareRenderOrdinal);
        Assert.AreEqual(2, blocks[1].BareRenderOrdinal);
        Assert.IsNull(blocks[0].GridNumber);
        Assert.IsNull(blocks[1].GridNumber);
    }

    [TestMethod]
    public void ToHtml_MissingGridPartial_ReportsSourceAndLine()
    {
        // Arrange
        var service = new MarkdownService();
        var markdown = "Before\n\n[[gallery]]";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [CreateImage("photo")], _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => "<section class=\"gallery\"></section>",
            ensureGalleryGrid: line => throw new InvalidOperationException(
                $"{SourcePath}:{line}: theme is missing required template 'Partials/GalleryGrid.revela'."));

        // Act
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            service.ToHtml(markdown, context));

        // Assert
        Assert.Contains($"{SourcePath}:3:", exception.Message);
        Assert.Contains("Partials/GalleryGrid.revela", exception.Message);
    }

    [TestMethod]
    public void ToHtml_NoGalleryToken_MatchesExistingMarkdownOutput()
    {
        // Arrange
        var service = new MarkdownService();
        var markdown = "# Heading\n\nBody with **formatting**.";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [CreateImage("photo")], _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => throw new AssertFailedException("No grid should be rendered."));

        // Act
        var existingHtml = service.ToHtml(markdown);
        var inlineEnabledHtml = service.ToHtml(markdown, context);

        // Assert
        Assert.AreEqual(existingHtml, inlineEnabledHtml);
    }

    [TestMethod]
    public void ToHtml_NestedToken_RemainsLiteralAndReportsWarning()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        var markdown = "- [[gallery]]";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [CreateImage("photo")], _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => throw new AssertFailedException("Nested tokens must not render a grid."),
            reportWarning: warnings.Add);

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.Contains("[[gallery]]", html);
        Assert.HasCount(1, warnings);
        Assert.Contains("only top-level blocks are recognized", warnings[0]);
        Assert.Contains($"{SourcePath}:1:", warnings[0]);
    }

    [TestMethod]
    public void ToHtml_EscapedNestedToken_RemainsLiteralWithoutWarning()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        var markdown = "- \\[[gallery]]";
        var prepared = service.PrepareGalleryBlocks(markdown, SourcePath, [CreateImage("photo")], _ => []);
        var context = CreateContext(
            prepared,
            (_, _) => throw new AssertFailedException("Escaped tokens must not render a grid."),
            reportWarning: warnings.Add);

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.Contains("[[gallery]]", html);
        Assert.IsEmpty(warnings);
    }

    [TestMethod]
    [DataRow("unknownProperty == 'value'")]
    [DataRow("unknownFunction(filename)")]
    public void PrepareGalleryBlocks_SemanticFilterFailure_ThrowsSourceLocatedError(string expression)
    {
        // Arrange
        var service = new MarkdownService();
        var markdown = $"Intro\n\n[[gallery: {expression}]]";
        var imageContents = new Dictionary<string, ImageContent>
        {
            ["gallery/photo.jpg"] = new ImageContent
            {
                Filename = "photo.jpg",
                Width = 1920,
                Height = 1080,
                Sizes = [320, 640]
            }
        };

        // Act
        var exception = Assert.ThrowsExactly<GalleryBlockParseException>(() =>
            service.PrepareGalleryBlocks(
                markdown,
                SourcePath,
                [],
                filterExpression => GalleryImageResolver.Resolve(imageContents, filterExpression)));

        // Assert
        Assert.AreEqual(SourcePath, exception.SourcePath);
        Assert.AreEqual(3, exception.Line);
        Assert.AreEqual(expression, exception.FilterExpression);
        Assert.IsTrue(exception.FilterPosition >= 0);
        Assert.Contains(expression, exception.Message);
        Assert.Contains("position", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ContentImageContext CreateContext(
        PreparedGalleryBlocks preparedBlocks,
        Func<PreparedGalleryBlock, int, string> renderGalleryGrid,
        Action<string>? reportWarning = null,
        Action<int>? ensureGalleryGrid = null)
    {
        var galleryContext = new GalleryBlockContext(
            SourcePath,
            preparedBlocks,
            ensureGalleryGrid ?? (_ => { }),
            renderGalleryGrid,
            reportWarning ?? (_ => { }));

        return new ContentImageContext(
            new Dictionary<string, Image>(),
            "gallery",
            (_, _, _) => string.Empty,
            galleryContext);
    }

    private static Image CreateImage(string name) => new()
    {
        SourcePath = $"gallery/{name}.jpg",
        FileName = name,
        Slug = $"gallery/{name}",
        Width = 1920,
        Height = 1080,
        Sizes = [320, 640]
    };
}

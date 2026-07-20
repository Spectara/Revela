using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;

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
    public void ToHtml_BareGallery_RendersPageImagesAtTokenPositionAndMarksPage()
    {
        // Arrange
        var service = new MarkdownService();
        var wasMarked = false;
        var pageImages = new[] { CreateImage("first"), CreateImage("second") };
        var context = CreateContext(
            pageImages,
            (_, _) => "<section class=\"gallery\">first,second</section>",
            () => wasMarked = true);

        // Act
        var html = service.ToHtml("Before\n\n[[gallery]]\n\nAfter", context);

        // Assert
        Assert.IsTrue(wasMarked);
        var beforePosition = html.IndexOf("Before", StringComparison.Ordinal);
        var galleryPosition = html.IndexOf("<section class=\"gallery\">", StringComparison.Ordinal);
        var afterPosition = html.IndexOf("After", StringComparison.Ordinal);
        Assert.IsTrue(beforePosition < galleryPosition);
        Assert.IsTrue(galleryPosition < afterPosition);
    }

    [TestMethod]
    public void ToHtml_FilteredGallery_UsesGlobalResolver()
    {
        // Arrange
        var service = new MarkdownService();
        var resolvedFilter = string.Empty;
        var globalImage = CreateImage("global");
        var renderedImages = new List<Image>();
        var context = CreateContext(
            [CreateImage("local")],
            (images, _) =>
            {
                renderedImages.AddRange(images);
                return "<section class=\"gallery\"></section>";
            },
            resolveImages: filterExpression =>
            {
                resolvedFilter = filterExpression;
                return [globalImage];
            });

        // Act
        service.ToHtml("[[gallery: filename == 'global.jpg']]", context);

        // Assert
        Assert.AreEqual("filename == 'global.jpg'", resolvedFilter);
        Assert.HasCount(1, renderedImages);
        Assert.AreSame(globalImage, renderedImages[0]);
    }

    [TestMethod]
    public void ToHtml_EmptyMatch_ReportsWarningAndEmitsNoGrid()
    {
        // Arrange
        var service = new MarkdownService();
        var warnings = new List<string>();
        var wasMarked = false;
        var context = CreateContext(
            [],
            (_, _) => throw new AssertFailedException("Empty galleries must not be rendered."),
            () => wasMarked = true,
            warnings.Add);

        // Act
        var html = service.ToHtml("Before\n\n[[gallery]]\n\nAfter", context);

        // Assert
        Assert.IsTrue(wasMarked);
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
        var context = CreateContext(
            [CreateImage("photo")],
            (_, _) => "<section class=\"gallery\"></section>",
            reportWarning: warnings.Add);

        // Act
        service.ToHtml("[[gallery]]\n\nText\n\n[[gallery]]", context);

        // Assert
        Assert.HasCount(1, warnings);
        Assert.Contains("multiple bare", warnings[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{SourcePath}:5:", warnings[0]);
    }

    [TestMethod]
    public void ToHtml_MissingGridPartial_ReportsSourceAndLine()
    {
        // Arrange
        var service = new MarkdownService();
        var context = CreateContext(
            [CreateImage("photo")],
            (_, _) => "<section class=\"gallery\"></section>",
            ensureGalleryGrid: line => throw new InvalidOperationException(
                $"{SourcePath}:{line}: theme is missing required template 'Partials/GalleryGrid.revela'."));

        // Act
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            service.ToHtml("Before\n\n[[gallery]]", context));

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
        var context = CreateContext(
            [CreateImage("photo")],
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
        var context = CreateContext(
            [CreateImage("photo")],
            (_, _) => throw new AssertFailedException("Nested tokens must not render a grid."),
            reportWarning: warnings.Add);

        // Act
        var html = service.ToHtml("- [[gallery]]", context);

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
        var context = CreateContext(
            [CreateImage("photo")],
            (_, _) => throw new AssertFailedException("Escaped tokens must not render a grid."),
            reportWarning: warnings.Add);

        // Act
        var html = service.ToHtml("- \\[[gallery]]", context);

        // Assert
        Assert.Contains("[[gallery]]", html);
        Assert.IsEmpty(warnings);
    }

    private static ContentImageContext CreateContext(
        IReadOnlyList<Image> pageImages,
        Func<IReadOnlyList<Image>, int, string> renderGalleryGrid,
        Action? markInlineGallery = null,
        Action<string>? reportWarning = null,
        Func<string, IReadOnlyList<Image>>? resolveImages = null,
        Action<int>? ensureGalleryGrid = null)
    {
        var galleryContext = new GalleryBlockContext(
            SourcePath,
            pageImages,
            ensureGalleryGrid ?? (_ => { }),
            renderGalleryGrid,
            markInlineGallery ?? (() => { }),
            reportWarning ?? (_ => { }));

        return new ContentImageContext(
            new Dictionary<string, Image>(),
            "gallery",
            "../images/",
            ["avif", "webp", "jpg"],
            (_, _, _) => string.Empty,
            resolveImages ?? (_ => []),
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

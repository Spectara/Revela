using Markdig;
using Markdig.Syntax;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Tests for standalone inline-gallery Markdown blocks.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class GalleryBlockTests
{
    private const string SourcePath = "source/landscapes/_index.revela";

    [TestMethod]
    public void Parse_BareStandaloneToken_CreatesGalleryBlock()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse("Before\n\n[[gallery]]\n\nAfter", pipeline);
        var blocks = document.Descendants<GalleryBlock>().ToList();

        // Assert
        Assert.HasCount(1, blocks);
        Assert.IsNull(blocks[0].FilterExpression);
        Assert.AreEqual(2, blocks[0].Line);
        Assert.AreEqual(0, blocks[0].Column);
    }

    [TestMethod]
    public void Parse_FilteredStandaloneToken_PreservesSharedFilterGrammar()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var filter = "contains(filename, 'sun') and width > height | sort dateTaken desc | limit 6";

        // Act
        var document = Markdown.Parse($"[[gallery: {filter}]]", pipeline);
        var block = document.Descendants<GalleryBlock>().Single();

        // Assert
        Assert.AreEqual(filter, block.FilterExpression);
    }

    [TestMethod]
    public void Parse_TokenWithOuterWhitespace_CreatesGalleryBlock()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse("   [[gallery]]   ", pipeline);
        var block = document.Descendants<GalleryBlock>().Single();

        // Assert
        Assert.AreEqual(3, block.Column);
    }

    [TestMethod]
    [DataRow("```text\n[[gallery]]\n```")]
    [DataRow("    [[gallery]]")]
    [DataRow("`[[gallery]]`")]
    [DataRow("Paragraph with [[gallery]] inside.")]
    [DataRow("- [[gallery]]")]
    [DataRow("> [[gallery]]")]
    [DataRow("Paragraph\n[[gallery]]")]
    public void Parse_NestedOrNonStandaloneToken_DoesNotCreateGalleryBlock(string markdown)
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse(markdown, pipeline);

        // Assert
        Assert.IsEmpty(document.Descendants<GalleryBlock>());
    }

    [TestMethod]
    public void ToHtml_EscapedToken_RendersLiteralText()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var html = Markdown.ToHtml("\\[[gallery]]", pipeline);

        // Assert
        Assert.Contains("[[gallery]]", html);
        Assert.DoesNotContain("\\[[gallery]]", html);
    }

    [TestMethod]
    public void Parse_MalformedToken_RemainsParagraphText()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse("[[gallery filename == 'photo.jpg']]", pipeline);

        // Assert
        Assert.IsEmpty(document.Descendants<GalleryBlock>());
        Assert.HasCount(1, document.Descendants<ParagraphBlock>());
    }

    [TestMethod]
    public void Parse_InvalidFilter_ThrowsSourceLocatedError()
    {
        // Arrange
        var pipeline = CreatePipeline();
        var markdown = "Intro\n\n[[gallery: filename == ]]";

        // Act
        var exception = Assert.ThrowsExactly<GalleryBlockParseException>(() => Markdown.Parse(markdown, pipeline));

        // Assert
        Assert.AreEqual(SourcePath, exception.SourcePath);
        Assert.AreEqual(3, exception.Line);
        Assert.AreEqual("filename ==", exception.FilterExpression);
        Assert.IsTrue(exception.FilterPosition >= 0);
        Assert.Contains($"{SourcePath}:3:", exception.Message);
        Assert.Contains("position", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Parse_EmptyFilter_ThrowsSourceLocatedError()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var exception = Assert.ThrowsExactly<GalleryBlockParseException>(() =>
            Markdown.Parse("[[gallery: ]]", pipeline));

        // Assert
        Assert.AreEqual(1, exception.Line);
        Assert.AreEqual(0, exception.FilterPosition);
    }

    private static MarkdownPipeline CreatePipeline() => new MarkdownPipelineBuilder()
        .Use(new GalleryBlockExtension(SourcePath))
        .Build();
}

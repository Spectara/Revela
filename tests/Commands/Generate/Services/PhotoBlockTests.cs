using Markdig;
using Markdig.Syntax;
using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Tests for standalone <c>[[photo: …]]</c> Markdown blocks.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class PhotoBlockTests
{
    private const string SourcePath = "source/story/_index.revela";

    [TestMethod]
    public void Parse_PhotoToken_CreatesPageContextPhotoBlock()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse("Before\n\n[[photo: Years/2018/002190.jpg]]\n\nAfter", pipeline);
        var blocks = document.Descendants<PhotoBlock>().ToList();

        // Assert
        Assert.HasCount(1, blocks);
        Assert.AreEqual("Years/2018/002190.jpg", blocks[0].ImagePath);
        Assert.IsTrue(blocks[0].UsesPageContext);
        Assert.AreEqual(2, blocks[0].Line);
    }

    [TestMethod]
    public void Parse_GalleryOption_UsesPrimaryContext()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse("[[photo: Years/2018/002190.jpg | gallery]]", pipeline);
        var block = document.Descendants<PhotoBlock>().Single();

        // Assert
        Assert.AreEqual("Years/2018/002190.jpg", block.ImagePath);
        Assert.IsFalse(block.UsesPageContext);
    }

    [TestMethod]
    public void Parse_PathWithSpaces_PreservesPathWithoutAngleBrackets()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse("  [[photo:   Summer 2024/at the lake.jpg   |  gallery ]]  ", pipeline);
        var block = document.Descendants<PhotoBlock>().Single();

        // Assert
        Assert.AreEqual("Summer 2024/at the lake.jpg", block.ImagePath);
        Assert.IsFalse(block.UsesPageContext);
    }

    [TestMethod]
    [DataRow("```text\n[[photo: a.jpg]]\n```")]
    [DataRow("    [[photo: a.jpg]]")]
    [DataRow("`[[photo: a.jpg]]`")]
    [DataRow("Paragraph with [[photo: a.jpg]] inside.")]
    [DataRow("- [[photo: a.jpg]]")]
    [DataRow("> [[photo: a.jpg]]")]
    [DataRow("Paragraph\n[[photo: a.jpg]]")]
    [DataRow("[[photography]]")]
    [DataRow("\\[[photo: a.jpg]]")]
    public void Parse_NestedOrNonTokenText_DoesNotCreatePhotoBlock(string markdown)
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse(markdown, pipeline);

        // Assert
        Assert.IsEmpty(document.Descendants<PhotoBlock>());
    }

    [TestMethod]
    [DataRow("[[photo]]")]
    [DataRow("[[photo: ]]")]
    [DataRow("[[photo:  | gallery]]")]
    [DataRow("[[photo a.jpg]]")]
    [DataRow("[[photo: a.jpg")]
    [DataRow("[[photo: a.jpg | page]]")]
    [DataRow("[[photo: a.jpg | Gallery]]")]
    [DataRow("[[photo: a.jpg | gallery | gallery]]")]
    public void Parse_MalformedToken_ThrowsSourceLocatedError(string token)
    {
        // Arrange
        var pipeline = CreatePipeline();
        var markdown = $"Intro\n\n{token}";

        // Act
        var exception = Assert.ThrowsExactly<PhotoBlockParseException>(() => Markdown.Parse(markdown, pipeline));

        // Assert
        Assert.AreEqual(SourcePath, exception.SourcePath);
        Assert.AreEqual(3, exception.Line);
        Assert.AreEqual(token, exception.Token);
        Assert.Contains($"{SourcePath}:3:", exception.Message);
        Assert.Contains(token, exception.Message);
        Assert.Contains("[[photo: <path>]]", exception.Message);
    }

    [TestMethod]
    public void Parse_UnknownOption_NamesTheOption()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var exception = Assert.ThrowsExactly<PhotoBlockParseException>(() =>
            Markdown.Parse("[[photo: a.jpg | lightbox]]", pipeline));

        // Assert
        Assert.Contains("unknown option 'lightbox'", exception.Message);
    }

    [TestMethod]
    public void Parse_PhotoAndGalleryTokens_ProduceIndependentBlocks()
    {
        // Arrange
        var pipeline = CreatePipeline();

        // Act
        var document = Markdown.Parse(
            "[[photo: a.jpg]]\n\n[[gallery: filename == 'b.jpg']]\n\n[[photo: c.jpg | gallery]]",
            pipeline);

        // Assert
        Assert.HasCount(2, document.Descendants<PhotoBlock>());
        Assert.HasCount(1, document.Descendants<GalleryBlock>());
    }

    private static MarkdownPipeline CreatePipeline() => new MarkdownPipelineBuilder()
        .Use(new GalleryBlockExtension(SourcePath))
        .Build();
}

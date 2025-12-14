using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Parsing;

namespace Spectara.Revela.Commands.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class RevelaParserTests
{
    [TestMethod]
    public void Parse_EmptyContent_ReturnsEmpty()
    {
        // Act
        var result = RevelaParser.Parse(string.Empty);

        // Assert
        Assert.IsFalse(result.HasMetadata);
        Assert.AreSame(result, DirectoryMetadata.Empty);
    }

    [TestMethod]
    public void Parse_WhitespaceContent_ReturnsEmpty()
    {
        // Act
        var result = RevelaParser.Parse("   \n\t  ");

        // Assert
        Assert.IsFalse(result.HasMetadata);
    }

    [TestMethod]
    public void Parse_NoFrontMatter_ReturnsBodyOnly()
    {
        // Arrange
        var content = "# Hello World\n\nThis is some content.";

        // Act
        var result = RevelaParser.Parse(content);

        // Assert - No frontmatter means no metadata fields, only body
        // HasMetadata is true because RawBody is not null
        Assert.IsNotNull(result.RawBody);
        Assert.IsNull(result.Title);
        Assert.IsNull(result.Slug);
        Assert.IsNull(result.Description);
        Assert.IsFalse(result.Hidden);
        // Body is stored raw, not rendered (rendering happens at render time)
        Assert.IsTrue(result.RawBody.Contains("# Hello World", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_TitleOnly_ExtractsTitle()
    {
        // Arrange - Scriban frontmatter uses +++ and = for assignment
        var content = """
            +++
            title = "My Custom Title"
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.AreEqual("My Custom Title", result.Title);
        Assert.IsNull(result.Slug);
        Assert.IsNull(result.Description);
        Assert.IsFalse(result.Hidden);
    }

    [TestMethod]
    public void Parse_AllFields_ExtractsAll()
    {
        // Arrange - Scriban format with all fields
        var content = """
            +++
            title = "Gallery Title"
            slug = "custom-slug"
            description = "This is the description"
            hidden = true
            +++
            # Body Content

            Some paragraph text.
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.AreEqual("Gallery Title", result.Title);
        Assert.AreEqual("custom-slug", result.Slug);
        Assert.AreEqual("This is the description", result.Description);
        Assert.IsTrue(result.Hidden);
        Assert.IsNotNull(result.RawBody);
        Assert.IsTrue(result.RawBody.Contains("# Body Content", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_TemplateField_ExtractsTemplate()
    {
        // Arrange
        var content = """
            +++
            title = "Statistics Page"
            template = "statistics/overview"
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.AreEqual("Statistics Page", result.Title);
        Assert.AreEqual("statistics/overview", result.Template);
    }

    [TestMethod]
    public void Parse_DataSources_ExtractsDataObject()
    {
        // Arrange - Scriban object syntax
        var content = """
            +++
            title = "Data Page"
            data = { statistics: "stats.json", galleries: "$galleries" }
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.AreEqual("Data Page", result.Title);
        Assert.HasCount(2, result.DataSources);
        Assert.AreEqual("stats.json", result.DataSources["statistics"]);
        Assert.AreEqual("$galleries", result.DataSources["galleries"]);
    }

    [TestMethod]
    public void Parse_HiddenFalse_ReturnsFalse()
    {
        // Arrange
        var content = """
            +++
            hidden = false
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsFalse(result.Hidden);
    }

    [TestMethod]
    public void Parse_HiddenTrue_ReturnsTrue()
    {
        // Arrange
        var content = """
            +++
            hidden = true
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsTrue(result.Hidden);
    }

    [TestMethod]
    public void Parse_SingleQuotedStrings_Works()
    {
        // Arrange - Scriban supports single quotes
        var content = """
            +++
            title = 'Single Quoted Title'
            description = 'Single quoted description'
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.AreEqual("Single Quoted Title", result.Title);
        Assert.AreEqual("Single quoted description", result.Description);
    }

    [TestMethod]
    public void Parse_EmptyFrontMatter_ReturnsEmpty()
    {
        // Arrange - Empty frontmatter block
        var content = """
            +++
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsFalse(result.HasMetadata);
    }

    [TestMethod]
    public void Parse_BodyWithScribanExpressions_PreservesExpressions()
    {
        // Arrange - Body contains Scriban expressions that should be preserved
        var content = """
            +++
            title = "Dynamic Page"
            +++
            # Welcome

            Total images: {{ images.size }}
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsNotNull(result.RawBody);
        Assert.IsTrue(result.RawBody.Contains("{{ images.size }}", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_MultilineBody_PreservesFormatting()
    {
        // Arrange
        var content = """
            +++
            title = "Page"
            +++
            Line 1

            Line 2

            Line 3
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.IsNotNull(result.RawBody);
        Assert.IsTrue(result.RawBody.Contains("Line 1", StringComparison.Ordinal));
        Assert.IsTrue(result.RawBody.Contains("Line 2", StringComparison.Ordinal));
        Assert.IsTrue(result.RawBody.Contains("Line 3", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_CommentInFrontMatter_IgnoresComment()
    {
        // Arrange - Scriban comments with #
        var content = """
            +++
            # This is a comment
            title = "Title"
            # Another comment
            +++
            """;

        // Act
        var result = RevelaParser.Parse(content);

        // Assert
        Assert.AreEqual("Title", result.Title);
    }

    [TestMethod]
    public void IndexFileName_IsRevela()
    {
        // Arrange
        const string expected = "_index.revela";

        // Act
        var actual = RevelaParser.IndexFileName;

        // Assert
        Assert.AreEqual(expected, actual);
    }
}

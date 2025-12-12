using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Parsing;

namespace Spectara.Revela.Commands.Tests;

[TestClass]
public sealed class FrontMatterParserTests
{
    [TestMethod]
    public void Parse_EmptyContent_ReturnsEmpty()
    {
        // Act
        var result = FrontMatterParser.Parse(string.Empty);

        // Assert
        Assert.IsFalse(result.HasMetadata);
        Assert.AreSame(result, DirectoryMetadata.Empty);
    }

    [TestMethod]
    public void Parse_WhitespaceContent_ReturnsEmpty()
    {
        // Act
        var result = FrontMatterParser.Parse("   \n\t  ");

        // Assert
        Assert.IsFalse(result.HasMetadata);
    }

    [TestMethod]
    public void Parse_NoFrontMatter_ReturnsBodyOnly()
    {
        // Arrange
        var content = "# Hello World\n\nThis is some content.";

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.IsNull(result.Title);
        Assert.IsNull(result.Slug);
        Assert.IsNull(result.Description);
        Assert.IsFalse(result.Hidden);
        Assert.IsNotNull(result.Body);
        Assert.IsTrue(result.Body.Contains("<h1>Hello World</h1>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_TitleOnly_ExtractsTitle()
    {
        // Arrange
        var content = """
            ---
            title: My Custom Title
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

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
        // Arrange
        var content = """
            ---
            title: Gallery Title
            slug: custom-slug
            description: This is the description
            hidden: true
            ---
            # Body Content

            Some paragraph text.
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.AreEqual("Gallery Title", result.Title);
        Assert.AreEqual("custom-slug", result.Slug);
        Assert.AreEqual("This is the description", result.Description);
        Assert.IsTrue(result.Hidden);
        Assert.IsNotNull(result.Body);
        Assert.IsTrue(result.Body.Contains("<h1>Body Content</h1>", StringComparison.Ordinal));
        Assert.IsTrue(result.Body.Contains("<p>Some paragraph text.</p>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_QuotedValues_RemovesQuotes()
    {
        // Arrange
        var content = """
            ---
            title: "Quoted Title"
            description: 'Single Quoted'
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.AreEqual("Quoted Title", result.Title);
        Assert.AreEqual("Single Quoted", result.Description);
    }

    [TestMethod]
    public void Parse_HiddenVariants_ParsesCorrectly()
    {
        // Test "true"
        var result1 = FrontMatterParser.Parse("""
            ---
            hidden: true
            ---
            """);
        Assert.IsTrue(result1.Hidden);

        // Test "yes"
        var result2 = FrontMatterParser.Parse("""
            ---
            hidden: yes
            ---
            """);
        Assert.IsTrue(result2.Hidden);

        // Test "1"
        var result3 = FrontMatterParser.Parse("""
            ---
            hidden: 1
            ---
            """);
        Assert.IsTrue(result3.Hidden);

        // Test "false"
        var result4 = FrontMatterParser.Parse("""
            ---
            hidden: false
            ---
            """);
        Assert.IsFalse(result4.Hidden);

        // Test "no"
        var result5 = FrontMatterParser.Parse("""
            ---
            hidden: no
            ---
            """);
        Assert.IsFalse(result5.Hidden);
    }

    [TestMethod]
    public void Parse_CaseInsensitiveKeys_Works()
    {
        // Arrange
        var content = """
            ---
            Title: Mixed Case Title
            SLUG: uppercase-slug
            Description: Some description
            Hidden: TRUE
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.AreEqual("Mixed Case Title", result.Title);
        Assert.AreEqual("uppercase-slug", result.Slug);
        Assert.AreEqual("Some description", result.Description);
        Assert.IsTrue(result.Hidden);
    }

    [TestMethod]
    public void Parse_EmptyFrontMatter_ReturnsEmpty()
    {
        // Arrange
        var content = """
            ---
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsFalse(result.HasMetadata);
    }

    [TestMethod]
    public void Parse_UnknownKeys_Ignored()
    {
        // Arrange
        var content = """
            ---
            title: Valid Title
            unknown_key: some value
            another_key: another value
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.AreEqual("Valid Title", result.Title);
        Assert.IsNull(result.Slug);
    }

    [TestMethod]
    public void Parse_EmptyValues_TreatedAsNull()
    {
        // Arrange
        var content = """
            ---
            title:
            slug:
            description: ""
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsNull(result.Title);
        Assert.IsNull(result.Slug);
        Assert.IsNull(result.Description);
        Assert.IsFalse(result.HasMetadata);
    }

    [TestMethod]
    public void Parse_ColonInValue_PreservesValue()
    {
        // Arrange
        var content = """
            ---
            title: Title: With Colon
            description: URL: https://example.com
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.AreEqual("Title: With Colon", result.Title);
        Assert.AreEqual("URL: https://example.com", result.Description);
    }

    [TestMethod]
    public void Parse_WindowsLineEndings_Works()
    {
        // Arrange
        var content = "---\r\ntitle: Windows Title\r\nslug: win-slug\r\n---\r\n";

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.AreEqual("Windows Title", result.Title);
        Assert.AreEqual("win-slug", result.Slug);
    }

    [TestMethod]
    public void Parse_BodyWithCodeBlocks_RendersCorrectly()
    {
        // Arrange
        var content = """
            ---
            title: Code Example
            ---
            Here is some code:

            ```csharp
            Console.WriteLine("Hello");
            ```
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsNotNull(result.Body);
        // Markdig renders code blocks with <pre> tags
        Assert.IsTrue(result.Body.Contains("<pre>", StringComparison.Ordinal) ||
                     result.Body.Contains("<code", StringComparison.Ordinal),
                     $"Expected code block markup, but got: {result.Body}");
    }

    [TestMethod]
    public void Parse_OnlyBody_ReturnsBodyMetadata()
    {
        // Arrange - No frontmatter, just markdown
        var content = "Just some text without frontmatter.";

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.IsNull(result.Title);
        Assert.IsNotNull(result.Body);
        Assert.IsTrue(result.Body.Contains("<p>Just some text without frontmatter.</p>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Parse_DataSingleValue_ExtractsAsStatisticsKey()
    {
        // Arrange - Legacy single-value syntax defaults to "statistics" key
        var content = """
            ---
            title: Stats Page
            data: statistics.json
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.AreEqual("Stats Page", result.Title);
        Assert.IsNotNull(result.DataSources);
        Assert.HasCount(1, result.DataSources);
        Assert.IsTrue(result.DataSources.ContainsKey("statistics"));
        Assert.AreEqual("statistics.json", result.DataSources["statistics"]);
    }

    [TestMethod]
    public void Parse_DataObjectSyntax_ExtractsMultipleSources()
    {
        // Arrange - Object syntax with multiple named sources
        var content = """
            ---
            title: Overview Page
            data:
              stats: statistics.json
              galleries: $galleries
              images: $images
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.AreEqual("Overview Page", result.Title);
        Assert.IsNotNull(result.DataSources);
        Assert.HasCount(3, result.DataSources);
        Assert.AreEqual("statistics.json", result.DataSources["stats"]);
        Assert.AreEqual("$galleries", result.DataSources["galleries"]);
        Assert.AreEqual("$images", result.DataSources["images"]);
    }

    [TestMethod]
    public void Parse_DataObjectSyntax_SingleSource()
    {
        // Arrange - Object syntax with single named source
        var content = """
            ---
            title: Photo Statistics
            template: statistics/overview
            data:
              statistics: statistics.json
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsTrue(result.HasMetadata);
        Assert.AreEqual("Photo Statistics", result.Title);
        Assert.AreEqual("statistics/overview", result.Template);
        Assert.IsNotNull(result.DataSources);
        Assert.HasCount(1, result.DataSources);
        Assert.AreEqual("statistics.json", result.DataSources["statistics"]);
    }

    [TestMethod]
    public void Parse_DataWithBuiltinSources_ExtractsCorrectly()
    {
        // Arrange - Built-in sources with $ prefix
        var content = """
            ---
            title: Home
            data:
              allGalleries: $galleries
              allImages: $images
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsNotNull(result.DataSources);
        Assert.HasCount(2, result.DataSources);
        Assert.AreEqual("$galleries", result.DataSources["allGalleries"]);
        Assert.AreEqual("$images", result.DataSources["allImages"]);
    }

    [TestMethod]
    public void Parse_NoDataField_ReturnsEmptyDataSources()
    {
        // Arrange
        var content = """
            ---
            title: Simple Page
            ---
            """;

        // Act
        var result = FrontMatterParser.Parse(content);

        // Assert
        Assert.IsNotNull(result.DataSources);
        Assert.IsEmpty(result.DataSources);
    }
}

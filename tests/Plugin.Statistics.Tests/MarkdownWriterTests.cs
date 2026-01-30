using Spectara.Revela.Plugin.Statistics.Services;

namespace Spectara.Revela.Plugin.Statistics.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class MarkdownWriterTests
{
    [TestMethod]
    public void CreateNewFile_ReturnsDefaultFrontmatterWithContent()
    {
        // Arrange
        var statsContent = "<!-- STATS:BEGIN -->\n<p>Test</p>\n<!-- STATS:END -->";

        // Act
        var result = MarkdownWriter.CreateNewFile(statsContent);

        // Assert
        Assert.IsTrue(result.StartsWith("---", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("title: Site Statistics", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("<!-- STATS:BEGIN -->", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("<p>Test</p>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MergeContent_NoMarkers_AppendsContent()
    {
        // Arrange
        var existing = """
            ---
            title: My Custom Title
            ---

            Some user content here.
            """;
        var statsContent = "<!-- STATS:BEGIN -->\n<p>Stats</p>\n<!-- STATS:END -->";

        // Act
        var result = MarkdownWriter.MergeContent(existing, statsContent);

        // Assert
        Assert.IsTrue(result.Contains("title: My Custom Title", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("Some user content here.", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("<!-- STATS:BEGIN -->", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("<p>Stats</p>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MergeContent_WithMarkers_ReplacesContentBetweenMarkers()
    {
        // Arrange
        var existing = """
            ---
            title: My Custom Title
            ---

            User content before stats.

            <!-- STATS:BEGIN -->
            <p>Old stats content</p>
            <!-- STATS:END -->

            User content after stats.
            """;
        var statsContent = "<!-- STATS:BEGIN -->\n<p>New stats content</p>\n<!-- STATS:END -->";

        // Act
        var result = MarkdownWriter.MergeContent(existing, statsContent);

        // Assert
        Assert.IsTrue(result.Contains("title: My Custom Title", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("User content before stats.", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("User content after stats.", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("<p>New stats content</p>", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("<p>Old stats content</p>", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MergeContent_PreservesFrontmatter()
    {
        // Arrange
        var existing = """
            ---
            title: Custom Statistics
            description: My photo stats
            hidden: true
            ---

            <!-- STATS:BEGIN -->
            <p>Old</p>
            <!-- STATS:END -->
            """;
        var statsContent = "<!-- STATS:BEGIN -->\n<p>New</p>\n<!-- STATS:END -->";

        // Act
        var result = MarkdownWriter.MergeContent(existing, statsContent);

        // Assert
        Assert.IsTrue(result.Contains("title: Custom Statistics", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("description: My photo stats", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("hidden: true", StringComparison.Ordinal));
    }
}

namespace Spectara.Revela.Core.Tests;

/// <summary>
/// Example tests demonstrating how to use TestDataHelper
/// </summary>
[TestClass]
public sealed class TestDataHelperExampleTests
{
    [TestMethod]
    public void TestDataHelperShouldFindTestDataRoot()
    {
        // Arrange & Act
        var testDataRoot = TestDataHelper.TestDataRoot;

        // Assert - Skip if test-data not yet created (infrastructure test)
        Assert.IsFalse(string.IsNullOrEmpty(testDataRoot));
        if (!Directory.Exists(testDataRoot))
        {
            Assert.Inconclusive($"test-data directory not yet created at: {testDataRoot}");
        }
    }

    [TestMethod]
    public void TestDataHelperShouldFindSamplesRoot()
    {
        // Arrange & Act
        var samplesRoot = TestDataHelper.SamplesRoot;

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(samplesRoot));
        Assert.IsTrue(Directory.Exists(samplesRoot), $"samples directory should exist at: {samplesRoot}");
    }

    [TestMethod]
    public void TestDataHelperShouldFindSubdirectorySample()
    {
        // Arrange & Act
        var samplePath = TestDataHelper.GetSamplePath("subdirectory");

        // Assert
        Assert.IsFalse(string.IsNullOrEmpty(samplePath));
        Assert.IsTrue(Directory.Exists(samplePath), $"subdirectory sample should exist at: {samplePath}");

        // Check for config file
        var configPath = Path.Combine(samplePath, "project.json");
        Assert.IsTrue(File.Exists(configPath), "subdirectory sample should have project.json");
    }

    [TestMethod]
    public void TestDataHelperRequireTestImageShouldSkipIfNotFound()
    {
        // This test will be marked as Inconclusive if the image doesn't exist
        TestDataHelper.RequireTestImage("non-existent-image.jpg");

        // This line won't execute if image is missing
        Assert.Fail("This should not be reached if image is missing");
    }

    [TestMethod]
    public void TestDataHelperRequireSampleShouldSkipIfNotFound()
    {
        // This test will be marked as Inconclusive if sample doesn't exist
        TestDataHelper.RequireSample("non-existent-sample");

        // This line won't execute if sample is missing
        Assert.Fail("This should not be reached if sample is missing");
    }

    [TestMethod]
    public void TestDataHelperGetTestImagePathShouldReturnCorrectPath()
    {
        // Arrange
        var filename = "test-small.jpg";

        // Act
        var imagePath = TestDataHelper.GetTestImagePath(filename);

        // Assert
        Assert.IsTrue(imagePath.EndsWith(Path.Combine("test-data", "images", filename), StringComparison.Ordinal));
    }

    [TestMethod]
    public void TestDataHelperGetExpectedPathShouldReturnCorrectPath()
    {
        // Arrange
        var category = "thumbnails";
        var filename = "test-small-640.jpg";

        // Act
        var expectedPath = TestDataHelper.GetExpectedPath(category, filename);

        // Assert
        Assert.IsTrue(expectedPath.EndsWith(
            Path.Combine("test-data", "expected", category, filename), StringComparison.Ordinal));
    }
}




using FluentAssertions;

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

        // Assert
        _ = testDataRoot.Should().NotBeNullOrEmpty();
        _ = Directory.Exists(testDataRoot).Should().BeTrue(
            $"test-data directory should exist at: {testDataRoot}");
    }

    [TestMethod]
    public void TestDataHelperShouldFindSamplesRoot()
    {
        // Arrange & Act
        var samplesRoot = TestDataHelper.SamplesRoot;

        // Assert
        _ = samplesRoot.Should().NotBeNullOrEmpty();
        _ = Directory.Exists(samplesRoot).Should().BeTrue(
            $"samples directory should exist at: {samplesRoot}");
    }

    [TestMethod]
    public void TestDataHelperShouldFindMinimalSample()
    {
        // Arrange & Act
        var minimalPath = TestDataHelper.GetSamplePath("minimal");

        // Assert
        _ = minimalPath.Should().NotBeNullOrEmpty();
        _ = Directory.Exists(minimalPath).Should().BeTrue(
            $"minimal sample should exist at: {minimalPath}");

        // Check for config file
        var configPath = Path.Combine(minimalPath, "expose.json");
        _ = File.Exists(configPath).Should().BeTrue(
            "minimal sample should have expose.json");
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
        _ = imagePath.Should().EndWith(Path.Combine("test-data", "images", filename));
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
        _ = expectedPath.Should().EndWith(
            Path.Combine("test-data", "expected", category, filename));
    }
}



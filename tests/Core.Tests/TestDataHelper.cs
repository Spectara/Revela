namespace Spectara.Revela.Core.Tests;

/// <summary>
/// Helper class for accessing test data in unit tests
/// </summary>
public static class TestDataHelper
{
    /// <summary>
    /// Gets the root directory of the test-data folder
    /// </summary>
    public static string TestDataRoot
    {
        get
        {
            var assemblyLocation = Path.GetDirectoryName(
                typeof(TestDataHelper).Assembly.Location)!;

            // Navigate up from bin/Debug/net10.0 to solution root
            var solutionRoot = Path.GetFullPath(
                Path.Combine(assemblyLocation, "..", "..", "..", "..", ".."));

            return Path.Combine(solutionRoot, "test-data");
        }
    }

    /// <summary>
    /// Gets the path to the test images directory
    /// </summary>
    public static string TestImagesPath =>
        Path.Combine(TestDataRoot, "images");

    /// <summary>
    /// Gets the path to the expected output directory
    /// </summary>
    public static string ExpectedOutputPath =>
        Path.Combine(TestDataRoot, "expected");

    /// <summary>
    /// Gets the root directory of the samples folder
    /// </summary>
    public static string SamplesRoot
    {
        get
        {
            var assemblyLocation = Path.GetDirectoryName(
                typeof(TestDataHelper).Assembly.Location)!;

            var solutionRoot = Path.GetFullPath(
                Path.Combine(assemblyLocation, "..", "..", "..", "..", ".."));

            return Path.Combine(solutionRoot, "samples");
        }
    }

    /// <summary>
    /// Gets the path to a specific sample site
    /// </summary>
    /// <param name="sampleName">Name of the sample (e.g., "minimal")</param>
    public static string GetSamplePath(string sampleName) =>
        Path.Combine(SamplesRoot, sampleName);

    /// <summary>
    /// Gets the path to a test image by filename
    /// </summary>
    /// <param name="filename">Name of the test image file</param>
    public static string GetTestImagePath(string filename) =>
        Path.Combine(TestImagesPath, filename);

    /// <summary>
    /// Gets the path to an expected output file
    /// </summary>
    /// <param name="category">Category (e.g., "thumbnails", "exif")</param>
    /// <param name="filename">Name of the expected output file</param>
    public static string GetExpectedPath(string category, string filename) =>
        Path.Combine(ExpectedOutputPath, category, filename);

    /// <summary>
    /// Ensures a test image exists, skips test if not found
    /// </summary>
    /// <param name="filename">Name of the required test image</param>
    public static void RequireTestImage(string filename)
    {
        var path = GetTestImagePath(filename);
        if (!File.Exists(path))
        {
            Assert.Inconclusive(
                $"Test image not found: {filename}. " +
                $"Please add test image to test-data/images/");
        }
    }

    /// <summary>
    /// Ensures a sample site exists, skips test if not found
    /// </summary>
    /// <param name="sampleName">Name of the required sample</param>
    public static void RequireSample(string sampleName)
    {
        var path = GetSamplePath(sampleName);
        if (!Directory.Exists(path))
        {
            Assert.Inconclusive(
                $"Sample site not found: {sampleName}. " +
                $"Please add sample to samples/{sampleName}/");
        }
    }
}



using Spectara.Revela.Sdk.Configuration.Keys;

namespace Spectara.Revela.Tests.Core.Configuration;

/// <summary>
/// Regression guard for the <c>ConfigKeysGenerator</c> output. These constants are
/// what the config writers use for their raw-JSON keys, so the assertions here pin
/// the camelCase key values to the POCO property names. Renaming a POCO property
/// (e.g. <c>ProjectConfig.BaseUrl</c>) breaks both this test and the writers that
/// reference the generated constant — exactly the drift the generator prevents.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ConfigKeysGeneratorTests
{
    // Routes the generated constant through a runtime call so the assertion is not a
    // compile-time constant comparison (MSTEST0032). The rename guard is preserved:
    // the field must still exist (compile) and carry the expected value (assert).
    private static string Actual(string value) => value;

    [TestMethod]
    public void ProjectConfigKeys_MapPropertiesToCamelCaseKeys()
    {
        Assert.AreEqual("project", Actual(ProjectConfigKeys.Section));
        Assert.AreEqual("name", Actual(ProjectConfigKeys.Name));
        Assert.AreEqual("baseUrl", Actual(ProjectConfigKeys.BaseUrl));
        Assert.AreEqual("assetsBasePath", Actual(ProjectConfigKeys.AssetsBasePath));
        Assert.AreEqual("basePath", Actual(ProjectConfigKeys.BasePath));
    }

    [TestMethod]
    public void GenerateConfigKeys_MapNestedSectionsToCamelCaseKeys()
    {
        Assert.AreEqual("generate", Actual(GenerateConfigKeys.Section));
        Assert.AreEqual("images", Actual(GenerateConfigKeys.Images));
        Assert.AreEqual("sorting", Actual(GenerateConfigKeys.Sorting));
        Assert.AreEqual("render", Actual(GenerateConfigKeys.Render));
        Assert.AreEqual("cameras", Actual(GenerateConfigKeys.Cameras));
    }

    [TestMethod]
    public void ImageConfigKeys_MapFormatPropertiesToLowercaseKeys()
    {
        Assert.AreEqual("webp", Actual(ImageConfigKeys.Webp));
        Assert.AreEqual("jpg", Actual(ImageConfigKeys.Jpg));
        Assert.AreEqual("avif", Actual(ImageConfigKeys.Avif));
        Assert.AreEqual("maxDegreeOfParallelism", Actual(ImageConfigKeys.MaxDegreeOfParallelism));
        Assert.AreEqual("minWidth", Actual(ImageConfigKeys.MinWidth));
        Assert.AreEqual("minHeight", Actual(ImageConfigKeys.MinHeight));
        Assert.AreEqual("placeholder", Actual(ImageConfigKeys.Placeholder));
    }

    [TestMethod]
    public void SortingConfigKeys_MapImageSortPropertiesToCamelCaseKeys()
    {
        Assert.AreEqual("galleries", Actual(SortingConfigKeys.Galleries));
        Assert.AreEqual("images", Actual(SortingConfigKeys.Images));

        // Nested config POCO reached transitively from GenerateConfig.Sorting.Images.
        Assert.AreEqual("field", Actual(ImageSortConfigKeys.Field));
        Assert.AreEqual("direction", Actual(ImageSortConfigKeys.Direction));
        Assert.AreEqual("fallback", Actual(ImageSortConfigKeys.Fallback));
    }

    [TestMethod]
    public void PathsConfigKeys_MapPropertiesToCamelCaseKeys()
    {
        Assert.AreEqual("paths", Actual(PathsConfigKeys.Section));
        Assert.AreEqual("source", Actual(PathsConfigKeys.Source));
        Assert.AreEqual("output", Actual(PathsConfigKeys.Output));
    }

    [TestMethod]
    public void ThemeConfigKeys_MapPropertiesToCamelCaseKeys()
    {
        Assert.AreEqual("theme", Actual(ThemeConfigKeys.Section));
        Assert.AreEqual("name", Actual(ThemeConfigKeys.Name));
        Assert.AreEqual("images", Actual(ThemeConfigKeys.Images));
    }
}

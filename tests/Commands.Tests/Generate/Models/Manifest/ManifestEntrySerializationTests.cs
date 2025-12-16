using System.Text.Json;
using System.Text.Json.Serialization;
using Spectara.Revela.Commands.Generate.Models.Manifest;

namespace Spectara.Revela.Commands.Tests.Generate.Models.Manifest;

[TestClass]
public sealed class ManifestEntrySerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    [TestMethod]
    public void Serialize_WithTemplateAndDataSources_ShouldIncludeInJson()
    {
        // Arrange
        var entry = new ManifestEntry
        {
            Text = "Test Gallery",
            Path = "test/path",
            Template = "statistics/overview",
            DataSources = new Dictionary<string, string>
            {
                ["statistics"] = "statistics.json"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(entry, JsonOptions);

        // Assert
        Assert.Contains("\"template\"", json);
        Assert.Contains("\"statistics/overview\"", json);
        Assert.Contains("\"dataSources\"", json);
        Assert.Contains("\"statistics\"", json);
        Assert.Contains("\"statistics.json\"", json);
    }

    [TestMethod]
    public void Serialize_WithNullTemplate_ShouldIncludeTemplateAsNull()
    {
        // Arrange
        var entry = new ManifestEntry
        {
            Text = "Test Gallery",
            Path = "test/path",
            Template = null,
            DataSources = []
        };

        // Act
        var json = JsonSerializer.Serialize(entry, JsonOptions);

        // Assert - Should include null template because DefaultIgnoreCondition.Never
        Assert.Contains("\"template\": null", json);
    }

    [TestMethod]
    public void Serialize_WithEmptyDataSources_ShouldIncludeEmptyObject()
    {
        // Arrange
        var entry = new ManifestEntry
        {
            Text = "Test Gallery",
            Path = "test/path",
            Template = null,
            DataSources = []
        };

        // Act
        var json = JsonSerializer.Serialize(entry, JsonOptions);

        // Assert - Should include empty object because DefaultIgnoreCondition.Never
        Assert.Contains("\"dataSources\": {}", json);
    }
}

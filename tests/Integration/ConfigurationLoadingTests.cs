using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for configuration loading from project.json.
/// Verifies that JSON configuration values are correctly bound to
/// strongly-typed options via <see cref="IOptions{T}"/>.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ConfigurationLoadingTests
{
    [TestMethod]
    public void ProjectConfig_LoadsFromProjectJson()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new
                {
                    name = "My Portfolio",
                    baseUrl = "https://photos.example.com",
                    language = "de"
                }
            }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<ProjectConfig>>().Value;

        // Assert
        Assert.AreEqual("My Portfolio", config.Name);
        Assert.AreEqual("https://photos.example.com", config.BaseUrl);
        Assert.AreEqual("de", config.Language);
    }

    [TestMethod]
    public void ProjectConfig_DefaultValues_WhenNotSpecified()
    {
        // Arrange: Minimal project.json without project section
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { theme = new { name = "Lumina" } }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<ProjectConfig>>().Value;

        // Assert: Defaults should apply
        Assert.AreEqual(string.Empty, config.Name);
        Assert.AreEqual("en", config.Language);
    }

    [TestMethod]
    public void ThemeConfig_LoadsFromProjectJson()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                theme = new
                {
                    name = "Lumina",
                    images = new
                    {
                        sizes = new List<int> { 320, 640, 1280, 1920 }
                    }
                }
            }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<ThemeConfig>>().Value;

        // Assert
        Assert.AreEqual("Lumina", config.Name);
        Assert.HasCount(4, config.Images.Sizes);
        Assert.AreEqual(320, config.Images.Sizes[0]);
        Assert.AreEqual(1920, config.Images.Sizes[3]);
    }

    [TestMethod]
    public void PathsConfig_DefaultValues()
    {
        // Arrange
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<PathsConfig>>().Value;

        // Assert: Default paths are "source" and "output"
        Assert.AreEqual("source", config.Source);
        Assert.AreEqual("output", config.Output);
    }

    [TestMethod]
    public void PathsConfig_CustomPaths_LoadFromProjectJson()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                paths = new
                {
                    source = "photos",
                    output = "dist"
                }
            }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<PathsConfig>>().Value;

        // Assert
        Assert.AreEqual("photos", config.Source);
        Assert.AreEqual("dist", config.Output);
    }

    [TestMethod]
    public void GenerateConfig_ImageQualities_LoadFromProjectJson()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                generate = new
                {
                    images = new
                    {
                        avif = 70,
                        webp = 80,
                        jpg = 85
                    }
                }
            }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<GenerateConfig>>().Value;

        // Assert
        Assert.AreEqual(70, config.Images.Avif);
        Assert.AreEqual(80, config.Images.Webp);
        Assert.AreEqual(85, config.Images.Jpg);
    }
}

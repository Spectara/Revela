using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IConfigService"/> reading and writing
/// project.json and site.json on the real filesystem.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ConfigServiceTests
{
    [TestMethod]
    public void IsProjectInitialized_WithProjectJson_ReturnsTrue()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();

        Assert.IsTrue(configService.IsProjectInitialized());
    }

    [TestMethod]
    public void IsProjectInitialized_EmptyDirectory_ReturnsFalse()
    {
        // Create temp dir without project.json
        var tempDir = Path.Combine(Path.GetTempPath(), "revela-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            using var host = RevelaTestHost.Build(tempDir, s => s.AddRevelaCommands());
            var configService = host.Services.GetRequiredService<IConfigService>();

            Assert.IsFalse(configService.IsProjectInitialized());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void IsSiteConfigured_WithSiteJson_ReturnsTrue()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Test" }));
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();

        Assert.IsTrue(configService.IsSiteConfigured());
    }

    [TestMethod]
    public void IsSiteConfigured_WithoutSiteJson_ReturnsFalse()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();

        Assert.IsFalse(configService.IsSiteConfigured());
    }

    [TestMethod]
    public async Task ReadProjectConfigAsync_ReturnsJsonObject()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Site" },
                theme = new { name = "Lumina" }
            }));
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();
        var config = await configService.ReadProjectConfigAsync();

        Assert.IsNotNull(config);
        Assert.AreEqual("My Site", config["project"]?["name"]?.GetValue<string>());
        Assert.AreEqual("Lumina", config["theme"]?["name"]?.GetValue<string>());
    }

    [TestMethod]
    public async Task ReadProjectConfigAsync_NoFile_ReturnsNull()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "revela-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            using var host = RevelaTestHost.Build(tempDir, s => s.AddRevelaCommands());
            var configService = host.Services.GetRequiredService<IConfigService>();

            var config = await configService.ReadProjectConfigAsync();

            Assert.IsNull(config);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_DeepMergesValues()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Original", language = "en" },
                theme = new { name = "Lumina" }
            }));
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();

        // Update only the project name — other fields should be preserved
        var updates = new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "Updated Name" }
        };
        await configService.UpdateProjectConfigAsync(updates);

        // Read back and verify merge
        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.AreEqual("Updated Name", config["project"]?["name"]?.GetValue<string>());
        Assert.AreEqual("en", config["project"]?["language"]?.GetValue<string>(),
            "Existing fields should be preserved during merge");
        Assert.AreEqual("Lumina", config["theme"]?["name"]?.GetValue<string>(),
            "Unrelated sections should be preserved");
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_WritesFormattedJson()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();

        var updates = new JsonObject { ["project"] = new JsonObject { ["name"] = "Test" } };
        await configService.UpdateProjectConfigAsync(updates);

        // Verify file is pretty-printed (contains newlines/indentation)
        var content = await File.ReadAllTextAsync(project.ProjectJsonPath);
        Assert.IsTrue(content.Contains('\n', StringComparison.Ordinal), "JSON should be pretty-printed");
        Assert.IsTrue(content.Contains("  ", StringComparison.Ordinal), "JSON should be indented");
    }

    [TestMethod]
    public async Task ReadSiteConfigAsync_ReturnsSiteConfig()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new
            {
                title = "My Portfolio",
                author = "Test Author"
            }));
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var configService = host.Services.GetRequiredService<IConfigService>();
        var config = await configService.ReadSiteConfigAsync();

        Assert.IsNotNull(config);
        Assert.AreEqual("My Portfolio", config["title"]?.GetValue<string>());
        Assert.AreEqual("Test Author", config["author"]?.GetValue<string>());
    }
}

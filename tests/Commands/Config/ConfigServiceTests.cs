using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Config;

[TestClass]
[TestCategory("Integration")]
public sealed class ConfigServiceTests
{
    [TestMethod]
    public async Task UpdateProjectConfigAsync_DeepMergesAndPreservesExistingValues()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            project = new { name = "Original", baseUrl = "https://example.com" },
            theme = new { name = "Lumina" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var updates = new JsonObject
        {
            ["project"] = new JsonObject { ["name"] = "Updated" }
        };

        await configService.UpdateProjectConfigAsync(updates);

        var config = await configService.ReadProjectConfigAsync();
        Assert.IsNotNull(config);
        Assert.AreEqual("Updated", config["project"]?["name"]?.GetValue<string>());
        Assert.AreEqual("https://example.com", config["project"]?["baseUrl"]?.GetValue<string>());
        Assert.AreEqual("Lumina", config["theme"]?["name"]?.GetValue<string>());
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_CanceledBeforeReplacement_PreservesOriginalBytes()
    {
        using var project = TestProject.Create(p => p.WithProjectJson(new
        {
            project = new { name = "Original" },
            theme = new { name = "Lumina" }
        }));
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();
        var original = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => configService.UpdateProjectConfigAsync(
            new JsonObject { ["theme"] = new JsonObject { ["name"] = "Other" } },
            cancellation.Token));

        var afterCancellation = await File.ReadAllBytesAsync(project.ProjectJsonPath);
        CollectionAssert.AreEqual(original, afterCancellation);
        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    [TestMethod]
    public async Task UpdateProjectConfigAsync_Success_LeavesNoTempFile()
    {
        using var project = TestProject.Create();
        using var host = RevelaTestHost.Build(project.RootPath, services => services.AddRevelaCommands());
        var configService = host.Services.GetRequiredService<IConfigService>();

        await configService.UpdateProjectConfigAsync(
            new JsonObject { ["theme"] = new JsonObject { ["name"] = "Lumina" } });

        Assert.IsEmpty(GetTempFiles(project.RootPath));
    }

    private static IReadOnlyList<string> GetTempFiles(string projectPath) =>
        Directory.GetFiles(projectPath, ".project.json.*.tmp");
}

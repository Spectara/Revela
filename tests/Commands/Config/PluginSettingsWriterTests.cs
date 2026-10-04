using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Commands.Config;

[TestClass]
[TestCategory("Integration")]
public sealed class PluginSettingsWriterTests
{
    private const string OriginalJson = /*lang=json,strict*/ """
        {"project":{"name":"Writer"},"plugins":{"serve":{"port":8080},"writerProbe":{"mode":"slow","keep":true}}}
        """;

    private static readonly Assembly OwnAssembly = typeof(WriterProbeConfig).Assembly;
    private static readonly Assembly OtherAssembly = typeof(IPlugin).Assembly;

    [TestMethod]
    public async Task WriteAsync_KeyClaimedByDeclaringAssembly_MergesOnlyOwnSection()
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, OriginalJson);
        using var host = BuildHost(project, new PluginConfigClaim("writerProbe", "Probe.Plugin", OwnAssembly), new PluginConfigClaim("serve", "Spectara.Revela.Plugins.Serve", OtherAssembly));
        var writer = host.Services.GetRequiredService<IPluginSettingsWriter<WriterProbeConfig>>();

        await writer.WriteAsync(new JsonObject { ["mode"] = "fast", ["keep"] = null });

        var saved = JsonNode.Parse(await File.ReadAllTextAsync(project.ProjectJsonPath));
        var expected = JsonNode.Parse(/*lang=json,strict*/ """
            {"project":{"name":"Writer"},"plugins":{"serve":{"port":8080},"writerProbe":{"mode":"fast"}}}
            """);
        Assert.IsTrue(JsonNode.DeepEquals(expected, saved), saved?.ToJsonString());
    }

    [TestMethod]
    public async Task WriteAsync_KeyClaimedByAnotherPackage_ThrowsAndLeavesFileUntouched()
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, OriginalJson);
        using var host = BuildHost(project, new PluginConfigClaim("writerProbe", "Contoso.Plugin", OtherAssembly));
        var writer = host.Services.GetRequiredService<IPluginSettingsWriter<WriterProbeConfig>>();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => writer.WriteAsync(new JsonObject { ["mode"] = "fast" }));

        Assert.Contains("Contoso.Plugin", exception.Message, StringComparison.Ordinal);
        Assert.AreEqual(OriginalJson, await File.ReadAllTextAsync(project.ProjectJsonPath));
    }

    [TestMethod]
    public async Task WriteAsync_KeyNotClaimed_ThrowsAndLeavesFileUntouched()
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, OriginalJson);
        using var host = BuildHost(project);
        var writer = host.Services.GetRequiredService<IPluginSettingsWriter<WriterProbeConfig>>();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => writer.WriteAsync(new JsonObject { ["mode"] = "fast" }));

        Assert.Contains("plugins:writerProbe", exception.Message, StringComparison.Ordinal);
        Assert.AreEqual(OriginalJson, await File.ReadAllTextAsync(project.ProjectJsonPath));
    }

    [TestMethod]
    public async Task WriteAsync_HostSectionType_Throws()
    {
        using var project = TestProject.Create();
        await File.WriteAllTextAsync(project.ProjectJsonPath, OriginalJson);
        using var host = BuildHost(project, new PluginConfigClaim("writerProbe", "Probe.Plugin", OwnAssembly));
        var writer = host.Services.GetRequiredService<IPluginSettingsWriter<ProjectConfig>>();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => writer.WriteAsync(new JsonObject { ["name"] = "Hijacked" }));

        Assert.AreEqual(OriginalJson, await File.ReadAllTextAsync(project.ProjectJsonPath));
    }

    [TestMethod]
    public async Task WriteAsync_TypeWithoutRevelaConfig_Throws()
    {
        using var project = TestProject.Create();
        using var host = BuildHost(project, new PluginConfigClaim("writerProbe", "Probe.Plugin", OwnAssembly));
        var writer = host.Services.GetRequiredService<IPluginSettingsWriter<UnmarkedProbeConfig>>();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => writer.WriteAsync(new JsonObject { ["mode"] = "fast" }));

        Assert.Contains(nameof(UnmarkedProbeConfig), exception.Message, StringComparison.Ordinal);
    }

    private static IHost BuildHost(TestProject project, params PluginConfigClaim[] claims) =>
        RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddSingleton(PluginConfigOwnership.FromClaims(claims));
        });
}

[RevelaConfig("plugins:writerProbe")]
internal sealed class WriterProbeConfig
{
    public const string Section = "plugins:writerProbe";

    public string Mode { get; set; } = "slow";
}

internal sealed class UnmarkedProbeConfig
{
    public string Mode { get; set; } = "slow";
}

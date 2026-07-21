using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Spectara.Revela.Commands;
using Spectara.Revela.Commands.Config.Project;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ConfigProjectCommand"/>, guarding the drift bug where the
/// command wrote/read the base URL under a <c>"url"</c> key while
/// <see cref="ProjectConfig.BaseUrl"/> binds from <c>"baseUrl"</c> (a #76 rename artifact). The
/// command now writes and reads <c>"baseUrl"</c> so the value actually reaches the bound config.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ConfigProjectCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithUrl_BindsToProjectConfigBaseUrl()
    {
        // Arrange: a fresh project without any base URL.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Original" },
                theme = new { name = "Lumina" },
            }));
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var command = host.Services.GetRequiredService<ConfigProjectCommand>();

        // Act: set a base URL non-interactively.
        var exitCode = await command.ExecuteAsync("My Site", "https://photo.example.com", CancellationToken.None);

        // Assert: the URL must actually reach the bound config — this is the drift regression.
        Assert.AreEqual(0, exitCode);
        var config = host.Services.GetRequiredService<IOptionsMonitor<ProjectConfig>>().CurrentValue;
        Assert.IsNotNull(config.BaseUrl);
        Assert.AreEqual("https://photo.example.com/", config.BaseUrl.ToString());
    }

    [TestMethod]
    public async Task ExecuteAsync_EmptyUrl_YieldsNullBaseUrlAndNoStrayKey()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Original" },
                theme = new { name = "Lumina" },
            }));
        using var host = RevelaTestHost.Build(project.RootPath, s => s.AddRevelaCommands());

        var command = host.Services.GetRequiredService<ConfigProjectCommand>();

        // Act: an empty base URL (allowed by the wizard / `--url ""`).
        var exitCode = await command.ExecuteAsync("My Site", "", CancellationToken.None);

        // Assert: no bind exception, BaseUrl is null ("not configured"), and no stray legacy key.
        Assert.AreEqual(0, exitCode);
        var config = host.Services.GetRequiredService<IOptionsMonitor<ProjectConfig>>().CurrentValue;
        Assert.IsNull(config.BaseUrl);

        var written = await File.ReadAllTextAsync(project.ProjectJsonPath, CancellationToken.None);
        Assert.IsFalse(
            written.Contains("\"url\"", StringComparison.Ordinal),
            "project.json must not contain a stray legacy 'url' key.");
        Assert.IsFalse(
            written.Contains("\"baseUrl\"", StringComparison.Ordinal),
            "An empty base URL must not be persisted as an empty 'baseUrl'.");
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterSettingUrl_CheckDropsNoBaseUrlHint()
    {
        // Arrange: a valid project with no base URL initially emits the "No baseUrl" hint.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "HintProbe" },
                theme = new { name = "Lumina" },
            })
            .WithSiteJson(new { title = "Hint Probe", author = "Test" })
            .AddGallery("Landscapes", g => g.AddImage("sunset.jpg")));
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var validator = host.Services.GetRequiredService<ISiteValidator>();
        var command = host.Services.GetRequiredService<ConfigProjectCommand>();

        // Act: confirm the hint is present, set a URL, then re-validate.
        var before = await validator.ValidateAsync();
        Assert.IsTrue(
            before.Any(d => d.Severity == ValidationSeverity.Hint
                && d.Message.Contains("baseUrl", StringComparison.OrdinalIgnoreCase)),
            "Precondition: a project without a base URL should emit the hint.");

        await command.ExecuteAsync("HintProbe", "https://x.example.com", CancellationToken.None);
        var after = await validator.ValidateAsync();

        // Assert: the hint is gone once a base URL is configured.
        Assert.IsFalse(
            after.Any(d => d.Severity == ValidationSeverity.Hint
                && d.Message.Contains("baseUrl", StringComparison.OrdinalIgnoreCase)),
            "The 'No baseUrl' hint must disappear after a base URL is set.");
    }
}

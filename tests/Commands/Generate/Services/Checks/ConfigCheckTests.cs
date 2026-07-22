using Microsoft.Extensions.Options;

using NSubstitute;

using Spectara.Revela.Features.Generate.Services.Checks;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Commands.Generate.Services.Checks;

/// <summary>
/// Unit tests for <see cref="ConfigCheck"/> — the required site title error and the
/// non-blocking base-URL hint.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ConfigCheckTests
{
    [TestMethod]
    public async Task ValidateAsync_MissingTitle_ReportsError()
    {
        var check = CreateCheck(baseUrl: new Uri("https://example.com"), title: "");

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Error
                && d.Message.Contains("title", StringComparison.OrdinalIgnoreCase)),
            "A missing site title must be a blocking error.");
    }

    [TestMethod]
    public async Task ValidateAsync_NoBaseUrl_EmitsHint()
    {
        var check = CreateCheck(baseUrl: null, title: "My Site");

        var diagnostics = await check.ValidateAsync();

        Assert.IsTrue(
            diagnostics.Any(d => d.Severity == ValidationSeverity.Hint
                && d.Message.Contains("baseUrl", StringComparison.OrdinalIgnoreCase)),
            "A missing baseUrl must emit a hint.");
        Assert.IsEmpty(diagnostics.Where(d => d.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task ValidateAsync_TitleAndBaseUrlPresent_ReportsNothing()
    {
        var check = CreateCheck(baseUrl: new Uri("https://example.com"), title: "My Site");

        var diagnostics = await check.ValidateAsync();

        Assert.IsEmpty(diagnostics);
    }

    private static ConfigCheck CreateCheck(Uri? baseUrl, string title)
    {
        var projectConfig = Substitute.For<IOptionsMonitor<ProjectConfig>>();
        projectConfig.CurrentValue.Returns(new ProjectConfig { Name = "Test", BaseUrl = baseUrl });

        var siteConfig = Substitute.For<IOptionsMonitor<SiteCoreConfig>>();
        siteConfig.CurrentValue.Returns(new SiteCoreConfig { Title = title });

        return new ConfigCheck(projectConfig, siteConfig);
    }
}

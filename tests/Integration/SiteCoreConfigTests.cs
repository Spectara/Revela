using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="SiteCoreConfig"/> — the validated identity
/// core of site.json — and the breaking-change guard that rejects a stray
/// <c>language</c> left in project.json (issue #75).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class SiteCoreConfigTests
{
    [TestMethod]
    public void SiteCoreConfig_BindsRootLevelSiteJson_UnderSiteSection()
    {
        // Arrange: site.json stores properties at the document root; the AddSiteJson
        // source re-keys them under the "site" section so SiteCoreConfig can bind.
        using var project = TestProject.Create(p => p
            .WithSiteJson(new
            {
                title = "My Portfolio",
                description = "Landscapes and portraits",
                author = "Jane Doe",
                copyright = "© 2026 Jane Doe",
                language = "de"
            }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<SiteCoreConfig>>().Value;

        // Assert
        Assert.AreEqual("My Portfolio", config.Title);
        Assert.AreEqual("Landscapes and portraits", config.Description);
        Assert.AreEqual("Jane Doe", config.Author);
        Assert.AreEqual("© 2026 Jane Doe", config.Copyright);
        Assert.AreEqual("de", config.Language);
    }

    [TestMethod]
    public void SiteCoreConfig_LanguageDefaultsToEn_WhenNotSpecified()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "My Portfolio" }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act
        var config = host.Services.GetRequiredService<IOptions<SiteCoreConfig>>().Value;

        // Assert
        Assert.AreEqual("en", config.Language);
    }

    [TestMethod]
    public void SiteCoreConfig_MissingTitle_FailsValidation()
    {
        // Arrange: site.json without the required title.
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { author = "Jane Doe" }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act + Assert
        var options = host.Services.GetRequiredService<IOptions<SiteCoreConfig>>();
        var ex = Assert.ThrowsExactly<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("title", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ProjectConfig_WithLanguage_FailsWithErrorPointingToSiteJson()
    {
        // Arrange: 'language' has moved to site.json — a stray value in project.json
        // must fail loudly instead of being silently ignored.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "My Portfolio", language = "de" }
            }));
        using var host = RevelaTestHost.Build(project.RootPath);

        // Act + Assert
        var options = host.Services.GetRequiredService<IOptions<ProjectConfig>>();
        var ex = Assert.ThrowsExactly<OptionsValidationException>(() => _ = options.Value);
        Assert.Contains("site.json", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

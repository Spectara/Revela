using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk.Json;
using Spectara.Revela.Tests.Shared.Fixtures;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Verifies that non-core <c>site.json</c> properties (beyond the validated
/// <c>SiteCoreConfig</c> identity core) survive into templates via the dynamic
/// <see cref="JsonElement"/> site tail that the render model exposes (issue #75).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class SiteJsonTailTests
{
    [TestMethod]
    public void SiteTail_NonCoreProperty_IsReachableInTemplates()
    {
        // Arrange: site.json carries the identity core plus theme-free tail
        // properties (heroImage, contactEmail) that no config class binds.
        using var project = TestProject.Create(p => p
            .WithSiteJson(new
            {
                title = "My Portfolio",
                description = "Landscapes and portraits",
                author = "Jane Doe",
                language = "de",
                heroImage = "hero.jpg",
                contactEmail = "jane@example.com"
            }));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
        });

        // Load the site tail exactly the way RenderService builds SiteModel.Site.
        var siteJson = File.ReadAllText(Path.Combine(project.RootPath, "site.json"));
        var siteTail = JsonDocument.Parse(siteJson, RevelaJsonOptions.LenientDocument).RootElement.Clone();

        var engine = host.Services.GetRequiredService<ITemplateEngine>();

        // Act: render a template against the same model key ("site") the pipeline uses.
        var output = engine.Render(
            "{{ site.heroImage }}|{{ site.contactEmail }}|{{ site.title }}",
            new Dictionary<string, object?> { ["site"] = siteTail });

        // Assert: the non-core tail properties reach the template alongside the core.
        Assert.AreEqual("hero.jpg|jane@example.com|My Portfolio", output);
    }
}

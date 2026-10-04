using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// Renders pages with the revela.website theme override (samples/revela-website/themes/Lumina,
/// linked into the test output) on top of Lumina.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public sealed partial class WebsiteThemeEndToEndTests
{
    [TestMethod]
    public async Task RenderAsync_DocsSidebar_ListsThirdLevelPages()
    {
        var pages = await RenderWebsiteAsync();

        var guidePage = Sidebar(pages["docs/guide/source-structure/index.html"]);
        Assert.Contains(">Static Files</a>", guidePage);
        Assert.Contains("aria-current=\"page\">Source Structure</a>", guidePage);

        var staticFiles = Sidebar(pages["docs/guide/source-structure/static-files/index.html"]);
        Assert.Contains("aria-current=\"page\">Static Files</a>", staticFiles);
        Assert.AreEqual(1, AriaCurrentPattern().Count(staticFiles), "Only the current page is marked.");
        Assert.Contains(">Source Structure</a>", staticFiles);
        Assert.Contains(">Pages</a>", staticFiles);
    }

    [TestMethod]
    public async Task RenderAsync_WebsitePages_HeadingLevelsNeverSkip()
    {
        var pages = await RenderWebsiteAsync();

        foreach (var (page, html) in pages)
        {
            HeadingOrderAssert.Sequential(page, html);
        }
    }

    private static string Sidebar(string html)
    {
        var start = html.IndexOf("<aside>", StringComparison.Ordinal);
        var end = html.IndexOf("</aside>", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, "The docs page needs a sidebar.");
        return html[start..end];
    }

    /// <summary>
    /// Renders a docs tree three levels deep below the Docs section:
    /// Docs / Guide (container) / Source Structure / Static Files, plus Guide / Pages.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>> RenderWebsiteAsync()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Website" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Revela", author = "Test", description = "Site description" }));

        var themeSource = Path.Combine(AppContext.BaseDirectory, "WebsiteTheme");
        var themeTarget = Path.Combine(project.RootPath, ProjectPaths.Themes, "Lumina");
        foreach (var file in Directory.GetFiles(themeSource, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(themeTarget, Path.GetRelativePath(themeSource, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        await WriteIndexAsync(project, "", "+++\ntitle = \"Revela\"\n+++\n");
        await WriteIndexAsync(project, "01 Docs", "+++\ntitle = \"Docs\"\ntemplate = \"docs\"\n+++\nDocs intro.\n");
        await WriteIndexAsync(project, "01 Docs/01 Guide", "+++\ntitle = \"Guide\"\ncontainer = true\n+++\n");
        await WriteIndexAsync(project, "01 Docs/01 Guide/01 Source-Structure", "+++\ntitle = \"Source Structure\"\ntemplate = \"docs\"\n+++\n## Folders\n");
        await WriteIndexAsync(project, "01 Docs/01 Guide/01 Source-Structure/Static-Files", "+++\ntitle = \"Static Files\"\ntemplate = \"docs\"\n+++\n## Favicon\n");
        await WriteIndexAsync(project, "01 Docs/01 Guide/02 Pages", "+++\ntitle = \"Pages\"\ntemplate = \"docs\"\n+++\nPages text.\n");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();

        Assert.IsTrue(scan.Success, scan.ErrorMessage);
        Assert.IsTrue(render.Success, render.ErrorMessage);
        return Directory.GetFiles(project.OutputPath, "*.html", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(project.OutputPath, path).Replace('\\', '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
    }

    private static async Task WriteIndexAsync(TestProject project, string folder, string content)
    {
        var directory = Path.Combine(project.SourcePath, folder);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "_index.revela"), content);
    }

    [GeneratedRegex("aria-current=\"page\"")]
    private static partial Regex AriaCurrentPattern();
}

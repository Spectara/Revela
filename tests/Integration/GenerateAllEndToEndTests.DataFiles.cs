using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;
using Spectara.Revela.Themes.Lumina.Statistics;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// A page's <c>data</c> front matter names a JSON file in a plugin's owner folder
/// (<c>.revela/&lt;owner&gt;/&lt;page&gt;/&lt;file&gt;</c>). Only bare <c>*.json</c> file names are read,
/// so a value like <c>../../x.json</c> cannot reach files outside the page's data folder.
/// </summary>
public sealed partial class GenerateAllEndToEndTests
{
    private static readonly object CameraStatistics = new
    {
        total_images = 7,
        total_galleries = 1,
        cameras = new object[] { new { name = "Data File Camera", count = 7, percentage = 100 } },
        lenses = new object[] { new { name = "Data File Lens", count = 7, percentage = 100 } },
        photo_heatmap = new object[] { new { year = "2024", month = 1, count = 7, level = 4 } },
        heatmap_years = new object[] { "2024" },
    };

    [TestMethod]
    public async Task RenderAsync_CustomDataFileName_RendersDataFromThatFile()
    {
        var html = await RenderStatisticsPageWithDataAsync(
            dataSource: "camera-stats.json",
            files: [("camera-stats.json", CameraStatistics)]);

        Assert.Contains("Data File Camera", html, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("../escape.json")]
    [DataRow("../../statistics/escape.json")]
    [DataRow("nested/escape.json")]
    public async Task RenderAsync_DataFileNameWithFolders_IsNotRead(string dataSource)
    {
        // The escaping targets exist and hold valid data, so only the name check keeps them out.
        var html = await RenderStatisticsPageWithDataAsync(
            dataSource,
            files: [(dataSource, CameraStatistics)]);

        Assert.DoesNotContain("Data File Camera", html, StringComparison.Ordinal);
        Assert.Contains("No statistics yet.", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Renders <c>stats/</c> with <c>template = "statistics/overview"</c> and
    /// <c>data.statistics = <paramref name="dataSource"/></c>; each file is written relative to
    /// the page's folder in the statistics owner folder.
    /// </summary>
    private static async Task<string> RenderStatisticsPageWithDataAsync(
        string dataSource,
        IReadOnlyList<(string RelativePath, object Data)> files)
    {
        const string pageFolder = "stats";
        using var project = TestProject.Create(builder => builder
            .WithProjectJson(new { project = new { name = "Data Files" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Data Files", author = "Test", language = "en" }));
        var pagePath = Path.Combine(project.SourcePath, pageFolder);
        Directory.CreateDirectory(pagePath);
        await File.WriteAllTextAsync(Path.Combine(pagePath, "_index.revela"), $"""
            +++
            title = "Extension"
            template = "statistics/overview"
            data.statistics = "{dataSource}"
            +++
            """);
        var pageDataPath = Path.Combine(project.RootPath, ProjectPaths.GetOwnerDirectory("statistics"), pageFolder);
        Directory.CreateDirectory(pageDataPath);
        foreach (var (relativePath, data) in files)
        {
            var filePath = Path.GetFullPath(Path.Combine(pageDataPath, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(data));
        }

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<ITheme>(new LuminaStatisticsExtension());
        });

        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();

        Assert.IsTrue(scan.Success, scan.ErrorMessage);
        Assert.IsTrue(render.Success, render.ErrorMessage);
        return await File.ReadAllTextAsync(Path.Combine(project.OutputPath, pageFolder, "index.html"));
    }
}

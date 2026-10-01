using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// End-to-end tests: XMP metadata embedded in real JPEGs flows through
/// scan → manifest → filter/render into Lumina's output.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public sealed class XmpMetadataEndToEndTests
{
    private static async Task RenderAsync(TestProject project)
    {
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var renderService = host.Services.GetRequiredService<IRenderService>();
        renderService.SetTheme(host.Services.GetRequiredService<ITheme>());
        renderService.SetExtensions([]);

        var scanResult = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
    }

    private static TestProject CreateDescribedPhotoProject() => TestProject.Create(p => p
        .WithSiteJson(new { title = "XMP", author = "Test" })
        .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 1920, 1080, exif => exif
            .WithIso(200)
            .WithXmpMetadata(
                title: "Abendlicht <am> \"See\"",
                description: "Sonnenuntergang & Wolken",
                keywords: ["Selected"],
                rating: 5))));

    [TestMethod]
    public async Task GeneratePages_PhotoPageWithXmp_ShowsEscapedTitleAndDescription()
    {
        using var project = CreateDescribedPhotoProject();

        await RenderAsync(project);

        var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "photo", "photos", "photo", "index.html"));
        const string escapedTitle = "Abendlicht &lt;am&gt; &quot;See&quot;";
        Assert.Contains($"<h1 class=\"visually-hidden\">{escapedTitle}</h1>", html);
        Assert.Contains($"<span data-photo-label>{escapedTitle}</span>", html);
        Assert.Contains($"alt=\"{escapedTitle}\"", html);
        Assert.Contains("<p data-photo-description>Sonnenuntergang &amp; Wolken</p>", html);
        Assert.Contains($"<meta name=\"description\" content=\"{escapedTitle}\">", html);
        Assert.DoesNotContain("Selected", html);
    }

    [TestMethod]
    public async Task GeneratePages_LightboxWithXmp_ShowsTitleAndDescription()
    {
        using var project = CreateDescribedPhotoProject();
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Photos", "_index.revela"),
            "+++\nphoto_viewer = \"lightbox\"\n+++\n");

        await RenderAsync(project);

        var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "photos", "index.html"));
        Assert.Contains("<span data-photo-label>Abendlicht &lt;am&gt; &quot;See&quot;</span>", html);
        Assert.Contains("<p data-photo-description>Sonnenuntergang &amp; Wolken</p>", html);
        Assert.DoesNotContain("Selected", html);
    }

    [TestMethod]
    public async Task GeneratePages_PhotoWithoutTitle_KeepsFileNameLabelAndFallbackHeading()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "XMP", author = "Test" })
            .AddGallery("Photos", g => g.AddRealImage("photo.jpg", 1920, 1080, exif => exif
                .WithXmpMetadata(keywords: ["Selected"], rating: 3))));

        await RenderAsync(project);

        var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "photo", "photos", "photo", "index.html"));
        Assert.Contains("<h1 class=\"visually-hidden\">Photo photo</h1>", html);
        Assert.Contains("<span data-photo-label>photo</span>", html);
        Assert.DoesNotContain("data-photo-description", html);
    }

    [TestMethod]
    public async Task GeneratePages_FilterByRatingAndKeyword_SelectsMatchingImages()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "XMP", author = "Test" })
            .AddGallery("Photos", g => g
                .AddRealImage("best.jpg", 1280, 720, exif => exif.WithXmpMetadata(keywords: ["Startseite"], rating: 5))
                .AddRealImage("good.jpg", 1280, 720, exif => exif.WithXmpMetadata(keywords: ["Selected"], rating: 4))
                .AddRealImage("okay.jpg", 1280, 720, exif => exif.WithXmpMetadata(keywords: ["startseite"], rating: 2))
                .AddRealImage("plain.jpg", 1280, 720)));
        Directory.CreateDirectory(Path.Combine(project.SourcePath, "Best"));
        Directory.CreateDirectory(Path.Combine(project.SourcePath, "Home"));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Best", "_index.revela"),
            "+++\nfilter = \"rating >= 4 | sort rating desc\"\n+++\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Home", "_index.revela"),
            "+++\nfilter = \"contains(keywords, 'Startseite')\"\n+++\n");

        await RenderAsync(project);

        var best = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "best", "index.html"));
        Assert.Contains("photos/best/", best);
        Assert.Contains("photos/good/", best);
        Assert.DoesNotContain("photos/okay/", best);
        Assert.DoesNotContain("photos/plain/", best);
        Assert.IsLessThan(best.IndexOf("photos/good/", StringComparison.Ordinal), best.IndexOf("photos/best/", StringComparison.Ordinal));

        var home = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "home", "index.html"));
        Assert.Contains("photos/best/", home);
        Assert.Contains("photos/okay/", home);
        Assert.DoesNotContain("photos/good/", home);
        Assert.DoesNotContain("photos/plain/", home);
    }
}

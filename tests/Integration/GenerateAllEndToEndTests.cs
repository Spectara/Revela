using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Spectara.Revela.Commands;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Tests.Shared.Fixtures;
using Spectara.Revela.Themes.Lumina;
using Spectara.Revela.Themes.Lumina.Calendar;
using Spectara.Revela.Themes.Lumina.Statistics;

namespace Spectara.Revela.Tests.Integration;

/// <summary>
/// End-to-end test that runs the full generation pipeline on a
/// project with generated test images and verifies the output.
/// </summary>
/// <remarks>
/// <para>
/// Creates a temporary project with real JPEG images (via <see cref="TestImageGenerator"/>),
/// registers the Lumina theme, runs scan → render → image processing, and verifies
/// that HTML pages and resized images are generated correctly.
/// </para>
/// <para>
/// Calls services directly (not CLI commands) to avoid Spectre.Console
/// terminal handle issues in non-interactive test runners.
/// </para>
/// </remarks>
[TestClass]
[TestCategory("E2E")]
public sealed class GenerateAllEndToEndTests
{
    private const string UntrustedText = "<script>x</script> & \"q\"";
    private const string EscapedText = "&lt;script&gt;x&lt;/script&gt; &amp; &quot;q&quot;";
    private const string UntrustedAttribute = "1\" onmouseover=\"x";
    private const string EscapedAttribute = "1&quot; onmouseover=&quot;x";

    [TestMethod]
    public async Task RenderAsync_LuminaEscapesTextButPreservesBodyHtml()
    {
        using var project = TestProject.Create(builder => builder
            .WithProjectJson(new { project = new { name = "Escaping" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Site <mark> & \"name\"", author = "Author \"quoted\"", copyright = "<strong>Copyright</strong>" })
            .AddGallery("Photos", gallery => gallery.AddRealImage("one.jpg", 800, 600)));
        await File.WriteAllTextAsync(Path.Combine(project.SourcePath, "Photos", "_index.revela"), """
            +++
            title = 'Gallery <mark> & "title"'
            description = 'Description "quoted" & <mark>'
            pinned = true
            +++
            **Body stays formatted**

            ![Alt "quoted" & <mark>](one.jpg)
            """);
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();

        Assert.IsTrue(scan.Success);
        Assert.IsTrue(render.Success, render.ErrorMessage);
        var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "photos", "index.html"));
        Assert.DoesNotContain("<mark>", html, StringComparison.Ordinal);
        Assert.Contains("Gallery &lt;mark&gt; &amp; &quot;title&quot;", html, StringComparison.Ordinal);
        Assert.Contains("content=\"Description &quot;quoted&quot; &amp; &lt;mark&gt;\"", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Body stays formatted</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Copyright</strong>", html, StringComparison.Ordinal);
        var photoFile = Directory.GetFiles(Path.Combine(project.OutputPath, "photo"), "index.html", SearchOption.AllDirectories).Single();
        var photoHtml = await File.ReadAllTextAsync(photoFile);
        Assert.DoesNotContain("<mark>", photoHtml, StringComparison.Ordinal);
        Assert.Contains("Site &lt;mark&gt; &amp; &quot;name&quot;", photoHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("og:image", photoHtml, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/gallery/")]
    public async Task RenderAsync_AssetUrl_ResolvesToWrittenThemeAsset(string basePath)
    {
        using var project = TestProject.Create(builder => builder
            .WithProjectJson(new { project = new { name = "Assets", basePath }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Assets", author = "Test" })
            .AddGallery("Photos", gallery => gallery.AddRealImage("one.jpg", 800, 600)));
        var partialsPath = Path.Combine(project.RootPath, ProjectPaths.Themes, "Lumina", "Partials");
        Directory.CreateDirectory(partialsPath);
        await File.WriteAllTextAsync(
            Path.Combine(partialsPath, "Favicon.revela"),
            "<link rel=\"x-asset\" href=\"{{ asset_url 'main.css' }}\">");
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
        foreach (var page in new[] { "index.html", "photos/index.html" })
        {
            var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, page));
            var href = ExtractAttributeValues(html, "rel=\"x-asset\" href=\"").Single();
            var siteRoot = new Uri($"https://example.com{basePath}");
            var resolved = new Uri(new Uri(siteRoot, page), href);
            Assert.StartsWith(siteRoot.AbsolutePath, resolved.AbsolutePath, $"{page}: {href}");
            var outputRelative = resolved.AbsolutePath[siteRoot.AbsolutePath.Length..];
            Assert.IsTrue(
                File.Exists(Path.Combine(project.OutputPath, outputRelative)),
                $"{page}: asset_url produced '{href}', which does not resolve to a written file.");
        }
    }

    [TestMethod]
    public async Task RenderAsync_LuminaStatisticsEscapesExifData()
    {
        var statistics = new
        {
            total_images = 1,
            total_galleries = 1,
            cameras = new object[] { new { name = UntrustedText, count = UntrustedAttribute, percentage = 100 } },
            lenses = new object[] { new { name = UntrustedText, count = 1, percentage = 100 } },
            photo_heatmap = new object[] { new { year = UntrustedText, month = 1, count = UntrustedAttribute, level = UntrustedAttribute } },
            heatmap_years = new object[] { UntrustedText },
        };

        var html = await RenderExtensionPageAsync(
            new LuminaStatisticsExtension(),
            "stats",
            "statistics/overview",
            "statistics.json",
            statistics);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("\" onmouseover=\"", html, StringComparison.Ordinal);
        Assert.Contains($"<dt data-count=\"{EscapedAttribute}\">{EscapedText}</dt>", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"heatmap-label\">{EscapedText}</span>", html, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_StatisticsHomePage_ReceivesExtensionDataLikeGalleryPages()
    {
        var statistics = new
        {
            total_images = 4242,
            total_galleries = 7,
            cameras = Array.Empty<object>(),
            lenses = Array.Empty<object>(),
            photo_heatmap = Array.Empty<object>(),
            heatmap_years = Array.Empty<object>(),
        };

        var html = await RenderExtensionPageAsync(
            new LuminaStatisticsExtension(),
            string.Empty,
            "statistics/overview",
            "statistics.json",
            statistics);

        Assert.Contains("<strong>4242</strong>", html, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_LuminaCalendarEscapesCalendarData()
    {
        var calendar = new
        {
            day_names = new[] { UntrustedText },
            labels = new { booked = UntrustedText, free = UntrustedText, arrive = UntrustedText, depart = UntrustedText },
            months = new object[]
            {
                new
                {
                    name = UntrustedText,
                    weeks = new object[] { new object[] { new { number = 1, css = UntrustedAttribute } } },
                },
            },
        };

        var html = await RenderExtensionPageAsync(
            new LuminaCalendarExtension(),
            "availability",
            "calendar/page",
            "calendar.json",
            calendar);

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("\" onmouseover=\"", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"legend-free\">{EscapedText}</span>", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"legend-booked\">{EscapedText}</span>", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"legend-arrive\">{EscapedText}</span>", html, StringComparison.Ordinal);
        Assert.Contains($"<span class=\"legend-depart\">{EscapedText}</span>", html, StringComparison.Ordinal);
        Assert.Contains($"<h2>{EscapedText}</h2>", html, StringComparison.Ordinal);
        Assert.Contains($"<th>{EscapedText}</th>", html, StringComparison.Ordinal);
        Assert.Contains($"<td class=\"{EscapedAttribute}\">1</td>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Body stays formatted</strong>", html, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_GermanSiteLanguage_LocalizesLuminaPhotoUi()
    {
        var pages = await RenderLocalizedPhotoSiteAsync("de");

        Assert.Contains("title=\"Menü\"", pages.Gallery, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">Menü</span>", pages.Gallery, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Foto schließen\"", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Nächstes Foto\"", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Vorheriges Foto\"", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("<nav aria-label=\"Fotonavigation\">", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("<strong>Zu sehen in:</strong>", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Fotonavigation\"", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Zurück zu Island &lt;script&gt;x&lt;/script&gt;\"", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Nächstes Foto in Island &lt;script&gt;x&lt;/script&gt;\"", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">Blende </span>", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">Belichtungszeit </span>", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">Brennweite </span>", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("<span>EF 50mm an Canon EOS R5</span>", pages.Photo, StringComparison.Ordinal);
        foreach (var english in new[]
        {
            "Close photo", "Next photo", "Previous photo", "Photo navigation", "Seen in:", "Return to",
            ">Menu<", "\"Menu\"", "Aperture", "Shutter", "Focal length", "EF 50mm on Canon",
        })
        {
            Assert.DoesNotContain(english, pages.Gallery + pages.Lightbox + pages.Photo, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<script>x", pages.Photo, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_DefaultSiteLanguage_KeepsEnglishLuminaPhotoUi()
    {
        var pages = await RenderLocalizedPhotoSiteAsync(language: null);

        Assert.Contains("title=\"Menu\"", pages.Gallery, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Close photo\"", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("<strong>Seen in:</strong>", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Return to Island &lt;script&gt;x&lt;/script&gt;\"", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("<span class=\"visually-hidden\">Aperture </span>", pages.Photo, StringComparison.Ordinal);
        Assert.Contains("<span>EF 50mm on Canon EOS R5</span>", pages.Photo, StringComparison.Ordinal);
        Assert.DoesNotContain("Foto schließen", pages.Lightbox, StringComparison.Ordinal);
        foreach (var key in new[] { "nav.menu", "photo.close", "photo.return_to", "photo.aperture", "photo.seen_in" })
        {
            Assert.DoesNotContain(key, pages.Gallery + pages.Lightbox + pages.Photo, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task RenderAsync_LocalLocaleOverride_ReplacesOnlyOverriddenLabel()
    {
        var pages = await RenderLocalizedPhotoSiteAsync("de", localOverride: /*lang=json,strict*/ """{ "photo.close": "Schließen" }""");

        Assert.Contains("aria-label=\"Schließen\"", pages.Lightbox, StringComparison.Ordinal);
        Assert.DoesNotContain("Foto schließen", pages.Lightbox, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Nächstes Foto\"", pages.Lightbox, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_GermanSiteLanguage_LocalizesLuminaStatistics()
    {
        var statistics = new
        {
            total_images = 3,
            total_galleries = 1,
            cameras = new object[]
            {
                new { name = "Canon EOS R5", count = 2, percentage = 100 },
                new { name = "Other", key = "other", count = 1, percentage = 50 },
            },
            lenses = new object[] { new { name = "EF 50mm", count = 1, percentage = 100 } },
            orientations = new object[] { new { name = "Landscape", key = "orientation.landscape", count = 3, percentage = 100 } },
            images_by_month = new object[] { new { name = "March", key = "month.3", count = 3, percentage = 100 } },
            photo_heatmap = new object[] { new { year = 2024, month = 3, count = 3, level = 4 } },
            heatmap_years = new object[] { 2024 },
        };

        var html = await RenderExtensionPageAsync(
            new LuminaStatisticsExtension(),
            "stats",
            "statistics/overview",
            "statistics.json",
            statistics,
            language: "de");

        foreach (var german in new[]
        {
            "<h2>Übersicht</h2>", "</strong> Bilder</span>", "</strong> Galerien</span>", "</strong> Kameras</span>",
            "</strong> Objektive</span>", "<h3>Kameramodelle</h3>", "<h3>Fotoaktivität</h3>", "<h3>Fotos nach Monat</h3>",
            "<h3>Ausrichtung</h3>", ">Querformat</dt>", ">März</dt>", ">Andere</dt>", ">Canon EOS R5</dt>",
            "title=\"3 Fotos\"", "<span class=\"heatmap-month\">Mär</span>", "<span>Weniger</span>",
        })
        {
            Assert.Contains(german, html, StringComparison.Ordinal);
        }

        foreach (var english in new[] { "Overview", "Camera Models", "Photo Activity", "Photos by Month", "Landscape", "March", ">Other<", "photos\"", ">Less<" })
        {
            Assert.DoesNotContain(english, html, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task RenderAsync_GermanSiteLanguage_TranslatesUnsetCalendarLabels()
    {
        var calendar = new
        {
            day_names = new[] { "Mo" },
            labels = new { booked = (string?)null, free = (string?)null, arrive = (string?)null, depart = "Abfahrt" },
            months = Array.Empty<object>(),
        };

        var html = await RenderExtensionPageAsync(
            new LuminaCalendarExtension(),
            "availability",
            "calendar/page",
            "calendar.json",
            calendar,
            language: "de");

        Assert.Contains("<span class=\"legend-free\">Frei</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"legend-booked\">Belegt</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"legend-arrive\">Anreise</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"legend-depart\">Abfahrt</span>", html, StringComparison.Ordinal);
    }

    [TestMethod]
    [DataRow("en", "No statistics yet.", "No calendar data yet.")]
    [DataRow("de", "Noch keine Statistik vorhanden.", "Noch keine Kalenderdaten vorhanden.")]
    public async Task RenderAsync_ExtensionDataFileMissing_RendersPageWithNoticeInsteadOfFailing(
        string language,
        string statisticsNotice,
        string calendarNotice)
    {
        // `generate scan` deletes stale plugin data; running `generate pages` next must not abort every page.
        var statisticsHtml = await RenderExtensionPageAsync(
            new LuminaStatisticsExtension(), "stats", "statistics/overview", "statistics.json", data: null, language);
        var calendarHtml = await RenderExtensionPageAsync(
            new LuminaCalendarExtension(), "availability", "calendar/page", "calendar.json", data: null, language);

        Assert.Contains("<h1>Extension</h1>", statisticsHtml, StringComparison.Ordinal);
        Assert.Contains(statisticsNotice, statisticsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("stats-summary", statisticsHtml, StringComparison.Ordinal);
        Assert.Contains("<h1>Extension</h1>", calendarHtml, StringComparison.Ordinal);
        Assert.Contains(calendarNotice, calendarHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("calendar-legend", calendarHtml, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_LuminaExtensionPages_RenderExactlyOneTitleH1()
    {
        var statisticsHtml = await RenderExtensionPageAsync(
            new LuminaStatisticsExtension(),
            "stats",
            "statistics/overview",
            "statistics.json",
            new { total_images = 0, total_galleries = 0, cameras = Array.Empty<object>(), lenses = Array.Empty<object>() });
        var calendarHtml = await RenderExtensionPageAsync(
            new LuminaCalendarExtension(),
            "availability",
            "calendar/page",
            "calendar.json",
            new
            {
                day_names = Array.Empty<string>(),
                labels = new { },
                months = new object[] { new { name = "January", weeks = new object[] { new object[] { new { number = 1, css = "free" } } } } },
            });

        foreach (var html in new[] { statisticsHtml, calendarHtml })
        {
            Assert.AreEqual(1, CountOccurrences(html, "<h1"));
            Assert.Contains("<h1>Extension</h1>", html, StringComparison.Ordinal);
        }

        HeadingOrderAssert.Sequential("statistics", statisticsHtml);
        HeadingOrderAssert.Sequential("calendar", calendarHtml);
    }

    private sealed record LocalizedPages(string Gallery, string Lightbox, string Photo);

    /// <summary>
    /// Renders a Lumina site with one photo-page gallery (EXIF metadata, hostile title)
    /// and one lightbox gallery for the given <c>site.language</c>.
    /// </summary>
    private static async Task<LocalizedPages> RenderLocalizedPhotoSiteAsync(string? language, string? localOverride = null)
    {
        object site = language is null
            ? new { title = "Localized", author = "Test" }
            : new { title = "Localized", author = "Test", language };
        static void Exif(ExifOptions exif) => exif
            .WithCamera("Canon", "EOS R5").WithLens("EF 50mm").WithAperture(2.8).WithShutterSpeed(0.004).WithFocalLength(50).WithIso(200);
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Localized" }, theme = new { name = "Lumina" } })
            .WithSiteJson(site)
            .AddGallery("Island", g => g.AddRealImage("first.jpg", 800, 600, Exif).AddRealImage("second.jpg", 800, 600, Exif))
            .AddGallery("Lightbox", g => g.AddRealImage("one.jpg", 800, 600, Exif).AddRealImage("two.jpg", 800, 600, Exif)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Island", "_index.revela"),
            "+++\ntitle = \"Island <script>x</script>\"\n+++\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Lightbox", "_index.revela"),
            "+++\nphoto_viewer = \"lightbox\"\n+++\n");
        if (localOverride is not null)
        {
            var localesPath = Path.Combine(project.RootPath, ProjectPaths.Themes, "Lumina", "Locales");
            Directory.CreateDirectory(localesPath);
            await File.WriteAllTextAsync(Path.Combine(localesPath, $"{language}.json"), localOverride);
        }

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
        var photoFile = Directory.GetFiles(Path.Combine(project.OutputPath, "photo"), "index.html", SearchOption.AllDirectories)
            .First(path => File.ReadAllText(path).Contains("rel=\"next\"", StringComparison.Ordinal));
        return new LocalizedPages(
            await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "island", "index.html")),
            await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "lightbox", "index.html")),
            await File.ReadAllTextAsync(photoFile));
    }

    /// <summary>
    /// Renders one page that uses an extension template with a plugin-style
    /// <c>.cache/&lt;page&gt;/&lt;dataFile&gt;</c> data file and returns its HTML.
    /// </summary>
    private static async Task<string> RenderExtensionPageAsync(
        ITheme extension,
        string pageFolder,
        string template,
        string dataFile,
        object? data,
        string language = "en")
    {
        using var project = TestProject.Create(builder => builder
            .WithProjectJson(new { project = new { name = "Extension Escaping" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Extension Escaping", author = "Test", language }));
        var pagePath = Path.Combine(project.SourcePath, pageFolder);
        Directory.CreateDirectory(pagePath);
        await File.WriteAllTextAsync(Path.Combine(pagePath, "_index.revela"), $"""
            +++
            title = "Extension"
            template = "{template}"
            +++
            **Body stays formatted**
            """);
        var cachePath = Path.Combine(project.RootPath, ProjectPaths.Cache, pageFolder);
        Directory.CreateDirectory(cachePath);
        if (data is not null)
        {
            await File.WriteAllTextAsync(Path.Combine(cachePath, dataFile), JsonSerializer.Serialize(data));
        }
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton(extension);
        });

        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();

        Assert.IsTrue(scan.Success, scan.ErrorMessage);
        Assert.IsTrue(render.Success, render.ErrorMessage);
        return await File.ReadAllTextAsync(Path.Combine(project.OutputPath, pageFolder, "index.html"));
    }

    [TestMethod]
    public async Task RenderAsync_DerivedArtifactInvalidationFails_DoesNotWriteOutput()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Invalidation Failure" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Invalidation Failure", author = "Test" }));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
            services.AddSingleton<IArtifactInvalidator, FailingRenderedSiteDependent>();
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");
        Assert.IsFalse(renderResult.Success);
        StringAssert.Contains(renderResult.ErrorMessage, "locked sidecar", StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(project.OutputPath, "index.html")));
    }

    [TestMethod]
    public async Task GenerateAll_ImagelessSite_ImageStepIsSuccessfulNoOp()
    {
        // Arrange: a photo-less site (e.g. calendar-only) with no galleries, no images,
        // and no image formats configured. The image step must be a successful no-op
        // rather than failing the build on the "no formats configured" guard.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Imageless" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Imageless", author = "Test" }));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();
        var themePlugin = host.Services.GetRequiredService<ITheme>();

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");
        await renderService.RenderAsync();
        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Assert: no images to process → success with zero processed, no error
        Assert.IsTrue(imageResult.Success,
            $"Image step must succeed on an image-less site, not fail: {imageResult.ErrorMessage}");
        Assert.AreEqual(0, imageResult.ProcessedCount, "No images means nothing processed");
        Assert.IsNull(imageResult.ErrorMessage, "Image-less site must not surface an error");
    }

    private sealed class FailingRenderedSiteDependent : IArtifactInvalidator
    {
        public ArtifactId Artifact { get; } = new("example/failing-derived-site");

        public IReadOnlyCollection<ArtifactId> DependsOn { get; } = [CoreArtifacts.RenderedSite];

        public ValueTask<OperationResult> InvalidateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<OperationResult>(
                OperationResult.Fail("locked sidecar"));
        }
    }

    [TestMethod]
    public async Task GenerateAll_RootOnlySite_CountsIndexExactlyOnce()
    {
        // Arrange: a site with no galleries — only the root index page (#99 regression)
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Root Only" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Root Only", author = "Test" }));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var themePlugin = host.Services.GetRequiredService<ITheme>();

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");

        var renderResult = await renderService.RenderAsync();

        // Assert: the root index page contributes exactly one to the count
        Assert.IsTrue(renderResult.Success, $"Render should succeed: {renderResult.ErrorMessage}");
        Assert.AreEqual(1, renderResult.PageCount,
            "A root-only site renders exactly one page (the index, counted once — see #99)");
    }

    [TestMethod]
    public async Task GenerateAll_WithTestImages_ProducesHtmlAndImages()
    {
        // Arrange: Create a realistic project structure with real images
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "E2E Test Portfolio" },
                theme = new { name = "Lumina" },
                generate = new
                {
                    images = new { avif = 80, webp = 85, jpg = 90 }
                }
            })
            .WithSiteJson(new
            {
                title = "Test Portfolio",
                language = "de",
                author = "Test Author",
                copyright = "2025 Test",
                description = "E2E test site"
            })
            .AddGallery("Landscapes", g => g
                .WithMarkdown("# Landscapes\n\nBeautiful scenery from around the world.")
                .AddRealImage("sunset.jpg", 1920, 1080, exif => exif
                    .WithCamera("Canon", "EOS R5")
                    .WithDescription("Golden sunset")
                    .WithIso(100)
                    .WithAperture(8.0)
                    .WithFocalLength(24)
                    .WithDateTaken(new DateTime(2025, 6, 15, 19, 30, 0, DateTimeKind.Utc)))
                .AddRealImage("mountain.jpg", 2560, 1440, exif => exif
                    .WithCamera("Sony", "ILCE-7M4")
                    .WithIso(200)
                    .WithAperture(11)
                    .WithFocalLength(70)))
            .AddGallery("Portraits", g => g
                .WithMarkdown("# Portraits\n\nPeople and faces.")
                .AddRealImage("person.jpg", 1280, 1920, exif => exif
                    .WithCamera("Canon", "EOS R5")
                    .WithIso(400)
                    .WithAperture(2.8)
                    .WithFocalLength(85)
                    .WithLens("RF 85mm F1.2L"))));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        // Configure render service with Lumina theme
        var themePlugin = host.Services.GetRequiredService<ITheme>();

        // Act: Run the pipeline steps directly (avoids Spectre.Console terminal issues)

        // Step 1: Scan content
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");
        Assert.AreEqual(2, scanResult.GalleryCount, "Should find 2 galleries");
        Assert.AreEqual(3, scanResult.ImageCount, "Should find 3 images");

        // Step 2: Render HTML pages
        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, $"Render should succeed: {renderResult.ErrorMessage}");
        Assert.AreEqual(6, renderResult.PageCount,
            "Root index + 2 galleries + 3 photo pages = 6 (index counted once, see #99/#77)");

        // Step 3: Process images
        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Image processing should succeed: {imageResult.ErrorMessage}");

        // Assert: Output directory structure
        Assert.IsTrue(Directory.Exists(project.OutputPath),
            "Output directory should be created");

        // Assert: HTML pages generated
        var indexHtml = Path.Combine(project.OutputPath, "index.html");
        Assert.IsTrue(File.Exists(indexHtml),
            "Root index.html should be generated");

        // Slugs are lowercase (UrlBuilder.ToSlug lowercases all names)
        var landscapesHtml = Path.Combine(project.OutputPath, "landscapes", "index.html");
        Assert.IsTrue(File.Exists(landscapesHtml),
            "Landscapes gallery page should be generated");

        var portraitsHtml = Path.Combine(project.OutputPath, "portraits", "index.html");
        Assert.IsTrue(File.Exists(portraitsHtml),
            "Portraits gallery page should be generated");

        // Assert: HTML content
        var landscapesContent = await File.ReadAllTextAsync(landscapesHtml);
        Assert.Contains("<html lang=\"de\">", landscapesContent);
        Assert.IsTrue(landscapesContent.Contains("Landscapes", StringComparison.Ordinal),
            "Gallery page should contain gallery title");
        Assert.Contains("<nav id=\"site-menu\" popover=\"auto\" aria-label=\"Menü\">", landscapesContent);
        Assert.Contains("<button type=\"button\" popovertarget=\"site-menu\"", landscapesContent);
        // site.json "language": "de" selects the German theme strings; html_escape keeps umlauts literal.
        Assert.Contains($"<span class=\"visually-hidden\">Menü</span>", landscapesContent);
        Assert.AreEqual(3, CountOccurrences(landscapesContent, "<span aria-hidden=\"true\">i</span>"));
        Assert.DoesNotContain("aria-label=\"Menu\"", landscapesContent);
        Assert.DoesNotContain("type=\"checkbox\"", landscapesContent);
        Assert.DoesNotContain("nav-trigger", landscapesContent);

        // Assert: Image variants generated
        var imagesDir = Path.Combine(project.OutputPath, "images");
        Assert.IsTrue(Directory.Exists(imagesDir),
            "Images output directory should be created");

        var jpgFiles = Directory.EnumerateFiles(imagesDir, "*.jpg", SearchOption.AllDirectories).ToList();
        Assert.IsTrue(jpgFiles.Count > 0,
            $"Expected JPG image variants, found {jpgFiles.Count}");

        var webpFiles = Directory.EnumerateFiles(imagesDir, "*.webp", SearchOption.AllDirectories).ToList();
        Assert.IsTrue(webpFiles.Count > 0,
            $"Expected WebP image variants, found {webpFiles.Count}");

        // Assert: Theme assets copied
        var assetsDir = Path.Combine(project.OutputPath, "_assets");
        Assert.IsTrue(Directory.Exists(assetsDir),
            "Assets directory should be created with theme assets");

        var cssFiles = Directory.EnumerateFiles(assetsDir, "*.css", SearchOption.AllDirectories).ToList();
        Assert.IsTrue(cssFiles.Count > 0,
            "Theme CSS files should be copied to output");

        // Assert: photo pages (#77) — one canonical page per published source image.
        var sunsetPhoto = Path.Combine(project.OutputPath, "photo", "landscapes", "sunset", "index.html");
        Assert.IsTrue(File.Exists(sunsetPhoto), "Photo page for sunset.jpg should be generated");

        var sunsetPhotoContent = await File.ReadAllTextAsync(sunsetPhoto);
        Assert.Contains("<html lang=\"de\">", sunsetPhotoContent);
        Assert.Contains("<meta name=\"description\" content=\"E2E test site\">", sunsetPhotoContent);
        Assert.Contains("fetchpriority=\"high\" decoding=\"async\">", sunsetPhotoContent);
        // No baseUrl configured: canonical links need an absolute URL, so none is emitted.
        Assert.DoesNotContain("rel=\"canonical\"", sunsetPhotoContent);
        // up returns to the originating gallery occurrence via the #photo-* anchor.
        Assert.Contains("#photo-i-landscapes_sunset", sunsetPhotoContent);
        // no wraparound: the first image in the gallery has a next but no previous link.
        Assert.Contains("photo/landscapes/mountain/", sunsetPhotoContent);
        var mountainPhotoContent = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "photo", "landscapes", "mountain", "index.html"));
        var landscapePhotoContent = sunsetPhotoContent + mountainPhotoContent;
        Assert.Contains("aria-label=\"Vorheriges Foto in Landscapes\"><span aria-hidden=\"true\">&lsaquo;</span></a>", landscapePhotoContent);
        Assert.Contains($"aria-label=\"Nächstes Foto in Landscapes\"><span aria-hidden=\"true\">&rsaquo;</span></a>", landscapePhotoContent);
        Assert.Contains("aria-hidden=\"true\"></span>", landscapePhotoContent);
        Assert.Contains("data-photo-return rel=\"up\"", sunsetPhotoContent);
        Assert.Contains($"aria-label=\"Zurück zu Landscapes\">&times;</a>", sunsetPhotoContent);
        Assert.Contains("<span data-photo-label>Golden sunset</span>", sunsetPhotoContent);
        Assert.Contains("<a href=\"../../../landscapes/#photo-i-landscapes_sunset\">Landscapes</a>", sunsetPhotoContent);
        Assert.AreEqual(1, CountOccurrences(sunsetPhotoContent, ">Landscapes</a>"));
        var normalizedSunsetPhoto = NormalizeLineEndings(sunsetPhotoContent);
        Assert.Contains("<body class=\"photo-page\">\n    <main>\n        <h1 class=\"visually-hidden\">Foto sunset</h1>\n        <article style=", normalizedSunsetPhoto);
        Assert.Contains("--lqip:", sunsetPhotoContent);
        Assert.Contains(" data-lqip>", sunsetPhotoContent);
        Assert.DoesNotContain("sizes=\"100vw\"", sunsetPhotoContent);
        Assert.Contains("/1920.avif 1920w\">", sunsetPhotoContent);
        Assert.Contains("sizes=\"(min-aspect-ratio: ", sunsetPhotoContent);
        Assert.Contains("</picture>\n            <section>\n                <nav class=\"photo-nav\"", normalizedSunsetPhoto);
        Assert.AreEqual(1, CountOccurrences(sunsetPhotoContent, "<nav class=\"photo-nav\""));
        Assert.Contains("<div id=\"ctx-g-landscapes\" data-primary>", sunsetPhotoContent);
        Assert.AreEqual(1, CountOccurrences(sunsetPhotoContent, "data-photo-return rel=\"up\""));
        Assert.Contains("<aside class=\"photo-metadata\">", sunsetPhotoContent);
        Assert.Contains("<aside class=\"photo-metadata\">\n    <p>", normalizedSunsetPhoto);
        Assert.Contains($"<footer>\n                    <strong>Zu sehen in:</strong>", normalizedSunsetPhoto);
        AssertRetiredPhotoClassesAreAbsent(sunsetPhotoContent);
        Assert.DoesNotContain("photo-stage", sunsetPhotoContent);
        Assert.DoesNotContain("photo-detail", sunsetPhotoContent);
        Assert.DoesNotContain("photo-memberships", sunsetPhotoContent);
        Assert.DoesNotContain("Appears in", sunsetPhotoContent);
        Assert.DoesNotContain("photo-metadata-file", sunsetPhotoContent);
        Assert.DoesNotContain("Date taken", sunsetPhotoContent);
        Assert.DoesNotContain("Dimensions", sunsetPhotoContent);
        Assert.DoesNotContain("File size", sunsetPhotoContent);
        Assert.Contains("_assets/zoom.js", sunsetPhotoContent);
        Assert.DoesNotContain("_assets/lightbox.js", sunsetPhotoContent);

        // Assert: the Lumina gallery no longer emits an inline lightbox figure (#77).
        Assert.IsFalse(landscapesContent.Contains("<figure", StringComparison.Ordinal),
            "Gallery markup must not contain the removed inline lightbox figure");
        Assert.Contains("/photo/landscapes/sunset/", landscapesContent);
    }

    [TestMethod]
    public async Task GenerateAll_CollidingGalleryFolders_ScanFailsAndWritesNoOutput()
    {
        // Arrange: "01 Events" and "02 Events" both normalize to the slug "events", and each
        // holds a "photo.jpg" — so both the gallery page and the image variants would target the
        // same output paths. The scan must reject this before anything is written (#97).
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Colliding" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Colliding Site", author = "Test" })
            .AddGallery("01 Events", g => g.AddImage("photo.jpg"))
            .AddGallery("02 Events", g => g.AddImage("photo.jpg")));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act: the scan step is the gate — it must fail before rendering.
        var scanResult = await contentService.ScanAsync();

        // Assert: scan failed and the error names the slug plus every conflicting source path.
        Assert.IsFalse(scanResult.Success, "Scan must fail when two galleries collide to one slug.");
        Assert.IsNotNull(scanResult.ErrorMessage);
        Assert.Contains("events", scanResult.ErrorMessage);
        Assert.Contains("01 Events", scanResult.ErrorMessage);
        Assert.Contains("02 Events", scanResult.ErrorMessage);

        // Assert: nothing was written — no gallery page could silently overwrite another.
        var writtenHtml = Directory.Exists(project.OutputPath)
            ? Directory.EnumerateFiles(project.OutputPath, "*.html", SearchOption.AllDirectories).ToList()
            : [];
        Assert.IsEmpty(writtenHtml, "A failed scan must not write any output.");
    }

    [TestMethod]
    public async Task GenerateAll_GalleryNameNormalizingToEmptySlug_ScanFails()
    {
        // Arrange: a folder whose name consists only of removed characters → empty slug,
        // which would collide with the site root (#97).
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Empty Slug" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Empty Slug Site", author = "Test" })
            .AddGallery("!!!", g => g.AddImage("photo.jpg")));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();

        // Act
        var scanResult = await contentService.ScanAsync();

        // Assert
        Assert.IsFalse(scanResult.Success, "Scan must fail on an empty gallery slug.");
        Assert.IsNotNull(scanResult.ErrorMessage);
        Assert.Contains("!!!", scanResult.ErrorMessage);

        var writtenHtml = Directory.Exists(project.OutputPath)
            ? Directory.EnumerateFiles(project.OutputPath, "*.html", SearchOption.AllDirectories).ToList()
            : [];
        Assert.IsEmpty(writtenHtml, "A failed scan must not write any output.");
    }

    [TestMethod]
    public async Task GenerateAll_NestedGalleries_ProducesCorrectStructure()
    {
        // Arrange: Project with nested gallery structure (like OneDrive sample)
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Nested Test", author = "Test" })
            .AddGallery("Events", g => g
                .AddSubGallery("Fireworks", sg => sg
                    .WithMarkdown("# Fireworks\n\nNew Year celebrations.")
                    .AddRealImage("boom.jpg", 1920, 1080, exif => exif
                        .WithCamera("Canon", "EOS R5")
                        .WithIso(3200)
                        .WithAperture(2.8)))
                .AddSubGallery("Racing", sg => sg
                    .AddRealImage("car.jpg", 2560, 1440)))
            .AddGallery("Nature", g => g
                .WithMarkdown("# Nature")
                .AddRealImage("tree.jpg", 1920, 1080)));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<ITheme>();

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");

        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Images failed: {imageResult.ErrorMessage}");

        // Assert: Nested galleries create nested output structure
        Assert.AreEqual(3, scanResult.GalleryCount,
            "Should find 3 galleries (Fireworks, Racing, Nature)");
        Assert.AreEqual(3, scanResult.ImageCount,
            "Should find 3 images total");

        // Assert: Root index exists
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "index.html")));

        // Assert: Nested pages generated (slugified paths)
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "events", "fireworks", "index.html")),
            "Nested gallery Fireworks should have its page");
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "nature", "index.html")),
            "Top-level gallery Nature should have its page");

        // Assert: Navigation should include nested items
        var rootHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "index.html"));
        Assert.IsTrue(rootHtml.Contains("Nature", StringComparison.Ordinal),
            "Root page should contain Nature in navigation");
    }

    [TestMethod]
    public async Task GenerateAll_IncrementalBuild_SkipsCachedImages()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Cache Test", author = "Test" })
            .AddGallery("Photos", g => g
                .AddRealImage("photo1.jpg", 1280, 720)
                .AddRealImage("photo2.jpg", 1280, 720)));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<ITheme>();

        // Act: First run — everything processed
        await contentService.ScanAsync();
        await renderService.RenderAsync();
        var firstRun = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Act: Second run — should skip cached images
        await contentService.ScanAsync();
        await renderService.RenderAsync();
        var secondRun = await imageService.ProcessAsync(new ProcessImagesOptions());

        // Assert: Both succeed
        Assert.IsTrue(firstRun.Success);
        Assert.IsTrue(secondRun.Success);

        // Assert: Second run processes fewer (or zero) images due to caching
        Assert.IsTrue(secondRun.ProcessedCount <= firstRun.ProcessedCount,
            $"Second run should process same or fewer images (first: {firstRun.ProcessedCount}, second: {secondRun.ProcessedCount})");
    }

    [TestMethod]
    public async Task GenerateAll_DuplicateFilenames_CreatesDistinctOutputPaths()
    {
        // Arrange: Two galleries with the same image filename (e.g., camera numbering "001.jpg")
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Duplicate Test", author = "Test" })
            .AddGallery("Gallery A", g => g
                .AddRealImage("001.jpg", 1920, 1080, exif => exif
                    .WithCamera("Canon", "EOS R5")))
            .AddGallery("Gallery B", g => g
                .AddRealImage("001.jpg", 2560, 1440, exif => exif
                    .WithCamera("Sony", "A7R V"))));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var imageService = host.Services.GetRequiredService<IImageService>();

        var themePlugin = host.Services.GetRequiredService<ITheme>();

        // Act
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");

        var renderResult = await renderService.RenderAsync();
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var imageResult = await imageService.ProcessAsync(new ProcessImagesOptions());
        Assert.IsTrue(imageResult.Success, $"Images failed: {imageResult.ErrorMessage}");

        // Assert: Both images should be processed (not overwritten)
        Assert.AreEqual(2, scanResult.ImageCount,
            "Should find 2 images (one per gallery)");

        // Assert: Distinct output directories for each image
        var imagesDir = Path.Combine(project.OutputPath, "images");
        var galleryADir = Path.Combine(imagesDir, "gallery-a", "001");
        var galleryBDir = Path.Combine(imagesDir, "gallery-b", "001");

        Assert.IsTrue(Directory.Exists(galleryADir),
            $"Gallery A image directory should exist at: gallery-a/001");
        Assert.IsTrue(Directory.Exists(galleryBDir),
            $"Gallery B image directory should exist at: gallery-b/001");

        // Assert: Both directories contain image variants
        var galleryAFiles = Directory.GetFiles(galleryADir).Length;
        var galleryBFiles = Directory.GetFiles(galleryBDir).Length;

        Assert.IsTrue(galleryAFiles > 0,
            "Gallery A should have image variants");
        Assert.IsTrue(galleryBFiles > 0,
            "Gallery B should have image variants");
    }

    [TestMethod]
    public async Task GeneratePages_InlineGallery_RendersMidContentAndSuppressesTrailingGrid()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Inline Gallery Test", author = "Test" })
            .AddGallery("Inline Gallery", g => g
                .AddRealImage("first.jpg", 1920, 1080)
                .AddRealImage("second.jpg", 1920, 1080))
            .AddGallery("Default Gallery", g => g
                .AddRealImage("default.jpg", 1920, 1080)));

        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Inline Gallery", "_index.revela"),
            "Before inline gallery.\n\n[[gallery]]\n\nAfter inline gallery.");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Default Gallery", "_index.revela"),
            "Default gallery body.");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var theme = host.Services.GetRequiredService<ITheme>();

        // Act
        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var inlineHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "inline-gallery", "index.html"));
        var beforePosition = inlineHtml.IndexOf("Before inline gallery.", StringComparison.Ordinal);
        var gridPosition = inlineHtml.IndexOf("<section class=\"gallery\">", StringComparison.Ordinal);
        var afterPosition = inlineHtml.IndexOf("After inline gallery.", StringComparison.Ordinal);

        Assert.IsTrue(beforePosition < gridPosition, "Inline grid should render after preceding content.");
        Assert.IsTrue(gridPosition < afterPosition, "Inline grid should render before following content.");
        Assert.AreEqual(1, CountOccurrences(inlineHtml, "<section class=\"gallery\">"),
            "Inline page must not render the automatic trailing grid.");
        Assert.DoesNotContain("[[gallery]]", inlineHtml);

        var defaultHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "default-gallery", "index.html"));
        Assert.AreEqual(1, CountOccurrences(defaultHtml, "<section class=\"gallery\">"),
            "A page without a token must retain its automatic trailing grid.");
        Assert.HasCount(1, ExtractPhotoHrefs(defaultHtml));
        Assert.IsTrue(File.Exists(
            Path.Combine(project.OutputPath, "photo", "default-gallery", "default", "index.html")));

        var inlinePhotoHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "photo", "inline-gallery", "first", "index.html"));
        Assert.AreEqual(1, CountOccurrences(inlinePhotoHtml, "id=\"ctx-g-inline-gallery\""),
            "Duplicate bare blocks must share one base membership context.");
    }

    [TestMethod]
    public async Task GeneratePages_InlineGalleryFilteredSharedImage_CreatesLinkedPhotoPage()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Filtered Inline Gallery", author = "Test" })
            .AddGallery("Filtered", g => g
                .WithMarkdown("[[gallery: filename == 'shared.jpg']]")
                .AddRealImage("local.jpg", 1920, 1080)));
        TestImageGenerator.CreateJpeg(
            Path.Combine(project.SourcePath, "_images", "shared.jpg"),
            1920,
            1080);
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Filtered", "_index.revela"),
            "[[gallery: filename == 'shared.jpg']]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        var galleryHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "filtered", "index.html"));
        var href = ExtractPhotoHrefs(galleryHtml).Single();
        Assert.EndsWith("photo/shared/#ctx-g-filtered.grid-1", href);
        Assert.IsTrue(File.Exists(
            Path.Combine(project.OutputPath, "photo", "shared", "index.html")));
        Assert.IsFalse(File.Exists(
            Path.Combine(project.OutputPath, "photo", "filtered-local", "index.html")),
            "A suppressed base grid must not create hidden photo pages.");
    }

    [TestMethod]
    public async Task GeneratePages_OverlappingFilteredInlineGalleryBlocks_UseDistinctOccurrenceLinks()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Overlapping Inline Galleries", author = "Test" })
            .AddGallery("Featured", g => g
                .WithMarkdown(
                    "[[gallery: filename == 'shared.jpg']]\n\n[[gallery: filename == 'shared.jpg']]")
                .AddRealImage("local.jpg", 1920, 1080)));
        TestImageGenerator.CreateJpeg(
            Path.Combine(project.SourcePath, "_images", "shared.jpg"),
            1920,
            1080);
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Featured", "_index.revela"),
            "[[gallery: filename == 'shared.jpg']]\n\n[[gallery: filename == 'shared.jpg']]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        var galleryHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "featured", "index.html"));
        Assert.AreEqual(1, CountOccurrences(galleryHtml, "id=\"grid-1-photo-i-shared\""));
        Assert.AreEqual(1, CountOccurrences(galleryHtml, "id=\"grid-2-photo-i-shared\""));
        var hrefs = ExtractPhotoHrefs(galleryHtml);
        Assert.IsTrue(hrefs.Any(href => href.EndsWith("#ctx-g-featured.grid-1", StringComparison.Ordinal)));
        Assert.IsTrue(hrefs.Any(href => href.EndsWith("#ctx-g-featured.grid-2", StringComparison.Ordinal)));

        var photoHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "photo", "shared", "index.html"));
        Assert.Contains("featured/#grid-1-photo-i-shared", photoHtml);
        Assert.Contains("featured/#grid-2-photo-i-shared", photoHtml);
    }

    [TestMethod]
    public async Task GeneratePages_BareInlineGallery_EveryPhotoPageLinkTargetsAnExistingAnchor()
    {
        // Arrange: a home page and a gallery that both place their own photos with [[gallery]]
        // between texts, as the bare block replaces the trailing grid.
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Anchors", author = "Test" })
            .AddGallery("Years", g => g
                .AddRealImage("fence.jpg", 1920, 1080)
                .AddRealImage("lake.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "_index.revela"),
            "+++\nfilter = \"all\"\n+++\nBefore.\n\n[[gallery]]\n\nAfter.");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Years", "_index.revela"),
            "Before.\n\n[[gallery]]\n\nAfter.");

        // Act
        var (scanResult, renderResult) = await ScanAndRenderAsync(project, new LuminaTheme());

        // Assert: every return and "seen in" link on a photo page lands on an element of its page.
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        var photoPages = Directory.GetFiles(Path.Combine(project.OutputPath, "photo"), "index.html", SearchOption.AllDirectories);
        Assert.HasCount(2, photoPages);
        var checkedLinks = 0;
        foreach (var photoPage in photoPages)
        {
            var html = await File.ReadAllTextAsync(photoPage);
            var pageUrl = new Uri(
                new Uri("https://site.test/"),
                Path.GetRelativePath(project.OutputPath, Path.GetDirectoryName(photoPage)!).Replace('\\', '/') + "/");
            var links = ExtractAttributeValues(html, "data-photo-return rel=\"up\" href=\"")
                .Concat(ExtractAttributeValues(html[html.IndexOf("<footer>", StringComparison.Ordinal)..], "<a href=\""));
            foreach (var link in links)
            {
                var target = new Uri(pageUrl, link);
                var targetFile = Path.Combine(project.OutputPath, target.AbsolutePath.TrimStart('/'), "index.html");
                var targetHtml = await File.ReadAllTextAsync(targetFile);
                Assert.Contains($" id=\"{target.Fragment.TrimStart('#')}\"", targetHtml,
                    $"{link} on {pageUrl.AbsolutePath} points to a missing anchor.");
                checkedLinks++;
            }
        }

        Assert.AreEqual(8, checkedLinks, "Each photo has a return link and a footer link for the home page and Years.");
    }

    private const string StoryContextId = "g-story";
    private const string FenceSlugId = "years_fence";

    [TestMethod]
    public async Task GeneratePages_PhotoToken_LinksPhotoPageWithPageContextAndKeepsGridIds()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Photo Token", author = "Test" })
            .AddGallery("Years", g => g
                .AddRealImage("fence.jpg", 1920, 1080)
                .AddRealImage("lake.jpg", 1920, 1080))
            .AddGallery("Story", g => g.AddRealImage("local.jpg", 1920, 1080)));
        TestImageGenerator.CreateJpeg(Path.Combine(project.SourcePath, "_images", "shared.jpg"), 1920, 1080);
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Years", "_index.revela"),
            "Our favourite:\n\n[[photo: fence.jpg]]");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Story", "_index.revela"),
            "Intro.\n\n[[photo: Years/fence.jpg]]\n\nMiddle.\n\n[[photo: Years/lake.jpg | gallery]]\n\n" +
            "[[gallery: filename == 'shared.jpg']]\n\nEnd.");

        // Act
        var (scanResult, renderResult) = await ScanAndRenderAsync(project, new LuminaTheme());

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var storyHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "story", "index.html"));
        Assert.AreEqual(2, CountOccurrences(storyHtml, "<figure class=\"photo-figure\""));
        Assert.AreEqual(1, CountOccurrences(storyHtml, $"id=\"photo-1-photo-i-{FenceSlugId}\""));
        var storyHrefs = ExtractPhotoHrefs(storyHtml);
        Assert.IsTrue(
            storyHrefs.Any(href => href.EndsWith($"photo/years/fence/#ctx-{StoryContextId}.photo-1", StringComparison.Ordinal)),
            string.Join(", ", storyHrefs));
        Assert.IsTrue(
            storyHrefs.Any(href => href.EndsWith("photo/years/lake/", StringComparison.Ordinal)),
            "A '| gallery' photo links to the photo page without a context fragment.");
        Assert.IsTrue(
            storyHrefs.Any(href => href.EndsWith($"photo/shared/#ctx-{StoryContextId}.grid-1", StringComparison.Ordinal)),
            "A [[photo]] before a filtered gallery must not shift its grid number.");
        Assert.AreEqual(1, CountOccurrences(storyHtml, "id=\"grid-1-photo-i-shared\""));
        Assert.AreEqual(1, CountOccurrences(storyHtml, "<section class=\"gallery\">"));
        Assert.IsLessThan(
            storyHtml.IndexOf("Middle.", StringComparison.Ordinal),
            storyHtml.IndexOf("<figure class=\"photo-figure\"", StringComparison.Ordinal));

        var yearsHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "years", "index.html"));
        Assert.AreEqual(1, CountOccurrences(yearsHtml, "<section class=\"gallery\">"),
            "A [[photo]] block must not suppress the page's trailing gallery grid.");
        Assert.AreEqual(1, CountOccurrences(yearsHtml, "<figure class=\"photo-figure\""));

        var fencePhotoHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "photo", "years", "fence", "index.html"));
        Assert.AreEqual(1, CountOccurrences(fencePhotoHtml, $"id=\"ctx-{StoryContextId}.photo-1\""));
        Assert.Contains($"story/#photo-1-photo-i-{FenceSlugId}\"", fencePhotoHtml);
        Assert.AreEqual(1, CountOccurrences(fencePhotoHtml, ">Years</a>"),
            "Two contexts on the same page must list the page label once.");

        var lakePhotoHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "photo", "years", "lake", "index.html"));
        Assert.DoesNotContain("-photo-", ExtractAttributeValues(lakePhotoHtml, "id=\"ctx-").Single());
    }

    [TestMethod]
    public async Task GeneratePages_PhotoTokenOnLightboxPage_CreatesPhotoPagesForSharedImages()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Photo Token", baseUrl = "https://example.com" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Photo Token Lightbox", author = "Test" })
            .AddGallery("Story", g => g.AddRealImage("local.jpg", 1920, 1080)));
        TestImageGenerator.CreateJpeg(Path.Combine(project.SourcePath, "_images", "only.jpg"), 1920, 1080);
        TestImageGenerator.CreateJpeg(Path.Combine(project.SourcePath, "_images", "other.jpg"), 1920, 1080);
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Story", "_index.revela"),
            "+++\nphoto_viewer = \"lightbox\"\n+++\n\n[[photo: only.jpg]]\n\n[[photo: other.jpg | gallery]]");

        // Act
        var (scanResult, renderResult) = await ScanAndRenderAsync(project, new LuminaTheme());

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var storyHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "story", "index.html"));
        var storyHrefs = ExtractPhotoHrefs(storyHtml);
        Assert.IsTrue(
            storyHrefs.Any(href => href.EndsWith($"photo/only/#ctx-{StoryContextId}.photo-1", StringComparison.Ordinal)),
            string.Join(", ", storyHrefs));
        Assert.IsTrue(
            storyHrefs.Any(href => href.EndsWith($"photo/other/#ctx-{StoryContextId}.photo-2", StringComparison.Ordinal)),
            "Without any gallery context, '| gallery' falls back to the page context.");
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "photo", "only", "index.html")));
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "photo", "other", "index.html")));
        Assert.IsFalse(File.Exists(Path.Combine(project.OutputPath, "photo", "story", "local", "index.html")),
            "Lightbox gallery photos still get no photo page.");

        // index + story + two photo pages
        Assert.AreEqual(4, renderResult.PageCount);
        var sitemap = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "sitemap.xml"));
        Assert.Contains("https://example.com/photo/only/", sitemap);
        Assert.Contains("https://example.com/photo/other/", sitemap);
        Assert.DoesNotContain("photo/story/local/", sitemap);
    }

    [TestMethod]
    public async Task GeneratePages_ThemeWithoutPhotoFigure_RendersLinkedContentImage()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Photo Token Fallback", author = "Test" })
            .AddGallery("Years", g => g.AddRealImage("fence.jpg", 1920, 1080))
            .AddGallery("Story", g => g.AddRealImage("local.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Story", "_index.revela"),
            "Intro.\n\n[[photo: Years/fence.jpg]]");

        // Act
        var (scanResult, renderResult) = await ScanAndRenderAsync(
            project,
            new ThemeWithoutFile("Partials/PhotoFigure.revela"));

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        var storyHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "story", "index.html"));
        Assert.DoesNotContain("photo-figure", storyHtml);
        var linkStart = storyHtml.IndexOf($"#ctx-{StoryContextId}.photo-1\"", StringComparison.Ordinal);
        Assert.IsGreaterThan(0, linkStart);
        var pictureStart = storyHtml.IndexOf("<picture class=\"content-image\"", linkStart, StringComparison.Ordinal);
        var linkEnd = storyHtml.IndexOf("</a>", linkStart, StringComparison.Ordinal);
        Assert.IsGreaterThan(linkStart, pictureStart);
        Assert.IsLessThan(linkEnd, pictureStart, "The content image must be wrapped in the photo-page link.");
        Assert.Contains($"id=\"photo-1-photo-i-{FenceSlugId}\"", storyHtml);
    }

    [TestMethod]
    public async Task GeneratePages_ThemeWithoutPhotoFigure_UntitledPhotoGetsEmptyAltInsteadOfFileName()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Photo Token Alt", author = "Test" })
            .AddGallery("Story", g => g.AddRealImage("img_4711.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Story", "_index.revela"),
            "Intro.\n\n[[photo: img_4711.jpg]]");

        var (scanResult, renderResult) = await ScanAndRenderAsync(
            project,
            new ThemeWithoutFile("Partials/PhotoFigure.revela"));

        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");
        var storyHtml = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "story", "index.html"));
        var pictureStart = storyHtml.IndexOf("<picture class=\"content-image\"", StringComparison.Ordinal);
        Assert.IsGreaterThan(0, pictureStart);
        var picture = storyHtml[pictureStart..storyHtml.IndexOf("</picture>", pictureStart, StringComparison.Ordinal)];
        Assert.Contains("alt=\"\"", picture);
        Assert.DoesNotContain("img_4711", picture.Replace("/img-4711/", "/", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task RenderAsync_ThemeWithoutLayout_FailsWithoutWritingOutput()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Missing Layout", author = "Test" })
            .AddGallery("Photos", g => g.AddRealImage("one.jpg", 800, 600)));

        var (scanResult, renderResult) = await ScanAndRenderAsync(project, new ThemeWithoutFile("Layout.revela"));

        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsFalse(renderResult.Success);
        Assert.IsNotNull(renderResult.ErrorMessage);
        Assert.Contains("layout template", renderResult.ErrorMessage, StringComparison.Ordinal);
        Assert.IsFalse(File.Exists(Path.Combine(project.OutputPath, "index.html")));
    }

    private static async Task<(ContentResult Scan, RenderResult Render)> ScanAndRenderAsync(TestProject project, ITheme theme)
    {
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton(theme);
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();
        return (scanResult, renderResult);
    }

    [TestMethod]
    public async Task GeneratePages_PhotoPageCollidingSlugs_PreservesMembershipsAndResolvesEveryViewerLink()
    {
        var galleries = new[]
        {
            (Route: string.Empty, ContextId: "r"),
            (Route: "home/", ContextId: "g-home"),
            (Route: "a/b/", ContextId: "g-a_b"),
            (Route: "a-b/", ContextId: "g-a-b"),
            (Route: "r-grid-1/", ContextId: "g-r-grid-1"),
            (Route: "home-grid-1/", ContextId: "g-home-grid-1")
        };
        using var project = TestProject.Create(builder =>
        {
            builder.WithSiteJson(new { title = "Distinct Viewer IDs", author = "Test" });
            foreach (var (route, _) in galleries)
            {
                builder.AddGallery(route);
            }
        });
        TestImageGenerator.CreateJpeg(Path.Combine(project.SourcePath, "_images", "a", "b.jpg"), 800, 600);
        TestImageGenerator.CreateJpeg(Path.Combine(project.SourcePath, "_images", "a-b.jpg"), 800, 600);
        foreach (var (route, _) in galleries)
        {
            await File.WriteAllTextAsync(Path.Combine(project.SourcePath, route, "_index.revela"),
                "[[gallery: all | sort filename asc]]\n\n[[gallery: all | sort filename asc]]");
        }

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var scanResult = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        Assert.AreEqual(2, scanResult.ImageCount);
        var renderResult = await host.Services.GetRequiredService<IRenderService>().RenderAsync();
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);

        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        var photoRoutes = new[] { "photo/a-b/", "photo/a/b/" };
        foreach (var route in galleries.Select(gallery => gallery.Route).Concat(photoRoutes))
        {
            var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, route, "index.html"));
            pages.Add(route, html);
            var ids = ExtractAttributeValues(html, " id=\"");
            Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.Ordinal).Count(),
                $"Generated page '{route}' contains duplicate IDs.");
        }

        var expectedContextIds = galleries.SelectMany(gallery => new[]
        {
            $"ctx-{gallery.ContextId}.grid-1", $"ctx-{gallery.ContextId}.grid-2"
        }).ToArray();
        var expectedAnchors = new[]
        {
            "grid-1-photo-i-a-b", "grid-1-photo-i-a_b",
            "grid-2-photo-i-a-b", "grid-2-photo-i-a_b"
        };
        foreach (var (route, contextId) in galleries)
        {
            var html = pages[route];
            CollectionAssert.AreEqual(expectedAnchors, ExtractAttributeValues(html, "<article id=\"").ToArray());
            var hrefs = ExtractPhotoHrefs(html);
            Assert.HasCount(4, hrefs);
            CollectionAssert.AreEqual(new[]
            {
                $"/photo/a-b/#ctx-{contextId}.grid-1", $"/photo/a/b/#ctx-{contextId}.grid-1",
                $"/photo/a-b/#ctx-{contextId}.grid-2", $"/photo/a/b/#ctx-{contextId}.grid-2"
            }, hrefs.Select(href =>
            {
                var target = ResolveViewerHref(route, href);
                return target.PathAndQuery + target.Fragment;
            }).ToArray());
        }

        foreach (var photoRoute in photoRoutes)
        {
            var html = pages[photoRoute];
            CollectionAssert.AreEquivalent(expectedContextIds, ExtractAttributeValues(html, "<div id=\"").ToArray());
            var upLinks = ExtractAttributeValues(html, "data-photo-return rel=\"up\" href=\"");
            Assert.HasCount(12, upLinks);
            CollectionAssert.AreEquivalent(galleries.SelectMany(gallery => new[] { gallery.Route, gallery.Route }).ToArray(),
                upLinks.Select(href => ResolveViewerHref(photoRoute, href).AbsolutePath.TrimStart('/')).ToArray());
            var previousLinks = ExtractAttributeValues(html, "data-photo-previous rel=\"prev\" href=\"");
            var nextLinks = ExtractAttributeValues(html, "data-photo-next rel=\"next\" href=\"");
            Assert.HasCount(photoRoute == photoRoutes[0] ? 0 : 12, previousLinks);
            Assert.HasCount(photoRoute == photoRoutes[0] ? 12 : 0, nextLinks);
            var neighborRoute = photoRoute == photoRoutes[0] ? "/photo/a/b/" : "/photo/a-b/";
            Assert.IsTrue(previousLinks.Concat(nextLinks).All(href =>
                ResolveViewerHref(photoRoute, href).AbsolutePath.Equals(neighborRoute, StringComparison.Ordinal)));
        }

        foreach (var (route, html) in pages)
        {
            foreach (var href in ExtractAttributeValues(html, "href=\""))
            {
                var target = ResolveViewerHref(route, href);
                if (target.Fragment.Length == 0)
                {
                    continue;
                }

                Assert.IsTrue(pages.TryGetValue(target.AbsolutePath.TrimStart('/'), out var targetHtml),
                    $"Viewer link '{href}' from '{route}' has no generated page.");
                Assert.AreEqual(1, ExtractAttributeValues(targetHtml, " id=\"").Count(id =>
                    id.Equals(target.Fragment[1..], StringComparison.Ordinal)),
                    $"Viewer link '{href}' from '{route}' must resolve to exactly one ID.");
            }
        }
    }

    private static Uri ResolveViewerHref(string route, string href) =>
        new(new Uri($"https://revela.test/{route}"), href);

    [TestMethod]
    public async Task GeneratePages_CustomBodyInlineGallery_CreatesLinkedPhotoPage()
    {
        // Arrange
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Custom Page", author = "Test" })
            .AddGallery("Custom Page", g => g.AddRealImage("first.jpg", 1920, 1080)));

        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Custom Page", "_index.revela"),
            "+++\ntemplate = \"page\"\n+++\n\n[[gallery]]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var theme = host.Services.GetRequiredService<ITheme>();

        // Act
        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        // Assert
        Assert.IsTrue(scanResult.Success, $"Scan failed: {scanResult.ErrorMessage}");
        Assert.IsTrue(renderResult.Success, $"Render failed: {renderResult.ErrorMessage}");

        var pageHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "custom-page", "index.html"));
        Assert.Contains("<section class=\"gallery\">", pageHtml);
        Assert.Contains("<picture>", pageHtml);

        var photoHrefs = ExtractPhotoHrefs(pageHtml);
        foreach (var photoHref in photoHrefs)
        {
            var route = photoHref.Split('#')[0].TrimStart('/');
            var targetPath = Path.GetFullPath(Path.Combine(
                project.OutputPath,
                "custom-page",
                route.Replace('/', Path.DirectorySeparatorChar),
                "index.html"));
            Assert.IsTrue(File.Exists(targetPath), $"Photo link '{photoHref}' has no generated target.");
        }

        Assert.HasCount(1, photoHrefs);
        Assert.IsTrue(File.Exists(
            Path.Combine(project.OutputPath, "photo", "custom-page", "first", "index.html")));
    }

    [TestMethod]
    public async Task GeneratePages_CustomBodyWithoutInlineGallery_CreatesNoPhotoPage()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Custom Page", author = "Test" })
            .AddGallery("Custom Page", g => g.AddRealImage("first.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Custom Page", "_index.revela"),
            "+++\ntemplate = \"page\"\n+++\n\nNo inline gallery.");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
        var pageHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "custom-page", "index.html"));
        Assert.IsEmpty(ExtractPhotoHrefs(pageHtml));
        Assert.IsFalse(File.Exists(
            Path.Combine(project.OutputPath, "photo", "custom-page", "first", "index.html")));
    }

    [TestMethod]
    public async Task GeneratePages_PhotoViewerNone_DefaultAndCustomInlineRenderStaticPictures()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "No Viewer", author = "Test" })
            .AddGallery("Default", g => g.AddRealImage("first.jpg", 1920, 1080))
            .AddGallery("Custom", g => g.AddRealImage("second.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Default", "_index.revela"),
            "+++\nphoto_viewer = \"none\"\n+++\n\n[[gallery]]");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Custom", "_index.revela"),
            "+++\ntemplate = \"page\"\nphoto_viewer = \"none\"\n+++\n\n[[gallery]]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
        foreach (var gallerySlug in new[] { "default", "custom" })
        {
            var html = await File.ReadAllTextAsync(
                Path.Combine(project.OutputPath, gallerySlug, "index.html"));
            Assert.Contains("<picture>", html);
            Assert.IsEmpty(ExtractPhotoHrefs(html));
            Assert.DoesNotContain("aria-haspopup=\"dialog\"", html);
            Assert.DoesNotContain("<dialog", html);
            Assert.DoesNotContain("_assets/lightbox.js", html);
            Assert.DoesNotContain("_assets/zoom.js", html);
        }

        Assert.IsFalse(Directory.Exists(Path.Combine(project.OutputPath, "photo")));
    }

    [TestMethod]
    public async Task GeneratePages_PhotoViewerLightbox_RendersProgressiveDialogsWithoutPhotoPages()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Lightbox Viewer", author = "Test" })
            .AddGallery("Default", g => g
                .AddRealImage("first.jpg", 1920, 1080, exif => exif.WithIso(200).WithDescription("Evening light"))
                .AddRealImage("second.jpg", 1920, 1080, exif => exif.WithIso(200))
                .AddRealImage("third.jpg", 1920, 1080, exif => exif.WithIso(200)))
            .AddGallery("Custom", g => g.AddRealImage("custom.jpg", 1920, 1080, exif => exif.WithIso(200))));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Default", "_index.revela"),
            "+++\nphoto_viewer = \"lightbox\"\n+++\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Custom", "_index.revela"),
            "+++\ntemplate = \"page\"\nphoto_viewer = \"lightbox\"\n+++\n\n[[gallery]]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
        foreach (var gallerySlug in new[] { "default", "custom" })
        {
            var html = await File.ReadAllTextAsync(
                Path.Combine(project.OutputPath, gallerySlug, "index.html"));
            var normalizedHtml = NormalizeLineEndings(html);
            var expectedCount = gallerySlug == "default" ? 3 : 1;
            var dialogIds = ExtractAttributeValues(html, "<dialog id=\"");
            var targets = ExtractAttributeValues(html, "data-lightbox-target=\"");
            var commandTargets = ExtractAttributeValues(html, "commandfor=\"");
            var controls = ExtractAttributeValues(html, "aria-controls=\"");
            Assert.AreEqual(expectedCount, CountOccurrences(html, "type=\"button\" aria-haspopup=\"dialog\""));
            Assert.AreEqual(expectedCount, CountOccurrences(html, "class=\"photo-lightbox\" data-lightbox"));
            Assert.AreEqual((expectedCount * 2) - 2, CountOccurrences(html, "data-lightbox-target=\"lightbox-"));
            Assert.AreEqual((expectedCount * 2) - 2, CountOccurrences(html, "type=\"button\" hidden aria-controls=\"lightbox-"));
            Assert.AreEqual(expectedCount, CountOccurrences(html, "command=\"show-modal\""));
            Assert.AreEqual(expectedCount, CountOccurrences(html, "command=\"close\""));
            Assert.DoesNotContain("hidden></button>", html);
            Assert.AreEqual(expectedCount, dialogIds.Distinct().Count());
            Assert.AreEqual(targets.Count + expectedCount, controls.Count);
            Assert.IsTrue(targets.All(dialogIds.Contains), "Every lightbox target must resolve to a dialog.");
            Assert.IsTrue(commandTargets.All(dialogIds.Contains), "Every declarative dialog command must resolve to a dialog.");
            Assert.IsTrue(controls.All(dialogIds.Contains), "Every aria-controls value must resolve to a dialog.");
            Assert.AreEqual(expectedCount, CountOccurrences(normalizedHtml,
                "command=\"close\" aria-label=\"Close photo\">&times;</button>\n    <article style="));
            Assert.AreEqual(expectedCount, CountOccurrences(html, "<picture data-lqip>"));
            Assert.DoesNotContain("sizes=\"100vw\"", html);
            Assert.AreEqual(expectedCount, CountOccurrences(normalizedHtml,
                "</picture>\n        <section>\n            <nav aria-label=\"Photo navigation\">"));
            Assert.AreEqual(expectedCount, CountOccurrences(html, "<aside class=\"photo-metadata\">"));
            Assert.AreEqual(expectedCount, CountOccurrences(normalizedHtml,
                "<footer>\n                <strong>Seen in:</strong>"));
            Assert.AreEqual(expectedCount, CountOccurrences(html, "loading=\"lazy\" fetchpriority=\"low\" decoding=\"async\""));
            Assert.AreEqual(expectedCount,
                CountOccurrences(html, $">{char.ToUpperInvariant(gallerySlug[0])}{gallerySlug[1..]}</span>"));
            AssertRetiredPhotoClassesAreAbsent(html);
            Assert.DoesNotContain("photo-lightbox-stage", html);
            Assert.DoesNotContain("photo-metadata-file", html);
            Assert.IsEmpty(ExtractPhotoHrefs(html));
            Assert.Contains("_assets/lightbox.js", html);
            Assert.Contains("_assets/zoom.js", html);
            var expectedDisplayLabel = gallerySlug == "default" ? "Evening light" : "custom";
            Assert.Contains($">{expectedDisplayLabel}</span>", html);
        }

        var defaultHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "default", "index.html"));
        Assert.AreEqual(2, CountOccurrences(defaultHtml, "aria-label=\"Previous photo\""));
        Assert.AreEqual(2, CountOccurrences(defaultHtml, "aria-label=\"Next photo\""));
        Assert.AreEqual(1, CountOccurrences(defaultHtml, "<span data-photo-previous aria-hidden=\"true\"></span>"));
        Assert.AreEqual(1, CountOccurrences(defaultHtml, "<span data-photo-next aria-hidden=\"true\"></span>"));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.OutputPath, "photo")));
    }

    [TestMethod]
    public async Task GeneratePages_PhotoViewerLightboxRepeatedBareInlineGallery_UsesUniqueLocalNavigationIds()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Repeated Lightbox Viewer", author = "Test" })
            .AddGallery("Repeated", g => g
                .AddRealImage("first.jpg", 1920, 1080)
                .AddRealImage("second.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Repeated", "_index.revela"),
            "+++\ntemplate = \"page\"\nphoto_viewer = \"lightbox\"\n+++\n\n[[gallery]]\n\n[[gallery]]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
        var html = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "repeated", "index.html"));
        var ids = ExtractAttributeValues(html, " id=\"");
        var dialogIds = ExtractAttributeValues(html, "<dialog id=\"");
        var targets = ExtractAttributeValues(html, "data-lightbox-target=\"");
        var commandTargets = ExtractAttributeValues(html, "commandfor=\"");

        Assert.AreEqual(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.HasCount(4, dialogIds);
        Assert.IsTrue(targets.All(dialogIds.Contains), "Every trigger and navigation target must resolve to a dialog.");
        Assert.IsTrue(commandTargets.All(dialogIds.Contains), "Every declarative dialog command must resolve to a dialog.");
        Assert.AreEqual(2, targets.Count(target => target.StartsWith("lightbox-photo-i-", StringComparison.Ordinal)));
        Assert.AreEqual(2, targets.Count(target => target.StartsWith("lightbox-bare-2-", StringComparison.Ordinal)));
        Assert.AreEqual(4, commandTargets.Count(target => target.StartsWith("lightbox-photo-i-", StringComparison.Ordinal)));
        Assert.AreEqual(4, commandTargets.Count(target => target.StartsWith("lightbox-bare-2-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task GeneratePages_PhotoViewerMixedModes_PhotoPageContainsOnlyPageContext()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Mixed Viewer", author = "Test" })
            .AddGallery("Page Viewer", g => g.AddRealImage("local.jpg", 1920, 1080))
            .AddGallery("None Viewer", g => g.AddRealImage("local.jpg", 1920, 1080)));
        TestImageGenerator.CreateJpeg(
            Path.Combine(project.SourcePath, "_images", "shared.jpg"),
            1920,
            1080);
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Page Viewer", "_index.revela"),
            "[[gallery: filename == 'shared.jpg']]");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "None Viewer", "_index.revela"),
            "+++\nphoto_viewer = \"none\"\n+++\n\n[[gallery: filename == 'shared.jpg']]");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();

        var scanResult = await contentService.ScanAsync();
        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);
        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
        var pageGalleryHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "page-viewer", "index.html"));
        var noneGalleryHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "none-viewer", "index.html"));
        Assert.HasCount(1, ExtractPhotoHrefs(pageGalleryHtml));
        Assert.IsEmpty(ExtractPhotoHrefs(noneGalleryHtml));

        var photoHtml = await File.ReadAllTextAsync(
            Path.Combine(project.OutputPath, "photo", "shared", "index.html"));
        Assert.Contains("id=\"ctx-g-page-viewer.grid-1\"", photoHtml);
        Assert.DoesNotContain("ctx-g-none-viewer.grid-1", photoHtml);
    }

    [TestMethod]
    public async Task GeneratePages_ThemeWithoutGalleryGrid_OnlyFailsWhenTokenIsPresent()
    {
        // Arrange: first prove a no-token page remains unaffected. Lumina's own gallery body
        // renders its grid through GalleryGrid, so this page uses the text page body.
        using var noTokenProject = TestProject.Create(p => p
            .WithSiteJson(new { title = "No Token", author = "Test" })
            .AddGallery("Gallery", g => g.AddRealImage("photo.jpg", 1920, 1080)));
        await File.WriteAllTextAsync(
            Path.Combine(noTokenProject.SourcePath, "Gallery", "_index.revela"),
            "+++\ntemplate = \"page\"\n+++\nBody without an inline gallery.");

        using var noTokenHost = RevelaTestHost.Build(noTokenProject.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new ThemeWithoutGalleryGrid());
        });
        var noTokenContentService = noTokenHost.Services.GetRequiredService<IContentService>();
        var noTokenRenderService = noTokenHost.Services.GetRequiredService<IRenderService>();
        await noTokenContentService.ScanAsync();

        // Act
        var noTokenResult = await noTokenRenderService.RenderAsync();

        // Assert
        Assert.IsTrue(noTokenResult.Success, noTokenResult.ErrorMessage);

        // Arrange: the same theme with an inline token must require the missing partial.
        using var tokenProject = TestProject.Create(p => p
            .WithSiteJson(new { title = "With Token", author = "Test" })
            .AddGallery("Gallery", g => g.AddRealImage("photo.jpg", 1920, 1080)));
        var sourcePath = Path.Combine(tokenProject.SourcePath, "Gallery", "_index.revela");
        await File.WriteAllTextAsync(sourcePath, "Before.\n\n[[gallery]]");

        using var tokenHost = RevelaTestHost.Build(tokenProject.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new ThemeWithoutGalleryGrid());
        });
        var tokenContentService = tokenHost.Services.GetRequiredService<IContentService>();
        var tokenRenderService = tokenHost.Services.GetRequiredService<IRenderService>();
        await tokenContentService.ScanAsync();

        // Act
        var tokenResult = await tokenRenderService.RenderAsync();

        // Assert
        Assert.IsFalse(tokenResult.Success);
        Assert.IsNotNull(tokenResult.ErrorMessage);
        Assert.Contains($"{sourcePath}:3:", tokenResult.ErrorMessage);
        Assert.Contains("Partials/GalleryGrid.revela", tokenResult.ErrorMessage);
    }

    [TestMethod]
    public async Task RenderAsync_ProgressReports_CountPhotoPagesAndReachOneHundredPercent()
    {
        // Arrange: one gallery with multiple real images so the render produces photo pages (#77),
        // which dominate the output. The progress bar must count them — not just the galleries.
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Progress" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Progress", author = "Test" })
            .AddGallery("Photos", g => g
                .AddRealImage("one.jpg", 1280, 720)
                .AddRealImage("two.jpg", 1280, 720)
                .AddRealImage("three.jpg", 1280, 720)));

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var themePlugin = host.Services.GetRequiredService<ITheme>();

        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, $"Scan should succeed: {scanResult.ErrorMessage}");

        var recorder = new RecordingProgress();

        // Act
        var renderResult = await renderService.RenderAsync(recorder);

        // Assert: index + 1 gallery + 3 photo pages = 5 (photo pages are the majority of output).
        Assert.IsTrue(renderResult.Success, $"Render should succeed: {renderResult.ErrorMessage}");
        Assert.AreEqual(5, renderResult.PageCount, "index + 1 gallery + 3 photo pages");
        var reports = recorder.Reports;
        Assert.IsTrue(reports.Count > 0, "Render must report progress");

        // Every reported Total must equal the REAL total (galleries + photo pages + index),
        // never the gallery-only count that pinned the bar at 100% too early.
        var distinctTotals = reports.Select(r => r.Total).Distinct().ToList();
        Assert.HasCount(1, distinctTotals);
        Assert.AreEqual(renderResult.PageCount, distinctTotals[0],
            "Reported Total must match the final page count, not the gallery count");

        // Photo-page rendering DOES report progress: the bar reaches 100% only at the end.
        var maxRendered = reports.Max(r => r.Rendered);
        Assert.AreEqual(renderResult.PageCount, maxRendered,
            "Progress must reach the total (photo pages report progress too)");

        // The bar must never overshoot.
        Assert.IsTrue(reports.All(r => r.Rendered <= r.Total),
            "Rendered must never exceed Total");
    }

    [TestMethod]
    [DataRow(PhotoViewerMode.Lightbox)]
    [DataRow(PhotoViewerMode.None)]
    public async Task RenderAsync_ThemeWithoutPhotoTemplateWithoutPageSupport_RendersWithoutPhotoWarning(
        PhotoViewerMode viewerMode)
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Non-Page Viewer" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Non-Page Viewer", author = "Test" })
            .AddGallery("Photos", gallery => gallery.AddRealImage("one.jpg", 1280, 720)));
        var resolverLogger = new RecordingTemplateLogger();

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new ThemeWithoutPhotoTemplate(viewerMode));
            services.AddSingleton<ILogger<TemplateResolver>>(resolverLogger);
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);

        var renderResult = await renderService.RenderAsync();

        Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
        Assert.AreEqual(2, renderResult.PageCount);
        Assert.IsTrue(File.Exists(Path.Combine(project.OutputPath, "index.html")));
        var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "photos", "index.html"));
        Assert.Contains("<picture", html);
        Assert.AreEqual(viewerMode is PhotoViewerMode.Lightbox, html.Contains("<dialog", StringComparison.Ordinal));
        Assert.IsEmpty(ExtractPhotoHrefs(html));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.OutputPath, "photo")));
        Assert.AreEqual(0, resolverLogger.MissingPhotoTemplateWarnings,
            "A theme without page support must not probe the missing photo template.");
    }

    [TestMethod]
    [DataRow("page")]
    [DataRow("none")]
    public async Task RenderAsync_ThemeWithoutPhotoTemplateSupportingPage_FailsBeforeHtmlWrite(string viewerMode)
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "Missing Photo Template" },
                theme = new { name = "Lumina" }
            })
            .WithSiteJson(new { title = "Missing Photo Template", author = "Test" })
            .AddGallery("Photos", gallery => gallery.AddRealImage("one.jpg", 1280, 720)));
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "_index.revela"),
            $"+++\nphoto_viewer = \"{viewerMode}\"\n+++\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Photos", "_index.revela"),
            $"+++\nphoto_viewer = \"{viewerMode}\"\n+++\n");
        var resolverLogger = new RecordingTemplateLogger();

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new ThemeWithoutPhotoTemplate());
            services.AddSingleton<ILogger<TemplateResolver>>(resolverLogger);
        });

        var contentService = host.Services.GetRequiredService<IContentService>();
        var renderService = host.Services.GetRequiredService<IRenderService>();
        var scanResult = await contentService.ScanAsync();
        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);

        var renderResult = await renderService.RenderAsync();

        Assert.IsFalse(renderResult.Success);
        Assert.IsNotNull(renderResult.ErrorMessage);
        Assert.Contains("Body/Photo.revela", renderResult.ErrorMessage, StringComparison.Ordinal);
        Assert.AreEqual(1, resolverLogger.MissingPhotoTemplateWarnings);
        Assert.IsFalse(Directory.Exists(project.OutputPath));
    }

    private sealed class RecordingTemplateLogger : ILogger<TemplateResolver>
    {
        private int missingPhotoTemplateWarnings;

        public int MissingPhotoTemplateWarnings => Volatile.Read(ref missingPhotoTemplateWarnings);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel is LogLevel.Warning
                && state is IReadOnlyList<KeyValuePair<string, object?>> properties
                && properties.Any(property =>
                    property.Key.Equals("Key", StringComparison.Ordinal)
                    && property.Value is string key
                    && key.Equals("body/photo", StringComparison.Ordinal)))
            {
                Interlocked.Increment(ref missingPhotoTemplateWarnings);
            }
        }
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var startIndex = 0;

        while ((startIndex = value.IndexOf(search, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += search.Length;
        }

        return count;
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void AssertRetiredPhotoClassesAreAbsent(string html)
    {
        var retiredClasses = new[]
        {
            "photo-article",
            "photo-picture",
            "photo-sheet",
            "photo-nav-context",
            "primary",
            "photo-prev",
            "photo-up",
            "photo-next",
            "photo-tags",
            "photo-tags-label",
            "photo-tag-context",
            "photo-metadata-line",
            "photo-metadata-exposure",
            "photo-metadata-equipment",
            "lightbox-trigger",
            "photo-lightbox-close",
            "photo-lightbox-article",
            "photo-lightbox-controls",
            "photo-lightbox-prev",
            "photo-lightbox-next",
            "photo-lightbox-context"
        };

        foreach (var retiredClass in retiredClasses)
        {
            Assert.DoesNotContain($"class=\"{retiredClass}", html);
        }
    }

    [TestMethod]
    public async Task RenderAsync_PageFilterSortRandom_ShufflesOnEveryRenderLikeInlineGalleries()
    {
        using var project = TestProject.Create(p => p
            .WithSiteJson(new { title = "Random", author = "Test" })
            .AddGallery("Picks"));
        for (var index = 0; index < 8; index++)
        {
            TestImageGenerator.CreateJpeg(
                Path.Combine(project.SourcePath, "_images", $"pick-{index}.jpg"), 64, 48);
        }

        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Picks", "_index.revela"),
            "+++\nfilter = \"all | sort random\"\n+++\n");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var scanResult = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        Assert.IsTrue(scanResult.Success, scanResult.ErrorMessage);

        // 8! orders: four renders of one scan in the same order would mean the order was frozen at scan.
        var orders = new List<string>();
        for (var render = 0; render < 4; render++)
        {
            var renderResult = await host.Services.GetRequiredService<IRenderService>().RenderAsync();
            Assert.IsTrue(renderResult.Success, renderResult.ErrorMessage);
            var html = await File.ReadAllTextAsync(Path.Combine(project.OutputPath, "picks", "index.html"));
            var order = ExtractPhotoHrefs(html);
            Assert.HasCount(8, order);
            orders.Add(string.Join(',', order));
        }

        Assert.IsGreaterThan(1, orders.Distinct(StringComparer.Ordinal).Count());
    }

    private static IReadOnlyList<string> ExtractPhotoHrefs(string html)
    {
        const string hrefPrefix = "href=\"";
        var hrefs = new List<string>();
        var searchIndex = 0;

        while ((searchIndex = html.IndexOf(hrefPrefix, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            var valueStart = searchIndex + hrefPrefix.Length;
            var valueEnd = html.IndexOf('"', valueStart);
            if (valueEnd < 0)
            {
                break;
            }

            var href = html[valueStart..valueEnd];
            if (href.Contains("photo/", StringComparison.Ordinal))
            {
                hrefs.Add(href);
            }

            searchIndex = valueEnd + 1;
        }

        return hrefs;
    }

    private static IReadOnlyList<string> ExtractAttributeValues(string html, string prefix)
    {
        var values = new List<string>();
        var searchIndex = 0;

        while ((searchIndex = html.IndexOf(prefix, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            var valueStart = searchIndex + prefix.Length;
            var valueEnd = html.IndexOf('"', valueStart);
            if (valueEnd < 0)
            {
                break;
            }

            values.Add(html[valueStart..valueEnd]);
            searchIndex = valueEnd + 1;
        }

        return values;
    }

    private sealed class RecordingProgress : IProgress<RenderProgress>
    {
        private readonly Lock gate = new();
        private readonly List<RenderProgress> reports = [];

        // Photo pages report from Parallel.ForEachAsync, so Report is called concurrently.
        public IReadOnlyList<RenderProgress> Reports
        {
            get
            {
                lock (gate)
                {
                    return [.. reports];
                }
            }
        }

        public void Report(RenderProgress value)
        {
            lock (gate)
            {
                reports.Add(value);
            }
        }
    }

    private sealed class ThemeWithoutGalleryGrid : ITheme
    {
        private const string GalleryGridPath = "Partials/GalleryGrid.revela";
        private readonly LuminaTheme inner = new();

        public PackageMetadata Metadata => inner.Metadata;

        public string? Prefix => inner.Prefix;

        public string? TargetTheme => inner.TargetTheme;

        public ThemeManifest Manifest => inner.Manifest;

        public Stream? GetFile(string relativePath) =>
            IsGalleryGrid(relativePath) ? null : inner.GetFile(relativePath);

        public IEnumerable<string> GetAllFiles() =>
            inner.GetAllFiles().Where(file => !IsGalleryGrid(file));

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            inner.ExtractToAsync(targetDirectory, cancellationToken);

        public Stream? GetSiteTemplate() => inner.GetSiteTemplate();

        public Stream? GetImagesTemplate() => inner.GetImagesTemplate();

        public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey) =>
            inner.GetTemplateDataDefaults(templateKey);

        private static bool IsGalleryGrid(string path) =>
            path.Replace('\\', '/').Equals(GalleryGridPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThemeWithoutFile(string removedPath) : ITheme
    {
        private readonly LuminaTheme inner = new();

        public PackageMetadata Metadata => inner.Metadata;

        public string? Prefix => inner.Prefix;

        public string? TargetTheme => inner.TargetTheme;

        public ThemeManifest Manifest => inner.Manifest;

        public Stream? GetFile(string relativePath) =>
            IsRemoved(relativePath) ? null : inner.GetFile(relativePath);

        public IEnumerable<string> GetAllFiles() =>
            inner.GetAllFiles().Where(file => !IsRemoved(file));

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            inner.ExtractToAsync(targetDirectory, cancellationToken);

        public Stream? GetSiteTemplate() => inner.GetSiteTemplate();

        public Stream? GetImagesTemplate() => inner.GetImagesTemplate();

        public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey) =>
            inner.GetTemplateDataDefaults(templateKey);

        private bool IsRemoved(string path) =>
            path.Replace('\\', '/').Equals(removedPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ThemeWithoutPhotoTemplate : ITheme
    {
        private const string PhotoTemplatePath = "Body/Photo.revela";
        private readonly LuminaTheme inner = new();

        public ThemeWithoutPhotoTemplate(PhotoViewerMode? supportedMode = null)
        {
            var manifest = inner.Manifest;
            Manifest = supportedMode is { } mode
                ? new ThemeManifest
                {
                    LayoutTemplate = manifest.LayoutTemplate,
                    PhotoViewer = new PhotoViewerCapabilities { Supported = [mode], Default = mode },
                    Stylesheets = manifest.Stylesheets,
                    Scripts = manifest.Scripts
                }
                : manifest;
        }

        public PackageMetadata Metadata => inner.Metadata;

        public string? Prefix => inner.Prefix;

        public string? TargetTheme => inner.TargetTheme;

        public ThemeManifest Manifest { get; }

        public Stream? GetFile(string relativePath) =>
            IsPhotoTemplate(relativePath) ? null : inner.GetFile(relativePath);

        public IEnumerable<string> GetAllFiles() =>
            inner.GetAllFiles().Where(file => !IsPhotoTemplate(file));

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            inner.ExtractToAsync(targetDirectory, cancellationToken);

        public Stream? GetSiteTemplate() => inner.GetSiteTemplate();

        public Stream? GetImagesTemplate() => inner.GetImagesTemplate();

        public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey) =>
            inner.GetTemplateDataDefaults(templateKey);

        private static bool IsPhotoTemplate(string path) =>
            path.Replace('\\', '/').Equals(PhotoTemplatePath, StringComparison.OrdinalIgnoreCase);
    }
}

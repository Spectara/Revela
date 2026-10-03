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
/// End-to-end checks for the SEO-relevant document structure Lumina renders:
/// headings, canonical links, Open Graph metadata, picture markup and the 404 page.
/// </summary>
[TestClass]
[TestCategory("E2E")]
public sealed partial class LuminaSeoEndToEndTests
{
    private const string BaseUrl = "https://photos.example.com";

    [TestMethod]
    public async Task RenderAsync_LuminaPages_RenderExactlyOneH1()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl);

        foreach (var (page, html) in site.Pages)
        {
            Assert.AreEqual(1, H1Pattern().Count(html), $"{page} must contain exactly one <h1>.");
        }

        Assert.Contains("<h1 class=\"visually-hidden\">Home &amp; More</h1>", site.Pages["index.html"]);
        var island = site.Pages["island/index.html"];
        Assert.Contains("<h1>Island</h1>", island);
        Assert.IsLessThan(
            island.IndexOf("Island intro", StringComparison.Ordinal),
            island.IndexOf("<h1>Island</h1>", StringComparison.Ordinal),
            "The gallery heading must precede the gallery body content.");
        Assert.DoesNotContain("<h2>Island</h2>", island);
        var notes = site.Pages["notes/index.html"];
        Assert.Contains("<h1 id=\"own-heading\">Own heading</h1>", notes);
        Assert.DoesNotContain("<h1>Notes</h1>", notes);
        Assert.Contains("<h1>About</h1>", site.Pages["about/index.html"]);
        Assert.Contains("<h1 class=\"visually-hidden\">Photo one</h1>", site.Pages["photo/island/one/index.html"]);
    }

    [TestMethod]
    public async Task RenderAsync_LuminaPages_HeadingLevelsNeverSkip()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl);

        foreach (var (page, html) in site.Pages)
        {
            HeadingOrderAssert.Sequential(page, html);
        }
    }

    [TestMethod]
    public async Task RenderAsync_LuminaPages_LabelEveryNavigationLandmarkDistinctly()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl, language: "de");

        foreach (var (page, html) in site.Pages)
        {
            var navs = NavPattern().Matches(html).Select(match => match.Value).ToList();
            var labels = navs
                .Select(nav => AriaLabelPattern().Match(nav))
                .Where(match => match.Success)
                .Select(match => match.Groups["label"].Value)
                .ToList();

            Assert.HasCount(navs.Count, labels, $"{page}: every <nav> needs an aria-label.");
            Assert.HasCount(labels.Count, labels.Distinct(StringComparer.Ordinal), $"{page}: <nav> labels must be unique.");
        }

        var home = site.Pages["index.html"];
        Assert.Contains("<nav id=\"site-menu\" popover=\"auto\" aria-label=\"Menü\">", home);
        Assert.Contains("<nav aria-label=\"Hauptnavigation\">", home);
        Assert.Contains("<nav aria-label=\"Seitenübersicht\">", home);
    }

    [TestMethod]
    public async Task RenderAsync_WithBaseUrl_EmitsAbsoluteCanonicalOnEveryIndexablePage()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl);

        foreach (var (page, expected) in new[]
        {
            ("index.html", $"{BaseUrl}/"),
            ("island/index.html", $"{BaseUrl}/island/"),
            ("notes/index.html", $"{BaseUrl}/notes/"),
            ("about/index.html", $"{BaseUrl}/about/"),
            ("photo/island/one/index.html", $"{BaseUrl}/photo/island/one/"),
        })
        {
            Assert.AreEqual(expected, AttributeAfter(site.Pages[page], "rel=\"canonical\" href=\""), page);
            Assert.AreEqual(expected, AttributeAfter(site.Pages[page], "property=\"og:url\" content=\""), page);
        }

        Assert.DoesNotContain("rel=\"canonical\"", site.Pages["404.html"]);
    }

    [TestMethod]
    public async Task RenderAsync_WithoutBaseUrl_OmitsCanonicalAndAbsoluteOpenGraphUrls()
    {
        var site = await RenderSiteAsync(baseUrl: null);

        foreach (var (page, html) in site.Pages)
        {
            Assert.DoesNotContain("rel=\"canonical\"", html, StringComparison.Ordinal, page);
            Assert.DoesNotContain("og:url", html, StringComparison.Ordinal, page);
            Assert.DoesNotContain("og:image", html, StringComparison.Ordinal, page);
        }
    }

    [TestMethod]
    public async Task RenderAsync_WithBaseUrl_OpenGraphUsesSocialSizedJpgVariant()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl, language: "de");

        var photo = site.Pages["photo/island/one/index.html"];
        Assert.AreEqual($"{BaseUrl}/images/island/one/1920.jpg", AttributeAfter(photo, "property=\"og:image\" content=\""));
        Assert.AreEqual("1920", AttributeAfter(photo, "property=\"og:image:width\" content=\""));
        Assert.AreEqual("1280", AttributeAfter(photo, "property=\"og:image:height\" content=\""));
        Assert.AreEqual("de_DE", AttributeAfter(photo, "property=\"og:locale\" content=\""));
        Assert.AreEqual("Foto one", AttributeAfter(photo, "property=\"og:title\" content=\""));
        Assert.AreEqual("SEO Site", AttributeAfter(photo, "property=\"og:site_name\" content=\""));
        Assert.Contains("property=\"og:type\" content=\"", photo);

        var gallery = site.Pages["island/index.html"];
        Assert.AreEqual("Island", AttributeAfter(gallery, "property=\"og:title\" content=\""));
        Assert.AreEqual("de_DE", AttributeAfter(gallery, "property=\"og:locale\" content=\""));
        Assert.AreEqual("Island description", AttributeAfter(gallery, "property=\"og:description\" content=\""));
        Assert.AreEqual($"{BaseUrl}/images/island/one/1920.jpg", AttributeAfter(gallery, "property=\"og:image\" content=\""));
    }

    [TestMethod]
    public async Task RenderAsync_LuminaPictures_UseJpegMimeTypeAndResponsiveFallback()
    {
        var site = await RenderSiteAsync(baseUrl: null);

        foreach (var page in new[] { "island/index.html", "notes/index.html", "photo/island/one/index.html" })
        {
            var html = site.Pages[page];
            Assert.DoesNotContain("type=\"image/jpg\"", html, StringComparison.Ordinal, page);
            Assert.Contains("type=\"image/jpeg\"", html, StringComparison.Ordinal, page);
            foreach (Match img in ImgPattern().Matches(html))
            {
                Assert.Contains(" srcset=\"", img.Value, StringComparison.Ordinal, $"{page}: fallback <img> needs a srcset: {img.Value}");
            }
        }
    }

    [TestMethod]
    public async Task RenderAsync_LuminaPictures_SizeEverySourceAndPaintContentImagePlaceholders()
    {
        var site = await RenderSiteAsync(baseUrl: null);

        foreach (var (page, html) in site.Pages)
        {
            foreach (Match source in SourcePattern().Matches(html))
            {
                Assert.Contains(" sizes=\"", source.Value, StringComparison.Ordinal, $"{page}: without sizes a browser assumes 100vw: {source.Value}");
            }
        }

        var notes = site.Pages["notes/index.html"];
        Assert.Contains("data-lqip>", notes);
        Assert.AreEqual(3, CountOccurrences(notes, "sizes=\"auto, (min-width: 900px) 900px, 100vw\""),
            "The content image's two <source> elements and its <img> fit the 900px text column.");
    }

    [TestMethod]
    public async Task RenderAsync_LuminaNavigation_MarksOnlyTheCurrentPage()
    {
        var site = await RenderSiteAsync(baseUrl: null);

        var island = site.Pages["island/index.html"];
        Assert.AreEqual(2, CountOccurrences(island, "aria-current=\"page\">Island</a>"), "Overlay menu and footer navigation.");
        Assert.AreEqual(2, CountOccurrences(island, "aria-current=\"page\""));
        Assert.DoesNotContain("class=\"active\"", island);
    }

    [TestMethod]
    public async Task RenderAsync_GalleryWithoutCover_PreviewsItsFirstShownPhoto()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl);

        Assert.AreEqual($"{BaseUrl}/images/notes/three/800.jpg", AttributeAfter(site.Pages["notes/index.html"], "property=\"og:image\" content=\""));
        Assert.DoesNotContain("og:image", site.Pages["about/index.html"], StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task RenderAsync_HomeTitledLikeTheSite_PrintsTheTitleOnce()
    {
        // An untitled home page falls back to the site title; either way "SEO Site - SEO Site" reads badly.
        var site = await RenderSiteAsync(baseUrl: null, homeTitle: "SEO Site");

        Assert.Contains("<title>SEO Site</title>", site.Pages["index.html"]);
        Assert.Contains("<title>SEO Site - Island</title>", site.Pages["island/index.html"]);
    }

    [TestMethod]
    [DataRow("/")]
    [DataRow("/photos/")]
    public async Task RenderAsync_NotFoundPage_IsLocalizedNoindexAndUsesRootAbsoluteUrls(string basePath)
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl, language: "de", basePath: basePath);

        Assert.IsTrue(site.Pages.TryGetValue("404.html", out var notFound), "404.html must be written to the output root.");
        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", notFound);
        Assert.Contains("Seite nicht gefunden", notFound);
        Assert.MatchesRegex($"href=\"{Regex.Escape(basePath)}_assets/main\\.css\\?v=[0-9a-f]{{8}}\"", notFound);
        Assert.Contains($"href=\"{basePath}island/\"", notFound);
        foreach (var url in UrlAttributePattern().Matches(notFound).Select(match => match.Groups["url"].Value))
        {
            Assert.IsTrue(
                url.StartsWith(basePath, StringComparison.Ordinal) || url.StartsWith("https://", StringComparison.Ordinal),
                $"404.html is served at arbitrary URLs, so '{url}' must be root-absolute.");
        }

        Assert.IsNotNull(site.Sitemap);
        Assert.DoesNotContain("404.html", site.Sitemap, StringComparison.Ordinal);
        Assert.IsFalse(site.Pages.Keys.Any(page => page.Contains("notfound", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task RenderAsync_ThemeWithoutNotFoundTemplate_SkipsNotFoundPage()
    {
        var site = await RenderSiteAsync(baseUrl: BaseUrl, theme: new ThemeWithoutNotFoundTemplate());

        Assert.IsFalse(site.Pages.ContainsKey("404.html"));
        Assert.IsTrue(site.Pages.ContainsKey("index.html"));
    }

    [TestMethod]
    public async Task RenderAsync_StaticNotFoundPage_ReplacesGeneratedPage()
    {
        using var project = TestProject.Create(p => p
            .WithProjectJson(new { project = new { name = "Static 404" }, theme = new { name = "Lumina" } })
            .WithSiteJson(new { title = "Static 404", author = "Test" }));
        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton<ITheme>(new LuminaTheme());
        });
        var content = host.Services.GetRequiredService<IContentService>();
        var render = host.Services.GetRequiredService<IRenderService>();
        Assert.IsTrue((await content.ScanAsync()).Success);
        Assert.IsTrue((await render.RenderAsync()).Success);
        var outputPage = Path.Combine(project.OutputPath, "404.html");
        var generatedLength = (int)new FileInfo(outputPage).Length;

        // A hand-written page of the same size: the static copy alone would treat it as unchanged
        // against a freshly re-rendered 404.html and keep the generated page.
        var handWritten = "<!DOCTYPE html><title>Custom 404</title>".PadRight(generatedLength, ' ');
        var staticPath = Path.Combine(project.SourcePath, ProjectPaths.Static);
        Directory.CreateDirectory(staticPath);
        await File.WriteAllTextAsync(Path.Combine(staticPath, "404.html"), handWritten);
        Assert.IsTrue((await render.RenderAsync()).Success);

        Assert.AreEqual(handWritten, await File.ReadAllTextAsync(outputPage));
    }

    private sealed record RenderedSite(IReadOnlyDictionary<string, string> Pages, string? Sitemap);

    /// <summary>
    /// Renders a small Lumina site: a titled home page, a gallery with intro text and images,
    /// a gallery whose Markdown body brings its own H1 (plus a content image) and a text page.
    /// </summary>
    private static async Task<RenderedSite> RenderSiteAsync(
        string? baseUrl,
        string? language = null,
        string basePath = "/",
        ITheme? theme = null,
        string homeTitle = "Home & More")
    {
        object site = language is null
            ? new { title = "SEO Site", author = "Test", description = "Site description" }
            : new { title = "SEO Site", author = "Test", description = "Site description", language };
        using var project = TestProject.Create(p => p
            .WithProjectJson(new
            {
                project = new { name = "SEO", baseUrl, basePath },
                theme = new { name = "Lumina" },
                generate = new { images = new { webp = 85, jpg = 90 } }
            })
            .WithSiteJson(site)
            .AddGallery("Island", g => g.AddRealImage("one.jpg", 2400, 1600).AddRealImage("two.jpg", 800, 1200))
            .AddGallery("Notes", g => g.AddRealImage("three.jpg", 800, 600))
            .AddGallery("About"));
        await File.WriteAllTextAsync(Path.Combine(project.SourcePath, "_index.revela"), $"+++\ntitle = \"{homeTitle}\"\n+++\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Island", "_index.revela"),
            "+++\ntitle = \"Island\"\ndescription = \"Island description\"\ncover = \"one.jpg\"\n+++\nIsland intro.\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "Notes", "_index.revela"),
            "+++\ntitle = \"Notes\"\n+++\n# Own heading\n\n![Three](three.jpg)\n");
        await File.WriteAllTextAsync(
            Path.Combine(project.SourcePath, "About", "_index.revela"),
            "+++\ntitle = \"About\"\ntemplate = \"page\"\n+++\nAbout text.\n");

        using var host = RevelaTestHost.Build(project.RootPath, services =>
        {
            services.AddRevelaCommands();
            services.AddGenerateFeature();
            services.AddSingleton(theme ?? new LuminaTheme());
        });
        var scan = await host.Services.GetRequiredService<IContentService>().ScanAsync();
        var render = await host.Services.GetRequiredService<IRenderService>().RenderAsync();

        Assert.IsTrue(scan.Success, scan.ErrorMessage);
        Assert.IsTrue(render.Success, render.ErrorMessage);
        var pages = Directory.GetFiles(project.OutputPath, "*.html", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(project.OutputPath, path).Replace('\\', '/'),
                File.ReadAllText,
                StringComparer.Ordinal);
        var sitemapPath = Path.Combine(project.OutputPath, "sitemap.xml");
        return new RenderedSite(pages, File.Exists(sitemapPath) ? await File.ReadAllTextAsync(sitemapPath) : null);
    }

    private static string AttributeAfter(string html, string prefix)
    {
        var start = html.IndexOf(prefix, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start, $"Missing '{prefix}'.");
        start += prefix.Length;
        return html[start..html.IndexOf('"', start)];
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    [GeneratedRegex(@"<h1[\s>]")]
    private static partial Regex H1Pattern();

    [GeneratedRegex(@"<img\s[^>]*>")]
    private static partial Regex ImgPattern();

    [GeneratedRegex(@"<source\s[^>]*>")]
    private static partial Regex SourcePattern();

    [GeneratedRegex(@"<nav(?:\s[^>]*)?>")]
    private static partial Regex NavPattern();

    [GeneratedRegex(@"aria-label=""(?<label>[^""]*)""")]
    private static partial Regex AriaLabelPattern();

    [GeneratedRegex("(?:href|src|srcset)=\"(?<url>[^\"\\s]+)")]
    private static partial Regex UrlAttributePattern();

    /// <summary>Lumina without <c>Body/NotFound.revela</c>, like a third-party theme that predates the 404 page.</summary>
    private sealed class ThemeWithoutNotFoundTemplate : ITheme
    {
        private readonly LuminaTheme inner = new();

        public PackageMetadata Metadata => inner.Metadata;

        public string? Prefix => inner.Prefix;

        public string? TargetTheme => inner.TargetTheme;

        public ThemeManifest Manifest => inner.Manifest;

        public Stream? GetFile(string relativePath) =>
            IsNotFoundTemplate(relativePath) ? null : inner.GetFile(relativePath);

        public IEnumerable<string> GetAllFiles() => inner.GetAllFiles().Where(file => !IsNotFoundTemplate(file));

        public Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default) =>
            inner.ExtractToAsync(targetDirectory, cancellationToken);

        public Stream? GetSiteTemplate() => inner.GetSiteTemplate();

        public Stream? GetImagesTemplate() => inner.GetImagesTemplate();

        public IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey) =>
            inner.GetTemplateDataDefaults(templateKey);

        private static bool IsNotFoundTemplate(string path) =>
            path.Replace('\\', '/').Equals("Body/NotFound.revela", StringComparison.OrdinalIgnoreCase);
    }
}

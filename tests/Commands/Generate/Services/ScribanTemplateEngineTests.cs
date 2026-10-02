using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Core.Themes;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
#pragma warning disable IDE0005 // Using directive is unnecessary — namespace holds source-generated extension methods the analyzer cannot see.
using Spectara.Revela.Sdk.TemplateModels;
#pragma warning restore IDE0005

namespace Spectara.Revela.Tests.Commands.Generate.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class ScribanTemplateEngineTests
{
    private static ScribanTemplateEngine CreateEngine() =>
        new(Substitute.For<ILogger<ScribanTemplateEngine>>(), new MarkdownService(), Substitute.For<ITemplateResolver>());

    private static Image CreateImage(string slug) => new()
    {
        SourcePath = $"{slug}.jpg",
        FileName = "029081",
        Slug = slug,
        Width = 1920,
        Height = 1080,
        Sizes = [320, 640]
    };

    [TestMethod]
    public void Render_LargePageFollowedBySmallPage_PreservesCompleteOutputs()
    {
        var engine = CreateEngine();
        const string template = "<html><body>{{ for section in sections }}<p>{{ section }}</p>{{ end }}</body></html>";
        var sections = Enumerable.Repeat(new string('x', 65_536), 20).ToArray();
        var smallSections = new[] { "Small page" };
        var expectedBody = string.Concat(sections.Select(section => $"<p>{section}</p>"));

        var largePage = engine.Render(template, Model(("sections", sections)));
        var smallPage = engine.Render(template, Model(("sections", smallSections)));

        Assert.AreEqual($"<html><body>{expectedBody}</body></html>", largePage);
        Assert.AreEqual("<html><body><p>Small page</p></body></html>", smallPage);
    }

    [TestMethod]
    public void Render_ShouldBeThreadSafe_ForConcurrentInvocations()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ScribanTemplateEngine>>();
        var markdown = new MarkdownService();
        var resolver = Substitute.For<ITemplateResolver>();
        var engine = new ScribanTemplateEngine(logger, markdown, resolver);

        const string template = "Hello {{ name }}!";
        var model = Model(("name", "World"));

        // Act
        var outputs = new string[100];
        Parallel.For(0, outputs.Length, i => outputs[i] = engine.Render(template, model));

        // Assert
        foreach (var output in outputs)
        {
            Assert.AreEqual("Hello World!", output);
        }
    }

    [TestMethod]
    public void Render_SameIncludeOnManyPages_LoadsAndParsesIncludeOnce()
    {
        var resolver = Substitute.For<ITemplateResolver>();
        resolver.GetTemplate("partials/figure")
            .Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes("[{{ name }}]")));
        var engine = new ScribanTemplateEngine(Substitute.For<ILogger<ScribanTemplateEngine>>(), new MarkdownService(), resolver);

        var outputs = Enumerable.Range(1, 3)
            .Select(page => engine.Render(
                "{{ include 'figure' }}{{ include 'figure' }}",
                Model(("name", page.ToString(CultureInfo.InvariantCulture)))))
            .ToList();

        Assert.AreEqual("[1][1]|[2][2]|[3][3]", string.Join('|', outputs));
        resolver.Received(1).GetTemplate("partials/figure");
    }

    [TestMethod]
    public void FormatFileSize_ShouldUseInvariantCulture()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ScribanTemplateEngine>>();
        var markdown = new MarkdownService();
        var resolver = Substitute.For<ITemplateResolver>();
        var engine = new ScribanTemplateEngine(logger, markdown, resolver);
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            const string template = "{{ format_filesize 1048576 }}"; // 1 MB
            var result = engine.Render(template, Model());

            // Assert
            Assert.AreEqual("1 MB", result.Trim());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void PageUrl_WithGallery_PrefixesBasePathToSlug()
    {
        var engine = CreateEngine();
        var gallery = new Gallery { Path = "events/fireworks", Slug = "events/fireworks/", Title = "Fireworks" }.ToScriptObject();

        var result = engine.Render("{{ page_url(gallery) }}", Model(("basepath", "/"), ("gallery", gallery)));

        Assert.AreEqual("/events/fireworks/", result.Trim());
    }

    [TestMethod]
    public void PageUrl_WithImage_UsesDedicatedImagePagePath()
    {
        var engine = CreateEngine();
        var image = CreateImage("blubb/peng").ToScriptObject();

        var result = engine.Render("{{ page_url(image) }}", Model(("basepath", "/"), ("image", image)));

        Assert.AreEqual("/photo/blubb/peng/", result.Trim());
    }

    [TestMethod]
    public void PageUrl_WithNavigationItem_PrefixesRelativeBasePath()
    {
        var engine = CreateEngine();
        var item = new NavigationItem { Text = "Vacation", Url = "gallery/2024/" }.ToScriptObject();

        var result = engine.Render("{{ page_url(item) }}", Model(("basepath", "../"), ("item", item)));

        Assert.AreEqual("../gallery/2024/", result.Trim());
    }

    [TestMethod]
    public void PageUrl_WithSlugString_NormalizesToDirectory()
    {
        var engine = CreateEngine();

        var result = engine.Render("{{ page_url('blog/post') }}", Model(("basepath", "/")));

        Assert.AreEqual("/blog/post/", result.Trim());
    }

    [TestMethod]
    public void PageUrl_WithPagelessNavigationItem_RendersEmpty()
    {
        var engine = CreateEngine();
        var item = new NavigationItem { Text = "Section", Url = null }.ToScriptObject();

        // Scriban treats "" as truthy but null as falsy; the helper must return null.
        var result = engine.Render("{{ if page_url(item) }}LINK{{ else }}NONE{{ end }}", Model(("basepath", "/"), ("item", item)));

        Assert.AreEqual("NONE", result.Trim());
    }

    [TestMethod]
    [DataRow("", "_assets/css/x.css")]
    [DataRow("../../", "../../_assets/css/x.css")]
    [DataRow("/gallery/", "/gallery/_assets/css/x.css")]
    public void AssetUrl_ResolvesAgainstPageBasePath(string basePath, string expected)
    {
        var engine = CreateEngine();

        var result = engine.Render("{{ asset_url \"/css\\\\x.css\" }}", Model(("basepath", basePath)));

        Assert.AreEqual(expected, result.Trim());
    }

    [TestMethod]
    public void VariantUrl_BuildsAssetPathFromSlugSizeAndFormat()
    {
        var engine = CreateEngine();
        var image = CreateImage("events/fireworks/029081").ToScriptObject();

        var result = engine.Render(
            "{{ variant_url(image, 640, 'jpg') }}",
            Model(("assets_basepath", "../images/"), ("image", image)));

        Assert.AreEqual("../images/events/fireworks/029081/640.jpg", result.Trim());
    }

    [TestMethod]
    public void AbsoluteUrl_WithBaseUrl_PrependsHostToRootRelativePath()
    {
        var engine = CreateEngine();
        var gallery = new Gallery { Path = "events/fireworks", Slug = "events/fireworks/", Title = "Fireworks" }.ToScriptObject();

        var result = engine.Render(
            "{{ absolute_url(gallery) }}",
            Model(("basepath", "../"), ("base_url", "https://example.com"), ("gallery", gallery)));

        Assert.AreEqual("https://example.com/events/fireworks/", result.Trim());
    }

    [TestMethod]
    public void AbsoluteUrl_WithoutBaseUrl_FallsBackToRootRelative()
    {
        var engine = CreateEngine();
        var gallery = new Gallery { Path = "events/fireworks", Slug = "events/fireworks/", Title = "Fireworks" }.ToScriptObject();

        var result = engine.Render("{{ absolute_url(gallery) }}", Model(("basepath", "../"), ("gallery", gallery)));

        Assert.AreEqual("/events/fireworks/", result.Trim());
    }

    [TestMethod]
    [DataRow("https://example.com", "/photos/", "../../images/", "https://example.com/photos/images/events/fireworks/029081/640.jpg")]
    [DataRow("https://example.com/", "../../", "../../images/", "https://example.com/images/events/fireworks/029081/640.jpg")]
    [DataRow("https://example.com", "/photos/", "/media/", "https://example.com/media/events/fireworks/029081/640.jpg")]
    [DataRow("https://example.com", "/photos/", "media/", "https://example.com/photos/events/fireworks/media/events/fireworks/029081/640.jpg")]
    [DataRow("", "../../", "../../images/", "/images/events/fireworks/029081/640.jpg")]
    [DataRow("", "/photos/", "https://cdn.example.com/images/", "https://cdn.example.com/images/events/fireworks/029081/640.jpg")]
    public void AbsoluteVariantUrl_ResolvesPageAndAssetContext(string origin, string basePath, string assetsBasePath, string expected)
    {
        var engine = CreateEngine();
        var gallery = new Scriban.Runtime.ScriptObject { ["slug"] = "events/fireworks/" };
        var image = new Scriban.Runtime.ScriptObject { ["slug"] = "events/fireworks/029081", ["width"] = 1920, ["height"] = 1080 };

        var result = engine.Render("{{ absolute_variant_url image 640 'jpg' }}",
            Model(("base_url", origin), ("basepath", basePath), ("assets_basepath", assetsBasePath),
                ("gallery", gallery), ("image", image)));

        Assert.AreEqual(expected, result.Trim());
    }

    [TestMethod]
    public void AbsoluteVariantUrl_OnPhotoPage_UsesPhotoPageDepth()
    {
        var engine = CreateEngine();
        var result = engine.Render("{{ absolute_variant_url image 640 'jpg' }}",
            Model(("base_url", "https://example.com"), ("basepath", "/photos/"), ("assets_basepath", "../../../../images/"),
                ("photo", new Scriban.Runtime.ScriptObject()), ("image", CreateImage("events/fireworks/029081").ToScriptObject())));

        Assert.AreEqual("https://example.com/photos/images/events/fireworks/029081/640.jpg", result.Trim());
    }

    [TestMethod]
    public void AbsoluteUrl_WithDeploymentPrefix_PreservesPhotoDestination()
    {
        var result = CreateEngine().Render("{{ absolute_url image }}",
            Model(("base_url", "https://example.com/"), ("basepath", "/photos/"), ("image", CreateImage("one").ToScriptObject())));

        Assert.AreEqual("https://example.com/photos/photo/one/", result.Trim());
    }

    [TestMethod]
    public void HtmlEscape_WithHostileAttributeValue_EncodesMarkupCharacters()
    {
        var engine = CreateEngine();

        var result = engine.Render(
            "<img alt=\"{{ html_escape value }}\">",
            Model(("value", "photo\" & <script>")));

        Assert.AreEqual("<img alt=\"photo&quot; &amp; &lt;script&gt;\">", result.Trim());
    }

    [TestMethod]
    public void HtmlEscape_WithNonAsciiText_KeepsCharactersLiteral()
    {
        var engine = CreateEngine();

        var result = engine.Render(
            "<p title=\"{{ html_escape value }}\">",
            Model(("value", "Menü Straße 日本 <script>alert('x')</script>")));

        Assert.AreEqual("<p title=\"Menü Straße 日本 &lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;\">", result.Trim());
    }

    [TestMethod]
    [DataRow("de", "de_DE")]
    [DataRow("en", "en_US")]
    [DataRow("de-CH", "de_CH")]
    public void OgLocale_WithSiteLanguage_ReturnsOpenGraphLocale(string language, string expected)
    {
        var engine = CreateEngine();
        engine.SetStrings(CreateStrings(language));

        var result = engine.Render("{{ og_locale }}", Model());

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Translate_WithPlaceholderArguments_ReturnsSiteLanguageText()
    {
        var engine = CreateEngine();
        engine.SetStrings(CreateStrings("de", ("photo.next_in", "Nächstes Foto in {0}"), ("photo.close", "Foto schließen")));

        var result = engine.Render(
            "{{ t 'photo.close' }}|{{ t 'photo.next_in' gallery_title }}",
            Model(("gallery_title", "Island")));

        Assert.AreEqual("Foto schließen|Nächstes Foto in Island", result);
    }

    [TestMethod]
    public void Translate_WithHostileArgument_IsEscapedByHtmlEscape()
    {
        var engine = CreateEngine();
        engine.SetStrings(CreateStrings("en", ("photo.return_to", "Return to {0}")));

        var result = engine.Render(
            "<a aria-label=\"{{ html_escape (t 'photo.return_to' title) }}\">",
            Model(("title", "<script>\"x\"</script>")));

        Assert.AreEqual("<a aria-label=\"Return to &lt;script&gt;&quot;x&quot;&lt;/script&gt;\">", result);
    }

    [TestMethod]
    public void Render_ImageDescriptiveMetadata_ExposesSnakeCaseFields()
    {
        var engine = CreateEngine();
        var image = new Image
        {
            SourcePath = "photos/029081.jpg",
            FileName = "029081",
            Slug = "photos/029081",
            Width = 1920,
            Height = 1080,
            Title = "Abendlicht",
            Description = "Evening light",
            Keywords = ["Selected", "Startseite"],
            Rating = 4
        };

        var result = engine.Render(
            "{{ image.title }}|{{ image.description }}|{{ image.keywords | array.join ',' }}|{{ image.rating }}|{{ image.rating >= 4 }}",
            Model(("image", image.ToScriptObject())));

        Assert.AreEqual("Abendlicht|Evening light|Selected,Startseite|4|true", result);
    }

    [TestMethod]
    public void Translate_WithoutStrings_RendersKey()
    {
        var engine = CreateEngine();

        var result = engine.Render("{{ t 'photo.close' }}", Model());

        Assert.AreEqual("photo.close", result);
    }

    [TestMethod]
    public void FormatDate_WithGermanSiteLanguage_UsesGermanMonthNames()
    {
        var engine = CreateEngine();
        engine.SetStrings(CreateStrings("de"));

        var result = engine.Render("{{ format_date day 'd. MMMM yyyy' }}", Model(("day", new DateTime(2024, 3, 5))));

        Assert.AreEqual("5. März 2024", result);
    }

    [TestMethod]
    public void FormatDate_WithoutStrings_UsesInvariantCulture()
    {
        var engine = CreateEngine();
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            var result = engine.Render("{{ format_date day 'MMMM' }}", Model(("day", new DateTime(2024, 3, 5))));

            Assert.AreEqual("March", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static ThemeStrings CreateStrings(string language, params (string Key, string Text)[] entries) =>
        new(
            language,
            [(language, entries.ToDictionary(e => e.Key, e => e.Text, StringComparer.Ordinal))],
            CultureInfo.GetCultureInfo(language),
            NullLogger.Instance);

    private static Dictionary<string, object?> Model(params (string Key, object? Value)[] entries)
    {
        var model = new Dictionary<string, object?>();
        foreach (var (key, value) in entries)
        {
            model[key] = value;
        }
        return model;
    }
}


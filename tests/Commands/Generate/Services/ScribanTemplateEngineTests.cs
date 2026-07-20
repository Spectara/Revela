using System.Globalization;
using NSubstitute;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Services;

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
    public void Render_ShouldBeThreadSafe_ForConcurrentInvocations()
    {
        // Arrange
        var logger = Substitute.For<ILogger<ScribanTemplateEngine>>();
        var markdown = new MarkdownService();
        var resolver = Substitute.For<ITemplateResolver>();
        var engine = new ScribanTemplateEngine(logger, markdown, resolver);

        const string template = "Hello {{ name }}!";
        var model = new { name = "World" };

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
            var result = engine.Render(template, new { });

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
        var gallery = new Gallery { Path = "events/fireworks", Slug = "events/fireworks/", Name = "Fireworks" };

        var result = engine.Render("{{ page_url(gallery) }}", Model(("basepath", "/"), ("gallery", gallery)));

        Assert.AreEqual("/events/fireworks/", result.Trim());
    }

    [TestMethod]
    public void PageUrl_WithImage_UsesDedicatedImagePagePath()
    {
        var engine = CreateEngine();
        var image = CreateImage("blubb/peng");

        var result = engine.Render("{{ page_url(image) }}", Model(("basepath", "/"), ("image", image)));

        Assert.AreEqual("/photo/blubb/peng/", result.Trim());
    }

    [TestMethod]
    public void PageUrl_WithNavigationItem_PrefixesRelativeBasePath()
    {
        var engine = CreateEngine();
        var item = new NavigationItem { Text = "Vacation", Url = "gallery/2024/" };

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
        var item = new NavigationItem { Text = "Section", Url = null };

        // Scriban treats "" as truthy but null as falsy; the helper must return null.
        var result = engine.Render("{{ if page_url(item) }}LINK{{ else }}NONE{{ end }}", Model(("basepath", "/"), ("item", item)));

        Assert.AreEqual("NONE", result.Trim());
    }

    [TestMethod]
    public void VariantUrl_BuildsAssetPathFromSlugSizeAndFormat()
    {
        var engine = CreateEngine();
        var image = CreateImage("events/fireworks/029081");

        var result = engine.Render(
            "{{ variant_url(image, 640, 'jpg') }}",
            Model(("assets_basepath", "../images/"), ("image", image)));

        Assert.AreEqual("../images/events/fireworks/029081/640.jpg", result.Trim());
    }

    [TestMethod]
    public void AbsoluteUrl_WithBaseUrl_PrependsHostToRootRelativePath()
    {
        var engine = CreateEngine();
        var gallery = new Gallery { Path = "events/fireworks", Slug = "events/fireworks/", Name = "Fireworks" };

        var result = engine.Render(
            "{{ absolute_url(gallery) }}",
            Model(("basepath", "../"), ("base_url", "https://example.com"), ("gallery", gallery)));

        Assert.AreEqual("https://example.com/events/fireworks/", result.Trim());
    }

    [TestMethod]
    public void AbsoluteUrl_WithoutBaseUrl_FallsBackToRootRelative()
    {
        var engine = CreateEngine();
        var gallery = new Gallery { Path = "events/fireworks", Slug = "events/fireworks/", Name = "Fireworks" };

        var result = engine.Render("{{ absolute_url(gallery) }}", Model(("basepath", "../"), ("gallery", gallery)));

        Assert.AreEqual("/events/fireworks/", result.Trim());
    }

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


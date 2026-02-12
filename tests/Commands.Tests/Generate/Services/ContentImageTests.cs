using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Services;

namespace Spectara.Revela.Commands.Tests.Generate.Services;

/// <summary>
/// Tests for <see cref="MarkdownService"/> content image resolution.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ContentImageTests
{
    private static readonly string[] Formats = ["avif", "webp", "jpg"];

    #region ToHtml with ContentImageContext

    [TestMethod]
    public void ToHtml_WithImageContext_LocalImage_GeneratesPicture()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["Landscapes/sunset.jpg"] = CreateImage("sunset", 1920, 1080, [320, 640, 1280, 1920])
        };
        var context = new ContentImageContext(images, "Landscapes", "../images/", Formats);

        // Act
        var html = service.ToHtml("![Beautiful Sunset](sunset.jpg)", context);

        // Assert
        Assert.Contains("<picture class=\"content-image\"", html);
        Assert.Contains("image/avif", html);
        Assert.Contains("image/webp", html);
        Assert.Contains("image/jpeg", html);
        Assert.Contains("../images/sunset/320.avif", html);
        Assert.Contains("../images/sunset/1920.avif", html);
        Assert.Contains("alt=\"Beautiful Sunset\"", html);
        Assert.Contains("width=\"1920\"", html);
        Assert.Contains("height=\"1080\"", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.Contains("</picture>", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_SharedImage_GeneratesPicture()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/screenshots/wizard.jpg"] = CreateImage("wizard", 1280, 800, [320, 640, 1280])
        };
        var context = new ContentImageContext(images, "docs/getting-started", "../images/", Formats);

        // Act
        var html = service.ToHtml("![Setup Wizard](screenshots/wizard.jpg)", context);

        // Assert — should resolve via _images/ prefix
        Assert.Contains("<picture class=\"content-image\"", html);
        Assert.Contains("../images/wizard/", html);
        Assert.Contains("alt=\"Setup Wizard\"", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_ExplicitImagesPath_GeneratesPicture()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/screenshots/hero.jpg"] = CreateImage("hero", 1920, 1080, [320, 640, 1920])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act
        var html = service.ToHtml("![Hero](_images/screenshots/hero.jpg)", context);

        // Assert
        Assert.Contains("<picture class=\"content-image\"", html);
        Assert.Contains("images/hero/", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_LocalBeatsShared()
    {
        // Arrange — same filename exists both locally and in _images
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["Landscapes/photo.jpg"] = CreateImage("photo", 1920, 1080, [320, 640, 1920]),
            ["_images/photo.jpg"] = CreateImage("photo", 800, 600, [320, 640])
        };
        var context = new ContentImageContext(images, "Landscapes", "../images/", Formats);

        // Act
        var html = service.ToHtml("![Photo](photo.jpg)", context);

        // Assert — local match should win (1920x1080)
        Assert.Contains("width=\"1920\"", html);
        Assert.Contains("height=\"1080\"", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_ExternalUrl_FallsThrough()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act
        var html = service.ToHtml("![Logo](https://example.com/logo.png)", context);

        // Assert — should produce standard <img>, NOT <picture>
        Assert.DoesNotContain("<picture", html);
        Assert.Contains("<img", html);
        Assert.Contains("https://example.com/logo.png", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_UnresolvedPath_FallsThrough()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        var context = new ContentImageContext(images, "docs", "images/", Formats);

        // Act
        var html = service.ToHtml("![Missing](nonexistent.jpg)", context);

        // Assert — should produce standard <img>
        Assert.DoesNotContain("<picture", html);
        Assert.Contains("<img", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_RegularLink_NotAffected()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/photo.jpg"] = CreateImage("photo", 1920, 1080, [320, 640])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act — regular link (not image!) should not be affected
        var html = service.ToHtml("[Click here](photo.jpg)", context);

        // Assert
        Assert.DoesNotContain("<picture", html);
        Assert.Contains("<a", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_MixedContent_RendersCorrectly()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/screenshots/wizard.jpg"] = CreateImage("wizard", 1280, 800, [320, 640, 1280])
        };
        var context = new ContentImageContext(images, "docs", "../images/", Formats);

        var markdown = """
            # Getting Started

            Here's what the wizard looks like:

            ![Setup Wizard](screenshots/wizard.jpg)

            And that's it!
            """;

        // Act
        var html = service.ToHtml(markdown, context);

        // Assert
        Assert.Contains("<h1", html);
        Assert.Contains("<picture class=\"content-image\"", html);
        Assert.Contains("And that's it!", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_PortraitImage_CalculatesCorrectWidth()
    {
        // Arrange — portrait image (height > width)
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/portrait.jpg"] = CreateImage("portrait", 1080, 1920, [320, 640, 1280])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act
        var html = service.ToHtml("![Portrait](portrait.jpg)", context);

        // Assert — srcset should have adjusted widths for portrait
        // For size 320: actualWidth = floor(320 * 1080 / 1920) = 180
        Assert.Contains("180w", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_Placeholder_IncludesLqip()
    {
        // Arrange
        var service = new MarkdownService();
        var image = CreateImage("photo", 1920, 1080, [320, 640]);
        image = new Image
        {
            SourcePath = "photo.jpg",
            FileName = "photo",
            Width = 1920,
            Height = 1080,
            Sizes = [320, 640],
            Placeholder = "-721311"
        };
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["Gallery/photo.jpg"] = image
        };
        var context = new ContentImageContext(images, "Gallery", "images/", Formats);

        // Act
        var html = service.ToHtml("![Photo](photo.jpg)", context);

        // Assert
        Assert.Contains("--lqip:-721311", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_ImageWithNoSizes_FallsThrough()
    {
        // Arrange — image exists but has no sizes (edge case)
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/tiny.jpg"] = CreateImage("tiny", 50, 50, [])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act
        var html = service.ToHtml("![Tiny](tiny.jpg)", context);

        // Assert — should fall through to default <img>
        Assert.DoesNotContain("<picture", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_SubfolderInSharedImages()
    {
        // Arrange — nested subfolder in _images
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/docs/setup/step1.jpg"] = CreateImage("step1", 1280, 720, [320, 640, 1280])
        };
        var context = new ContentImageContext(images, "getting-started", "../images/", Formats);

        // Act
        var html = service.ToHtml("![Step 1](docs/setup/step1.jpg)", context);

        // Assert
        Assert.Contains("<picture class=\"content-image\"", html);
        Assert.Contains("../images/step1/", html);
    }

    #endregion

    #region Generic Attributes (CSS classes)

    [TestMethod]
    public void ToHtml_WithImageContext_GenericAttribute_AddsClassToPicture()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/screenshot.jpg"] = CreateImage("screenshot", 1920, 1080, [320, 640, 1920])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act — {.browser-mockup} adds a CSS class
        var html = service.ToHtml("![Screenshot](screenshot.jpg){.browser-mockup}", context);

        // Assert
        Assert.Contains("class=\"content-image browser-mockup\"", html);
        Assert.Contains("<picture", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_MultipleClasses_AllAdded()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/hero.jpg"] = CreateImage("hero", 1920, 1080, [320, 640, 1920])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act — multiple classes
        var html = service.ToHtml("![Hero](hero.jpg){.browser-mockup .breakout}", context);

        // Assert
        Assert.Contains("class=\"content-image browser-mockup breakout\"", html);
    }

    [TestMethod]
    public void ToHtml_WithImageContext_NoGenericAttribute_OnlyContentImageClass()
    {
        // Arrange
        var service = new MarkdownService();
        var images = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase)
        {
            ["_images/photo.jpg"] = CreateImage("photo", 1920, 1080, [320, 640])
        };
        var context = new ContentImageContext(images, "", "images/", Formats);

        // Act — no {.class} suffix
        var html = service.ToHtml("![Photo](photo.jpg)", context);

        // Assert — only default class
        Assert.Contains("class=\"content-image\"", html);
        Assert.DoesNotContain("browser-mockup", html);
    }

    #endregion

    #region Original ToHtml (without context) still works

    [TestMethod]
    public void ToHtml_WithoutContext_StandardBehavior()
    {
        // Arrange
        var service = new MarkdownService();

        // Act
        var html = service.ToHtml("![Logo](logo.png)");

        // Assert — standard <img>, no <picture>
        Assert.DoesNotContain("<picture", html);
        Assert.Contains("<img", html);
        Assert.Contains("logo.png", html);
    }

    #endregion

    #region Helpers

    private static Image CreateImage(string name, int width, int height, int[] sizes) => new()
    {
        SourcePath = $"{name}.jpg",
        FileName = name,
        Width = width,
        Height = height,
        Sizes = sizes
    };

    #endregion
}

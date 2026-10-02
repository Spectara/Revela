using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Models;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class SitemapGeneratorTests
{
    private static readonly DateTime BuildDate = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void Generate_WithGalleries_ProducesValidSitemap()
    {
        // Arrange
        var model = new SiteModel
        {
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Title = "Home" },
                new Gallery { Path = "landscapes", Slug = "landscapes/", Title = "Landscapes" },
                new Gallery { Path = "portraits", Slug = "portraits/", Title = "Portraits" }
            ],
            Navigation = [],
            Images = []
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com", "/", BuildDate);

        // Assert
        Assert.Contains("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);
        Assert.Contains("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">", xml);
        Assert.Contains("<loc>https://example.com/</loc>", xml);
        Assert.Contains("<loc>https://example.com/landscapes/</loc>", xml);
        Assert.Contains("<loc>https://example.com/portraits/</loc>", xml);
        Assert.Contains("<lastmod>2026-03-15</lastmod>", xml);
    }

    [TestMethod]
    public void Generate_WithBasePath_IncludesBasePath()
    {
        // Arrange
        var model = new SiteModel
        {
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Title = "Home" },
                new Gallery { Path = "gallery", Slug = "gallery/", Title = "Gallery" }
            ],
            Navigation = [],
            Images = []
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com", "/photos/", BuildDate);

        // Assert
        Assert.Contains("<loc>https://example.com/photos/</loc>", xml);
        Assert.Contains("<loc>https://example.com/photos/gallery/</loc>", xml);
    }

    [TestMethod]
    public void Generate_TrailingSlashOnBaseUrl_NormalizesCorrectly()
    {
        // Arrange
        var model = new SiteModel
        {
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Title = "Home" }
            ],
            Navigation = [],
            Images = []
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com/", "/", BuildDate);

        // Assert — no double slash
        Assert.Contains("<loc>https://example.com/</loc>", xml);
        Assert.DoesNotContain("https://example.com//", xml);
    }

    [TestMethod]
    public void Generate_WithPhotoPages_IncludesEachOnceWithoutFragment()
    {
        // Arrange — one shared image in two eligible galleries → one photo page, two contexts.
        var image = new Image
        {
            SourcePath = "_images/ocean.jpg",
            FileName = "ocean",
            Slug = UrlBuilder.ToImageSlug("_images/ocean.jpg"),
            Width = 100,
            Height = 100
        };
        var galleries = new[]
        {
            new Gallery { Path = "canon", Slug = "canon/", Title = "Canon", Images = [image] },
            new Gallery { Path = "sony", Slug = "sony/", Title = "Sony", Images = [image] }
        };
        var photoPages = PhotoPageCatalog.Build([.. galleries.Select(gallery => new PhotoMembership(gallery, gallery.Images, null, PhotoViewerMode.Page))]);

        var model = new SiteModel
        {
            Galleries = galleries,
            Navigation = [],
            Images = [image]
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com", "/", BuildDate, photoPages);

        // Assert — the photo appears exactly once, canonical route, no context fragment.
        Assert.Contains("<loc>https://example.com/photo/ocean/</loc>", xml);
        Assert.DoesNotContain("#ctx-", xml);

        var occurrences = xml.Split("/photo/ocean/").Length - 1;
        Assert.AreEqual(1, occurrences, "The photo must be listed exactly once.");
    }
}


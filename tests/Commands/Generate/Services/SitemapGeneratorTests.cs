using Spectara.Revela.Plugins.Core.Generate.Models;
using Spectara.Revela.Plugins.Core.Generate.Services;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class SitemapGeneratorTests
{
    [TestMethod]
    public void Generate_WithGalleries_ProducesValidSitemap()
    {
        // Arrange
        var model = new SiteModel
        {
            Project = new RenderProjectSettings { Name = "test" },
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Name = "Home", Title = "Home" },
                new Gallery { Path = "landscapes", Slug = "landscapes/", Name = "Landscapes", Title = "Landscapes" },
                new Gallery { Path = "portraits", Slug = "portraits/", Name = "Portraits", Title = "Portraits" }
            ],
            Navigation = [],
            Images = [],
            BuildDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com", "/");

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
            Project = new RenderProjectSettings { Name = "test" },
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Name = "Home", Title = "Home" },
                new Gallery { Path = "gallery", Slug = "gallery/", Name = "Gallery", Title = "Gallery" }
            ],
            Navigation = [],
            Images = [],
            BuildDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com", "/photos/");

        // Assert
        Assert.Contains("<loc>https://example.com/photos/</loc>", xml);
        Assert.Contains("<loc>https://example.com/photos/gallery/</loc>", xml);
    }

    [TestMethod]
    public void Generate_WithGalleryDate_UsesGalleryDate()
    {
        // Arrange
        var model = new SiteModel
        {
            Project = new RenderProjectSettings { Name = "test" },
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Name = "Home", Title = "Home" },
                new Gallery { Path = "events", Slug = "events/", Name = "Events", Title = "Events", Date = new DateTime(2025, 12, 25, 0, 0, 0, DateTimeKind.Utc) }
            ],
            Navigation = [],
            Images = [],
            BuildDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com", "/");

        // Assert — index uses build date, gallery uses its own date
        Assert.Contains("<loc>https://example.com/events/</loc>", xml);
        Assert.Contains("<lastmod>2025-12-25</lastmod>", xml);
        Assert.Contains("<lastmod>2026-03-15</lastmod>", xml);
    }

    [TestMethod]
    public void Generate_TrailingSlashOnBaseUrl_NormalizesCorrectly()
    {
        // Arrange
        var model = new SiteModel
        {
            Project = new RenderProjectSettings { Name = "test" },
            Galleries =
            [
                new Gallery { Path = "", Slug = "", Name = "Home", Title = "Home" }
            ],
            Navigation = [],
            Images = [],
            BuildDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var xml = SitemapGenerator.Generate(model, "https://example.com/", "/");

        // Assert — no double slash
        Assert.Contains("<loc>https://example.com/</loc>", xml);
        Assert.DoesNotContain("https://example.com//", xml);
    }
}

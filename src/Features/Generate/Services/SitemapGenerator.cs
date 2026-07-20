using System.Globalization;
using System.Security;
using System.Text;
using Spectara.Revela.Features.Generate.Models;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Generates a sitemap.xml for search engine indexing.
/// </summary>
internal static class SitemapGenerator
{
    /// <summary>
    /// Generates sitemap.xml content from the site model.
    /// </summary>
    /// <param name="model">The site model containing all galleries.</param>
    /// <param name="baseUrl">Absolute base URL (e.g., "https://example.com").</param>
    /// <param name="basePath">Base path for subdirectory hosting (e.g., "/photos/").</param>
    /// <param name="photoPages">Canonical photo pages to include once each, without fragments.</param>
    /// <returns>The sitemap XML string.</returns>
    public static string Generate(
        SiteModel model,
        string baseUrl,
        string basePath,
        IReadOnlyList<PhotoPage>? photoPages = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        var normalizedBase = baseUrl.TrimEnd('/') + basePath.TrimEnd('/');
        var lastmod = model.BuildDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Index page
        AppendUrl(sb, normalizedBase + "/", lastmod);

        // Gallery pages
        foreach (var gallery in model.Galleries)
        {
            if (string.IsNullOrEmpty(gallery.Slug))
            {
                continue; // Skip root (already added as index)
            }

            var date = gallery.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? lastmod;
            AppendUrl(sb, $"{normalizedBase}/{gallery.Slug}", date);
        }

        // Photo pages (#77): each eligible source image exactly once, canonical route, no fragment.
        if (photoPages is not null)
        {
            foreach (var page in photoPages)
            {
                AppendUrl(sb, $"{normalizedBase}/photo/{page.Slug}/", lastmod);
            }
        }

        sb.AppendLine("</urlset>");
        return sb.ToString();
    }

    private static void AppendUrl(StringBuilder sb, string loc, string lastmod)
    {
        sb.AppendLine("  <url>");
        sb.Append("    <loc>");
        sb.Append(SecurityElement.Escape(loc));
        sb.AppendLine("</loc>");
        sb.Append("    <lastmod>");
        sb.Append(SecurityElement.Escape(lastmod));
        sb.AppendLine("</lastmod>");
        sb.AppendLine("  </url>");
    }
}


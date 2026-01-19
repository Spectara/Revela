# Static Compression Plugin

**Package:** `Spectara.Revela.Plugin.Compress`  
**Version:** 1.0.0  
**Author:** Spectara

## Overview

The Compress plugin pre-compresses static files in your output directory with **Gzip** and **Brotli**. This improves loading times on hosting platforms that don't support on-the-fly compression.

> **Note:** Many modern platforms (GitHub Pages, Netlify, Vercel, Cloudflare Pages) compress automatically. This plugin is primarily for self-hosted servers, AWS S3 (without CloudFront), or basic web hosts.

## Do You Need This Plugin?

| Platform | Need Plugin? | Reason |
|----------|--------------|--------|
| **GitHub Pages** | ❌ No | Automatic Gzip compression |
| **Netlify** | ❌ No | Automatic Gzip + Brotli |
| **Vercel** | ❌ No | Automatic Gzip + Brotli |
| **Cloudflare Pages** | ❌ No | Automatic Gzip + Brotli |
| **AWS S3 + CloudFront** | 🟡 Optional | CloudFront compresses, but pre-compressed is faster |
| **AWS S3 (direct)** | ✅ Yes | No automatic compression |
| **nginx / Apache** | ✅ Yes | Saves CPU, serves pre-compressed instantly |
| **Basic web hosting** | ✅ Yes | Often no compression support |

## Why Pre-Compression?

Some static hosting platforms serve files as-is without compression. By pre-generating `.gz` and `.br` files, you enable:

- **70-95% smaller file sizes** for text-based content
- **Faster page loads** for visitors
- **Lower bandwidth costs** for high-traffic sites
- **Reduced server CPU** - no on-the-fly compression needed

Modern web servers (nginx, Apache, Caddy) can automatically serve pre-compressed files when the browser supports them.

## Installation

```bash
# Install from NuGet
revela plugin install Compress

# Or with full package ID
revela plugin install Spectara.Revela.Plugin.Compress
```

## Usage

### Compress Output Files

```bash
# Compress all static files
revela generate compress

# Or run the full pipeline (compression runs last)
revela generate all
```

### Clean Compressed Files

```bash
# Remove all .gz and .br files
revela clean compress
```

## Pipeline Integration

The compress step runs **after** all content is generated:

```
scan (100) → statistics (200) → pages (300) → images (400) → compress (500)
```

This ensures all HTML, CSS, and JS files exist before compression.

## Supported File Types

| Extension | MIME Type | Typical Savings |
|-----------|-----------|-----------------|
| `.html` | text/html | 70-85% |
| `.css` | text/css | 75-90% |
| `.js` | text/javascript | 65-80% |
| `.json` | application/json | 70-85% |
| `.svg` | image/svg+xml | 50-70% |
| `.xml` | application/xml | 70-85% |

**Not compressed:** Images (`.avif`, `.webp`, `.jpg`, `.png`) and fonts (`.woff2`) are already compressed.

## Output Structure

Original files remain unchanged. Compressed versions are created alongside:

```
output/
├── index.html          # Original (10 KB)
├── index.html.gz       # Gzip compressed (2.5 KB)
├── index.html.br       # Brotli compressed (2.1 KB)
├── _assets/
│   ├── main.css        # Original
│   ├── main.css.gz     # Gzip
│   ├── main.css.br     # Brotli
│   └── ...
```

## Compression Settings

The plugin uses maximum compression for smallest file sizes:

| Format | Level | Notes |
|--------|-------|-------|
| **Gzip** | 9 (max) | Universal browser support |
| **Brotli** | 11 (max) | ~20% smaller than Gzip, modern browsers |

Files smaller than **256 bytes** are skipped (compression overhead exceeds savings).

## Server Configuration

### nginx

```nginx
# Enable pre-compressed file serving
gzip_static on;
brotli_static on;
```

### Apache

```apache
# Serve .br files for Brotli-capable browsers
RewriteCond %{HTTP:Accept-Encoding} br
RewriteCond %{REQUEST_FILENAME}.br -f
RewriteRule ^(.*)$ $1.br [L]

# Serve .gz files for Gzip-capable browsers  
RewriteCond %{HTTP:Accept-Encoding} gzip
RewriteCond %{REQUEST_FILENAME}.gz -f
RewriteRule ^(.*)$ $1.gz [L]
```

### Caddy

```caddyfile
# Caddy automatically serves pre-compressed files
encode gzip zstd
```

### GitHub Pages / Netlify / Cloudflare Pages

These platforms **automatically** serve pre-compressed files when available. No configuration needed!

## Example Output

```
╭─Success────────────────────────────╮
│ Compression complete!              │
│                                    │
│ Summary:                           │
│   Files:    14                     │
│   Original: 1.22 MB                │
│                                    │
│ Compressed sizes:                  │
│   Gzip:    93.4 KB (92.5% savings) │
│   Brotli:  61.6 KB (95.1% savings) │
╰────────────────────────────────────╯
```

## Requirements

- Revela 1.0.0 or later
- .NET 10.0 or later
- No external dependencies (uses built-in .NET compression)

## See Also

- [Deployment Guide](deployment.md) - Publishing your site
- [Plugin Management](plugin-management.md) - Installing and managing plugins

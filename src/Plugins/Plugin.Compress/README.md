# Spectara.Revela.Plugin.Compress

Static file compression plugin for Revela - compresses HTML, CSS, JS, JSON, SVG, and XML files with Gzip and Brotli.

## Features

- **Gzip compression** (`.gz` files) - Maximum compression (Level 9)
- **Brotli compression** (`.br` files) - Maximum compression (Level 11)
- **Smart filtering** - Only compresses text-based files (HTML, CSS, JS, JSON, SVG, XML)
- **Size threshold** - Skips files smaller than 256 bytes
- **Parallel processing** - Fast compression using all available CPU cores
- **Statistics** - Shows compression savings per format

## Usage

### Compress Output Files

```bash
# Compress all static files in output directory
revela generate compress

# Or run full pipeline (includes compression at the end)
revela generate all
```

### Clean Compressed Files

```bash
# Remove all .gz and .br files from output
revela clean compress

# Or clean everything
revela clean all
```

## Pipeline Integration

The compress step runs **after** all content is generated:

```
scan (100) → statistics (200) → pages (300) → images (400) → compress (500)
```

## Supported File Types

| Extension | MIME Type | Typical Savings |
|-----------|-----------|-----------------|
| `.html` | text/html | 70-85% |
| `.css` | text/css | 75-90% |
| `.js` | text/javascript | 65-80% |
| `.json` | application/json | 70-85% |
| `.svg` | image/svg+xml | 50-70% |
| `.xml` | application/xml | 70-85% |

## Requirements

- Revela 1.0.0 or later
- .NET 10.0 or later

## License

MIT - See LICENSE file in repository root.

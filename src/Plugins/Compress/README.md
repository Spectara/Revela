# Spectara.Revela.Plugins.Compress

Static file compression plugin for Revela - compresses HTML, CSS, JS, JSON, SVG, and XML files with Gzip and Brotli.

## Features

- **Gzip compression** (`.gz` files) - Maximum compression (`CompressionLevel.SmallestSize`, zlib level 9)
- **Brotli compression** (`.br` files) - Maximum compression (`CompressionLevel.SmallestSize`, quality 11)
- **Smart filtering** - Only compresses text-based files (HTML, CSS, JS, JSON, SVG, XML)
- **Size threshold** - Skips files smaller than 256 bytes
- **Parallel processing** - Compresses up to one file per logical CPU core (`Environment.ProcessorCount`)
- **Statistics** - Shows compression savings per format

## Installation

The Standalone build has Compress built in. In the Full build, install it from the bundled
`packages/` folder with `revela plugin install Compress`. The package is not on NuGet.org; it is
attached to each [GitHub Release](https://github.com/spectara/revela/releases).

## Usage

### Compress Output Files

Compression is opt-in and is **not** part of `revela generate all`. Run it after generating:

```bash
revela generate all

# Compress all eligible files in the output directory
revela generate compress
```

### Clean Compressed Files

```bash
# Remove sidecars created and tracked by this plugin
revela clean compress

# Or clean everything
revela clean all
```

### Ownership and Conflicts

The ownership record `.revela/state/compress.json` (in the project, not in the
output, so it is never published) lists the sidecars created by this plugin.
`clean cache` keeps it; `clean output` removes it together with the output.
Compression cleanup removes tracked sidecars even when their source files
have disappeared, but preserves independent gzip/Brotli downloads.

An existing untracked destination, a changed tracked file, or an invalid
ownership record causes an explicit failure instead of overwriting or deleting
unknown data. Old untracked sidecars are not adopted automatically. Resolve such
conflicts manually after inspecting the files, or regenerate a disposable output
directory with `clean output`. That command intentionally removes all output,
including independently supplied downloads.

## Pipeline Integration

`revela generate compress` is registered under `generate` (menu order 500), but it is not a
pipeline step, so `revela generate all` does not run it:

```
generate all:  scan (100) → calendar (150, plugin) → statistics (200, plugin) → pages (300) → images (400)
separately:    compress
```

Cleanup, in contrast, is a pipeline step: `revela clean all` includes `clean compress`.

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

- Revela host of the same release version (plugins are packed and released together with Revela)
- .NET 10.0 or later

## License

MIT - See LICENSE file in repository root.

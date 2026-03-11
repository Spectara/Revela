# Serve Plugin

**Package:** `Spectara.Revela.Plugins.Serve`  
**Version:** 1.0.0  
**Author:** Spectara

## Overview

The Serve plugin provides a local HTTP server for previewing generated sites during development. It uses .NET's built-in `HttpListener` — no external dependencies required.

## Installation

```bash
# Install from NuGet
revela plugin install Serve

# Or with full package ID
revela plugin install Spectara.Revela.Plugins.Serve
```

## Usage

### Start the Server

```bash
# Start server with default settings (port 8080)
revela serve

# Custom port
revela serve --port 3000

# Verbose mode (log all requests)
revela serve --verbose

# Combined
revela serve -p 3000 -v
```

Press `Ctrl+C` to stop the server and return to the interactive menu.

### Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--port` | `-p` | 8080 | Port number for the HTTP server |
| `--verbose` | `-v` | false | Log all requests (default: only 404 errors) |

## Configuration

### Interactive Setup

```bash
# Interactive configuration wizard
revela config serve

# Non-interactive overrides
revela config serve --port 8000
revela config serve --verbose
```

### project.json

Plugin configuration is stored in `project.json`:

```json
{
  "Spectara.Revela.Plugins.Serve": {
    "Port": 8080,
    "Verbose": false
  }
}
```

### Environment Variables

```bash
SPECTARA__REVELA__PLUGIN__SERVE__PORT=3000
SPECTARA__REVELA__PLUGIN__SERVE__VERBOSE=true
```

### Priority Order

Configuration values are resolved in this order (highest priority first):

1. Command-line arguments (`--port`, `--verbose`)
2. Environment variables (`SPECTARA__REVELA__PLUGIN__SERVE__*`)
3. Project config file (`project.json`)
4. Default values (Port: 8080, Verbose: false)

## Features

- **Zero dependencies** — Uses .NET built-in `HttpListener`
- **Correct MIME types** — Supports HTML, CSS, JS, JSON, AVIF, WebP, JPG, PNG, SVG, fonts, and more
- **Security** — Directory traversal protection
- **404 logging** — Shows missing files by default
- **Verbose mode** — Log all requests with color-coded status codes for debugging
- **Cache headers** — Asset files served with `Cache-Control: public, max-age=3600` (HTML excluded)
- **Async streaming** — Large files served efficiently via async I/O
- **Graceful shutdown** — `Ctrl+C` returns to interactive menu

## Output

**Standard mode (only 404 errors):**

```
🌐 Serving output/ at http://localhost:8080
   Press Ctrl+C to stop

⚠ 404: /favicon.ico
```

**Verbose mode:**

```
🌐 Serving output/ at http://localhost:8080
   Press Ctrl+C to stop

GET /index.html 200
GET /css/style.css 200
GET /images/photo.avif 200
⚠ 404: /favicon.ico
```

## Supported MIME Types

| Extension | MIME Type |
|-----------|-----------|
| `.html`, `.htm` | `text/html; charset=utf-8` |
| `.css` | `text/css; charset=utf-8` |
| `.js`, `.mjs` | `text/javascript; charset=utf-8` |
| `.json`, `.map` | `application/json` |
| `.xml` | `application/xml; charset=utf-8` |
| `.avif` | `image/avif` |
| `.webp` | `image/webp` |
| `.jpg`, `.jpeg` | `image/jpeg` |
| `.png` | `image/png` |
| `.gif` | `image/gif` |
| `.svg` | `image/svg+xml` |
| `.ico` | `image/x-icon` |
| `.woff` | `font/woff` |
| `.woff2` | `font/woff2` |
| `.ttf` | `font/ttf` |
| `.otf` | `font/otf` |

Unlisted extensions are served as `application/octet-stream`.

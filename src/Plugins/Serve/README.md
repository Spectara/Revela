# Spectara.Revela.Plugins.Serve

Local static HTTP server plugin for Revela - preview generated sites during development.

It serves the generated `output/` folder at `http://localhost:8080` (configurable). It does
**not** open a browser, watch files, or live-reload pages: after changes, run
`revela generate all` again and refresh the browser.

## Installation

The Standalone build has Serve built in. In the Full build, install it from the bundled
`packages/` folder:

```bash
revela plugin install Serve

# Or with full package ID
revela plugin install Spectara.Revela.Plugins.Serve
```

The package is not on NuGet.org. With the .NET tool, download
`Spectara.Revela.Plugins.Serve.<version>.nupkg` from
[GitHub Releases](https://github.com/spectara/revela/releases) and register its folder with
`revela config feed add releases <folder>` first.

## Usage

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

## Setup

### Modify configuration

```bash
# Interactive
revela config serve

# Non-interactive
revela config serve --port 8000
revela config serve --verbose
```

## Configuration

Plugin configuration is stored in `project.json`:

```json
{
  "Spectara.Revela.Plugins.Serve": {
    "Port": 8080,
    "Verbose": false
  }
}
```

Or use environment variables:

```bash
SPECTARA__REVELA__PLUGIN__SERVE__PORT=3000
SPECTARA__REVELA__PLUGIN__SERVE__VERBOSE=true
```

## Features

- **Zero dependencies** - Uses .NET built-in `HttpListener`
- **Correct MIME types** - Supports HTML, CSS, JS, JSON, AVIF, WebP, JPG, PNG, SVG, ICO
- **404 logging** - Shows missing files by default
- **Verbose mode** - Log all requests for debugging
- **Graceful shutdown** - Ctrl+C returns to interactive menu
- **No live reload** - no file watching or browser auto-open; regenerate and refresh manually

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

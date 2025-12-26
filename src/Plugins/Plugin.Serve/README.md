# Spectara.Revela.Plugin.Serve

Local HTTP server plugin for Revela - preview generated sites during development.

## Installation

```bash
revela plugin install Spectara.Revela.Plugin.Serve
```

## Usage

```bash
# Start server with default settings (port 8080)
revela serve

# Custom port
revela serve --port 3000

# Verbose mode (log all requests)
revela serve --verbose

# Custom output directory
revela serve --path dist

# Combined
revela serve -p 3000 -v
```

Press `Ctrl+C` to stop the server and return to the interactive menu.

## Setup

### Initialize configuration

```bash
# Interactive
revela init serve

# Non-interactive
revela init serve --port 3000 --verbose
```

### Modify configuration

```bash
# Interactive
revela config serve

# Non-interactive
revela config serve --port 8000
revela config serve --verbose
```

## Configuration

The plugin configuration file is stored at `config/Spectara.Revela.Plugin.Serve.json`:

```json
{
  "Spectara.Revela.Plugin.Serve": {
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

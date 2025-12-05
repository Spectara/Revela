# Subdirectory Hosting Sample

This sample demonstrates hosting a Revela site in a subdirectory (e.g., `example.com/photos/`).

## Configuration

### `project.json`

```json
{
  "name": "Subdirectory Sample",
  "theme": "Expose",
  "basePath": "/photos/"
}
```

### Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `basePath` | string | `/` | Path prefix for subdirectory hosting |

## How It Works

When `basePath` is set to `/photos/`:

| Element | Generated Path |
|---------|----------------|
| CSS | `href="/photos/main.css"` |
| Navigation | `href="/photos/events/"` |
| Site title link | `href="/photos/"` |
| Images | `src="images/..."` (unchanged) |

All HTML asset links become absolute paths with the configured prefix,
ensuring they work correctly regardless of the page's nesting depth.

## Use Case

Host your photo portfolio at `https://example.com/photos/` instead of root:

1. Configure `basePath: "/photos/"`
2. Generate the site: `revela generate`
3. Deploy `output/` contents to your server's `/photos/` directory

## Running This Sample

```bash
cd samples/subdirectory
revela generate --skip-images
```

Check the generated HTML to verify paths:
```bash
Select-String 'href="/photos/' output/index.html
```

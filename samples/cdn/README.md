# CDN Sample

This sample demonstrates using a CDN for images while hosting HTML locally.

## Configuration

### `project.json`

```json
{
  "name": "CDN Sample",
  "theme": "Lumina",
  "imageBasePath": "https://cdn.example.com/images/"
}
```

### Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `imageBasePath` | string | (calculated) | Absolute URL for image references |
| `basePath` | string | `/` | Path prefix for subdirectory hosting |

## Output Structure

```
output/
├── index.html
├── main.css
├── images/           ← Upload to CDN
│   └── {image}/
│       ├── 320.jpg
│       ├── 640.jpg
│       └── ...
└── {gallery}/
    └── index.html
```

## Use Cases

### 1. CDN Deployment

Deploy images to a CDN while hosting HTML on your server:

```json
{
  "imageBasePath": "https://cdn.example.com/images/"
}
```

- Upload `output/images/` to CDN (CloudFront, Cloudflare, etc.)
- Upload rest of `output/` to web server
- Image URLs in HTML point to CDN

### 2. Subdirectory Hosting

Host the site in a subdirectory (e.g., `example.com/gallery/`):

```json
{
  "basePath": "/gallery/"
}
```

- All CSS and navigation links use absolute paths with `/gallery/` prefix
- Works correctly regardless of gallery nesting depth
- Site title link goes to `/gallery/` instead of `/`

### 3. Combined CDN + Subdirectory

Full setup with CDN images and subdirectory hosting:

```json
{
  "imageBasePath": "https://cdn.example.com/images/",
  "basePath": "/gallery/"
}
```

## Running This Sample

1. Add source images:
   ```bash
   # Option A: Copy images manually
   cp -r /path/to/images ./source/
   
   # Option B: Use OneDrive plugin
   revela source onedrive download
   ```

2. Generate site:
   ```bash
   cd samples/cdn
   revela generate
   ```

3. Check output:
   ```bash
   ls -R output/
   ```

## Image Path Examples

With `imageBasePath: "https://cdn.example.com/images/"`:

```html
<!-- All pages use absolute CDN URL -->
<img src="https://cdn.example.com/images/photo-1/640.jpg">
```

Without `imageBasePath` (default):

```html
<!-- From index.html (root) -->
<img src="images/photo-1/640.jpg">

<!-- From gallery/index.html -->
<img src="../images/photo-1/640.jpg">
```

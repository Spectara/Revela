# Template Functions

## Overview

Revela templates (Scriban) provide built-in functions for common tasks like URL generation,
image handling, date formatting, and Markdown rendering.

## Image Functions

### `find_image`

Resolve any image from the project by path. Uses the same 3-step lookup as Markdown content images.

```scriban
{{~ logo = find_image "logo.jpg" ~}}

{{~ if logo ~}}
<img src="{{ image_basepath }}{{ logo.url }}/640.jpg"
     width="{{ logo.width }}" height="{{ logo.height }}">
{{~ end ~}}
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `path` | string | Image path (relative to gallery, `_images/`, or exact) |

**Returns:** Image object or `null` if not found.

**Image object properties:**

| Property | Type | Description |
|----------|------|-------------|
| `url` | string | Path segment for image variants (e.g., `"logo"`) |
| `width` | int | Original width in pixels |
| `height` | int | Original height in pixels |
| `sizes` | int[] | Available widths (e.g., `[320, 640, 1280, 1920]`) |
| `placeholder` | string? | CSS LQIP hash (if enabled) |
| `exif` | object? | EXIF metadata (if available) |

**Resolution order:**
1. Gallery-local: `{current gallery path}/{path}`
2. Shared images: `_images/{path}`
3. Exact match: `{path}` as-is

### `image_url`

Generate a URL for a specific image variant (size + format).

```scriban
{{ image_url "photo.jpg" 1920 "webp" }}
```

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `fileName` | string | Image filename |
| `width` | int | Target width |
| `format` | string | Image format (`"avif"`, `"webp"`, `"jpg"`) |

**Returns:** URL string (e.g., `"/images/photo-1920w.webp"`)

## URL Functions

### `url_for`

Generate a URL for a page or gallery.

```scriban
{{ url_for "gallery/vacation" }}
```

**Returns:** `"/gallery/vacation/index.html"`

### `asset_url`

Generate a URL for a static asset (CSS, JS).

```scriban
{{ asset_url "css/style.css" }}
```

**Returns:** `"/assets/css/style.css"`

## Formatting Functions

### `format_date`

Format a date using a custom format string.

```scriban
{{ format_date image.date_taken "yyyy-MM-dd" }}
{{ format_date image.date_taken "MMMM yyyy" }}
```

### `format_filesize`

Format a file size in bytes to a human-readable string.

```scriban
{{ format_filesize image.file_size }}
```

**Returns:** e.g., `"2.4 MB"`, `"340 KB"`

### `format_exif_exposure`

Format an exposure time value to photography notation.

```scriban
{{ format_exif_exposure image.exif.exposure_time }}
```

**Returns:** e.g., `"1/250s"`, `"2s"`

### `format_exif_aperture`

Format an aperture value to photography notation.

```scriban
{{ format_exif_aperture image.exif.f_number }}
```

**Returns:** e.g., `"f/2.8"`, `"f/11"`

## Content Functions

### `markdown`

Convert Markdown text to HTML.

```scriban
{{ "**bold** text" | markdown }}
```

**Returns:** `"<p><strong>bold</strong> text</p>"`

## Template Variables

These are not functions but variables available in all templates:

| Variable | Type | Description |
|----------|------|-------------|
| `site` | object | Site settings from `site.json` |
| `gallery` | Gallery | Current page (title, body, cover_image, template) |
| `images` | Image[] | Images for current gallery |
| `nav_items` | NavItem[] | Navigation tree |
| `basepath` | string | Relative path to site root |
| `image_basepath` | string | Path/URL to image variants |
| `image_formats` | string[] | Active formats (`["avif", "webp", "jpg"]`) |
| `page_content` | string | Original Markdown body as HTML |
| `theme` | object | Theme variables from `theme.json` |
| `stylesheets` | string[] | CSS filenames |
| `scripts` | string[] | JS filenames |

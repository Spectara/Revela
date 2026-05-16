# Theme Customization

## Overview

Revela allows you to customize themes without modifying the original theme files. You can:
- **Extract a theme** to your project for full customization
- **Override specific files** (templates, assets, configuration)
- **Tweak visual design** by editing the CSS Custom Properties in the theme's stylesheet

## How It Works

When generating your site, Revela resolves themes with this priority:

1. **Local theme** (`themes/<name>/`) - Highest priority
2. **Installed plugins** (NuGet packages)
3. **Default theme** (Lumina)

This means: If you have a `themes/Lumina/` folder in your project, it takes precedence over the installed Lumina theme.

## Extracting a Theme

### Full Extraction

Extract an entire theme for complete customization:

```bash
# Extract to themes/Lumina/
revela theme extract Lumina

# Extract with custom name
revela theme extract Lumina MyTheme
```

### Interactive Mode

Run without arguments for guided extraction:

```bash
revela theme extract
```

This shows:
1. **Theme selection** - Choose from available themes
2. **Extraction mode** - Full theme or select specific files
3. **File picker** - Multi-select files by category (if selective)

### Selective Extraction

Extract only specific files:

```bash
# Extract single file
revela theme extract Lumina --file layout.revela

# Extract multiple files
revela theme extract Lumina --file layout.revela --file Assets/styles.css

# Extract entire folder
revela theme extract Lumina --file Assets/

# Configuration only
revela theme extract Lumina --file theme.json --file Configuration/images.json
```

## Theme Structure

After extraction, your theme folder looks like this:

```
your-project/
├── themes/
│   └── Lumina/                    # Your customized theme
│       ├── manifest.json          # Theme manifest (name, version, templates)
│       ├── layout.revela          # Main layout template
│       ├── Assets/                # CSS, JS, fonts, images
│       │   ├── styles.css
│       │   └── scripts.js
│       ├── Body/                  # Body templates
│       │   ├── Gallery.revela
│       │   └── Page.revela
│       ├── Partials/              # Partial templates
│       │   ├── ContentImage.revela  # Required: Markdown image rendering
│       │   ├── Navigation.revela
│       │   └── Image.revela
│       └── Configuration/         # Theme configuration
│           ├── site.json
│           └── images.json
├── source/                        # Your photos
├── project.json
└── site.json
```

## Required Templates

Every theme **must** include `Partials/ContentImage.revela`. This template renders images
from Markdown body content (`![alt](path)` syntax).

### Template Variables

| Variable | Type | Description |
|----------|------|-------------|
| `image` | Image | Image object (url, width, height, sizes, placeholder, exif) |
| `alt` | string | Alt text from Markdown |
| `classes` | string[] | CSS classes from `{.class}` syntax |
| `image_basepath` | string | Base path to image variants |
| `image_formats` | string[] | Active formats (e.g., `["avif", "webp", "jpg"]`) |

### Minimal Example

```scriban
{{~ if !image.sizes || image.sizes.size == 0; ret; end ~}}
<picture class="content-image{{ for cls in classes }} {{ cls }}{{ end }}">
{{~ for format in image_formats ~}}
  <source type="image/{{ format }}" srcset="
    {{~ for size in image.sizes ~}}
    {{ image_basepath }}{{ image.url }}/{{ size }}.{{ format }} {{ size }}w{{ if !for.last }},{{ end }}
    {{~ end ~}}">
{{~ end ~}}
  <img src="{{ image_basepath }}{{ image.url }}/{{ image.sizes | array.last }}.jpg"
       alt="{{ alt }}" loading="lazy" decoding="async">
</picture>
```

### With Lightbox

Themes can add lightbox functionality by wrapping the image:

```scriban
{{~ largest_size = image.sizes | array.last ~}}
<a href="{{ image_basepath }}{{ image.url }}/{{ largest_size }}.jpg" class="lightbox">
  <picture class="content-image{{ for cls in classes }} {{ cls }}{{ end }}">
    ...
  </picture>
</a>
```

## Customization Options

### Visual Design (CSS Custom Properties)

Lumina (and any theme following the same convention) exposes its design tokens as
native CSS Custom Properties in `Assets/main.css`. To customize colors, spacing,
or typography, extract the stylesheet and edit the properties directly:

```bash
revela theme extract Lumina --file Assets/main.css
```

Then edit `themes/Lumina/Assets/main.css`:

```css
:root {
  --color: light-dark(hsl(0 0% 40%), hsl(0 0% 70%));
  --color-bg: light-dark(hsl(0 0% 100%), hsl(0 0% 0%));
  --space-m: clamp(1.125rem, 0.9181rem + 1.0345vw, 1.5rem);
  --content-max-width: 900px;
  /* ... */
}
```

This is the standard CSS approach — it supports automatic dark mode
(`light-dark()`), fluid spacing (`clamp()`), and runtime cascading. No JSON or
template edits required.

### Footer Text

Footer attribution comes from `site.copyright` in `site.json`. The theme renders
whatever you put there — set it to your studio name, leave it blank, or add
your own attributions:

```json
{
  "copyright": "© 2026 Jane Doe Photography"
}
```

### Templates

Customize HTML output by editing `.revela` files:

**layout.revela** - Main page structure:
```html
<!DOCTYPE html>
<html>
<head>
    <title>{{ site.title }} - {{ gallery.title }}</title>
    {{ for css in stylesheets }}
    <link rel="stylesheet" href="{{ basepath }}{{ css }}">
    {{ end }}
</head>
<body>
    {{ include 'navigation' }}
    {{ body }}
</body>
</html>
```

**Body/Gallery.revela** - Gallery page content:
```html
<main class="gallery">
    <h1>{{ gallery.title }}</h1>
    {{ if gallery.body }}
    <div class="description">{{ gallery.body }}</div>
    {{ end }}
    <div class="images">
        {{ for image in images }}
        {{ include 'image' }}
        {{ end }}
    </div>
</main>
```

### Assets (CSS/JS)

CSS files are static assets — they are copied as-is, not processed through the
template engine. Edit them directly after extraction.

### Image Sizes (Configuration/images.json)

Customize responsive image sizes:

```json
{
  "sizes": [160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560]
}
```

**Note:** Image formats and quality are configured in `project.json`, not in the theme:

```json
{
  "generate": {
    "images": {
      "avif": 80,
      "webp": 85,
      "jpg": 90
    }
  }
}
```

## Partial Overrides

You don't need to extract the entire theme. Extract only what you want to change:

```bash
# Only customize the navigation
revela theme extract Lumina --file Partials/Navigation.revela

# Only change image sizes
revela theme extract Lumina --file Configuration/images.json
```

The rest will be loaded from the installed theme.

**Note:** Partial overrides work at the file level. If you extract a file, the entire file is your responsibility.

## Theme Extensions

Theme extensions (e.g., Statistics for Lumina) add functionality like charts, maps, or analytics.

### Automatic Extension Extraction

When you extract a theme, **extensions are automatically included** in category subfolders:

```bash
revela theme extract Lumina
```

This creates:
```
themes/Lumina/
├── layout.revela
├── theme.json
├── Assets/
│   ├── styles.css              # Theme assets
│   └── Statistics/             # Extension assets in subfolder
│       └── statistics.css
├── Partials/
│   ├── Navigation.revela       # Theme partials
│   └── Statistics/             # Extension partials in subfolder
│       └── Statistics.revela
└── Configuration/
    └── images.json
```

**Why subfolders?** Extension files are placed in `Category/ExtensionName/` subfolders. This keeps your theme organized and avoids file name conflicts.

### Customizing Extension Files

You can extract and modify specific extension files using the interactive mode:

```bash
revela theme extract Lumina
# → Select specific files
# → Extension files show their target path: Partials/Statistics/Statistics.revela
```

Or via CLI:

```bash
# Extract specific extension file
revela theme extract Lumina --file Partials/Statistics/Statistics.revela
```

### How Extensions Are Found

When generating, Revela looks for extension files in category subfolders. For example, a Statistics extension partial is found at:
- `themes/Lumina/Partials/Statistics/Statistics.revela` (local)
- Or from the installed extension plugin

## Workflow Example

### 1. Start with Default Theme

```bash
revela create my-portfolio
cd my-portfolio
revela generate all
```

### 2. Customize Colors

```bash
# Extract the main stylesheet
revela theme extract Lumina --file Assets/main.css
```

Edit `themes/Lumina/Assets/main.css`:
```css
:root {
  --color: light-dark(hsl(0 0% 20%), hsl(0 0% 80%));
  --color-bg: light-dark(hsl(40 30% 98%), hsl(220 15% 8%));
}
```

### 3. Customize Layout

```bash
# Extract layout template
revela theme extract Lumina --file layout.revela
```

Edit `themes/Lumina/layout.revela` to add your custom header.

### 4. Regenerate

```bash
revela generate all
```

Your customizations are automatically used.

## CLI Reference

```bash
# Full extraction (includes extensions)
revela theme extract Lumina
revela theme extract Lumina MyTheme

# Selective extraction
revela theme extract Lumina --file Body/Gallery.revela
revela theme extract Lumina --file Body/Gallery.revela --file Assets/

# Extract specific extension file (note the subfolder structure)
revela theme extract Lumina --file Partials/Statistics/Statistics.revela

# Overwrite existing files
revela theme extract Lumina --force

# Interactive mode
revela theme extract

# List available files
revela theme files
revela theme files --theme Lumina
```

## Tips

1. **Start small** - Extract only what you need to change
2. **Version control** - Commit your `themes/` folder
3. **Update carefully** - When the base theme updates, your overrides remain
4. **Prefer CSS Custom Properties** - For visual changes edit the CSS tokens in `Assets/main.css` rather than touching templates
5. **Test incrementally** - Run `revela serve` to preview changes live

# Theme Customization

## Overview

Revela allows you to customize themes without modifying the original theme files. You can:
- **Extract a theme** to your project for full customization
- **Override specific files** (templates, assets, configuration)
- **Modify theme variables** (colors, fonts, sizes)

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
│       ├── theme.json             # Theme manifest & variables
│       ├── layout.revela          # Main layout template
│       ├── Assets/                # CSS, JS, fonts, images
│       │   ├── styles.css
│       │   └── scripts.js
│       ├── Body/                  # Body templates
│       │   ├── Gallery.revela
│       │   └── Page.revela
│       ├── Partials/              # Partial templates
│       │   ├── Navigation.revela
│       │   └── Image.revela
│       └── Configuration/         # Theme configuration
│           ├── site.json
│           └── images.json
├── source/                        # Your photos
├── project.json
└── site.json
```

## Customization Options

### Theme Variables (theme.json)

Modify colors, fonts, and other design tokens:

```json
{
  "name": "Lumina",
  "version": "1.0.0",
  "variables": {
    "primary-color": "#2563eb",
    "background-color": "#ffffff",
    "text-color": "#1f2937",
    "font-family": "Inter, sans-serif",
    "border-radius": "0.5rem"
  }
}
```

Variables are available in templates as `{{ theme.primary-color }}`.

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
<body style="background: {{ theme.background-color }}">
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

Modify styles in `Assets/styles.css`:

```css
:root {
    --primary: {{ theme.primary-color }};
    --bg: {{ theme.background-color }};
}

.gallery {
    max-width: 1200px;
    margin: 0 auto;
}
```

### Image Sizes (Configuration/images.json)

Customize responsive image sizes:

```json
{
  "sizes": [640, 1024, 1280, 1920, 2560]
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
# Extract only theme.json
revela theme extract Lumina --file theme.json
```

Edit `themes/Lumina/theme.json`:
```json
{
  "variables": {
    "primary-color": "#dc2626",
    "background-color": "#0f172a"
  }
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
4. **Use variables** - Prefer theme variables over hardcoded values
5. **Test incrementally** - Run `revela serve` to preview changes live

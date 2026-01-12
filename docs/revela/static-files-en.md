# Static Files

## Overview

Static files are files that should be copied directly to the output root without any processing. Common use cases include:

- **Favicons** - Browser icons in various formats
- **robots.txt** - Search engine crawler instructions
- **CNAME** - Custom domain for GitHub Pages
- **.nojekyll** - Disable Jekyll processing on GitHub Pages
- **sitemap.xml** - Pre-generated sitemaps
- **ads.txt** - Advertising authorization
- **security.txt** - Security contact information

## Convention

Place static files in the `source/_static/` folder. They will be copied 1:1 to the output root.

```
source/
├── _static/                    # Static files folder
│   ├── favicon/               # Favicon files
│   │   ├── favicon.ico
│   │   ├── favicon.svg
│   │   ├── favicon-96x96.png
│   │   ├── apple-touch-icon.png
│   │   └── site.webmanifest
│   ├── CNAME                  # GitHub Pages custom domain
│   ├── .nojekyll              # Disable Jekyll on GitHub Pages
│   └── robots.txt             # Search engine instructions
├── _index.revela
└── gallery/
    └── ...
```

**Output:**

```
output/
├── favicon/
│   ├── favicon.ico
│   ├── favicon.svg
│   ├── favicon-96x96.png
│   ├── apple-touch-icon.png
│   └── site.webmanifest
├── CNAME
├── .nojekyll
├── robots.txt
├── index.html
└── gallery/
    └── ...
```

## Favicon Setup

### Step 1: Generate Favicons

Use a favicon generator like [realfavicongenerator.net](https://realfavicongenerator.net) or [favicon.io](https://favicon.io):

1. Upload your logo/image
2. Configure platform-specific icons
3. Download the generated package
4. Copy files to `source/_static/favicon/`

### Step 2: Create Favicon Partial

Create a theme override to include the favicon HTML in your pages.

**File:** `themes/Lumina/Partials/Favicon.revela`

```html
    <link rel="icon" type="image/png" href="/favicon/favicon-96x96.png" sizes="96x96" />
    <link rel="icon" type="image/svg+xml" href="/favicon/favicon.svg" />
    <link rel="shortcut icon" href="/favicon/favicon.ico" />
    <link rel="apple-touch-icon" sizes="180x180" href="/favicon/apple-touch-icon.png" />
    <meta name="apple-mobile-web-app-title" content="Your Site Name" />
    <link rel="manifest" href="/favicon/site.webmanifest" />
```

> **Note:** Copy the exact HTML provided by your favicon generator. The format varies depending on which icons you generated.

### Step 3: Generate Site

```bash
revela generate pages
```

The favicon files are copied to `output/favicon/` and the HTML is included in every page's `<head>` section.

## Project Structure

```
my-project/
├── project.json
├── site.json
├── source/
│   ├── _static/              # ← Static files here
│   │   └── favicon/
│   └── ...
├── themes/
│   └── Lumina/
│       └── Partials/
│           └── Favicon.revela  # ← Favicon HTML here
└── output/
    ├── favicon/              # ← Copied here
    └── ...
```

## How It Works

1. **Scanning:** Folders starting with `_` are skipped by the content scanner (no galleries created)
2. **Copying:** After page rendering, all files from `source/_static/` are copied to `output/`
3. **Structure:** Directory structure is preserved (`_static/favicon/` → `output/favicon/`)
4. **Overwrite:** Existing files in output are overwritten silently

## Theme Customization

The default Lumina theme includes an empty `Favicon.revela` partial. To add favicons:

1. Create the override directory: `themes/Lumina/Partials/`
2. Create `Favicon.revela` with your favicon HTML
3. The theme will automatically use your override

This pattern keeps the theme clean while allowing full customization.

## Common Static Files

### robots.txt

```
User-agent: *
Allow: /

Sitemap: https://example.com/sitemap.xml
```

### CNAME (GitHub Pages)

```
example.com
```

### .nojekyll (GitHub Pages)

Empty file - just create it, no content needed.

## Best Practices

- **Favicon folder:** Keep all favicon files in `_static/favicon/` for organization
- **Root files:** Place files that must be at the root (CNAME, robots.txt) directly in `_static/`
- **No processing:** Static files are copied as-is, no minification or optimization
- **Version control:** Commit static files to your repository
- **Generated files:** If using a favicon generator, save the generator's output for future updates

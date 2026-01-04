# Creating Pages

## Overview

Revela provides commands to quickly create new pages. You can create:
- **Gallery pages** (photo collections with optional text)
- **Text pages** (About, Contact, Imprint, etc.)
- **Statistics pages** (EXIF analysis of your photos)

## Available Page Types

| Type | Description | Template |
|------|-------------|----------|
| `gallery` | Photo gallery with image grid | Default (body/gallery) |
| `text` | Text-only page without images | page |
| `statistics` | EXIF statistics | statistics/overview |

## Gallery Pages

Create a new gallery for your photos:

```bash
# Simple gallery
revela create page gallery vacation --title "Summer Vacation 2024"

# With description and sorting
revela create page gallery best-shots --title "Highlights" \
    --description "My best shots" \
    --sort "exif.raw.Rating:desc"

# Hidden gallery (not in navigation)
revela create page gallery drafts --title "Drafts" --hidden

# With custom URL segment
revela create page gallery 2024-12-christmas --title "Christmas" --slug "xmas"
```

### Options

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--title` | `-t` | Page title | "Gallery" |
| `--description` | `-d` | Description (for SEO) | "" |
| `--sort` | `-s` | Override sorting | (global) |
| `--hidden` | - | Hide from navigation | false |
| `--slug` | - | Custom URL segment | (folder name) |

### Sort Options

The `--sort` option supports all fields from [Sorting](sorting-en.md):

```bash
--sort "dateTaken:asc"        # Oldest first
--sort "dateTaken:desc"       # Newest first
--sort "filename:asc"         # A → Z
--sort "exif.raw.Rating:desc" # Highest rating first
--sort "exif.focalLength:asc" # Wide angle → Telephoto
```

### Generated File

```toml
+++
title = "Summer Vacation 2024"
description = ""
+++
Add an optional introduction here.

This text appears above the image gallery.
```

## Text Pages

Create pages without image gallery (About, Contact, Imprint):

```bash
# About page
revela create page text about --title "About Me" \
    --description "Learn more about me and my photography"

# Contact page
revela create page text contact --title "Contact"

# Imprint (hidden)
revela create page text imprint --title "Imprint" --hidden
```

### Options

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--title` | `-t` | Page title | "Page" |
| `--description` | `-d` | Description (for SEO) | "" |
| `--hidden` | - | Hide from navigation | false |
| `--slug` | - | Custom URL segment | (folder name) |

### Generated File

```toml
+++
title = "About Me"
description = "Learn more about me and my photography"
template = "page"
+++
Write your content here using **Markdown**.

## Example Heading

- List item one
- List item two

*Edit this file to add your own content.*
```

## Statistics Pages

Create a page with EXIF statistics of your photo collection:

```bash
revela create page statistics stats --title "Photo Statistics" \
    --description "Analysis of my camera and lens usage"
```

> **Note:** The Statistics plugin must be installed (`revela plugins install Statistics`).

### Generated File

```toml
+++
title = "Photo Statistics"
description = "Analysis of my camera and lens usage"
template = "statistics/overview"
+++
```

## Interactive Mode

All page types support an interactive mode. Start without the path argument:

```bash
revela create page gallery
revela create page text
revela create page statistics
```

The wizard guides you through all options:

1. **Enter path** - Relative path to `source/`
2. **Title** - Enter page title
3. **Description** - Optional description
4. **Sorting** (gallery only) - Choose from presets or enter custom
5. **Hidden** - Hide from navigation?
6. **Slug** - Custom URL segment
7. **Preview** - Show generated file
8. **Confirm** - Create file?

## Frontmatter Fields

### All Page Types

| Field | Description |
|-------|-------------|
| `title` | Page title (in navigation and `<title>`) |
| `description` | SEO description |
| `hidden` | `true` = not in navigation (but accessible via URL) |
| `slug` | Overrides URL segment (last segment only) |
| `template` | Body template (default: `gallery` or `page`) |

### Gallery Pages Only

| Field | Description |
|-------|-------------|
| `sort` | Override image sorting (e.g., `dateTaken:asc`) |

### Statistics Pages Only

| Field | Description |
|-------|-------------|
| `template` | Always `statistics/overview` |

## Folder Structure

Pages are always created in the `source/` directory:

```
source/
├── vacation/
│   └── _index.revela      ← revela create page gallery vacation
├── about/
│   └── _index.revela      ← revela create page text about
└── stats/
    └── _index.revela      ← revela create page statistics stats
```

Nested paths are supported:

```bash
revela create page gallery "2024/summer/italy" --title "Italy 2024"
# Creates: source/2024/summer/italy/_index.revela
```

## Tips

### Editing Markdown Body

After creating, you can freely edit the Markdown section below `+++`:

```toml
+++
title = "About Me"
template = "page"
+++
## Welcome!

I'm a photographer from Berlin specializing in **landscape** and **architecture photography**.

### Contact

- Email: photo@example.com
- Instagram: @myaccount

![Portrait](portrait.jpg)
```

### Images in Text Pages

Text pages can also contain images - they just won't be displayed as a gallery:

```markdown
![My Setup](setup.jpg)
```

### Navigation and Order

The order in navigation is determined by folder names:

```
source/
├── 01 Galleries/
│   ├── 01 Landscape/
│   └── 02 Portrait/
├── 02 About/
└── 03 Contact/
```

Use number prefixes for desired sorting.

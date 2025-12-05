# Minimal Sample

This is a minimal Revela project for testing and demonstration.

## Structure

```
minimal/
├── project.json              # Project configuration
├── site.json                 # Site metadata
└── content/
    ├── _index.md             # Root metadata (optional)
    ├── 01 Events/
    │   ├── _index.md         # Section metadata
    │   └── 2024 Wedding/
    │       ├── _index.md     # Gallery metadata with custom slug
    │       └── *.jpg         # Add photos here
    ├── 02 Portraits/
    │   ├── _index.md
    │   └── *.jpg
    └── 99 Hidden/
        ├── _index.md         # hidden: true
        └── *.jpg
```

## _index.md Format

Each directory can have an optional `_index.md` file with YAML frontmatter:

```yaml
---
title: Custom Display Title
slug: custom-url-slug
description: SEO description
hidden: true
---

Optional body content in Markdown.
```

### Fields

| Field | Description |
|-------|-------------|
| `title` | Overrides display name (instead of folder name) |
| `slug` | Custom URL segment (only affects last segment) |
| `description` | For SEO and display purposes |
| `hidden` | Hide from navigation (page still generated) |

## Usage

```bash
cd samples/minimal

# Add photos to the gallery folders
# Then generate:
revela generate
```

## Configuration

- **Project:** minimal-sample
- **Theme:** default (built-in)
- **Output:** output/

Edit `project.json` and `site.json` to customize.

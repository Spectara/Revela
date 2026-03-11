# OneDrive Sample Project

This sample demonstrates using the OneDrive Source Plugin to download images from a shared OneDrive folder.

## Configuration

### `plugins/Spectara.Revela.Plugins.Source.OneDrive.json`

Contains the OneDrive share link configuration:

```json
{
  "Spectara.Revela.Plugins.Source.OneDrive": {
    "ShareUrl": "https://1drv.ms/f/..."
  }
}
```

> **Note:** The Package-ID is used directly as root key (no wrapper object needed).

### `project.json`

Project-specific settings (input/output directories, resolutions, etc.)

### `site.json`

Site metadata (title, description, author, etc.)

## Usage

### 1. Sync images from OneDrive

```bash
cd samples/onedrive
revela source onedrive sync
```

This syncs all images from the shared OneDrive folder into `source/`.

If you're running from the repo without installing the global tool:

```powershell
Push-Location "samples/onedrive"
dotnet run --project "../../src/Cli" -- source onedrive sync
Pop-Location
```

### 2. Generate the site

```bash
revela generate
```

This processes images and generates the static site in `output/`.

From the repo:

```powershell
Push-Location "samples/onedrive"
dotnet run --project "../../src/Cli" -- generate
Pop-Location
```

## Folder Structure

After running both commands:

```
onedrive/
├── plugins/
│   └── Spectara.Revela.Plugins.Source.OneDrive.json
├── project.json        # Project settings
├── site.json           # Site metadata
├── source/             # Downloaded images (gitignored)
│   ├── 01 Events/
│   │   ├── _index.md   # Section metadata
│   │   ├── Fireworks/
│   │   │   ├── _index.md
│   │   │   └── *.jpg
│   │   └── Racing/
│   │       ├── _index.md
│   │       └── *.jpg
│   ├── 02 Miscellaneous/
│   │   ├── _index.md
│   │   └── *.jpg
│   └── 03 Pages/
│       └── _index.md   # hidden: true
└── output/             # Generated site (gitignored)
    ├── index.html
    └── images/
```

## Directory Metadata (_index.md)

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

| Field | Description |
|-------|-------------|
| `title` | Overrides display name (instead of folder name) |
| `slug` | Custom URL segment (only affects last segment) |
| `description` | For SEO and display purposes |
| `hidden` | Hide from navigation (page still generated) |

## Notes

- The `source/` and `output/` folders are excluded from Git
- The OneDrive share link is read-only and public (no authentication required)
- Images retain their EXIF metadata for gallery display

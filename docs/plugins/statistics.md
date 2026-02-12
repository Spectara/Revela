# Statistics Plugin

**Package:** `Spectara.Revela.Plugin.Statistics`  
**Version:** 1.0.0  
**Author:** Spectara

## Overview

The Statistics plugin analyzes EXIF data from your photo library and generates statistics pages showing camera usage, lens preferences, exposure settings, and a timeline.

It produces a `statistics.json` file that is consumed by the theme extension **Theme.Lumina.Statistics** to render interactive Chart.js visualizations.

## Installation

```bash
# Plugin (data generation)
revela plugin install Statistics

# Theme extension (Chart.js visualizations) – recommended
revela plugin install Theme.Lumina.Statistics
```

## How It Works

1. **Scan** collects EXIF metadata from all images into `manifest.json`
2. **Statistics** reads the manifest and aggregates data into `statistics.json`
3. **Pages** renders HTML using the theme extension's statistics template

Pipeline order: `scan (10) → statistics (20) → pages (30) → images (40)`

## CLI Commands

### Generate Statistics

```bash
# Full pipeline (includes statistics)
revela generate all

# Only statistics step
revela generate statistics
```

**Prerequisites:** Requires `manifest.json` – run `revela generate scan` first.

### Clean Statistics

```bash
# Remove generated statistics files
revela clean statistics
```

### Configure Settings

```bash
# Interactive configuration
revela config statistics

# Set specific options
revela config statistics --max-entries 20
revela config statistics --sort-by-count false
```

## Configuration

**Section:** `Spectara.Revela.Plugin.Statistics`

```json
{
  "Spectara.Revela.Plugin.Statistics": {
    "MaxEntriesPerCategory": 15,
    "SortByCount": true,
    "MaxBarWidth": 100
  }
}
```

| Property | Type | Default | Validation | Description |
|----------|------|---------|------------|-------------|
| `MaxEntriesPerCategory` | `int` | `15` | 0-100 | Top N entries per category. Remaining grouped as "Other". 0 = unlimited. |
| `SortByCount` | `bool` | `true` | – | Sort entries by count (descending) instead of alphabetically. |
| `MaxBarWidth` | `int` | `100` | 10-100 | Bar chart width percentage for HTML output. |

### Environment Variables

```bash
SPECTARA__REVELA__PLUGIN__STATISTICS__MAXENTRIESPERCATEGORY=20
SPECTARA__REVELA__PLUGIN__STATISTICS__SORTBYCOUNT=false
SPECTARA__REVELA__PLUGIN__STATISTICS__MAXBARWIDTH=80
```

## Creating a Statistics Page

A statistics page needs the `data.statistics` frontmatter field:

```
+++
title = "Statistics"
template = "statistics"
data = { statistics = "statistics.json" }
+++

Optional markdown content above the charts.
```

Create one with the CLI:

```bash
revela create page statistics
```

The plugin scans the manifest for pages with `data.statistics` set and writes the JSON file to `.cache/{page.Path}/statistics.json`.

## Output Format

The generated `statistics.json` follows this schema:

```json
{
  "TotalImages": 450,
  "ImagesWithExif": 423,
  "TotalGalleries": 12,
  "CameraModels": [
    { "Label": "Sony ILCE-7M4", "Count": 142, "Percent": 100 },
    { "Label": "Canon EOS R5", "Count": 58, "Percent": 40 }
  ],
  "LensModels": [...],
  "FocalLengths": [...],
  "Apertures": [...],
  "IsoValues": [...],
  "ShutterSpeeds": [...],
  "ImagesByYear": [...],
  "GeneratedAt": "2026-02-12T10:30:00Z"
}
```

Each category entry has:

| Field | Type | Description |
|-------|------|-------------|
| `Label` | `string` | Display label (e.g., "f/1.4-2.0", "Sony A7IV") |
| `Count` | `int` | Number of images in this category |
| `Percent` | `int` | Percentage relative to the maximum count (0-100), used for bar width |

## Bucket Definitions

Continuous values are grouped into predefined ranges:

### Aperture (f-stop)

| Bucket | Range |
|--------|-------|
| f/1.0-1.4 | 1.0 ≤ f < 1.4 |
| f/1.4-2.0 | 1.4 ≤ f < 2.0 |
| f/2.0-2.8 | 2.0 ≤ f < 2.8 |
| f/2.8-4.0 | 2.8 ≤ f < 4.0 |
| f/4.0-5.6 | 4.0 ≤ f < 5.6 |
| f/5.6-8.0 | 5.6 ≤ f < 8.0 |
| f/8.0-11.0 | 8.0 ≤ f < 11.0 |
| f/11.0-16.0 | 11.0 ≤ f < 16.0 |
| f/16.0-22.0 | 16.0 ≤ f < 22.0 |
| f/22.0+ | 22.0+ |

### Focal Length

| Bucket | Range |
|--------|-------|
| 10-18mm | 0 ≤ mm < 18 |
| 18-35mm | 18 ≤ mm < 35 |
| 35-70mm | 35 ≤ mm < 70 |
| 70-135mm | 70 ≤ mm < 135 |
| 135-300mm | 135 ≤ mm < 300 |
| 300mm+ | 300+ |

### ISO

| Bucket | Range |
|--------|-------|
| ISO 50-100 | 0 ≤ ISO < 100 |
| ISO 100-400 | 100 ≤ ISO < 400 |
| ISO 400-800 | 400 ≤ ISO < 800 |
| ISO 800-1600 | 800 ≤ ISO < 1600 |
| ISO 1600-3200 | 1600 ≤ ISO < 3200 |
| ISO 3200-6400 | 3200 ≤ ISO < 6400 |
| ISO 6400+ | 6400+ |

Shutter speeds and camera/lens models use exact values (no bucketing).

## Theme Extension: Theme.Lumina.Statistics

The theme extension provides Scriban templates and CSS/JS assets for rendering statistics:

- **Chart.js** bar charts for all categories
- **Responsive** layout (mobile + desktop)
- **Dark/light mode** following the site theme
- Summary cards (total images, cameras, lenses)

```bash
revela plugin install Theme.Lumina.Statistics
```

Without the theme extension, the JSON data is still generated but no HTML visualization is rendered. Custom themes can consume `statistics.json` directly.

## Requirements

- Revela CLI v1.0.0 or later
- Photos with EXIF data (JPG, TIFF – most cameras write EXIF by default)
- Optional: Theme.Lumina.Statistics for Chart.js visualizations

## See Also

- [Plugin Management](../plugin-management.md) – Installing and managing plugins
- [Compression Plugin](compress.md) – Pre-compress static files

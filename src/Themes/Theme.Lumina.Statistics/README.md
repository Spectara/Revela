# Spectara.Revela.Theme.Lumina.Statistics

[![NuGet](https://img.shields.io/nuget/v/Spectara.Revela.Theme.Lumina.Statistics.svg)](https://www.nuget.org/packages/Spectara.Revela.Theme.Lumina.Statistics)

Statistics extension for the Lumina theme — displays EXIF statistics as a pure-CSS dashboard with bar charts.

## Prerequisites

This is a **theme extension** that requires:
- [Spectara.Revela.Theme.Lumina](https://www.nuget.org/packages/Spectara.Revela.Theme.Lumina) (default theme)
- [Spectara.Revela.Plugin.Statistics](https://www.nuget.org/packages/Spectara.Revela.Plugin.Statistics) (data generation)

## Installation

```bash
# Install statistics plugin (generates data)
revela plugin install Statistics

# Install theme extension (visualizes data)
revela plugin install Theme.Lumina.Statistics
```

## What It Adds

Extends the Lumina theme with a statistics dashboard page:

- 📊 **Overview Cards** — Total images, galleries, cameras, and lenses at a glance
- 📷 **Camera & Lens Charts** — Bar charts of your most-used gear
- ⚡ **Technical Distribution** — Aperture, ISO, shutter speed, and focal length breakdowns
- 🧭 **Orientation** — Landscape vs. portrait vs. square distribution
- 📅 **Timeline** — Photos per year and per month
- 🎨 **Pure CSS** — No JavaScript required, uses CSS custom properties for bar widths
- 🌙 **Dark Mode** — Inherits Lumina's color scheme automatically

## How It Works

The plugin generates a `statistics.json` data file during `revela generate all`. This theme extension provides a **Scriban template** (`statistics/overview`) that renders the JSON data into a single dashboard page with 9 chart sections.

All charts use a semantic `<dl>/<dt>/<dd>` structure with CSS `--percent` custom properties for bar widths — no JavaScript dependencies.

## Usage

```bash
# Full pipeline (scan → statistics → pages → images)
revela generate all
```

## Template Structure

```
Body/
└── overview.revela         # Main dashboard template

Partials/                   # Chart partials (included by overview)
├── cameras.revela          # Camera models
├── lenses.revela           # Lens models
├── focal-lengths.revela    # Focal length ranges (mm)
├── apertures.revela        # f-stop ranges
├── shutter-speeds.revela   # Exposure times
├── iso.revela              # ISO ranges
├── orientations.revela     # Landscape / Portrait / Square
├── timeline.revela         # Photos per year
└── months.revela           # Photos per month

Assets/
└── main.css                # Dashboard styles (cards, bar charts, responsive grid)
```

## Screenshots

*Coming soon*

## License

MIT — See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

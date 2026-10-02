# Spectara.Revela.Themes.Lumina.Statistics

Statistics extension for the Lumina theme — displays EXIF statistics as a pure-CSS dashboard with bar charts.

## Prerequisites

This is a **theme extension** that requires:
- `Spectara.Revela.Themes.Lumina` (default theme)
- `Spectara.Revela.Plugins.Statistics` (data generation)

All three packages are built into the Standalone build and bundled with the Full build. They are
not on NuGet.org; the `.nupkg` files are attached to each
[GitHub Release](https://github.com/spectara/revela/releases).

## Installation

In the Full build:

```bash
# Install statistics plugin (generates data)
revela plugin install Statistics

# Install theme extension (visualizes data) — it is a theme package
revela theme install Lumina.Statistics
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

The plugin generates a `statistics.json` data file during `revela generate all`. This theme extension provides a **Scriban template** (`statistics/overview`) that renders the JSON data into a single dashboard page: summary cards, nine bar charts and a month × year activity heatmap.

All charts use a semantic `<dl>/<dt>/<dd>` structure with CSS `--percent` custom properties for bar widths — no JavaScript dependencies.

## Usage

```bash
# Full pipeline (scan → statistics → pages → images)
revela generate all
```

## Template Structure

```
Body/
└── overview.revela         # Dashboard: summary cards, then one section per chart

Partials/                   # Included by overview.revela
├── bar-chart.revela        # One <dl> bar chart (cameras, lenses, focal lengths, apertures,
│                           #   shutter speeds, ISO, orientation, per year, per month)
└── heatmap.revela          # Photo activity per month and year

Locales/                    # UI strings (en, de), keys prefixed with "statistics."

Assets/
└── main.css                # Dashboard styles (cards, bar charts, heatmap), statistics pages only
```

## Screenshots

*Coming soon*

## License

MIT — See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

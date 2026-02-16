# Spectara.Revela.Plugin.Statistics

[![NuGet](https://img.shields.io/nuget/v/Spectara.Revela.Plugin.Statistics.svg)](https://www.nuget.org/packages/Spectara.Revela.Plugin.Statistics)

Generate EXIF statistics from your Revela photography site.

## Installation

```bash
revela plugin install Statistics
```

Or with full package name:
```bash
revela plugin install Spectara.Revela.Plugin.Statistics
```

## What It Does

Analyzes EXIF data from your photos and generates a `statistics.json` data file with:

- 📷 **Camera Models** — Which cameras you shoot with most
- 🔭 **Lens Models** — Your favorite lenses
- 🔍 **Focal Lengths** — Bucketed by photography ranges (18–35mm, 35–70mm, …)
- ⚡ **Apertures** — f-stop distribution
- 🎚️ **ISO** — Sensitivity ranges
- ⏱️ **Shutter Speeds** — Exposure time analysis
- 📅 **Timeline** — Photos per year
- 🗓️ **Monthly** — Photos per month (aggregated across years)
- 🧭 **Orientation** — Landscape vs. portrait vs. square

## Usage

```bash
# Full pipeline (scan → statistics → pages → images)
revela generate all

# Or run only the statistics step
revela generate statistics
```

## Output

Creates a `statistics.json` in the cache directory for each statistics page:

```
.cache/
└── {page-path}/
    └── statistics.json     # Statistics data consumed by theme templates
```

The JSON is rendered into HTML by a theme extension (see below).

## Configuration

Settings in `project.json`:

```json
{
  "Spectara.Revela.Plugin.Statistics": {
    "MaxEntriesPerCategory": 15,
    "SortByCount": true
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `MaxEntriesPerCategory` | `15` | Top N entries per category (0 = unlimited). Remaining entries are aggregated into "Other". |
| `SortByCount` | `true` | Sort by count (descending) instead of natural order |

```bash
# Configure interactively
revela config statistics

# Or set specific options
revela config statistics --max-entries 20 --sort-by-count false
```

## Theme Extension

For a ready-made dashboard with pure-CSS bar charts, install the matching theme extension:

```bash
revela plugin install Theme.Lumina.Statistics
```

This provides:
- Overview cards (totals at a glance)
- Bar charts for all 9 categories
- Responsive layout, dark/light mode
- No JavaScript required

> Without the theme extension, statistics data is still generated — but you'll need custom templates to display it.

## CLI Commands

| Command | Description |
|---------|-------------|
| `revela generate statistics` | Generate statistics JSON |
| `revela generate all` | Full pipeline (includes statistics) |
| `revela clean statistics` | Remove generated statistics files |
| `revela config statistics` | Configure plugin settings |

## Requirements

- Photos with EXIF data (JPG, TIFF — most cameras write EXIF by default)
- Run `revela generate scan` first (or use `generate all`)

## License

MIT — See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

# Spectara.Revela.Plugin.Statistics

[![NuGet](https://img.shields.io/nuget/v/Spectara.Revela.Plugin.Statistics.svg)](https://www.nuget.org/packages/Spectara.Revela.Plugin.Statistics)

Generate EXIF statistics pages for your Revela photography site.

## Installation

```bash
revela plugin install Statistics
```

Or with full package name:
```bash
revela plugin install Spectara.Revela.Plugin.Statistics
```

## What It Does

Analyzes EXIF data from your photos and generates statistics pages showing:

- 📷 **Camera Usage** - Which cameras you shoot with most
- 🔭 **Lens Statistics** - Your favorite lenses
- ⚡ **Aperture Distribution** - f/stop preferences
- 🎚️ **ISO Distribution** - Sensitivity patterns
- ⏱️ **Shutter Speed** - Exposure time analysis
- 📅 **Timeline** - Photos over time

## Usage

```bash
# Generate statistics after site generation
revela generate
revela statistics generate

# Or as part of your workflow
revela generate && revela statistics generate
```

## Output

Creates a `statistics/` folder in your output directory with:

```
output/
├── statistics/
│   ├── index.html      # Main statistics dashboard
│   ├── cameras.html    # Camera breakdown
│   ├── lenses.html     # Lens breakdown
│   └── data.json       # Raw statistics data
```

## Theme Support

For beautiful charts and styling, install the matching theme extension:

```bash
revela plugin install Theme.Lumina.Statistics
```

This adds:
- Chart.js visualizations
- Responsive design
- Dark/light mode support

## Requirements

- Revela CLI v1.0.0 or later
- Photos with EXIF data
- Optional: Theme.Lumina.Statistics for enhanced display

## License

MIT - See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

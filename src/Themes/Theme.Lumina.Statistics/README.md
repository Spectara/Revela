# Spectara.Revela.Theme.Lumina.Statistics

[![NuGet](https://img.shields.io/nuget/v/Spectara.Revela.Theme.Lumina.Statistics.svg)](https://www.nuget.org/packages/Spectara.Revela.Theme.Lumina.Statistics)

Statistics extension for the Lumina theme - displays EXIF statistics with beautiful charts.

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

Extends the Lumina theme with:

- 📊 **Interactive Charts** - Powered by Chart.js
- 📷 **Camera Stats** - Pie chart of camera usage
- 🔭 **Lens Stats** - Bar chart of lens preferences
- ⚡ **Aperture/ISO** - Distribution histograms
- 🎨 **Consistent Styling** - Matches Lumina design
- 🌙 **Dark Mode** - Charts adapt to theme

## Usage

```bash
# 1. Generate your site
revela generate

# 2. Generate statistics
revela statistics generate

# Statistics pages automatically use the enhanced templates
```

## Output

Enhances the statistics pages with:

```
output/
└── statistics/
    ├── index.html      # Dashboard with overview charts
    ├── cameras.html    # Detailed camera statistics
    ├── lenses.html     # Detailed lens statistics
    └── charts.js       # Chart.js visualizations
```

## Chart Types

| Statistic | Chart Type |
|-----------|------------|
| Cameras | Doughnut chart |
| Lenses | Horizontal bar |
| Aperture | Histogram |
| ISO | Histogram |
| Shutter Speed | Histogram |
| Timeline | Line chart |

## Customization

The extension respects Lumina theme options:

```json
{
  "theme": {
    "options": {
      "accentColor": "#007bff",
      "chartStyle": "minimal"
    }
  }
}
```

## Screenshots

*Coming soon*

## License

MIT - See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

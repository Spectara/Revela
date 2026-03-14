# Spectara.Revela.Themes.Lumina.Calendar

Availability calendar extension for the Lumina theme.

## What it provides

- **Template:** `body/calendar/page.revela` — Month grid with CSS-styled day states
- **CSS:** Responsive calendar grid, legend, arrive/depart diagonals
- **Data defaults:** Auto-loads `calendar.json` for `calendar/page` template

## Day CSS classes

| Class | Visual | Used in |
|-------|--------|---------|
| `free` | Green tint | Both modes |
| `booked` | Red tint | Both modes |
| `arrive` | Diagonal ↘ (green→red) | Nights mode |
| `depart` | Diagonal ↗ (red→green) | Nights mode |
| `past` | Dimmed | Both modes |
| `today` | Accent outline | Both modes |

## Related Packages

- **Spectara.Revela.Plugins.Calendar** — Generates calendar.json from iCal data
- **Spectara.Revela.Plugins.Source.Calendar** — Fetches iCal feeds from URLs

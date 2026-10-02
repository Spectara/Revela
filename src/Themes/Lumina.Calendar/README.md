# Spectara.Revela.Themes.Lumina.Calendar

Availability calendar extension for the Lumina theme.

## What it provides

- **Template:** `calendar/page` (`Body/page.revela`) — the page's Markdown body, a legend and a month grid
- **CSS:** `calendar.css`, loaded only on calendar pages — responsive month grid, legend, arrive/depart diagonals
- **Data defaults:** `calendar/page` loads `calendar.json` automatically

Use it in a page's front matter:

```text
+++
title = "Availability"
template = "calendar/page"
calendar.source = "bookings.ics"
+++
```

## Day CSS classes

Day states are monochrome: tints of the text color (`currentColor`), so they follow
Lumina's light and dark scheme.

| Class | Visual | Used in |
|-------|--------|---------|
| `free` | Light tint | Both modes |
| `booked` | Strong tint | Both modes |
| `arrive` | Diagonal split, light → strong | Nights mode |
| `depart` | Diagonal split, strong → light | Nights mode |
| `past` | Dimmed | Both modes |
| `today` | Outline | Both modes |

## Related Packages

- **Spectara.Revela.Plugins.Calendar** — Generates calendar.json from iCal data
- **Spectara.Revela.Plugins.Source.Calendar** — Fetches iCal feeds from URLs

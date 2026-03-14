# Spectara.Revela.Plugins.Calendar

Availability calendar plugin for Revela — generates calendar data from iCal (RFC 5545) files.

## What it does

Reads local `.ics` files and produces `calendar.json` with month/week/day structures
for Scriban template rendering. Designed for vacation rental availability calendars.

## Usage

1. Place a `.ics` file in your page's source directory (manually or via `Source.Calendar` plugin)
2. Create an `_index.revela` with calendar frontmatter:

```
+++
title = "Availability"
template = "calendar/page"
data.calendar = "calendar.json"

calendar.source = "bookings.ics"
calendar.months = 12
calendar.locale = "de"
calendar.labels.booked = "belegt"
calendar.labels.free = "frei"
calendar.labels.arrive = "Anreise"
calendar.labels.depart = "Abreise"
+++
```

3. Run `revela generate all` or `revela generate calendar`

## Related Packages

- **Spectara.Revela.Plugins.Source.Calendar** — Fetches iCal feeds from URLs
- **Spectara.Revela.Themes.Lumina.Calendar** — Calendar template and CSS for the Lumina theme

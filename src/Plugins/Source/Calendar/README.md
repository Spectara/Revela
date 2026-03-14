# Spectara.Revela.Plugins.Source.Calendar

Fetches iCal (RFC 5545) feeds from URLs and saves them to the source directory.

## Configuration

In `project.json`:

```json
{
  "Spectara.Revela.Plugins.Source.Calendar": {
    "feeds": {
      "booking": {
        "url": "https://ical.booking.com/v1/export/t/xxx.ics",
        "output": "availability/bookings.ics"
      },
      "google": {
        "url": "https://calendar.google.com/calendar/ical/xxx/public/basic.ics",
        "output": "schedule/schedule.ics"
      }
    }
  }
}
```

## Usage

```bash
# Fetch all configured feeds
revela source calendar fetch

# Fetch a single feed by name
revela source calendar fetch --name booking
```

## Related Packages

- **Spectara.Revela.Plugins.Calendar** — Parses .ics files and generates calendar data
- **Spectara.Revela.Themes.Lumina.Calendar** — Calendar template and CSS for Lumina theme

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

## Failure and Safety Boundaries

- Each selected feed needs a unique relative output file below the configured
  source directory. Absolute/escaping paths, Windows alternate-stream syntax,
  linked components, conflicting ancestor/descendant outputs, file parents and directory destinations are rejected before
  any selected download starts. The configured source root itself is trusted.
- Downloads stream into a new temporary file beside the destination. Only a
  completed download replaces the existing file. HTTP errors, body failures,
  cancellation and the configured HTTP-client timeout preserve the previous file.
  If temporary cleanup fails, a sanitized warning is emitted without replacing
  the primary exception; the leftover temporary file may need manual removal.
- Redirects are handled explicitly: each target is checked, at most five redirects
  are followed, and HTTPS cannot redirect to HTTP. Cookies and default HTTP-client
  URL logging are disabled for this client; feed URL paths/queries may contain tokens.
- A selected feed failure makes the command fail even when another selected feed
  succeeds. Successful files are not rolled back: this is not a multi-file transaction.
  User cancellation propagates; other expected failures name the feed without
  printing the remote response's exception text or credentials.
- The network checks reject literal private/local targets but do not resolve DNS
  for sandboxing or prevent a hostile concurrent filesystem actor from changing
  paths. Use trusted configuration and deployment-level outbound restrictions.
- Fetch success confirms transport, not valid/current availability. The Calendar
  plugin validates the downloaded iCalendar data during check/generation. No private
  feed is required for tests: the regression suite uses local mock HTTP responses.

## Related Packages

- **Spectara.Revela.Plugins.Calendar** — Parses .ics files and generates calendar data
- **Spectara.Revela.Themes.Lumina.Calendar** — Calendar template and CSS for Lumina theme

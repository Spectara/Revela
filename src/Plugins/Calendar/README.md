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

`calendar.json` lives in `.cache/<page>/` and is derived from the manifest: a rescan
(`generate scan`) removes it, and `generate calendar` rebuilds it for every calendar
page and removes the files of pages that no longer exist. `revela clean calendar`
removes all of them; symbolic links and junctions inside `.cache` are never followed.

## Invalid and Empty Calendars

A complete `BEGIN:VCALENDAR` / `END:VCALENDAR` document with no events is a valid
empty calendar. It generates free future dates. Missing files, malformed calendar
boundaries, and incomplete or invalid booking events fail the generation step;
they are never silently interpreted as no bookings. All pages are read before
anything is written, so when one page fails, every previous `calendar.json` is kept.

The supported booking format is individual all-day events with one `DTSTART` and
one later `DTEND` in `YYYYMMDD` format, optionally with `;VALUE=DATE`. Folded lines
are supported. Recurrence rules/dates/exceptions, `DURATION`, and timed bookings are rejected
instead of partially represented. Re-export them as individual all-day bookings.
Only event components (with optional alarms) and time-zone components are accepted;
unknown or malformed component names are errors rather than silently ignored bookings.
This is a booking-feed subset, not a general-purpose RFC 5545 validator.

`revela check calendar` uses the same parser and reports the same problems up front;
it does not block `generate`. Neither a successful HTTP download
nor an empty calendar establishes that the provider's data is fresh or complete;
deployment monitoring and feed credentials remain separate concerns.

## Related Packages

- **Spectara.Revela.Plugins.Source.Calendar** — Fetches iCal feeds from URLs
- **Spectara.Revela.Themes.Lumina.Calendar** — Calendar template and CSS for the Lumina theme

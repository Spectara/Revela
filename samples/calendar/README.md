# Calendar Demo Sample

Demonstrates the **Calendar plugin** with two pages:

## Pages

### Availability (Nights Mode)
- `source/availability/` — Vacation rental calendar with arrive/depart diagonals
- German labels, 12 months
- Includes a changeover booking (Aug 15: one guest departs, next arrives)

### Schedule (Days Mode)  
- `source/schedule/` — Photographer booking calendar
- Whole days booked, no arrive/depart distinction
- English labels, 6 months

## Test Data

Both pages include `.ics` files with sample bookings. The iCal data covers:
- Single day bookings
- Multi-day bookings
- Consecutive bookings (changeover day)
- Cross-year bookings (Christmas → New Year)

## Running

```bash
cd samples/calendar
dotnet run --project ../../src/Cli -- generate calendar
```

This generates `calendar.json` files in `.cache/` — the data pipeline input for `generate pages`.

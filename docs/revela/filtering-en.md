# Filter Galleries

## Overview

Revela supports **virtual galleries** that automatically select images from a shared pool using filter expressions. Instead of manually organizing photos into folders, you define criteria and Revela builds the gallery dynamically.

**Key Benefits:**
- **Single source of truth** - Images stored once in `_images/` folder
- **Dynamic galleries** - Automatically update when images change
- **Powerful queries** - Filter by EXIF data, filename, date, and more
- **Pipe syntax** - Chain sort and limit operations

## Basic Setup

### 1. Create Shared Images Folder

Create an `_images/` folder (note the underscore prefix) in your source directory:

```
source/
├── _images/           # Shared images (not a gallery itself)
│   ├── photo-001.jpg
│   ├── photo-002.jpg
│   └── ...
├── canon/             # Filter gallery
│   └── _index.revela
├── sony/              # Filter gallery
│   └── _index.revela
└── _index.revela      # Homepage
```

### 2. Add Filter Expression

In each gallery's `_index.revela`, add a `filter` property:

```toml
+++
title = "Canon Photos"
filter = "exif.make == 'Canon'"
+++

All photos taken with Canon cameras.
```

## Filter Syntax

### Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `==` | Equal | `exif.make == 'Canon'` |
| `!=` | Not equal | `exif.make != 'Sony'` |
| `<` | Less than | `exif.iso < 800` |
| `<=` | Less than or equal | `exif.focalLength <= 35` |
| `>` | Greater than | `exif.iso > 1600` |
| `>=` | Greater than or equal | `exif.iso >= 3200` |

### Logical Operators

Combine conditions with `and`, `or`, and `not`:

```toml
# Both conditions must be true
filter = "exif.make == 'Canon' and exif.iso >= 800"

# Either condition can be true
filter = "exif.make == 'Canon' or exif.make == 'Sony'"

# Negate a condition
filter = "not exif.make == 'Canon'"
```

**Precedence:** `and` binds tighter than `or`. Use parentheses to change order:

```toml
# Evaluated as: a or (b and c)
filter = "exif.make == 'Canon' or exif.make == 'Sony' and exif.iso >= 800"

# Evaluated as: (a or b) and c
filter = "(exif.make == 'Canon' or exif.make == 'Sony') and exif.iso >= 800"
```

## Available Properties

### Image Properties

| Property | Type | Description |
|----------|------|-------------|
| `filename` | string | Image filename (e.g., `photo-001.jpg`) |
| `sourcePath` | string | Full path within source directory |
| `width` | int | Image width in pixels |
| `height` | int | Image height in pixels |
| `fileSize` | long | File size in bytes |
| `dateTaken` | DateTime | Date photo was taken |

### EXIF Properties

Access via `exif.` prefix:

| Property | Type | Description |
|----------|------|-------------|
| `exif.make` | string | Camera manufacturer |
| `exif.model` | string | Camera model |
| `exif.lensModel` | string | Lens name |
| `exif.fNumber` | double | Aperture (f/2.8 = 2.8) |
| `exif.exposureTime` | double | Shutter speed in seconds |
| `exif.iso` | int | ISO sensitivity |
| `exif.focalLength` | int | Focal length in mm |
| `exif.dateTaken` | DateTime | EXIF date/time |
| `exif.gpsLatitude` | double? | GPS latitude |
| `exif.gpsLongitude` | double? | GPS longitude |

### Raw EXIF Access

Access any EXIF tag via `exif.raw.TagName`:

```toml
filter = "exif.raw.Software == 'Lightroom'"
filter = "exif.raw.Artist == 'John Doe'"
filter = "exif.raw.Rating >= 4"
```

## Built-in Functions

### Date Functions

| Function | Description | Example |
|----------|-------------|---------|
| `year(date)` | Extract year | `year(dateTaken) == 2024` |
| `month(date)` | Extract month (1-12) | `month(dateTaken) == 12` |
| `day(date)` | Extract day (1-31) | `day(dateTaken) == 25` |

### String Functions

| Function | Description | Example |
|----------|-------------|---------|
| `contains(str, substr)` | Check if contains | `contains(filename, 'portrait')` |
| `starts_with(str, prefix)` | Check prefix | `starts_with(filename, 'IMG_')` |
| `ends_with(str, suffix)` | Check suffix | `ends_with(filename, '-edit.jpg')` |
| `lower(str)` | To lowercase | `lower(exif.make) == 'canon'` |
| `upper(str)` | To uppercase | `upper(exif.make) == 'CANON'` |

## Pipe Syntax

Chain operations using the pipe `|` operator:

```
filter_expression | sort property [asc|desc] | limit n
```

### The `all` Keyword

Select all images without filtering:

```toml
filter = "all"
filter = "all | sort dateTaken desc"
filter = "all | sort dateTaken desc | limit 10"
```

### Sort Clause

Sort results by any property:

```toml
# Newest first
filter = "all | sort dateTaken desc"

# Alphabetically by filename
filter = "all | sort filename asc"

# By EXIF property
filter = "exif.make == 'Canon' | sort exif.iso desc"
```

**Default direction:** `asc` (ascending)

### Limit Clause

Restrict the number of results:

```toml
# Only 5 images
filter = "all | limit 5"

# 10 newest photos
filter = "all | sort dateTaken desc | limit 10"
```

### Combined Example

```toml
# Top 5 high-ISO Canon photos, newest first
filter = "exif.make == 'Canon' and exif.iso >= 1600 | sort dateTaken desc | limit 5"
```

## Literal Values

### Strings

Use single or double quotes:

```toml
filter = "filename == 'test.jpg'"
filter = "filename == \"test.jpg\""
```

### Numbers

```toml
filter = "exif.iso == 800"       # Integer
filter = "exif.fNumber == 2.8"   # Decimal
```

### Null

Check for missing values:

```toml
filter = "exif.gpsLatitude != null"  # Has GPS data
filter = "exif.lensModel == null"    # No lens info
```

## Common Patterns

### By Camera Brand

```toml
filter = "exif.make == 'Canon'"
filter = "exif.make == 'Sony'"
filter = "exif.make == 'Nikon'"
filter = "exif.make == 'Canon' or exif.make == 'Sony'"
```

### By Year

```toml
filter = "year(dateTaken) == 2024"
filter = "year(dateTaken) >= 2020 and year(dateTaken) <= 2024"
```

### High ISO (Low Light)

```toml
filter = "exif.iso >= 3200"
filter = "exif.iso >= 1600 | sort exif.iso desc"
```

### By Filename Pattern

```toml
filter = "contains(filename, 'portrait')"
filter = "contains(lower(filename), 'portrait')"
filter = "starts_with(filename, 'IMG_')"
```

### Wide Angle / Telephoto

```toml
filter = "exif.focalLength <= 35"   # Wide angle
filter = "exif.focalLength >= 85"   # Portrait/Telephoto
filter = "exif.focalLength >= 200"  # Super telephoto
```

### Recent Photos (Homepage)

```toml
filter = "all | sort dateTaken desc | limit 5"
```

### Best Rated

```toml
filter = "exif.raw.Rating >= 4 | sort exif.raw.Rating desc"
```

## Filter vs Sort

| Feature | Filter (`filter =`) | Sort (`sort =`) |
|---------|---------------------|-----------------|
| Purpose | Select images from `_images/` | Order images in any gallery |
| Scope | Creates virtual gallery | Works on existing images |
| Syntax | Expression language | `field:direction` |
| Use case | Dynamic collections | Custom ordering |

**Filter galleries** pull images from `_images/`. **Sort** orders whatever images a gallery already has.

You can use both together:

```toml
+++
filter = "exif.make == 'Canon'"
sort = "dateTaken:desc"
+++
```

## Error Handling

Invalid expressions show helpful error messages:

```
Filter parse error at position 15: Unexpected token 'xyz'
Expression: exif.make == xyz
                         ^^^
```

Use `revela generate scan` to validate filters before full generation.

## See Also

- [Sorting Configuration](sorting-en.md) - Gallery and image sorting
- [Pages Documentation](pages-en.md) - Content pages without images

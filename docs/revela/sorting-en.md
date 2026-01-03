# Sorting Configuration

## Overview

Revela supports flexible sorting of galleries and images. You can configure:
- **Gallery order** in navigation (ascending/descending)
- **Image order** within galleries (by any field including EXIF data)
- **Per-gallery overrides** via front matter

## Global Configuration (project.json)

```jsonv
{
  "generate": {
    "sorting": {
      "galleries": "asc",
      "images": {
        "field": "dateTaken",
        "direction": "desc",
        "fallback": "filename"
      }
    }
  }
}
```

| Property | Description | Default |
|----------|-------------|---------|
| `galleries` | Gallery sort direction: `asc` or `desc` | `asc` |
| `images.field` | Field to sort images by | `dateTaken` |
| `images.direction` | Image sort direction: `asc` or `desc` | `desc` |
| `images.fallback` | Fallback field when primary is null | `filename` |

## Available Sort Fields

| Field | Description |
|-------|-------------|
| `filename` | File name (alphabetical) |
| `dateTaken` | EXIF capture date |
| `exif.focalLength` | Focal length in mm |
| `exif.fNumber` | Aperture (f-number) |
| `exif.exposureTime` | Shutter speed |
| `exif.iso` | ISO sensitivity |
| `exif.make` | Camera manufacturer |
| `exif.model` | Camera model |
| `exif.lensModel` | Lens model |
| `exif.raw.Rating` | Star rating (1-5) |
| `exif.raw.Copyright` | Copyright field |
| `exif.raw.{FieldName}` | Any EXIF field from Raw dictionary |

## Per-Gallery Override (Front Matter)

Override the global sort settings for individual galleries using front matter in `_index.revela`:

**Format:**
```
sort = "field"           # Use field, direction from global config
sort = "field:asc"       # Use field with ascending order
sort = "field:desc"      # Use field with descending order
```

**Examples:**

```toml
+++
title = "Lens Comparison"
sort = "exif.focalLength:asc"
+++

Compare shots from wide-angle to telephoto.
```

```toml
+++
title = "Best Shots"
sort = "exif.raw.Rating:desc"
+++

My highest rated photos.
```

```toml
+++
title = "Timeline"
sort = "dateTaken:asc"
+++

Photos in chronological order (oldest first).
```

```toml
+++
title = "Latest Work"
sort = "dateTaken:desc"
+++

Most recent photos first.
```

## CLI Configuration

Configure sorting interactively or via command line:

```bash
# Interactive wizard
revela config sorting

# Set image sort field
revela config sorting --field dateTaken --direction desc

# Sort by rating
revela config sorting --field exif.raw.Rating --direction desc

# Sort by focal length
revela config sorting --field exif.focalLength --direction asc

# Change gallery order
revela config sorting --galleries desc
```

## Logic Flow

1. **No front matter `sort`** → Use global config (`generate.sorting.images`)
2. **`sort = "field"`** → Override field, keep global direction
3. **`sort = "field:direction"`** → Override both field and direction
4. **Fallback** is always from global config (not overridable per gallery)
5. **Final tie-breaker** is always filename (for stable sorting)

## Technical Details

### Configuration Binding

The `SortDirection` enum uses short names for IConfiguration binding compatibility:

```csharp
public enum SortDirection
{
    Asc,   // JSON: "asc"
    Desc   // JSON: "desc"
}
```

### EXIF Raw Dictionary

Additional EXIF fields are extracted into `ExifData.Raw` dictionary:

- Rating, Copyright, Artist
- ExposureProgram, MeteringMode, Flash
- WhiteBalance, SceneCaptureType
- FocalLengthIn35mmFormat
- And ~30 more photographer-relevant fields

Only non-empty values are stored to keep the manifest compact.

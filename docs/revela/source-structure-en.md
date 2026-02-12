# Source Folder Structure

## Overview

Revela supports two approaches for organizing your photos, and you can combine them in the same project:

| Approach | Best For | Images Stored In |
|----------|----------|------------------|
| **Traditional Galleries** | Photos belong to one gallery | Gallery folder |
| **Filter Galleries** | Photos appear in multiple galleries | `_images/` folder |
| **Hybrid** | Mix of both approaches | Both locations |

## Traditional Galleries

Each gallery contains its own images directly in the folder.

### Structure

```
source/
├── _index.revela          # Homepage
├── events/
│   ├── _index.revela      # Gallery page
│   ├── event-001.jpg
│   ├── event-002.jpg
│   └── event-003.jpg
├── portraits/
│   ├── _index.revela
│   ├── portrait-001.jpg
│   └── portrait-002.jpg
└── landscapes/
    ├── _index.revela
    ├── mountain.jpg
    └── ocean.jpg
```

### Characteristics

- ✅ Simple and intuitive
- ✅ Each photo belongs to exactly one gallery
- ✅ Easy to manage manually
- ❌ Photos cannot appear in multiple galleries
- ❌ Reorganizing requires moving files

### Example Front Matter

```toml
+++
title = "Events 2025"
description = "Photos from various events"
+++

A collection of event photography.
```

## Filter Galleries

All images are stored in a shared `_images/` folder. Galleries use filter expressions to select which images to display.

### Structure

```
source/
├── _index.revela          # Homepage (can also use filter)
├── _images/               # Shared image pool (underscore prefix!)
│   ├── canon-event-001.jpg
│   ├── canon-portrait-002.jpg
│   ├── sony-landscape-003.jpg
│   └── sony-portrait-004.jpg
├── canon/
│   └── _index.revela      # filter = "exif.make == 'Canon'"
├── sony/
│   └── _index.revela      # filter = "exif.make == 'Sony'"
├── portraits/
│   └── _index.revela      # filter = "contains(filename, 'portrait')"
└── landscapes/
    └── _index.revela      # filter = "contains(filename, 'landscape')"
```

### Characteristics

- ✅ Same image can appear in multiple galleries
- ✅ Galleries update automatically when images change
- ✅ Powerful queries based on EXIF, filename, date
- ✅ Great for cross-cutting categories (by camera, by year, etc.)
- ❌ Requires consistent naming or EXIF data
- ❌ More abstract organization

### The `_images/` Folder

The underscore prefix is significant:
- `_images/` is **not** rendered as a gallery itself
- Images are accessible via filter expressions and content images (`![](path)`)
- Subdirectories are supported: `_images/screenshots/`, `_images/portfolio/`

### Example Front Matter

```toml
+++
title = "Canon Photos"
description = "All photos taken with Canon cameras"
filter = "exif.make == 'Canon'"
+++

My Canon camera collection.
```

## Hybrid Approach

Combine both approaches in the same project. This is powerful for sites where some content is exclusive and other content is cross-referenced.

### Structure

```
source/
├── _index.revela              # Homepage with recent photos
├── _images/                   # Shared pool for filter galleries
│   ├── 2024-trip-001.jpg
│   ├── 2024-trip-002.jpg
│   ├── 2025-event-001.jpg
│   └── 2025-event-002.jpg
│
├── by-camera/                 # Filter galleries (virtual)
│   ├── _index.revela          # Category page
│   ├── canon/
│   │   └── _index.revela      # filter = "exif.make == 'Canon'"
│   └── sony/
│       └── _index.revela      # filter = "exif.make == 'Sony'"
│
├── by-year/                   # Filter galleries (virtual)
│   ├── _index.revela
│   ├── 2024/
│   │   └── _index.revela      # filter = "year(dateTaken) == 2024"
│   └── 2025/
│       └── _index.revela      # filter = "year(dateTaken) == 2025"
│
├── clients/                   # Traditional galleries (exclusive)
│   ├── _index.revela
│   ├── wedding-smith/
│   │   ├── _index.revela
│   │   ├── ceremony-001.jpg   # Only in this gallery
│   │   └── reception-002.jpg
│   └── corporate-abc/
│       ├── _index.revela
│       └── headshot-001.jpg   # Only in this gallery
│
└── personal/                  # Traditional gallery
    ├── _index.revela
    ├── family-001.jpg
    └── vacation-002.jpg
```

### How It Works

1. **`_images/`** contains photos you want to categorize multiple ways
2. **Filter galleries** (`by-camera/`, `by-year/`) query the `_images/` pool
3. **Traditional galleries** (`clients/`, `personal/`) have their own exclusive images
4. **No overlap** - Traditional gallery images are not in `_images/`

### Characteristics

- ✅ Best of both worlds
- ✅ Client work stays organized and exclusive
- ✅ Personal/portfolio work can be cross-referenced
- ✅ Flexible organization per use case
- ⚠️ Requires clear mental model of which images go where

## Choosing the Right Approach

| Scenario | Recommended Approach |
|----------|---------------------|
| Simple portfolio with distinct categories | Traditional |
| Photos need multiple categorizations | Filter |
| Client work (exclusive galleries) | Traditional |
| "By camera", "By year" views | Filter |
| Homepage with "Recent Photos" | Filter (`all \| sort dateTaken desc \| limit 5`) |
| Mix of exclusive and shared content | Hybrid |

## Common Patterns

### Homepage with Recent Photos

```toml
+++
title = "Welcome"
filter = "all | sort dateTaken desc | limit 6"
+++

My latest work.
```

### Category Landing Page (No Images)

```toml
+++
title = "Browse by Camera"
template = "page"
+++

Select a camera brand below.
```

### Nested Filter Galleries

```
source/
├── _images/
├── gear/
│   ├── _index.revela          # template = "page" (no images)
│   ├── cameras/
│   │   ├── _index.revela      # template = "page"
│   │   ├── canon/
│   │   │   └── _index.revela  # filter = "exif.make == 'Canon'"
│   │   └── sony/
│   │       └── _index.revela  # filter = "exif.make == 'Sony'"
│   └── lenses/
│       ├── _index.revela      # template = "page"
│       ├── wide/
│       │   └── _index.revela  # filter = "exif.focalLength <= 35"
│       └── tele/
│           └── _index.revela  # filter = "exif.focalLength >= 85"
```

## Migration Tips

### From Traditional to Filter

1. Create `_images/` folder
2. Move images from galleries to `_images/`
3. Add `filter = "..."` to each gallery's `_index.revela`
4. Ensure images have proper EXIF data or naming convention

### Adding Filter Galleries to Existing Site

1. Create `_images/` folder
2. Copy (or move) images you want cross-referenced
3. Create new filter gallery folders with `_index.revela`
4. Keep traditional galleries unchanged

## See Also

- [Filter Galleries](filtering-en.md) - Complete filter syntax reference
- [Sorting Configuration](sorting-en.md) - Image and gallery sorting
- [Creating Pages](pages-en.md) - Page types and templates
- [Static Files](static-files-en.md) - Favicons, robots.txt, and other static assets

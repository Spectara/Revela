# Expose.NET Sample Sites

This directory contains example sites for testing and demonstration purposes.

## Samples

### 1. Minimal (`minimal/`)

A minimal photography site with basic configuration.

**Structure:**
```
minimal/
├── expose.json          # Site configuration
├── content/            # Content directory
│   ├── _index.md      # Homepage content
│   └── gallery/       # Gallery directory
│       ├── _index.md  # Gallery metadata
│       └── *.jpg      # Photos
└── themes/
    └── default/       # Theme templates
        ├── layout.html
        ├── index.html
        └── gallery.html
```

**Usage:**
```bash
# Generate site
expose generate -p samples/minimal

# Output will be in samples/minimal/output/
```

### 2. Portfolio (`portfolio/`) - TODO

Full-featured photographer portfolio with:
- Multiple galleries
- About page
- Contact form
- Blog posts

### 3. Blog (`blog/`) - TODO

Photo blog with:
- Chronological posts
- Tags & categories
- RSS feed

## Test Data

Sample images for testing are stored in `../test-data/images/`:
- Small images (< 100KB) for fast unit tests
- Images with EXIF data for metadata extraction
- Various formats (JPEG, PNG) for format conversion tests

**Note:** Large test images are tracked with Git LFS.

## Adding New Samples

1. Create a new directory under `samples/`
2. Add `expose.json` configuration
3. Create `content/` directory with images and markdown
4. Add theme templates if needed
5. Document in this README

## Usage in Tests

```csharp
// Integration tests can reference samples
var samplePath = Path.Combine(
    TestContext.CurrentContext.TestDirectory,
    "..", "..", "..", "..", "samples", "minimal");

var generator = new SiteGenerator(samplePath);
await generator.GenerateAsync();

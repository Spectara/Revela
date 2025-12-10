# Revela Sample Sites

This directory contains example sites for testing and demonstration purposes.

## Samples

### 1. OneDrive (`onedrive/`)

Sample project for testing the OneDrive Source Plugin. Downloads images from a shared OneDrive folder.

**Structure:**
```
onedrive/
├── plugins/
│   └── Spectara.Revela.Plugin.Source.OneDrive.json  # OneDrive plugin configuration
├── project.json        # Project configuration
├── site.json           # Site metadata
├── source/             # Downloaded images (gitignored)
└── output/             # Generated site (gitignored)
```

**Usage:**
```bash
# Download images from OneDrive share
revela source onedrive download -p samples/onedrive

# Generate site
revela generate -p samples/onedrive
```

> **Note:** The `source/` and `output/` folders are excluded from Git.
> Run `revela source onedrive download` to populate them.

### 2. Subdirectory (`subdirectory/`)

Sample project demonstrating nested gallery structure with sub-galleries.

### 3. CDN (`cdn/`)

Sample project demonstrating CDN configuration for serving images from external URLs.

### 4. Portfolio (`portfolio/`) - TODO

Full-featured photographer portfolio with:
- Multiple galleries
- About page
- Contact form
- Blog posts

### 5. Blog (`blog/`) - TODO

Photo blog with:
- Chronological posts
- Tags & categories
- RSS feed

## Git Exclusions

The following paths are excluded from version control:

```gitignore
samples/**/source/    # Downloaded/input images
samples/**/output/    # Generated output
```

This keeps the repository small while allowing real test data via plugins.

## Adding New Samples

1. Create a new directory under `samples/`
2. Add `project.json` configuration
3. Add `site.json` with site metadata
4. Create `source/` directory with images (or use a source plugin)
5. Document in this README

## Usage in Tests

```csharp
// Integration tests can reference samples
var samplePath = Path.Combine(
    TestContext.CurrentContext.TestDirectory,
    "..", "..", "..", "..", "samples", "onedrive");

// Use ISiteGenerationService (resolve from DI)
var service = serviceProvider.GetRequiredService<ISiteGenerationService>();
await service.GenerateAsync(samplePath, CancellationToken.None);
```

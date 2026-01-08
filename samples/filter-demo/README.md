# Filter Demo Sample

Demonstrates **virtual galleries** using filter expressions and the shared `_images/` folder.

## Features Demonstrated

1. **Shared Images Folder** (`source/_images/`)
   - Images available for filtering but not displayed as their own gallery
   - Supports subdirectories for organization

2. **Filter Galleries** (using Scriban frontmatter `+++`)
   - `01 Canon Only/` - `filter = "exif.make == 'Canon'"` (EXIF-based)
   - `02 Sony Only/` - `filter = "exif.make == 'Sony'"` (EXIF-based)
   - `03 Landscapes/` - `filter = "width > height"` (dimension-based)
   - `04 Portraits/` - `filter = "height > width"` (dimension-based)
   - `05 Night Photos/` - `filter = "exif.iso >= 3200"` (numeric EXIF)

## Setup

### 1. Generate Test Images (Windows only)

```powershell
# From repository root
$outDir = "samples/filter-demo/source/_images"

# Canon images
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/canon-landscape-001.jpg" -Width 1920 -Height 1280 -CameraMake Canon -CameraModel "EOS R5" -ISO 100
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/canon-landscape-002.jpg" -Width 1920 -Height 1280 -CameraMake Canon -CameraModel "EOS R5" -ISO 200
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/canon-portrait-001.jpg" -Width 1280 -Height 1920 -CameraMake Canon -CameraModel "EOS R6" -ISO 800
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/canon-night-001.jpg" -Width 1920 -Height 1280 -CameraMake Canon -CameraModel "EOS R5" -ISO 3200

# Sony images
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/sony-landscape-001.jpg" -Width 1920 -Height 1280 -CameraMake Sony -CameraModel "A7 IV" -ISO 100
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/sony-portrait-001.jpg" -Width 1280 -Height 1920 -CameraMake Sony -CameraModel "A7C" -ISO 400
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/sony-portrait-002.jpg" -Width 1280 -Height 1920 -CameraMake Sony -CameraModel "A7 IV" -ISO 1600
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/sony-night-001.jpg" -Width 1920 -Height 1280 -CameraMake Sony -CameraModel "A7S III" -ISO 6400

# Nikon images
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/nikon-landscape-001.jpg" -Width 1920 -Height 1280 -CameraMake Nikon -CameraModel "Z8" -ISO 100
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/nikon-portrait-001.jpg" -Width 1280 -Height 1920 -CameraMake Nikon -CameraModel "Z6 III" -ISO 800

# Subdirectory images
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/landscapes/mountain-sunrise.jpg" -Width 1920 -Height 1280 -CameraMake Canon -CameraModel "EOS R5" -ISO 200
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/landscapes/ocean-sunset.jpg" -Width 1920 -Height 1280 -CameraMake Sony -CameraModel "A7 IV" -ISO 100
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/portraits/studio-portrait-001.jpg" -Width 1280 -Height 1920 -CameraMake Canon -CameraModel "EOS R6" -ISO 400
pwsh scripts/generate-test-image.ps1 -OutPath "$outDir/portraits/outdoor-portrait-001.jpg" -Width 1280 -Height 1920 -CameraMake Sony -CameraModel "A7C" -ISO 200
```

Or use any existing JPEG images with EXIF data.

### 2. Generate the Site

```bash
cd samples/filter-demo
revela generate scan
revela generate images
revela generate pages
```

### 3. Preview

```bash
revela serve
```

## Filter Syntax Examples

```
# String comparison
exif.make == 'Canon'
filename != 'test.jpg'

# Numeric comparison  
exif.iso >= 1600
exif.iso < 400

# Date functions
year(dateTaken) == 2024
month(dateTaken) == 12

# String functions
contains(filename, 'portrait')
startswith(filename, 'IMG_')
endswith(filename, '.jpg')

# Case-insensitive matching
contains(tolower(filename), 'portrait')

# Logical operators
exif.make == 'Canon' and year(dateTaken) == 2024
exif.make == 'Canon' or exif.make == 'Sony'
not contains(filename, 'draft')

# Parentheses for precedence
(exif.make == 'Canon' or exif.make == 'Sony') and exif.iso >= 800
```

## Project Structure

```
filter-demo/
├── project.json              # Project config
├── site.json                 # Site metadata
├── source/
│   ├── _index.revela         # Homepage
│   ├── _images/              # Shared images (no gallery)
│   │   ├── canon-*.jpg
│   │   ├── sony-*.jpg
│   │   ├── nikon-*.jpg
│   │   ├── landscapes/
│   │   └── portraits/
│   ├── 01 Canon Only/
│   │   └── _index.revela     # filter = "contains(tolower(filename), 'canon')"
│   ├── 02 Sony Only/
│   │   └── _index.revela     # filter = "contains(tolower(filename), 'sony')"
│   ├── 03 Landscapes/
│   │   └── _index.revela     # filter = "width > height"
│   ├── 04 Portraits/
│   │   └── _index.revela     # filter = "height > width"
│   └── 05 Night Photos/
│       └── _index.revela     # filter = "contains(tolower(filename), 'night')"
└── output/                   # Generated site (gitignored)
```

# OneDrive Sample Project

This sample demonstrates using the OneDrive Source Plugin to download images from a shared OneDrive folder.

## Configuration

### `onedrive.json`

Contains the OneDrive share link configuration:

```json
{
  "Plugins": {
    "OneDrive": {
      "ShareUrl": "https://1drv.ms/f/..."
    }
  }
}
```

### `project.json`

Project-specific settings (input/output directories, resolutions, etc.)

### `site.json`

Site metadata (title, description, author, etc.)

## Usage

### 1. Download images from OneDrive

```bash
revela source onedrive download -p samples/onedrive
```

This downloads all images from the shared OneDrive folder into `source/`.

### 2. Generate the site

```bash
revela generate -p samples/onedrive
```

This processes images and generates the static site in `output/`.

## Folder Structure

After running both commands:

```
onedrive/
├── onedrive.json       # OneDrive configuration
├── project.json        # Project settings
├── site.json           # Site metadata
├── source/             # Downloaded images (gitignored)
│   ├── 01 Events/
│   ├── 02 Miscellaneous/
│   ├── 03 Pages/
│   └── *.jpg
└── output/             # Generated site (gitignored)
    ├── index.html
    └── images/
```

## Notes

- The `source/` and `output/` folders are excluded from Git
- The OneDrive share link is read-only and public (no authentication required)
- Images retain their EXIF metadata for gallery display

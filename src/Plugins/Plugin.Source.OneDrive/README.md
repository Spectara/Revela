# Spectara.Revela.Plugin.Source.OneDrive

[![NuGet](https://img.shields.io/nuget/v/Spectara.Revela.Plugin.Source.OneDrive.svg)](https://www.nuget.org/packages/Spectara.Revela.Plugin.Source.OneDrive)

Download images from OneDrive shared folders for [Revela](https://github.com/spectara/revela).

## Installation

```bash
revela plugin install OneDrive
```

Or with full package name:
```bash
revela plugin install Spectara.Revela.Plugin.Source.OneDrive
```

## Configuration

Configure the plugin using the interactive command:

```bash
revela config onedrive
```

Or add to `project.json`:

```json
{
  "Spectara.Revela.Plugin.Source.OneDrive": {
    "ShareUrl": "https://1drv.ms/f/your-shared-folder-link",
    "OutputDirectory": "source",
    "DefaultConcurrency": 4
  }
}
```

### Configuration Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `ShareUrl` | Yes | - | OneDrive shared folder URL (1drv.ms or onedrive.live.com) |
| `OutputDirectory` | No | `source` | Local directory for downloaded files |
| `DefaultConcurrency` | No | `4` | Number of parallel downloads (increase for fast connections) |
| `IncludePatterns` | No | `["*.jpg", "*.jpeg", ...]` | File patterns to include |
| `ExcludePatterns` | No | `[]` | File patterns to exclude |

## Usage

### Sync Images

```bash
# Sync images from configured OneDrive folder
revela source onedrive sync

# Override share URL for one-time sync
revela source onedrive sync --share-url "https://1drv.ms/f/..."

# Preview changes without downloading
revela source onedrive sync --dry-run

# Force re-download all files
revela source onedrive sync --force

# Remove local files not in OneDrive
revela source onedrive sync --clean
```

### Workflow Example

```bash
# 1. Install plugin
revela plugin install OneDrive

# 2. Configure (interactive)
revela config onedrive

# 3. Sync images
revela source onedrive sync

# 4. Generate site
revela generate
```

## Features

- ✅ Downloads from OneDrive shared folder links
- ✅ Preserves folder structure
- ✅ Progress bar with file count
- ✅ Skips already downloaded files (by name)
- ✅ Supports nested folders

## Requirements

- Revela CLI v1.0.0 or later
- OneDrive shared folder link (public or organization-shared)

## Supported Link Formats

- `https://1drv.ms/f/...` (short link)
- `https://onedrive.live.com/...` (full link)

## License

MIT - See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

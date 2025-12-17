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

Create a configuration file at `plugins/Spectara.Revela.Plugin.Source.OneDrive.json`:

```json
{
  "Spectara.Revela.Plugin.Source.OneDrive": {
    "ShareUrl": "https://1drv.ms/f/your-shared-folder-link"
  }
}
```

### Configuration Options

| Option | Required | Description |
|--------|----------|-------------|
| `ShareUrl` | Yes | OneDrive shared folder URL (1drv.ms or onedrive.live.com) |

## Usage

### Download Images

```bash
# Download all images from configured OneDrive folder
revela source onedrive download

# Override share URL via command line
revela source onedrive download --share-url "https://1drv.ms/f/..."

# Specify output directory
revela source onedrive download --output ./source
```

### Workflow Example

```bash
# 1. Install plugin
revela plugin install OneDrive

# 2. Configure share URL
# Edit plugins/Spectara.Revela.Plugin.Source.OneDrive.json

# 3. Download images
revela source onedrive download

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

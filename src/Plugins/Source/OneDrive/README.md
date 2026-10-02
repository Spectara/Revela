# Spectara.Revela.Plugins.Source.OneDrive

Download images from OneDrive shared folders for [Revela](https://github.com/spectara/revela).

## Installation

The Standalone build has this plugin built in. In the Full build, install it from the bundled
`packages/` folder:

```bash
revela plugin install Source.OneDrive
```

Or with full package name:
```bash
revela plugin install Spectara.Revela.Plugins.Source.OneDrive
```

The package is not on NuGet.org; it is attached to each
[GitHub Release](https://github.com/spectara/revela/releases).

## Configuration

Configure the plugin using the interactive command:

```bash
revela config onedrive
```

In CI or scripts (no terminal), pass the value instead; without options the command
fails rather than prompting:

```bash
revela config onedrive --share-url "https://1drv.ms/f/your-shared-folder-link"
```

Or add to `project.json` below `plugins.oneDrive`:

```json
{
  "plugins": {
    "oneDrive": {
      "shareUrl": "https://1drv.ms/f/your-shared-folder-link",
      "defaultConcurrency": 4
    }
  }
}
```

Environment variables override `project.json`, e.g. `SPECTARA__REVELA__PLUGINS__ONEDRIVE__SHAREURL=https://1drv.ms/f/...`.

### Configuration Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `shareUrl` | Yes | - | OneDrive shared folder URL (1drv.ms or onedrive.live.com) |
| `defaultConcurrency` | No | `4` | Number of parallel downloads (increase for fast connections) |
| `includePatterns` | No | `[]` (all files) | File-name patterns to download (`*`, `?`, case-insensitive) |
| `excludePatterns` | No | `[]` | File-name patterns to skip; exclusion wins over inclusion |

Patterns match the file name only, not the folder path. Without `includePatterns`,
`--clean` only considers local images (`.jpg`, `.jpeg`, `.png`, `.webp`) and
Markdown files; excluded files are never downloaded or cleaned up.

Downloaded files are saved to the project's source directory (configured via `paths.source` in `project.json`).

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

# Remove local files not in OneDrive (asks for confirmation)
revela source onedrive sync --clean

# Same without asking — required when not running in a terminal (CI, scripts)
revela source onedrive sync --clean --yes
```

Without a terminal, `sync` prints plain progress lines instead of a progress bar.

### Workflow Example

```bash
# 1. Install plugin (Full build only; built into Standalone)
revela plugin install Source.OneDrive

# 2. Configure (interactive)
revela config onedrive

# 3. Sync images
revela source onedrive sync

# 4. Generate site
revela generate all
```

### Data Safety and Diagnostics

Cleanup skips linked files and directories below the configured source root and
checks descendant path components again before deleting. An intentionally linked
source root remains supported. These checks do not prevent a hostile concurrent
process from replacing paths between inspection and deletion.

Downloads are staged beside their destination. Failed or cancelled transfers
retain the previous file and timestamp; successful transfers replace it only
after the new data is complete. Existing Unix permissions are preserved, and
new staged files are private by default. Sync is not a transaction across files.

Sync console output uses a host/hash share reference, and this client's default
HTTP/resilience logs omit raw share/CDN credentials and transport exception text.
Custom telemetry subscribers and shell history are outside that protection;
avoid putting private share links directly into recorded command lines.

Download URLs, paging links and redirects come from OneDrive's responses. They are
only followed over HTTPS to OneDrive, SharePoint and Microsoft Graph hosts (at most
five redirects), and the share token is never sent to another host.

## Features

- ✅ Downloads from OneDrive shared folder links
- ✅ Preserves folder structure
- ✅ Progress bar with file count
- ✅ Smart sync — skips unchanged files (by size and timestamp)
- ✅ Supports nested folders
- ✅ Dry-run mode for previewing changes
- ✅ Orphan detection and cleanup
- ✅ Automatic retry with exponential backoff

## Requirements

- Revela host of the same release version (plugins are packed and released together with Revela)
- OneDrive shared folder link (public or organization-shared)

## Supported Link Formats

- `https://1drv.ms/f/...` (short link)
- `https://onedrive.live.com/...` (full link)

## License

MIT - See [LICENSE](https://github.com/spectara/revela/blob/main/LICENSE)

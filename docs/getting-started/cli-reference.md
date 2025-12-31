# CLI Reference

Command-line reference for advanced users, automation, and CI/CD integration.

---

## Operating Modes

Revela has two operating modes depending on how it's installed:

### Standalone Mode (Portable)

When running `revela.exe` from a folder with a `projects/` subdirectory.

```
C:\Revela\
├── revela.exe          ← Run from here
├── projects/           ← Multi-project support
│   ├── MyPhotos/
│   └── Wedding2025/
└── packages/
```

**Characteristics:**
- Multiple projects in `projects/` folder
- Requires `--project` for CLI commands
- Interactive mode shows project selection
- `projects` command available in menu

### Tool Mode (dotnet tool)

When installed as a global .NET tool: `dotnet tool install -g Spectara.Revela`

```bash
cd ~/photos/my-portfolio
revela generate all
```

**Characteristics:**
- Single project per directory (like git)
- Uses current working directory
- No `--project` argument needed
- No `projects` command

---

## Command Syntax

### Standalone Mode

```bash
# Interactive mode (project selection)
revela

# Specify project for CLI commands
revela --project <name> <command> [options]
revela -p <name> <command> [options]

# Project-independent commands (no --project needed)
revela plugins list
revela packages refresh
revela projects list
```

### Tool Mode

```bash
# Run in project directory
cd /path/to/project
revela <command> [options]

# Interactive mode
revela
```

---

## Project Argument (Standalone Only)

The `--project` (or `-p`) argument specifies which project to use:

```bash
# Long form
revela --project MyPhotos generate all

# Short form
revela -p MyPhotos generate all

# With equals sign
revela --project=MyPhotos generate all
revela -p=Wedding2025 clean all
```

**Error handling:**

```bash
# Missing --project for project-requiring command
$ revela generate all
Error: No project specified.
Use --project <name> or run without arguments for interactive mode.

Available project folders:
  • MyPhotos
  • Wedding2025
```

---

## Project-Independent Commands

These commands work without a project context:

| Command | Description |
|---------|-------------|
| `plugins list` | List installed plugins |
| `plugins install` | Install a plugin |
| `plugins uninstall` | Remove a plugin |
| `theme list` | List installed themes |
| `theme install` | Install a theme |
| `packages list` | List all available packages |
| `packages refresh` | Update package index |
| `projects list` | List project folders (standalone only) |
| `projects create` | Create project folder (standalone only) |
| `projects delete` | Delete project folder (standalone only) |
| `wizard` | Run setup wizard |
| `--help` | Show help |
| `--version` | Show version |

---

## Command Reference

### generate

Generate website content.

```bash
revela -p MyPhotos generate all        # Full pipeline
revela -p MyPhotos generate scan       # Scan source files
revela -p MyPhotos generate pages      # Render HTML only
revela -p MyPhotos generate images     # Process images only
revela -p MyPhotos generate statistics # Generate statistics JSON
```

**Pipeline order:** `scan` → `statistics` → `pages` → `images`

### clean

Delete generated files.

```bash
revela -p MyPhotos clean all      # Delete output + cache
revela -p MyPhotos clean output   # Delete output only
revela -p MyPhotos clean cache    # Delete cache only
```

### serve (requires Serve plugin)

Start local preview server.

```bash
revela -p MyPhotos serve start
revela -p MyPhotos serve start --port 8080
```

### config

Edit configuration interactively.

```bash
revela -p MyPhotos config project   # Project settings
revela -p MyPhotos config theme     # Change theme
revela -p MyPhotos config images    # Image settings
revela -p MyPhotos config site      # Site metadata
```

### theme

Manage themes.

```bash
revela theme list                   # List installed
revela theme install                # Install new theme
revela -p MyPhotos theme extract    # Create custom copy
```

### plugins

Manage plugins.

```bash
revela plugins list                 # List installed
revela plugins install              # Install new plugin
revela plugins uninstall            # Remove plugin
```

### packages

Manage package sources.

```bash
revela packages list      # List available packages
revela packages refresh   # Update package index
```

### projects (Standalone only)

Manage project folders.

```bash
revela projects list      # List all projects with status
revela projects create    # Create new project folder
revela projects delete    # Delete project folder
```

---

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Error (check output) |
| 2 | Restart required (packages installed) |

---

## Automation Examples

### Batch Processing (PowerShell)

```powershell
# Generate all projects
$projects = @("MyPhotos", "Wedding2025", "Landscapes")
foreach ($project in $projects) {
    Write-Host "Generating $project..."
    revela -p $project generate all
}
```

### Watch Mode (PowerShell)

```powershell
# Regenerate pages when templates change
$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = "C:\Revela\packages\themes\Lumina"
$watcher.Filter = "*.html"
$watcher.EnableRaisingEvents = $true

Register-ObjectEvent $watcher Changed -Action {
    revela -p MyPhotos generate pages
}
```

### CI/CD (GitHub Actions)

```yaml
name: Generate Site

on:
  push:
    paths:
      - 'projects/MyPhotos/source/**'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install Revela
        run: dotnet tool install -g Spectara.Revela
      
      - name: Generate Site
        run: |
          cd projects/MyPhotos
          revela generate all
      
      - name: Upload Artifact
        uses: actions/upload-artifact@v4
        with:
          name: website
          path: projects/MyPhotos/output/
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Install Revela
RUN dotnet tool install -g Spectara.Revela
ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /project
COPY . .

RUN revela generate all

# Output in /project/output/
```

---

## Environment Variables

| Variable | Description |
|----------|-------------|
| `REVELA__LOGGING__LOGLEVEL__DEFAULT` | Set log level (Debug, Information, Warning, Error) |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | Disable .NET telemetry |

**Example:**
```bash
# Enable debug logging
REVELA__LOGGING__LOGLEVEL__DEFAULT=Debug revela -p MyPhotos generate all
```

---

## Troubleshooting

### "No project specified" Error

**Cause:** Running a project-requiring command without `--project` in standalone mode.

**Solution:**
```bash
# Add --project
revela --project MyPhotos generate all

# Or use interactive mode
revela
```

### "Not in a Revela project directory" Error

**Cause:** Running in tool mode outside a project folder.

**Solution:**
```bash
# Navigate to project first
cd /path/to/my-project
revela generate all

# Or initialize this folder
revela
```

### Command hangs or no output

**Cause:** Interactive prompts waiting for input.

**Solution:** For automation, ensure all required options are provided. Some commands require interactive input and aren't suitable for automation.

---

## See Also

- [Getting Started (English)](getting-started-en.md) - Beginner-friendly guide
- [Getting Started (German)](getting-started-de.md) - Anfänger-Anleitung
- [Plugin Development](../plugin-development.md) - Create custom plugins
- [Architecture](../architecture.md) - Technical documentation

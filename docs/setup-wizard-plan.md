# Setup Wizard Plan (`revela init`)

**Status:** In Progress  
**Created:** 2025-12-25  
**Updated:** 2025-12-26

## Completed Prerequisites

- ✅ `CommandDescriptor.RequiresProject` - Commands can now declare if they need a project
- ✅ Project-independent commands work in empty folders (config, plugin, theme, packages)
- ✅ Interactive menu filters commands based on project status
- ✅ `create project` removed (use `config project` instead)
- ✅ Empty folder detection with helpful menu

## Overview

Guided first-time setup that creates all config files, installs theme/plugins, and optionally creates sample content.

## Flow

```
┌─────────────────────────────────────────────────────────────┐
│  Welcome to Revela Setup                                    │
├─────────────────────────────────────────────────────────────┤
│  ○ New project    ○ Express setup (smart defaults)          │
└─────────────────────────────────────────────────────────────┘
                            ↓
        ┌───────────────────────────────────────┐
        │  — config project —                   │  ← existing
        │  Name, Base URL, Language             │
        └───────────────────────────────────────┘
                            ↓
        ┌───────────────────────────────────────┐
        │  — theme select + restore —           │  ← existing
        │  Lumina, Lumina+Statistics, ...       │
        └───────────────────────────────────────┘
                            ↓
        ┌───────────────────────────────────────┐
        │  — config site —                      │  ← existing
        │  Title, Author, Copyright, Social     │
        └───────────────────────────────────────┘
                            ↓
        ┌───────────────────────────────────────┐
        │  — plugin install (multi-select) —    │  ← new: Multi-Select UI
        │  [x] Serve  [x] Statistics  [ ] ...   │
        └───────────────────────────────────────┘
                            ↓
        ┌───────────────────────────────────────┐
        │  — sample content —                   │  ← new
        │  Empty / Sample gallery / Import      │
        └───────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│  Setup complete!                                            │
│                                                             │
│  Created:                                                   │
│    ✓ project.json                                           │
│    ✓ site.json                                              │
│    ✓ source/                                                │
│                                                             │
│  Next steps:                                                │
│    1. Add images to source/                                 │
│    2. revela generate all                                   │
│    3. revela serve                                          │
└─────────────────────────────────────────────────────────────┘
```

## Architecture

The wizard orchestrates existing commands - no duplicated code:

```
InitCommand (orchestrator)
    │
    ├─→ ConfigProjectCommand.ExecuteAsync()   // Step 2: Project settings
    │
    ├─→ ThemeManager + PluginManager          // Step 3: Theme install/select
    │       └─→ RestoreCommand (auto)
    │
    ├─→ ConfigSiteCommand.ExecuteAsync()      // Step 4: Site info
    │
    ├─→ PluginManager.InstallAsync()          // Step 5: Plugins
    │
    └─→ SampleContentService (new)            // Step 6: Sample content
```

## Steps Detail

### 1. Welcome

- New project vs Express setup (smart defaults)
- `--yes` flag for CI/scripting (accept all defaults)

### 2. Project Settings (existing: `config project`)

| Field | Default | Required |
|-------|---------|----------|
| Name | Folder name | Yes |
| Base URL | (empty) | No |
| Language | en | No |

### 3. Theme Selection (existing: `config theme`)

- Show installed themes
- Offer to download Lumina if not installed
- Auto-restore after selection
- Theme extensions (e.g., Lumina.Statistics)

### 4. Site Configuration (existing: `config site`)

| Field | Default | Required |
|-------|---------|----------|
| Title | Project name | Yes |
| Author | (empty) | No |
| Copyright | © {year} {author} | No |
| Social links | (empty) | No |

### 5. Plugin Selection (new: multi-select UI)

Show available/recommended plugins:

| Plugin | Description | Default |
|--------|-------------|---------|
| Serve | Local preview server | ✓ Selected |
| Statistics | EXIF statistics pages | ✓ Selected |
| Source.OneDrive | Download from OneDrive | Not selected |

### 6. Sample Content (new: `SampleContentService`)

Options:
- **Empty project** - Just create directories
- **Sample gallery** - Create example with placeholder images
- **Import existing** - Detect images already in source/

### 7. Summary

- List created files
- Show next steps
- Suggest commands to run

## New Components Needed

1. **`InitCommand`** - Orchestrates the flow
2. **Plugin multi-select UI** - In InitCommand
3. **`SampleContentService`** - Creates example galleries

## CLI Options

```bash
# Interactive wizard (default)
revela init

# Express setup with all defaults
revela init --yes

# Specify project directory
revela init ./my-portfolio
```

## Edge Cases

- **Existing files** - Ask to overwrite/merge?
- **No network** - Skip theme download, use bundled
- **Existing source/ with images** - Offer to import

## Future Enhancements

- Project templates (minimal, portfolio, multi-gallery)
- Git ignore file creation
- VS Code workspace settings

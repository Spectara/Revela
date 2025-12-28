# Setup Wizard Plan

**Status:** ✅ Revela Setup Wizard Implemented, Project Init pending  
**Created:** 2025-12-25  
**Updated:** 2025-12-27

## Two-Step Approach

Revela now uses a two-step setup:

1. **Revela Setup Wizard** (Program-level) - Configures Revela itself (themes, plugins)
2. **Project Init** (Project-level) - Creates a new project (future)

## ✅ Revela Setup Wizard (Implemented)

### Trigger

The Setup Wizard is shown automatically when:
- `revela.json` does not exist (fresh installation)

### Flow

```
revela                    ← Start without arguments
  │
  ├── revela.json missing?
  │     ├── "Start Setup Wizard" → Wizard → Exit (for plugin reload)
  │     └── "Skip" → Normal menu (limited functionality)
  │
  └── revela.json exists → Normal menu
        └── "Setup" group → "🔧 Setup Wizard" to re-run
```

### Wizard Steps

```
┌─────────────────────────────────────────────────────────────┐
│  SETUP WIZARD                                               │
├─────────────────────────────────────────────────────────────┤
│  Step 1/3: Package Sources                                  │
│    • Show current NuGet feeds                               │
│    • Optional: Add custom feed                              │
├─────────────────────────────────────────────────────────────┤
│  [Packages Refresh - automatic]                             │
│    • Download package index from all feeds                  │
├─────────────────────────────────────────────────────────────┤
│  Step 2/3: Install Themes                                   │
│    • Multi-select from available themes                     │
│    • Already installed = disabled                           │
│    • At least 1 theme required                              │
├─────────────────────────────────────────────────────────────┤
│  Step 3/3: Install Plugins (Optional)                       │
│    • Multi-select from available plugins                    │
│    • Already installed = disabled                           │
├─────────────────────────────────────────────────────────────┤
│  ✓ Setup completed!                                         │
│  Revela will exit. Please restart to continue.              │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Files

- [SetupWizard.cs](../src/Cli/Hosting/SetupWizard.cs) - Wizard orchestrator
- [InteractiveMenuService.cs](../src/Cli/Hosting/InteractiveMenuService.cs) - First-run detection
- [MenuChoice.cs](../src/Cli/Hosting/MenuChoice.cs) - `MenuAction.RunSetupWizard`
- [GlobalConfigManager.cs](../src/Core/Services/GlobalConfigManager.cs) - `ConfigFileExists()`, `GetThemesAsync()`

### Automation (without Wizard)

Advanced users can bypass the wizard:

```bash
revela packages refresh
revela theme install Spectara.Revela.Theme.Lumina
revela plugin install Spectara.Revela.Plugin.Serve
```

---

## 📋 Project Init Wizard (Planned)

The project init wizard creates a new Revela project. It requires the Revela Setup to be completed first (at least one theme installed).

### Trigger

- User runs `revela init` OR
- User selects "Create Project" from menu (when no project.json exists)

### Planned Flow

```
revela init
  │
  ├── Check: Theme installed? → If not, show error
  │
  ├── 1. Project Settings (config project)
  │     • Name, Base URL, Language
  │
  ├── 2. Theme Selection (config theme select)
  │     • Choose from installed themes
  │
  ├── 3. Site Configuration (config site)
  │     • Title, Author, Copyright
  │
  ├── 4. Create source/ directory
  │
  └── Summary: "Project created! Add images to source/"
```

### Architecture

The init wizard orchestrates existing commands:

```
InitCommand (orchestrator)
    │
    ├─→ ConfigProjectCommand    // Project settings
    │
    ├─→ ConfigThemeCommand      // Theme selection
    │
    ├─→ ConfigSiteCommand       // Site info
    │
    └─→ Create source/          // Directory creation
```

### CLI Options

```bash
# Interactive wizard (default)
revela init

# Express setup with all defaults
revela init --yes

# Specify project directory
revela init ./my-portfolio
```

---

## Summary

| Wizard | Trigger | Purpose | Files Created |
|--------|---------|---------|---------------|
| Revela Setup | No revela.json | Install themes/plugins | revela.json |
| Project Init | No project.json | Create project | project.json, site.json, source/ |

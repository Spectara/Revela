# Setup Wizard Plan

**Status:** ✅ Both Wizards Implemented  
**Created:** 2025-12-25  
**Updated:** 2025-12-28

## Two-Step Approach

Revela uses a two-step setup:

1. **Revela Setup Wizard** (Program-level) - Configures Revela itself (themes, plugins)
2. **Project Setup Wizard** (Project-level) - Creates a new project

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
        └── "Addons" group → "wizard" to re-run
```

### Wizard Steps

```
┌─────────────────────────────────────────────────────────────┐
│  SETUP WIZARD                                               │
├─────────────────────────────────────────────────────────────┤
│  [Packages Refresh - automatic/silent]                      │
│    • Download package index from all feeds                  │
├─────────────────────────────────────────────────────────────┤
│  Step 1/2: Install Themes                                   │
│    • Multi-select from available themes                     │
│    • "» All «" option to install all                        │
│    • Already installed shown with checkmarks                │
│    • At least 1 theme required                              │
├─────────────────────────────────────────────────────────────┤
│  Step 2/2: Install Plugins (Optional)                       │
│    • Multi-select from available plugins                    │
│    • "» All «" option to install all                        │
│    • Already installed shown with checkmarks                │
├─────────────────────────────────────────────────────────────┤
│  ✓ Setup completed!                                         │
│  Revela will exit. Please restart to continue.              │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Files

- [Commands/Revela/Wizard.cs](../src/Commands/Revela/Wizard.cs) - Wizard orchestrator
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

## ✅ Project Setup Wizard (Implemented)

The project wizard creates a new Revela project. It requires the Revela Setup to be completed first (at least one theme installed).

### Trigger

The Project Wizard is shown automatically when:
- `revela.json` exists (Revela is set up)
- `project.json` does not exist (no project in current directory)

### Flow

```
revela                    ← Start without arguments
  │
  ├── revela.json missing? → Revela Setup Wizard
  │
  └── revela.json exists?
        │
        ├── project.json missing?
        │     ├── "Create New Project" → Project Wizard → Menu
        │     └── "Skip" → Normal menu (limited functionality)
        │
        └── project.json exists → Normal menu
```

### Wizard Steps

```
┌─────────────────────────────────────────────────────────────┐
│  PROJECT SETUP WIZARD                                       │
├─────────────────────────────────────────────────────────────┤
│  Step 1/3: Project Settings                                 │
│    • Project name (default: directory name)                 │
│    • Base URL (optional)                                    │
│    → Creates: project.json, source/, output/                │
├─────────────────────────────────────────────────────────────┤
│  Step 2/3: Select Theme                                     │
│    • Choose from installed themes                           │
│    → Updates: project.json (theme.name)                     │
├─────────────────────────────────────────────────────────────┤
│  Step 3/3: Site Metadata                                    │
│    • Title, author, copyright, etc.                         │
│    • Fields based on theme template                         │
│    → Creates: site.json                                     │
├─────────────────────────────────────────────────────────────┤
│  ✓ Project created successfully!                            │
│  Next: Add images to source/ and run revela generate        │
└─────────────────────────────────────────────────────────────┘
```

### Implementation Files

- [Commands/Project/Wizard.cs](../src/Commands/Project/Wizard.cs) - Wizard orchestrator
- [InteractiveMenuService.cs](../src/Cli/Hosting/InteractiveMenuService.cs) - No-project detection
- [Config/Project/ConfigProjectCommand.cs](../src/Commands/Config/Project/ConfigProjectCommand.cs) - Step 1
- [Config/Theme/ConfigThemeCommand.cs](../src/Commands/Config/Theme/ConfigThemeCommand.cs) - Step 2
- [Config/Site/ConfigSiteCommand.cs](../src/Commands/Config/Site/ConfigSiteCommand.cs) - Step 3

### Automation (without Wizard)

Advanced users can create projects manually:

```bash
revela config project --name "My Portfolio" --url "https://photos.example.com"
revela config theme --set Lumina
revela config site
```

---

## Summary

| Wizard | Trigger | Purpose | Files Created |
|--------|---------|---------|---------------|
| Revela Setup | No revela.json | Install themes/plugins | revela.json |
| Project Setup | No project.json | Create project | project.json, site.json, source/, output/ |

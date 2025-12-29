# Screenshots

This folder contains screenshots for the README and documentation.

## Required Screenshots

Please create the following screenshots (PNG format, ~800-1200px wide):

### 1. `setup-wizard.png`
- First-run setup wizard
- Shows theme selection step
- Capture: Run `revela` in empty folder (no `revela.json`)

### 2. `project-wizard.png`  
- Project creation wizard
- Shows one of the 4 steps (theme selection looks nice)
- Capture: Run `revela` in folder with `revela.json` but no `project.json`

### 3. `generate-progress.png`
- Generation progress bars
- Shows image processing and page rendering
- Capture: Run `revela generate all` with some images

### 4. `interactive-menu.png` (optional)
- Main menu with all options
- Capture: Run `revela` in a project folder

### 5. `lumina-theme.png` (optional)
- Generated website with Lumina theme
- Shows gallery view
- Capture: Open `output/index.html` in browser

## Tips for Good Screenshots

- Use a clean terminal (PowerShell or Windows Terminal)
- Dark theme looks better in READMEs
- Crop to show only the relevant part
- Avoid personal information in paths
- Resolution: 1x or 2x for retina displays

## How to Add to README

Once screenshots are created, uncomment the image tags in README.md:

```markdown
<!-- Remove these comment markers -->
![Setup Wizard](assets/screenshots/setup-wizard.png)
```

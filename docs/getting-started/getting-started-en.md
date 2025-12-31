# Revela - Getting Started

A step-by-step guide for photographers to create a portfolio website with Revela.

---

## What is Revela?

**Revela** is a program that automatically creates a beautiful portfolio website from your photos. You put your images in folders, run Revela, and get a complete website with:

- Automatically scaled images (various sizes for fast loading)
- Modern gallery view with lightbox
- Responsive design (works on phone, tablet, desktop)
- Fast loading times through optimized image formats (AVIF, WebP, JPG)

**Easy to use:** When you double-click `revela.exe`, interactive wizards guide you through the setup. Just follow the prompts - no command line knowledge required!

---

## 1. Download Revela

### Step 1.1: Download

1. Go to **GitHub Releases**: https://github.com/spectara/revela/releases
2. Download the latest version:
   - `revela-win-x64.zip` for Windows (64-bit)
3. Extract the ZIP file to a folder of your choice, e.g.:
   - `C:\Revela\`
   - or `D:\Tools\Revela\`

**Optional (advanced):** You can verify the release artifacts. Each version provides checksums (`SHA256SUMS`), cosign signatures, and GitHub Attestations — see the release page for details.

After extracting, you'll have these files:
```
C:\Revela\
├── revela.exe                          ← The main program
├── projects/                           ← Your projects will be here
│   └── (empty)
└── getting-started/                    ← Guides (multilingual)
    ├── README.md
    ├── de.md                           ← Deutsch
    └── en.md                           ← English
```

### Step 1.2: Start Revela

1. Open the folder `C:\Revela` in Windows Explorer
2. **Double-click on `revela.exe`**
3. The **Revela Setup Wizard** opens automatically

That's it! No installation required, no command line needed.

---

## 2. Revela Setup Wizard (First Run)

When you start Revela for the first time, the **Setup Wizard** appears automatically. This wizard helps you install themes and plugins.

### What You'll See

```
┌─────────────────────────────────────────────────────────────┐
│  Welcome to the Revela Setup Wizard!                        │
│                                                             │
│  This wizard will help you configure Revela for first use:  │
│    1. Install a theme (required)                            │
│    2. Install plugins (optional)                            │
│                                                             │
│  You can re-run this wizard later via: Addons → wizard      │
└─────────────────────────────────────────────────────────────┘
```

### Step 2.1: Install a Theme

1. The wizard automatically downloads the package index
2. You'll see a list of available themes
3. Select at least one theme (use **Space** to select, **Enter** to confirm)
4. Recommended: **Lumina** (the default photography theme)

**Tip:** You can select multiple themes if you want to try different looks.

### Step 2.2: Install Plugins (Optional)

1. You'll see a list of available plugins
2. Select any plugins you want (or none)
3. Useful plugins:
   - **Serve** - Preview your site locally before uploading
   - **Statistics** - Track image count, total size, etc.
   - **Source.OneDrive** - Import photos from OneDrive shared folders

### Step 2.3: Restart

After installation, Revela needs to restart to load the new packages:

```
✓ Setup completed successfully!

Installed themes:
  • Lumina

Please restart Revela to load the new packages.
```

**Double-click `revela.exe` again** to continue.

---

## 3. Select or Create a Project Folder

After the setup wizard (and restart), you'll see the **Project Selection** screen:

```
Select a project folder:

Projects
  (no projects yet)
Setup
  Create new project folder
Exit
```

### Step 3.1: Create a Project Folder

1. Select **Create new project folder**
2. Enter a name for your project (e.g., "MyPhotos", "Wedding2025")
3. The folder will be created at `C:\Revela\projects\MyPhotos\`

**Tip:** Use descriptive names - you can have multiple projects for different photo collections!

After creating the folder, Revela restarts and opens your new project.

---

## 4. Project Wizard (First Time in a Project)

When you enter a project folder for the first time (no `project.json`), the **Project Wizard** appears automatically.

### What You'll See

```
┌─────────────────────────────────────────────────────────────┐
│  Create a New Revela Project                                │
│                                                             │
│  This wizard will help you set up a new photo gallery:      │
│    1. Project settings (name, URL)                          │
│    2. Select a theme                                        │
│    3. Image settings (formats, sizes)                       │
│    4. Site metadata (title, author)                         │
│                                                             │
│  You can change these settings later via: revela config     │
└─────────────────────────────────────────────────────────────┘
```

### Step 4.1: Project Settings

Enter your project details:

- **Project name:** A short name for your project (e.g., "MyPortfolio")
- **Base URL:** Your website address (e.g., "https://photos.example.com")
  - Leave empty if you don't know yet

### Step 4.2: Select Theme

Choose a theme from your installed themes. If you only installed Lumina, it will be selected automatically.

### Step 4.3: Image Settings

Configure how your images should be processed:

- **Formats:** Which formats to generate (AVIF, WebP, JPG)
- **Quality:** Higher = better quality but larger files
- **Sizes:** Which widths to generate (responsive images)

**Tip for beginners:** The defaults work great! Just press Enter to accept them.

**Tip for speed:** AVIF offers the best compression but takes longer. For a quick first test, you can disable AVIF and keep only WebP and JPG.

### Step 4.4: Site Metadata

Enter information about your website:

- **Title:** Your website title (appears in browser tab)
- **Author:** Your name
- **Copyright:** Copyright notice (e.g., "© 2025 John Smith")

### After the Wizard

The wizard creates these files and folders in your project:

```
C:\Revela\
├── revela.exe
├── revela.json                         ← Revela configuration
├── packages/                           ← Installed themes & plugins
│   └── ...
└── projects/
    └── MyPhotos/                       ← Your project folder
        ├── project.json                ← Project settings
        ├── site.json                   ← Website metadata
        ├── source/                     ← Put your photos here
        │   └── (empty)
        ├── output/                     ← Generated website goes here
        │   └── (empty)
        └── cache/                      ← Image cache
            └── (empty)
```

---

## 5. Add Photos

### Step 5.1: Create Galleries as Folders

Create subfolders in the `source` folder for your galleries. The folder names become gallery titles:

```
C:\Revela\projects\MyPhotos\source\
├── 01 Weddings/
│   ├── photo1.jpg
│   ├── photo2.jpg
│   └── photo3.jpg
├── 02 Portraits/
│   ├── portrait1.jpg
│   └── portrait2.jpg
└── 03 Landscapes/
    ├── mountains.jpg
    └── sunset.jpg
```

**Tips for folder structure:**

- **Numbering:** Numbers at the beginning (`01`, `02`, `03`) control menu order
- **Folder name = Gallery title:** "01 Weddings" becomes "Weddings" on the website
- **Nested galleries:** You can create sub-galleries:
  ```
  source/
  └── 01 Events/
      ├── 01 Lisa & Tom Wedding/
      └── 02 Miller Corp Company Party/
  ```

### Step 5.2: Add Gallery Description (Optional)

To customize a gallery's title or add a description, create an `_index.md` file:

**File:** `source/01 Weddings/_index.md`
```markdown
---
title: Wedding Photography
description: Emotional moments captured forever
---

Every wedding tells its own story. Here you'll find 
a selection of my most beautiful wedding photos.
```

| Field | Meaning |
|-------|---------|
| `title` | Overrides the folder name as title |
| `description` | Description text for the gallery |

The text below the `---` lines appears as introduction text on the gallery page.

---

## 6. Generate Website

### Step 6.1: Generate Your Website

1. Double-click `revela.exe` to open the menu
2. Select **generate**
3. Select **all** to run the complete pipeline

**What happens:**

1. **Scan:** Revela finds all images in `source/`
2. **Process images:** Each image is created in all configured sizes and formats
3. **Render pages:** HTML files are generated from the templates

You'll see a progress bar:

```
Scanning...
✓ Found 47 images in 5 galleries

Processing images [████████████████████] 100% 47/47 - mountains.jpg
Rendering pages   [████████████████████] 100% 12/12 - index.html

✓ Generation complete!
```

### Step 6.2: Regenerate Only Parts (Optional)

In the **generate** submenu, you can select specific steps:

| Option | What it does |
|--------|-------------|
| **all** | Complete pipeline (scan → statistics → pages → images) |
| **scan** | Scan source files (run first when images changed) |
| **statistics** | Generate statistics (requires Statistics plugin) |
| **pages** | Only re-render HTML pages |
| **images** | Only re-process images |

**Note:** After adding/deleting images or changing `_index.md` files, run **scan** first.

---

## 7. Preview & Upload

### Step 7.1: Preview Locally (with Serve Plugin)

If you installed the **Serve** plugin:

1. In the menu, select **serve** → **start**
2. Your browser opens automatically with your site
3. Press **Ctrl+C** in the terminal to stop the server

### Step 7.2: Open Files Directly

Without the Serve plugin, you can open the files directly:

1. Go to `C:\Revela\projects\MyPhotos\output\` in Windows Explorer
2. Double-click on `index.html`
3. The website opens in your browser

**Note:** Some features (like lazy loading) work better with a real server.

### Step 7.3: Upload to Web Server

To put your website online, upload the complete contents of the `output` folder to your web server via FTP, SFTP, or your hosting provider's file manager.

---

## 8. Menu Reference

### Main Menu

| Menu | Submenu | Description |
|------|---------|-------------|
| **generate** | all | Generate website (full pipeline) |
| | scan | Scan source files |
| | images | Only process images |
| | pages | Only create HTML pages |
| | statistics | Generate statistics JSON |
| **clean** | all | Delete output + cache |
| | output | Delete only output |
| | cache | Delete only cache |
| **config** | project | Edit project settings |
| | theme | Change theme |
| | images | Edit image settings |
| | site | Edit site metadata |
| | feed | Manage package feeds |
| **theme** | list | Show installed themes |
| | install | Install new theme |
| | extract | Create custom theme copy |
| **plugins** | list | Show installed plugins |
| | install | Install new plugin |
| | uninstall | Remove a plugin |
| **packages** | refresh | Update package index |
| | list | Show all available packages |
| **serve** | start | Start local preview server |
| | | *(requires Serve plugin)* |

### Setup Group (Standalone Mode)

| Option | Description |
|--------|-------------|
| **projects** | Manage project folders (list, create, delete) |

### Addons Group

| Option | Description |
|--------|-------------|
| **wizard** | Re-run the Revela Setup Wizard |

---

## 9. Working with Multiple Projects

Revela supports multiple projects in standalone mode. Each project has its own folder with separate `source/`, `output/`, and `cache/` directories.

### Switching Between Projects

1. Double-click `revela.exe`
2. The project selection screen shows all your projects:
   ```
   Select a project folder:
   
   Projects
     > MyPhotos
       Wedding2025
       Landscapes (not configured)
   Setup
     Create new project folder
   Exit
   ```
3. Select the project you want to work on

**Note:** Projects marked "(not configured)" don't have a `project.json` yet - the Project Wizard will run when you select them.

### Managing Projects

From the main menu, go to **Setup** → **projects**:

| Option | Description |
|--------|-------------|
| **list** | Show all project folders with status |
| **create** | Create a new project folder |
| **delete** | Delete a project folder (with confirmation) |

### Project Structure

```
C:\Revela\
├── revela.exe
├── revela.json                    ← Global config (shared)
├── packages/                      ← Themes & plugins (shared)
└── projects/
    ├── MyPhotos/                  ← Project 1
    │   ├── project.json
    │   ├── site.json
    │   ├── source/
    │   ├── output/
    │   └── cache/
    └── Wedding2025/               ← Project 2
        ├── project.json
        ├── site.json
        ├── source/
        ├── output/
        └── cache/
```

**Benefits:**
- Separate photo collections in different projects
- Each project can have different settings (theme, image sizes)
- Shared packages - install once, use everywhere

---

## 10. Configuration Files

### project.json

Technical settings for your project:

```json
{
  "name": "MyPortfolio",
  "url": "https://www.my-website.com",
  "theme": {
    "name": "Lumina"
  },
  "generate": {
    "images": {
      "formats": {
        "webp": 85,
        "jpg": 90
      },
      "sizes": [640, 1024, 1280, 1920, 2560],
      "minWidth": 800,
      "minHeight": 600
    }
  }
}
```

| Setting | Meaning |
|---------|---------|
| `name` | Project name |
| `url` | Website base URL |
| `theme.name` | Active theme |
| `formats` | Image formats with quality (0-100) |
| `sizes` | Image widths in pixels |
| `minWidth/minHeight` | Ignore smaller images (filters thumbnails) |

### site.json

Website metadata:

```json
{
  "title": "John Smith Photography",
  "author": "John Smith",
  "description": "Professional wedding and portrait photography",
  "copyright": "© 2025 John Smith"
}
```

### revela.json

Global Revela configuration (in Revela folder):

```json
{
  "feeds": [
    {
      "name": "Official",
      "url": "https://nuget.pkg.github.com/spectara/index.json"
    }
  ]
}
```

---

## 11. Common Problems

### "The menu doesn't appear" or "Window closes immediately"

**Causes:**
- Revela crashed before loading
- Missing dependencies

**Solution:** Try running from command line to see error messages:
1. Open PowerShell in the Revela folder
2. Run `.\revela.exe`
3. Check the error message

### "No images found"

**Cause:** The `source` folder is empty or images are not in subfolders.

**Solution:** Create at least one subfolder with images:
```
source/
└── 01 My Photos/
    └── image.jpg
```

### "Error processing images"

**Causes:**
- Corrupted image file
- Unsupported format (only JPG, PNG, TIFF supported)
- Very large images (>100 MP) can cause memory issues

**Solution:** Check the error message - it shows which image caused the problem.

### Website looks different than expected

**Causes:**
- Browser cache showing old version
- Configuration error

**Solutions:**
1. Press **Ctrl+F5** in browser (hard refresh)
2. Run **clean** → **all**, then **generate** → **all**
3. Check `site.json` and `project.json` for typos

### "No themes available" in wizard

**Cause:** Package index not loaded or network issue.

**Solution:**
1. Check your internet connection
2. Run **packages** → **refresh** from the menu
3. Or re-run the wizard via **Addons** → **wizard**

---

## 12. Next Steps

Once your website works:

- **Customize theme:** Select **theme** → **extract** to create your own copy
- **Install more plugins:** Select **plugins** → **install**
- **Change settings:** Select **config** for all configuration options
- **Upload to server:** Copy contents of `output/` via FTP/SFTP

---

## Help & Support

- **GitHub Issues:** https://github.com/spectara/revela/issues
- **Documentation:** https://github.com/spectara/revela/tree/main/docs

Feel free to create an issue on GitHub if you have questions or problems!

# Revela - Getting Started

A step-by-step guide for photographers to create a portfolio website with Revela.

---

## What is Revela?

**Revela** is a program that automatically creates a beautiful portfolio website from your photos. You put your images in folders, run Revela, and get a complete website with:

- Automatically scaled images (various sizes for fast loading)
- Modern gallery view with lightbox
- Responsive design (works on phone, tablet, desktop)
- Fast loading times through optimized image formats (AVIF, WebP, JPG)

**Important:** Revela is a **command-line program** (CLI = Command Line Interface). This means:
- It has **no graphical interface** with windows and buttons
- You control it via **text commands** in the command line (CMD or PowerShell)
- If you double-click `revela.exe`, nothing seems to happen – that's normal!

---

## 1. Installation (Windows)

### Step 1.1: Download Revela

1. Go to the **GitHub Releases**: https://github.com/spectara/revela/releases
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
├── Spectara.Revela.Theme.Lumina.dll    ← The default theme
└── getting-started/                    ← Guides (multilingual)
    ├── README.md
    ├── de.md                           ← Deutsch
    └── en.md                           ← English
```

### Step 1.2: Test the Installation

1. Open the **command line in the Revela folder**:
   
   **Easiest method (recommended):**
   - Open the folder `C:\Revela` in Windows Explorer
   - Right-click on an empty area in the folder
   - Select **"Open in Terminal"** (Windows 11) or **"Open PowerShell window here"** (Windows 10)
   
   **Alternative via Run dialog:**
   - Press `Windows + R`
   - Type `cmd` and press Enter
   - Navigate to the Revela folder: `cd C:\Revela`
   
2. Test if Revela works:
   ```
   .\revela.exe --version
   ```
   
   You should see the version number, e.g.:
   ```
   revela 1.0.0
   ```

3. Show all available commands:
   ```
   .\revela.exe --help
   ```

---

## 2. Create a Project

### Step 2.1: Initialize Project

Open the command line in the Revela folder (as described in Step 1.2) and run:

```
.\revela.exe init project
```

Revela automatically creates the basic structure:

```
C:\Revela\
├── revela.exe                          ← The main program
├── Spectara.Revela.Theme.Lumina.dll    ← The default theme
├── getting-started/                    ← Guides
├── project.json                        ← Project settings (new)
├── site.json                           ← Website information (new)
├── source/                             ← Put your photos here (new)
│   └── (empty)
└── output/                             ← The finished website goes here (new)
    └── (empty)
```

---

## 3. Add Photos

### Step 3.1: Create Galleries as Folders

Create subfolders in the `source` folder for your galleries. The folder names become gallery titles:

```
C:\Revela\source\
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

- **Numbering:** The numbers at the beginning (`01`, `02`, `03`) determine the order in the menu
- **Folder name = Gallery title:** "01 Weddings" becomes "Weddings" on the website
- **Sub-galleries:** You can also create nested folders:
  ```
  source/
  └── 01 Events/
      ├── 01 Lisa & Tom Wedding/
      └── 02 Miller Corp Company Party/
  ```

### Step 3.2: Add Gallery Description (optional)

To give a gallery its own title or description, create an `_index.md` file in the gallery folder:

**File:** `source/01 Weddings/_index.md`
```markdown
---
title: Wedding Photography
description: Emotional moments captured for eternity
---

Every wedding tells its own story. Here you'll find 
a selection of my most beautiful wedding photos.
```

The fields mean:
| Field | Meaning |
|-------|---------|
| `title` | Overrides the folder name as title |
| `description` | Description text for the gallery |

The text below the `---` lines is displayed as introductory text on the gallery page.

---

## 4. Customize Configuration

### Step 4.1: Website Information (site.json)

Open `site.json` with a text editor (Notepad, VS Code, etc.) and adjust the values:

```json
{
  "title": "John Smith Photography",
  "author": "John Smith",
  "description": "Professional wedding and portrait photography in New York",
  "copyright": "© 2025 John Smith"
}
```

| Field | Meaning |
|-------|---------|
| `title` | Title of the website (appears in browser tab) |
| `author` | Your name |
| `description` | Short description for search engines |
| `copyright` | Copyright notice in the footer |

### Step 4.2: Project Settings (project.json)

The `project.json` contains technical settings. For starters, you can keep the default values:

```json
{
  "name": "MyPortfolio",
  "url": "https://www.my-website.com",
  "theme": "Lumina",
  "generate": {
    "images": {
      "formats": {
        "avif": 80,
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

**What do the image settings mean?**

| Setting | Meaning |
|---------|---------|
| `formats` | Which image formats are created (AVIF, WebP, JPG) |
| The numbers (80, 85, 90) | Quality level (0-100), higher = better quality, larger files |
| `sizes` | Image widths in pixels that are created || `minWidth` | Minimum image width in pixels (smaller images are ignored) |
| `minHeight` | Minimum image height in pixels (smaller images are ignored) |

**Tip:** Use `minWidth` and `minHeight` to filter out preview/thumbnail files that some programs or phones place alongside your photos.
**Tip for getting started:** AVIF offers the best compression but takes significantly longer to process. For a quick first test, we recommend using only JPG:
```json
"formats": {
  "jpg": 90
}
```

---

## 5. Generate Website

### Step 5.1: Run Generate Command

Open the command line in the Revela folder and run:

```
.\revela.exe generate
```

**What happens now?**

1. **Scan:** Revela finds all images in `source/`
2. **Process images:** Each image is created in all configured sizes and formats
3. **Render pages:** HTML files are generated from the templates

Depending on the number and size of your images, this can take several minutes. You'll see a progress bar:

```
Scanning...
✓ Found 47 images in 5 galleries

Processing images [████████████████████] 100% 47/47 - mountains.jpg
Rendering pages   [████████████████████] 100% 12/12 - index.html

✓ Generation complete!
```

### Step 5.2: Regenerate Only Parts (optional)

If you've only made small changes, you can regenerate only parts:

```
.\revela.exe generate scan      # Scan source files (always first if images changed)
.\revela.exe generate images    # Only reprocess images
.\revela.exe generate pages     # Only re-render HTML pages
```

**Note:** If you've added/deleted images or modified `_index.md` files, run `generate scan` first so Revela recognizes the changes.

---

## 6. View the Result

### Step 6.1: Open Website in Browser

After generating, you'll find the finished website in the `output` folder:

```
C:\Revela\output\
├── index.html          ← Homepage
├── main.css
├── main.js
├── weddings/
│   └── index.html
├── portraits/
│   └── index.html
├── images/
│   └── (all processed images)
└── ...
```

**How to open the website:**

1. Go to `C:\Revela\output\` in Windows Explorer
2. Double-click on `index.html`
3. The website opens in your default browser

### Step 6.2: Upload Website to a Web Server

To put your website online, upload the complete contents of the `output` folder to your web server (FTP, SFTP, etc.).

---

## 7. Useful Commands

### All Commands at a Glance

| Command | Description |
|---------|-------------|
| `.\revela.exe --help` | Shows all available commands |
| `.\revela.exe init project` | Create new project |
| `.\revela.exe generate` | Generate website |
| `.\revela.exe generate images` | Only process images |
| `.\revela.exe generate pages` | Only create HTML pages |
| `.\revela.exe clean` | Delete generated files |
| `.\revela.exe clean --all` | Delete everything (incl. cache) |
| `.\revela.exe theme list` | Show available themes |

### Help for Individual Commands

```
.\revela.exe generate --help
.\revela.exe clean --help
.\revela.exe init --help
```

---

## Common Problems

### "The CMD window opens briefly and closes again"

**Cause:** You double-clicked `revela.exe`.

**Solution:** Revela is a command-line program and must be started via CMD or PowerShell:

1. Press `Windows + R`
2. Type `cmd` and press Enter
3. Navigate to the Revela folder: `cd C:\Revela`
4. Run the command: `.\revela.exe generate`

### "No images found"

**Cause:** The `source` folder is empty or the images are not in subfolders.

**Solution:** Create at least one subfolder in `source/` and put images in it:
```
source/
└── 01 My Photos/
    └── image.jpg
```

### "Error processing images"

**Possible causes:**
- Corrupted image file
- Unsupported format (only JPG, PNG, TIFF are supported)
- Very large images (>100 MP) can cause memory problems

**Solution:** Check the error message in the console. It shows which image caused the problem.

### Website looks different than expected

**Possible causes:**
- Browser cache showing old version
- Error in configuration

**Solutions:**
1. Press `Ctrl + F5` in the browser (Hard Refresh)
2. Run `.\revela.exe clean --all` and regenerate
3. Check `site.json` and `project.json` for typos

---

## Next Steps

Once your website works, you can:

- **Customize theme:** `.\revela.exe theme extract Lumina MyTheme` creates a copy to edit
- **Install plugins:** For extended features like OneDrive integration or statistics
- **Upload to a server:** Upload the `output` folder via FTP/SFTP

---

## Help & Support

- **GitHub Issues:** https://github.com/spectara/revela/issues
- **Documentation:** https://github.com/spectara/revela/tree/main/docs

Feel free to create an issue on GitHub if you have questions or problems!

# Revela Quick Start Guide

Welcome to **Revela** - a modern static site generator for photographers.

## 🚀 Getting Started

### 1. Extract Files
Place `revela.exe` (or `revela` on Linux/macOS) in any folder.

### 2. Create a New Project
```bash
# Navigate to your photos folder
cd /path/to/my-photos

# Initialize a new project
revela init project
```

This creates:
- `site.json` - Site settings (title, author, etc.)
- `project.json` - Project settings (image sizes, formats, etc.)
- `source/` - Put your photos here (organized in folders = galleries)

**Note:** Revela includes the **Expose** theme by default - a beautiful, minimal photography theme.

### 3. Add Your Photos
```
source/
├── 01 Portraits/
│   ├── photo1.jpg
│   └── photo2.jpg
├── 02 Landscapes/
│   ├── mountains.jpg
│   └── sunset.jpg
└── _about.md          # Optional: pages (underscore prefix)
```

### 4. Generate Your Site
```bash
revela generate
```

Your site is ready in the `output/` folder!

### 5. Preview
Open `output/index.html` in your browser, or use any local web server.

---

## 📦 Installing Plugins

Plugins extend Revela with additional features.

```bash
# Install from ZIP file
revela plugin install --from-zip Spectara.Revela.Plugin.Source.OneDrive.zip

# List installed plugins
revela plugin list
```

---

## 🔧 Configuration

### site.json
```json
{
  "title": "My Photography Portfolio",
  "author": "Your Name",
  "description": "A collection of my best work",
  "copyright": "© 2025 Your Name"
}
```

### project.json
```json
{
  "source": "source",
  "output": "output",
  "theme": "expose",
  "generate": {
    "images": {
      "formats": { "jpg": 90 },
      "sizes": [640, 1280, 1920]
    }
  }
}
```

**Tip:** For production, enable modern formats for smaller files:
```json
"formats": { "avif": 80, "webp": 85, "jpg": 90 }
```

---

## 📚 More Information

- **Documentation:** https://revela.website
- **GitHub:** https://github.com/Spectara/Revela
- **Issues:** https://github.com/Spectara/Revela/issues

---

## 🎯 Common Commands

| Command | Description |
|---------|-------------|
| `revela init project` | Create a new project |
| `revela generate` | Build your site |
| `revela generate --watch` | Build and watch for changes |
| `revela clean` | Remove generated files |
| `revela plugin list` | Show installed plugins |
| `revela --help` | Show all commands |

---

Happy photographing! 📸

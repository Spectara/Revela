<div align="center">

![Revela](assets/revela_200.png)

# Revela

**Reveal your stories through beautiful portfolios**

Modern static site generator for photographers, built with .NET 10.

[![CI](https://github.com/spectara/revela/actions/workflows/ci.yml/badge.svg)](https://github.com/spectara/revela/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

🌐 **[revela.website](https://revela.website)** — Documentation & Demo

[Getting Started](https://revela.website/docs/) · [Download](https://github.com/spectara/revela/releases) · [GitHub](https://github.com/spectara/revela)

</div>

---

> [!NOTE]
> **🚧 Beta Release**
> 
> Revela is ready for testing! Features working:
> Setup Wizard • Project Wizard • Image Processing • Plugin System • Local Dev Server
>
> **[Download Latest Release →](https://github.com/spectara/revela/releases)**

---

## ✨ Features

- **🖼️ Smart Image Processing** — WebP, JPG (AVIF optional) with responsive sizes
- **🧙 Interactive Wizards** — No command line knowledge required
- **📁 Multi-Project** — Manage multiple portfolios from one installation
- **🔌 Plugin System** — Extend with Compress, OneDrive, Statistics, Dev Server
- **🎨 Themeable** — Customizable templates with Scriban
- **⚡ Fast** — Powered by libvips, parallel processing
- **📱 Responsive** — Works on phone, tablet, desktop

---

## 🚀 Quick Start

### 1. Download & Run

Download from [Releases](https://github.com/spectara/revela/releases), extract, and double-click `revela.exe`.

**That's it!** The Setup Wizard guides you through the rest.

### 2. Create Project

The Project Wizard appears automatically and guides you through:

1. **Project settings** — Name and URL
2. **Theme selection** — Choose your look
3. **Image settings** — Formats and sizes
4. **Site metadata** — Title, author, copyright

<!-- 
### Screenshot: Project Wizard
![Project Wizard](assets/screenshots/project-wizard.png)
*4-step project creation*
-->

### 3. Add Photos

Create folders in `source/` — folder names become gallery titles:

```
source/
├── 01 Weddings/
│   └── *.jpg
├── 02 Portraits/
│   └── *.jpg
└── 03 Landscapes/
    └── *.jpg
```

### 4. Generate

Select **generate** → **all** from the menu:

<!-- 
### Screenshot: Generate Progress
![Generate](assets/screenshots/generate-progress.png)
*Progress bar during generation*
-->

```
Processing images [████████████████████] 100% 47/47
Rendering pages   [████████████████████] 100% 12/12

✓ Generation complete!
```

### 5. Preview

With the Serve plugin installed:

```bash
revela serve
```

Your browser opens automatically with a live preview.

---

## 📦 Installation Options

| Method | Best For | Command |
|--------|----------|---------|
| **Standalone** | Most users | [Download ZIP](https://github.com/spectara/revela/releases) |
| **.NET Tool** | Developers | `dotnet tool install -g Spectara.Revela` |
| **From Source** | Contributors | See [Development Guide](docs/development.md) |

**[Detailed Installation Guide →](https://revela.website/docs/)**

---

## 🔌 Official Plugins

Install via the Setup Wizard or manually:

| Plugin | Description |
|--------|-------------|
| **Compress** | Pre-compress static files with Gzip/Brotli |
| **Serve** | Local dev server with live preview |
| **Statistics** | Image count, sizes, analytics |
| **Source.OneDrive** | Import from OneDrive shared folders |

```bash
revela plugin install Spectara.Revela.Plugins.Serve
```

**[Plugin Management Guide →](https://revela.website/docs/)**

---

## 📖 Documentation

Visit **[revela.website/docs](https://revela.website/docs/)** for the full documentation:

- **[Source Structure](https://revela.website/docs/source-structure/)** — Organize photos with galleries or filters
- **[Filter Galleries](https://revela.website/docs/filtering/)** — Dynamic galleries with EXIF queries
- **[Sorting](https://revela.website/docs/sorting/)** — Configure image and gallery order
- **[Creating Pages](https://revela.website/docs/pages/)** — Gallery, text, and statistics pages
- **[Theme Customization](https://revela.website/docs/themes/)** — Extract and customize themes

**Offline/GitHub:** [docs/](docs/) folder contains the same documentation in Markdown.

---

## 🛠️ For Developers

```bash
git clone https://github.com/spectara/revela.git
cd revela
dotnet build
dotnet run --project src/Cli
```

**[Development Guide →](docs/development.md)**

---

## 🤝 Contributing

Contributions welcome! Please open an [issue](https://github.com/spectara/revela/issues) or pull request.

## 📄 License

[MIT License](LICENSE)

## 🙏 Acknowledgments

- [Expose](https://github.com/kirkone/Expose) — Original inspiration
- [libvips](https://www.libvips.org/) — Image processing
- [Scriban](https://github.com/scriban/scriban) — Templates
- [CSS-only LQIP](https://leanrada.com/notes/css-only-lqip/) — Blur placeholder technique by Lean Rada

---

<div align="center">

**[⬆ Back to top](#revela)**

</div>


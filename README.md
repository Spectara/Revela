<div align="center">

![Revela](assets/revela_200.png)

# Revela

**Reveal your stories through beautiful portfolios**

Modern static site generator for photographers, built with .NET 10.

[![CI](https://github.com/spectara/revela/actions/workflows/ci.yml/badge.svg)](https://github.com/spectara/revela/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

[Getting Started](docs/getting-started/getting-started-en.md) · [Documentation](docs/) · [Download](https://github.com/spectara/revela/releases)

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
- **🔌 Plugin System** — Extend with OneDrive, Statistics, Dev Server
- **🎨 Themeable** — Customizable templates with Scriban
- **⚡ Fast** — Powered by libvips, parallel processing
- **📱 Responsive** — Works on phone, tablet, desktop

---

## 🚀 Quick Start

### 1. Download & Run

Download from [Releases](https://github.com/spectara/revela/releases), extract, and double-click `revela.exe`.

**That's it!** The Setup Wizard guides you through the rest.

<!-- 
### Screenshot: Setup Wizard
![Setup Wizard](assets/screenshots/setup-wizard.png)
*First run: Install themes and plugins*
-->

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
revela serve start
```

Your browser opens automatically with a live preview.

---

## 📦 Installation Options

| Method | Best For | Command |
|--------|----------|---------|
| **Standalone** | Most users | [Download ZIP](https://github.com/spectara/revela/releases) |
| **.NET Tool** | Developers | `dotnet tool install -g Spectara.Revela` |
| **From Source** | Contributors | See [Development Guide](docs/development.md) |

**[Detailed Installation Guide →](docs/installation.md)**

---

## 🔌 Official Plugins

Install via the Setup Wizard or manually:

| Plugin | Description |
|--------|-------------|
| **Serve** | Local dev server with live preview |
| **Statistics** | Image count, sizes, analytics |
| **Source.OneDrive** | Import from OneDrive shared folders |

```bash
revela plugin install Spectara.Revela.Plugin.Serve
```

**[Plugin Management Guide →](docs/plugin-management.md)**

---

## 📖 Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/getting-started/getting-started-en.md) | Step-by-step tutorial |
| [Erste Schritte (DE)](docs/getting-started/getting-started-de.md) | Deutsche Anleitung |
| [CLI Reference](docs/getting-started/cli-reference.md) | Commands for automation |
| [Installation](docs/installation.md) | All installation options |
| [Plugin Management](docs/plugin-management.md) | Install & create plugins |
| [Development](docs/development.md) | Build & contribute |
| [Architecture](docs/architecture.md) | Technical overview |

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

---

<div align="center">

**[⬆ Back to top](#revela)**

🌐 [revela.website](https://revela.website) · 🏢 [Spectara](https://github.com/spectara)

</div>


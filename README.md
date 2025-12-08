# Revela

[![Build](https://github.com/spectara/revela/actions/workflows/build.yml/badge.svg)](https://github.com/spectara/revela/actions/workflows/build.yml)
[![Dependencies](https://github.com/spectara/revela/actions/workflows/dependency-update-check.yml/badge.svg)](https://github.com/spectara/revela/actions/workflows/dependency-update-check.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> [!NOTE]
> **🚧 BETA - Core Features Complete 🚧**
> 
> Revela is in beta (v1.0.0-dev). All core features are working!
> 
> **Working Features:**
> - ✅ Site generation (`revela generate`) - **Full image processing & template rendering**
> - ✅ Project initialization (`revela init project`)
> - ✅ Plugin management (`revela plugin list/install/uninstall`)
> - ✅ Theme management (`revela theme list/extract`)
> - ✅ Dependency restore (`revela restore`)
> - ✅ OneDrive source plugin
> 
> **Coming Soon:**
> - ⏳ Watch mode with auto-rebuild
> - ⏳ Local dev server with hot reload
> - ⏳ Deploy plugins (SSH, Azure)
> 
> **Ready for Testing!** Star ⭐ and watch this repo for updates.

**Reveal your stories through beautiful portfolios**

Modern static site generator for photographers, built with .NET 10 and optimized for performance.

🌐 **Website:** [revela.website](https://revela.website)  
🏢 **Organization:** [Spectara](https://github.com/spectara)

> **Note:** This is a complete rewrite of the original [Expose](https://github.com/kirkone/Expose) project from Bash to .NET 10.  
> **Goal:** Same beautiful output, better performance, more features.

## 🚀 Features

- **High-Performance Image Processing** - Powered by NetVips (libvips)
- **Modern Templates** - Scriban template engine with Liquid-like syntax
- **Plugin System** - Extensible architecture with NuGet-based plugins
- **Responsive Images** - Multiple formats (WebP, AVIF, JPG) and sizes
- **EXIF Support** - Automatic camera settings extraction
- **Markdown Content** - Frontmatter + Markdown for pages
- **Smart Caching** - Fast rebuilds with intelligent caching

## 📦 Installation

### As .NET Tool (Global)

```bash
dotnet tool install -g Revela
```

### From Source

```bash
git clone https://github.com/spectara/revela.git
cd revela
dotnet build
dotnet pack src/Cli
dotnet tool install -g --add-source ./artifacts/packages Revela
```

## 🎯 Quick Start

### 1. Create a New Site

```bash
mkdir my-photo-site
cd my-photo-site
revela init project
```

### 2. Add Your Photos

```
my-photo-site/
├── project.json
├── site.json
└── content/
    ├── photo1.jpg
    ├── photo2.jpg
    └── galleries/
        └── vacation/
            └── *.jpg
```

### 3. Configure

Edit `project.json`:

```json
{
  "name": "my-photo-site",
  "url": "https://example.com",
  "theme": "default"
}
```

Edit `site.json`:

```json
{
  "title": "My Photography",
  "author": "Your Name",
  "description": "Photography portfolio by Your Name"
}
```

### 4. Generate

```bash
revela generate
```

Output in `./output/`

## ⌨️ Shell Completion

Enable tab-completion for commands:

```bash
# Install dotnet-suggest (once)
dotnet tool install --global dotnet-suggest

# Add to your shell profile
# PowerShell: Add to $PROFILE
# Bash/Zsh: Follow dotnet-suggest instructions

dotnet suggest register
```

Then use `revela th<TAB>` → `revela theme`

## 🔌 Plugins

### Official Plugins (Verified by Spectara)

All plugins with the `Spectara.Revela.Plugin.*` prefix are officially maintained and verified by Spectara.

```bash
# Official OneDrive Source Plugin
revela plugin install Spectara.Revela.Plugin.Source.OneDrive

# Official Deploy Plugin (SSH/SFTP) - Coming Soon
revela plugin install Spectara.Revela.Plugin.Deploy.SSH
```

**Package Names:**
- `Spectara.Revela.Plugin.Source.OneDrive` ✅ **Verified**
- `Spectara.Revela.Plugin.Deploy.SSH` ✅ **Verified** (Coming Soon)

### Community Plugins

Community plugins use their own prefix and are maintained by third-party developers.

**Example:**
- `JohnDoe.Revela.Plugin.AWS` ⚠️ Community Plugin
- `CommunityDev.Revela.Plugin.FTP` ⚠️ Community Plugin

**Note:** Community plugins are not officially verified by Spectara. Install at your own risk.

### Security

The `Spectara` prefix is **reserved on NuGet.org** and can only be used by the Spectara organization. This ensures that all `Spectara.Revela.*` packages are authentic and trustworthy.

### List Plugins

```bash
revela plugin list
```

### Uninstall Plugins

```bash
revela plugin uninstall onedrive
```

## 📖 Documentation

- [Getting Started](docs/getting-started.md)
- [Configuration Reference](docs/configuration.md)
- [Template Guide](docs/templates.md)
- [Plugin Development](docs/plugin-development.md)

## 🏗️ Architecture

- **Vertical Slice Architecture** - Features are self-contained
- **Plugin System** - Extensible via NuGet packages
- **NetVips** - High-performance image processing
- **Scriban** - Powerful template engine
- **System.CommandLine 2.0** - Modern CLI framework

## 🛠️ Development

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022 / VS Code / Rider

### Build

```bash
dotnet restore
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run Locally

```bash
dotnet run --project src/Cli -- init project
dotnet run --project src/Cli -- generate
```

### Check for Dependency Updates

```bash
# Install dotnet-outdated tool (once)
dotnet tool install --global dotnet-outdated-tool

# Check for updates
dotnet outdated

# Update packages
dotnet outdated -u:prompt
```

See [Dependency Management](.github/DEPENDENCY_MANAGEMENT.md) for details.

## 🤝 Contributing

Contributions welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) first.

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🙏 Acknowledgments

- [Original Expose](https://github.com/kirkone/Expose) - Inspiration for this project
- [libvips](https://www.libvips.org/) - Fast image processing library
- [Scriban](https://github.com/scriban/scriban) - Template engine
- [System.CommandLine](https://github.com/dotnet/command-line-api) - CLI framework

## 💡 Why "Revela"?

**Revela** comes from the Latin *revelare* meaning "to reveal" or "to unveil" - perfectly capturing what photographers do: revealing stories and moments through their images.


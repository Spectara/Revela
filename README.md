<div align="center">

![Revela](assets/Spectara_200.png)

# Revela

[![Build](https://github.com/spectara/revela/actions/workflows/build.yml/badge.svg)](https://github.com/spectara/revela/actions/workflows/build.yml)
[![Dependencies](https://github.com/spectara/revela/actions/workflows/dependency-update-check.yml/badge.svg)](https://github.com/spectara/revela/actions/workflows/dependency-update-check.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/spectara/revela)

</div>

> [!NOTE]
> **🚧 BETA - Ready for Testing! 🚧**
> 
> Revela v0.1.0-beta is available for testing.
> 
> **Working Features:**
> - ✅ Site generation (`revela generate`) - Full image processing & template rendering
> - ✅ Project initialization (`revela init project`)
> - ✅ Plugin system with dependency isolation
> - ✅ Plugin management (`revela plugin list/install/uninstall`)
> - ✅ Theme management (`revela theme list/extract`)
> - ✅ OneDrive source plugin
> - ✅ Multi-platform builds (Windows, Linux, macOS)
> 
> **Coming Soon:**
> - ⏳ Watch mode with auto-rebuild
> - ⏳ Local dev server with hot reload
> - ⏳ Deploy plugins (SSH, Azure)
> 
> **Download:** [Latest Release](https://github.com/Spectara/Revela/releases)
> 
> Star ⭐ and watch this repo for updates.

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

Choose the installation method that best fits your needs:

### Option A: .NET Global Tool (Recommended for Developers)

**Requirements:** .NET Runtime 10.0 or later

```bash
# Install from NuGet.org
dotnet tool install -g Spectara.Revela

# Verify installation
revela --version
```

**Advantages:**
- ✅ Small download size (~10 MB)
- ✅ Easy updates with `dotnet tool update -g Spectara.Revela`
- ✅ Automatic PATH configuration

### Option B: Native Executable (For Users Without .NET SDK)

**Requirements:** None - fully self-contained

**Download for your platform:**
- [Windows (x64)](https://github.com/spectara/revela/releases/latest) - `revela-win-x64.zip`
- [Linux (x64)](https://github.com/spectara/revela/releases/latest) - `revela-linux-x64.tar.gz`
- [Linux (ARM64)](https://github.com/spectara/revela/releases/latest) - `revela-linux-arm64.tar.gz`
- [macOS (Intel)](https://github.com/spectara/revela/releases/latest) - `revela-osx-x64.tar.gz`
- [macOS (Apple Silicon)](https://github.com/spectara/revela/releases/latest) - `revela-osx-arm64.tar.gz`

**Extract and run:**
```bash
# Windows
Expand-Archive revela-win-x64.zip -DestinationPath C:\Tools\revela
$env:PATH += ";C:\Tools\revela"

# Linux/macOS
tar -xzf revela-linux-x64.tar.gz -C /usr/local/bin
chmod +x /usr/local/bin/revela
```

**Advantages:**
- ✅ No .NET Runtime required
- ✅ Works on systems without SDK
- ✅ Ideal for CI/CD environments

### Option C: From Source (For Contributors)

```bash
git clone https://github.com/spectara/revela.git
cd revela
dotnet restore
dotnet build
dotnet pack src/Cli -c Release
dotnet tool install -g --add-source ./artifacts/packages Spectara.Revela
```

## 📖 Getting Started Guides

New to Revela? Check out our step-by-step guides:

| Language | Guide |
|----------|-------|
| 🇬🇧 English | [Getting Started](docs/getting-started/en.md) |
| 🇩🇪 Deutsch | [Erste Schritte](docs/getting-started/de.md) |

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
└── source/
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
  "theme": "lumina",
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

# Official Statistics Plugin
revela plugin install Spectara.Revela.Plugin.Statistics

# Official Deploy Plugin (SSH/SFTP) - Coming Soon
revela plugin install Spectara.Revela.Plugin.Deploy.SSH
```

**Package Names:**
- `Spectara.Revela.Plugin.Source.OneDrive` ✅ **Verified**
- `Spectara.Revela.Plugin.Statistics` ✅ **Verified**
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

- [Getting Started](docs/getting-started/README.md)
- [Architecture Overview](docs/architecture.md)
- [Plugin Development](docs/plugin-development.md)
- [HttpClient Pattern](docs/httpclient-pattern.md)

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

Contributions welcome! Please open an issue or pull request on GitHub.

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

## 🙏 Acknowledgments

- [Original Expose](https://github.com/kirkone/Expose) - Inspiration for this project
- [libvips](https://www.libvips.org/) - Fast image processing library
- [Scriban](https://github.com/scriban/scriban) - Template engine
- [System.CommandLine](https://github.com/dotnet/command-line-api) - CLI framework

## 💡 Why "Revela"?

**Revela** comes from the Latin *revelare* meaning "to reveal" or "to unveil" - perfectly capturing what photographers do: revealing stories and moments through their images.


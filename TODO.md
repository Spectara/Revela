# TODO - Revela Development Tasks

**Last Updated:** 2025-12-11  
**Current Version:** v0.1.0-beta  
**Repository:** https://github.com/Spectara/Revela

---

## 📟 COMMAND OVERVIEW

### ✅ Working Commands

```bash
# Project Initialization
revela init project [--name <name>] [--author <author>]  # ✅ WORKING
revela init theme --name <theme-name>                     # ✅ WORKING

# Site Generation
revela generate                                           # ✅ WORKING
revela clean                                              # ✅ WORKING

# Plugin Management
revela plugin list                                        # ✅ WORKING
revela plugin install <name>                              # ✅ WORKING (NuGet + ZIP)
revela plugin install --from-zip <path>                   # ✅ WORKING
revela plugin uninstall <name>                            # ✅ WORKING

# Theme Management
revela theme list                                         # ✅ WORKING
revela theme extract                                      # ✅ WORKING

# Dependency Management
revela restore                                            # ✅ WORKING

# OneDrive Source Plugin
revela source onedrive sync                               # ✅ WORKING
```

### ⏳ Not Yet Implemented

```bash
# Watch Mode (v1.1+)
revela generate --watch                                   # ⏳ TODO

# Dev Server (v1.1+)
revela serve [--port <port>]                             # ⏳ TODO

# Deploy Plugins (Future)
revela deploy ssh                                         # ⏳ TODO
revela deploy azure                                       # ⏳ TODO
```

---

## ✅ COMPLETED FEATURES

### Core Features (v0.1.0)

- [x] **GenerateCommand** - Full site generation
  - [x] Content scanning with gallery tree building
  - [x] NetVips image processing (resize, optimize)
  - [x] Multi-format output (AVIF, WebP, JPG)
  - [x] Multi-size generation (responsive images)
  - [x] EXIF extraction from images
  - [x] Smart caching with image manifest
  - [x] Parallel processing (5× speedup)

- [x] **Template Engine**
  - [x] Scriban integration
  - [x] Custom functions (url_for, format_exposure, etc.)
  - [x] Partial templates support
  - [x] Layout inheritance
  - [x] Navigation builder

- [x] **Content Processing**
  - [x] Markdown parsing (Markdig)
  - [x] Frontmatter extraction (YAML)
  - [x] Gallery metadata (_index.md)
  - [x] Automatic navigation generation

- [x] **Plugin System**
  - [x] Plugin discovery & loading
  - [x] AssemblyLoadContext isolation (no dependency conflicts)
  - [x] NuGet-based installation
  - [x] ZIP installation with dependencies
  - [x] Self-contained plugins with .deps.json

- [x] **Theme System**
  - [x] Embedded themes (Theme.Expose)
  - [x] Theme extraction for customization
  - [x] Custom theme support

### Plugins (v0.1.0)

- [x] **Theme.Expose** - Default photography theme
  - [x] Responsive design
  - [x] Gallery navigation
  - [x] Image lightbox
  - [x] EXIF display

- [x] **Plugin.Source.OneDrive** - OneDrive shared folder source
  - [x] Badger API authentication (no OAuth)
  - [x] Smart file filtering (images + markdown)
  - [x] Parallel downloads (configurable)
  - [x] Progress reporting
  - [x] Incremental sync (--dry-run, --clean)

### Build & Release (v0.1.0)

- [x] **GitHub Actions Pipeline**
  - [x] Automated build on push
  - [x] Multi-platform releases (Windows, Linux, macOS)
  - [x] Single-file executables (~100 MB)
  - [x] Plugin packaging with dependencies
  - [x] QUICKSTART.md in release ZIP

---

## ⏳ IN PROGRESS

Nothing currently in progress.

---

## 🟡 NEXT PRIORITIES (v1.0 Release)

### Watch Mode
- [ ] File system watcher for source directory
- [ ] Incremental rebuild (only changed files)
- [ ] Console output for changes

### Dev Server
- [ ] Local HTTP server for preview
- [ ] Hot reload on changes
- [ ] Browser auto-refresh

### Documentation
- [ ] Complete Getting Started guide
- [ ] Configuration reference
- [ ] Theme development guide
- [ ] Plugin development tutorial

### Testing
- [ ] Increase unit test coverage
- [ ] End-to-end tests for generate workflow
- [ ] Cross-platform testing (Linux, macOS)

---

## 🟢 FUTURE (v1.1+)

### Deploy Plugins
- [ ] **Deploy.SSH** - SSH/SFTP deployment
- [ ] **Deploy.Azure** - Azure Blob Storage / Static Web Apps
- [ ] **Deploy.S3** - AWS S3 deployment

### Additional Features
- [ ] Image watermarking
- [ ] Custom image filters
- [ ] SEO optimization (sitemap, robots.txt)
- [ ] RSS feed generation
- [ ] Social media meta tags

### GUI
- [ ] Desktop app (WPF/MAUI)
- [ ] Visual theme editor
- [ ] Drag & drop gallery management

---

## 📊 Version History

| Version | Date | Highlights |
|---------|------|------------|
| v0.1.0-beta | 2025-12-11 | First public test build, full generate, plugin system |

---

## 🔗 Links

- **Repository:** https://github.com/Spectara/Revela
- **Website:** https://revela.website
- **Issues:** https://github.com/Spectara/Revela/issues

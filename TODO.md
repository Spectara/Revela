# TODO - Revela Development Tasks

**Last Updated:** 2025-01-20  
**Current Version:** v1.0.0-dev  
**Repository:** https://github.com/Spectara/Revela

---

## 📟 COMMAND OVERVIEW

### ✅ Working Commands

```bash
# Project Initialization
revela init project [--name <name>] [--author <author>]  # ✅ WORKING
revela init theme --name <theme-name>                     # ✅ WORKING

# Plugin Management
revela plugin list                                        # ✅ WORKING
revela plugin install <plugin-name>                       # ⚠️ PARTIAL (placeholder)
revela plugin uninstall <plugin-name>                     # ✅ WORKING

# OneDrive Source Plugin (Official)
revela source onedrive init [--share-url <url>]          # ✅ WORKING
revela source onedrive download                           # ✅ WORKING (26 files tested)
```

### ❌ Not Yet Implemented

```bash
# Site Generation (CRITICAL - Main Feature!)
revela generate [--config <path>]                        # ❌ TODO
revela generate --watch                                   # ❌ TODO (v1.1+)

# Deploy Plugin (Official - Future)
revela deploy ssh init                                    # ❌ TODO
revela deploy ssh upload                                  # ❌ TODO

# Development Tools (Future)
revela serve [--port <port>]                             # ❌ TODO (v1.1+)
revela clean                                              # ❌ TODO
```

### 🔧 Command Details

#### `revela init project`
- **Status:** ✅ Fully working
- **Creates:** `project.json`, `site.json`, `content/` directory
- **Options:**
  - `--name, -n`: Project name (defaults to directory name)
  - `--author, -a`: Author name (defaults to username)

#### `revela init theme`
- **Status:** ✅ Fully working
- **Creates:** `themes/{name}/` with layout.html, index.html, gallery.html
- **Options:**
  - `--name, -n`: Theme name (required)

#### `revela plugin list`
- **Status:** ✅ Fully working
- **Shows:** All installed plugins with metadata

#### `revela plugin install`
- **Status:** ⚠️ Partially implemented (placeholder)
- **TODO:** Actual NuGet download and extraction

#### `revela plugin uninstall`
- **Status:** ✅ Fully working
- **Removes:** Plugin directory from `%APPDATA%/Revela/plugins/`

#### `revela source onedrive init`
- **Status:** ✅ Fully working
- **Creates:** `onedrive.json` config, `source/` directory
- **Options:**
  - `--share-url, -u`: OneDrive share URL (optional, interactive prompt)

#### `revela source onedrive download`
- **Status:** ✅ Fully working
- **Features:**
  - Badger API authentication (no OAuth needed)
  - Smart file filtering (images via MIME type + markdown)
  - Parallel downloads (6 concurrent by default)
  - Progress reporting with Spectre.Console
  - Token caching (7-day validity)
- **Tested:** 26 files successfully downloaded

#### `revela generate` ⏳ COMING SOON
- **Status:** ❌ Not yet implemented (TOP PRIORITY!)
- **Planned Features:**
  - Process images with NetVips (resize, optimize, EXIF)
  - Render templates with Scriban
  - Generate responsive HTML site
  - Create gallery structure
  - Copy assets
- **Target:** v1.0.0-alpha

---

## 🔴 CRITICAL (Blocking v1.0)

### Core Features

- [ ] **GenerateCommand** - Site generation orchestration
  - [ ] NetVipsImageProcessor implementation
  - [ ] ScribanTemplateEngine implementation
  - [ ] Site generation workflow
  - [ ] Gallery structure processing
  - [ ] First test site generation

- [ ] **Image Processing**
  - [ ] NetVips integration
  - [ ] Multi-format support (WebP, AVIF, JPG)
  - [ ] Multi-size generation (responsive images)
  - [ ] EXIF extraction from images
  - [ ] Image caching strategy
  - [ ] Thumbnail generation

- [ ] **Template Engine**
  - [ ] Scriban integration
  - [ ] Custom functions (url_for, asset, image_url)
  - [ ] Partial templates support
  - [ ] Layout inheritance
  - [ ] Template caching

- [ ] **Content Processing**
  - [ ] Markdown parsing (Markdig)
  - [ ] Frontmatter extraction
  - [ ] Gallery metadata processing
  - [ ] Navigation generation

---

## 🟡 HIGH PRIORITY (v1.0 Release)

### Stability & Testing

- [ ] **Unit Tests**
  - [ ] PluginLoader tests
  - [ ] PluginManager tests
  - [ ] ScaffoldingService tests
  - [ ] Config loading tests
  - [ ] Template rendering tests
  - [ ] Image processing tests

- [ ] **Integration Tests**
  - [x] OneDrive Plugin e2e ✅ (26 files downloaded successfully)
  - [ ] Generate workflow e2e
  - [ ] Plugin install/uninstall workflow
  - [ ] Full site generation test

### Documentation

- [ ] **User Documentation**
  - [ ] Getting Started guide
  - [ ] Configuration reference (project.json, site.json)
  - [ ] Template development guide
  - [ ] Theme customization guide
  - [ ] Troubleshooting section

- [ ] **Developer Documentation**
  - [ ] Architecture deep-dive
  - [ ] Plugin development tutorial
  - [ ] Contributing guide
  - [ ] Code style guide

### Error Handling

- [ ] Network failure handling (OneDrive, downloads)
- [ ] File system error handling
- [ ] Plugin loading error recovery
- [ ] Configuration validation errors
- [ ] Build error reporting

---

## 🟢 MEDIUM PRIORITY (v1.1+)

### Official Plugins

- [x] ~~OneDrive Plugin~~ ✅ **COMPLETE** (2025-01-20)
  - [x] SharedLinkProvider with Badger API
  - [x] Smart file filtering (images + markdown)
  - [x] Parallel downloads (configurable concurrency)
  - [x] Progress reporting (Spectre.Console)
  - [x] Token caching (7-day validity)

- [ ] **Deploy Plugin (SSH/SFTP)**
  - [ ] SSH.NET client integration
  - [ ] SFTP upload implementation
  - [ ] Deploy command (revela deploy ssh)
  - [ ] Deploy config setup
  - [ ] Incremental upload (only changed files)
  - [ ] Deploy rollback support

### Tooling & Automation

- [ ] **CI/CD Pipeline (GitHub Actions)**
  - [ ] Automated build on push
  - [ ] Run tests on PR
  - [ ] Dependency security scanning
  - [ ] Release automation
  - [ ] NuGet package publishing
  - [ ] GitHub Release creation

- [ ] **Development Tools**
  - [ ] Watch mode (auto-rebuild on file change)
  - [ ] Dev server with live reload
  - [ ] Performance profiling tools
  - [ ] Build time optimization

### Website & Publishing

- [ ] **revela.website Setup**
  - [ ] Landing page design
  - [ ] Documentation site (static or Docusaurus)
  - [ ] Plugin showcase
  - [ ] Examples gallery
  - [ ] Download instructions
  - [ ] Community section

- [ ] **NuGet Publishing**
  - [ ] Publish Revela.Cli as .NET Tool
  - [ ] Publish Core libraries
  - [ ] Publish official plugins
  - [ ] Setup package signing
  - [ ] Setup SymbolSource for debugging

---

## 🔵 LOW PRIORITY (Future / v2.0+)

### Advanced Features

- [ ] Watch mode with incremental rebuilds
- [ ] Dev server with live reload (WebSocket)
- [ ] Image optimization profiles (quality presets)
- [ ] Custom plugin hooks (before/after generation)
- [ ] Theme marketplace integration
- [ ] Multi-language support (i18n)
- [ ] SEO optimization tools
- [ ] Sitemap generation
- [ ] RSS feed generation
- [ ] Social media meta tags

### Community Plugins (Ideas)

- [ ] AWS S3 deploy plugin
- [ ] Azure Blob Storage deploy plugin
- [ ] Cloudflare Pages plugin
- [ ] Netlify plugin
- [ ] Google Photos source plugin
- [ ] Flickr source plugin
- [ ] Dropbox source plugin
- [ ] Instagram import plugin

### GUI (Future)

- [ ] WPF desktop app (Windows)
- [ ] MAUI cross-platform app
- [ ] Electron-based app
- [ ] Web-based admin panel

---

## ✅ COMPLETED

### Session 2025-01-20 (Code Quality & OneDrive Plugin)

- [x] `.editorconfig` perfected - `_camelCase` permanently banned! 🚫
- [x] Anti-underscore naming rule with detailed documentation
- [x] OneDrive Plugin **COMPLETE** and tested
  - [x] SharedLinkProvider implementation (Badger API)
  - [x] OneDriveSourceCommand (download workflow)
  - [x] OneDriveInitCommand (config setup)
  - [x] Smart file filtering (MIME type + patterns)
  - [x] Two-phase progress display (Scan + Download)
  - [x] 26 files successfully downloaded in testing
- [x] Typed HttpClient pattern documented
- [x] `copilot-instructions.md` updated
  - [x] Corrected naming examples (no underscore)
  - [x] Updated code samples
- [x] Build successful (zero errors, zero warnings)
- [x] All changes committed and pushed to GitHub

### Session 2025-01-19 (Foundation & Setup)

- [x] Complete rename: Expose → Revela → Spectara.Revela
- [x] Plugin System architecture designed
  - [x] IPlugin interface
  - [x] PluginLoader (reflection-based discovery)
  - [x] PluginManager (NuGet-based installation)
  - [x] 3-phase plugin lifecycle (ConfigureServices → Initialize → GetCommands)
- [x] Init commands implemented
  - [x] `revela init project`
  - [x] `revela init theme`
- [x] ScaffoldingService with embedded templates
- [x] Plugin management commands
  - [x] `revela plugin list`
  - [x] `revela plugin install`
  - [x] `revela plugin uninstall`
- [x] Git repository setup
  - [x] GitHub repository created
  - [x] All code pushed to main branch
  - [x] `.gitignore` configured
- [x] Domain purchased: **revela.website** 🌐
- [x] NuGet prefix reservation requested: "Spectara"

---

## 📝 NOTES & DECISIONS

### Next Session Goals

**Primary Goal:** GenerateCommand implementation  
**Milestone:** First site generation working  
**Success Criteria:** `revela generate` produces a functional HTML site

### Architecture Decisions

✅ **Naming Convention:**
- Private instance fields: `camelCase` (NO underscore!)
- Const fields: `PascalCase`
- Static readonly: `PascalCase`
- **Rationale:** Modern C# style, better readability, matches Microsoft's codebase

✅ **Global Usings:**
- Do NOT use global usings for `Microsoft.Extensions.Logging`
- **Rationale:** Source Generator compatibility (LoggerMessage pattern requires explicit using)

✅ **using Directive Placement:**
- `outside_namespace:warning` (C# 10+ standard)
- **Rationale:** File-scoped namespaces make inside-namespace placement redundant

✅ **Plugin Architecture:**
- NuGet-based plugin distribution
- Official plugins: `Spectara.Revela.Plugin.*` (verified ✅)
- Community plugins: `YourName.Revela.Plugin.*` (community ⚠️)
- **Rationale:** Security, trust, and ecosystem growth

✅ **HttpClient Pattern:**
- Always use Typed Client pattern for plugins
- Register in `ConfigureServices`, inject directly in constructor
- **Rationale:** Type safety, connection pooling, testability

### Known Issues

- [ ] PluginManager: NuGet install not fully implemented (placeholder code)
- [ ] No error handling for network failures in OneDrive plugin
- [ ] Some CA-warnings suppressed (CA1031, CA1062, CA2007) - need review
- [ ] No retry logic for failed downloads

### Technical Debt

- [ ] Add retry logic with Polly for HTTP requests
- [ ] Implement proper NuGet package download in PluginManager
- [ ] Add telemetry/analytics (optional, privacy-respecting)
- [ ] Performance profiling for large galleries
- [ ] Memory optimization for large image processing

### Design Considerations

**Templates:**
- Default theme embedded in Infrastructure/Scaffolding/Templates/
- User themes in `themes/{name}/` (optional override)
- Fallback chain: User theme → Built-in theme
- **Status:** ⏳ Template engine not yet implemented

**Image Processing:**
- NetVips for performance (3-5× faster than ImageSharp)
- Multi-format: WebP (primary), AVIF (future), JPG (fallback)
- Multi-size: 640, 1024, 1280, 1920, 2560 (responsive)
- **Status:** ⏳ NetVips integration pending

**Caching:**
- EXIF cache: JSON files in `.revela/cache/exif/`
- Image cache: Generated images in `.revela/cache/images/`
- HTML cache: Rendered pages in `.revela/cache/html/`
- **Status:** ⏳ Caching strategy not yet implemented

---

## 🎯 PRIORITY MATRIX

| Task | Impact | Effort | Priority |
|------|--------|--------|----------|
| GenerateCommand | 🔥🔥🔥 | High | 🔴 CRITICAL |
| NetVips Integration | 🔥🔥🔥 | Medium | 🔴 CRITICAL |
| Scriban Integration | 🔥🔥🔥 | Medium | 🔴 CRITICAL |
| Unit Tests | 🔥🔥 | Medium | 🟡 HIGH |
| Deploy Plugin | 🔥🔥 | High | 🟢 MEDIUM |
| Website Setup | 🔥 | High | 🟢 MEDIUM |
| Watch Mode | 🔥 | Medium | 🔵 LOW |

---

## 🚀 RELEASE ROADMAP

### v1.0.0-alpha (Target: Q1 2025)
- [x] Project setup
- [x] Plugin system
- [x] OneDrive plugin
- [ ] Generate command (MVP)
- [ ] Basic documentation

### v1.0.0-beta (Target: Q2 2025)
- [ ] All core features complete
- [ ] Deploy plugin
- [ ] Comprehensive tests
- [ ] User documentation
- [ ] revela.website live

### v1.0.0 (Target: Q2 2025)
- [ ] Production-ready
- [ ] Published to NuGet
- [ ] Migration guide from original Expose
- [ ] Community feedback integrated

### v1.1.0 (Target: Q3 2025)
- [ ] Watch mode
- [ ] Dev server
- [ ] Performance optimizations
- [ ] Community plugins

---

**Legend:**
- 🔴 CRITICAL - Blocking release, must be done
- 🟡 HIGH - Important for quality and UX
- 🟢 MEDIUM - Nice to have, improves experience
- 🔵 LOW - Future considerations, low priority

---

**Contributing:**
See [CONTRIBUTING.md](CONTRIBUTING.md) for how to help with these tasks!

**Questions?**
- 💬 [GitHub Discussions](https://github.com/Spectara/Revela/discussions)
- 🐛 [Report Issues](https://github.com/Spectara/Revela/issues)
- 📧 Contact: https://spectara.dev

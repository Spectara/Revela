# Revela - Development Status

**Last Updated:** 2025-06-15

## 🔗 Original Project Reference

This is a **complete rewrite** of the original Bash-based Revela:
- **Original:** https://github.com/kirkone/Revela
- **Language:** Bash → .NET 10 / C# 14
- **Goal:** Same output, better performance, more extensibility

## 📊 Current Status: CLI & PLUGINS COMPLETE ✅

### ✅ Completed

#### Phase 1: Foundation (DONE)
- [x] Project structure created
- [x] All projects configured
- [x] Solution file created
- [x] Central Package Management (CPM) configured
- [x] .editorconfig with C# 14 best practices
- [x] Directory.Build.props with central settings
- [x] System.CommandLine 2.0.0 (final) integrated
- [x] NetVips 3.1.0 integrated

#### Core Models (DONE)
- [x] `Image.cs` - Image model with variants
- [x] `ExifData.cs` - EXIF metadata
- [x] `Gallery.cs` - Gallery with sub-galleries
- [x] `RevelaConfig.cs` - Complete configuration model

#### Core Abstractions (DONE)
- [x] `IPlugin.cs` - Plugin interface with 4-phase lifecycle
- [x] `IThemePlugin.cs` - Theme plugin interface
- [x] `ITemplateEngine.cs` - Template engine abstraction
- [x] `IImageProcessor.cs` - Image processing abstraction

#### Core Infrastructure (DONE)
- [x] `PluginLoader.cs` - Plugin discovery & loading
- [x] `PluginManager.cs` - Plugin install/update/uninstall (NuGet-based)
- [x] `PluginContext.cs` - Plugin lifecycle management
- [x] `EmbeddedThemePlugin.cs` - Base class for theme plugins

#### CLI Commands (DONE)
- [x] `init project` - Initialize new project
- [x] `plugin list` - List installed plugins
- [x] `plugin install` - Install plugin from NuGet
- [x] `plugin uninstall` - Uninstall plugin
- [x] `theme list` - List available themes
- [x] `theme extract` - Extract theme for customization
- [x] `restore` - Restore project dependencies
- [x] `source onedrive init` - Initialize OneDrive source
- [x] `source onedrive download` - Download from OneDrive shared link

#### Plugins (DONE)
- [x] `Theme.Expose` - Default Expose theme (embedded)
- [x] `Theme.Minimal` - Minimal theme (embedded)
- [x] `Plugin.Source.OneDrive` - OneDrive shared folder source

### 🚧 In Progress

#### Phase 2: Generate Command
1. [ ] **Site Generation**
   - Content scanning
   - Image processing pipeline
   - Template rendering
   - Output generation

### 📝 Next Steps

#### Phase 2: Generate Command
1. [ ] **Content Scanner**
   - Scan source directory for images
   - Parse metadata from JSON files
   - Build gallery tree

2. [ ] **Image Processing Pipeline**
   - NetVips multi-format output (WebP, AVIF, JPG)
   - Responsive image variants
   - EXIF extraction
   - Smart caching

3. [ ] **Template Rendering**
   - Scriban template engine
   - Custom functions (url_for, asset, etc.)
   - Partial support
   - Layout inheritance

4. [ ] **Output Generation**
   - HTML pages
   - Asset copying
   - Sitemap generation

#### Future Plugins
1. [ ] **Deploy.SSH Plugin**
   - SSH/SFTP deployment
   - Rsync support

---

## 🏗️ Architecture Decisions

### Technology Stack
- **.NET 10** - Latest .NET version
- **System.CommandLine 2.0.0** - Modern CLI framework (FINAL release)
- **NetVips 3.1.0** - High-performance image processing
- **Scriban 6.5.1** - Liquid-like template engine
- **Markdig 0.43.0** - CommonMark Markdown parser
- **Spectre.Console 0.49.1** - Rich console output
- **Microsoft.Extensions.Hosting 10.0.0** - Host builder
- **MSTest 4.0.2** - Modern test framework with Microsoft.Testing.Platform
- **NSubstitute 5.3.0** - Mocking framework

### Architecture Pattern
- **Vertical Slice Architecture** - Features are self-contained
- **Plugin System** - NuGet-based extensibility
- **Options Pattern** - Strongly-typed configuration

### Key Design Decisions

1. **Central Package Management**
   - All versions in `Directory.Packages.props`
   - Consistent across all projects

2. **File-Scoped Namespaces**
   - Modern C# 14 style
   - Enforced via .editorconfig

3. **Nullable Reference Types**
   - Enabled globally
   - Safer code

4. **Plugin Architecture**
   - User plugins in `%APPDATA%/Revela/plugins/`
   - NuGet-based installation
   - Reflection-based loading

---

## 🐛 Known Issues

### Build Status
- ✅ **Clean Build** - No errors or warnings
- ✅ **LoggerMessage** - High-performance logging implemented
- ✅ **MSTest v4** - Modern testing framework with Microsoft.Testing.Platform
- ✅ **All projects compile** successfully
- ✅ **Dependencies** - Automated weekly checks via GitHub Actions

### FluentAssertions Removed
FluentAssertions was removed in favor of MSTest's built-in assertions:
- **No external dependency** - MSTest v4 includes powerful assertions
- **No license concerns** - FluentAssertions 8.x required paid license for commercial use
- MSTest provides `Assert.AreEqual`, `Assert.IsTrue`, `Assert.ThrowsExactlyAsync`, etc.

---

## 📦 Project Structure

```
Revela/
├── src/
│   ├── Core/                     # ✅ Models, Abstractions, Plugin System
│   ├── Infrastructure/           # ✅ Caching, Image Processing, Scaffolding
│   ├── Features/                 # ✅ Commands (Init, Plugin, Theme, Restore)
│   ├── Cli/                      # ✅ Entry Point with Host.CreateApplicationBuilder
│   └── Plugins/
│       ├── Theme.Expose/         # ✅ Expose theme (embedded)
│       ├── Theme.Minimal/        # ✅ Minimal theme (embedded)
│       ├── Plugin.Deploy.SSH/    # 📝 SSH deployment
│       └── Plugin.Source.OneDrive/ # ✅ OneDrive shared folder source
├── tests/
│   ├── Core.Tests/               # ✅ Unit tests
│   ├── IntegrationTests/         # ✅ Integration tests
│   └── Plugin.Source.OneDrive.Tests/ # ✅ OneDrive plugin tests
└── docs/
    ├── architecture.md           # ✅ Architecture decisions
    ├── setup.md                  # ✅ Setup instructions
    └── plugin-development.md     # ✅ Plugin development guide
```

**Legend:**
- ✅ Complete
- 🚧 In Progress  
- 📝 Not Started

---

## 🚀 Quick Start (for new sessions)

### Resume Development

```bash
# 1. Pull latest
git pull

# 2. Restore packages
dotnet restore

# 3. Check current status
dotnet build

# 4. See this file for what's next
cat DEVELOPMENT.md
```

### Build & Test

```bash
# Build
dotnet build

# Run tests (.NET 10 with Microsoft.Testing.Platform)
dotnet run --project tests/Core.Tests
dotnet run --project tests/IntegrationTests
dotnet run --project tests/Plugin.Source.OneDrive.Tests

# Run CLI locally
dotnet run --project src/Cli -- --help
dotnet run --project src/Cli -- theme list
dotnet run --project src/Cli -- restore --check
```

---

## 📝 Notes for Future Sessions

### When Resuming:
1. Read this file first
2. Check "Next Steps" section
3. Review "Known Issues"
4. Continue where we left off

### Important Context:
- We're building a photography-focused static site generator
- Focus on performance (that's why NetVips)
- Plugin system is core to extensibility
- Template theme will be migrated from existing Bash version

---

## 🤝 Contribution Guidelines

### Code Style
- Follow .editorconfig rules
- Use file-scoped namespaces
- Add XML doc comments to public APIs
- Keep methods small and focused

### Commit Messages
- Use conventional commits format
- Examples:
  - `feat: add NetVips image processor`
  - `fix: resolve plugin loading issue`
  - `docs: update architecture guide`

### Before Committing
```bash
dotnet build        # Must succeed
dotnet test         # All tests pass
dotnet format       # Auto-format code
```

---

**Ready to continue? Check the "Next Steps" section above! 🚀**


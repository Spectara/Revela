# Add Web UI Plugin (Blazor Server) for GUI-based workflow

**Labels:** `enhancement`, `plugin`, `ui`, `good first epic`

**Milestone:** `v2.0`

---

## 🎯 Vision

Add a **web-based UI plugin** using Blazor Server to make Revela accessible to photographers who prefer graphical interfaces over CLI. The UI will run locally (CLI hosts web server), open browser automatically, and provide real-time progress updates during image processing.

**Target Users:** Photographers with low-to-medium technical skills who want visual feedback and easier configuration.

---

## 🏗️ Technology Decision

**Chosen:** Blazor Server (.NET 10)

**Why not alternatives?**
- ❌ **Blazor WASM:** 3-4 MB download, still needs API for file access, slower
- ❌ **WPF/MAUI:** Windows-only (WPF) or complex (MAUI), larger binaries
- ❌ **React/Vue SPA:** Requires separate API layer, less code sharing
- ✅ **Blazor Server:** ~50 KB load, direct DI access to services, SignalR built-in, cross-platform

**Key .NET 10 Features:**
- Circuit State Persistence (`[PersistentState]`) for long-running operations
- WebSocket compression (automatic)
- Improved form validation (source-generated)
- Hot Reload for faster development
- JIT/stack allocation improvements (10-50% faster)

---

## 📋 Implementation Plan

### Phase 1: Minimal MVP (4-6 days) ✅ Must-Have

**Goal:** CLI launches browser with basic working UI

- [ ] Create `src/Plugins/Plugin.UI.Web/` project
  - [ ] Implement `UIWebPlugin : IPlugin`
  - [ ] Register Blazor services (`AddRazorComponents`, `AddInteractiveServerComponents`)
  - [ ] Create `WebCommand` with `--port` and `--no-browser` options
- [ ] Kestrel hosting in command
  - [ ] `WebApplication.CreateBuilder()` with service sharing
  - [ ] `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`
  - [ ] Browser launch (`Process.Start()` with OS detection)
- [ ] Basic UI layout & styling
  - [ ] `Pages/_Host.cshtml` entry point
  - [ ] `Shared/MainLayout.razor` with navigation
  - [ ] `Pages/Index.razor` landing page
  - [ ] **Custom CSS** (clean, minimal, photography-focused design)
  - [ ] CSS Grid/Flexbox for responsive layout
  - [ ] CSS custom properties for theming
- [ ] Generate page (simple)
  - [ ] Scan button → `IContentService.ScanAsync()`
  - [ ] Progress bar with `IProgress<ContentProgress>`
  - [ ] Display result (gallery count, image count)
- [ ] Error handling
  - [ ] Try-catch with user-friendly messages
  - [ ] Logger integration

**Deliverable:** `revela web` opens browser, shows scan progress, displays results

---

### Phase 2: Core Features (1 week) 🎯 Should-Have

**Goal:** Full generation workflow with real-time feedback

- [ ] Full generate pipeline
  - [ ] Three-phase progress: Scan → Images → Pages
  - [ ] Individual progress bars per phase
  - [ ] "Generate All" button (one-click)
  - [ ] Duration tracking
- [ ] Config viewer (read-only)
  - [ ] Display `project.json` contents
  - [ ] Display `site.json` contents
  - [ ] Formatted JSON or property grid
- [ ] Gallery browser
  - [ ] List all galleries from manifest
  - [ ] Click to view images in gallery
  - [ ] Show EXIF data if available
  - [ ] Thumbnail preview (if already generated)
  - [ ] **Photography-focused card layout**

**Deliverable:** Complete read-only UI for generating sites and browsing results

---

### Phase 3: Advanced Features (1-2 weeks) 💡 Nice-to-Have

**Goal:** Configuration editing and live preview

- [ ] Config editor
  - [ ] Forms with validation for image settings
  - [ ] Site metadata editor
  - [ ] Theme settings
  - [ ] Save back to JSON files
  - [ ] Validation with error messages
- [ ] Live output preview
  - [ ] iFrame showing `output/index.html`
  - [ ] Auto-refresh after generation
  - [ ] Mobile device preview (responsive)
- [ ] Theme management
  - [ ] List available themes
  - [ ] Theme switcher dropdown
  - [ ] Theme-specific settings editor
  - [ ] Preview theme changes

**Deliverable:** Full-featured UI with editing capabilities

---

### Phase 4: Polish & Extensions (Future) 🚀 Could-Have

- [ ] Plugin manager UI (install/update/remove plugins)
- [ ] Image upload/management
- [ ] Drag & drop file support
- [ ] Keyboard shortcuts
- [ ] Dark mode toggle
- [ ] Multi-language support
- [ ] Live generation (watch mode with auto-rebuild)
- [ ] Performance metrics dashboard
- [ ] Accessibility improvements (ARIA labels, keyboard navigation)

---

## 📁 Project Structure

```
src/Plugins/Plugin.UI.Web/
├── Plugin.UI.Web.csproj
├── UIWebPlugin.cs              # IPlugin implementation
├── Commands/
│   └── WebCommand.cs           # 'revela web' command
├── Pages/
│   ├── _Host.cshtml           # Entry point
│   ├── Index.razor            # Home page
│   ├── Generate.razor         # Generation workflow
│   ├── Config.razor           # Configuration viewer/editor
│   └── Galleries.razor        # Gallery browser
├── Shared/
│   ├── MainLayout.razor       # Layout with navigation
│   └── NavMenu.razor          # Navigation menu
├── Components/                 # Reusable components
│   ├── ProgressDisplay.razor
│   └── GalleryCard.razor
└── wwwroot/
    ├── css/
    │   ├── app.css            # Main styles
    │   ├── layout.css         # Grid/flex layouts
    │   ├── components.css     # Component-specific styles
    │   └── variables.css      # CSS custom properties
    ├── js/
    │   └── interop.js         # Optional JS interop
    └── favicon.ico
```

---

## 🎨 UI/UX Considerations

**Design Principles:**
- **Simple & Clean:** Photographers aren't developers
- **Visual Feedback:** Show progress, not just text
- **Fast:** Leverage .NET 10 performance improvements
- **Responsive:** Works on desktop and tablets
- **Photography-focused:** Image-centric design, clean typography

**Technology Stack:**
- **Backend:** ASP.NET Core + Blazor Server
- **Frontend:** Razor Components (no separate SPA)
- **Styling:** Custom CSS (modern features)
  - CSS Grid for layouts
  - CSS Custom Properties for theming
  - CSS Container Queries for responsive components
  - CSS Cascade Layers for organization
- **Real-time:** SignalR (built into Blazor Server)

**CSS Architecture:**
```css
/* variables.css - Design tokens */
:root {
  --color-primary: #2563eb;
  --color-bg: #ffffff;
  --color-text: #1f2937;
  --spacing-unit: 0.5rem;
  --border-radius: 0.5rem;
}

/* layout.css - Grid/flex patterns */
.container { ... }
.grid { ... }

/* components.css - UI elements */
.button { ... }
.progress-bar { ... }
.card { ... }
```

---

## 📊 Success Metrics

- [ ] MVP completed and usable (Phase 1)
- [ ] User feedback collected from 5+ photographers
- [ ] UI adoption rate: 30%+ of users prefer UI over CLI
- [ ] No performance degradation vs. CLI
- [ ] Cross-platform verified (Windows, Linux, macOS)
- [ ] Lighthouse score: 90+ (Performance, Accessibility)

---

## 🔗 Related

- Architecture: `docs/architecture.md`
- Plugin Development: `docs/plugin-development.md`
- Copilot Context: `.github/copilot-instructions.md`

---

## 💬 Discussion

- Should we support multiple concurrent sessions (multi-user)?
  - **Answer:** No, single-user local tool (one browser session)
- Should we add authentication?
  - **Answer:** No, localhost only (no external access)
- Should we support mobile?
  - **Answer:** Responsive design yes, but primary target is desktop
- Dark mode from the start?
  - **Answer:** Phase 4 (CSS custom properties make it easy to add later)

---

## 🚀 Getting Started (for contributors)

```bash
# Create plugin project
dotnet new razorcomponents -n Plugin.UI.Web -o src/Plugins/Plugin.UI.Web

# Add project references
cd src/Plugins/Plugin.UI.Web
dotnet add reference ../../Core/Core.csproj
dotnet add reference ../../Commands/Commands.csproj

# Run in development mode
revela web --port 5000
```

**First-time contributors:** Start with Phase 1, Task 1 (create plugin structure)

**Design contributors:** Start with Phase 1, Task 3 (basic layout & custom CSS)

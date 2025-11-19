# Migration Notes: Bash Expose → Revela

> **Note:** This is a complete rewrite of the original Bash-based [Expose](https://github.com/kirkone/Expose).  
> Since there are no existing users yet, this file is mainly for reference and future planning.

## 🔗 Original Project

- **Repository:** https://github.com/kirkone/Expose
- **Author:** @kirkone
- **Language:** Bash/Shell + CLI tools (VIPS, ExifTool, Perl)

---

## Key Differences

| Aspect | Original (Bash) | Revela |
|--------|-----------------|------------|
| **Config** | `config.sh` (Bash variables) | `project.json and site.json` (JSON) |
| **Templates** | Mustache-like (regex) | Scriban (Liquid-like) |
| **Markdown** | Perl script | Markdig (C#) |
| **Images** | VIPS CLI (external process) | NetVips (in-process library) |
| **EXIF** | ExifTool CLI | NetVips built-in |
| **Performance** | Baseline | **3-5× faster** |
| **Plugins** | None | NuGet-based |
| **Cross-platform** | Unix/Linux | Windows + Linux + macOS |

---

## Why Rewrite?

1. **Performance** - NetVips library is significantly faster than calling VIPS CLI
2. **Windows Support** - Native Windows support without WSL
3. **Developer Experience** - Modern IDE support, type safety, better debugging
4. **Extensibility** - Plugin system for community contributions
5. **Maintainability** - Easier to maintain and extend

---

## What Stays Compatible

- ✅ **Content structure** - `content/` folder layout unchanged
- ✅ **Image organization** - Same folder hierarchy
- ✅ **Markdown frontmatter** - Compatible format
- ✅ **Output quality** - Generated sites look equivalent

---

## What Changes (When Migrating)

- ❌ **Configuration format** - Bash → JSON
- ❌ **Template syntax** - Mustache → Scriban
- ❌ **Build command** - `./expose (original bash) build` → `revela generate`

---

## Quick Reference

### Template Syntax Comparison

| Feature | Original | Revela |
|---------|----------|------------|
| Variable | `{{VAR}}` | `{{ var }}` |
| Loop | `{{#ITEMS}}...{{/ITEMS}}` | `{{ for item in items }}...{{ end }}` |
| Conditional | `{{#IF}}...{{/IF}}` | `{{ if condition }}...{{ end }}` |
| Include | `{{>partial}}` | `{{ include 'partial' }}` |

### Config Mapping

```bash
# Original (config.sh)
SITE_TITLE="My Site"
IMAGE_QUALITY=90

# New (project.json and site.json)
{
  "site": { "title": "My Site" },
  "build": { "images": { "quality": 90 } }
}
```

---

## Migration Strategy (Future)

When Revela v1.0 is ready and there are actual users:

1. **Create migration tool** - Auto-convert `config.sh` → `project.json and site.json`
2. **Template converter** - Mustache → Scriban translator
3. **Side-by-side comparison** - Generate with both, compare output
4. **Detailed guide** - Step-by-step migration instructions

---

## Current Status

**This file is a placeholder.** Detailed migration documentation will be created when:
- Revela reaches v1.0
- There are actual users migrating from the Bash version
- Migration pain points are identified

For now, focus is on **building the core functionality** to match and exceed the original.

---

**See also:**
- `docs/architecture.md` - Design decisions and comparisons
- `DEVELOPMENT.md` - Current development status


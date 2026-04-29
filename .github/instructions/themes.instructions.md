---
applyTo: "src/Themes/**/*"
description: "Theme conventions — Scriban templates, manifest, partials, CSS/SCSS"
---

# Theme Conventions — Revela

Themes live under `src/Themes/`. Two kinds:
- **Base themes** (`Lumina`) — standalone, render the entire site.
- **Theme extensions** (`Lumina.Calendar`, `Lumina.Statistics`) — add views/partials to a target theme. Set `TargetTheme` in metadata.

## Theme Class
```csharp
namespace Spectara.Revela.Themes.Lumina;

public sealed class LuminaTheme : EmbeddedTheme  // base class for NuGet themes
{
    public override string? Prefix => null;          // null = base, "statistics" = extension
    public override string? TargetTheme => null;     // null = standalone, "Lumina" = extends Lumina
    // PackageMetadata + ThemeManifest come from manifest.json
}
```

## Required Files
| File | Purpose |
|------|---------|
| `manifest.json` (or `theme.json` for local themes) | Metadata, version, target theme, asset list |
| `Layouts/Default.revela` | Default page layout |
| `Layouts/Gallery.revela` | Gallery page layout |
| `Partials/ContentImage.revela` | **Required for all themes** — renders `![alt](path)` from Markdown |
| `Assets/*.css`, `Assets/*.js` | Static assets — copied to `_assets/<theme>/` |

## Templates — Scriban
- File extension: `.revela` (Scriban templates).
- Front-matter not used — themes get a model object.

### Available context (every template)
| Variable | Meaning |
|----------|---------|
| `site` | Site settings from `site.json` (title, author, description, copyright, baseUrl) |
| `basepath` | Relative path to root (`""`, `"../"`, `"../../"`) |
| `image_basepath` | Path/URL to images (CDN-aware) |
| `image_formats` | Global formats: `["avif", "webp", "jpg"]` (same for all images) |
| `nav_items` | Navigation tree with active state |
| `gallery` | Current gallery: `title`, `body`, `cover_image`, `template` |
| `gallery.cover_image` | Resolved `Image` from `cover` front-matter (null if unset) |
| `page_content` | Rendered Markdown body (same as `gallery.body`) |
| `images` | Array of `Image` objects (per-image: `sizes`, `placeholder`) |

### Built-in functions
| Function | Returns |
|----------|---------|
| `find_image "path"` | Resolve any image — returns `Image` or null |
| `url_for "path"` | Generate page URL |
| `asset_url "path"` | Generate asset URL |
| `image_url "file" width "format"` | Generate image variant URL |
| `format_date date "format"` | Format date |
| `format_filesize bytes` | Human-readable size |
| `format_exif_exposure value` | "1/250s" |
| `format_exif_aperture value` | "f/2.8" |
| `markdown "text"` | Render Markdown to HTML |

## ContentImage.revela (mandatory)
Every theme must implement this partial — it's invoked for every `![alt](path)` in Markdown:
```scriban
{{- # variables: image, alt, classes, image_basepath, image_formats -}}
<picture class="{{ classes }}">
  {{ for fmt in image_formats }}
    <source type="image/{{ fmt }}" srcset="..." />
  {{ end }}
  <img src="..." alt="{{ alt }}" loading="lazy" />
</picture>
```

## Theme Extensions
- Override or add specific files — only files NOT present in extension fall through to the base theme.
- Use `Prefix` for namespacing partials.
- Declare base theme via `TargetTheme = "Lumina"` and `ExtendsPackages = ["Spectara.Revela.Themes.Lumina"]` in metadata.

## CSS / SCSS
- Source SCSS → compiled CSS → copied to `_assets/<theme>/`.
- For CSS-only iteration: also update the live copy (e.g. `samples/revela-website/output/_assets/website.css`) so Live Server picks it up without rebuild.
- Only rebuild when HTML/template changes are needed.

## Assets
- Listed in `manifest.json` under `assets`.
- Copied to `_assets/<theme-id>/` during generation.
- Use `asset_url "name.css"` in templates — never hardcode paths.

## Sitemap
Generated automatically by `generate pages` when `site.baseUrl` is set. Themes don't generate sitemaps.

## Theme Tests
Themes are usually tested via E2E generation tests (`tests/Integration`). Key checks:
- Required partials present (`ContentImage.revela`).
- Manifest valid (deserializes, version present).
- No hardcoded paths in templates (use `url_for`, `asset_url`, `image_url`).

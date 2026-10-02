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
| `Layout.revela` (or `templates.layout`) | Main page layout |
| `Body/Photo.revela` | Required only when the theme declares `page` viewer support |
| `Body/NotFound.revela` | Optional — rendered once into `404.html` at the output root (root-absolute `basepath`, `not_found = true`); skipped when absent or when `source/_static/404.html` exists |
| `Partials/ContentImage.revela` | **Required for all themes** — renders `![alt](path)` from Markdown |
| `Locales/en.json` | UI strings (fallback language); add `Locales/<lang>.json` per language |
| `Assets/*.css`, `Assets/*.js` | Static assets copied to `_assets/`; declarations determine which pages link them |

## Templates — Scriban
- File extension: `.revela` (Scriban templates).
- Front-matter not used — themes get a model object.

### Available context (every template)
| Variable | Meaning |
|----------|---------|
| `site` | Site settings from `site.json` (title, language, author, description, copyright) |
| `base_url` | Normalized `project.baseUrl`, separate from site settings |
| `og_locale` | Open Graph locale from `site.language` (`de` → `de_DE`, empty when unknown) |
| `not_found` | `true` only while rendering `404.html` |
| `basepath` | Relative path to root (`""`, `"../"`, `"../../"`); root-absolute with a configured `basePath` and always on `404.html` |
| `assets_basepath` | Path/URL to image assets (CDN-aware) |
| `image_formats` | Global formats: `["avif", "webp", "jpg"]` (same for all images) |
| `nav_items` | Navigation tree with active state |
| `gallery` | Current page (home page included): `title`, `description`, `body` (rendered Markdown), `cover_image`, `template`, `slug`, `images`. The home page without a front-matter title uses the site title |
| `gallery.cover_image` | Resolved `Image` from `cover` front-matter (null if unset) |
| `images` | Array of `Image` objects (per-image: `sizes`, `placeholder`) |
| *(data sources)* | Front matter `data = { name: source }` adds variables: `$galleries` (all galleries), `$images` (page images) or a plugin JSON file from `.cache/` (e.g. `statistics.json`). Extensions can declare defaults per template |

A missing layout or `Partials/ContentImage.revela` fails the render with a clear error — there is no built-in fallback markup. Templates and includes are parsed once per build and shared by all pages.

### Built-in functions
| Function | Returns |
|----------|---------|
| `find_image "path"` | Resolve any image (page folder → `_images/` → exact path) — returns the same image object as `images`, or null |
| `page_url(target)` | Page URL for an `Image`/`Gallery`/`NavigationItem`/slug (null for pageless nav) |
| `absolute_url(target)` | Absolute URL (host from `baseUrl`) for OG/RSS/sitemap; root-relative fallback |
| `asset_url "path"` | Theme asset URL: `basepath + "_assets/" + path` (base-path safe) |
| `variant_url(image, size, format)` | Generate image variant URL |
| `absolute_variant_url(image, size, format)` | Absolute variant URL in a full page context, or local root-relative fallback without base_url |
| `html_escape(value)` | Encode dynamic text/attribute values (only `& < > " '`; non-ASCII stays literal); Scriban does not auto-escape |
| `format_date date "format"` | Format date (culture of `site.language`) |
| `format_filesize bytes` | Human-readable size (culture of `site.language`) |
| `format_exif_exposure value` | "1/250s" |
| `format_exif_aperture value` | "f/2.8" |
| `markdown "text"` | Render Markdown to HTML |
| `t "key" args…` | Theme UI string for `site.language`; `{0}`, `{1}` filled with args. Plain text — escape it |

## UI Text — never hardcode it
- **No hardcoded user-visible text in theme templates or JS** — labels, headings, `aria-label`s, `title`s, visually hidden text. Use `{{ html_escape (t 'photo.close') }}` / `{{ html_escape (t 'photo.return_to' ctx.label) }}`.
- Strings live in `Locales/<lang>.json` (flat `"key": "text"`). `en.json` is required (fallback); ship `de.json` alongside. Keep both files' key sets identical (`ThemeLocalesTests` guards the embedded Lumina themes).
- Namespace keys by owner: base theme `photo.*`, `nav.*`; extensions use their prefix (`statistics.*`, `calendar.*`).
- `t` output is **not** trusted HTML — args are often user data. Always escape.
- JS needs text? Render it into a `data-` attribute with `t`; don't hardcode strings in `Assets/*.js`.
- Embedded themes embed `Locales\*.json` with `LogicalName` `Locales\%(Filename)%(Extension)`.
- Never translate user content (titles, Markdown) or EXIF values. Data-driven labels a plugin synthesizes (e.g. statistics months) carry a `key` the template translates.
- Lookup: `de-CH` → `de` → `en` → key itself. Layers (later wins per key): theme → extensions → `themes/<Theme>/Locales/<lang>.json` → `themes/<Theme>/Locales/<Prefix>/<lang>.json`.

## Document Structure (SEO)
- **One `<h1>` per page**, rendered by the body template (the layout has none). Skip the title `<h1>` when the Markdown body contains one; use `.visually-hidden` where the design shows no title (home, photo pages).
- Canonical / `og:url` / `og:image` only when `base_url` is set (absolute URLs). `og:image` uses a generated JPG variant ≤ 1920px (`Partials/OpenGraphImage.revela`), never the original.
- `<source type>` must be a MIME type: map the format `jpg` to `image/jpeg`. Give the fallback `<img>` a `srcset`.

## ContentImage.revela (mandatory)
Every theme must implement this partial — it's invoked for every `![alt](path)` in Markdown:
```scriban
{{- # variables: image, alt, classes, assets_basepath, image_formats -}}
<picture class="{{ classes }}">
  {{ for fmt in image_formats }}
    <source type="image/{{ fmt == 'jpg' ? 'jpeg' : fmt }}" srcset="..." />
  {{ end }}
  <img src="..." alt="{{ html_escape alt }}" loading="lazy" />
</picture>
```

## Theme Extensions
- Override or add specific files — only files NOT present in extension fall through to the base theme.
- Use `Prefix` for namespacing partials.
- Declare base theme via `TargetTheme = "Lumina"` and `ExtendsPackages = ["Spectara.Revela.Themes.Lumina"]` in metadata.

## CSS / SCSS
- Source SCSS is compiled before packaging; assets are copied to `_assets/`.
- Regenerate the assigned preview after source changes. Do not validate stale output or manually patch generated files as the final result.

## Assets
- Declare `stylesheets` and `scripts` in `manifest.json` (embedded) or `theme.json` (local). Theme entries are objects, for example `"stylesheets": [{ "path": "main.css" }, { "path": "photo.css", "scope": ["photo"] }]`. Omitted scope means global. `site.json` also accepts string shorthand, but theme manifests do not.
- Render the resolved `stylesheets`/`scripts` arrays using escaped `basepath + '_assets/' + path`, as Lumina's layout does, or `asset_url path` for individual assets (same result). Undeclared assets may be copied without being linked.
- Use `variant_url` for local/CDN image references and `absolute_variant_url` for absolute image metadata. Neither helper creates variants. Use only prepared sizes and formats.
- Escape text and attributes with `html_escape`; deliberately rendered Markdown/body HTML and documented HTML-valued fields are separate trusted boundaries.

## Sitemap
Generated automatically by `generate pages` when `project.baseUrl` is set. Themes don't generate sitemaps.

## Theme Tests
Themes are usually tested via E2E generation tests (`tests/Integration`). Key checks:
- Required partials present (`ContentImage.revela`).
- Manifest valid (deserializes, version present).
- No hardcoded paths in templates (use `page_url`, `asset_url`, `variant_url`).

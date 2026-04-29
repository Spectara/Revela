---
mode: agent
description: "Scaffold a new Revela theme or theme extension — manifest, layouts, partials, assets"
---

# New Theme Scaffold

Create a new theme or theme extension under `src/Themes/${input:themeName}/`.

## Inputs (ask the user if not supplied)

1. **Theme name** — PascalCase (e.g. `Aurora`, `Lumina.Calendar`)
2. **Type:**
   - **Base theme** — standalone, renders the entire site (`TargetTheme: null`)
   - **Extension** — adds to a target theme (`TargetTheme: "Lumina"`)
3. **Description** — one-sentence summary
4. **Tech stack** — plain CSS or SCSS? Vanilla JS or framework?

## Steps

Use the **Revela Dev** agent for implementation. Follow [`.github/instructions/themes.instructions.md`](../instructions/themes.instructions.md).

1. **Create csproj** at `src/Themes/${themeName}/Spectara.Revela.Themes.${themeName}.csproj`
   - Reference `src/Sdk/Spectara.Revela.Sdk.csproj`
2. **Theme class** — `${ThemeName}Theme.cs` extending `EmbeddedTheme`
   - Set `Prefix` (null for base, e.g. `"calendar"` for extension)
   - Set `TargetTheme` (null for base, `"Lumina"` for extension)
3. **manifest.json** — metadata, version, asset list, target theme
4. **Layouts (base theme only):**
   - `Layouts/Default.revela`
   - `Layouts/Gallery.revela`
5. **Required partials:**
   - `Partials/ContentImage.revela` — **MANDATORY for all themes** (renders Markdown `![alt](path)`)
6. **Assets:**
   - `Assets/${themeName}.css` (or `.scss` source + compiled `.css`)
   - `Assets/${themeName}.js` (if needed)
7. **Embed assets** as resources via csproj — pattern from `Lumina`
8. **Add to `EmbeddedPackageSource`** — `src/Cli.Embedded/EmbeddedPackageSource.cs`
9. **Sample/preview** — add a screenshot to `assets/screenshots/` and a sample under `samples/`
10. **Theme tests (if base theme)** — add E2E generation test in `tests/Integration` to verify pages render

## Template Context Reference

Available in every template (see [`themes.instructions.md`](../instructions/themes.instructions.md)):
- `site`, `basepath`, `image_basepath`, `image_formats`, `nav_items`
- `gallery` (with `title`, `body`, `cover_image`, `template`)
- `images` array with per-image `sizes` and `placeholder`

Built-in functions: `find_image`, `url_for`, `asset_url`, `image_url`, `format_date`, `format_filesize`, `markdown`.

## Validation Gate
- `dotnet build` — must succeed
- Run sample: `cd samples/showcase ; dotnet run --project ../../src/Cli -- generate all --theme ${themeName}`
- Visual check via Live Server (don't auto-open browser — user uses VS Code Live Server extension)
- `dotnet format --verify-no-changes` — must be clean

## Hand-off

Tell the user:
- Where files live
- How to preview (`generate all --theme ${themeName}`)
- Next steps (refine layouts, add custom partials, polish CSS)

---
name: new-theme
description: "Scaffold a new Revela theme or theme extension — manifest, layouts, partials, assets"
argument-hint: "[theme-name]"
user-invocable: true
disable-model-invocation: true
---

# New Theme Scaffold

Create a new theme or theme extension under `src/Themes/${themeName}/`, where `${themeName}` is the name collected below, not a VS Code input variable.

Use **Revela Dev**. If that agent is already active, work here; otherwise
explicitly delegate to it. Skills do not switch agents automatically. If the
agent is unavailable, report the limitation rather than silently changing personas.

## Inputs (ask the user if not supplied)

1. **Theme name** — PascalCase (e.g. `Aurora`, `Lumina.Calendar`)
2. **Type:**
   - **Base theme** — standalone, renders the entire site (`TargetTheme: null`)
   - **Extension** — adds to a target theme (`TargetTheme: "Lumina"`)
3. **Description** — one-sentence summary
4. **Tech stack** — plain CSS or SCSS? Vanilla JS or framework?

## Steps

Use the **Revela Dev** agent for implementation. Follow [`.github/instructions/themes.instructions.md`](../../instructions/themes.instructions.md).

1. **Create csproj** at `src/Themes/${themeName}/${themeName}.csproj`
   - Reference [Sdk.csproj](../../../src/Sdk/Sdk.csproj) with `Private=false` and `ExcludeAssets=runtime`.
   - Set `IsPackable=true`, `PackageType=RevelaTheme` and `IsAotCompatible=true`; include the README and any declared icon. Inherit names/output paths from the scoped build props.
2. **Theme class** — use a valid C# type name (e.g. `AuroraTheme` or `LuminaCalendarExtension`) and mirror [LuminaTheme](../../../src/Themes/Lumina/LuminaTheme.cs): pass the declaring assembly to `EmbeddedTheme`.
3. **manifest.json** — name, description, author and declared asset objects. The package version comes from the assembly, not a hardcoded manifest version.
   - Base: `templates.layout = "Layout.revela"` and supported/default photo viewer declarations.
   - Extension: `targetTheme = "Lumina"` and a unique `prefix` such as `"calendar"`; these are manifest fields, not class overrides.
4. **Templates (base theme only):**
   - `Layout.revela` and the required body templates for supported page/viewer modes.
5. **Partials:**
   - A base theme must provide `Partials/ContentImage.revela` for Markdown images. Extensions inherit the base theme's partial unless they intentionally override it.
6. **Assets:**
   - `Assets/${themeName}.css` (or `.scss` source + compiled `.css`)
   - `Assets/${themeName}.js` (if needed)
   - Localized UI strings belong in `Locales/en.json` and `Locales/de.json`, not templates or JavaScript.
7. **Embed resources** — manifest, templates, partials, assets and locales, preserving the logical names from [Lumina.csproj](../../../src/Themes/Lumina/Lumina.csproj) or [Lumina.Calendar.csproj](../../../src/Themes/Lumina.Calendar/Lumina.Calendar.csproj).
8. **Wire the host and solution** — add the theme project to `Spectara.Revela.slnx`, reference it from [Cli.Embedded.csproj](../../../src/Cli.Embedded/Cli.Embedded.csproj), and register it in [EmbeddedPackageSource](../../../src/Cli.Embedded/EmbeddedPackageSource.cs).
9. **Sample/preview** — add a screenshot to `assets/screenshots/` and a sample under `samples/`
10. **Theme tests** — add relevant generation/locale tests; a base theme needs an E2E render test. Include any required test-project reference.

## Template Context Reference

Available in every template (see [`themes.instructions.md`](../../instructions/themes.instructions.md)):
- `site`, `basepath`, `assets_basepath`, `base_url`, `image_formats`, `nav_items`
- `gallery` (with `title`, `body`, `cover_image`, `template`)
- `images` array with per-image `sizes` and `color`

Built-in functions: `find_image`, `page_url`, `absolute_url`, `asset_url`, `variant_url`, `format_date`, `format_filesize`, `markdown`.

## Validation Gate
- For the new .NET project: `dotnet build`, relevant `dotnet test --project <test-project-path>`, then `dotnet format --verify-no-changes`; serialize shared build outputs.
- Reserve a task-owned sample/preview and output directory. Set its `theme.name` to the new base theme; for an extension select its target base theme. Registered extensions matching `targetTheme` load automatically; include a preview page using the extension's prefixed template and required data. There is no `theme.extensions` setting. Do not replace another preview's configuration or output.
- From that preview directory, freshly generate with `dotnet run --no-build --project <absolute-path-to-src\Cli.Embedded> -- generate all`. There is no `generate all --theme` option.
- Check generated pages, local links, image references, declared assets/locales, and the intended viewer/extension behavior under the target configuration.
- Use an exclusively owned local browser page for desktop/mobile and keyboard/no-JS checks of the affected journeys. Restore injected/intercepted browser state or dispose the test page/context; never navigate the user's page. Report unavailable browser checks as gaps.
- For packaged themes, follow the existing [release checks](../../../docs/development.md#building-releases); managed generation does not establish Native AOT compatibility.

## Hand-off

Tell the user:
- Where files live
- The preview configuration, exact generation command and output location
- Passed, failed and unavailable checks
- Next steps (refine layouts, add custom partials, polish CSS)

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.1-beta.21] - 2026-09-30

### Added

- **Inline galleries and explicit photo viewers** - place `[[gallery]]` blocks in page content and choose `page`, `lightbox`, or `none` through theme defaults, project configuration, or page frontmatter. Dedicated `/photo/` pages participate in the sitemap; occurrence-specific navigation returns visitors to the selected gallery position. Lumina supports native dialog opening and closing without JavaScript in browsers with invoker-command support, with optional JavaScript navigation and zoom enhancements.
- **First-class `check` command group** - run individual checks or collect diagnostics with `check all`. Plugins contribute checks through `ICheck`; generation steps validate their own prerequisites rather than running a hidden preliminary check phase.
- **Derived-artifact lifecycle coordination** - generators invalidate dependent artifacts before replacing their inputs. Compression and statistics participate so stale derived files are not left serving outdated content.
- **`absolute_variant_url` template helper** - resolves an image variant against the current page, asset path and site origin. Photo-page Open Graph images now point to an image file rather than an HTML photo page.
- **Trim-safe template and configuration generators** - `[RevelaTemplateModel]` exposes template models without runtime property reflection; generated configuration-key constants keep configuration writers aligned with serialized names.
- **`IConsoleCapabilities` SDK service** — new `Spectara.Revela.Sdk.Hosting.IConsoleCapabilities` is the single source of truth for "is this an interactive terminal?". Exposes `IsInteractive` (stdin + stdout interactive — gate prompts/menus) and `CanRenderLive` (stdout can render animated output — gate `AnsiConsole.Live` progress displays). Replaces scattered `Console.IsOutputRedirected` / `Environment.UserInteractive` checks with one injectable, fakeable contract that the host and plugins share. Registered idempotently in `HostBootstrap`.
- **`<meta name="generator">` tag in Lumina layout** — the layout now emits the standards-compliant `<meta name="generator" content="Revela {version}">` in the document `<head>`. Standard mechanism used by Hugo, Jekyll, Astro and friends; visible to web telemetry (BuiltWith, Wappalyzer) without forcing visible branding on the photographer's site.
- **`revela` global template variable** — exposes build-time identity to every template via `IBuildInfo`. Currently `revela.version` (informational version string). ([#6](https://github.com/Spectara/Revela/issues/6))
- **`revela info` command tree** — new diagnostic command tree exposing version data and installed packages. `revela info` (default action) prints a Revela summary panel; `revela info plugins` lists all installed plugins with version and source; `revela info themes` lists themes with the active one marked. Plugins/themes can contribute per-package detail subcommands by registering with `ParentCommand: "info plugins"` / `"info themes"` (read-only diagnostic convention documented in `plugins.instructions.md`). ([#23](https://github.com/Spectara/Revela/issues/23))
- **`IBuildInfo` SDK service** — new `Spectara.Revela.Sdk.Hosting.IBuildInfo` interface (with `HostKind` enum: `Full` / `Standalone`, matching the published editions) exposes the immutable build-time identity of the running host. Single source of truth for `--version`, `revela info`, and any plugin that needs to branch on host kind (e.g. self-update plugins hiding themselves in the Standalone edition). Detected via `Revela.HostKind` assembly metadata attribute set in `Cli.Embedded.csproj` — sidesteps the `AssemblyName="revela"` collision that makes name-based detection impossible. ([#23](https://github.com/Spectara/Revela/issues/23))
- **`CommandDescriptor.InlineInMenu` + `InlineDefaultActionLabel`** — opt-in fields that flatten a parent command's inline appearance in the interactive menu while leaving CLI behavior unchanged. Used by `revela info` to render `Info → Revela / Plugins → / Themes →` instead of `Info → info → …`. Default `false`, zero impact on existing descriptors. ([#23](https://github.com/Spectara/Revela/issues/23))

### Removed

- **`theme.variables` mechanism (Expose legacy)** — the per-theme key/value variable dictionary in `manifest.json` (and the corresponding `{{ theme.* }}` template context) has been removed. It was an artefact of Expose's 2014-era sed-regex templating; in 2026 visual customization is solved natively by CSS Custom Properties (`Assets/main.css` already uses `light-dark()`, `clamp()`, etc.) and user-owned text belongs in `site.json`. Net surface reduction: removed `ThemeManifest.Variables`, `ThemeJsonConfig.Variables`, `IThemeVariablesProvider` (and impl), three template-context entries in `RenderService`, and the related test assertions. Theme-customization docs (EN + DE) rewritten to point users to CSS Custom Properties for design tokens and `site.copyright` for footer text. ([#6](https://github.com/Spectara/Revela/issues/6))
- **Hardcoded "powered by Revela" footer credit** — Lumina's footer no longer ships a default attribution line pointing to `revela.website`. The footer now renders only `{{ site.copyright }}` — the photographer owns what appears there. Project attribution stays discoverable via the new `<meta name="generator">` tag (invisible to end users, visible to web telemetry — exactly how Hugo, Jekyll, and Astro handle it). ([#6](https://github.com/Spectara/Revela/issues/6))

### Changed

- **Website onboarding for photographers** - revela.website now leads with plain language and a Download button, adds a six-step "Your First Site" quickstart, turns the docs hub, Get Started and Showcase indexes into clickable cards (with screenshots), lists Windows ARM64 and Linux ARM64 downloads, adds a developer orientation page, and corrects outdated FAQ answers (image sizes, local themes need `theme.json`).
- **Breaking: plugin settings move below `plugins:<key>`** - each plugin declares its own key (`plugins.serve`, `plugins.statistics`, `plugins.oneDrive`, `plugins.calendarFeeds`) instead of a package-ID section such as `Spectara.Revela.Plugins.Serve`; environment variables become `SPECTARA__REVELA__PLUGINS__<KEY>__<SETTING>` (e.g. `SPECTARA__REVELA__PLUGINS__SERVE__PORT`). The SDK generator rejects other plugin/theme sections at compile time (`REVELA001`/`REVELA002`), plugin loading fails when two packages claim the same key, and unclaimed keys produce a warning. The unused `IPageTemplate.ConfigSectionName`/`ConfigProperties`/`HasConfigCommand` and `TemplateProperty.ConfigKey` SDK members are removed.
- **Breaking: dependencies move to `dependencies.packages` / `dependencies.feeds`** - `revela.json` and `project.json` now share one shape: `"dependencies": { "feeds": { name: url-or-folder }, "packages": { packageId: exactVersion } }`. To convert, move every entry of the old root `themes` and root `plugins` maps into `dependencies.packages` (same ID and version; replace `"latest"` with a real version or leave it to be resolved) and move `packages.feeds` to `dependencies.feeds`. The root `plugins` node is no longer read as dependencies. Install commands, the setup wizard and `restore` persist the exact installed version. `restore` now installs any declared package ID (including third-party packages), resolves `theme.name` through installed theme manifests, and uses the official `Spectara.Revela.Themes.<name>` package only as a fallback. Feeds declared only in `project.json` require confirmation, or `--allow-project-feeds` on `restore`, `plugin install` and `theme install` in non-interactive runs. SDK: `IGlobalConfigManager` replaces the theme/plugin methods with `AddPackageAsync`/`RemovePackageAsync`/`GetPackagesAsync`, `IPackageInstaller.InstallAsync` and `IThemeService.InstallAsync` return the `InstalledPackage` (ID, exact version, package types), and `PackagesConfig` is merged into `DependenciesConfig`.
- **Standalone releases use Native AOT** - `Cli.Embedded` is trimmed and compiled to a native executable with bundled plugins and themes. The Full release keeps dynamic NuGet plugin loading and a managed self-contained CLI.
- **Breaking configuration and template changes** - site language moves from `project.json` to `site.json`; the calendar defaults to that language unless explicitly overridden. Image identity is exposed as `image.slug` instead of `image.url`, and `assets_basepath` replaces `image_basepath`. Templates use `page_url`, `asset_url`, `variant_url`, `absolute_url`, and `absolute_variant_url` for their respective destinations.
- **Explicit theme viewer capabilities** - base-theme manifests declare supported `photoViewers` and a `defaultPhotoViewer`. Custom page bodies opt into gallery membership with `[[gallery]]`; photo templates are required only for themes that declare page-viewer support. Stylesheets can be limited to the page types that need them.
- **JSONC configuration** - user-facing JSON configuration accepts comments and trailing commas. Missing site configuration and malformed configuration files produce actionable CLI diagnostics.
- **Image generation progress and concurrency** - a lock-free progress display replaces the worker grid, smaller variants are processed first, and libvips concurrency is capped to avoid oversubscribing AVIF encoding. Sites without images complete the image step without an error.
- **Fresh precompressed responses** - the development server negotiates available compressed variants and ignores stale ones when their source file has changed.
- **Documentation and samples** - the website documents the current URL helpers, inline galleries, viewer modes, configuration ownership and plugin artifact invalidation. Showcase demonstrates all three viewer modes.
- **`revela --version` is now human-readable and host-kind aware** — the System.CommandLine default action is replaced with a renderer that prints `revela 1.0.0 (.NET 10.0.7) — Full edition` (or `— Standalone edition`). Identical to the first line of `revela info`, so both surfaces report the same identifier. ([#23](https://github.com/Spectara/Revela/issues/23))
- **Welcome panel slimmed down** — the ASCII-art Revela logo is removed from interactive startup (it was off-brand vs. the actual aperture wordmark and added ~6 lines of friction on every menu render). The welcome panel header is now `Revela`; the redundant `Version` line and the `Modern static site generator for photographers` tagline are gone. Version data lives in `revela info` (canonical for both CLI and TUI users). The first-run panel is unchanged. ([#23](https://github.com/Spectara/Revela/issues/23))

### Fixed

- **Interrupted image runs started over** - image processing saved its progress only at the end, so a run stopped by Ctrl+C, an error or a closed session re-encoded every photo on the next run. Progress is now saved every 25 photos or 30 seconds and when a run is cancelled or fails, so the next run continues with the unfinished photos.
- **Scan data from older versions stayed in the cache** - cached image metadata (dimensions, EXIF, placeholders) was reused as long as the source file was unchanged, so projects upgraded from beta.20 kept sideways dimensions for EXIF-rotated photos and pre-sRGB placeholder colors. The scan cache key now includes a metadata version, so the first scan after an upgrade re-reads all photos.
- **Partially extracted theme configuration was ignored** - `revela theme extract --file Configuration/images.json` (and `theme files`) used a project-root `theme/configuration/` folder that generation never read. Configuration files now go to `themes/<name>/Configuration/`, and `themes/<name>/Configuration/images.json` overrides an installed theme's image sizes (scan, `check` and `config image` alike). The theme manifest is no longer offered for partial extraction; a full extraction writes it as `theme.json`.
- **Calendar pages created with `revela create page calendar` were not recognised** - the frontmatter parser stopped at the first plugin key such as `calendar.source = "…"` (its parent object did not exist), so the `template` line after it was never read. The parser now creates parent objects for dotted keys and evaluates each line independently, and `create page` writes `template` first. The `--source` help now describes the local `.ics` file instead of a URL.
- **`revela --version` and `revela info` used internal build names** - they said "embedded build" and "use the standalone CLI", the opposite of the published edition names. They now report the Standalone or Full edition and point Standalone users to the Full edition for package management; `HostKind` is renamed to `Full` / `Standalone` accordingly.
- **Extracted themes were not usable** - `revela theme extract` copied the bundled read-only `manifest.json`, but local themes are only recognised by `theme.json`, so a renamed extraction (`revela theme extract Lumina MyTheme`) reported success and then failed with "Theme 'MyTheme' is not installed". Full extractions now write an editable `theme.json` carrying the target name (CLI and theme service alike), and `revela theme files` lists it for local themes.
- **`revela check` missed themes without image sizes** - a (local) theme without a usable `Configuration/images.json` passed the check and only failed later during scan. The theme check now reports it.
- **`asset_url` template helper returned broken links** - it hardcoded `/assets/{path}`, but theme assets are written to `_assets/` and the site base path was ignored. It now returns `{basepath}_assets/{path}` like Lumina's layout, so it works at the root, in nested pages and under a subdirectory `basePath`.
- **CLI hints pointed to wrong commands and files** - several next-step hints suggested bare `revela generate` (the pipeline command is `revela generate all`), and `config image` suggested creating `theme/images.json` instead of extracting the theme and editing `themes/<name>/Configuration/images.json`. Leftover references to the removed multi-project mode (`--project`, `projects/` folder, `Projects` feature) were removed from code comments and contributor instructions.
- **Website documentation described removed or wrong behavior** - installation, user journey, CLI reference, configuration, deployment, image, page and plugin docs no longer mention the removed multi-project mode (`projects/`, `revela projects`, `--project`) and now document the project wizard in the current folder, the Standalone/Full editions (with Full-only commands marked), `check`, `info`, calendar and package commands, `language` in `site.json`, per-format quality regeneration, the `_assets/` output layout and one Standalone CI recipe that doesn't rely on GitHub's `latest` link (all releases are pre-releases). New pages cover the Calendar and Source.Calendar plugins.
- **Lumina photo viewers downloaded the original-size variant** - photo pages and the lightbox always requested the largest variant (the full original, e.g. 6341 px / ~620 KB AVIF on a phone). They now pick a viewport-sized variant via `srcset`/`sizes` and switch to the original only when the visitor zooms in.
- **Lumina lightbox had no arrow-key navigation** - ←/→ now move to the previous/next photo in the open lightbox (progressive enhancement; Escape and focus return are unchanged).
- **Lumina thumbnail focus ring left a stray line** - the lightbox trigger drew its own UA focus ring in addition to the thumbnail ring, whose outer edge was clipped or covered by neighbouring thumbnails. The ring is now a single inset outline on the thumbnail.
- **Lumina header site name was an `<h2>` before the page's `<h1>`** - the menu button and site name are now plain text, so each page starts its heading outline with its own `<h1>`.
- **Built-in features were still packed as plugins** - `Features/Generate` and `Features/Theme` kept the plugin packaging from an abandoned "everything is a plugin" design (#24, reverted in #32), so every build produced `Spectara.Revela.Features.*` plugin packages that were bundled in the Full release and offered by the setup wizard. They are now plain built-in libraries, and the wizard's unused auto-installed "core plugin" tier is removed.
- **Plugin and theme packages built on Linux lacked their dependencies** - `Directory.Build.targets` imported `src\Sdk\Build\...` while the folder is `src/Sdk/build/`, so on case-sensitive file systems the SDK packaging target silently did not run and released packages (e.g. OneDrive) contained only the plugin assembly without `.deps.json` or private dependencies. The path is fixed, a unit test compares build-file paths ordinally on every OS, and release/test scripts now reject plugin or theme packages without their `.deps.json`.
- **Wide-gamut and CMYK photos were published with wrong colors** - variants were saved without their ICC profile but also without converting the pixels, so browsers showed Display P3 / Adobe RGB photos desaturated (placeholders too) and the original-size variant of CMYK JPEGs stayed 4-channel CMYK. Variants and placeholders are now converted to sRGB using the embedded profile (perceptual intent) after resizing. HDR gain-map JPEGs are published as their SDR image. The image output version changes, so existing variants are regenerated once.
- **Edited source images kept their stale variants** - the scan stored the new file size and modification time in the manifest before image processing compared against those same values, so replaced photos were reported as cached. Image processing now records its own fingerprint per image (source size + modification time, theme resize mode, image output version) only after all variants were written, and regenerates when it differs. Changing the theme's resize mode now regenerates affected images as well. Existing projects regenerate all images once after upgrading.
- **Theme resize modes `width` and `height` behaved like `longest`** - the unconstrained side was passed to libvips as `int.MaxValue`, which it rejects, silently falling back to a square bounding box. The side is now bounded by libvips' maximum coordinate.
- **OneDrive honours include/exclude patterns** - `IncludePatterns` and `ExcludePatterns` now filter remote files before download, using the same case-insensitive file-name wildcards as cleanup (exclusion wins). Without include patterns all non-excluded files are still downloaded.
- **Extension theme escaping** - Lumina.Statistics and Lumina.Calendar now HTML-escape EXIF-derived chart labels and calendar labels/values with `html_escape`, like base Lumina, so crafted EXIF or calendar data can no longer inject markup into generated pages.
- **Calendar page template** - `revela create page calendar` now writes `template = "calendar/page"`, the template Lumina.Calendar provides, instead of the non-existent `calendar/overview`.
- **`latest` package versions** - themes and plugins recorded with version `latest` (setup wizard, `plugin install` without an index entry) now restore and install as "no explicit version" instead of failing with a version-parse exception. Other unparsable versions fail with a clear logged error.
- **Documentation claims corrected** - install instructions no longer assume Revela packages on NuGet.org (the tool, SDK, plugins and themes come from GitHub Release `.nupkg` files or the Full build's bundled `packages/` feed); `revela serve` is described as a static preview server without browser launch or live reload; Calendar is documented as an iCal availability calendar and Source.Calendar as an iCal feed downloader; the Compress README no longer claims compression runs in `generate all`; wrong plugin short names (`OneDrive`, `Lumina.Statistics`) and NuGet.org badges were removed.
- **Package registration integrity** - package installs and removals use the shared project configuration writer, preserving mixed-case keys and unrelated settings. Same-host updates serialize the complete read-through-reload operation, preventing parallel restore from losing registrations. Failed registration reports failure instead of installation success; extracted files remain available for recovery, and cancellation propagates through the install command.
- **Review data-integrity fixes** - OneDrive cleanup excludes linked descendants and rechecks deletion boundaries; interrupted downloads preserve existing files and timestamps. Configuration updates reject invalid replacements before persistence and retain unrelated global settings, including nested and mixed-case sections. Unix staging starts private and preserves existing source/project file modes.
- **Compression ownership** - independent gzip/Brotli downloads are no longer deleted or overwritten. A private output-local ownership record tracks generated sidecars, including orphaned ones; changed or unowned targets cause explicit failures.
- **Dependency and SDK package contracts** - restore reads the writer's root package maps and active `theme.name`, matches exact package identities, and recognizes theme extensions. Embedded theme identities come from their assembly instead of display names. The SDK ships its source generator and is tested through an isolated NuGet consumer, including `pack --no-build`.
- **Unique viewer IDs and server cleanup** - injective context/anchor encoding distinguishes paths such as `a/b` and `a-b` and root versus `home`. Server disposal waits for active handlers; tests bind the actual listener before claiming a port. `config locations` reports local project paths and release smoke tests prove valid images survive obsolete-variant cleanup.
- **Theme file listing** - `theme files` loads packaged themes before resolving their files and returns a failure exit code for a missing theme. The release smoke test now requires bundled templates and assets before extraction and verifies the unknown-theme failure path.
- **Release dry-runs do not deploy the website** - automatic website deployment requires a successful tag-triggered Release workflow. A manually dispatched Release workflow builds, attests and signs artifacts without publishing a GitHub Release; intentionally dispatching Deploy Website still deploys content updates.
- **Image orientation and slug validation** - EXIF orientation is applied before metadata is stripped. Empty or colliding normalized slugs are rejected during scanning before output is written. Page progress includes dedicated photo pages without double-counting the index.
- **Calendar availability validation** - incomplete calendars, malformed component structure and unsupported booking forms are rejected before replacing availability data. Failed calendar generation reports failure while preserving the current page's existing JSON; a valid empty calendar remains supported.
- **Invalid local themes remain visible as errors** - diagnostics preserve the manifest path and underlying validation reason instead of silently falling back. Restore reports invalid installed themes and stops before installing dependencies.
- **Configuration and package robustness** - project configuration is replaced atomically, the base URL is written using its correct configuration key, and unavailable NuGet metadata resources are handled without a null-reference failure.
- **Lightbox focus restoration** - closing a dialog after switching to another image returns keyboard focus to the original gallery opener, including Firefox's deferred close-event behavior.
- **Release packaging tests** - build and pack symbol settings match CI; tool installation uses an isolated directory, checks the exact version and cleans up without modifying global tools.
- **Plural-named plugins and themes were missed by package-search type inference** — when a NuGet feed's search API does not return `PackageTypes`, Revela infers the type from the package ID. The heuristic only matched the singular segments `.Plugin.` / `.Theme.`, so the official plural-named packages (`Spectara.Revela.Plugins.*`, `Spectara.Revela.Themes.*`) were not classified and could be filtered out of `revela plugin search` / `revela theme search` results. `PackageSearchService.InferPackageTypes` now recognizes both the plural (`.Plugins.` / `.Themes.`) and singular (`.Plugin.` / `.Theme.`) forms. Authoritative typing still comes from the `<PackageType>` marker in the `.nuspec` at install time; this only affects pre-install search classification.
- **`generate images` crashed on a non-interactive console** — the images step rendered progress via Spectre's low-level `AnsiConsole.Live()` primitive, which unconditionally hides the terminal cursor. With redirected output or no TTY (CI, Docker, `revela generate all > log.txt`, piped output) this threw `IOException: The handle is invalid` and aborted the whole pipeline. Unlike `Status()`/`Progress()`, `Live()` has no built-in non-interactive fallback, so `ImagesCommand` now gates it on the shared `IConsoleCapabilities.CanRenderLive` check and runs without the live display when the console can't render it.
- **Inlined-parent menu entries dispatched to the wrong command** — clicking an inlined subcommand in the interactive main menu (e.g. `Plugins` under `Info`) built the args path from the leaf name only (`["plugins"]`) instead of the absolute path (`["info","plugins"]`), producing an `Unrecognized command or argument` error from System.CommandLine. The top-level menu now routes `Navigate`/`Execute` selections through the same dispatcher as nested menus, honoring `MenuChoice.CommandPathOverride`. ([#23](https://github.com/Spectara/Revela/issues/23))

### Security

- **Destructive-path guards** - `clean output` and `clean images` refuse to run, and delete nothing, when the configured output is a filesystem root, the home directory, the project or source directory, or contains one of them. `theme extract --force` only replaces a folder strictly inside `themes/`, and `plugin uninstall` only deletes a valid package ID's folder strictly inside the plugins directory. Symbolic links and junctions below those roots are refused.
- **Official package index and setup wizard trust** - `packages refresh` and the setup wizard now accept only `Spectara.Revela.*` package IDs (case-insensitive); remote search results must also be `verified` by nuget.org, while bundled and configured local folders are trusted by prefix. Look-alike IDs from the nuget.org full-text search (e.g. `Evil.Spectara.Revela.*`) can no longer enter the index, the "Full" install or the auto-installed core plugins. Package titles, versions and descriptions are escaped in console output.
- **Package install hardening** - package IDs are validated with NuGet's `PackageIdValidator` before they are used in install and extraction paths (including IDs read from `.nuspec`), preventing extraction outside the plugin directory. Plain `http://` package sources and package URLs are rejected (loopback `http://` stays allowed). Without an explicit version, prerelease packages are only selected when the running host is a prerelease build.
- **Pinned CI actions** - GitHub Actions workflows pin every third-party action to a full commit SHA (tag kept as a comment, kept current by Dependabot) and no checkout persists its credentials. `sigstore/cosign-installer` now references the existing `v4.1.2` release; the previous `v4` reference does not exist upstream.
- **Calendar-source transport and output hardening** - HTTP(S) destinations are validated on each redirect; HTTPS downgrades are rejected and downloads have a deadline covering the response body. Downloads replace their destination only after successful completion. Output paths are preflighted against the source directory, existing reparse points and overlapping feed destinations. URL credentials are omitted from logs and user-facing errors; default HTTP-client URL logging, automatic redirects and cookies are disabled. DNS rebinding protection, a download byte quota and transactional updates across multiple feeds are not provided.
- **HTML metadata escaping** - Lumina escapes dynamic text and attributes, including titles, navigation and image metadata. Authored Markdown HTML and `site.copyright` remain trusted HTML, not sanitized input.
- **Security guidance** - added a private vulnerability-reporting route and clarified the trust model, configuration-file protection and EXIF/GPS removal from published image variants.

### Dependencies

- Updated the .NET SDK baseline to `10.0.400` and Microsoft.Extensions packages to `10.0.11` (Http.Resilience `10.9.0`).
- Updated Scriban to `7.4.0`, including output-budget reset and rendering-performance fixes. Revela continues disabling the output limit for trusted templates; a regression test covers complete large-page output followed by a small page.
- Updated MSTest and MSTest.Analyzers to `4.4.0`, with Microsoft.Testing.Platform execution, TRX reporting and Microsoft Code Coverage verified locally.
- Updated other centrally managed dependencies, including System.CommandLine `2.0.11`, Spectre.Console `0.57.2`, Markdig `1.3.2`, NetVips `3.2.0`, NetVips.Native `8.18.6`, NuGet packages `7.9.0`, and NSubstitute `6.2.0`.

## [0.0.1-beta.20] - 2026-05-06

### Fixed

- **Silent HTML truncation on large galleries** — Scriban's `TemplateContext.LimitToString` defaults to 1 MiB and silently truncates rendered output with `...` when exceeded. Galleries with ~150+ images and full responsive image markup (3 formats × ~10 srcset sizes) easily exceed this limit, producing pages that ended mid-document with no error. The template engine now sets `LimitToString = 0` per Scriban's official safe-runtime guidance for trusted templates, and wires `RenderRuntimeException` to the logger so future Scriban runtime errors surface instead of being swallowed.

### Changed

- **`RenderService` warns on truncated HTML output** — a sanity check now logs a warning when a rendered page does not end with `</html>`, catching any future regression regardless of source.


## [0.0.1-beta.19] - 2026-05-04

### Removed

- **Multi-project / standalone-mode feature** — the `projects/`-folder convention, `--project` argument, `revela projects list/create/delete` commands, and interactive folder-picker on startup are gone. Revela now follows the convention of every other static site generator: **the current working directory is the project**. Multiple sites? Use multiple folders. ~1,640 lines of production code removed; no tests affected. ([#67](https://github.com/Spectara/Revela/issues/67))

### Changed

- **`Cli.Embedded` no longer shows the setup wizard menu entry** — the wizard is provided by the Full release only (it manages NuGet packages, which Embedded doesn't have). The menu now hides the entry automatically when no `ISetupWizard` is registered.
- **JSON serialization migrated to source-generated contexts** — `Core` and `Features` now use `JsonSerializerContext`s for `revela.json`, `.cache/manifest.json`, plugin `.meta.json` files, and theme manifests. Eliminates the IL2026 trim warnings and prepares the codebase for AOT/trimmed publishing. ([#45](https://github.com/Spectara/Revela/issues/45))
- **`CreatePageCommand` no longer uses reflection** — replaced `MakeGenericType` / `Activator.CreateInstance` / `MakeGenericMethod` with an explicit `string`/`int`/`bool` type-switch. Removes the codebase's only IL2060 trim warning and prevents a runtime crash under `PublishTrimmed`. ([#46](https://github.com/Spectara/Revela/issues/46))
- **Documentation simplified** — `docs/getting-started/getting-started-{en,de}.md` and `docs/getting-started/cli-reference.md` updated for the new "site = CWD" model. The "Working with multiple projects" section was replaced by a short note on using multiple folders.

### Fixed

- **`sitemap.xml` no longer contains fake `https://example.com` URLs** — `RenderProjectSettings.BaseUrl` is now `string?`, so the existing `is not null` gate at the sitemap site actually works. Without `baseUrl` configured, the sitemap is now correctly skipped (as the existing `LogSitemapSkipped` info message already promised).
- **`theme extract` and `theme info` work in standalone builds** — these read-only commands no longer skip package loading, so bundled themes (e.g. Lumina) are visible to them. ([#33](https://github.com/Spectara/Revela/issues/33))

## [0.0.1-beta.18] - 2026-05-02

### Security

- **Path traversal hardening in dev server** — `revela serve` now uses `Path.GetRelativePath` to validate request paths, rejecting sibling directories that share a name prefix with the configured root (previously a naive `StartsWith` check could be bypassed). 7 new unit tests cover the vector. ([#53](https://github.com/Spectara/Revela/issues/53))
- **SSRF guardrails for source plugins** — New `Spectara.Revela.Sdk.Validation.UrlSafety` helper validates outbound URLs before HTTP requests. Rejects loopback, RFC 1918 private, RFC 6598 CGN, link-local (incl. cloud metadata IP `169.254.169.254`), IPv6 link-local/site-local/ULA, multicast, and IPv4-mapped loopback. Now used by Source.Calendar (iCal feeds) and Source.OneDrive (share URLs). 40 new unit tests. Plugin authors building source plugins should adopt this — see [`docs/httpclient-pattern.md`](docs/httpclient-pattern.md#url-validation-ssrf-prevention). ([#58](https://github.com/Spectara/Revela/issues/58))
- **Sensitive URLs no longer logged at Information level** — OneDrive share URLs (which contain account-scoped resource IDs) and iCal feed URLs (which often carry auth tokens in query strings) are now logged at Debug. iCal logs the host at Information for diagnostics. ([#57](https://github.com/Spectara/Revela/issues/57))
- **Markup escaping for user input** — All `Spectre.Console` `MarkupLine` calls now wrap user-controlled strings (package IDs, theme names, project paths, file paths, exception messages) in `Markup.Escape` to prevent display corruption. ([#61](https://github.com/Spectara/Revela/issues/61))

### Documentation

- **New `docs/security-model.md`** — comprehensive threat model: trust assumptions, what Revela protects against, what it explicitly does NOT (raw HTML in markdown, EXIF GPS, third-party theme review), plugin trust model, and upgrade paths.
- **`MarkdownService` trust model documented** — Inline XML doc explains why `.DisableHtml()` is deliberately not called (Markdig itself states it's not a sanitizer; same trust model as Jekyll, Eleventy, MkDocs, Astro, Zola). ([#60](https://github.com/Spectara/Revela/issues/60))
- **Plugin trust model documented** — Same model as `dotnet tool install`: trust at install time, no hash-pinning between install and load. Explains why author-signing isn't pursued (NuGet certificate lock-in via NU3038/NU3018). Documents existing free verification paths (`dotnet nuget verify` for nuget.org, `gh attestation verify` for GitHub releases). ([#55](https://github.com/Spectara/Revela/issues/55))
- **`docs/plugin-development.md`, `docs/httpclient-pattern.md`, `.github/copilot-instructions.md`** updated with `TryAddTransient` (was `AddTransient`) and `UrlSafety` guidance for plugin authors.

### Changed

- **Genuinely-async file IO** — `NavigationBuilder.BuildAsync` is now actually async (was wrapping sync code in `Task.FromResult`); `RenderService.LoadConfigurationAsync`/`LoadSiteJsonAsync`, `ThemeService.UpdateThemeNameAsync`, and `ThemeExtractCommand.UpdateThemeNameAsync`/`PromptForThemeSelectionAsync` use async file IO with `CancellationToken` plumbing. ([#56](https://github.com/Spectara/Revela/issues/56), [#59](https://github.com/Spectara/Revela/issues/59))
- **Plugin command registration uses `TryAddTransient`** — Serve, Source.OneDrive, and Source.Calendar plugins now register commands idempotently. ([#54](https://github.com/Spectara/Revela/issues/54))
- **OneDrive URL validation tightened** — Host equality check (`uri.Host == "1drv.ms"`) replaces substring match (`url.Contains("1drv.ms")`), which previously accepted `https://attacker.com/?fake=1drv.ms`.

### Fixed

- **OneDrive wizard crash on first project setup** — running the new-project wizard with the OneDrive source plugin installed but not yet configured threw an unhandled exception instead of prompting for the share URL. The wizard now correctly detects the empty configuration and walks you through setting it up.
- **Plugin test projects build again** — `InternalsVisibleTo` mismatches in Compress and Serve `AssemblyInfo.cs` files were silently breaking ~163 tests; stale `Microsoft.Extensions.Telemetry.Abstractions` reference (NU1010) in Compress and Statistics test csprojs; 16 `StatisticsAggregator` constructor calls updated for new `TimeProvider` parameter; obsolete `CleanCompressCommand.Order` test removed.

### Removed

- **Dead `TestDataHelper` infrastructure** — referenced a non-existent `test-data/` directory and was unused. Real test infrastructure lives in `tests/Shared/Fixtures/`.

### Build

- Bumped MSTest from 4.2.1 to 4.2.2 (patch).

## [0.0.1-beta.17] - 2026-03-15

### Added
- **Cover Image**: New `cover` frontmatter field for gallery/page cover images
  - Path resolution identical to Markdown content images (gallery-local → `_images/` → exact match)
  - Available in templates as `{{ gallery.cover_image }}` (full Image object with url, sizes, width, height)
  - Example: `cover = "panorama/panorama.jpg"`
- **`find_image` Template Function**: Resolve any image from the project in templates
  - Same 3-step lookup as Markdown content images
  - Returns full Image object: `{{ find_image "logo.jpg" }}` → url, sizes, width, height
  - Returns `null` if image not found
- **Content Image Template**: Markdown body images (`![alt](path)`) now rendered via
  Scriban template `Partials/ContentImage.revela` instead of hardcoded C#
  - Every theme must include this template (no fallback)
  - Themes can customize for lightbox, styling, etc.
  - Template variables: `image`, `alt`, `classes`, `image_basepath`, `image_formats`
- **Sitemap Generation**: Automatic `sitemap.xml` during `generate pages`
  - Requires `baseUrl` in project.json (`"project": { "baseUrl": "https://..." }`)
  - Includes all pages with `<lastmod>` (gallery date or build date)
  - Skipped with info log when `baseUrl` is not configured

### Changed
- **Custom Templates**: Now receive `{{ images }}` array (previously empty for custom templates)
- **Content Image Rendering**: Removed ~80 lines of C# HTML generation code, replaced by
  theme-owned Scriban template

### Fixed
- **Body Duplication**: Fixed duplicate body text on pages with custom templates
  (e.g., Calendar plugin). Removed redundant pre-rendering step that caused
  `{{ gallery.body }}` and `{{ page_content }}` to both contain the same content
- **Website**: Showcase screenshot too small due to overly broad CSS selector
  targeting all images in hero section

## [0.0.1-beta.16] - 2026-03-13

### Added
- **CLI: `--project/-p` path support**: Use `-p path/to/project` to specify a project directory
  without `cd`. Works in both Tool Mode (paths) and Standalone Mode (names or paths)
- **Statistics: Photo Activity Heatmap**: Calendar-style visualization showing when photos
  were taken, with color-coded intensity per day
- **SDK: `AddPluginConfig<T>()`**: Simplified one-line plugin configuration registration
  with validation and hot-reload support
- **Navigation: Container nodes**: New `container = true` frontmatter property for
  navigation-only nodes that group child pages without their own content
- **Website**: FAQ page, Docs overview page, Showcase page, glassmorphism redesign with
  neon logo, demo video on homepage

### Changed
- **Statistics Plugin**: Complete overhaul with pure-CSS dashboard (no JavaScript),
  restructured charts and layout
- **Plugin System**: Simplified `IPlugin` interface using default interface methods —
  `ConfigureConfiguration` and `GetCommands` are now optional with sensible defaults
- **Plugin Architecture**: Extracted `PluginManager` into focused service classes,
  extracted `IGlobalConfigManager` interface
- **Theme System**: Simplified with shared abstractions, modernized Lumina CSS with
  nesting and custom properties
- **Namespaces**: Unified conventions via `Directory.Build.props` — automatic
  `Spectara.Revela.*` prefix for all projects
- **Project Layout**: Renamed plugin/theme folders and namespaces for consistency
- **Code Quality**: Comprehensive code reviews across CLI, Serve, OneDrive, Lumina,
  and Statistics — restricted type visibility, extracted helpers, reduced duplication
- **Single-file bundle**: Enabled Brotli compression for smaller executable
- **Code Coverage**: Migrated from coverlet.collector to Microsoft Code Coverage

### Fixed
- **Linux Compatibility**: Forward slashes in NuGet `PackagePath`, case-sensitive
  `Build/` → `build/` rename, lowercase slugs in test assertions
- **Statistics**: Bar charts not rendering data, scoped CSS selectors to main content
- **Theme**: Nav scrollbar hidden behind sticky header
- **Build**: Plugins/themes now built in Release mode for pack step
- **Website**: Duplicate FAQ/Docs navigation entries, broken links, container labels
  clickable in sidebar

### Dependencies
- Microsoft.Extensions.* 10.0.3 → 10.0.5
- Microsoft.Extensions.Http.Resilience 10.3.0 → 10.4.0
- System.CommandLine 2.0.3 → 2.0.5
- Scriban 6.5.2 → 6.5.7
- Markdig 0.45.0 → 1.1.1

### Testing
- Full E2E pipeline integration tests (scan → render → images)
- Nested galleries and incremental build tests
- NetVips-based `TestImageGenerator` for real JPEG creation in tests
- `ConfigService` and `ManifestService` integration tests
- Improved SDK test coverage toward 100%
- Restructured test projects with shared infrastructure

## [0.0.1-beta.15] - 2026-02-12

### Added
- **Content Images**: Markdig extension for responsive images in Markdown body content
  - Transform `![alt](path)` into `<picture>` elements with AVIF/WebP/JPG srcset
  - 3-step image resolution: gallery-local → `_images/` shared → exact match
  - LQIP placeholder support via `--lqip` CSS custom property
  - GenericAttributes support: `{.class}` syntax passes CSS classes to `<picture>`
- **Shared Images**: `_images/` folder included as hidden node in manifest tree
  - Images available for content references across all galleries
  - Subdirectories supported (e.g., `_images/screenshots/`)
- **Browser Mockup CSS**: Simulated browser chrome for screenshots on website
  - Titlebar with traffic light dots (red/yellow/green)
  - Uses CSS variables for dark/light mode compatibility
- **Showcase Section**: "See it in action" section on revela.website homepage

### Changed
- **Website CSS**: Convert website.css to native CSS nesting
  - Nested `@media`, `&::before`, `&:hover`, `&.active` selectors
  - Reduced repetition, improved readability
- **Lumina Theme**: Add `.content-image` and `.breakout` CSS classes

### Fixed
- **Documentation**: Correct false "any name" claim for `_images/` folder
- **Website**: Remove unnecessary local main.css theme override

## [0.0.1-beta.14] - 2026-02-12

### Added
- **LQIP Placeholders**: CSS-only low-quality image placeholders
  - 20-bit integer encoding (~7 bytes per image)
  - CSS-only decoding using `calc()`, `mod()`, `pow()` - no JavaScript needed
  - 3×2 brightness grid with grayscale cells
  - Dual-layer blend modes (hard-light + overlay) for smooth appearance
  - Average color calculation in Oklab color space
  - Configurable via `generate.images.placeholder` in project.json
- **BenchmarkDotNet Benchmarks**: Image processing performance benchmarks
  - ResizeStrategyBenchmark: StarFromOriginal vs ThumbnailPerSize vs ThumbnailThenResize
  - FormatSequentialBenchmark: all-formats-per-image vs format-sequential processing
  - New `benchmarks/` folder with dedicated configuration
- **Documentation**: Complete plugin documentation section on website
  - Plugin overview with install/manage/uninstall commands
  - Statistics plugin: EXIF analysis, configuration, CLI reference
  - Serve plugin: port configuration, verbose mode, workflow
  - Source.OneDrive plugin: setup, sync commands, environment variables
- **Documentation**: Comprehensive User Journey guide (17 phases)
- **Tests**: ScanCachingTests for cache hit/miss conditions
- **Tests**: ManifestServiceTests for config hash computation

### Changed
- **Image Processing**: Switch from `Resize()` to `ThumbnailImage()` for correct alpha channel handling
  - Based on libvips maintainer recommendation (libvips/libvips#4588)
  - Properly handles alpha premultiplication for transparent PNGs
- **Image Processing**: "Star from Original" strategy - load once, resize all sizes
  - 13% faster than previous shrink-on-load per size approach
  - Remove unnecessary `CopyMemory()` call
- **AVIF Threading**: Optimal (CPU/2) × (CPU/2) threading strategy
  - Workers = ProcessorCount/2, NetVips.Concurrency = ProcessorCount/2
  - ~50% fewer threads with similar performance, prevents system freeze
- **Change Detection**: Replace hash-based detection with LastModified + FileSize
  - Much faster: no SHA256 computation for unchanged files
  - Scan metadata caching: skip NetVips reads for unchanged source files
  - ScanConfigHash invalidates cache when placeholder/minDimensions change
  - FormatQualities tracking: automatic regeneration when quality changes
- **Progress Display**: Dynamic legend with format-specific colors
  - JPG=green, WebP=blue, AVIF=magenta, PNG=cyan
  - Only shows configured formats
- **Image Sizes**: Updated defaults optimized for High-DPI displays
  - `[160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560]`
- **Manifest Schema**: Simplified change tracking
  - ImageContent: Replace Hash+ProcessedAt with LastModified
  - ManifestMeta: Add ScanConfigHash and FormatQualities
  - GalleryContent: Remove Hash (moved to MarkdownContent only)
- **Dependencies**: Major package updates
  - .NET SDK 10.0.102 → 10.0.103
  - Microsoft.Extensions.* 10.0.2 → 10.0.3
  - Microsoft.Extensions.Http.Resilience 10.2.0 → 10.3.0
  - System.CommandLine 2.0.2 → 2.0.3
  - Markdig 0.44.0 → 0.45.0
  - NuGet.* 7.0.1 → 7.3.0
  - MSTest 4.0.2 → 4.1.0
  - Remove unused Hosting.Abstractions, Configuration.CommandLine packages

### Fixed
- **CA1873**: Add `IsEnabled()` guards for 15 logging performance warnings
- **CA1508**: Fix false positives in ScanCachingTests

## [0.0.1-beta.13] - 2026-01-12

### Added
- **Clean Images Command**: Intelligently remove unused image files
  - Detects orphaned folders (deleted source images)
  - Detects unused sizes (removed from theme config)
  - Detects unused formats (disabled in project config)
  - `--dry-run` option to preview without deleting
  - Safety check: requires valid manifest to prevent accidental deletion
- **Incremental Image Generation**: Only generate missing variants
  - Adding a new format only generates that format (existing files kept)
  - Adding a new size only generates that size
  - Progress display shows green ■ (new) vs gray ■ (skipped)
  - Processing order: largest sizes first for smoother progress display
- **Compress**: Static compression plugin (Gzip/Brotli)
- **Setup Wizard**: Full/Custom installation modes
- **Documentation**: New "Image Processing" page on website
- **Documentation**: Number prefix sorting for galleries (e.g., `01 Weddings`, `02 Portraits`)

### Fixed
- **NavigationBuilder**: URL slugs now correctly strip number prefixes (was including `01-` in URLs)
- **Documentation**: Fixed incorrect JSON format examples (`formats` wrapper removed)
- **Documentation**: Plugin naming consistency (`Source.OneDrive` instead of `OneDrive`)
- **CI/CD**: Added missing Compress to all workflows and scripts

## [0.0.1-beta.12] - 2026-01-12

### Added
- **Static Files**: New `_static/` folder support for custom assets (CNAME, .nojekyll, favicon)
- **Favicon Partial**: Configurable favicon via `site.favicon` and theme partial
- **HeaderNavigation Partial**: Extracted from Layout for easier customization
- **NavigationItem.Current**: Property for active page detection in navigation
- **robots.txt**: Added to revela-website sample
- **CI/CD**: Automatic website deployment after release workflow

### Changed
- **Website Styling**: Complete color palette overhaul
  - Purple accent (263°) for links, buttons, active states
  - Pink accent-light (309°) for hover states
  - Separate light/dark mode gradients (logo-inspired)
  - Dark code blocks with Prism.js syntax highlighting
  - Consistent navigation hover colors across all sections
- **Documentation**: Updated CLI reference and plugin command names

### Fixed
- **Website**: Table styling in docs-content, duplicate H1 removal
- **Docs Navigation**: Proper active state and current page detection

## [0.0.1-beta.10] - 2026-01-07

### Fixed
- **Serve Plugin**: Prevent ObjectDisposedException when port is in use
  - Stop() now checks isRunning flag before calling HttpListener.Stop()
  - Dispose() catches ObjectDisposedException during Close()
- **Template Rendering**: Fix double `<section class="page-content">` wrapper on text pages
  - Pre-rendering now only applies to custom templates with data sources
  - Standard body templates (page, gallery) no longer get double-wrapped
- **Create Page**: Allow empty path input for source root pages
  - Pressing Enter without input creates page directly in source/

### Changed
- **Package Index**: packages.json now stored directly in ConfigDirectory
  - No separate cache/ folder in standalone root anymore
  - More consistent structure alongside revela.json
- **Path Constants**: Replace all hardcoded paths with ProjectPaths.* constants
  - source, output, .cache, themes centralized in Sdk/ProjectPaths.cs
  - Better maintainability and consistency
- **Config Menu**: Move sorting command to Project group
  - Now appears alongside project, theme, image, site commands

## [0.0.1-beta.8] - 2026-01-04

### Added
- **Create Page Command**: Extended page templates with interactive mode
  - `create page gallery`: New options --sort, --hidden, --slug
  - `create page text`: New template for text-only pages (About, Contact)
  - Interactive wizard when path argument is missing
  - DefaultBody property for starter content
- **Theme Customization**: Local theme variables via theme/theme.json
- **Documentation**: Theme customization guide (DE/EN), pages documentation (DE/EN)

### Changed
- **Standalone Mode**: Setup wizard now appears BEFORE project selection
- **Getting-Started Guides**: Focus on interactive menu mode
- **UX**: Base URL prompt with helpful description
- **Template System**: Unified theme/extension structure with implicit template prefixes
- **Generate Pipeline**: Unified IGenerateStep interface
- **Config System**: Fixed IConfiguration array-merge problem

### Fixed
- Statistics rendering with implicit template prefix system
- Path handling: Pages are created relative to source/
- Option constructor for properties without short alias

## [0.0.1-beta.7] - 2025-12-29

### Changed
- Documentation: Comprehensive revision and asset restructuring
- CI/CD: Fix duplicate SHA256SUMS uploads in release workflow

## [0.0.1-beta.6] - 2025-12-29

### Added
- **Setup Wizard**: Interactive assistant for new projects
- **Packages Command**: Package index with NuGet packageTypes support
- **Serve**: Local HTTP server for site preview
- **Spectara.Revela.Sdk**: Separate SDK package for plugin development
- Unified command registration with CommandDescriptor
- Context-aware interactive menu
- DevContainer debug configurations (Local/Container)

### Changed
- **CLI Restructuring**: New commands `create`, `init`, `config`
- **Config System**: Unified global config with `revela.json`
- **Theme-based Configuration**: ThemeConfig system
- **IOptions Pattern**: Hierarchical config merging
- Simplified plugin directory structure (removed CWD and global options)
- Consolidated plugin configuration in project.json
- Interactive mode UX improvements
- Revised release pipeline and standardized NuGet metadata

### Fixed
- Cross-platform path handling in NuGetSourceManagerTests
- Added missing Packages Command files to Git

## [0.0.1-beta.5] - 2025-12-21

### Added
- **NuGet Plugin System**: Full NuGet package support (instead of ZIP)
- **plugin.meta.json**: Automatic creation with package metadata
- **Restore Command**: NuGet-based plugin restore with parallelization
- **NuGet Source Management**: add/remove/list commands for NuGet sources
- **Multi-Source Discovery**: --source parameter for plugin installation
- **GitHub Workflows**: 3-stage NuGet release process
- **.NET Global Tool**: CLI as installable dotnet tool
- Dedicated README for each plugin and theme
- Provenance attestations and keyless signatures

### Changed
- Switched plugin installation from ZIP to NuGet
- Updated CI workflow for MSTest v4 Testing Platform
- Added HttpClient parameter to PluginManager (Typed HttpClient Pattern)
- Single-file publish compatibility for plugin system
- Comprehensive documentation updates

### Fixed
- Embedded resource loading for theme assets
- Skip plugin loading for plugin management commands
- Added --yes flag for plugin uninstall

## [0.0.1-beta.4] - 2025-12-15

### Added
- **Template/Asset Resolver System**: Theme files command
- **FileHashService**: Service for file hashing
- CancellationToken propagation through all CLI commands
- Interactive mode with menu-driven interface

### Changed
- Improved image caching (moved hash to processing phase)
- Optimized logging defaults

### Fixed
- Emojis durch ASCII ersetzt für Terminal-Kompatibilität
- Plugin Name Prefix korrigiert und Uninstall Cleanup verbessert
- Using Directives aufgeräumt und Data Source Loading verbessert
- Cancellation + Exit Codes für Generate Pipeline

## [0.0.1-beta.3] - 2025-12-14

### Added
- Test Infrastructure: SharedTestDataHelper und MockHttpMessageHandler
- Statistics.Tests: Unit Tests für StatisticsAggregator
- TestData Factory für konsistente Test-Daten
- IntegrationTests Placeholder-Struktur

### Changed
- Tests nutzen jetzt shared Shared-Projekt für gemeinsame Test-Utilities
- Alle Test-Projekte referenzieren nun das Shared-Projekt

### Fixed
- Test-Projekt Konfiguration für MSTest v4 vereinheitlicht

## [0.0.1-beta.2] - 2025-12-13

### Added
- Deutsche Anleitung für Fotografen ([docs/getting-started-de.md](docs/getting-started-de.md))
- Theme Extension Support mit CSS-Loading für Plugins
- Statistics-Plugin Styling für Lumina
- Data Sources und Custom Templates für Frontmatter
- Statistics für erweiterte Galerie-Statistiken
- Lumina.Statistics als Theme-Erweiterung

### Changed
- Theme.Expose umbenannt zu Lumina
- Migration von .sln zu .slnx Format (Visual Studio 2022 17.10+)
- Code Quality Verbesserungen und Cleanup

### Fixed
- Statistics: Verbessertes Sorting und Template-Anzeige

## [0.0.1-beta.1] - 2025-12-10

### Added
- Initiales Release
- CLI mit System.CommandLine 2.0
- Image Processing mit NetVips (AVIF, WebP, JPG)
- Scriban Template Engine
- Plugin System (NuGet-basiert)
- Lumina (Standard-Theme)
- Source.OneDrive (OneDrive Shared Folder Support)
- Commands: generate, init, clean, theme, plugins, restore

[Unreleased]: https://github.com/spectara/revela/compare/v0.0.1-beta.21...HEAD
[0.0.1-beta.21]: https://github.com/spectara/revela/compare/v0.0.1-beta.20...v0.0.1-beta.21
[0.0.1-beta.20]: https://github.com/spectara/revela/compare/v0.0.1-beta.19...v0.0.1-beta.20
[0.0.1-beta.19]: https://github.com/spectara/revela/compare/v0.0.1-beta.18...v0.0.1-beta.19
[0.0.1-beta.18]: https://github.com/spectara/revela/compare/v0.0.1-beta.17...v0.0.1-beta.18
[0.0.1-beta.17]: https://github.com/spectara/revela/compare/v0.0.1-beta.16...v0.0.1-beta.17
[0.0.1-beta.16]: https://github.com/spectara/revela/compare/v0.0.1-beta.15...v0.0.1-beta.16
[0.0.1-beta.15]: https://github.com/spectara/revela/compare/v0.0.1-beta.14...v0.0.1-beta.15
[0.0.1-beta.14]: https://github.com/spectara/revela/compare/v0.0.1-beta.13...v0.0.1-beta.14
[0.0.1-beta.13]: https://github.com/spectara/revela/compare/v0.0.1-beta.12...v0.0.1-beta.13
[0.0.1-beta.12]: https://github.com/spectara/revela/compare/v0.0.1-beta.10...v0.0.1-beta.12
[0.0.1-beta.10]: https://github.com/spectara/revela/compare/v0.0.1-beta.8...v0.0.1-beta.10
[0.0.1-beta.8]: https://github.com/spectara/revela/compare/v0.0.1-beta.7...v0.0.1-beta.8
[0.0.1-beta.7]: https://github.com/spectara/revela/compare/v0.0.1-beta.6...v0.0.1-beta.7
[0.0.1-beta.6]: https://github.com/spectara/revela/compare/v0.0.1-beta.5...v0.0.1-beta.6
[0.0.1-beta.5]: https://github.com/spectara/revela/compare/v0.0.1-beta.4...v0.0.1-beta.5
[0.0.1-beta.4]: https://github.com/spectara/revela/compare/v0.0.1-beta.3...v0.0.1-beta.4
[0.0.1-beta.3]: https://github.com/spectara/revela/compare/v0.0.1-beta.2...v0.0.1-beta.3
[0.0.1-beta.2]: https://github.com/spectara/revela/compare/v0.0.1-beta.1...v0.0.1-beta.2
[0.0.1-beta.1]: https://github.com/spectara/revela/releases/tag/v0.0.1-beta.1

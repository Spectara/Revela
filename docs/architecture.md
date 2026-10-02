# Architecture Overview

Revela is a .NET static site generator for photographer-owned content. It combines
vertical slices for built-in capabilities with NuGet packages for optional plugins
and themes. This document describes current ownership and execution boundaries;
usage examples belong in the linked developer guides.

## Ownership Boundaries

| Area                            | Responsibility                                                                                   |
| ------------------------------- | ------------------------------------------------------------------------------------------------ |
| `src/Sdk/`                      | Public contracts, configuration models, engine results, and plugin/theme authoring support.      |
| `src/Sdk.Generators/`           | Compile-time configuration keys and template-model conversions, packaged with the SDK.           |
| `src/Core/`                     | Shared services, configuration persistence, paths, package context, and lifecycle coordination.  |
| `src/Commands/`                 | Host-owned configuration and diagnostic commands, including the shared project writer.           |
| `src/Features/`                 | Built-in generation, package management/restore and theme capabilities; not optional plugins.    |
| `src/Plugins/`                  | Optional capabilities such as source synchronization, Calendar, Statistics, Compress, and Serve. |
| `src/Themes/`                   | Base themes and theme extensions: templates, assets, and manifests.                              |
| `src/Cli/`, `src/Cli.Embedded/` | Host composition, command registration, and entry points.                                        |

Features keep their commands, services, and internal models together so unrelated
capabilities do not depend on a shared application layer. External plugins depend
on SDK contracts, not feature implementations. `IRevelaEngine` exposes generation
without requiring callers to invoke the CLI or use internal rendering models.

Both entry points use
[`HostBootstrap`](../src/Cli/Hosting/HostBootstrap.cs). Their package sources differ:
the standard CLI uses `DiskPackageSource` for runtime discovery; the embedded host
uses statically referenced packages through `EmbeddedPackageSource`, supporting
debugging and Native AOT without runtime assembly discovery.

See [Project Structure](project-structure.md) for the repository/build layout and
[Development](development.md) for contributor commands. Dependency versions belong
in the build configuration, not in a second version catalog here.

## Configuration And Paths

Configuration binds to strongly typed options. The normal sources, in increasing
precedence, are:

1. Property defaults on options classes.
2. Global `revela.json` for user-wide defaults.
3. Project-local `project.json`.
4. `site.json`, re-keyed under `site` by `AddSiteJson`.
5. Optional project-local `logging.json`.
6. Environment variables prefixed `SPECTARA__REVELA__`.

Command handlers apply their supported CLI overrides when executing. Plugin
`ConfigureConfiguration` hooks can append explicit sources after the environment
provider; arbitrary plugin sources are not restricted to the normal precedence.
The host does not implicitly load a `plugins/*.json` configuration directory.
See [host configuration wiring](../src/Cli/Hosting/HostBuilderExtensions.cs) and
[plugin configuration wiring](../src/Core/Extensions/PackageServiceCollectionExtensions.cs).

`site.json` has two consumers. `SiteCoreConfig` binds the identity fields used by
host features, such as title and language. `RenderService` also reads the document
dynamically for theme-specific properties that have no fixed options schema.
Build/hosting settings belong to `ProjectConfig`, not the theme-specific site data.

Plugin settings live below the host-owned `plugins` node as `plugins:<key>`,
where each plugin declares its key (`^[a-z][a-zA-Z0-9]*$`; official keys:
`serve`, `statistics`, `oneDrive`, `calendarFeeds`). Keys map directly to
environment variables such as `SPECTARA__REVELA__PLUGINS__SERVE__PORT`. The SDK
source generator rejects other sections in plugin/theme assemblies at compile time
and emits a `RevelaPluginConfigKey` assembly attribute per claimed key.
`AddPackages` reads those claims before any plugin configures services and fails
loading when two packages claim the same key; after the host is built, keys below
`plugins` that no loaded package claims are logged as warnings. Children that are
not key-shaped (for example dependency-map package IDs) are ignored by that check.
See [plugin configuration ownership](../src/Core/Configuration/PluginConfigOwnership.cs).

Options use handwritten `Section` constants and writable properties. Binding and
validation must remain trim-safe: generated .NET configuration binding and
`[OptionsValidator]` implementations replace reflection-based binding/validation.
Validation is normally lazy when options are read, allowing commands to configure
an incomplete project. `[RevelaConfig]` does not itself register options.
The [plugin guide](plugin-development.md#configuration) contains the authoring pattern.

Configurable source/output locations resolve through `IPathResolver`; fixed
project-relative locations use `ProjectPaths`. Services must not assume default
folder names or use the process working directory as a substitute for project
context.

### Dependencies And Feeds

`revela.json` and `project.json` declare dependencies with the same shape, bound
to `DependenciesConfig` (section `dependencies`) and merged per key:

```jsonc
{
  "theme": { "name": "Lumina" },
  "dependencies": {
    "feeds":    { "test": "../my-feed", "myFeed": "https://example.com/v3/index.json" },
    "packages": { "Spectara.Revela.Themes.Lumina": "1.0.0", "Acme.Revela.Watermark": "1.0.0" }
  }
}
```

- `dependencies.packages` is a flat package ID → exact version map. Install
  commands and restore persist the version that was actually installed; a
  missing value or `latest` resolves per the stable/prerelease host policy.
- One rule decides where an install is declared (`PackageDeclarations`): inside a
  project (a `project.json` exists) only in `project.json`, otherwise only in the
  global `revela.json`. Uninstall removes the declaration from that same file.
  Restore pins versions only for entries the target file already declares; it
  never copies `revela.json` dependencies into `project.json`.
- `plugin install` and `theme install` share one flow (`PackageInstallService`)
  that validates the package type from the package's own nuspec, not the package
  index. A freshly extracted package of the wrong type is removed again.
- Package kind is never derived from the ID. Restore treats every entry as
  required, checks "installed" by package ID across loaded plugins and themes,
  and reports the kind from the nuspec package type after extraction.
- `theme.name` is a manifest name. Restore resolves it through local and
  installed themes; the official `Spectara.Revela.Themes.<name>` package is only
  a fallback when neither an installed theme nor a newly restored theme package
  can provide it.
- `dependencies.feeds` values are URLs or folders; relative folders resolve from
  the declaring file. Feeds declared only in `project.json` are excluded from
  package sources until the owner consents (see
  [Security Model](security-model.md#project-declared-package-feeds)).
  Provenance comes from reading `revela.json` and `project.json` separately,
  because the merged configuration no longer knows which file set a key.
- The root `plugins` node is reserved for plugin settings and is never read as a
  dependency list.

Configuration commands and package registration update project settings through
`IConfigService`. Reading, merging, provider validation, staged replacement and
reload are serialized within the service instance. Invalid originals or ambiguous
case-split patches fail unchanged; null deletes against the current document,
and valid semantic no-ops preserve bytes without reloading. Global writers mutate
their own sections in the JSON document rather than rebuilding it from a DTO,
preserving unknown nested settings. This is not an interprocess lock or protection
against arbitrary concurrent editors.

No configuration source watches its file: a CLI run is short-lived, and recursive
file watching stalled startup in large projects on Linux. Every in-process write of
`revela.json`, `project.json` or `site.json` goes through `ConfigFileWriter`
(validate with the JSON reader, write a hidden temp file, replace atomically,
reload `IConfigurationRoot`), so the interactive menu sees the new values.

## Generation Pipeline

The built-in sequence for `revela generate all` is:

```text
scan -> optional Calendar -> optional Statistics -> pages -> images
 100          150                  200              300       400
```

The values come from `PipelineOrder` in the
[pipeline contracts](../src/Sdk/Abstractions/IPipelineStep.cs). Installed plugins can
contribute sequential steps through `CommandDescriptor` and `IPipelineStep`.
`CommandDescriptor.Order` is the ordering source used by the host's
`IPipelineStepOrderProvider`; the
[engine](../src/Features/Generate/Services/RevelaEngine.cs) uses it for UI-free
execution. A failed step stops the sequence. Structural checks under `check` are
separate from generation, not additional generate steps.

The CLI `all` command passes the hidden `--in-pipeline` option
([`PipelineInvocation`](../src/Sdk/Abstractions/PipelineInvocation.cs)) to each
step. Steps read it with `parseResult.IsInPipeline()` and omit their standalone
"Next steps" hints, so `generate all` prints a single next-step hint at the end.

### Scan

Scanning discovers `_index.revela` content, images, shared images, and navigation
under the configured source root. It parses frontmatter, reads image metadata and
EXIF, and prepares the content tree and image manifest. Placeholders are prepared
during scanning when configured. Invalid content or conflicting output slugs must
fail before rendering.

### Enrich And Render

Optional Calendar and Statistics steps prepare data consumed by templates.
Calendar rejects missing/invalid data and unsupported timed or recurring events;
a valid empty calendar is allowed. Failure preserves that page's previous JSON,
without promising cross-page transactions or provider freshness.
Page generation combines the content tree, site data, theme configuration, and
image manifest. Markdig renders Markdown; Scriban renders the selected theme's
layout, body, and partials. Assets and static content are published with the site.

Rendering determines image usage, including content images, filtered galleries,
and viewer memberships. Inline selections are prepared before canonical photo-page
aggregation and reused for final output. An image's identity is distinct from its
occurrences and their navigation contexts; details belong in
[Inline Galleries](inline-galleries.md).

### Generate Image Variants

NetVips produces the required responsive sizes and configured formats after page
generation. Metadata and variant descriptions are available earlier, so templates
can generate URLs before the variant files exist. Rendering and image processing
have separate parallelism settings. There is no fixed speedup guarantee: work
depends on input images, formats, cache state, and hardware.

Skipping unchanged images relies on the image processing state in
`.revela/state/images.json` ([`ImageStateStore`](../src/Features/Generate/Services/ImageStateStore.cs)),
not on the scan manifest. Per source image it records a fingerprint of the source
file (size, modification time) and the pipeline (output version, resize mode) plus
the quality each format was encoded with. An image is re-encoded only when that
fingerprint changes, a configured quality differs, or an expected variant file is
missing. The state has its own schema version, so manifest format changes and
manifest rebuilds never re-encode images; only image-output changes do. It is
checkpointed during long runs and written atomically.

### Project Folders: Cache And State

Revela keeps its own data in `.revela/` in the project
([`ProjectPaths`](../src/Sdk/ProjectPaths.cs)), split by what losing it costs:

| Folder | Holds | Lost by | Cost of losing it |
|--------|-------|---------|-------------------|
| `.revela/cache/` (`ProjectPaths.Cache`) | Scan manifest, plugin data (`<page>/statistics.json`, `<page>/calendar.json`) | `clean cache`, `clean all` | A rescan |
| `.revela/state/` (`ProjectPaths.State`) | State of the output: `images.json` (image variants), `compress.json` (owned `.gz`/`.br` sidecars) | `clean output`, `clean all` | Re-encoding every image while the output still exists |

The state describes the output, so the two are deleted together; `clean cache`
never touches it. Nothing Revela-internal is written into the output directory,
so a deployed site contains only the site, and `revela serve` refuses dot-files
and dot-folders (except `/.well-known/`) as defense in depth. Projects from
before beta.21 kept everything in `.cache/`; it is carried over once
([`LegacyCacheCarryOver`](../src/Features/Generate/Services/LegacyCacheCarryOver.cs)):
the image state (`.cache/images.json`, or the `processedImages` of an old
manifest) moves to `.revela/state/images.json`, then the rest of `.cache/` moves
to `.revela/cache/`. If `.revela/cache/` already exists, the old folder is left
in place with a warning.

NetVips keeps image processing and EXIF extraction in-process. Markdig and Scriban
separate content parsing from theme presentation without requiring a web server.
These are implementation choices, not claims that rendering arbitrary input is safe.

## Packages And Plugin Lifecycle

NuGet supplies package identity, versions, feeds, and distribution. The SDK is the
author-facing contract; package loading and installation remain host-owned.
Plugins and themes share `PackageMetadata`; the required package ID is distinct
from its display name. Embedded theme IDs come from the resource assembly identity,
not a display name that may contain spaces.

The normal host lifecycle is:

1. `IPackageSource` discovers plugins and themes. The disk source uses the
   application's and user's package locations; the embedded source supplies
   statically referenced implementations.
2. Required plugin dependencies are checked before registration. Plugins whose
   required plugins are missing are excluded; optional extension targets are
   represented separately by `ExtendsPackages`.
3. Plugin `ConfigureConfiguration` hooks run before plugin `ConfigureServices`.
   Services and options are registered before the host service provider is built.
4. After host construction, `GetCommands(IServiceProvider)` supplies command
   descriptors for the CLI tree and interactive menu.

`ConfigureConfiguration` and `GetCommands` are optional; `ConfigureServices` is the
required registration hook. Use idempotent DI registrations and typed HTTP clients.
Package-mutating CLI commands avoid normal package loading so loaded assemblies do
not lock files being installed or removed.

Installation extracts a package and registers its exact installed version through the
shared configuration service. Extraction and registration are not a whole-install
transaction: registration failure is a failed install and can leave extracted
files. It must not announce success or retry another feed after that failure;
cancellation remains cancellation. Automatic rollback cannot safely infer which
previously installed files to restore. Restore reconciles declared dependencies
with available packages; it is not runtime hot-reloading of an already constructed
host.

SDK package targets include plugin-specific dependencies and dependency metadata
while excluding host-provided assemblies. Keeping the same SDK/CLI contracts in
the host and plugins avoids duplicate contract types across load contexts.
See [Plugin Development](plugin-development.md) for packaging and authoring examples.

## Artifact Ownership

Producers must invalidate downstream artifacts before replacing their inputs.
The shared artifact lifecycle follows declared dependencies transitively and
rejects missing dependencies, duplicate owners, and cycles. The producer then
cleans its own previous artifact; cleanup failure must stop the write.

This applies to direct commands and engine calls, not just `generate all`.
Registration does not intercept arbitrary plugin filesystem writes. Plugins must
honor the same preparation/cleanup contract; see
[Derived Output Artifacts](plugin-development.md#derived-output-artifacts).

Changing the installed or enabled plugin set requires `revela clean all` before
regeneration because unloaded plugins cannot participate in invalidation. Artifact
dependencies do not imply ownership of unrelated files. Compression separately
records owned sidecars and fingerprints in `.revela/state/compress.json` (outside
the output, so it is never published; a legacy output-root
`.revela-compress.manifest` is moved there on open). It survives cache cleanup and
is removed by `clean output`. It never adopts files
based on their extension or contents; unowned or externally changed targets are
preserved and conflicts fail explicitly. Completed sibling-staged writes are
recorded, and failed registration rolls back only files created by that operation.
A crash can leave an unowned file requiring manual resolution; no whole-run or
power-loss transaction is promised. Generation requires exclusive output ownership.

## Themes And Trust

Themes own presentation through manifests, Scriban templates, and assets. Local
overrides and theme extensions supply replacements or additions without moving
feature services into the theme layer. Theme-provided HTML, CSS, and JavaScript
are trusted author-selected code, not an isolation boundary.

A selected local manifest that exists but is invalid is an error, not a missing
theme. It must not silently fall back to an installed namesake; restore inspects
dependencies but installs nothing when an invalid local theme is present.
A genuinely absent local manifest still permits normal fallback.

`Partials/ContentImage.revela` is mandatory for rendering content images, and a
missing layout or `ContentImage` partial fails the render with a clear message
(there is no built-in fallback markup). Inline
grids require `Partials/GalleryGrid.revela` when used. `[[photo]]` blocks use the
optional `Partials/PhotoFigure.revela` and fall back to a linked content image. Viewer capabilities and
defaults belong to the base theme; `Body/Photo.revela` is required when that theme
supports the `page` viewer. See [Inline Galleries](inline-galleries.md#theme-contract)
for occurrence models and viewer behavior rather than duplicating that catalog.
`Body/NotFound.revela` is optional: when present (and no `source/_static/404.html`
exists) the renderer writes `404.html` once at the output root with a root-absolute
`basepath`, outside navigation, sitemap and page count.

Template properties describe identity and data. URL helpers such as `page_url`,
`variant_url`, and `asset_url` own rendering paths and prefixes. The SDK's
[template-model generation](../src/Sdk/README.md#template-models) provides
direct-property conversions without runtime reflection; consumers using it must
reference Scriban explicitly.

Plugins run in-process with the user's permissions. Content and templates are
trusted, raw HTML can pass through Markdown, and Scriban is not a security sandbox
for untrusted submissions. URL checks provide guardrails, not DNS or network
isolation. Published image variants strip embedded metadata, but themes can still
publish EXIF/GPS values read into the manifest. See the
[Security Model](security-model.md) for the complete boundaries and limitations.

## Verification

Build, test, and format procedures live in [Development](development.md).
Release artifacts and website publication follow the [CI/CD guide](../.github/workflows/README.md).
Successful compilation or static HTML checks do not establish browser behavior,
cross-platform correctness, or hosted release/deployment success.


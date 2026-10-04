# Security Model

This document describes Revela's threat model, security defaults, and the rationale
behind them. It is intended for contributors and power users — end users do not need to
read this to use the tool safely in its intended scenario.

---

## Intended scenario

Revela is a **single-author static site generator**. The expected workflow is:

1. A photographer authors content (`_index.revela`, `site.json`, themes) on their own machine.
2. The CLI reads that content and renders static HTML/CSS/images.
3. The output is uploaded to a static host (Netlify, GitHub Pages, S3, …).

In this scenario, **all input to the renderer is trusted by the same person who owns the
output**. There is no untrusted user-submitted content, no multi-tenant boundary, and no
rendering of third-party submissions.

---

## Trust assumptions

| Source                                  | Trust                                       | Why                                                                                                                              |
| --------------------------------------- | ------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `_index.revela` files                   | **Trusted**                                 | Authored by the site owner.                                                                                                      |
| `site.json`, `project.json`             | **Trusted**                                 | Configured by the site owner.                                                                                                    |
| Theme files (`*.revela`, CSS, JS)       | **Trusted**                                 | Either authored locally, or installed via NuGet from a feed the user explicitly configured.                                      |
| Images in the source folder             | **Trusted**                                 | Provided by the site owner.                                                                                                      |
| OneDrive shared folders (Source plugin) | **Trusted**                                 | The site owner controls the share.                                                                                               |
| iCal feeds (Source plugin)              | **Trusted URL, validated network target**   | URL itself is configured by the owner. SSRF guardrails reject loopback/private/link-local targets.                               |
| Plugin DLLs from NuGet                  | **Trusted source, OS-permission-protected** | Loaded only from feeds the user explicitly added or confirmed. Same trust model as `dotnet tool install` — see [Plugin trust](#plugin-trust). |
| HTTP requests to the dev server         | **Trusted (loopback only)**                 | The Serve plugin binds to `localhost`. Path-traversal attempts return 403; dot-files and dot-folders (except `/.well-known/`) return 404. |

---

## What Revela protects against

- **Path traversal in the dev server** — see [`StaticFileServer.TryResolveSafePath`](../src/Plugins/Serve/StaticFileServer.cs).
- **Revela-internal files in the published site** — the scan manifest, image state, plugin data and compression ownership record live in the owners' folders below the project's `.revela/`, never in the output, so they are not uploaded. As defense in depth the dev server does not serve dot-files or dot-folders (`StaticFileServer.IsHiddenPath`).
- **SSRF in source-plugin URL fetches** — see [`UrlSafety`](../src/Sdk/Validation/UrlSafety.cs). Rejects loopback (`127.0.0.0/8`, `::1`, `localhost`), RFC 1918 private (`10/8`, `172.16/12`, `192.168/16`), RFC 6598 CGN (`100.64/10`), link-local (`169.254/16`, including AWS/Azure metadata IP), IPv6 link-local/site-local/ULA (`fc00::/7`), multicast, IPv4-mapped loopback, and non-https schemes (http opt-in for legacy iCal feeds).
- **Sensitive URLs leaking into source-sync logs** - Source.Calendar logs the host only. OneDrive sync uses a host/hash reference. Both typed clients remove default HTTP request loggers; OneDrive additionally replaces its resilience logger with event/host/status/error-category output. Command failures omit potentially credential-bearing exception text. This does not cover arbitrary custom telemetry subscribers, shell history or every HTTP client.
- **OneDrive cleanup and interrupted downloads** - linked descendants are excluded from orphan analysis and freshly checked before deletion. An intentionally linked source root is allowed. Downloads stage complete data beside the destination before replacement; failures preserve prior bytes and timestamps. Unix staging starts private and existing destination modes are retained. These are per-file safeguards, not hostile-filesystem-race protection or a whole-sync transaction.
- **Configuration and compression data preservation** - project updates validate exact candidate JSON with the configuration provider before replacement; global writers preserve unrelated JSON settings and reject invalid originals. Compression-specific operations act only on explicitly recorded sidecars and reject unowned or changed destinations; the ownership record is not a sandbox against a process that can edit it. The ownership record lives in `.revela/compress/ownership.json`, outside the published output. `clean output` intentionally deletes the entire output content (through the core and plugin output artifacts, including the image state and the compression record).
- **Accidental calendar output escape and interrupted replacement** — selected feed destinations are checked below the configured source root, including existing link components and duplicate targets. Downloads use same-directory temporary replacement; failed/aborted requests retain the previous destination. This is not protection against a hostile concurrent process with the author's filesystem permissions.
- **Spectre.Console markup injection from user-controlled strings** — every `MarkupLine` call passes user data through `Markup.Escape`.

---

## What Revela explicitly does NOT protect against

### DNS targets and provider data quality

`UrlSafety` does not resolve DNS. A permitted hostname can resolve to a private
address; deployment-level egress policy is needed for stronger isolation.
Source.Calendar validates each redirect's scheme and literal host and disallows
HTTPS downgrade, but does not claim DNS-rebinding protection.

A downloaded response can still be invalid or stale calendar data. Calendar
generation validates the supported booking format and fails on invalid/missing
inputs; it cannot attest to a provider's completeness or freshness. Successful
downloads across multiple feeds are not rolled back when another feed fails.

OneDrive downloads use `ResponseHeadersRead`; the request timeout does not cover
the subsequent body copy. That copy currently receives caller cancellation only,
with no body-wide deadline, so a stalled body can keep synchronization waiting.
Staging protects the previous destination but does not fix this hang risk; see
[`SharedLinkProvider.DownloadFileAsync`](../src/Plugins/Source/OneDrive/Providers/SharedLinkProvider.cs).

### Raw HTML in `_index.revela` bodies

Revela renders Markdown via [Markdig](https://github.com/xoofx/markdig) **without** calling `.DisableHtml()`. Raw HTML — including `<script>`, `<iframe>`, `onclick=` attributes, and `javascript:` URLs in links — passes through to the rendered HTML.

This matches the default behaviour of:

- Jekyll (Kramdown)
- Eleventy (markdown-it with `html: true`)
- MkDocs (Python-Markdown)
- Astro (remark)
- Zola (pulldown-cmark)
- Docusaurus / Gatsby

The only mainstream outlier is Hugo, which gates raw HTML behind an `unsafe = true` flag.

**Why Revela follows the majority pattern:**

Markdig's own documentation makes the trade-off explicit (quoted from [Markdig usage docs § Configuration options](https://xoofx.github.io/markdig/docs/usage/#configuration-options)):

> ⚠️ **Caution**
>
> Markdig is a Markdown processor, not an HTML sanitizer. Disabling HTML parsing reduces risk from raw HTML input, but it does not make rendering untrusted Markdown to HTML "safe" by itself. If you accept user-provided Markdown, sanitize the generated HTML and consider filtering/rewriting link and image URLs.

In other words: calling `.DisableHtml()` alone would be **security theater** — `[click](javascript:alert(1))` and `![x](data:image/svg+xml,<svg onload=…>)` still work even with HTML parsing disabled. Real protection requires a downstream HTML sanitizer (e.g. Ganss.Xss) plus URL-scheme filtering. That complexity is not justified for the single-author scenario where the input is by definition trusted.

The `revela-website` sample relies on this — landing pages use `<picture>`, `<video autoplay loop muted>`, `<section class="hero-panorama">`, and similar layout HTML directly inside Markdown bodies.

**When this assumption breaks:**

- You accept `_index.revela` contributions from people who are not you (e.g. collaborative photo book, public submission portal).
- You blindly include third-party themes that bundle arbitrary JS without reviewing them.
- You shared-OneDrive your source folder with users you do not fully trust.

If any of those apply to you, the current Revela renderer is not enough. The upgrade path is sketched below.

### Stale image EXIF / GPS data in published images

**Published image variants are stripped of all embedded metadata** — EXIF, XMP, ICC profiles, and GPS coordinates. The image writer saves every variant with `keep: ForeignKeep.None` for JPEG, WebP, AVIF, and PNG (see [`NetVipsImageProcessor.SaveImage`](../src/Features/Generate/Services/NetVipsImageProcessor.cs), the `Jpegsave`/`Webpsave`/`Heifsave`/`Pngsave` calls), so the GPS coordinates of your home do **not** leak into the rendered site.

Distinguish two separate things:

- **Manifest EXIF extraction (read-only):** Revela reads EXIF from your source photos into the in-memory `ImageManifest` (see `ExtractExifData` in the same file) so camera settings can be shown in templates and aggregated by the Statistics plugin. GPS latitude/longitude are among the fields read. This data lives in the manifest and is surfaced only if your theme chooses to render it.
- **Published variant metadata (stripped):** The resized/re-encoded files written to the output folder carry no embedded metadata at all.

Since #98 the loader also calls `Autorot()` before stripping, so orientation is baked into the pixels rather than left in a now-removed EXIF tag. Likewise, pixels are converted to sRGB using the embedded ICC profile before the profile is dropped, so stripping it does not change the displayed colors.

If your theme deliberately renders GPS from the manifest and you do not want coordinates published, omit them in your theme templates — the embedded file metadata is already gone.

### Third-party theme review

Themes installed via `revela theme install` are NuGet packages. Their HTML/CSS/JS is rendered as part of your site. **Review themes before installing**, just as you would review any dependency.

---

## Plugin trust

### Project-declared package feeds

A project can declare feeds in `project.json` (`dependencies.feeds`). Because a project may be
a cloned repository rather than something the machine owner wrote, such feeds are **not trusted
by default**:

- A feed counts as project-declared when `project.json` declares it and the global `revela.json`
  does not declare the same name for the same location. Provenance is determined by reading both
  files separately; the merged configuration cannot tell which file set a key. If `project.json`
  cannot be read, every feed not declared globally is treated as project-declared. A feed that
  only a `SPECTARA__REVELA__DEPENDENCIES__FEEDS__*` environment variable supplies is set by whoever
  runs Revela and is not project-declared.
- Project-declared feeds are excluded from package sources (install, restore, package
  index refresh, setup wizard) until the owner consents for the current process.
- `revela restore`, `revela plugin install` and `revela theme install` list each such feed (name,
  URL or resolved folder, and the `project.json` path) and ask for confirmation. Declining
  installs nothing.
- Non-interactive runs (CI, redirected output, see `IConsoleCapabilities`) fail with a non-zero
  exit code and point to `--allow-project-feeds`, which approves them explicitly.
- An explicit `--source <url-or-folder>` does not use configured feeds and needs no consent;
  naming a project-declared feed with `--source` does.
- Local folders and remote URLs are handled alike. The existing `http://` rejection still applies
  after consent.

Packages listed in `dependencies.packages` are explicit owner choices and may use any package ID,
including third-party ones. The official-prefix rule described in the next section applies only to
what the package index and setup wizard *offer*, not to dependencies the configuration declares.

### Why no hash-pinning between install and load

Revela follows the **same trust model as `dotnet tool install`** — trust is established at install time, not re-verified at load time. Microsoft's official `dotnet tool` documentation makes this explicit:

> ⚠️ **Important**
>
> .NET tools run in full trust. Don't install a .NET tool unless you trust the author.

Several other Microsoft .NET tools (`dotnet-ef`, `dotnet-format`, `dotnet-counters`, `dotnet-trace`, `dotnet-script`) and ecosystem tools (`Cake.Tool`, `fake-cli`) all behave the same way. None of them hash-pin DLLs between install and load.

The rationale: any attacker with the filesystem permissions needed to tamper with `%APPDATA%/Revela/plugins/` already has those same permissions on **every other executable in the user's PATH** — browsers, IDEs, the .NET runtime itself. Hash-pinning the plugin folder would be security theater because the same attacker can also rewrite `revela.json` (where the hash would live) in the same step.

What actually matters — and what we do — is making install-time trust explicit:

- **Spectara prefix is reserved on nuget.org** — nobody else can publish under `Spectara.Revela.*`.
- **Third-party plugins use their own prefix** — the user sees the package owner before installing and decides whether to trust them, the same way they would for any `dotnet tool install`.
- **The setup wizard and package index offer only official packages** — `packages refresh` indexes only `Spectara.Revela.*` IDs (case-insensitive). Results from remote feed searches must additionally be marked `verified` (nuget.org reserved prefix); bundled and explicitly configured local folders are trusted by prefix only. Everything else, including look-alike IDs such as `Evil.Spectara.Revela.*`, is ignored. Third-party packages are installed only by explicit ID. See [`PackageTrustPolicy`](../src/Core/Services/PackageTrustPolicy.cs).
- **Package transport and extraction are constrained** — package sources and package URLs must be `https://`, a local folder (including UNC paths) or loopback `http://`; plain `http://` and any other URL scheme are rejected, and `revela config feed add` refuses such feeds before saving them. Package IDs must satisfy NuGet ID rules before they are used in plugin paths, including IDs read from a `.nuspec`, and every extracted file must stay inside its plugin directory. Without an explicit `--version`, prerelease packages are selected only when the running Revela host is itself a prerelease build.

### Verifying packages yourself (optional, manual)

If you want to verify a `.nupkg` you downloaded — either from nuget.org or from a Revela GitHub release — the standard tooling already covers it without any code changes in Revela:

**Packages from nuget.org** are auto-signed by nuget.org's repository signature. Verify with:

```bash
dotnet nuget verify path/to/Spectara.Revela.Plugins.Statistics.1.2.3.nupkg
```

**Packages from a Revela GitHub release** are attested by [GitHub Build Provenance](https://docs.github.com/en/actions/security-guides/using-artifact-attestations-to-establish-provenance-for-builds). Each `.nupkg` in `release.yml` is signed by GitHub's Sigstore-backed attestation step. Verify with the GitHub CLI:

```bash
gh attestation verify path/to/Spectara.Revela.Plugins.Statistics.1.2.3.nupkg --owner Spectara
```

The attestation proves the package was built by the official `Spectara/Revela` GitHub Actions workflow at a specific commit — cryptographically, against GitHub's public Sigstore transparency log.

### Why no author-signing of Spectara packages

NuGet author-signing is a **one-way door**: once you publish a signed package for a given package ID, all future versions of that ID must be signed (NU3038 / NU3018 enforcement). If our certificate source becomes unavailable — expired free OSS license, vendor change, revoked cert — we cannot publish unsigned updates either. For a small project, this is an unacceptable supply-chain risk.

GitHub Build Provenance has the opposite property: it can be added or removed per release without breaking anything downstream. We use it because it does not lock us in.

### When this trust model is not enough

Replace it with explicit verification (in Revela code, optional, currently not implemented):

1. **NuGet repository-signature verification on download** — use `NuGet.Packaging.Signing.PackageSignatureVerifier` with `SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy()`. Catches MITM and feed compromise. ~60 LOC. Tracked separately if needed.
2. **GitHub attestation verification on release-bundled `.nupkg`** — either shell out to `gh attestation verify` or use the (currently alpha) `sigstore-dotnet` library. Catches tampering of the GitHub release ZIP. Larger scope, defer until Sigstore-dotnet is stable.

Neither is necessary for the current single-user threat model. They become relevant if Revela starts being used in CI pipelines or air-gapped environments where the user cannot manually verify packages.

### Plugin configuration isolation

A plugin or theme reads and writes only its own settings below `plugins:<key>`. Host
settings reach it as the `IOptions<T>` the host registers (`PathsConfig`, `SiteCoreConfig`, …);
another plugin's settings do not reach it at all.

| Rule | When | Mechanism |
| ---- | ---- | --------- |
| Settings live below `plugins:<key>`; the `Section` const matches `[RevelaConfig]` | Compile time | `REVELA001` / `REVELA002` (SDK source generator) |
| `BindConfiguration` binds only a `[RevelaConfig]` type of the same assembly, to its own section | Compile time | `REVELA003` (SDK analyzer) |
| No `IConfiguration` & co., `ConfigurationBinder`, `Configure<T>(IConfiguration)`, `OptionsBuilder.Bind`, configuration sources | Compile time | `RS0030` with [`BannedSymbols.RevelaPlugin.txt`](../src/Sdk/build/BannedSymbols.RevelaPlugin.txt) |
| No plugin-added configuration sources; no general config writer | API | `IPlugin` has no configuration hook; `IConfigService` is host-only (Core, not in the SDK) |
| One owner per key | Load time | Generated `[assembly: RevelaPluginConfigKey]` claims; a duplicate claim stops plugin loading |
| A plugin writes only its own key | Runtime | `IPluginSettingsWriter<T>` throws unless the assembly declaring `T` claims `T`'s key |

[`Spectara.Revela.Sdk.targets`](../src/Sdk/build/Spectara.Revela.Sdk.targets) applies the
compile-time rules to every project with `PackageType` `RevelaPlugin` or `RevelaTheme` —
in this repository and through the NuGet package — and makes them errors via
`WarningsAsErrors`, which a project `.editorconfig` cannot downgrade. Host projects (Core,
Commands, Features, CLI) get no ban list. Code that the .NET configuration binding generator
emits for an allowed `BindConfiguration` call necessarily uses `IConfiguration`; only that
generated code is exempt (suppression `REVELASP001`). The packaged flow is proven by
[test-sdk-consumer.ps1](../scripts/test-sdk-consumer.ps1), which the release workflow runs.

**Limit:** a plugin is in-process .NET code running with the user's full trust (see above).
These rules prevent mistakes and keep the plugin contract clear; they are not a sandbox. A
plugin that drops its `PackageType`, suppresses the diagnostics, uses reflection or reads
`project.json` from disk can bypass them. The boundary against a malicious plugin remains the
install-time trust decision.

---

## Upgrade paths (if your threat model changes)

### Multi-user / untrusted Markdown

If you start accepting `_index.revela` contributions from less-trusted parties:

1. Add a downstream HTML sanitizer between Markdig and the renderer output. [Ganss.Xss](https://www.nuget.org/packages/HtmlSanitizer) is the established C# choice — Allow-list-based, ~150 KB, MIT, actively maintained, default config blocks `<script>`, `<object>`, `on*=` handlers, and `javascript:` URLs while keeping `<div>`, `<a>`, `<picture>`, `<video>`, `<section>`, `<article>` etc.
2. Combine with URL-scheme filtering on `<a href>` and `<img src>` (block `javascript:`, `data:` except `data:image/*`, `vbscript:`).
3. Optionally call `pipeline.UseDisableHtml()` as a defense-in-depth layer (still not sufficient on its own — see Markdig's caution above).

### Verifying plugin author identity

If you publish Revela plugins to nuget.org and want consumers to verify they came from you specifically:

1. Use **nuget.org's repository signature** (free, automatic for every package on nuget.org) — verifiable via `NuGet.Packaging.Signing.PackageSignatureVerifier` with `SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy()`.
2. Use **GitHub Build Provenance** if you ship plugins as GitHub release assets — free, automatic, no certificate lock-in (see [Plugin trust](#plugin-trust) above).
3. **Author-signing is not recommended** — it's a one-way door (see explanation in [Plugin trust](#plugin-trust)). If you need it anyway: [SignPath.io](https://signpath.io) (free for OSS, commercial tiers from ~€5/mo) or [Azure Trusted Signing](https://learn.microsoft.com/en-us/azure/trusted-signing/) (~$10/mo).

### Stripping image EXIF

Already done — published variants are written with `ForeignKeep.None` (see [Stale image EXIF / GPS data in published images](#stale-image-exif--gps-data-in-published-images)). Only the in-memory manifest retains EXIF, for display and statistics. No extra step is needed to keep metadata out of the output files.

---

## Reporting a security issue

Please **do not** open public GitHub issues for security vulnerabilities. Instead,
follow the private reporting route in the repository's [`SECURITY.md`](../SECURITY.md) —
use GitHub's **Report a vulnerability** button on the
[Security tab](https://github.com/Spectara/Revela/security) to open a private advisory.

---

## Related documentation

- [`docs/architecture.md`](architecture.md) — overall system design
- [Architecture](architecture.md) — plugin loading and ownership boundaries
- [Plugin Development](plugin-development.md) — how to write plugins (incl. URL safety guidance)
- [`src/Sdk/Validation/UrlSafety.cs`](../src/Sdk/Validation/UrlSafety.cs) — SSRF guardrails source
- [`src/Plugins/Serve/StaticFileServer.cs`](../src/Plugins/Serve/StaticFileServer.cs) — dev-server path-traversal protection

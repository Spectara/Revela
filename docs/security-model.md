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

| Source | Trust | Why |
|--------|-------|-----|
| `_index.revela` files | **Trusted** | Authored by the site owner. |
| `site.json`, `project.json` | **Trusted** | Configured by the site owner. |
| Theme files (`*.revela`, CSS, JS) | **Trusted** | Either authored locally, or installed via NuGet from a feed the user explicitly configured. |
| Images in the source folder | **Trusted** | Provided by the site owner. |
| OneDrive shared folders (Source plugin) | **Trusted** | The site owner controls the share. |
| iCal feeds (Source plugin) | **Trusted URL, validated network target** | URL itself is configured by the owner. SSRF guardrails reject loopback/private/link-local targets. |
| Plugin DLLs from NuGet | **Trusted source, OS-permission-protected** | Loaded only from feeds the user explicitly added. Same trust model as `dotnet tool install` — see [Plugin trust](#plugin-trust). |
| HTTP requests to the dev server | **Trusted (loopback only)** | The Serve plugin binds to `localhost`. Path-traversal attempts return 403. |

---

## What Revela protects against

- **Path traversal in the dev server** — see [`StaticFileServer.TryResolveSafePath`](../src/Plugins/Serve/StaticFileServer.cs).
- **SSRF in source-plugin URL fetches** — see [`UrlSafety`](../src/Sdk/Validation/UrlSafety.cs). Rejects loopback (`127.0.0.0/8`, `::1`, `localhost`), RFC 1918 private (`10/8`, `172.16/12`, `192.168/16`), RFC 6598 CGN (`100.64/10`), link-local (`169.254/16`, including AWS/Azure metadata IP), IPv6 link-local/site-local/ULA (`fc00::/7`), multicast, IPv4-mapped loopback, and non-https schemes (http opt-in for legacy iCal feeds).
- **Sensitive URLs leaking into default-verbosity logs** — OneDrive share URLs and iCal feed URLs are logged at `Debug`, only the host appears at `Information`.
- **Spectre.Console markup injection from user-controlled strings** — every `MarkupLine` call passes user data through `Markup.Escape`.

---

## What Revela explicitly does NOT protect against

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

Image processing preserves EXIF metadata. If your source photos contain GPS coordinates of your home, that GPS data ends up in the published image. Strip it before rendering if that matters to you.

### Third-party theme review

Themes installed via `revela theme install` are NuGet packages. Their HTML/CSS/JS is rendered as part of your site. **Review themes before installing**, just as you would review any dependency.

---

## Plugin trust

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
- **Plugins are loaded only from sources the user added** — there is no "discover and auto-install" mechanism.

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

Add a NetVips post-processing step that removes EXIF before writing the variant. Open an issue if you want this as a built-in option.

---

## Reporting a security issue

Please **do not** open public GitHub issues for security vulnerabilities. Instead, see
the project's `SECURITY.md` (or open a private security advisory in the repository).

---

## Related documentation

- [`docs/architecture.md`](architecture.md) — overall system design
- [`docs/plugin-system-v2.md`](plugin-system-v2.md) — plugin loading + integrity verification
- [`src/Sdk/Validation/UrlSafety.cs`](../src/Sdk/Validation/UrlSafety.cs) — SSRF guardrails source
- [`src/Plugins/Serve/StaticFileServer.cs`](../src/Plugins/Serve/StaticFileServer.cs) — dev-server path-traversal protection

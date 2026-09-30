# GitHub Actions CI/CD Guide

This directory hosts all automation used to build, test, sign, and ship Revela. The sections below summarise the current production workflows and provide a checklist for release verification.

---

## Workflow Summary

| Workflow           | File                                     | Triggers                                                     | Purpose                                                                                                                                                    |
| ------------------ | ---------------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| CI                 | [ci.yml](ci.yml)                         | Push/PR to `main` (subject to path filters), manual dispatch | Ubuntu Release build, full-solution tests with coverage and formatting.                                                                                    |
| Release            | [release.yml](release.yml)               | `v*` tag push or manual dispatch with `version`              | Validate, test and pack, verify actual platform archives, attest and sign. Only tag pushes create a GitHub Release; no NuGet.org publication.             |
| Deploy Website     | [deploy-website.yml](deploy-website.yml) | Reusable call after Release publication, or manual dispatch  | Automatic: caller's exact commit and tag. Manual: explicit source ref and existing release tag. Both generate and check before publishing to GitHub Pages. |
| Dependency Updates | [Dependabot](../dependabot.yml)          | Weekly (Monday), automatic                                   | Creates PRs for outdated NuGet packages and GitHub Actions.                                                                                                |

> **Note**
> `code-quality.yml` from early plans was replaced by the consolidated `ci.yml` pipeline.

This guide describes the implemented paths, not proof that a hosted run passed.
Remote workflow execution requires explicit permission. In particular, a manual
Release run still performs external attestation and keyless signing; it is not a
side-effect-free local check.

## CI Gates

### Action Versions

Stable upstream releases checked on 2026-09-14. Every third-party action is pinned
to the full commit SHA of the referenced tag, with the tag kept as a trailing
comment (`uses: actions/checkout@<sha> # v7`), so a repointed upstream tag cannot
change what runs. Every `actions/checkout` step sets `persist-credentials: false`;
no job pushes or tags with the checkout token. Dependabot checks the GitHub Actions
ecosystem weekly and updates SHA and comment together. Review version updates and
lint affected workflows.

| Action                          | Pinned tag         | Latest stable release checked |
| ------------------------------- | ------------------ | ----------------------------- |
| actions/checkout                | v7                 | v7.0.1                        |
| actions/setup-dotnet            | v6                 | v6.0.0                        |
| actions/upload-artifact         | v7                 | v7.0.1                        |
| actions/download-artifact       | v8                 | v8.0.1                        |
| actions/upload-pages-artifact   | v5                 | v5.0.0                        |
| actions/deploy-pages            | v5                 | v5.0.1                        |
| actions/attest-build-provenance | v4                 | v4.2.2                        |
| sigstore/cosign-installer       | v4.1.2             | v4.1.2                        |
| softprops/action-gh-release     | v3                 | v3.0.3                        |

The installer explicitly selects Cosign `v3.1.3`, rather than its older default.
Cosign 3 requires verification bundles for blob signing; the signing and release
steps publish `.sigstore.json` files as described below. The custom JavaScript
release resolver and Node metadata producer have been removed. Actions may use
their own internal runtimes. There is no repository-owned pipeline test framework
or Node/npm toolchain.

SDK selection continues to follow the repository's `global.json`. CI uses Ubuntu;
the release matrix retains all five native RIDs. NuGet packages remain available
as GitHub Release downloads; they are not pushed to NuGet.org.

### Execution

CI and Release default to `contents: read`. Release jobs explicitly add only
their required signing, attestation or release-publication rights. The website
build uses read-only contents access; only its deploy job receives Pages/OIDC
write permissions. The reusable caller grants that upper bound explicitly.

On Ubuntu, CI builds the full solution, including the referenced source generator, in
Release, then runs `dotnet test --solution Spectara.Revela.slnx --no-build -c Release`
with TRX and Cobertura coverage. Reports are uploaded from `TestResults`.
The formatting step sets the MSBuild environment property `Configuration=Release` for
`dotnet format Spectara.Revela.slnx --verify-no-changes --no-restore --verbosity minimal`,
so it uses the existing Release generator output without a separate Debug build.

CI does not package releases, publish Native AOT binaries or run a custom
workflow-contract suite. Package, SDK-consumer and Native AOT execution checks
remain in Release against the actual artifacts. This deliberately detects those
failures later. Windows/macOS-specific unit tests no longer run automatically;
the native release checks cover artifact workflows, not their entire unit suites.

## Release Stages

The publishing chain is `validate -> packages -> build -> sign -> release -> website`.
NuGet.org publication is not configured; adding it requires separate setup and authorization.

1. `validate` reads event/version/ref data through environment variables and
	invokes [validate-release-version.ps1](../../scripts/validate-release-version.ps1).
	Only a completely validated version becomes a workflow output. Tag-push runs
	must be strictly newer than all other release tags by SemVer precedence.
2. `packages` builds the Release solution, runs its full tests, then packs those
	binaries with `--no-build --no-restore`. It tests an isolated consumer of the
	packaged SDK before attesting and uploading the packages as `nupkgs`.
3. `build` publishes and archives Core, Full and Standalone on five native RIDs:
	`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64` and `osx-arm64`. Full bundles
	the packages downloaded from `packages`. Each `dotnet publish` restores its
	required dependencies for the selected RID; no separate solution restore runs
	in this job. For each RID, the shared
	[test-release.ps1](../../scripts/test-release.ps1) suite consumes each actual
	ZIP/TAR archive via `-ArtifactPath`, with its exact `-Version`, `-Variant` and
	`-RuntimeIdentifier`. Core receives `-PackageDirectory ./publish/packages` as
	an external fixture feed. These tests do not restore, build, publish or pack
	the product. Building the isolated SDK consumer is allowed. Archive tests
	precede archive attestation and upload; logs and hashes are uploaded as
	`artifact-tests-<rid>`, including on failure when evidence exists.
4. `sign` checksums and signs the resulting archives and NuGet packages using
	keyless cosign. Manual runs also attest and sign.
5. `release` runs only on tag pushes and creates the GitHub Release. The dependent
	`website` job calls the reusable website workflow with `github.sha` and
	`github.ref_name`. A skipped or failed release never deploys the website.

Full and Core run the modular suite; Standalone tests its Native AOT host and
supplied native companion without plugin management. Core does not ship a usable
theme. See [local artifact testing](../../docs/development.md#test-existing-release-artifacts)
for input preservation, host requirements and the limited Core restore fixture.

### Release Version Policy

The supported syntax remains `X.Y.Z[-alphanumeric[.digits]]`, for example
`1.0.0`, `1.0.0-beta` or `0.0.1-beta.21`. Numeric identifiers must be canonical,
without leading zeros. Hyphenated labels, multiple textual prerelease identifiers,
build metadata, whitespace and shell expressions are rejected. Values must fit
PowerShell's built-in `SemanticVersion` parser; its comparer supplies precedence,
not a shell version sort. Thus `1.0.0` follows `1.0.0-rc.1`, and `beta.10` follows
`beta.2` at the same core version.

A push must reference a lower-case `v`-prefixed tag. The exact current tag is
excluded from the comparison; invalid historical `v`-prefixed tags fail closed
instead of being silently ignored. The validator only lists tags and never
creates or modifies them. Manual dispatch requires an explicit version, but
allows an older or equal valid version for testing without consulting tags.

The validator and deployment guards remain in the real workflows. Their former
synthetic regression suite has been removed along with YAML text contracts.
Use `actionlint` for workflow syntax and review changes to publication conditions
and permissions directly. Linting does not prove SemVer behavior, GitHub permissions
or hosted publication; those are not covered by a replacement test framework.

## Website Publication

Automatic publication is a dependent job in Release, not an event listener in a
second workflow. The local reusable workflow reference uses the caller's revision.
After a successful tag-push publication it receives the exact source commit and
release tag directly. It needs no identity artifact, run-attempt protocol,
JavaScript resolver or secondary `release` event. Ordinary pushes, pull requests
and manual Release rehearsals do not deploy the website.

Both entry paths use the same steps: validate explicit inputs, check that the
selected release is published rather than a draft, check out `source-ref`,
download that exact tag's Linux x64 Standalone archive, substitute download links,
sync the existing homepage source, generate, check Pages markers and deploy.
Release checks and downloads use `gh` already available on the Ubuntu runner;
no additional download Action or custom GitHub API client is needed. The download
selects the explicit tag and exact archive name from `Spectara/Revela`, passing
the tag through a quoted environment variable. There is no `latest` fallback.
Checks or downloads failing stop publication. The published AOT binary runs
without SDK setup or SDK-specific environment variables.

For an emergency content correction, manually dispatch **Deploy Website** with:

- `source-ref`: the branch, tag or commit containing corrected content (default `main`).
- `release-tag`: an existing published tag, including `v`, for example `v0.0.1-beta.21`.

The dispatch branch selects the workflow implementation; `source-ref` selects
content. They need not match. Use trusted refs with content/templates compatible
with the selected binary. If `main` already requires an unreleased generator,
use a content-fix commit based on the published release instead. No new release
or tag is created. This path is not a bypass of generation or deployment checks.

The `github-pages` environment and common `pages` concurrency group remain in
use. A website failure does not roll back a GitHub Release already published;
correct the problem and explicitly retry the failed deployment path. Old runs
retain their old workflow revision; rerunning them does not apply this redesign.

---

## Release Verification Checklist

1. **Authorize the run.** Confirm the source revision and version. After explicit
	approval, manual Release dispatch can exercise `validate`, `packages`, `build`
	and `sign` without creating a release or automatically deploying the website.
	Attestation and signing still have external effects.
2. **Inspect the evidence.** Check all five RID jobs and all three archive variants,
	plus SDK consumer results. Download artifacts from that same run, retaining
	artifact-name directories under a common `artifacts` directory.
3. **Verify checksums and signatures.** Use the layout and identity requirements
	below. Stop on any missing file, failed hash or unexpected signing identity.
4. **Publish only with separate authorization.** A `v<VERSION>` tag push runs a
	new publishing workflow; it does not promote the manual run's bytes unchanged.
	Verify that run's artifacts too. There is no NuGet.org publication step.
5. **Record results.** Capture the run URL/attempt, source commit, version, date,
	hashes and observed checks in the release/PR or task result, linking retained
	workflow evidence. A description of the pipeline is not proof that it passed.

### Checksum Working Directory

The signed manifest contains paths relative to the signing job's `artifacts`
directory, before GitHub Release uploads flatten asset names. For downloaded
workflow artifacts, preserve this layout (other RID/variant directories omitted):

```text
artifacts/
  win-x64-full/revela-win-x64-full.zip
  nupkgs/Spectara.Revela.Sdk.<VERSION>.nupkg
  signatures/
	 SHA256SUMS
	 SHA256SUMS.sigstore.json
	 win-x64-full/revela-win-x64-full.zip.sigstore.json
```

Run GNU `sha256sum` from that root, not from `signatures`:

```bash
cd artifacts
sha256sum --check signatures/SHA256SUMS
```

Every manifest entry must report `OK`. With flat GitHub Release downloads, first
recreate the manifest's relative directories using copies of the corresponding
assets. Do not edit the signed manifest to make paths fit. Download all listed
archives and packages before a complete manifest check.

### Signature Identity

Follow [cosign blob verification](https://docs.sigstore.dev/cosign/verifying/verify/)
using Cosign 3. Verify `SHA256SUMS` and each distributed archive or package with
its matching `.sigstore.json` bundle. It includes the signature, certificate and
verification material. For an artifact at `<relative-path>`, the workflow download
stores the bundle in `signatures/<relative-path>.sigstore.json`.

Keyless verification must constrain both `--certificate-identity-regexp` and
`--certificate-oidc-issuer`; a signature alone is insufficient. For a tag-push
release in `Spectara/Revela`, the anchored identity expression is:

```text
^https://github\.com/Spectara/Revela/\.github/workflows/release\.yml@refs/tags/v<REGEX_ESCAPED_VERSION>$
```

Replace `<REGEX_ESCAPED_VERSION>` with the exact version, escaping regex characters
(for example, `0\.0\.1-beta\.21`). The expected OIDC issuer is exactly
`https://token.actions.githubusercontent.com`. A manual dispatch instead needs
the exact selected ref's identity, such as `refs/heads/main`; do not broaden the
expression to accept arbitrary workflows or refs. These are verification
requirements, not a claim that cosign has been run locally.

For example, from the downloaded workflow artifacts root, after replacing the
version with the release being checked:

```bash
cosign verify-blob \
	--bundle signatures/SHA256SUMS.sigstore.json \
	--certificate-identity-regexp '^https://github\.com/Spectara/Revela/\.github/workflows/release\.yml@refs/tags/v0\.0\.1-beta\.21$' \
	--certificate-oidc-issuer https://token.actions.githubusercontent.com \
	signatures/SHA256SUMS
```

Repeat for each archive/package with its corresponding bundle and payload path.
Older releases using separate `.sig`/`.crt` files keep their original verification
procedure; these new bundle paths apply to releases produced by this workflow.

---

## Helpful Commands

These commands start remote workflows and require explicit permission. Replace
`<SOURCE_REF>` and `<VERSION>` with the approved values; the version has no `v`
prefix. CI defines no `reason` input.

```bash
# Trigger CI manually
gh workflow run ci.yml --ref '<SOURCE_REF>'

# Trigger manual Release validation, including external attestation/signing
gh workflow run release.yml --ref '<SOURCE_REF>' -f version='<VERSION>'

# Emergency content deployment; choose a compatible, already published binary
gh workflow run deploy-website.yml --ref main -f source-ref='<CONTENT_REF>' -f release-tag='v<VERSION>'
```

For deeper details, inspect the workflow files alongside this guide.


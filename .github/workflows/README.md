# GitHub Actions CI/CD Guide

This directory hosts all automation used to build, test, sign, and ship Revela. The sections below summarise the current production workflows and provide a checklist for release verification.

---

## Workflow Summary

| Workflow | File | Triggers | Purpose |
| --- | --- | --- | --- |
| CI | [ci.yml](ci.yml) | Push/PR to `main` or `develop` (subject to path filters), manual dispatch | Three-OS Release build, full-solution tests with coverage, formatting, modular release suite and Native AOT canary. |
| Release | [release.yml](release.yml) | `v*` tag push or manual dispatch with `version` | Validate, test and pack, verify actual platform archives, attest and sign. Only tag pushes create a GitHub Release; NuGet publishing is disabled. |
| Deploy Website | [deploy-website.yml](deploy-website.yml) | Successful tag-push Release run, or manual dispatch | Automatic: use that run's exact release and source commit. Manual: use selected source and latest release, including prereleases. Publish to GitHub Pages at `revela.website`. |
| Dependency Updates | [Dependabot](../dependabot.yml) | Weekly (Monday), automatic | Creates PRs for outdated NuGet packages and GitHub Actions. |

> **Note**
> `code-quality.yml` from early plans was replaced by the consolidated `ci.yml` pipeline.

The verification contract is [ADR 0005](../../docs/decisions/0005-release-artifact-verification.md).
This guide describes the implemented paths, not proof that a hosted run passed.
Remote workflow execution requires explicit permission. In particular, a manual
Release run still performs external attestation and keyless signing; it is not a
side-effect-free local check.

## CI Gates

On Windows, Linux and macOS, CI builds the source generator and full solution in
Release, then runs `dotnet test --solution Spectara.Revela.slnx --no-build -c Release`
with TRX and Cobertura coverage. Reports are uploaded from `TestResults`.
It also builds the source generator in Debug before
`dotnet format Spectara.Revela.slnx --verify-no-changes --no-restore --verbosity minimal`,
because formatting needs the Debug generator output.

Each OS then runs the shared modular Build-mode suite:

```pwsh
./scripts/test-release.ps1 -SkipTests -Version 0.0.0-ci -KeepArtifacts
```

`-SkipTests` avoids repeating the unit tests already run above; it does not skip
the release installation, registration, generation, cleanup, local tool or SDK
consumer checks. The separate Native AOT canary publishes `Cli.Embedded`, checks
its version and generates Showcase, asserting HTML/gallery output and image
variants. It supplements the modular suite.

## Release Stages

The job chain is `validate -> packages -> build -> sign -> release -> publish-nuget`.

1. `validate` resolves and validates the version. Tag-push runs also compare it
	with existing version tags.
2. `packages` builds the Release solution, runs its full tests, then packs those
	binaries with `--no-build --no-restore`. It tests an isolated consumer of the
	packaged SDK before attesting and uploading the packages as `nupkgs`.
3. `build` publishes and archives Core, Full and Standalone on five native RIDs:
	`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64` and `osx-arm64`. Full bundles
	the packages downloaded from `packages`. For each RID, the shared
	[test-release.ps1](../../scripts/test-release.ps1) suite consumes each actual
	ZIP/TAR archive via `-ArtifactPath`, with its exact `-Version`, `-Variant` and
	`-RuntimeIdentifier`. Core receives `-PackageDirectory ./publish/packages` as
	an external fixture feed. These tests do not restore, build, publish or pack
	the product. Building the isolated SDK consumer is allowed. Archive tests
	precede archive attestation and upload; logs and hashes are uploaded as
	`artifact-tests-<rid>`, including on failure when evidence exists.
4. `sign` checksums and signs the resulting archives and NuGet packages using
	keyless cosign. Manual runs also attest and sign.
5. `release` runs only on tag pushes. It creates the GitHub Release and then
	uploads a `release-identity-<attempt>` artifact containing the tag, version, commit,
	run ID and run attempt.
6. `publish-nuget` is disabled by `if: false`. Environment approval or the GitHub
	UI's re-run controls cannot bypass that condition. Publishing requires a
	separately authorized workflow change and NuGet setup before approval in
	the `nuget-org` environment can apply.

Full and Core run the modular suite; Standalone tests its Native AOT host and
supplied native companion without plugin management. Core does not ship a usable
theme. See [local artifact testing](../../docs/development.md#test-existing-release-artifacts)
for input preservation, host requirements and the limited Core restore fixture.

## Website Release Identity

Automatic deployment accepts only a successful Release `workflow_run` whose
original event was `push`. It downloads `release-identity-<attempt>` from that exact run
and validates its run ID, run attempt, commit against `head_sha`, and tag/version
pair. `getReleaseByTag` must return a published, non-draft release for that tag;
`getCommit` for `refs/tags/<tag>` must resolve to the recorded commit. The workflow
then checks out that commit and downloads that tag's Linux x64 Standalone archive.
Download-link substitutions use the same tag. It never falls back to latest for
an automatic deployment: missing metadata, mismatches or a missing release fail
closed.

Attempt-specific artifact names avoid collisions with immutable artifacts from
earlier attempts. The successful attempt must execute the identity producer;
partial reruns that reuse an older attempt's metadata fail closed.

Manual website dispatch retains a separate path: checkout uses the selected
dispatch ref, while the binary comes from the latest available release with
`preRelease: true`. It intentionally permits current content with a different
release binary. A manual Release run does not trigger automatic website deployment.

Older Release runs without identity metadata cannot satisfy the automatic guard.
The website guard must be on the default branch and the identity producer must
be on the Release source ref. A new Release run after both changes land is needed;
re-running an old workflow revision does not add the missing producer. This is
an operational prerequisite, not permission to push a tag or start any workflow.

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
	Verify that run's artifacts too. NuGet publishing remains disabled.
5. **Record results.** Capture the run URL/attempt, source commit, version, date,
	hashes and observed checks. Keep historical R1 reports immutable; record new
	remediation evidence separately. The report owner maintains ADR verification
	status; this guide is not a replacement for that evidence.

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
	 SHA256SUMS.sig
	 SHA256SUMS.crt
	 win-x64-full/revela-win-x64-full.zip.sig
	 win-x64-full/revela-win-x64-full.zip.crt
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
for the installed cosign version. Verify `SHA256SUMS` and each distributed archive
or package with its matching `.sig` and `.crt`. For an artifact at `<relative-path>`,
the workflow download stores these in `signatures/<relative-path>.sig` and `.crt`.

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
```

For deeper details, inspect the workflow files alongside this guide.


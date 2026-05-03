# GitHub Actions CI/CD Guide

This directory hosts all automation used to build, test, sign, and ship Revela. The sections below summarise the current production workflows and provide a checklist for release verification.

---

## Workflow Summary

| Workflow | File | Triggers | Purpose |
| --- | --- | --- | --- |
| CI | [`ci.yml`](ci.yml) | Push/PR to `main` or `develop`, manual dispatch | Multi-OS build (Windows/Linux/macOS), run all tests, pack CLI/plugins/themes for verification. |
| Release | [`release.yml`](release.yml) | `v*` tag push or manual dispatch | Validate version, build binaries + NuGet packages, sign with cosign, create GitHub Release, publish to NuGet.org (with approval). |
| Deploy Website | [`deploy-website.yml`](deploy-website.yml) | After successful Release run, or manual dispatch | Generate `samples/revela-website` against the latest release binary and push to `Spectara/Revela.Website`. Content-only fixes between releases use `workflow_dispatch`. |
| Dependency Updates | [Dependabot](../dependabot.yml) | Weekly (Monday), automatic | Creates PRs for outdated NuGet packages and GitHub Actions. |

> **Note**
> `code-quality.yml` from early plans was replaced by the consolidated `ci.yml` pipeline.

---

## Release Verification Checklist

Follow this checklist whenever cutting a new release:

1. **Dry Run (workflow dispatch)**
	1. Navigate to the _Release_ workflow and trigger `workflow_dispatch` with a pre-release version (e.g., `0.2.0-rc.1`).
	2. Wait for the `validate`, `build`, `plugins`, and `sign` jobs to complete. The `release` job is skipped for manual runs.

2. **Download Artifacts**
	- Use “Download all artifacts” on the workflow run. You should receive:
	  - Platform archives (`revela-<rid>.*`)
	  - NuGet packages (`Spectara.Revela.*.nupkg`)
	  - `SHA256SUMS`, `*.sig`, and `*.crt` files under `signatures/`.

3. **Verify Checksums**
	```bash
	cd signatures
	sha256sum --check SHA256SUMS
	```
	- All files must report `OK`. If anything fails, stop and investigate before continuing.

4. **Verify Signatures (cosign)**
	```bash
	cosign verify-blob \
	  --certificate $(pwd)/SHA256SUMS.crt \
	  --signature   $(pwd)/SHA256SUMS.sig \
	  $(pwd)/SHA256SUMS

	cosign verify-blob \
	  --certificate <path-to-artifact>.crt \
	  --signature   <path-to-artifact>.sig \
	  <path-to-artifact>
	```
	- Repeat for each artifact you intend to distribute (`zip`, `tar.gz`, `nupkg`).

5. **Promote**
	- If the dry run is clean, push the final `vX.Y.Z` git tag to trigger `_Release_` again. This time the `release` job publishes the GitHub Release with all artifacts and signatures.
	- The `publish-nuget` stage runs automatically after the release is created. Approve the deployment in the `nuget-org` environment to push packages to NuGet.org.

6. **Record the Verification**
	- Capture a short summary (workflow run URL + verification date) in the release notes or issue tracker for traceability.

---

## Helpful Commands

```bash
# Trigger CI manually with reason
gh workflow run ci.yml -f reason="release smoke"

# Trigger release dry run (requires GitHub CLI 2.21+)
gh workflow run release.yml -f version=0.2.0-rc.1

# Manually re-run NuGet publish stage (via GitHub UI)
# Navigate to the release workflow run → Re-run job "Publish to NuGet.org"
```

For deeper details, inspect the workflow files alongside this guide.


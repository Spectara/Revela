---
name: create-release
description: Creates a new Revela release by updating CHANGELOG.md, Directory.Build.props version, committing changes, and creating a git tag. Use when the user asks to create a release, prepare a release, bump the version, or tag a new version.
---

# Create Release — Revela Project

Prepare and create a new release for Revela. Follow all steps in order.

## Prerequisites

Before starting, verify:
- All tests pass (`dotnet test`)
- No uncommitted changes (`git status`)
- `[Unreleased]` section in CHANGELOG.md has content

## Step 1: Determine Version

Ask the user for the version number if not provided. Follow [Semantic Versioning](https://semver.org/):
- **Pre-release**: `0.0.1-beta.15`, `0.0.1-rc.1`
- **Stable**: `1.0.0`, `1.0.1`, `1.1.0`

Check current version in `Directory.Build.props` (`<VersionPrefix>`) and latest tag in CHANGELOG.md.
The new version must be higher than the previous one.

## Step 2: Update CHANGELOG.md

The changelog follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

1. **Replace** `## [Unreleased]` content with the new version and today's date:
   ```markdown
   ## [Unreleased]

   ## [X.Y.Z-suffix] - YYYY-MM-DD
   ```

2. **Add comparison link** at the bottom of the file:
   ```markdown
   [Unreleased]: https://github.com/spectara/revela/compare/vX.Y.Z-suffix...HEAD
   [X.Y.Z-suffix]: https://github.com/spectara/revela/compare/vPREVIOUS...vX.Y.Z-suffix
   ```
   Update the existing `[Unreleased]` link to point to the new tag.

3. **Verify** the `[Unreleased]` section is now empty (ready for next development cycle).

## Step 3: Update Directory.Build.props

Update `<VersionPrefix>` in the root `Directory.Build.props`:
```xml
<VersionPrefix>X.Y.Z</VersionPrefix>
```

**Important:** `VersionPrefix` does NOT include pre-release suffixes like `-beta.15`.
- For `0.0.1-beta.15`: VersionPrefix stays `0.0.1` (suffix handled by VersionSuffix/CI)
- For `1.0.0`: VersionPrefix = `1.0.0`

Wait — actually check the current convention: if the project uses VersionPrefix for the FULL version including pre-release, follow that pattern. Read the current value first.

## Step 4: Commit

Create a single commit with both file changes:
```
git add CHANGELOG.md Directory.Build.props
git commit -m "Release vX.Y.Z-suffix"
```

## Step 5: Create Tag

```
git tag vX.Y.Z-suffix
```

**Do NOT push automatically.** Tell the user:
> Release `vX.Y.Z-suffix` is ready. To publish:
> ```
> git push origin main --tags
> ```
> This triggers the release workflow which builds all platforms, signs artifacts, and creates the GitHub Release.

## Workflow Reference

The release pipeline (`.github/workflows/release.yml`) handles everything after the tag push:
1. **Validate** — Version format and ordering
2. **Packages** — Build NuGet packages (plugins + themes + SDK)
3. **Build** — Native executables for 5 platforms (win-x64, linux-x64, linux-arm64, osx-x64, osx-arm64)
4. **Sign** — Keyless cosign signatures + SHA256SUMS
5. **Release** — Create GitHub Release with all artifacts

Additional workflows triggered after release:
- `publish-nuget.yml` — Publish packages to NuGet.org
- `publish-github-packages.yml` — Publish to GitHub Packages
- `deploy-website.yml` — Deploy revela.website

## Checklist

Before telling the user to push, verify:
- [ ] CHANGELOG.md: `[Unreleased]` section is empty
- [ ] CHANGELOG.md: New version section has correct date (today)
- [ ] CHANGELOG.md: Comparison links at bottom are correct
- [ ] Directory.Build.props: VersionPrefix is updated
- [ ] Git: Single commit with message "Release vX.Y.Z-suffix"
- [ ] Git: Tag `vX.Y.Z-suffix` created
- [ ] Git: Tag matches CHANGELOG version exactly

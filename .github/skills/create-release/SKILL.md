---
name: create-release
description: Creates a new Revela release by updating CHANGELOG.md, committing changes, and creating a git tag. Use when the user asks to create a release, prepare a release, bump the version, or tag a new version.
argument-hint: "[version e.g. 0.5.0 or 0.5.0-beta.1]"
---

# Create Release — Revela Project

Prepare and create a new release for Revela. Follow all steps in order.
**Do NOT commit or push automatically** — present changes and let the user decide.

## Prerequisites

Before starting, verify:
- All tests pass (`dotnet test`)
- No uncommitted changes (`git status`)
- `[Unreleased]` section in CHANGELOG.md has content

## Step 1: Determine Version

Ask the user for the version number if not provided. Follow [Semantic Versioning](https://semver.org/):
- **Pre-release**: `0.0.1-beta.15`, `0.0.1-rc.1`
- **Stable**: `1.0.0`, `1.0.1`, `1.1.0`

Check the latest version in CHANGELOG.md. The new version must be higher than the previous one.

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

## Step 3: Present Changes

Show the user what was changed and suggest next steps:

```
git add CHANGELOG.md
git commit -m "Release vX.Y.Z-suffix"
git tag vX.Y.Z-suffix
git push origin main --no-follow-tags
git push origin refs/tags/vX.Y.Z-suffix
```

**Do NOT execute these commands automatically.** Commit, tag and push each need
explicit approval. Push only the approved tag, never all local tags.

**Note:** `Directory.Build.props` does NOT need updating — the release workflow passes the version from the git tag via `-p:Version=` and `-p:PackageVersion=` build parameters.

## Workflow Reference

Use the [CI/CD guide](../../workflows/README.md#release-stages) for the current
platform matrix, artifact checks, signing and website publication. A tag push
publishes a release and calls the website workflow; manual Release rehearsals
do not publish or deploy but still attest/sign. NuGet packages are GitHub Release
assets; publishing to NuGet.org is not configured and requires separate setup
and authorization.

## Checklist

Before presenting to the user, verify:
- [ ] CHANGELOG.md: `[Unreleased]` section is empty
- [ ] CHANGELOG.md: New version section has correct date (today)
- [ ] CHANGELOG.md: Comparison links at bottom are correct
- [ ] Version is higher than the previous release

---
mode: agent
description: "Generate CHANGELOG.md entries from git commits since the last release tag"
---

# Release Notes Generator

Generate a new `CHANGELOG.md` section from commits since the last release tag.

## Inputs

1. **Target version** — semver (e.g. `0.5.0`). Ask the user if not supplied. Suggest based on commit types:
   - Breaking changes → major bump
   - New features → minor bump
   - Fixes only → patch bump
2. **Release date** — default to today.

## Steps

1. **Find last tag** — `git describe --tags --abbrev=0` (or `git tag --sort=-v:refname | Select-Object -First 1`)
2. **List commits** — `git log <last-tag>..HEAD --pretty=format:"%h %s"`
3. **Group commits by Conventional Commit type:**
   - `feat:` → **Added**
   - `fix:` → **Fixed**
   - `perf:` → **Performance**
   - `refactor:` → **Changed** (only if user-visible)
   - `docs:` → **Documentation** (skip unless major)
   - `chore:`, `style:`, `test:` → skip (internal noise)
   - `BREAKING CHANGE:` (in body) or `!:` → **⚠️ Breaking Changes** (top of section)
4. **Rewrite for users** — commit messages are for developers; CHANGELOG is for users.
   - "fix: NRE in PathResolver when source is absolute" → "Fixed crash when using absolute source paths"
   - "feat: add --watch flag to serve" → "`serve` command now supports `--watch` for auto-rebuild"
5. **Insert at top of `CHANGELOG.md`** — preserve existing entries.

## Format (Keep a Changelog 1.1.0)

```markdown
## [${version}] — ${date}

### ⚠️ Breaking Changes
- ...

### Added
- ...

### Changed
- ...

### Fixed
- ...

### Performance
- ...

### Documentation
- ...
```

## After Generating

- Show the new section to the user for review.
- **Do NOT commit, do NOT tag.** Tagging happens via the `create-release` skill, which the user invokes explicitly.
- Suggest: "Review the entries above. When happy, run the `create-release` skill to bump version, commit, and tag."

## Hard Constraints

- Don't fabricate entries — every line maps to a real commit (cite the short hash if helpful).
- Skip purely internal commits (test additions, CI tweaks, dependency bumps unless security-critical).
- Highlight security fixes prominently — prefix with `🔒` in the entry.
- Don't auto-commit. The user controls when/how releases happen.

---
name: commit-changes
description: Creates git commits with comprehensive, Conventional Commits-formatted messages. Use when the user asks to commit, stage changes, save progress, or create a commit message. Ensures consistent, descriptive commit messages following project conventions.
---

# Commit Changes — Revela Project

Create git commits with comprehensive messages following [Conventional Commits](https://www.conventionalcommits.org/) and project conventions.

**Do NOT commit automatically.** Always show the staged changes and proposed message first. Let the user confirm or adjust before executing.

## Step 1: Review Changes

Run `git diff --stat` (unstaged) and `git diff --cached --stat` (staged) to understand what changed.
If nothing is staged, suggest what to stage based on logical grouping.

## Step 2: Compose Commit Message

### Format

```
<type>(<scope>): <subject>

<body>
```

### Subject Line (required)

- **Type** — one of:
  | Type | Use for |
  |------|---------|
  | `feat` | New feature, new capability |
  | `fix` | Bug fix |
  | `docs` | Documentation only (copilot-instructions, website, README, skills) |
  | `refactor` | Code restructuring without behavior change |
  | `perf` | Performance improvement |
  | `test` | Adding or fixing tests |
  | `build` | Build system, CI/CD, dependencies, scripts |
  | `chore` | Maintenance tasks that don't fit elsewhere |

- **Scope** (optional) — affected area in parentheses:
  `plugin`, `theme`, `images`, `lqip`, `cli`, `config`, `website`, `sdk`, etc.

- **Subject** — imperative mood, lowercase, no period, max 72 chars
  - Good: `add EXIF extraction for AVIF files`
  - Bad: `Added EXIF extraction for AVIF files.`

### Body (required for non-trivial changes)

- Blank line after subject
- Explain **what** changed and **why** (not how — the diff shows how)
- Use bullet points (`-`) for multiple changes
- Group related changes under headings if the commit covers multiple areas
- Wrap lines at 72 characters
- Reference issue numbers where applicable: `Fixes #42`

### Examples

**Simple change:**
```
fix(serve): use correct default port 8080 in help text
```

**Feature with body:**
```
feat(images): switch from Resize() to ThumbnailImage()

Based on libvips maintainer recommendation (libvips/libvips#4588).
ThumbnailImage() properly handles alpha premultiplication for
transparent PNGs, which Resize() does not.
```

**Multi-area change with grouped body:**
```
docs: comprehensive website and documentation review

User Journey (00):
- Fix serve port: 5000 → 8080 (matches ServeConfig default)
- Fix command: revela generate → revela generate all

Configuration (07):
- Fix image config: remove non-existent 'formats' wrapper
- Use correct flat properties: webp, jpg, avif under generate.images

Deployment (09):
- Fix config section: site.basepath → project.basePath
```

## Step 3: Present to User

Show the proposed commit:

1. Files to be staged (if not yet staged)
2. The full commit message
3. The git commands to execute

**Wait for user confirmation before running any git commands.**

## Rules

- **One logical change per commit** — don't mix unrelated changes
- **Never commit generated files** (artifacts/, playground/standalone-*)
- **Never commit secrets** or user-specific config
- Check `.gitignore` is respected
- If changes span multiple logical units, suggest splitting into separate commits

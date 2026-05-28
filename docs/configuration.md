# Configuration files

Revela uses JSON for all its configuration. Across the toolchain we accept a
slightly relaxed dialect — commonly called **JSONC** — and you'll see it used
in our sample projects.

## JSONC: what's allowed

In every Revela-owned JSON file you can use:

- **Line comments**: `// ...`
- **Block comments**: `/* ... */`
- **Trailing commas** at the end of objects and arrays

The following files are read with this relaxed parser:

| File | Scope | Edited by |
|------|-------|-----------|
| `project.json` | per project | end user |
| `site.json` | per project | end user |
| `theme.json` | per theme | theme author |
| `images.json` | per theme | theme author |
| `revela.json` | global (`%APPDATA%/Revela/`) | end user |

Standard JSON is of course still valid — JSONC is a strict superset.

### Editor support

VS Code treats `*.json` as JSONC throughout this repo (see `.vscode/settings.json`).
If you author Revela projects in another editor, associate `project.json` /
`site.json` / `theme.json` with the JSONC language to avoid spurious lint
warnings on `//` comments.

## Files that Revela writes back

Some commands modify your configuration files in place — for example:

- `revela plugins add <id>` / `revela plugins remove <id>` → `project.json`
- `revela config init` / wizard flows → `project.json`, `site.json`
- internal CLI state changes → global `revela.json`

When Revela writes a file back it **does not preserve comments or your original
formatting**. The file is reserialised as pretty-printed JSON (2-space indent,
no comments, no trailing commas). This matches how comparable tools behave
(`npm`, `dotnet user-secrets`, `dotnet add package` for `global.json`, …).

If a file is important to you in its current shape — for instance because you
keep meaningful comments next to specific keys — prefer editing it manually
rather than letting a CLI command rewrite it.

## Environment variables

Configuration can also be supplied via environment variables using the
`SPECTARA__REVELA__` prefix. See `docs/setup.md` for the full chain and
merge order.

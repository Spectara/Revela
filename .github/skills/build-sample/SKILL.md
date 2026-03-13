---
name: build-sample
description: Builds a sample project (revela-website, showcase, onedrive) using the local CLI. Use when the user wants to build a sample, generate a site, test output, preview the website, or regenerate pages/images.
---

# Build Sample Project — Revela Project

Build one of the sample projects using the local development CLI.

## Available Samples

| Sample | Path | Description |
|--------|------|-------------|
| `revela-website` | `samples/revela-website` | The revela.website — docs, showcase, FAQ |
| `showcase` | `samples/showcase` | Photography portfolio demo |
| `onedrive` | `samples/onedrive` | OneDrive source plugin demo |

## Commands

```powershell
# Full build (scan → statistics → pages → images)
dotnet run --project src/Cli -- -p samples/<name> generate all

# Individual steps
dotnet run --project src/Cli -- -p samples/<name> generate scan
dotnet run --project src/Cli -- -p samples/<name> generate pages
dotnet run --project src/Cli -- -p samples/<name> generate images

# Clean and rebuild
dotnet run --project src/Cli -- -p samples/<name> clean all
dotnet run --project src/Cli -- -p samples/<name> generate all
```

## Default Sample

If the user doesn't specify which sample, use **revela-website** — it's the most actively developed.

## Preview

The user uses **VS Code Live Server** for preview. Do NOT run `Start-Process` to open HTML files.
Output is in `samples/<name>/output/`.

## Important

- Always run from the **repository root** (`d:\Work\GitHub\Revela`)
- The `-p` flag accepts a relative path to the sample project
- If the build fails with "Not in a Revela project directory", check that the path is correct
- The `--no-build` flag skips dotnet compilation (use after first build for speed)

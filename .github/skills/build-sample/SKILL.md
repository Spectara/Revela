---
name: build-sample
description: Builds a sample project (revela-website, showcase, onedrive) using the local CLI. Use when the user wants to build a sample, generate a site, test output, preview the website, or regenerate pages/images.
argument-hint: "[sample-name] [generate|clean] [step]"
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
# From the sample project directory
cd samples/<name>
dotnet run --project ../../src/Cli.Embedded -- generate all

# Individual steps
cd samples/<name>
dotnet run --project ../../src/Cli.Embedded -- generate scan
dotnet run --project ../../src/Cli.Embedded -- generate pages
dotnet run --project ../../src/Cli.Embedded -- generate images

# Clean and rebuild
cd samples/<name>
dotnet run --project ../../src/Cli.Embedded -- clean all
dotnet run --project ../../src/Cli.Embedded -- generate all
```

## Default Sample

If the user doesn't specify which sample, use **revela-website** — it's the most actively developed.

## Preview

The user uses **VS Code Live Server** for preview. Do NOT run `Start-Process` to open HTML files.
Output is in `samples/<name>/output/`.

## Important

- Run the command from the sample project directory (`samples/<name>`) — the CLI resolves the project from the current working directory.
- The current CLI build does not support a `-p` project-path flag.
- If the build fails with "Not in a Revela project directory", check that you are inside the sample project folder.
- The `--no-build` flag skips dotnet compilation (use after first build for speed).

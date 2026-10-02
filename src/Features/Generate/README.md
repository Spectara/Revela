# Spectara.Revela.Features.Generate

Built-in site generation feature of Revela — scans content, renders pages, and processes images. Compiled into the host; not a plugin package.

## Features

- **Content Scanning** — Discovers galleries, pages (`_index.revela`) and images
- **Page Rendering** — Generates HTML pages using Scriban templates
- **Image Processing** — Resizes and converts images using NetVips
- **Sitemap Generation** — Creates sitemap.xml for SEO
- **Content Images** — Renders responsive images in markdown content
- **Image Filtering** — DSL for filtering and sorting images

## Commands

### Generate
- `revela generate all` — Run the full generation pipeline
- `revela generate scan` — Scan source directory and build manifest
- `revela generate pages` — Render HTML pages from manifest
- `revela generate images` — Process and resize images

### Clean
- `revela clean all` — Run every clean step
- `revela clean output` — Delete the output directory
- `revela clean cache` — Delete the cache directory
- `revela clean images` — Smart cleanup of unused image variants

### Check
- `revela check` / `revela check all` — Run every check (exit code 2 on errors); `revela check <name>` runs one

### Config
- `revela config image` — Configure image formats and quality (sizes come from the theme)
- `revela config sorting` — Configure gallery and image sorting
- `revela config paths` — Set the source and output paths

### Create
- `revela create page` — Create a page from a template (required template options are enforced)

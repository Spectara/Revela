# Spectara.Revela.Features.Generate

Core site generation plugin for Revela — scans content, renders pages, and processes images.

## Features

- **Content Scanning** — Discovers galleries, images, and markdown content
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
- `revela clean output` — Delete the output directory
- `revela clean cache` — Delete the cache directory
- `revela clean images` — Smart cleanup of unused image variants

### Config
- `revela config images` — Configure image sizes and quality
- `revela config sorting` — Configure gallery sorting rules
- `revela config paths` — Show configured source/output paths

### Create
- `revela create page` — Create a new gallery or text page

# Revela Website

Source project for [revela.website](https://revela.website) — the official project homepage.

This is **not** a typical photography portfolio but uses Revela's page and content features to build the documentation site. It also serves as a **real-world example of theme customization** — showing how to extend and override the built-in Lumina theme without forking it.

## Theme Customization Example

The `themes/Lumina/` folder demonstrates Revela's **theme override system**: any file placed here takes priority over the same path in the built-in theme package.

```
themes/Lumina/
├── Assets/
│   ├── website.css              # Custom styles for the documentation site
│   ├── prism.css                # Syntax highlighting theme
│   └── prism.js                 # Syntax highlighting library
├── Body/
│   ├── home.revela              # Custom homepage layout (replaces default)
│   └── docs.revela              # Custom documentation page layout (new)
└── Partials/
    ├── Favicon.revela           # Custom favicon partial (replaces default)
    └── HeaderNavigation.revela  # Custom navigation (replaces default)
```

**What this demonstrates:**

- **Override body templates** — `home.revela` replaces the default homepage with a landing page design
- **Add new page types** — `docs.revela` is a layout for documentation pages (not in the base theme)
- **Override partials** — `Favicon.revela` and `HeaderNavigation.revela` replace specific UI components
- **Add custom assets** — CSS/JS files referenced via `site.json` stylesheets/scripts arrays
- **No theme fork needed** — Only the changed files are in the project, the rest comes from the theme package

## Usage

```bash
cd samples/revela-website
revela generate all
revela serve
```

## Deployment

Deployed automatically via GitHub Actions ([deploy-website.yml](../../.github/workflows/deploy-website.yml)) when a new release is published.

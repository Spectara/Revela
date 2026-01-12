# Statische Dateien

## Übersicht

Statische Dateien werden direkt ins Output-Verzeichnis kopiert, ohne jegliche Verarbeitung. Typische Anwendungsfälle:

- **Favicons** - Browser-Icons in verschiedenen Formaten
- **robots.txt** - Anweisungen für Suchmaschinen-Crawler
- **CNAME** - Custom Domain für GitHub Pages
- **.nojekyll** - Jekyll-Verarbeitung auf GitHub Pages deaktivieren
- **sitemap.xml** - Vorab erstellte Sitemaps
- **ads.txt** - Werbe-Autorisierung
- **security.txt** - Sicherheits-Kontaktinformationen

## Konvention

Platziere statische Dateien im `source/_static/` Ordner. Sie werden 1:1 ins Output-Verzeichnis kopiert.

```
source/
├── _static/                    # Ordner für statische Dateien
│   ├── favicon/               # Favicon-Dateien
│   │   ├── favicon.ico
│   │   ├── favicon.svg
│   │   ├── favicon-96x96.png
│   │   ├── apple-touch-icon.png
│   │   └── site.webmanifest
│   ├── CNAME                  # GitHub Pages Custom Domain
│   ├── .nojekyll              # Jekyll auf GitHub Pages deaktivieren
│   └── robots.txt             # Suchmaschinen-Anweisungen
├── _index.revela
└── gallery/
    └── ...
```

**Output:**

```
output/
├── favicon/
│   ├── favicon.ico
│   ├── favicon.svg
│   ├── favicon-96x96.png
│   ├── apple-touch-icon.png
│   └── site.webmanifest
├── CNAME
├── .nojekyll
├── robots.txt
├── index.html
└── gallery/
    └── ...
```

## Favicon einrichten

### Schritt 1: Favicons generieren

Nutze einen Favicon-Generator wie [realfavicongenerator.net](https://realfavicongenerator.net) oder [favicon.io](https://favicon.io):

1. Logo/Bild hochladen
2. Plattform-spezifische Icons konfigurieren
3. Generiertes Paket herunterladen
4. Dateien nach `source/_static/favicon/` kopieren

### Schritt 2: Favicon-Partial erstellen

Erstelle ein Theme-Override um das Favicon-HTML in deine Seiten einzubinden.

**Datei:** `themes/Lumina/Partials/Favicon.revela`

```html
    <link rel="icon" type="image/png" href="/favicon/favicon-96x96.png" sizes="96x96" />
    <link rel="icon" type="image/svg+xml" href="/favicon/favicon.svg" />
    <link rel="shortcut icon" href="/favicon/favicon.ico" />
    <link rel="apple-touch-icon" sizes="180x180" href="/favicon/apple-touch-icon.png" />
    <meta name="apple-mobile-web-app-title" content="Dein Seitenname" />
    <link rel="manifest" href="/favicon/site.webmanifest" />
```

> **Hinweis:** Kopiere das exakte HTML von deinem Favicon-Generator. Das Format variiert je nachdem welche Icons du generiert hast.

### Schritt 3: Seite generieren

```bash
revela generate pages
```

Die Favicon-Dateien werden nach `output/favicon/` kopiert und das HTML wird im `<head>` jeder Seite eingebunden.

## Projektstruktur

```
my-project/
├── project.json
├── site.json
├── source/
│   ├── _static/              # ← Statische Dateien hier
│   │   └── favicon/
│   └── ...
├── themes/
│   └── Lumina/
│       └── Partials/
│           └── Favicon.revela  # ← Favicon HTML hier
└── output/
    ├── favicon/              # ← Hierhin kopiert
    └── ...
```

## Funktionsweise

1. **Scannen:** Ordner die mit `_` beginnen werden vom Content-Scanner übersprungen (keine Galerien erstellt)
2. **Kopieren:** Nach dem Seiten-Rendering werden alle Dateien aus `source/_static/` nach `output/` kopiert
3. **Struktur:** Verzeichnisstruktur bleibt erhalten (`_static/favicon/` → `output/favicon/`)
4. **Überschreiben:** Existierende Dateien im Output werden stillschweigend überschrieben

## Theme-Anpassung

Das Standard-Lumina-Theme enthält ein leeres `Favicon.revela` Partial. Um Favicons hinzuzufügen:

1. Override-Verzeichnis erstellen: `themes/Lumina/Partials/`
2. `Favicon.revela` mit deinem Favicon-HTML erstellen
3. Das Theme verwendet automatisch dein Override

Dieses Pattern hält das Theme sauber und ermöglicht volle Anpassung.

## Häufige statische Dateien

### robots.txt

```
User-agent: *
Allow: /

Sitemap: https://example.com/sitemap.xml
```

### CNAME (GitHub Pages)

```
example.com
```

### .nojekyll (GitHub Pages)

Leere Datei - einfach erstellen, kein Inhalt nötig.

## Best Practices

- **Favicon-Ordner:** Alle Favicon-Dateien in `_static/favicon/` für bessere Organisation
- **Root-Dateien:** Dateien die im Root sein müssen (CNAME, robots.txt) direkt in `_static/`
- **Keine Verarbeitung:** Statische Dateien werden 1:1 kopiert, keine Minifizierung oder Optimierung
- **Versionskontrolle:** Statische Dateien ins Repository committen
- **Generierte Dateien:** Bei Favicon-Generatoren die Ausgabe für spätere Updates aufbewahren

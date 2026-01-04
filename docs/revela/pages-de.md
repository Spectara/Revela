# Seiten erstellen

## Überblick

Revela bietet Befehle zum schnellen Erstellen neuer Seiten. Du kannst:
- **Galerie-Seiten** erstellen (Foto-Sammlungen mit optionalem Text)
- **Text-Seiten** erstellen (About, Kontakt, Impressum, etc.)
- **Statistik-Seiten** erstellen (EXIF-Auswertungen deiner Fotos)

## Verfügbare Seitentypen

| Typ | Beschreibung | Template |
|-----|--------------|----------|
| `gallery` | Fotogalerie mit Bildraster | Standard (body/gallery) |
| `text` | Reine Textseite ohne Bilder | page |
| `statistics` | EXIF-Statistiken | statistics/overview |

## Galerie-Seiten

Erstelle eine neue Galerie für deine Fotos:

```bash
# Einfache Galerie
revela create page gallery vacation --title "Sommerurlaub 2024"

# Mit Beschreibung und Sortierung
revela create page gallery best-shots --title "Highlights" \
    --description "Meine besten Aufnahmen" \
    --sort "exif.raw.Rating:desc"

# Versteckte Galerie (nicht in Navigation)
revela create page gallery drafts --title "Entwürfe" --hidden

# Mit eigenem URL-Segment
revela create page gallery 2024-12-weihnachten --title "Weihnachten" --slug "christmas"
```

### Optionen

| Option | Alias | Beschreibung | Standard |
|--------|-------|--------------|----------|
| `--title` | `-t` | Seitentitel | "Gallery" |
| `--description` | `-d` | Beschreibung (für SEO) | "" |
| `--sort` | `-s` | Sortierung überschreiben | (global) |
| `--hidden` | - | Aus Navigation ausblenden | false |
| `--slug` | - | Eigenes URL-Segment | (Ordnername) |

### Sortier-Optionen

Die `--sort` Option unterstützt alle Felder aus der [Sortierung](sorting-de.md):

```bash
--sort "dateTaken:asc"        # Älteste zuerst
--sort "dateTaken:desc"       # Neueste zuerst
--sort "filename:asc"         # A → Z
--sort "exif.raw.Rating:desc" # Beste Bewertung zuerst
--sort "exif.focalLength:asc" # Weitwinkel → Tele
```

### Generierte Datei

```toml
+++
title = "Sommerurlaub 2024"
description = ""
+++
Add an optional introduction here.

This text appears above the image gallery.
```

## Text-Seiten

Erstelle Seiten ohne Bildergalerie (About, Kontakt, Impressum):

```bash
# About-Seite
revela create page text about --title "Über mich" \
    --description "Erfahre mehr über mich und meine Fotografie"

# Kontakt-Seite
revela create page text contact --title "Kontakt"

# Impressum (versteckt)
revela create page text imprint --title "Impressum" --hidden
```

### Optionen

| Option | Alias | Beschreibung | Standard |
|--------|-------|--------------|----------|
| `--title` | `-t` | Seitentitel | "Page" |
| `--description` | `-d` | Beschreibung (für SEO) | "" |
| `--hidden` | - | Aus Navigation ausblenden | false |
| `--slug` | - | Eigenes URL-Segment | (Ordnername) |

### Generierte Datei

```toml
+++
title = "Über mich"
description = "Erfahre mehr über mich und meine Fotografie"
template = "page"
+++
Write your content here using **Markdown**.

## Example Heading

- List item one
- List item two

*Edit this file to add your own content.*
```

## Statistik-Seiten

Erstelle eine Seite mit EXIF-Statistiken deiner Foto-Sammlung:

```bash
revela create page statistics stats --title "Foto-Statistiken" \
    --description "Auswertung meiner Kamera- und Objektiv-Nutzung"
```

> **Hinweis:** Das Statistics-Plugin muss installiert sein (`revela plugins install Statistics`).

### Generierte Datei

```toml
+++
title = "Foto-Statistiken"
description = "Auswertung meiner Kamera- und Objektiv-Nutzung"
template = "statistics/overview"
+++
```

## Interaktiver Modus

Alle Seitentypen unterstützen einen interaktiven Modus. Starte ohne Pfad-Argument:

```bash
revela create page gallery
revela create page text
revela create page statistics
```

Der Wizard führt dich durch alle Optionen:

1. **Pfad eingeben** - Relativer Pfad zu `source/`
2. **Titel** - Seitentitel eingeben
3. **Beschreibung** - Optionale Beschreibung
4. **Sortierung** (nur gallery) - Aus Vorlagen wählen oder eigene eingeben
5. **Versteckt** - Aus Navigation ausblenden?
6. **Slug** - Eigenes URL-Segment
7. **Vorschau** - Generierte Datei anzeigen
8. **Bestätigung** - Datei erstellen?

## Frontmatter-Felder

### Alle Seitentypen

| Feld | Beschreibung |
|------|--------------|
| `title` | Seitentitel (in Navigation und `<title>`) |
| `description` | SEO-Beschreibung |
| `hidden` | `true` = nicht in Navigation (aber per URL erreichbar) |
| `slug` | Überschreibt URL-Segment (nur letztes Segment) |
| `template` | Body-Template (Standard: `gallery` oder `page`) |

### Nur Galerie-Seiten

| Feld | Beschreibung |
|------|--------------|
| `sort` | Bild-Sortierung überschreiben (z.B. `dateTaken:asc`) |

### Nur Statistik-Seiten

| Feld | Beschreibung |
|------|--------------|
| `template` | Immer `statistics/overview` |

## Ordnerstruktur

Seiten werden immer im `source/` Verzeichnis erstellt:

```
source/
├── vacation/
│   └── _index.revela      ← revela create page gallery vacation
├── about/
│   └── _index.revela      ← revela create page text about
└── stats/
    └── _index.revela      ← revela create page statistics stats
```

Verschachtelte Pfade werden unterstützt:

```bash
revela create page gallery "2024/summer/italy" --title "Italien 2024"
# Erstellt: source/2024/summer/italy/_index.revela
```

## Tipps

### Markdown-Body bearbeiten

Nach dem Erstellen kannst du den Markdown-Teil unter `+++` frei bearbeiten:

```toml
+++
title = "Über mich"
template = "page"
+++
## Willkommen!

Ich bin Fotograf aus Berlin und spezialisiert auf **Landschafts-** und **Architekturfotografie**.

### Kontakt

- Email: foto@example.com
- Instagram: @meinaccount

![Portrait](portrait.jpg)
```

### Bilder in Text-Seiten

Auch Text-Seiten können Bilder enthalten - sie werden nur nicht als Galerie angezeigt:

```markdown
![Mein Setup](setup.jpg)
```

### Navigation und Reihenfolge

Die Reihenfolge in der Navigation wird durch Ordnernamen bestimmt:

```
source/
├── 01 Galerien/
│   ├── 01 Landschaft/
│   └── 02 Portrait/
├── 02 About/
└── 03 Kontakt/
```

Verwende Nummern-Präfixe für gewünschte Sortierung.

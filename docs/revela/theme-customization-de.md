# Theme Anpassung

## Überblick

Revela ermöglicht die Anpassung von Themes, ohne die Original-Dateien zu verändern. Du kannst:
- **Ein Theme extrahieren** in dein Projekt für vollständige Anpassung
- **Einzelne Dateien überschreiben** (Templates, Assets, Konfiguration)
- **Visuelles Design anpassen** durch Editieren der CSS Custom Properties im Theme-Stylesheet

## Wie es funktioniert

Beim Generieren deiner Seite löst Revela Themes mit dieser Priorität auf:

1. **Lokales Theme** (`themes/<name>/`) - Höchste Priorität
2. **Installierte Plugins** (NuGet-Pakete)
3. **Standard-Theme** (Lumina)

Das bedeutet: Wenn du einen `themes/Lumina/` Ordner in deinem Projekt hast, hat dieser Vorrang vor dem installierten Lumina-Theme.

## Theme extrahieren

### Vollständige Extraktion

Extrahiere ein komplettes Theme für vollständige Anpassung:

```bash
# Extrahieren nach themes/Lumina/
revela theme extract Lumina

# Extrahieren mit eigenem Namen
revela theme extract Lumina MeinTheme
```

### Interaktiver Modus

Starte ohne Argumente für geführte Extraktion:

```bash
revela theme extract
```

Dies zeigt:
1. **Theme-Auswahl** - Wähle aus verfügbaren Themes
2. **Extraktionsmodus** - Komplettes Theme oder einzelne Dateien
3. **Datei-Auswahl** - Multi-Select nach Kategorie (bei selektiver Extraktion)

### Selektive Extraktion

Extrahiere nur bestimmte Dateien:

```bash
# Einzelne Datei extrahieren
revela theme extract Lumina --file layout.revela

# Mehrere Dateien extrahieren
revela theme extract Lumina --file layout.revela --file Assets/styles.css

# Kompletten Ordner extrahieren
revela theme extract Lumina --file Assets/

# Nur Konfiguration extrahieren
revela theme extract Lumina --file theme.json --file Configuration/images.json
```

## Theme-Struktur

Nach der Extraktion sieht dein Theme-Ordner so aus:

```
dein-projekt/
├── themes/
│   └── Lumina/                    # Dein angepasstes Theme
│       ├── manifest.json          # Theme-Manifest (Name, Version, Templates)
│       ├── layout.revela          # Haupt-Layout-Template
│       ├── Assets/                # CSS, JS, Schriften, Bilder
│       │   ├── styles.css
│       │   └── scripts.js
│       ├── Body/                  # Body-Templates
│       │   ├── Gallery.revela
│       │   └── Page.revela
│       ├── Partials/              # Partial-Templates
│       │   ├── ContentImage.revela  # Pflicht: Markdown-Bild-Rendering
│       │   ├── Navigation.revela
│       │   └── Image.revela
│       └── Configuration/         # Theme-Konfiguration
│           ├── site.json
│           └── images.json
├── source/                        # Deine Fotos
├── project.json
└── site.json
```

## Pflicht-Templates

Jedes Theme **muss** `Partials/ContentImage.revela` enthalten. Dieses Template rendert Bilder
aus dem Markdown-Body (`![alt](pfad)` Syntax).

### Template-Variablen

| Variable | Typ | Beschreibung |
|----------|-----|--------------|
| `image` | Image | Bild-Objekt (url, width, height, sizes, placeholder, exif) |
| `alt` | string | Alt-Text aus dem Markdown |
| `classes` | string[] | CSS-Klassen aus `{.class}` Syntax |
| `image_basepath` | string | Basispfad zu den Bild-Varianten |
| `image_formats` | string[] | Aktive Formate (z.B. `["avif", "webp", "jpg"]`) |

### Minimales Beispiel

```scriban
{{~ if !image.sizes || image.sizes.size == 0; ret; end ~}}
<picture class="content-image{{ for cls in classes }} {{ cls }}{{ end }}">
{{~ for format in image_formats ~}}
  <source type="image/{{ format }}" srcset="
    {{~ for size in image.sizes ~}}
    {{ image_basepath }}{{ image.url }}/{{ size }}.{{ format }} {{ size }}w{{ if !for.last }},{{ end }}
    {{~ end ~}}">
{{~ end ~}}
  <img src="{{ image_basepath }}{{ image.url }}/{{ image.sizes | array.last }}.jpg"
       alt="{{ alt }}" loading="lazy" decoding="async">
</picture>
```

### Mit Lightbox

Themes können Lightbox-Funktionalität hinzufügen:

```scriban
{{~ largest_size = image.sizes | array.last ~}}
<a href="{{ image_basepath }}{{ image.url }}/{{ largest_size }}.jpg" class="lightbox">
  <picture class="content-image{{ for cls in classes }} {{ cls }}{{ end }}">
    ...
  </picture>
</a>
```

## Anpassungsmöglichkeiten

### Visuelles Design (CSS Custom Properties)

Lumina (und jedes Theme, das derselben Konvention folgt) exponiert seine
Design-Tokens als native CSS Custom Properties in `Assets/main.css`. Um Farben,
Abstände oder Typografie anzupassen, extrahiere das Stylesheet und editiere die
Properties direkt:

```bash
revela theme extract Lumina --file Assets/main.css
```

Dann bearbeite `themes/Lumina/Assets/main.css`:

```css
:root {
  --color: light-dark(hsl(0 0% 40%), hsl(0 0% 70%));
  --color-bg: light-dark(hsl(0 0% 100%), hsl(0 0% 0%));
  --space-m: clamp(1.125rem, 0.9181rem + 1.0345vw, 1.5rem);
  --content-max-width: 900px;
  /* ... */
}
```

Das ist der Standard-CSS-Ansatz — er unterstützt automatischen Dark Mode
(`light-dark()`), fluides Spacing (`clamp()`) und Runtime-Kaskadierung. Keine
JSON- oder Template-Änderungen nötig.

### Footer-Text

Der Footer-Hinweis kommt aus `site.copyright` in `site.json`. Das Theme rendert
genau das, was dort steht — setze deinen Studio-Namen, lasse es leer oder ergänze
eigene Hinweise:

```json
{
  "copyright": "© 2026 Jane Doe Photography"
}
```

### Templates

Passe die HTML-Ausgabe durch Bearbeiten der `.revela`-Dateien an:

**layout.revela** - Haupt-Seitenstruktur:
```html
<!DOCTYPE html>
<html>
<head>
    <title>{{ site.title }} - {{ gallery.title }}</title>
    {{ for css in stylesheets }}
    <link rel="stylesheet" href="{{ basepath }}{{ css }}">
    {{ end }}
</head>
<body>
    {{ include 'navigation' }}
    {{ body }}
</body>
</html>
```

**Body/Gallery.revela** - Galerie-Seiteninhalt:
```html
<main class="gallery">
    <h1>{{ gallery.title }}</h1>
    {{ if gallery.body }}
    <div class="description">{{ gallery.body }}</div>
    {{ end }}
    <div class="images">
        {{ for image in images }}
        {{ include 'image' }}
        {{ end }}
    </div>
</main>
```

### Assets (CSS/JS)

CSS-Dateien sind statische Assets — sie werden 1:1 kopiert und nicht durch die
Template-Engine verarbeitet. Editiere sie direkt nach der Extraktion.

### Bildgrößen (Configuration/images.json)

Passe responsive Bildgrößen an:

```json
{
  "sizes": [160, 320, 480, 640, 720, 960, 1280, 1440, 1920, 2560]
}
```

**Hinweis:** Bildformate und Qualität werden in `project.json` konfiguriert, nicht im Theme:

```json
{
  "generate": {
    "images": {
      "avif": 80,
      "webp": 85,
      "jpg": 90
    }
  }
}
```

## Partielle Überschreibung

Du musst nicht das gesamte Theme extrahieren. Extrahiere nur, was du ändern möchtest:

```bash
# Nur die Navigation anpassen
revela theme extract Lumina --file Partials/Navigation.revela

# Nur Bildgrößen ändern
revela theme extract Lumina --file Configuration/images.json
```

Der Rest wird vom installierten Theme geladen.

**Hinweis:** Partielle Überschreibung funktioniert auf Datei-Ebene. Wenn du eine Datei extrahierst, bist du für die gesamte Datei verantwortlich.

## Theme-Extensions

Theme-Extensions (z.B. Statistics für Lumina) fügen Funktionalität wie Diagramme, Karten oder Statistiken hinzu.

### Automatische Extension-Extraktion

Wenn du ein Theme extrahierst, werden **Extensions automatisch einbezogen** in Kategorie-Unterordner:

```bash
revela theme extract Lumina
```

Dies erstellt:
```
themes/Lumina/
├── layout.revela
├── theme.json
├── Assets/
│   ├── styles.css              # Theme-Assets
│   └── Statistics/             # Extension-Assets im Unterordner
│       └── statistics.css
├── Partials/
│   ├── Navigation.revela       # Theme-Partials
│   └── Statistics/             # Extension-Partials im Unterordner
│       └── Statistics.revela
└── Configuration/
    └── images.json
```

**Warum Unterordner?** Extension-Dateien werden in `Kategorie/ExtensionName/` Unterordner platziert. Das hält dein Theme organisiert und vermeidet Dateinamenkonflikte.

### Extension-Dateien anpassen

Du kannst bestimmte Extension-Dateien im interaktiven Modus extrahieren und anpassen:

```bash
revela theme extract Lumina
# → Wähle bestimmte Dateien aus
# → Extension-Dateien zeigen ihren Zielpfad: Partials/Statistics/Statistics.revela
```

Oder via CLI:

```bash
# Bestimmte Extension-Datei extrahieren
revela theme extract Lumina --file Partials/Statistics/Statistics.revela
```

### Wie Extensions gefunden werden

Bei der Generierung sucht Revela Extension-Dateien in Kategorie-Unterordnern. Zum Beispiel wird ein Statistics-Extension-Partial gefunden unter:
- `themes/Lumina/Partials/Statistics/Statistics.revela` (lokal)
- Oder vom installierten Extension-Plugin

## Workflow-Beispiel

### 1. Mit Standard-Theme starten

```bash
revela create mein-portfolio
cd mein-portfolio
revela generate all
```

### 2. Farben anpassen

```bash
# Haupt-Stylesheet extrahieren
revela theme extract Lumina --file Assets/main.css
```

Bearbeite `themes/Lumina/Assets/main.css`:
```css
:root {
  --color: light-dark(hsl(0 0% 20%), hsl(0 0% 80%));
  --color-bg: light-dark(hsl(40 30% 98%), hsl(220 15% 8%));
}
```

### 3. Layout anpassen

```bash
# Layout-Template extrahieren
revela theme extract Lumina --file layout.revela
```

Bearbeite `themes/Lumina/layout.revela` um deinen eigenen Header hinzuzufügen.

### 4. Neu generieren

```bash
revela generate all
```

Deine Anpassungen werden automatisch verwendet.

## CLI-Referenz

```bash
# Vollständige Extraktion (inkl. Extensions)
revela theme extract Lumina
revela theme extract Lumina MeinTheme

# Selektive Extraktion
revela theme extract Lumina --file Body/Gallery.revela
revela theme extract Lumina --file Body/Gallery.revela --file Assets/

# Bestimmte Extension-Datei extrahieren (beachte Unterordner-Struktur)
revela theme extract Lumina --file Partials/Statistics/Statistics.revela

# Vorhandene Dateien überschreiben
revela theme extract Lumina --force

# Interaktiver Modus
revela theme extract

# Verfügbare Dateien auflisten
revela theme files
revela theme files --theme Lumina
```

## Tipps

1. **Klein anfangen** - Extrahiere nur, was du ändern musst
2. **Versionskontrolle** - Committe deinen `themes/`-Ordner
3. **Vorsichtig aktualisieren** - Wenn das Basis-Theme aktualisiert wird, bleiben deine Überschreibungen erhalten
4. **Variablen nutzen** - Bevorzuge Theme-Variablen gegenüber hartcodierten Werten
5. **Inkrementell testen** - Nutze `revela serve` um Änderungen live zu sehen

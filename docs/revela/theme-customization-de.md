# Theme Anpassung

## Überblick

Revela ermöglicht die Anpassung von Themes, ohne die Original-Dateien zu verändern. Du kannst:
- **Ein Theme extrahieren** in dein Projekt für vollständige Anpassung
- **Einzelne Dateien überschreiben** (Templates, Assets, Konfiguration)
- **Theme-Variablen ändern** (Farben, Schriften, Größen)

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
│       ├── theme.json             # Theme-Manifest & Variablen
│       ├── layout.revela          # Haupt-Layout-Template
│       ├── Assets/                # CSS, JS, Schriften, Bilder
│       │   ├── styles.css
│       │   └── scripts.js
│       ├── Body/                  # Body-Templates
│       │   ├── Gallery.revela
│       │   └── Page.revela
│       ├── Partials/              # Partial-Templates
│       │   ├── Navigation.revela
│       │   └── Image.revela
│       └── Configuration/         # Theme-Konfiguration
│           ├── site.json
│           └── images.json
├── source/                        # Deine Fotos
├── project.json
└── site.json
```

## Anpassungsmöglichkeiten

### Theme-Variablen (theme.json)

Ändere Farben, Schriften und andere Design-Tokens:

```json
{
  "name": "Lumina",
  "version": "1.0.0",
  "variables": {
    "primary-color": "#2563eb",
    "background-color": "#ffffff",
    "text-color": "#1f2937",
    "font-family": "Inter, sans-serif",
    "border-radius": "0.5rem"
  }
}
```

Variablen sind in Templates als `{{ theme.primary-color }}` verfügbar.

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
<body style="background: {{ theme.background-color }}">
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

Ändere Styles in `Assets/styles.css`:

```css
:root {
    --primary: {{ theme.primary-color }};
    --bg: {{ theme.background-color }};
}

.gallery {
    max-width: 1200px;
    margin: 0 auto;
}
```

### Bildgrößen (Configuration/images.json)

Passe responsive Bildgrößen an:

```json
{
  "sizes": [640, 1024, 1280, 1920, 2560]
}
```

**Hinweis:** Bildformate und Qualität werden in `project.json` konfiguriert, nicht im Theme:

```json
{
  "generate": {
    "images": {
      "formats": {
        "avif": 80,
        "webp": 85,
        "jpg": 90
      }
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
# Nur theme.json extrahieren
revela theme extract Lumina --file theme.json
```

Bearbeite `themes/Lumina/theme.json`:
```json
{
  "variables": {
    "primary-color": "#dc2626",
    "background-color": "#0f172a"
  }
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

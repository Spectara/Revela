# Template-Funktionen

## Überblick

Revela Templates (Scriban) bieten eingebaute Funktionen für URL-Generierung,
Bildverarbeitung, Datumsformatierung und Markdown-Rendering.

## Bild-Funktionen

### `find_image`

Ein beliebiges Bild aus dem Projekt per Pfad auflösen. Verwendet die gleiche 3-Schritt-Auflösung
wie Markdown Content-Bilder.

```scriban
{{~ logo = find_image "logo.jpg" ~}}

{{~ if logo ~}}
<img src="{{ image_basepath }}{{ logo.url }}/640.jpg"
     width="{{ logo.width }}" height="{{ logo.height }}">
{{~ end ~}}
```

**Parameter:**

| Parameter | Typ | Beschreibung |
|-----------|-----|--------------|
| `path` | string | Bildpfad (relativ zur Galerie, `_images/`, oder exakt) |

**Gibt zurück:** Image-Objekt oder `null` wenn nicht gefunden.

**Image-Objekt Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `url` | string | Pfadsegment für Bild-Varianten (z.B. `"logo"`) |
| `width` | int | Originalbreite in Pixeln |
| `height` | int | Originalhöhe in Pixeln |
| `sizes` | int[] | Verfügbare Breiten (z.B. `[320, 640, 1280, 1920]`) |
| `placeholder` | string? | CSS LQIP Hash (wenn aktiviert) |
| `exif` | object? | EXIF-Metadaten (wenn vorhanden) |

**Auflösungs-Reihenfolge:**
1. Galerie-lokal: `{aktueller Galerie-Pfad}/{pfad}`
2. Geteilte Bilder: `_images/{pfad}`
3. Exakte Übereinstimmung: `{pfad}` direkt

### `image_url`

URL für eine bestimmte Bild-Variante (Größe + Format) generieren.

```scriban
{{ image_url "photo.jpg" 1920 "webp" }}
```

**Parameter:**

| Parameter | Typ | Beschreibung |
|-----------|-----|--------------|
| `fileName` | string | Bild-Dateiname |
| `width` | int | Zielbreite |
| `format` | string | Bildformat (`"avif"`, `"webp"`, `"jpg"`) |

**Gibt zurück:** URL-String (z.B. `"/images/photo-1920w.webp"`)

## URL-Funktionen

### `url_for`

URL für eine Seite oder Galerie generieren.

```scriban
{{ url_for "gallery/vacation" }}
```

**Gibt zurück:** `"/gallery/vacation/index.html"`

### `asset_url`

URL für ein statisches Asset (CSS, JS) generieren.

```scriban
{{ asset_url "css/style.css" }}
```

**Gibt zurück:** `"/assets/css/style.css"`

## Formatierungs-Funktionen

### `format_date`

Datum mit benutzerdefiniertem Format formatieren.

```scriban
{{ format_date image.date_taken "yyyy-MM-dd" }}
{{ format_date image.date_taken "MMMM yyyy" }}
```

### `format_filesize`

Dateigröße in Bytes als lesbaren String formatieren.

```scriban
{{ format_filesize image.file_size }}
```

**Gibt zurück:** z.B. `"2.4 MB"`, `"340 KB"`

### `format_exif_exposure`

Belichtungszeit in Fotografie-Notation formatieren.

```scriban
{{ format_exif_exposure image.exif.exposure_time }}
```

**Gibt zurück:** z.B. `"1/250s"`, `"2s"`

### `format_exif_aperture`

Blendenwert in Fotografie-Notation formatieren.

```scriban
{{ format_exif_aperture image.exif.f_number }}
```

**Gibt zurück:** z.B. `"f/2.8"`, `"f/11"`

## Content-Funktionen

### `markdown`

Markdown-Text in HTML konvertieren.

```scriban
{{ "**fett** text" | markdown }}
```

**Gibt zurück:** `"<p><strong>fett</strong> text</p>"`

## Template-Variablen

Diese sind keine Funktionen, sondern Variablen die in allen Templates verfügbar sind:

| Variable | Typ | Beschreibung |
|----------|-----|--------------|
| `site` | object | Seiten-Einstellungen aus `site.json` |
| `gallery` | Gallery | Aktuelle Seite (title, body, cover_image, template) |
| `images` | Image[] | Bilder der aktuellen Galerie |
| `nav_items` | NavItem[] | Navigationsbaum |
| `basepath` | string | Relativer Pfad zum Seiten-Root |
| `image_basepath` | string | Pfad/URL zu den Bild-Varianten |
| `image_formats` | string[] | Aktive Formate (`["avif", "webp", "jpg"]`) |
| `page_content` | string | Original Markdown-Body als HTML |
| `theme` | object | Theme-Variablen aus `theme.json` |
| `stylesheets` | string[] | CSS-Dateinamen |
| `scripts` | string[] | JS-Dateinamen |

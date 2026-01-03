# Sortierung konfigurieren

## Überblick

Revela unterstützt flexible Sortierung von Galerien und Bildern. Du kannst konfigurieren:
- **Galerie-Reihenfolge** in der Navigation (aufsteigend/absteigend)
- **Bild-Reihenfolge** innerhalb von Galerien (nach beliebigem Feld inkl. EXIF-Daten)
- **Pro-Galerie Überschreibung** via Front Matter

## Globale Konfiguration (project.json)

```json
{
  "generate": {
    "sorting": {
      "galleries": "asc",
      "images": {
        "field": "dateTaken",
        "direction": "desc",
        "fallback": "filename"
      }
    }
  }
}
```

| Eigenschaft | Beschreibung | Standard |
|-------------|--------------|----------|
| `galleries` | Galerie-Sortierrichtung: `asc` oder `desc` | `asc` |
| `images.field` | Feld nach dem Bilder sortiert werden | `dateTaken` |
| `images.direction` | Bild-Sortierrichtung: `asc` oder `desc` | `desc` |
| `images.fallback` | Fallback-Feld wenn Primärfeld leer ist | `filename` |

## Verfügbare Sortierfelder

| Feld | Beschreibung |
|------|--------------|
| `filename` | Dateiname (alphabetisch) |
| `dateTaken` | EXIF Aufnahmedatum |
| `exif.focalLength` | Brennweite in mm |
| `exif.fNumber` | Blende (f-Zahl) |
| `exif.exposureTime` | Verschlusszeit |
| `exif.iso` | ISO-Empfindlichkeit |
| `exif.make` | Kamera-Hersteller |
| `exif.model` | Kamera-Modell |
| `exif.lensModel` | Objektiv-Modell |
| `exif.raw.Rating` | Sternebewertung (1-5) |
| `exif.raw.Copyright` | Copyright-Feld |
| `exif.raw.{FeldName}` | Beliebiges EXIF-Feld aus Raw-Dictionary |

## Pro-Galerie Überschreibung (Front Matter)

Überschreibe die globalen Sortiereinstellungen für einzelne Galerien via Front Matter in `_index.revela`:

**Format:**
```
sort = "field"           # Feld überschreiben, Richtung von Global
sort = "field:asc"       # Feld mit aufsteigender Sortierung
sort = "field:desc"      # Feld mit absteigender Sortierung
```

**Beispiele:**

```toml
+++
title = "Objektiv-Vergleich"
sort = "exif.focalLength:asc"
+++

Vergleich von Weitwinkel bis Tele.
```

```toml
+++
title = "Beste Aufnahmen"
sort = "exif.raw.Rating:desc"
+++

Meine am höchsten bewerteten Fotos.
```

```toml
+++
title = "Chronik"
sort = "dateTaken:asc"
+++

Fotos in chronologischer Reihenfolge (älteste zuerst).
```

```toml
+++
title = "Neueste Arbeiten"
sort = "dateTaken:desc"
+++

Neueste Fotos zuerst.
```

## CLI-Konfiguration

Sortierung interaktiv oder per Kommandozeile konfigurieren:

```bash
# Interaktiver Wizard
revela config sorting

# Bild-Sortierfeld setzen
revela config sorting --field dateTaken --direction desc

# Nach Bewertung sortieren
revela config sorting --field exif.raw.Rating --direction desc

# Nach Brennweite sortieren
revela config sorting --field exif.focalLength --direction asc

# Galerie-Reihenfolge ändern
revela config sorting --galleries desc
```

## Logik-Ablauf

1. **Kein Front Matter `sort`** → Globale Config (`generate.sorting.images`)
2. **`sort = "field"`** → Feld überschreiben, Richtung von Global
3. **`sort = "field:direction"`** → Beides überschreiben
4. **Fallback** kommt immer aus globaler Config (nicht pro Galerie überschreibbar)
5. **Finaler Tie-Breaker** ist immer Dateiname (für stabile Sortierung)

## Technische Details

### Configuration Binding

Das `SortDirection` Enum verwendet kurze Namen für IConfiguration-Binding-Kompatibilität:

```csharp
public enum SortDirection
{
    Asc,   // JSON: "asc"
    Desc   // JSON: "desc"
}
```

### EXIF Raw Dictionary

Zusätzliche EXIF-Felder werden in das `ExifData.Raw` Dictionary extrahiert:

- Rating, Copyright, Artist
- ExposureProgram, MeteringMode, Flash
- WhiteBalance, SceneCaptureType
- FocalLengthIn35mmFormat
- Und ~30 weitere fotografenrelevante Felder

Nur nicht-leere Werte werden gespeichert, um das Manifest kompakt zu halten.

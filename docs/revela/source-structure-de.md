# Source-Ordner Struktur

## Übersicht

Revela unterstützt zwei Ansätze zur Organisation deiner Fotos, die auch kombiniert werden können:

| Ansatz | Ideal Für | Bilder Gespeichert In |
|--------|-----------|----------------------|
| **Traditionelle Galerien** | Fotos gehören zu einer Galerie | Galerie-Ordner |
| **Filter-Galerien** | Fotos in mehreren Galerien | `_images/` Ordner |
| **Hybrid** | Mix aus beiden Ansätzen | Beide Orte |

## Traditionelle Galerien

Jede Galerie enthält ihre eigenen Bilder direkt im Ordner.

### Struktur

```
source/
├── _index.revela          # Startseite
├── events/
│   ├── _index.revela      # Galerie-Seite
│   ├── event-001.jpg
│   ├── event-002.jpg
│   └── event-003.jpg
├── portraits/
│   ├── _index.revela
│   ├── portrait-001.jpg
│   └── portrait-002.jpg
└── landscapes/
    ├── _index.revela
    ├── mountain.jpg
    └── ocean.jpg
```

### Eigenschaften

- ✅ Einfach und intuitiv
- ✅ Jedes Foto gehört zu genau einer Galerie
- ✅ Einfach manuell zu verwalten
- ❌ Fotos können nicht in mehreren Galerien erscheinen
- ❌ Umstrukturierung erfordert Dateien verschieben

### Beispiel Front Matter

```toml
+++
title = "Events 2025"
description = "Fotos von verschiedenen Events"
+++

Eine Sammlung von Event-Fotografie.
```

## Filter-Galerien

Alle Bilder werden in einem gemeinsamen `_images/` Ordner gespeichert. Galerien verwenden Filter-Ausdrücke, um auszuwählen welche Bilder angezeigt werden.

### Struktur

```
source/
├── _index.revela          # Startseite (kann auch Filter nutzen)
├── _images/               # Gemeinsamer Bilder-Pool (Unterstrich-Präfix!)
│   ├── canon-event-001.jpg
│   ├── canon-portrait-002.jpg
│   ├── sony-landscape-003.jpg
│   └── sony-portrait-004.jpg
├── canon/
│   └── _index.revela      # filter = "exif.make == 'Canon'"
├── sony/
│   └── _index.revela      # filter = "exif.make == 'Sony'"
├── portraits/
│   └── _index.revela      # filter = "contains(filename, 'portrait')"
└── landscapes/
    └── _index.revela      # filter = "contains(filename, 'landscape')"
```

### Eigenschaften

- ✅ Gleiches Bild kann in mehreren Galerien erscheinen
- ✅ Galerien aktualisieren sich automatisch bei Änderungen
- ✅ Mächtige Abfragen basierend auf EXIF, Dateiname, Datum
- ✅ Ideal für übergreifende Kategorien (nach Kamera, nach Jahr, etc.)
- ❌ Erfordert konsistente Benennung oder EXIF-Daten
- ❌ Abstraktere Organisation

### Der `_images/` Ordner

Der Unterstrich-Präfix ist wichtig:
- `_images/` wird **nicht** als eigene Galerie gerendert
- Bilder sind nur über Filter-Ausdrücke zugänglich
- Du kannst jeden Namen verwenden: `_photos/`, `_pool/`, `_shared/`

### Beispiel Front Matter

```toml
+++
title = "Canon Fotos"
description = "Alle Fotos mit Canon Kameras aufgenommen"
filter = "exif.make == 'Canon'"
+++

Meine Canon Kamera-Sammlung.
```

## Hybrid-Ansatz

Kombiniere beide Ansätze im selben Projekt. Das ist ideal für Seiten, wo manche Inhalte exklusiv sind und andere mehrfach referenziert werden.

### Struktur

```
source/
├── _index.revela              # Startseite mit neuesten Fotos
├── _images/                   # Gemeinsamer Pool für Filter-Galerien
│   ├── 2024-trip-001.jpg
│   ├── 2024-trip-002.jpg
│   ├── 2025-event-001.jpg
│   └── 2025-event-002.jpg
│
├── by-camera/                 # Filter-Galerien (virtuell)
│   ├── _index.revela          # Kategorie-Seite
│   ├── canon/
│   │   └── _index.revela      # filter = "exif.make == 'Canon'"
│   └── sony/
│       └── _index.revela      # filter = "exif.make == 'Sony'"
│
├── by-year/                   # Filter-Galerien (virtuell)
│   ├── _index.revela
│   ├── 2024/
│   │   └── _index.revela      # filter = "year(dateTaken) == 2024"
│   └── 2025/
│       └── _index.revela      # filter = "year(dateTaken) == 2025"
│
├── clients/                   # Traditionelle Galerien (exklusiv)
│   ├── _index.revela
│   ├── wedding-smith/
│   │   ├── _index.revela
│   │   ├── ceremony-001.jpg   # Nur in dieser Galerie
│   │   └── reception-002.jpg
│   └── corporate-abc/
│       ├── _index.revela
│       └── headshot-001.jpg   # Nur in dieser Galerie
│
└── personal/                  # Traditionelle Galerie
    ├── _index.revela
    ├── family-001.jpg
    └── vacation-002.jpg
```

### Wie es funktioniert

1. **`_images/`** enthält Fotos, die mehrfach kategorisiert werden sollen
2. **Filter-Galerien** (`by-camera/`, `by-year/`) fragen den `_images/` Pool ab
3. **Traditionelle Galerien** (`clients/`, `personal/`) haben ihre eigenen exklusiven Bilder
4. **Keine Überschneidung** - Traditionelle Galerie-Bilder sind nicht in `_images/`

### Eigenschaften

- ✅ Das Beste aus beiden Welten
- ✅ Kundenarbeit bleibt organisiert und exklusiv
- ✅ Persönliche/Portfolio-Arbeit kann mehrfach referenziert werden
- ✅ Flexible Organisation je nach Anwendungsfall
- ⚠️ Erfordert klares mentales Modell, welche Bilder wohin gehören

## Den richtigen Ansatz wählen

| Szenario | Empfohlener Ansatz |
|----------|-------------------|
| Einfaches Portfolio mit klaren Kategorien | Traditionell |
| Fotos brauchen mehrere Kategorisierungen | Filter |
| Kundenarbeit (exklusive Galerien) | Traditionell |
| "Nach Kamera", "Nach Jahr" Ansichten | Filter |
| Startseite mit "Neueste Fotos" | Filter (`all \| sort dateTaken desc \| limit 5`) |
| Mix aus exklusiven und geteilten Inhalten | Hybrid |

## Häufige Muster

### Startseite mit neuesten Fotos

```toml
+++
title = "Willkommen"
filter = "all | sort dateTaken desc | limit 6"
+++

Meine neuesten Arbeiten.
```

### Kategorie-Seite (ohne Bilder)

```toml
+++
title = "Nach Kamera durchsuchen"
template = "page"
+++

Wähle unten eine Kameramarke aus.
```

### Verschachtelte Filter-Galerien

```
source/
├── _images/
├── equipment/
│   ├── _index.revela          # template = "page" (keine Bilder)
│   ├── cameras/
│   │   ├── _index.revela      # template = "page"
│   │   ├── canon/
│   │   │   └── _index.revela  # filter = "exif.make == 'Canon'"
│   │   └── sony/
│   │       └── _index.revela  # filter = "exif.make == 'Sony'"
│   └── lenses/
│       ├── _index.revela      # template = "page"
│       ├── wide/
│       │   └── _index.revela  # filter = "exif.focalLength <= 35"
│       └── tele/
│           └── _index.revela  # filter = "exif.focalLength >= 85"
```

## Migrations-Tipps

### Von Traditionell zu Filter

1. `_images/` Ordner erstellen
2. Bilder aus Galerien nach `_images/` verschieben
3. `filter = "..."` zu jeder `_index.revela` der Galerien hinzufügen
4. Sicherstellen, dass Bilder korrekte EXIF-Daten oder Namenskonvention haben

### Filter-Galerien zu bestehender Seite hinzufügen

1. `_images/` Ordner erstellen
2. Bilder, die mehrfach referenziert werden sollen, kopieren (oder verschieben)
3. Neue Filter-Galerie-Ordner mit `_index.revela` erstellen
4. Traditionelle Galerien unverändert lassen

## Siehe auch

- [Filter-Galerien](filtering-de.md) - Vollständige Filter-Syntax Referenz
- [Sortierung konfigurieren](sorting-de.md) - Bild- und Galerie-Sortierung
- [Seiten erstellen](pages-de.md) - Seitentypen und Templates
- [Statische Dateien](static-files-de.md) - Favicons, robots.txt und andere statische Dateien

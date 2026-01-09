# Filter-Galerien

## Übersicht

Revela unterstützt **virtuelle Galerien**, die automatisch Bilder aus einem gemeinsamen Pool anhand von Filter-Ausdrücken auswählen. Statt Fotos manuell in Ordner zu sortieren, definierst du Kriterien und Revela erstellt die Galerie dynamisch.

**Vorteile:**
- **Single Source of Truth** - Bilder nur einmal im `_images/` Ordner
- **Dynamische Galerien** - Aktualisieren sich automatisch bei Änderungen
- **Mächtige Abfragen** - Filtern nach EXIF-Daten, Dateiname, Datum und mehr
- **Pipe-Syntax** - Sortierung und Limitierung verketten

## Grundlegende Einrichtung

### 1. Gemeinsamen Bilder-Ordner erstellen

Erstelle einen `_images/` Ordner (beachte den Unterstrich) in deinem Source-Verzeichnis:

```
source/
├── _images/           # Gemeinsame Bilder (keine Galerie selbst)
│   ├── photo-001.jpg
│   ├── photo-002.jpg
│   └── ...
├── canon/             # Filter-Galerie
│   └── _index.revela
├── sony/              # Filter-Galerie
│   └── _index.revela
└── _index.revela      # Startseite
```

### 2. Filter-Ausdruck hinzufügen

In der `_index.revela` jeder Galerie einen `filter` angeben:

```toml
+++
title = "Canon Fotos"
filter = "exif.make == 'Canon'"
+++

Alle Fotos aufgenommen mit Canon Kameras.
```

## Filter-Syntax

### Vergleichsoperatoren

| Operator | Beschreibung | Beispiel |
|----------|--------------|----------|
| `==` | Gleich | `exif.make == 'Canon'` |
| `!=` | Ungleich | `exif.make != 'Sony'` |
| `<` | Kleiner als | `exif.iso < 800` |
| `<=` | Kleiner oder gleich | `exif.focalLength <= 35` |
| `>` | Größer als | `exif.iso > 1600` |
| `>=` | Größer oder gleich | `exif.iso >= 3200` |

### Logische Operatoren

Bedingungen mit `and`, `or` und `not` kombinieren:

```toml
# Beide Bedingungen müssen wahr sein
filter = "exif.make == 'Canon' and exif.iso >= 800"

# Eine der Bedingungen muss wahr sein
filter = "exif.make == 'Canon' or exif.make == 'Sony'"

# Bedingung negieren
filter = "not exif.make == 'Canon'"
```

**Priorität:** `and` bindet stärker als `or`. Klammern ändern die Reihenfolge:

```toml
# Ausgewertet als: a or (b and c)
filter = "exif.make == 'Canon' or exif.make == 'Sony' and exif.iso >= 800"

# Ausgewertet als: (a or b) and c
filter = "(exif.make == 'Canon' or exif.make == 'Sony') and exif.iso >= 800"
```

## Verfügbare Eigenschaften

### Bild-Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `filename` | string | Dateiname (z.B. `photo-001.jpg`) |
| `sourcePath` | string | Vollständiger Pfad im Source-Verzeichnis |
| `width` | int | Bildbreite in Pixeln |
| `height` | int | Bildhöhe in Pixeln |
| `fileSize` | long | Dateigröße in Bytes |
| `dateTaken` | DateTime | Aufnahmedatum |

### EXIF-Eigenschaften

Zugriff über `exif.` Präfix:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `exif.make` | string | Kamerahersteller |
| `exif.model` | string | Kameramodell |
| `exif.lensModel` | string | Objektivname |
| `exif.fNumber` | double | Blende (f/2.8 = 2.8) |
| `exif.exposureTime` | double | Belichtungszeit in Sekunden |
| `exif.iso` | int | ISO-Empfindlichkeit |
| `exif.focalLength` | int | Brennweite in mm |
| `exif.dateTaken` | DateTime | EXIF Datum/Zeit |
| `exif.gpsLatitude` | double? | GPS-Breitengrad |
| `exif.gpsLongitude` | double? | GPS-Längengrad |

### Raw EXIF Zugriff

Zugriff auf beliebige EXIF-Tags über `exif.raw.TagName`:

```toml
filter = "exif.raw.Software == 'Lightroom'"
filter = "exif.raw.Artist == 'Max Mustermann'"
filter = "exif.raw.Rating >= 4"
```

## Eingebaute Funktionen

### Datums-Funktionen

| Funktion | Beschreibung | Beispiel |
|----------|--------------|----------|
| `year(date)` | Jahr extrahieren | `year(dateTaken) == 2024` |
| `month(date)` | Monat extrahieren (1-12) | `month(dateTaken) == 12` |
| `day(date)` | Tag extrahieren (1-31) | `day(dateTaken) == 25` |

### String-Funktionen

| Funktion | Beschreibung | Beispiel |
|----------|--------------|----------|
| `contains(str, substr)` | Enthält prüfen | `contains(filename, 'portrait')` |
| `starts_with(str, prefix)` | Präfix prüfen | `starts_with(filename, 'IMG_')` |
| `ends_with(str, suffix)` | Suffix prüfen | `ends_with(filename, '-edit.jpg')` |
| `lower(str)` | Kleinschreibung | `lower(exif.make) == 'canon'` |
| `upper(str)` | Großschreibung | `upper(exif.make) == 'CANON'` |

## Pipe-Syntax

Operationen mit dem Pipe-Operator `|` verketten:

```
filter_ausdruck | sort eigenschaft [asc|desc] | limit n
```

### Das `all` Keyword

Alle Bilder ohne Filterung auswählen:

```toml
filter = "all"
filter = "all | sort dateTaken desc"
filter = "all | sort dateTaken desc | limit 10"
```

### Sort-Klausel

Ergebnisse nach beliebiger Eigenschaft sortieren:

```toml
# Neueste zuerst
filter = "all | sort dateTaken desc"

# Alphabetisch nach Dateiname
filter = "all | sort filename asc"

# Nach EXIF-Eigenschaft
filter = "exif.make == 'Canon' | sort exif.iso desc"
```

**Standard-Richtung:** `asc` (aufsteigend)

### Limit-Klausel

Anzahl der Ergebnisse begrenzen:

```toml
# Nur 5 Bilder
filter = "all | limit 5"

# 10 neueste Fotos
filter = "all | sort dateTaken desc | limit 10"
```

### Kombiniertes Beispiel

```toml
# Top 5 High-ISO Canon Fotos, neueste zuerst
filter = "exif.make == 'Canon' and exif.iso >= 1600 | sort dateTaken desc | limit 5"
```

## Literal-Werte

### Strings

Einfache oder doppelte Anführungszeichen verwenden:

```toml
filter = "filename == 'test.jpg'"
filter = "filename == \"test.jpg\""
```

### Zahlen

```toml
filter = "exif.iso == 800"       # Ganzzahl
filter = "exif.fNumber == 2.8"   # Dezimalzahl
```

### Null

Fehlende Werte prüfen:

```toml
filter = "exif.gpsLatitude != null"  # Hat GPS-Daten
filter = "exif.lensModel == null"    # Keine Objektiv-Info
```

## Häufige Muster

### Nach Kameramarke

```toml
filter = "exif.make == 'Canon'"
filter = "exif.make == 'Sony'"
filter = "exif.make == 'Nikon'"
filter = "exif.make == 'Canon' or exif.make == 'Sony'"
```

### Nach Jahr

```toml
filter = "year(dateTaken) == 2024"
filter = "year(dateTaken) >= 2020 and year(dateTaken) <= 2024"
```

### High ISO (Schwachlicht)

```toml
filter = "exif.iso >= 3200"
filter = "exif.iso >= 1600 | sort exif.iso desc"
```

### Nach Dateiname-Muster

```toml
filter = "contains(filename, 'portrait')"
filter = "contains(lower(filename), 'portrait')"
filter = "starts_with(filename, 'IMG_')"
```

### Weitwinkel / Tele

```toml
filter = "exif.focalLength <= 35"   # Weitwinkel
filter = "exif.focalLength >= 85"   # Portrait/Tele
filter = "exif.focalLength >= 200"  # Supertele
```

### Neueste Fotos (Startseite)

```toml
filter = "all | sort dateTaken desc | limit 5"
```

### Beste Bewertung

```toml
filter = "exif.raw.Rating >= 4 | sort exif.raw.Rating desc"
```

## Filter vs Sort

| Feature | Filter (`filter =`) | Sort (`sort =`) |
|---------|---------------------|-----------------|
| Zweck | Bilder aus `_images/` auswählen | Bilder in beliebiger Galerie ordnen |
| Geltungsbereich | Erstellt virtuelle Galerie | Wirkt auf vorhandene Bilder |
| Syntax | Ausdruckssprache | `field:direction` |
| Anwendung | Dynamische Sammlungen | Benutzerdefinierte Reihenfolge |

**Filter-Galerien** holen Bilder aus `_images/`. **Sort** ordnet die Bilder, die eine Galerie bereits hat.

Beides kann kombiniert werden:

```toml
+++
filter = "exif.make == 'Canon'"
sort = "dateTaken:desc"
+++
```

## Fehlerbehandlung

Ungültige Ausdrücke zeigen hilfreiche Fehlermeldungen:

```
Filter parse error at position 15: Unexpected token 'xyz'
Expression: exif.make == xyz
                         ^^^
```

Mit `revela generate scan` können Filter vor der vollständigen Generierung validiert werden.

## Siehe auch

- [Sortierung konfigurieren](sorting-de.md) - Galerie- und Bildsortierung
- [Seiten-Dokumentation](pages-de.md) - Inhaltsseiten ohne Bilder

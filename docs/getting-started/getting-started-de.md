# Revela - Erste Schritte

Eine Schritt-für-Schritt-Anleitung für Fotografen, um mit Revela eine Portfolio-Website zu erstellen.

---

## Was ist Revela?

**Revela** ist ein Programm, das aus deinen Fotos automatisch eine schöne Portfolio-Website erstellt. Du legst deine Bilder in Ordner, startest Revela, und bekommst eine fertige Website mit:

- Automatisch skalierten Bildern (verschiedene Größen für schnelles Laden)
- Moderner Galerie-Ansicht mit Lightbox
- Responsivem Design (funktioniert auf Handy, Tablet, Desktop)
- Schnellen Ladezeiten durch optimierte Bildformate (AVIF, WebP, JPG)

**Einfach zu bedienen:** Wenn du `revela.exe` doppelklickst, führen dich interaktive Assistenten durch die Einrichtung. Folge einfach den Anweisungen - keine Kommandozeilen-Kenntnisse erforderlich!

---

## 1. Revela herunterladen

### Schritt 1.1: Download

1. Gehe zu den **GitHub Releases**: https://github.com/spectara/revela/releases
2. Lade die neueste Version herunter:
   - `revela-win-x64.zip` für Windows (64-bit)
3. Entpacke die ZIP-Datei in einen Ordner deiner Wahl, z.B.:
   - `C:\Revela\`
   - oder `D:\Tools\Revela\`

**Optional (für Fortgeschrittene):** Du kannst die Release-Dateien verifizieren. Zu jeder Version gibt es Prüfsummen (`SHA256SUMS`), cosign-Signaturen und GitHub Attestations – Details findest du auf der jeweiligen Release-Seite.

Nach dem Entpacken hast du folgende Dateien:
```
C:\Revela\
├── revela.exe                          ← Das Hauptprogramm
├── projects/                           ← Hier werden deine Projekte sein
│   └── (leer)
└── getting-started/                    ← Anleitungen (mehrsprachig)
    ├── README.md
    ├── de.md                           ← Deutsch
    └── en.md                           ← English
```

### Schritt 1.2: Revela starten

1. Öffne den Ordner `C:\Revela` im Windows Explorer
2. **Doppelklicke auf `revela.exe`**
3. Der **Revela Setup-Assistent** öffnet sich automatisch

Das war's! Keine Installation nötig, keine Kommandozeile erforderlich.

---

## 2. Revela Setup-Assistent (Erster Start)

Beim ersten Start von Revela erscheint automatisch der **Setup-Assistent**. Dieser Assistent hilft dir, Themes und Plugins zu installieren.

### Was du siehst

```
┌─────────────────────────────────────────────────────────────┐
│  Willkommen beim Revela Setup-Assistenten!                  │
│                                                             │
│  Dieser Assistent hilft dir bei der Ersteinrichtung:        │
│    1. Theme installieren (erforderlich)                     │
│    2. Plugins installieren (optional)                       │
│                                                             │
│  Du kannst diesen Assistenten später erneut starten via:    │
│  Addons → wizard                                            │
└─────────────────────────────────────────────────────────────┘
```

### Schritt 2.1: Theme installieren

1. Der Assistent lädt automatisch den Paketindex herunter
2. Du siehst eine Liste verfügbarer Themes
3. Wähle mindestens ein Theme aus (**Leertaste** zum Auswählen, **Enter** zum Bestätigen)
4. Empfohlen: **Lumina** (das Standard-Fotografie-Theme)

**Tipp:** Du kannst mehrere Themes auswählen, wenn du verschiedene Looks ausprobieren möchtest.

### Schritt 2.2: Plugins installieren (Optional)

1. Du siehst eine Liste verfügbarer Plugins
2. Wähle beliebige Plugins aus (oder keine)
3. Nützliche Plugins:
   - **Serve** - Vorschau deiner Seite lokal vor dem Hochladen
   - **Statistics** - Bildanzahl, Gesamtgröße usw. erfassen
   - **Source.OneDrive** - Fotos aus OneDrive-Freigaben importieren

### Schritt 2.3: Neustart

Nach der Installation muss Revela neu gestartet werden, um die neuen Pakete zu laden:

```
✓ Setup erfolgreich abgeschlossen!

Installierte Themes:
  • Lumina

Bitte starte Revela neu, um die neuen Pakete zu laden.
```

**Doppelklicke erneut auf `revela.exe`** um fortzufahren.

---

## 3. Projekt-Ordner auswählen oder erstellen

Nach dem Setup-Assistenten (und Neustart) siehst du den **Projekt-Auswahl**-Bildschirm:

```
Select a project folder:

Projects
  (noch keine Projekte)
Setup
  Create new project folder
Exit
```

### Schritt 3.1: Projekt-Ordner erstellen

1. Wähle **Create new project folder**
2. Gib einen Namen für dein Projekt ein (z.B. "MeineFotos", "Hochzeit2025")
3. Der Ordner wird unter `C:\Revela\projects\MeineFotos\` erstellt

**Tipp:** Verwende aussagekräftige Namen - du kannst mehrere Projekte für verschiedene Foto-Sammlungen haben!

Nach dem Erstellen des Ordners startet Revela neu und öffnet dein neues Projekt.

---

## 4. Projekt-Assistent (Erstes Mal im Projekt)

Wenn du einen Projekt-Ordner zum ersten Mal betrittst (keine `project.json`), erscheint automatisch der **Projekt-Assistent**.

### Was du siehst

```
┌─────────────────────────────────────────────────────────────┐
│  Neues Revela-Projekt erstellen                             │
│                                                             │
│  Dieser Assistent hilft dir bei der Einrichtung:            │
│    1. Projekt-Einstellungen (Name, URL)                     │
│    2. Theme auswählen                                       │
│    3. Bild-Einstellungen (Formate, Größen)                  │
│    4. Website-Metadaten (Titel, Autor)                      │
│                                                             │
│  Du kannst diese Einstellungen später ändern via:           │
│  revela config                                              │
└─────────────────────────────────────────────────────────────┘
```

### Schritt 4.1: Projekt-Einstellungen

Gib deine Projektdetails ein:

- **Projektname:** Ein kurzer Name für dein Projekt (z.B. "MeinPortfolio")
- **Basis-URL:** Deine Website-Adresse (z.B. "https://fotos.beispiel.de")
  - Leer lassen, wenn du sie noch nicht weißt

### Schritt 4.2: Theme auswählen

Wähle ein Theme aus deinen installierten Themes. Wenn du nur Lumina installiert hast, wird es automatisch ausgewählt.

### Schritt 4.3: Bild-Einstellungen

Konfiguriere, wie deine Bilder verarbeitet werden sollen:

- **Formate:** Welche Formate erstellt werden (AVIF, WebP, JPG)
- **Qualität:** Höher = bessere Qualität, aber größere Dateien
- **Größen:** Welche Breiten erstellt werden (responsive Bilder)

**Tipp für Anfänger:** Die Standardwerte funktionieren super! Drücke einfach Enter, um sie zu übernehmen.

**Tipp für Geschwindigkeit:** AVIF bietet die beste Kompression, braucht aber länger. Für einen schnellen ersten Test kannst du AVIF deaktivieren und nur WebP und JPG behalten.

### Schritt 4.4: Website-Metadaten

Gib Informationen über deine Website ein:

- **Titel:** Dein Website-Titel (erscheint im Browser-Tab)
- **Autor:** Dein Name
- **Copyright:** Copyright-Hinweis (z.B. "© 2025 Max Mustermann")

### Nach dem Assistenten

Der Assistent erstellt diese Dateien und Ordner in deinem Projekt:

```
C:\Revela\
├── revela.exe
├── revela.json                         ← Revela-Konfiguration
├── packages/                           ← Installierte Themes & Plugins
│   └── ...
└── projects/
    └── MeineFotos/                     ← Dein Projekt-Ordner
        ├── project.json                ← Projekt-Einstellungen
        ├── site.json                   ← Website-Metadaten
        ├── source/                     ← Hier kommen deine Fotos rein
        │   └── (leer)
        ├── output/                     ← Hier landet die fertige Website
        │   └── (leer)
        └── cache/                      ← Bild-Cache
            └── (leer)
```

---

## 5. Fotos hinzufügen

### Schritt 5.1: Galerien als Ordner anlegen

Erstelle im `source`-Ordner Unterordner für deine Galerien. Die Ordnernamen werden zu Galerie-Titeln:

```
C:\Revela\projects\MeineFotos\source\
├── 01 Hochzeiten/
│   ├── foto1.jpg
│   ├── foto2.jpg
│   └── foto3.jpg
├── 02 Portraits/
│   ├── portrait1.jpg
│   └── portrait2.jpg
└── 03 Landschaften/
    ├── berge.jpg
    └── sonnenuntergang.jpg
```

**Tipps zur Ordnerstruktur:**

- **Nummerierung:** Zahlen am Anfang (`01`, `02`, `03`) bestimmen die Menü-Reihenfolge
- **Ordnername = Galerie-Titel:** "01 Hochzeiten" wird zu "Hochzeiten" auf der Website
- **Verschachtelte Galerien:** Du kannst Untergalerien anlegen:
  ```
  source/
  └── 01 Events/
      ├── 01 Hochzeit Lisa & Tom/
      └── 02 Firmenfeier Müller AG/
  ```

### Schritt 5.2: Galerie-Beschreibung hinzufügen (Optional)

Um den Titel einer Galerie anzupassen oder eine Beschreibung hinzuzufügen, erstelle eine `_index.md`-Datei:

**Datei:** `source/01 Hochzeiten/_index.md`
```markdown
---
title: Hochzeitsfotografie
description: Emotionale Momente für die Ewigkeit festgehalten
---

Jede Hochzeit erzählt ihre eigene Geschichte. Hier findest du 
eine Auswahl meiner schönsten Hochzeitsbilder.
```

| Feld | Bedeutung |
|------|-----------|
| `title` | Überschreibt den Ordnernamen als Titel |
| `description` | Beschreibungstext für die Galerie |

Der Text unter den `---`-Linien erscheint als Einleitungstext auf der Galerie-Seite.

---

## 6. Website generieren

### Schritt 6.1: Website generieren

1. Doppelklicke `revela.exe` um das Menü zu öffnen
2. Wähle **generate**
3. Wähle **all** für die vollständige Pipeline

**Was passiert:**

1. **Scan:** Revela findet alle Bilder in `source/`
2. **Bilder verarbeiten:** Jedes Bild wird in allen konfigurierten Größen und Formaten erstellt
3. **Seiten rendern:** HTML-Dateien werden aus den Templates generiert

Du siehst einen Fortschrittsbalken:

```
Scanning...
✓ Found 47 images in 5 galleries

Processing images [████████████████████] 100% 47/47 - berge.jpg
Rendering pages   [████████████████████] 100% 12/12 - index.html

✓ Generation complete!
```

### Schritt 6.2: Nur Teile neu generieren (Optional)

Im **generate**-Untermenü kannst du einzelne Schritte auswählen:

| Option | Was sie macht |
|--------|---------------|
| **all** | Vollständige Pipeline (scan → statistics → pages → images) |
| **scan** | Quelldateien scannen (zuerst ausführen bei Bildänderungen) |
| **statistics** | Statistiken generieren (erfordert Statistics-Plugin) |
| **pages** | Nur HTML-Seiten neu rendern |
| **images** | Nur Bilder neu verarbeiten |

**Hinweis:** Nach dem Hinzufügen/Löschen von Bildern oder Ändern von `_index.md`-Dateien zuerst **scan** ausführen.

---

## 7. Vorschau & Hochladen

### Schritt 7.1: Lokale Vorschau (mit Serve-Plugin)

Wenn du das **Serve**-Plugin installiert hast:

1. Wähle im Menü **serve** → **start**
2. Dein Browser öffnet sich automatisch mit deiner Seite
3. Drücke **Strg+C** im Terminal um den Server zu stoppen

### Schritt 7.2: Dateien direkt öffnen

Ohne das Serve-Plugin kannst du die Dateien direkt öffnen:

1. Gehe im Windows Explorer zu `C:\Revela\projects\MeineFotos\output\`
2. Doppelklicke auf `index.html`
3. Die Website öffnet sich in deinem Browser

**Hinweis:** Einige Funktionen (wie Lazy Loading) funktionieren besser mit einem echten Server.

### Schritt 7.3: Auf Webserver hochladen

Um deine Website online zu stellen, lade den kompletten Inhalt des `output`-Ordners auf deinen Webserver per FTP, SFTP oder den Dateimanager deines Hosting-Anbieters.

---

## 8. Menü-Übersicht

### Hauptmenü

| Menü | Untermenü | Beschreibung |
|------|-----------|--------------|
| **generate** | all | Website generieren (volle Pipeline) |
| | scan | Quelldateien scannen |
| | images | Nur Bilder verarbeiten |
| | pages | Nur HTML-Seiten erstellen |
| | statistics | Statistik-JSON generieren |
| **clean** | all | Output + Cache löschen |
| | output | Nur Output löschen |
| | cache | Nur Cache löschen |
| **config** | project | Projekt-Einstellungen bearbeiten |
| | theme | Theme wechseln |
| | images | Bild-Einstellungen bearbeiten |
| | site | Website-Metadaten bearbeiten |
| | feed | Paketquellen verwalten |
| **theme** | list | Installierte Themes anzeigen |
| | install | Neues Theme installieren |
| | extract | Eigene Theme-Kopie erstellen |
| **plugins** | list | Installierte Plugins anzeigen |
| | install | Neues Plugin installieren |
| | uninstall | Plugin entfernen |
| **packages** | refresh | Paketindex aktualisieren |
| | list | Alle verfügbaren Pakete anzeigen |
| **serve** | start | Lokalen Vorschau-Server starten |
| | | *(erfordert Serve-Plugin)* |

### Setup-Gruppe (Standalone-Modus)

| Option | Beschreibung |
|--------|--------------|
| **projects** | Projekt-Ordner verwalten (list, create, delete) |

### Addons-Gruppe

| Option | Beschreibung |
|--------|--------------|
| **wizard** | Revela Setup-Assistenten erneut starten |

---

## 9. Mit mehreren Projekten arbeiten

Revela unterstützt mehrere Projekte im Standalone-Modus. Jedes Projekt hat seinen eigenen Ordner mit separaten `source/`, `output/` und `cache/` Verzeichnissen.

### Zwischen Projekten wechseln

1. Doppelklicke `revela.exe`
2. Der Projekt-Auswahl-Bildschirm zeigt alle deine Projekte:
   ```
   Select a project folder:
   
   Projects
     > MeineFotos
       Hochzeit2025
       Landschaften (not configured)
   Setup
     Create new project folder
   Exit
   ```
3. Wähle das Projekt, mit dem du arbeiten möchtest

**Hinweis:** Projekte mit "(not configured)" haben noch keine `project.json` - der Projekt-Assistent wird gestartet, wenn du sie auswählst.

### Projekte verwalten

Vom Hauptmenü aus gehe zu **Setup** → **projects**:

| Option | Beschreibung |
|--------|--------------|
| **list** | Alle Projekt-Ordner mit Status anzeigen |
| **create** | Neuen Projekt-Ordner erstellen |
| **delete** | Projekt-Ordner löschen (mit Bestätigung) |

### Projekt-Struktur

```
C:\Revela\
├── revela.exe
├── revela.json                    ← Globale Konfiguration (geteilt)
├── packages/                      ← Themes & Plugins (geteilt)
└── projects/
    ├── MeineFotos/                ← Projekt 1
    │   ├── project.json
    │   ├── site.json
    │   ├── source/
    │   ├── output/
    │   └── cache/
    └── Hochzeit2025/              ← Projekt 2
        ├── project.json
        ├── site.json
        ├── source/
        ├── output/
        └── cache/
```

**Vorteile:**
- Separate Foto-Sammlungen in verschiedenen Projekten
- Jedes Projekt kann unterschiedliche Einstellungen haben (Theme, Bildgrößen)
- Geteilte Pakete - einmal installieren, überall nutzen

---

## 10. Konfigurationsdateien

### project.json

Technische Einstellungen für dein Projekt:

```json
{
  "name": "MeinPortfolio",
  "url": "https://www.meine-website.de",
  "theme": {
    "name": "Lumina"
  },
  "generate": {
    "images": {
      "formats": {
        "webp": 85,
        "jpg": 90
      },
      "sizes": [640, 1024, 1280, 1920, 2560],
      "minWidth": 800,
      "minHeight": 600
    }
  }
}
```

| Einstellung | Bedeutung |
|-------------|-----------|
| `name` | Projektname |
| `url` | Website-Basis-URL |
| `theme.name` | Aktives Theme |
| `formats` | Bildformate mit Qualität (0-100) |
| `sizes` | Bildbreiten in Pixeln |
| `minWidth/minHeight` | Kleinere Bilder ignorieren (filtert Thumbnails) |

### site.json

Website-Metadaten:

```json
{
  "title": "Max Mustermann Fotografie",
  "author": "Max Mustermann",
  "description": "Professionelle Hochzeits- und Portraitfotografie",
  "copyright": "© 2025 Max Mustermann"
}
```

### revela.json

Globale Revela-Konfiguration (im Revela-Ordner):

```json
{
  "feeds": [
    {
      "name": "Official",
      "url": "https://nuget.pkg.github.com/spectara/index.json"
    }
  ]
}
```

---

## 11. Häufige Probleme

### "Das Menü erscheint nicht" oder "Fenster schließt sich sofort"

**Ursachen:**
- Revela ist vor dem Laden abgestürzt
- Fehlende Abhängigkeiten

**Lösung:** Versuche, von der Kommandozeile zu starten, um Fehlermeldungen zu sehen:
1. Öffne PowerShell im Revela-Ordner
2. Führe `.\revela.exe` aus
3. Prüfe die Fehlermeldung

### "Keine Bilder gefunden"

**Ursache:** Der `source`-Ordner ist leer oder Bilder sind nicht in Unterordnern.

**Lösung:** Erstelle mindestens einen Unterordner mit Bildern:
```
source/
└── 01 Meine Fotos/
    └── bild.jpg
```

### "Fehler beim Verarbeiten von Bildern"

**Ursachen:**
- Beschädigte Bilddatei
- Nicht unterstütztes Format (nur JPG, PNG, TIFF unterstützt)
- Sehr große Bilder (>100 MP) können Speicherprobleme verursachen

**Lösung:** Prüfe die Fehlermeldung - sie zeigt an, welches Bild das Problem verursacht.

### Website sieht anders aus als erwartet

**Ursachen:**
- Browser-Cache zeigt alte Version
- Konfigurationsfehler

**Lösungen:**
1. Drücke **Strg+F5** im Browser (Hard Refresh)
2. Führe **clean** → **all**, dann **generate** → **all** aus
3. Prüfe `site.json` und `project.json` auf Tippfehler

### "Keine Themes verfügbar" im Assistenten

**Ursache:** Paketindex nicht geladen oder Netzwerkproblem.

**Lösung:**
1. Prüfe deine Internetverbindung
2. Führe **packages** → **refresh** im Menü aus
3. Oder starte den Assistenten erneut via **Addons** → **wizard**

---

## 12. Nächste Schritte

Wenn deine Website funktioniert:

- **Theme anpassen:** Wähle **theme** → **extract** um eine eigene Kopie zu erstellen
- **Weitere Plugins installieren:** Wähle **plugins** → **install**
- **Einstellungen ändern:** Wähle **config** für alle Konfigurationsoptionen
- **Auf Server hochladen:** Kopiere den Inhalt von `output/` per FTP/SFTP

---

## Hilfe & Support

- **GitHub Issues:** https://github.com/spectara/revela/issues
- **Dokumentation:** https://github.com/spectara/revela/tree/main/docs

Bei Fragen oder Problemen erstelle gerne ein Issue auf GitHub!

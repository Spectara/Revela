# Revela - Erste Schritte

Eine Schritt-für-Schritt-Anleitung für Fotografen, um mit Revela eine Portfolio-Website zu erstellen.

---

## Was ist Revela?

**Revela** ist ein Programm, das aus deinen Fotos automatisch eine schöne Portfolio-Website erstellt. Du legst deine Bilder in Ordner, startest Revela, und bekommst eine fertige Website mit:

- Automatisch skalierten Bildern (verschiedene Größen für schnelles Laden)
- Moderner Galerie-Ansicht mit Lightbox
- Responsivem Design (funktioniert auf Handy, Tablet, Desktop)
- Schneller Ladezeit durch optimierte Bildformate (AVIF, WebP, JPG)

**Wichtig:** Revela ist ein **Kommandozeilen-Programm** (CLI = Command Line Interface). Das bedeutet:
- Es hat **keine grafische Oberfläche** mit Fenstern und Buttons
- Du steuerst es über **Textbefehle** in der Kommandozeile (CMD oder PowerShell)
- Wenn du `revela.exe` doppelklickst, passiert scheinbar nichts – das ist normal!

---

## 1. Installation (Windows)

### Schritt 1.1: Revela herunterladen

1. Gehe zu den **GitHub Releases**: https://github.com/spectara/revela/releases
2. Lade die neueste Version herunter:
   - `revela-win-x64.zip` für Windows (64-bit)
3. Entpacke die ZIP-Datei in einen Ordner deiner Wahl, z.B.:
   - `C:\Revela\`
   - oder `D:\Tools\Revela\`

Nach dem Entpacken hast du folgende Dateien:
```
C:\Revela\
├── revela.exe                          ← Das Hauptprogramm
├── Spectara.Revela.Theme.Lumina.dll    ← Das Standard-Theme
└── QUICKSTART.md                       ← Kurzanleitung (Englisch)
```

### Schritt 1.2: Installation testen

1. Öffne die **Kommandozeile im Revela-Ordner**:
   
   **Einfachste Methode (empfohlen):**
   - Öffne den Ordner `C:\Revela` im Windows Explorer
   - Rechtsklick auf eine leere Stelle im Ordner
   - Wähle **"Im Terminal öffnen"** (Windows 11) oder **"PowerShell-Fenster hier öffnen"** (Windows 10)
   
   **Alternative über Ausführen-Dialog:**
   - Drücke `Windows + R`
   - Tippe `cmd` und drücke Enter
   - Navigiere zum Revela-Ordner: `cd C:\Revela`
   
2. Teste ob Revela funktioniert:
   ```
   .\revela.exe --version
   ```
   
   Du solltest die Versionsnummer sehen, z.B.:
   ```
   revela 1.0.0
   ```

3. Zeige alle verfügbaren Befehle:
   ```
   .\revela.exe --help
   ```

---

## 2. Projekt erstellen

### Schritt 2.1: Projekt initialisieren

Öffne die Kommandozeile im Revela-Ordner (wie in Schritt 1.2 beschrieben) und führe aus:

```
.\revela.exe init project
```

Revela erstellt automatisch die Grundstruktur:

```
C:\Revela\
├── revela.exe                          ← Das Hauptprogramm
├── Spectara.Revela.Theme.Lumina.dll    ← Das Standard-Theme
├── QUICKSTART.md                       ← Kurzanleitung (Englisch)
├── project.json                        ← Projekt-Einstellungen (neu)
├── site.json                           ← Website-Informationen (neu)
├── source/                             ← Hier kommen deine Fotos rein (neu)
│   └── (leer)
└── output/                             ← Hier landet die fertige Website (neu)
    └── (leer)
```

---

## 3. Fotos hinzufügen

### Schritt 3.1: Galerien als Ordner anlegen

Erstelle im `source`-Ordner Unterordner für deine Galerien. Die Ordnernamen werden zu Galerie-Titeln:

```
C:\Revela\source\
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

- **Nummerierung:** Die Zahlen am Anfang (`01`, `02`, `03`) bestimmen die Reihenfolge im Menü
- **Ordnername = Galerie-Titel:** "01 Hochzeiten" wird zu "Hochzeiten" auf der Website
- **Untergalerien:** Du kannst auch verschachtelte Ordner anlegen:
  ```
  source/
  └── 01 Events/
      ├── 01 Hochzeit Lisa & Tom/
      └── 02 Firmenfeier Müller AG/
  ```

### Schritt 3.2: Galerie-Beschreibung hinzufügen (optional)

Um einer Galerie einen eigenen Titel oder eine Beschreibung zu geben, erstelle eine `_index.md` Datei im Galerie-Ordner:

**Datei:** `source/01 Hochzeiten/_index.md`
```markdown
---
title: Hochzeitsfotografie
description: Emotionale Momente für die Ewigkeit festgehalten
---

Jede Hochzeit erzählt ihre eigene Geschichte. Hier findest du 
eine Auswahl meiner schönsten Hochzeitsbilder.
```

Die Felder bedeuten:
| Feld | Bedeutung |
|------|-----------|
| `title` | Überschreibt den Ordnernamen als Titel |
| `description` | Beschreibungstext für die Galerie |

Der Text unter den `---` Linien wird als Einleitungstext auf der Galerie-Seite angezeigt.

---

## 4. Konfiguration anpassen

### Schritt 4.1: Website-Informationen (site.json)

Öffne `site.json` mit einem Texteditor (Notepad, VS Code, etc.) und passe die Werte an:

```json
{
  "title": "Max Mustermann Fotografie",
  "author": "Max Mustermann",
  "description": "Professionelle Hochzeits- und Portraitfotografie in München",
  "copyright": "© 2025 Max Mustermann"
}
```

| Feld | Bedeutung |
|------|-----------|
| `title` | Titel der Website (erscheint im Browser-Tab) |
| `author` | Dein Name |
| `description` | Kurzbeschreibung für Suchmaschinen |
| `copyright` | Copyright-Hinweis im Footer |

### Schritt 4.2: Projekt-Einstellungen (project.json)

Die `project.json` enthält technische Einstellungen. Für den Anfang kannst du die Standardwerte belassen:

```json
{
  "name": "MeinPortfolio",
  "url": "https://www.meine-website.de",
  "theme": "Lumina",
  "generate": {
    "images": {
      "formats": {
        "avif": 80,
        "webp": 85,
        "jpg": 90
      },
      "sizes": [640, 1024, 1280, 1920, 2560]
    }
  }
}
```

**Was bedeuten die Bildeinstellungen?**

| Einstellung | Bedeutung |
|-------------|-----------|
| `formats` | Welche Bildformate erstellt werden (AVIF, WebP, JPG) |
| Die Zahlen (80, 85, 90) | Qualitätsstufe (0-100), höher = bessere Qualität, größere Dateien |
| `sizes` | Bildbreiten in Pixeln, die erstellt werden |

**Tipp für den Anfang:** AVIF bietet die beste Kompression, braucht aber deutlich länger zum Berechnen. Für einen schnellen ersten Test empfehlen wir nur JPG:
```json
"formats": {
  "jpg": 90
}
```

---

## 5. Website generieren

### Schritt 5.1: Generate-Befehl ausführen

Öffne die Kommandozeile im Revela-Ordner und führe aus:

```
.\revela.exe generate
```

**Was passiert jetzt?**

1. **Scan:** Revela findet alle Bilder in `source/`
2. **Bilder verarbeiten:** Jedes Bild wird in allen konfigurierten Größen und Formaten erstellt
3. **Seiten rendern:** HTML-Dateien werden aus den Templates generiert

Je nach Anzahl und Größe deiner Bilder kann das einige Minuten dauern. Du siehst einen Fortschrittsbalken:

```
Scanning...
✓ Found 47 images in 5 galleries

Processing images [████████████████████] 100% 47/47 - berge.jpg
Rendering pages   [████████████████████] 100% 12/12 - index.html

✓ Generation complete!
```

### Schritt 5.2: Nur Teile neu generieren (optional)

Wenn du nur kleine Änderungen gemacht hast, kannst du auch nur Teile neu generieren:

```
.\revela.exe generate scan      # Quelldateien scannen (immer zuerst, wenn sich Bilder geändert haben)
.\revela.exe generate images    # Nur Bilder neu verarbeiten
.\revela.exe generate pages     # Nur HTML-Seiten neu rendern
```

**Hinweis:** Wenn du Bilder hinzugefügt/gelöscht oder `_index.md` Dateien geändert hast, führe zuerst `generate scan` aus, damit Revela die Änderungen erkennt.

---

## 6. Ergebnis anschauen

### Schritt 6.1: Website im Browser öffnen

Nach dem Generieren findest du die fertige Website im `output`-Ordner:

```
C:\Revela\output\
├── index.html          ← Startseite
├── main.css
├── main.js
├── hochzeiten/
│   └── index.html
├── portraits/
│   └── index.html
├── images/
│   └── (alle verarbeiteten Bilder)
└── ...
```

**So öffnest du die Website:**

1. Gehe im Windows Explorer zu `C:\Revela\output\`
2. Doppelklicke auf `index.html`
3. Die Website öffnet sich in deinem Standard-Browser

### Schritt 6.2: Website auf einen Webserver hochladen

Um deine Website online zu stellen, lade den kompletten Inhalt des `output`-Ordners auf deinen Webserver (FTP, SFTP, etc.).

---

## 7. Nützliche Befehle

### Alle Befehle auf einen Blick

| Befehl | Beschreibung |
|--------|--------------|
| `.\revela.exe --help` | Zeigt alle verfügbaren Befehle |
| `.\revela.exe init project` | Neues Projekt erstellen |
| `.\revela.exe generate` | Website generieren |
| `.\revela.exe generate images` | Nur Bilder verarbeiten |
| `.\revela.exe generate pages` | Nur HTML-Seiten erstellen |
| `.\revela.exe clean` | Generierte Dateien löschen |
| `.\revela.exe clean --all` | Alles löschen (inkl. Cache) |
| `.\revela.exe theme list` | Verfügbare Themes anzeigen |

### Hilfe zu einzelnen Befehlen

```
.\revela.exe generate --help
.\revela.exe clean --help
.\revela.exe init --help
```

---

## Häufige Probleme

### "Das CMD-Fenster geht kurz auf und schließt sich wieder"

**Ursache:** Du hast `revela.exe` doppelgeklickt.

**Lösung:** Revela ist ein Kommandozeilen-Programm und muss über CMD oder PowerShell gestartet werden:

1. Drücke `Windows + R`
2. Tippe `cmd` und drücke Enter
3. Navigiere zum Revela-Ordner: `cd C:\Revela`
4. Führe den Befehl aus: `.\revela.exe generate`

### "Keine Bilder gefunden"

**Ursache:** Der `source`-Ordner ist leer oder die Bilder sind nicht in Unterordnern.

**Lösung:** Erstelle mindestens einen Unterordner in `source/` und lege Bilder hinein:
```
source/
└── 01 Meine Fotos/
    └── bild.jpg
```

### "Fehler beim Verarbeiten von Bildern"

**Mögliche Ursachen:**
- Beschädigte Bilddatei
- Nicht unterstütztes Format (nur JPG, PNG, TIFF werden unterstützt)
- Sehr große Bilder (>100 MP) können Speicherprobleme verursachen

**Lösung:** Überprüfe die Fehlermeldung in der Konsole. Sie zeigt an, welches Bild das Problem verursacht.

### Website sieht anders aus als erwartet

**Mögliche Ursachen:**
- Browser-Cache zeigt alte Version
- Fehler in der Konfiguration

**Lösungen:**
1. Drücke `Strg + F5` im Browser (Hard Refresh)
2. Führe `.\revela.exe clean --all` aus und generiere neu
3. Überprüfe `site.json` und `project.json` auf Tippfehler

---

## Nächste Schritte

Wenn deine Website funktioniert, kannst du:

- **Theme anpassen:** `.\revela.exe theme extract Lumina MeinTheme` erstellt eine Kopie zum Bearbeiten
- **Plugins installieren:** Für erweiterte Funktionen wie OneDrive-Integration oder Statistiken
- **Auf einen Server hochladen:** Den `output`-Ordner per FTP/SFTP hochladen

---

## Hilfe & Support

- **GitHub Issues:** https://github.com/spectara/revela/issues
- **Dokumentation:** https://github.com/spectara/revela/tree/main/docs

Bei Fragen oder Problemen erstelle gerne ein Issue auf GitHub!

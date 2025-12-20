# Revela - Erste Schritte

Eine Schritt-für-Schritt-Anleitung für Fotografen, um mit Revela eine Portfolio-Website zu erstellen.

---

## Was ist Revela?

**Revela** ist ein Programm, das aus deinen Fotos automatisch eine schöne Portfolio-Website erstellt. Du legst deine Bilder in Ordner, startest Revela, und bekommst eine fertige Website mit:

- Automatisch skalierten Bildern (verschiedene Größen für schnelles Laden)
- Moderner Galerie-Ansicht mit Lightbox
- Responsivem Design (funktioniert auf Handy, Tablet, Desktop)
- Schneller Ladezeit durch optimierte Bildformate (AVIF, WebP, JPG)

**Einfach zu bedienen:** Wenn du `revela.exe` doppelklickst, öffnet sich ein **interaktiver Modus** mit einer menügesteuerten Oberfläche. Wähle einfach aus, was du tun möchtest - keine Kommandozeilen-Kenntnisse erforderlich!

---

## 1. Installation (Windows)

### Schritt 1.1: Revela herunterladen

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
├── Spectara.Revela.Theme.Lumina.dll    ← Das Standard-Theme
└── getting-started/                    ← Anleitungen (mehrsprachig)
    ├── README.md
    ├── de.md                           ← Deutsch
    └── en.md                           ← English
```

### Schritt 1.2: Installation testen

1. Öffne den Ordner `C:\Revela` im Windows Explorer
2. Doppelklicke auf `revela.exe`
3. Der **interaktive Modus** öffnet sich mit einem Menü

Du solltest einen Willkommensbildschirm mit Optionen wie:
- generate
- clean
- init
- theme

**Das war's!** Du kannst jetzt über das Menü durch Revela navigieren.

---

## 2. Projekt erstellen

### Schritt 2.1: Projekt initialisieren

1. Doppelklicke `revela.exe` um den interaktiven Modus zu öffnen
2. Wähle **init** aus dem Menü
3. Wähle **project**

Revela erstellt automatisch die Grundstruktur:

```
C:\Revela\
├── revela.exe                          ← Das Hauptprogramm
├── Spectara.Revela.Theme.Lumina.dll    ← Das Standard-Theme
├── getting-started/                    ← Anleitungen
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
      "sizes": [640, 1024, 1280, 1920, 2560],
      "minWidth": 800,
      "minHeight": 600
    }
  }
}
```

**Was bedeuten die Bildeinstellungen?**

| Einstellung | Bedeutung |
|-------------|-----------|
| `formats` | Welche Bildformate erstellt werden (AVIF, WebP, JPG) |
| Die Zahlen (80, 85, 90) | Qualitätsstufe (0-100), höher = bessere Qualität, größere Dateien |
| `sizes` | Bildbreiten in Pixeln, die erstellt werden || `minWidth` | Minimale Bildbreite in Pixeln (kleinere Bilder werden ignoriert) |
| `minHeight` | Minimale Bildhöhe in Pixeln (kleinere Bilder werden ignoriert) |

**Tipp:** Verwende `minWidth` und `minHeight`, um Vorschau-/Thumbnail-Dateien herauszufiltern, die manche Programme oder Handys neben deine Fotos legen.
**Tipp für den Anfang:** AVIF bietet die beste Kompression, braucht aber deutlich länger zum Berechnen. Für einen schnellen ersten Test empfehlen wir nur JPG:
```json
"formats": {
  "jpg": 90
}
```

---

## 5. Website generieren

### Schritt 5.1: Website generieren

1. Doppelklicke `revela.exe` um den interaktiven Modus zu öffnen
2. Wähle **generate** aus dem Menü
3. Wähle **all** um die vollständige Pipeline auszuführen

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

Im **generate**-Untermenü kannst du auch einzelne Schritte auswählen:

| Menü-Option | Was sie macht |
|-------------|---------------|
| **all** | Vollständige Pipeline (scan → statistics → pages → images) |
| **scan** | Quelldateien scannen (zuerst ausführen wenn Bilder geändert) |
| **statistics** | Statistik-JSON generieren (erfordert Statistics-Plugin) |
| **pages** | Nur HTML-Seiten neu rendern |
| **images** | Nur Bilder neu verarbeiten |

**Hinweis:** Wenn du Bilder hinzugefügt/gelöscht oder `_index.md` Dateien geändert hast, führe zuerst **scan** aus, damit Revela die Änderungen erkennt.

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

## 7. Menü-Übersicht

### Hauptmenü-Optionen

| Menü | Untermenü | Beschreibung |
|------|-----------|-------------|
| **generate** | all | Website generieren (vollständige Pipeline) |
| | scan | Quelldateien scannen |
| | images | Nur Bilder verarbeiten |
| | pages | Nur HTML-Seiten erstellen |
| **clean** | all | Alles löschen (output + cache) |
| | output | Nur output-Verzeichnis löschen |
| | cache | Nur cache-Verzeichnis löschen |
| **init** | project | Neues Projekt erstellen |
| **theme** | list | Verfügbare Themes anzeigen |
| | extract | Eigene Theme-Kopie erstellen |

---

## Häufige Probleme

### "Das Menü erscheint nicht" oder "Fenster schließt sich sofort"

**Mögliche Ursachen:**
- Revela ist abgestürzt bevor das Menü laden konnte
- Fehlende Abhängigkeiten

**Lösung:** Überprüfe ob eine Fehlermeldung im Fenster erscheint bevor es sich schließt. Möglicherweise musst du Revela neu installieren oder die GitHub Issues nach bekannten Problemen durchsuchen.

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
2. In Revela: wähle **clean** → **all**, dann **generate** → **all**
3. Überprüfe `site.json` und `project.json` auf Tippfehler

---

## Nächste Schritte

Wenn deine Website funktioniert, kannst du:

- **Theme anpassen:** In Revela wähle **theme** → **extract** um eine eigene Theme-Kopie zu erstellen
- **Plugins installieren:** Für erweiterte Funktionen wie OneDrive-Integration oder Statistiken
- **Auf einen Server hochladen:** Den `output`-Ordner per FTP/SFTP hochladen

---

## Hilfe & Support

- **GitHub Issues:** https://github.com/spectara/revela/issues
- **Dokumentation:** https://github.com/spectara/revela/tree/main/docs

Bei Fragen oder Problemen erstelle gerne ein Issue auf GitHub!

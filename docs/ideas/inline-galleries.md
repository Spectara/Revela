# Spike (WIP): Inline-Galerien im Markdown-Content

> **Status:** Design-/Analyse-Notiz, KEINE Implementierung. Ablageort provisorisch
> (`docs/ideas/`) — später sauber einsortieren (evtl. GitHub-Issue oder Kommentar zu #77).
> Zusammengetragen 2026-07-17 aus: erstem Design-Entwurf, zwei Review-Runden mit dem
> `spike-analyst`-Agent (read-only, gegen echten Code), und der Verzahnung mit Issues #75/#76/#77.

## Ziel / Nutzerwunsch

Galerien direkt im Markdown-Body platzieren können, statt nur „Body, dann automatisch ein
Grid am Ende". Ermöglicht Text → Bilder → Text und mehrere, unterschiedlich gefilterte
Galerien auf einer Seite:

```text
Meine Strandbilder:

[[gallery: contains(sourcePath, 'strand/') | sort dateTaken desc]]

Und aus den Bergen:

[[gallery: width > height | limit 6]]
```

Konkreter Auslöser: Homepage soll „Hero → Fotos → warum es cool ist" zeigen — heute nicht
möglich, weil `Gallery.Body` EIN Block ist und das Grid immer dahinter/getrennt gerendert wird.

## Token-Semantik (Entwurf)

- `[[gallery]]` — Standardmenge der Seite an DIESER Position (normale Galerie: `gallery.Images`;
  virtuelle Galerie: Frontmatter-`filter`-Ergebnis; Homepage: definierte effektive Menge).
- `[[gallery: <filter>]]` — gefiltert aus ALLEN kanonischen Bildern; nutzt die BESTEHENDE
  Filtergrammatik (`and/or/not`, Funktionen, `sort`, `limit`).
- **Opt-in & abwärtskompatibel:** kein Token → exakt heutiges Verhalten.
- Sobald ≥1 Token vorhanden ist → automatisches Trailing-Grid unterdrücken (Flag
  `gallery.has_inline_galleries`); Core darf NICHT `images=[]` setzen (bricht Custom-Themes).

## Empfohlene Architektur

- Eigener **Markdig-BlockParser** (kein Regex/String-Split). Wichtig: die vorhandene
  `ContentImageExtension` ist nur ein Renderer-Swap für INLINE-Bilder (`![]()`), KEIN
  Blockparser — den braucht es hier neu (im Repo bisher unerprobt → Hauptaufwandsrisiko).
- Kontext-Wiring 1:1 vom `ContentImageContext`-Record übernehmen (Delegat-Callback statt
  hartem Theme-Coupling), ABER um Zugriff auf den GLOBALEN Bildpool erweitern
  (`IManifestRepository.Images`) — heute kennt der Kontext nur gallery-lokale Bilder.
- Neue Theme-Partial `Partials/GalleryGrid.revela` (nutzt weiter `Partials/Image.revela`);
  Grid-HTML wird Teil von `Gallery.Body`.

## Parsing-Regeln (MVP)

Nur einzeilige, alleinstehende Top-Level-Blöcke. NICHT als Token behandeln: in Codeblock,
Inline-Code, Absatz, Liste, Blockquote; `\[[gallery]]` = escaped. Ungültiges standalone Token
→ Build-Abbruch mit Datei+Zeile. Leere Treffermenge = kein Fehler, kein leeres Grid-Markup.

## Determinismus Filter → sort → limit

Reihenfolge fest: (1) Prädikat, (2) expliziter `sort` ODER effektiver Gallery-/Global-Sort,
(3) erst danach `limit`. Frontmatter- und Inline-Pfad sollten denselben, EINMAL berechneten
Ausführungspfad teilen (siehe #77-Invariante unten).

## Bewertung der Review-Punkte (spike-analyst, gegen echten Code)

1. **ContentImageExtension als Vorbild — bestätigt, mit Einschränkung.** Kontext-Muster
   übertragbar; aber es ist ein Renderer-Swap, kein Blockparser. Filter braucht globalen Pool,
   nicht nur lokalen `ImagesBySourcePath` (RenderService baut Kontext pro Gallery vor dem
   Markdown-Rendering, ~Z.522/568).
2. **Sticky-Reveal — teilweise widerlegt (gut).** Effekt sitzt pro `<article>`/`<picture>`
   (main.css ~Z.538–560, 710–719), NICHT am `.gallery`-Container → mehrere Grids mit Text
   dazwischen brechen sich nicht gegenseitig. Restrisiko: `.gallery > h2` nutzt
   `box-shadow`/`outline` mit `--color-bg` als Deckungstrick (~Z.527–533) — für Text NACH
   einem Grid prüfen, ob derselbe Trick nötig ist, sonst „durchscheinen".
   HINWEIS: Unser `main:has(.gallery)+footer`-Fix liegt im WEBSITE-Override
   (`samples/revela-website/...`), NICHT im Basis-Theme — daher website-spezifisch, kein
   generelles Feature-Thema.
3. **Incremental Builds — bestätigte offene Lücke, kein MVP-Blocker.** Kein Page→Page-
   Dependency-Tracking (nur Hash pro Bild + globaler ConfigHash); heute wird eh alles neu
   gerendert. Sobald es echtes inkrementelles Rendering gibt, wird ein Inline-Filter zur
   ungetrackten Cross-Page-Abhängigkeit → als „Known Limitation (Post-MVP)" dokumentieren.

## Weitere blinde Flecken (spike-analyst)

- **RSS/Sitemap/OG-Image**-Generatoren gehen vermutlich von `gallery.Images`/Trailing-Grid aus
  → müssen auf `has_inline_galleries` reagieren, sonst falsche Vorschaubilder.
- **Lightbox prev/next** (`:target`): Navigations-/Tab-Reihenfolge über Grid-Grenzen definieren
  — ABER siehe #77: die `:target`-Lightbox wird dort ganz entfernt (Punkt entschärft sich).
- **Block-Parser** ist der eigentliche Aufwandstreiber (im Repo unerprobt).

## Verzahnung mit dem Photo-Pages-Epic (#77) + Vorstufen #75/#76

- **#75 SiteCoreConfig:** `project.json` (Build) vs. `site.json` (Inhalt/Identität) trennen,
  typisierte `SiteCoreConfig` via `IOptions<>`.
- **#76 URL-Model-Refactor:** `image.url → image.slug`, `image_basepath → assets_basepath`,
  konsolidierte Helper (`page_url`, `absolute_url`, `variant_url`, `asset_url`). BREAKING für
  alle Lumina-Templates.
- **#77 Kanonische Photo-Pages:** jedes Bild eine eigene Seite `/photo/{slug}/`; Galerie-
  Vorkommen werden zu „Contexts" (prev/up/next); **Lumina `:target`-Lightbox wird ENTFERNT**
  (Thumbnails werden Links auf Photo-Page). Filter/Sort/Limit werden NICHT erneut ausgewertet —
  Kontextreihenfolge kommt aus dem finalen `Gallery.Images`.

### Reihenfolge (strikt sequenziell, keine Parallelität)

`#75 → #76 → #77` — alle fassen Render-Context/Templates an (Merge-Konflikte sonst). #76 muss
laut Issue vor #77 (dessen `page_url(image)` IST #76s Ergebnis).

### Wo hängt [[gallery]] ein?

**Nach #76, vor/parallel zu #77 — nicht davor.**
- Nutzt die Helper, die #76 gerade umbenennt → vorher bauen = sofortiger Rework.
- Vor #77 einklinken, damit „zählt ein Inline-Grid als eigener `PhotoContext`?" von Anfang an
  mitgedacht wird (statt Nachpatchen). Sequencing: Inline-Filterung muss VOR der Photo-Page-
  Aggregation abgeschlossen sein.

### Auswirkung auf frühere Punkte

- Lightbox-ID-Kollision (früher Punkt c) **hinfällig**, sobald #77 landet (keine In-Page-
  Lightbox mehr). Nur relevant, falls Inline VOR #77 kommt → Epic-Reihenfolge klären.
- Determinismus-Punkt (a) **verschärft**: Inline-Ergebnisse müssen in die Photo-Context-
  Aggregation einfließen, sonst kriegt ein „nur-Inline"-Bild keine Photo-Page/keine prev/next.

## Geteilte Fundamente — EINMAL bauen, mehrfach nutzen

1. **Globaler Bildpool-Kontext** (zentraler „alle publizierten Bilder"-Zugriff statt lokalem
   `ImagesBySourcePath`) → gebraucht von Inline-`[[gallery]]` UND #77.
2. **Vereinheitlichte Filter-Engine** (Frontmatter = Inline, EINMAL berechnetes, determinist-
   isches Ergebnis) → Voraussetzung für #77s „nicht erneut auswerten"-Invariante.
3. **URL-Helper-Konsolidierung** (= #76 selbst) → `GalleryGrid.revela` (Inline) UND
   `Photo.revela` (#77) müssen `page_url`/`variant_url` nutzen.

**Empfohlene Gesamtreihenfolge:** `#75 → #76` abschließen → dann globaler Bildpool-Kontext +
Filter-Engine-Vereinheitlichung als gemeinsame Basis → dann Inline-Galerie UND #77 darauf.

## Aufwand

Für Inline-Galerie isoliert: ~4–6 Entwicklertage (Blockparser, Sortsemantik, Theme-Vertrag,
Fehlerbehandlung, Tests, Doku). ABER: isoliert bauen = doppelte Rework-Kosten — daher an die
Kette oben koppeln.

## Testfälle (aus dem Entwurf)

kein Token = bisheriges HTML · nacktes Token = Standardmenge · mehrere Tokens = versch. Mengen ·
Filter mit sort+limit · limit ohne sort · leere Ergebnismenge · ungültiger Filter mit Datei+Zeile ·
Token in Codeblock / escaped · gleiches Bild in mehreren Grids · Homepage-Frontmatter-Filter ohne
Treffer · `_images` + Fremdgalerie-Bilder · Desktop/Mobile mit Text vor/zwischen/nach Grids.

## Offene Fragen (nächste Denkrunde)

- Zählt ein Inline-Grid als eigener `PhotoContext` (eigener Anker/Fragment) oder geht es im
  Eltern-Gallery-Context auf? (#77-kritisch, aktuell unspezifiziert)
- Wie sieht der gemeinsame „globaler Bildpool"-Service konkret aus (Schnittstelle, Ort)?
- Block- vs. kurzes Token-Format (`[[gallery: …]]` vs. `:::gallery … :::`) — reicht MVP-Token?

## ENTSCHEIDUNGEN (2026-07-17, aus Multi-Modell-Debatte + UX-Review)

Verfahren: Spike-Analyst-Agent (Architektur) in mehreren Runden; Streit-Runde Opus 4.8 vs.
GPT-5.6-sol (beide long_context, xhigh); neuer UX-Advocate-Agent (Besucher- + Fotografen-Sicht);
finale Schlichtungsrunde. Alle read-only, gegen echten Code.

### D1 — Kontext-Default: HYBRID (final, beide Agenten + beide Brillen konvergent)
> Nacktes `[[gallery]]` gehört zur SEITEN-Kette (ein Kontext, 1:1 zu #77).
> Jedes gefilterte/benannte `[[gallery: <filter>]]` bekommt eine EIGENE prev/next-Kette + Anker.
- Regel knüpft an PRÄSENZ eines Filter-Ausdrucks (im Parser deterministisch), NICHT an Mengenvergleich.
- Rationale: „getrennt navigieren" = „sieht getrennt aus"; getrenntes Aussehen kann nur durch
  Filter entstehen. Häufigster Fall (nacktes Token) = null Extra-#77-Kontexte; Extra-Kosten nur
  bei bewusst gefilterten Grids (proportional zum Nutzen).
- Ehrt den ursprünglichen User-Instinkt „ein Grid = ein Kontext" — aber nur dort, wo er zählt.
- Anker-Schema braucht `grid{n}`/named-Segment NUR für den gefilterten Pfad; Normalfall unberührt.

### D2 — Heading-Inferenz VERWORFEN (Post-MVP, evtl. nie)
Automatische Kontext-Herleitung aus vorangehender Überschrift war die Sollbruchstelle
(Markdig `UseAutoIdentifiers` erzeugt eigene IDs → Kollision; gleiche Überschrift + versch. Filter
→ Label lügt; h2/h3-Mehrdeutigkeit). Explizit-oder-Seite statt magisch.

### D3 — Explizites Label: `as "..."`, NICHT `context:`
Wort „context" und `#ctx-`-Anker aus ALLEN nutzer-/besucher-sichtbaren Flächen + URLs entfernen
(nur internes Modell; deckt #77 §4: Fragment ist UI-State, nie in canonical/sitemap).
Wenn ein sichtbares Label gewünscht ist: explizite, offensichtlich besucher-gerichtete Syntax,
z.B. `[[gallery: strand/ as "Strandtage"]]` — „dieser Text wird Besuchern gezeigt" ist dann
selbsterklärend statt geleaktes internes Token.

### D4 — Eine Filtersprache, überall identisch
Dieselbe Filtergrammatik in Frontmatter (`filter = "..."`) UND Inline-Token — kein zweiter Dialekt.
Filter-Ergonomie (fotografen-freundliche Ordner-Kurzform wie `[[gallery: strand/]]` statt
`contains(sourcePath,'strand/')`) ist ein SEPARATES Filter-Engine-Issue, das BEIDE Oberflächen
verbessert. Inline-Feature wartet NICHT darauf; nutzt die jeweils aktuelle Syntax.
UX-Befund: die heutige Grammatik (`contains()`, `width > height`, Pipe) ist für Fotografen
Entwickler-Sprech → Kurzform-Issue ist sinnvoll, aber entkoppelt.

### D5 — Leere Treffermenge: BUILD-WARNUNG (nicht still, nicht Fehler)
Entwurf sagte „kein Fehler"; UX-Review: stiller leerer Grid = Autor sieht nichts, versteht nichts.
Mittelweg: Warnung „matched 0 photos" beim Build.

### D6 — Rendern überall, Kontext nur aus Default-Body
`[[gallery]]` als Layout in Custom-Body-Seiten (`Page.revela`, Doku) erlaubt (rendert Bilder),
erzeugt dort aber KEINE #77-Kontexte/Photo-Pages (deckt #77s Eligibility-Gate: effektives
Template = default gallery body). Kein Blockieren des Renderings in Doku-Seiten.

### D7 — MVP-Schnitt
MVP: Block-Parser + `[[gallery]]` / `[[gallery: <filter>]]`; Trailing-Grid via
`has_inline_galleries` unterdrücken; Hybrid-Kontext (D1); `FilterService.ApplyQuery`
wiederverwenden (einmal berechnen, einfrieren für #77); Build-Warnung (D5).
Post-MVP: Heading-Inferenz, Multi-Grid-Merge über gleiche Namen, occurrence-genaue up-Anker bei
mehrfach sichtbarem Bild, `as "..."`-Label falls nicht schon im MVP billig machbar.

### Sequencing (unverändert bestätigt)
`#75 → #76` zuerst; dann geteilte Fundamente (globaler Bildpool-Kontext + Filter-Engine bereit);
dann Inline-Galerie NACH #76, vor/parallel zu #77 (damit Photo-Context-Kopplung von Anfang an
mitgedacht wird). Inline-Filterung muss VOR #77-Photo-Page-Aggregation abgeschlossen sein.

### Minimales Datenmodell (Konsens)
- `Gallery`: nur Flag `HasInlineGalleries`; AST/Grids in privatem `PreparedGalleryPage`.
- `InlineGrid { Anchor, Label?, Images[], DocumentOrder }` (Render-Record, nicht im Manifest).
- `#77 PhotoContext` von `GallerySlug` generalisieren zu `(Route, Fragment, Label)`, damit ein
  Kontext auf ein Seiten-Fragment zeigen kann (nicht nur eine Galerie).
- KEIN `images=[]` im Core setzen (bricht Custom-Themes); KEine Filterausdrücke im Photo-Modell.

### Offene Rest-Punkte
- Ist `as "..."`-Label im MVP billig genug (hängt an Anker-Namespace `grid{n}` sowieso)?
- Ordner-Kurzform-Syntax exakt (`strand/` vs. `folder:strand`) — eigenes Filter-Engine-Issue.
- Prozess-Note: UX-Advocate-Agent (`.github/agents/ux-advocate.agent.md`) in dieser Session
  NEU erstellt (experimentell) — Team muss entscheiden, ob er dauerhaft ins Repo gehört.

## EDGE CASES (2026-07-17, am Code belegt)

### E1 — Zwei nackte `[[gallery]]` auf einer Seite
Beide lösen dieselbe Standardmenge auf → zwei identische Grids (Beleg Normalmodus =
Ordnerbilder: `ContentService.cs:585-586`, gerendert `Gallery.revela:22-29`). Besucher sieht
Bilder doppelt, Seiten-Kette dedupliziert auf einmal → Inkonsistenz.
**Regel: tolerieren + Build-Warnung** („multiple bare [[gallery]] render identical sets; use a
filter to differentiate"). Kein harter Fehler (bewusste Layout-Wiederholung theoretisch denkbar).

### E2 — Filter-Scope: gefiltert = GLOBAL, nackt = LOKAL (bestehende Semantik)
FAKT am Code: Frontmatter-Filter wirkt GLOBAL — `ContentService.cs:565-567` filtert
`context.AllImages` (global aus `content.Images`, Z.376). OHNE Filter = LOKAL — Z.585-586
nutzt `folderImages` (nur dieser Ordner).
→ Inline übernimmt 1:1: `[[gallery: <filter>]]` = global; nacktes `[[gallery]]` = lokale
Seitenmenge. Diese Asymmetrie existiert bereits, wird NICHT neu erfunden.
Scope-Modifier (lokal vs. global, z.B. `scope: local`) ist eine VORBESTEHENDE Lücke (auch
Frontmatter-Filter kann heute nicht auf „nur dieser Ordner") → gehört ins gemeinsame
Filter-Engine-Issue, NICHT in den Inline-MVP.

### E3 — Default ohne Token (bestätigt)
Ohne Token hängt genau EIN Trailing-Grid unter den Content: `Gallery.revela:21-29`
(`if images && images.size > 0`). `has_inline_galleries`-Flag existiert noch NICHT im Code;
muss das Theme-Grid gaten und NUR greifen, wenn ≥1 Token vorhanden ist → kein Token = heutiges
Verhalten unverändert. Unterdrückung ist THEME-seitig (Core setzt Flag, Theme respektiert);
Core darf `images` NICHT leeren (bricht Custom-Themes).

### E4 — Gleiches Label auf mehreren Seiten
Zwei unabhängige, seitenlokale Ketten. Labels = reine Display-Strings, KEINE Identität;
„Strand" auf Seite A und B teilen nichts (billig, kein globaler Label-Index). Eine echte
seitenübergreifende Sammlung IST bereits die Filter-Galerie (eigene Seite/Permalink, global
scope). MVP: Inline-Labels seitenlokal, nie gemerged. Post-MVP (falls Bedarf): benanntes Label
→ generierte virtuelle Filter-Galerie mit Permalink (vorhandener Mechanismus), nicht das
Inline-Token-System erweitern.

### E5 — Token in Liste/Blockquote (nicht Top-Level)
Block-Parser matcht nur Top-Level → in verschachteltem Kontext bleibt `[[gallery: …]]` als
LITERALTEXT im Output. Schlecht nachvollziehbar. Regel: **Build-Warnung** bei nicht-geparstem
Token-artigem Text (`[[gallery`-Präfix) — „looks like a gallery token but is nested; only
top-level blocks are recognized". `\[[gallery]]` bleibt bewusster Escape. Kein harter Fehler.

### E6 — `[[gallery]]` in `template=page`-Seite mit eigenen Ordnerbildern
Lokale Bildmenge IST befüllt (`ContentService.cs:534-535`, für jede Gallery template-unabhängig;
`Page.revela` loopt nur normal nicht über `images`). Inline-Grid rendert (via `GalleryGrid.revela`),
erzeugt aber KEINE Photo-Pages (D6 + #77-Gate). Kein Handlungsbedarf, nur dokumentieren:
Inline-Grids auf Custom-Body-Seiten sind reine Layout-Elemente ohne Photo-Navigation.

### E7 — Filter matcht sehr viele Bilder ohne `limit`
Heute KEIN Default-Limit (`FilterService.cs:146-149`: `Take` nur bei gesetztem Limit) → alle
Treffer landen im Grid; gilt schon für Frontmatter-Filter. Regel: KEIN implizites Default-Limit
(würde still Bilder verschlucken + Frontmatter-Konsistenz brechen), stattdessen Build-Warnung ab
Schwelle (z.B. >100 ohne `limit`), im GEMEINSAMEN Filter-Verhalten (Frontmatter + Inline).

## THEME-IMPACT & THEME-KONTROLLE (2026-07-17, am Code belegt)

### T1 — Was ein Theme-Autor für Inline-Galerien NEU braucht
- Partial `Partials/GalleryGrid.revela` (rendert Grid, nutzt weiter `Image.revela`).
- Body-Template gatet Trailing-Grid mit `gallery.has_inline_galleries` (nur rendern wenn false).
- Partials werden per Dateiscan aufgelöst (`TemplateResolver.ScanTheme`) — „Datei = Vertrag",
  KEIN Manifest-Eintrag nötig (`ThemeManifest` trägt heute nur `LayoutTemplate`).

### T2 — Bestehende Custom-Themes ohne GalleryGrid.revela (Bruchgefahr)
**Graceful, kontextabhängig:** GalleryGrid nur dann Pflicht, wenn ein `[[gallery]]`-Token im
Content vorkommt UND die Partial fehlt → klarer Fehler mit Datei+Zeile. Themes ohne Token bleiben
unberührt (Opt-in-Prinzip). Unterschied zu `ContentImage` (hart-Pflicht via
`RenderService.cs:510-513`, weil `![]()` allgegenwärtig ist); Inline-Galerie ist Opt-in → weichere
Behandlung konsistenter. (`TemplateResolver.GetTemplate` gibt bei Fehlen null + Warn-Log,
`cs:55-59`.)

### T3 — Photo-Pages pro Bild (#77) sind bereits eine THEME-Entscheidung
Faktisch schon so gebaut: #77-Phase läuft nur, wenn das Theme `Body/Photo.revela` bereitstellt
(`TemplateResolver` liefert sonst null → Phase übersprungen). Zusätzlich Gate „effektives
Template = default gallery body".
- **Alpenstrasse konkret:** Custom-Panorama-Theme OHNE `Body/Photo.revela` + Custom-`home`-Body
  → #77 erzeugt NULL Photo-Pages. Kein Handlungsbedarf. Feature ist für Nicht-Portfolio-Themes
  standardmäßig „aus".
- **Mechanismus-Empfehlung: Konvention + Sicherheitsventil.** Photo-Pages an, wenn
  `Body/Photo.revela` auflösbar UND nicht per `theme.json`-Flag `"photoPages": false` deaktiviert.
  Default = Konvention (0 Aufwand, „Datei = Vertrag", konsistent mit `TemplateResolver`/ContentImage);
  Opt-out-Flag nur für den seltenen Fall (Theme erbt die Datei, will aber keine Detailseiten).
- **Verworfen:** C#-`ThemeManifest`-Property als Primärmechanismus — schließt Disk-basierte
  Custom-Themes aus, die über `theme.json` deklarieren.
- **Antwort auf „wie gibt das Theme das vor":** durch An-/Abwesenheit von `Body/Photo.revela`
  (+ optionales Opt-out-Flag). Kein neues Konzept.

## DATENPIPELINE / BILD-REPRÄSENTATIONEN (2026-07-17, am Code belegt)

Befürchtete Kern-Hürde: Filter läuft in Manifest-Phase auf `ImageContent`, aber `[[gallery]]`
lebt im Body-Rendering (Render-Phase, nur `Image`-Modell). Ergebnis der Analyse: **kleiner als
gedacht.**

- **Filterbarer Pool ist zur Render-Zeit BEREITS greifbar:** `BuildImageLookup` iteriert schon
  `manifestRepository.Images` (`RenderService.cs:443`). Render darf Manifest-Artefakte lesen.
- **Ein Konvertierungspunkt** `ImageContent → Image`: `Image.FromManifestEntry` (`Image.cs:97-112`).
  Kein EXIF-Feldverlust (Width/Height/DateTaken/Exif/Sizes überleben).
- **ABER: immer auf `ImageContent` filtern, nie auf Render-`Image`.** Grund: FilterService ist
  hart auf `ImageContent` typisiert (`FilterService.cs:44,58`); und `DateTaken`-Nullability
  kollabiert im Render-Modell (`DateTime?` → `MinValue`, `Image.cs:107`) → Sort/Null-Handling
  würde abweichen.
- **Weg (a) Pre-Render-Pass VERWORFEN:** Body wird bewusst erst zur Render-Zeit geladen (nicht im
  Manifest, `RenderService.cs:567`); ein früher AST-Pass verwischt die Phasentrennung + kein
  Body→Bildpool-Tracking im Inkrement-Hash-Modell. Hohes Risiko.
- **Weg (b) EMPFOHLEN (ImageContent-Pool zur Render-Zeit):** minimale Verrohrung, nutzt
  bestehende Engine + bestehenden Konvertierungspunkt, ordnet Pipeline NICHT um.

### Minimaler Umbau (= das geteilte Fundament aus D1-Kontext)
- KEIN neues Datenmodell. `ContentImageContext` (heute nur render-`Image` via `ImagesBySourcePath`,
  `ContentImageContext.cs:40-45`) erweitern um: `ImageContent`-Pool + Resolver
  (`filterExpr → geordnete render-Images`, via `FilterService.ApplyQuery` + `Image.FromManifestEntry`).
- Identischer Filter-Aufruf wie Frontmatter (`ContentService.cs:567`) → #77-Invariante „einmal
  auswerten" bleibt (der Token IST die eine Auswertung; Ergebnis eingefroren in
  `PreparedGalleryPage`/`Gallery.Images`).
- **Reihenfolge:** (1) Kontext-Erweiterung als Fundament ZUERST (nutzt Inline UND #77); (2) Inline
  darauf; (3) #77 konsumiert eingefrorene Memberships. Riskanten Reorder (a) NICHT vorziehen.
- **Aufwand Fundament: ~0,5–1 Entwicklertag** (Plumbing + Resolver). Eigentlicher Kostentreiber
  bleibt Markdig-Blockparser + Theme-Vertrag, NICHT die Datenversöhnung.

## Touchpoints (voraussichtlich)

`Features/Generate/Services/MarkdownService.cs` · neue `GalleryBlockExtension/Parser/Renderer` ·
neuer Gallery-Block-Kontext · `Features/Generate/Services/RenderService.cs` ·
`Features/Generate/Models/Gallery.cs` · `Infrastructure/Filtering/*` ·
`Themes/Lumina/Body/Gallery.revela` · neue `Themes/Lumina/Partials/GalleryGrid.revela` ·
`Themes/Lumina/Partials/Image.revela` · Unit-Tests neben `ContentImageTests` ·
E2E in `GenerateAllEndToEndTests` · Theme-/Feature-Doku.

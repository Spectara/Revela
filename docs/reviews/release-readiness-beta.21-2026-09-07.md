# Release-Readiness-Review: 0.0.1-beta.21

## Release-Blocker

> Historischer Review-Bericht vom **2026-09-07**, abgelegt am **2026-09-11**.
> Die Befunde und Pruefergebnisse beziehen sich auf den unten dokumentierten
> Arbeitsstand. Das Ablegen dieses Dokuments ist keine erneute Verifikation
> des heutigen Codes. Zeilenreferenzen bezeichnen den damals geprueften Stand.

### F1 - Kritisch: OneDrive-Cleanup kann Fotos ausserhalb des Source-Verzeichnisses loeschen

- **Stellen:** [DownloadAnalyzer.cs:178](../../src/Plugins/Source/OneDrive/Services/DownloadAnalyzer.cs#L178), [OneDriveSourceCommand.cs:198](../../src/Plugins/Source/OneDrive/Commands/OneDriveSourceCommand.cs#L198).
- **Ausloeser:** Unterhalb des Source-Verzeichnisses liegt eine Junction zu einem anderen Fotoverzeichnis. Die Dateien fehlen in der Remote-Liste, und der Benutzer bestaetigt `--clean`.
- **Auswirkung:** Die Orphan-Erkennung folgt der Verknuepfung. Die anschliessende Loeschung entfernt Originaldateien ausserhalb des konfigurierten Source-Verzeichnisses.
- **Beleg:** Mit der echten `DownloadAnalyzer.Analyze`-Methode reproduziert. Eine synthetische Datei im verknuepften Ziel wurde als Orphan ausgewaehlt; die vom Command verwendete `FileInfo.Delete()`-Operation entfernte diese Datei. Beide Testverzeichnisse lagen vollstaendig im eigenen Review-Artefaktbereich. Die Junction wurde anschliessend entfernt.
- **Reproduktion:** Zwei isolierte Verzeichnisse `source` und `external` anlegen, `external/keep.jpg` erzeugen, `source/library` als Junction auf `external` setzen. `Analyze` mit leerer Remote-Liste und `includeOrphans: true` ausfuehren. Pruefen, ob die zur Loeschung angebotene Datei ausserhalb von `source` liegt.
- **Empfehlung:** Verknuepfte Unterverzeichnisse bei der Enumeration ausschliessen und die tatsaechliche Pfadgrenze vor dem Loeschen pruefen.

### F2 - Kritisch: Konfigurationsupdates koennen den Projektstart dauerhaft verhindern

- **Stelle:** [ConfigService.cs:188](../../src/Commands/Config/Services/ConfigService.cs#L188).
- **Ausloeser:** Eine gueltige Konfiguration verwendet beispielsweise `Paths.Source`; ein CLI-Update schreibt `paths.source` mit geaendertem Wert.
- **Auswirkung:** Der case-sensitive JSON-Merge erzeugt zwei Schluessel, die der case-insensitive Konfigurationsprovider als Duplikate ablehnt. Die ungueltige Datei ist zu diesem Zeitpunkt bereits gespeichert. Auch der naechste Programmstart scheitert.
- **Beleg:** Sowohl in-memory mit den echten Produktionsmethoden als auch am frisch publizierten Native-AOT-Host reproduziert.

Ausgangskonfiguration des isolierten Projekts:

```json
{
  "theme": { "name": "Lumina" },
  "Paths": { "Source": "source" }
}
```

```text
revela config paths --source changed-source
CASE_UPDATE_EXIT=1
A duplicate key 'paths:source' was found.

revela config paths --help
RESTART_EXIT=-1073740791
```

Die gespeicherte Datei enthielt danach sowohl `Paths.Source` als auch
`paths.source`. Ein vorheriger Aufruf mit unveraendertem Wert war ein No-op
und wurde nicht als Reproduktion gewertet.

**Empfehlung:** Einheitliche Schluesselvergleiche verwenden und das neue Dokument
vor der atomaren Ersetzung mit den tatsaechlichen Leseregeln validieren.

### F3 - Schwerwiegend: Unterbrochene OneDrive-Downloads zerstoeren die vorherige Quelldatei

- **Stelle:** [SharedLinkProvider.cs:178](../../src/Plugins/Source/OneDrive/Providers/SharedLinkProvider.cs#L178).
- **Ausloeser:** Nach erfolgreichen HTTP-Headern bricht das Lesen des Response-Bodys ab. Die endgueltige Zieldatei wurde bereits mit `FileMode.Create` geoeffnet.
- **Auswirkung:** Statt der vorherigen vollstaendigen Quelldatei bleibt eine leere oder unvollstaendige Datei zurueck.
- **Beleg:** Echte `DownloadFileAsync`-Methode mit einem rein lokalen In-Memory-HTTP-Handler: HTTP 200, drei gelesene Bytes, danach `IOException("Synthetic body interruption")`. Die vorhandenen vier Bytes `FFD8FFD9` wurden durch `010203` ersetzt. Es fand kein Netzwerkzugriff statt.
- **Empfehlung:** In eine eindeutige temporaere Geschwisterdatei herunterladen, erfolgreich schliessen und erst danach die Zieldatei ersetzen. Fehler und Abbruch muessen die vorherige Datei erhalten.

### F4 - Schwerwiegend: Globale Konfigurationsupdates verwerfen fremde Abschnitte

- **Stellen:** [GlobalConfigManager.cs:87](../../src/Core/Services/GlobalConfigManager.cs#L87), [GlobalConfigFile:224](../../src/Core/Services/GlobalConfigManager.cs#L224).
- **Ausloeser:** Ein Feed wird hinzugefuegt oder ein Paket registriert, waehrend die globale Konfiguration weitere gueltige Abschnitte enthaelt.
- **Auswirkung:** Das feste Serialisierungsmodell besitzt keine Erhaltung unbekannter Eigenschaften. Globale Pfad-, Generierungs- und Plugin-Einstellungen koennen beim Speichern verschwinden.
- **Beleg:** Roundtrip durch den tatsaechlich generierten Produktionsserializer. Die synthetischen Abschnitte `paths`, `generate` und `Spectara.Revela.Plugins.Source.Calendar` gingen verloren; `packages` blieb erhalten. Keine globale Benutzerkonfiguration wurde veraendert.
- **Empfehlung:** Nicht vom jeweiligen Writer verwaltete Abschnitte erhalten und die Erhaltung mit einem echten Schreib-/Lese-Roundtrip testen.

## Weitere Wesentliche Findings

### F5 - Restore meldet Erfolg trotz fehlender deklarierter Abhaengigkeit

- **Stellen:** [ConfigurationServiceCollectionExtensions.cs:62](../../src/Sdk/Configuration/ConfigurationServiceCollectionExtensions.cs#L62), [PluginProjectService.cs:120](../../src/Features/Packages/Services/PluginProjectService.cs#L120).
- **Ausloeser:** Ein Projekt deklariert ein Plugin im vom Writer verwendeten Root-Abschnitt `plugins`. Der Leser bindet dagegen den Abschnitt `dependencies`.
- **Auswirkung:** `restore --check` kann fehlende Pakete uebersehen und faelschlich Erfolg melden.
- **Beleg:** Am echten modularen Host reproduziert, ohne Paketdownload. Ein isoliertes Projekt mit `plugins.Spectara.Revela.Plugins.ReleaseReviewMissing = "0.0.1-beta.21"` lieferte `No dependencies to restore.` und Exitcode **0**.
- **Testluecke:** [RestoreCommandTests.cs:125](../../tests/Commands/Packages/RestoreCommandTests.cs#L125) ersetzt den Scanner durch ein Substitute und umgeht damit den fehlerhaften Binding-Vertrag.
- **Empfehlung:** Writer und Reader auf denselben Vertrag ausrichten; einen echten Host mit deklarierter, fehlender Abhaengigkeit pruefen.

### F6 - Komprimierung loescht unabhaengige gzip-Downloads und meldet Erfolg

- **Stelle:** [CompressedSiteInvalidator.cs:31](../../src/Plugins/Compress/Services/CompressedSiteInvalidator.cs#L31).
- **Ausloeser:** Im Output liegt eine eigenstaendige `.gz`- oder `.br`-Datei, die kein vom Plugin erzeugter Sidecar ist.
- **Auswirkung:** Der Invalidator loescht alle Dateien mit diesen Endungen, ohne ihre Herkunft zu kennen. Eigenstaendige Downloads werden nicht wiederhergestellt.
- **Beleg:** In der eigenen Showcase-Ausgabe eine gueltige gzip-Datei als unabhaengige `review-download.gz` bereitgestellt. Nach `generate compress` war sie verschwunden; der Command endete mit Exitcode **0**.
- **Empfehlung:** Invalidierung auf vom Plugin verwaltete Artefakte begrenzen, einschliesslich verwaister eigener Sidecars.

### F7 - Das verteilte SDK enthaelt den zugesagten Source Generator nicht

- **Stellen:** [RevelaTemplateModelAttribute.cs:4](../../src/Sdk/Abstractions/RevelaTemplateModelAttribute.cs#L4), [Directory.Build.targets:29](../../Directory.Build.targets#L29), [Sdk.csproj:24](../../src/Sdk/Sdk.csproj#L24).
- **Ausloeser:** Ein externer Consumer verwendet nur das SDK-NuGet-Paket und erwartet die dokumentierte Generierung von `ToScriptObject()` fuer `[RevelaTemplateModel]`.
- **Auswirkung:** Im Repository wird der Generator zentral als Analyzer eingebunden. Der Paket-Consumer erhaelt diesen Build-Bestandteil nicht.
- **Beleg:** Das frisch erstellte SDK-Paket enthielt weder eine Analyzer-Assembly noch eine Generator-Abhaengigkeit. Geprueft wurden die ZIP-Eintraege und das NuSpec, nicht nur die Projektdatei.
- **Verifikationsgrenze:** Ein eigenstaendiger externer Consumer-Build wurde nicht ausgefuehrt. Der fehlende Paketbestandteil ist beobachtet; die Consumer-Auswirkung ergibt sich aus dem dokumentierten Generatorvertrag.
- **Empfehlung:** Generator mit verteilen und einen Paket-Consumer ausserhalb der geerbten Repository-Build-Konfiguration testen.

### F8 - Unterschiedliche Galerie- und Bildpfade erzeugen identische Viewer-IDs

- **Stelle:** [PhotoPageCatalog.cs:98](../../src/Features/Generate/Infrastructure/PhotoPageCatalog.cs#L98).
- **Ausloeser:** Pfade unterscheiden sich nur durch einen Bindestrich statt eines Verzeichnistrenners, oder eine Galerie heisst `home`, waehrend die Root-Galerie ebenfalls beteiligt ist.
- **Auswirkung:** Gemeinsame Fotos dieser Galerien erhalten kollidierende Kontext-IDs; Navigation und Ruecksprung koennen mehrdeutig werden. Entsprechend kollidieren Bildanker.
- **Beleg:** Echte Produktionsmethoden lieferten `a-b` sowohl fuer `a/b` als auch fuer `a-b`, `home` sowohl fuer den leeren Root-Slug als auch fuer `home`, sowie `photo-a-b` fuer beide entsprechenden Bild-Slugs.
- **Verifikationsgrenze:** Die nicht eindeutige ID-Abbildung wurde ausgefuehrt. Die spezielle kollidierende Galerie-Kombination wurde nicht durch einen vollstaendigen Browserlauf verifiziert. Der normale Showcase-Viewer bestand seine Browserpruefung.
- **Empfehlung:** Kollisionsfreie Kodierung verwenden und gemeinsame Bildmitgliedschaften ueber diese Pfadkombinationen testen.

### F9 - OneDrive gibt wiederverwendbare Share-Links im normalen Konsolenoutput aus

- **Stelle:** [OneDriveSourceCommand.cs:118](../../src/Plugins/Source/OneDrive/Commands/OneDriveSourceCommand.cs#L118).
- **Ausloeser:** Der normale Sync-Output wird als Log oder fuer eine Supportanfrage aufgezeichnet.
- **Auswirkung:** Der vollstaendige Share-Link landet im Mitschnitt. `Markup.Escape` verhindert Markup-Interpretation, entfernt aber keine Zugangsinformationen.
- **Beleg:** Im Quellcode bestaetigter direkter Ausgabeaufruf. Kein Live-Provider-Aufruf und keine Ausgabe eines echten Geheimnisses fuer die Pruefung.
- **Empfehlung:** Die bereits fuer Kalenderquellen angewendete Vertraulichkeitsgrenze auch auf OneDrive-Konsolenoutput und HTTP-Diagnoselogs anwenden.

## Pruefumfang Und Git-Basis

| Merkmal                        | Gepruefter Stand                                              |
| ------------------------------ | ------------------------------------------------------------- |
| Pruefdatum                     | 2026-09-07                                                    |
| Ablagedatum                    | 2026-09-11                                                    |
| Arbeitsgrenze                  | Ausschliesslich `D:\Work\GitHub\Revela`                       |
| Letzter lokaler Beta-Tag       | `v0.0.1-beta.20`                                              |
| Basis-Commit                   | `482a5b5be96451208b96445dd5539e58daa40948`                    |
| Gepruefter HEAD                | `de7f70af61cbfeed3c41c7acf88a6c04980bf3c6`                    |
| Branch-Status                  | `main`, 35 Commits vor `origin/main`                          |
| Zusaetzlicher Arbeitsstand     | Acht modifizierte versionierte Dateien und ein untracked Test |
| Versionierter Delta zu beta.20 | 427 Dateien, 25.503 hinzugefuegte und 11.356 entfernte Zeilen |
| Release Notes                  | Unter `Unreleased`; beta.21 war nicht getaggt                 |

Zusaetzlich zum HEAD waren folgende Dateien Bestandteil der Pruefung:

- [.github/workflows/deploy-website.yml](../../.github/workflows/deploy-website.yml)
- [CHANGELOG.md](../../CHANGELOG.md)
- [Directory.Packages.props](../../Directory.Packages.props)
- [docs/development.md](../development.md)
- [scripts/test-release.ps1](../../scripts/test-release.ps1)
- [src/Core/Extensions/PackageServiceCollectionExtensions.cs](../../src/Core/Extensions/PackageServiceCollectionExtensions.cs)
- [src/Features/Theme/Commands/ThemeFilesCommand.cs](../../src/Features/Theme/Commands/ThemeFilesCommand.cs)
- [tests/Commands/Generate/Services/ScribanTemplateEngineTests.cs](../../tests/Commands/Generate/Services/ScribanTemplateEngineTests.cs)
- [tests/Cli/Hosting/ThemeFilesCommandTests.cs](../../tests/Cli/Hosting/ThemeFilesCommandTests.cs), damals untracked

Die Befunde betreffen den aktuellen Stand der damaligen Pruefung. Nicht jeder
Befund ist als neu in beta.21 eingefuehrte Regression nachgewiesen.

Der [Full-Review-Workflow](../../.github/prompts/full-review.prompt.md) und die
Repository-Anweisungen wurden gelesen. Die Pruefung umfasste Inventar,
Architektur, Code-/Testqualitaet, Sicherheit und Performance-Risiken.
Build-Ausgaben wurden serialisiert; eigene Ausgaben lagen unter
`artifacts/rr21-3a6257/`.

Spezialisierte Teilpruefungen verwendeten unter anderem Plugin Auditor,
Test Doctor, Convention Sentry und Security Scout sowie begrenzte
Explore-Auftraege. Ihre Aussagen wurden nicht als eigene Verifikation
uebernommen: unzutreffende Behauptungen wurden verworfen, wichtige Befunde
am Code und durch eigene kleine Checks hinterfragt.

## Ausgefuehrte Pruefungen

### Build, Tests Und Format

- Frischer Release-Build: Exitcode **0**, keine Warnungen, keine Fehler.
- `dotnet format --verify-no-changes --no-restore`: Exitcode **0**, keine automatischen Formatierungen.
- Vollstaendige MTP-Testentdeckung mit expliziter Solution, TRX und Microsoft Code Coverage: **1.018 Tests, 1.015 bestanden, 3 fehlgeschlagen, 0 uebersprungen**, Exitcode **2**.
- Isolierter Serve-Nachlauf unter Coverage: **47/47 bestanden**, Exitcode **0**. Dieser Nachlauf hebt den fehlgeschlagenen Gesamtlauf nicht auf.
- `git diff --check`: Exitcode **0**. Abschliessender HEAD und Arbeitsstand entsprachen dem Ausgangsstand; waehrend des Reviews wurden keine Quellcodeaenderungen vorgenommen.

Die drei Fehler des Gesamtlaufs:

1. `Server_HeadRequest_ReturnsEncodedHeadersWithoutBody`: HTTP-Praefixkollision fuer `http://localhost:65289/`.
2. `Server_HtmlFile_NoCacheHeaders`: `IOException` beim Aufraeumen einer noch geoeffneten HTML-Datei.
3. `Server_AssetFile_HasCacheHeaders`: `IOException` beim Aufraeumen einer noch geoeffneten CSS-Datei.

Die Portreservierung wird in
[StaticFileServerTests.cs:633](../../tests/Plugins/Serve/StaticFileServerTests.cs#L633)
vor dem eigentlichen HTTP-Binding freigegeben. Zwei Tests verwenden synchrones
Dispose vor der Verzeichnisloeschung; der Server verfolgt gestartete Handler in
[StaticFileServer.cs:126](../../src/Plugins/Serve/StaticFileServer.cs#L126)
nicht bis zu deren Abschluss. Diese Ressourcenrennen sind nicht mit einem
funktionalen Fehler der geprueften HTTP-Header gleichzusetzen.

Zusammengefuehrte gefilterte Zeilen-Coverage, nach eindeutigen Quellzeilen ueber
die Testhosts ermittelt:

| Bereich                 | Coverage |
| ----------------------- | -------: |
| Features.Packages       |    7,7 % |
| Commands                |   19,3 % |
| Features.Theme          |   21,2 % |
| Plugins.Source.OneDrive |   40,7 % |
| Plugins.Statistics      |   50,1 % |
| Core                    |   57,4 % |
| Plugins.Serve           |   61,5 % |
| Plugins.Calendar        |   69,7 % |
| Features.Generate       |   69,8 % |
| Sdk                     |   72,1 % |
| Plugins.Source.Calendar |   78,8 % |
| Plugins.Compress        |   82,8 % |

Diese Werte sind keine vollstaendige CLI- oder JavaScript-Coverage. Bedingte
`Inconclusive`-Pfade existieren, unter anderem fuer ein fehlendes Embedded-Artefakt
und einen Windows-spezifischen Cleanup-Test. Im ausgefuehrten Gesamtlauf wurde
kein Fall uebersprungen.

### Packaging Und Generierung

- Frischer Windows-x64-Native-AOT-Publish: Exitcode **0**.
- Direkter Aufruf des publizierten Programms: `revela 0.0.1-beta.21 (.NET 10.0.11)`, Embedded-Kennzeichnung, Exitcode **0**.
- Frischer Build und Pack mit CI-Symbolparametern: beide Exitcode **0**; **13 NuGet-Pakete**, alle mit Version **0.0.1-beta.21**. Versionen und SDK-Dateien wurden aus den tatsaechlichen Archiven gelesen.
- Eigene Showcase-Kopie mit frischem AOT-Host: **22 Seiten, 14 Fotos, 228 Bildvarianten**, `generate all` Exitcode **0**.
- Fehlendes Theme: `theme files --theme MissingReleaseReviewTheme` meldete `Theme Not Found` und Exitcode **1**.
- Komprimierung: **56 Sidecars** aus 28 Dateien. Der unabhaengige gzip-Download ging wie unter F6 beschrieben verloren.
- Anschliessendes `generate pages` scheiterte einmal an einer Dateisperre waehrend der Invalidierung, Exitcode **1**. Beim Nachlauf war die Sperre frei; **22 Seiten** wurden gerendert und alle Sidecars entfernt. Der Verursacher der temporaeren Sperre blieb ungeklaert.

Eine delegierte Smoke-Ausfuehrung uebergab Argumente fehlerhaft und meldete
selbst fuer `--version` einen interaktiven Fehler. Sie wurde weder als
Produktdefekt noch als bestandener Smoke-Test gewertet. Die oben genannten
Ergebnisse stammen aus direkten, korrigierten Aufrufen.

### Browser

Die vorhandene [Lumina-Browser-Akzeptanz](../../scripts/browser/README.md)
lief gegen die frisch generierte eigene Ausgabe, mit temporaerem
Loopback-Server und neuen Headless-Kontexten. Fremde Browserseiten und
bestehende Vorschauen wurden nicht verwendet. Externe Requests waren blockiert.

**Edge 152.0.4191.66: alle vier Kombinationen bestanden.**

| Kombination    | Viewport    | JavaScript | Farbschema | Reduced Motion |
| -------------- | ----------- | ---------- | ---------- | -------------- |
| Desktop hell   | 1440 x 1000 | an         | hell       | aus            |
| Desktop dunkel | 1440 x 1000 | aus        | dunkel     | an             |
| Mobil hell     | 390 x 844   | aus        | hell       | an             |
| Mobil dunkel   | 390 x 844   | an         | dunkel     | aus            |

Beobachtet wurden alle sechs Canon-Lightbox-Ausloeser, angezeigte Bildidentitaet,
Schliessen per Button und Escape, Fokus-Rueckkehr, Next-Dialog-Navigation mit
JavaScript, nichtinteraktive Sony-Galerie, Foto-Seitenlinks, kanonische URLs,
Open-Graph-Bildvarianten, Ruecksprung zur ausgewaehlten Galerieposition und
fehlende lokale Ressourcen. Desktop-/Mobil-Screenshots wurden angesehen.

Firefox und WebKit konnten mangels der vom Browserpaket erwarteten lokalen
Browserbinaerdateien nicht starten. Es wurden keine Browser nachinstalliert.
Fruehere Browserergebnisse aus Entscheidungsdokumenten wurden nicht als eigene
Verifikation dieser Engines gezaehlt.

### Abhaengigkeiten, Dokumentation Und Automatisierung

- Oeffentliche direkte/transitive NuGet-Advisory-Abfrage: **30 Projekte**, keine gemeldeten verwundbaren Pakete, Exitcode **0**. Dies ist keine Garantie fuer Abwesenheit von Sicherheitsluecken.
- Korrigierter Aufruf `dotnet outdated --no-restore`: keine veralteten Abhaengigkeiten gemeldet, Exitcode **0**. Eine vorherige delegierte Meldung mit dem nicht unterstuetzten Parameter `--source` wurde nicht als Erfolg uebernommen.
- Changelog, Dokumentation und fruehere Entscheidungsbelege wurden als Referenzen mit dem Code abgeglichen, nicht als Nachweis aktueller Fehlerfreiheit. Die Release Notes lagen weiterhin unter `Unreleased`; insbesondere der Source-Generator-Vertrag ist durch F7 nicht fuer Paket-Consumer erfuellt.
- Statische Workflow-Pruefung: GitHub-Release-Erstellung ist auf Tag-Push begrenzt; die aktuelle Deploy-Bedingung schliesst einen manuellen Release-Dispatch als automatischen Website-Deployment-Ausloeser aus. Ein manueller Release-Dispatch fuehrt trotzdem Attestierung und Signierung aus und ist damit kein extern nebenwirkungsfreier Probelauf.
- Der SDK-Paketinhalt und die bekannten Grenzen der Smoke-Assertions wurden unabhaengig von der Abschlussmeldung des Release-Skripts bewertet.

## Verbleibende Smoke-Warnungen

### clean images: falsche Erwartung des Smoke-Tests

[test-release.ps1:1035](../../scripts/test-release.ps1#L1035) erwartet nach
`clean images` einen leeren Bilderordner. Der Command bereinigt laut
[CleanImagesCommand.cs](../../src/Features/Generate/Commands/CleanImagesCommand.cs)
jedoch nur ungenutzte Varianten, nicht alle gueltigen Bilder.

**Beobachtung:** Vorher und nachher waren **228 gueltige Bildvarianten** vorhanden,
mit **0 Hash-Unterschieden** und Exitcode **0**. Diese Warnung ist kein Nachweis
eines fehlgeschlagenen Image-Cleanups. Der Smoke-Test sollte stattdessen gezielt
das Entfernen veralteter Varianten und den Erhalt gueltiger Varianten pruefen.

### config locations: tatsaechliche diagnostische Auslassung

[ConfigLocationsCommand.cs:30](../../src/Commands/Config/Revela/ConfigLocationsCommand.cs#L30)
zeigt Installations- und globale Konfigurationsorte, aber keine lokale
Projektkonfiguration.

**Beobachtung:** Der echte AOT-Aufruf lieferte Exitcode **0**, jedoch weder
`project.json` noch einen Projekt-Konfigurationspfad. Die zusaetzliche Meldung
ueber eine fehlende globale Konfiguration war in der frischen portablen
Testinstallation erwartbar. Die Projektpfad-Warnung muss gegen einen expliziten
Command-Vertrag aufgeloest werden, statt dauerhaft ignoriert zu werden.

## Ausgelassene Pruefungen Und Restrisiken

- Keine Linux-, macOS- oder ARM-Ausfuehrung der Release-Artefakte.
- Firefox und WebKit nicht ausfuehrbar; keine reale Safari-/iOS-, Touch-/Zoom- oder Assistive-Technology-Pruefung.
- Kein vollstaendiger installerbasierter Lauf von `test-release.ps1`; keine Neuinstallation globaler Tools. Lokales Build/Pack, AOT und ausgewaehlte reale Host-Ablaufe ersetzen diesen End-to-End-Test nicht.
- Kein eigenstaendiger externer SDK-Paket-Consumer-Build und keine vollstaendige modulare Installieren/Entfernen/Neuinstallieren-Pruefung.
- Keine privaten Feeds, echten OneDrive-/Kalender-Provider oder Live-Zugangsdaten verwendet.
- Keine entfernten Workflows gestartet, keine Remote-Signierung, Veroeffentlichung oder Deployments ausgefuehrt. Workflow-Aussagen beruhen auf statischer Pruefung.
- Keine Benchmark-Vergleichsmessung. Die Large-Output-Scriban-Regressionstests bestanden; daraus folgt keine allgemeine Performance-Aussage.
- OneDrive-Body-Deadlines und direkt in finale Komprimierungsdateien geschriebene Sidecars bleiben zusaetzliche quellenbasierte Risiken fuer Hangs beziehungsweise unvollstaendige Artefakte bei Abbruch.
- DNS-Rebinding-Schutz, Download-Bytequoten und Transaktionen ueber mehrere Feeds sind keine nachgewiesenen Produkteigenschaften. Dokumentierte Trust-Grenzen sind keine Sandbox-Garantie.
- Die konkrete Ursache der temporaeren Invalidierungs-Dateisperre wurde nicht festgestellt. Der erfolgreiche Nachlauf beseitigt diese Beobachtung nicht.

## Lokale Nachweise

Die damaligen temporaeren Nachweise wurden unter folgenden Pfaden erzeugt;
sie sind lokale Artefakte und kein zugesicherter Bestandteil eines Checkouts:

```text
artifacts/rr21-3a6257/
  build/                         Isolierter Release-Build und synthetische Fixtures
  tests/                         Vollstaendiger Lauf: TRX und Cobertura
  serve-rerun/                   Isolierter Serve-Nachlauf
  format/                        Formatbericht
  standalone/                    Frisch publizierter Native-AOT-Host
  modular-build/                 Build mit CI-Symbolparametern
  packages/                      13 gepruefte NuGet-Pakete
  showcase/                      Eigene generierte Showcase-Kopie
  browser-fixture/artifacts/
    browser-checks/edge/          Eigene Browser-Screenshots
```

Es wurden waehrend des Reviews keine Implementierungen korrigiert, keine
automatischen Fixes oder Formatierungen vorgenommen und keine Git-Historie
veraendert. Bestehende Sample-Ausgaben, Vorschauen und andere Repositories
blieben ausserhalb der Arbeitsgrenze.

## Entscheidung

**Nicht bereit fuer beta.21.**

Reproduzierter Verlust von Quelldateien, dauerhaft unlesbare
Projektkonfiguration und falsche Erfolgs-Exitcodes bei Abhaengigkeitspruefungen
verhindern eine Freigabe trotz erfolgreichem Build, Packaging und
normalpfadbezogener Browserpruefung.

Vor einem Tagging sind die Blocker zu beheben, Regressionstests an den
tatsaechlichen Host- und Paketvertraegen zu ergaenzen und die fehlgeschlagenen
beziehungsweise noch offenen Release-Pruefungen erneut auszufuehren.
Dieser Bericht ist keine Freigabe fuer Commit, Tag, Push, Release oder Deployment.
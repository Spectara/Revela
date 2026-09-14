# Abschliessendes Release-Readiness-Review: 0.0.1-beta.21

- Pruefdatum und Ablagedatum: **2026-09-13**.
- Gepruefter Commit: **`088d79a6408b1d8fd1a748f1766d0c1c5aac7d10`**.
- Arbeitsstand beim Review: sauberer `main`, 47 Commits vor dem lokalen `origin/main`.
- Arbeitsgrenze: ausschliesslich `D:\Work\GitHub\Revela`.
- Ergebnis: **Nicht bereit fuer beta.21.** Ein Release-Blocker bleibt offen.

> Dieses Dokument haelt das abschliessende read-only Review des genannten
> Commitstands fest. Es ist kein erneutes pauschales Vollaudit. Das Ablegen
> des Berichts stellt keine zusaetzliche Verifikation und keine Freigabe fuer
> Implementierung, Commit, Push, Tag, Release, Deployment oder entfernte
> Workflow-Ausfuehrung dar. Zeilenreferenzen beziehen sich auf den geprueften Stand.

## Findings

### R1 - Schwerwiegend / Release-Blocker: Paketinstallation umgeht den reparierten Konfigurationswriter

**Stellen:**
[PluginProjectService.cs:120](../../src/Features/Packages/Services/PluginProjectService.cs#L120),
[PackageManager.cs:346](../../src/Features/Packages/Services/PackageManager.cs#L346),
[RestoreCommand.cs:174](../../src/Features/Packages/Commands/Restore/RestoreCommand.cs#L174).

`PluginProjectService` liest und schreibt ausschliesslich den kleingeschriebenen
Schluessel `plugins` in einem case-sensitiven `JsonObject`. Anschliessend schreibt
der Service die Datei direkt zurueck. Diese Implementierung ist gegenueber dem
urspruenglichen Review unveraendert. `PackageManager` ruft sie nach der
Paketextraktion auf und gibt Erfolg zurueck, ohne die resultierende Konfiguration
zu validieren.

**Ausloeser:** Ein gueltiges Projekt verwendet eine anders geschriebene
`Plugins`-Sektion, beispielsweise:

```json
{
  "Plugins": {
    "Spectara.Revela.Plugins.Compress": "0.0.1-beta.21"
  }
}
```

Wird dieses Paket installiert, kann der Writer eine zweite, kleingeschriebene
`plugins`-Sektion mit demselben Paketschluessel hinzufuegen. Der
Konfigurationsprovider lehnt danach den doppelten flachen Schluessel beim
Neuladen beziehungsweise beim naechsten Start ab. Auch der reparierte
Dependency-Reader kann diesen Pfad nun ueber `restore` erreichen, wenn das
deklarierte Paket fehlt.

**Auswirkung:** Eine zuvor lesbare Projektkonfiguration wird unlesbar. Die
F2-Reparatur in `ConfigService` schuetzt diesen separaten Writer nicht.
Die Paketregistrierung validiert den gespeicherten Inhalt nicht; Fehler beim
Schreiben werden in `AddPluginAsync` zudem abgefangen und nur geloggt, ohne
den aufrufenden Installer verlaesslich scheitern zu lassen.

**Belegklassifikation:** Im Quellcode bestaetigter erreichbarer Fehler,
unabhaengig gegengeprueft. Waehrend dieses read-only Reviews wurden weder eine
Installation noch eine Dateisystem-Reproduktion ausgefuehrt. Der vorhandene
Release-Smoke prueft sequenzielle Installationen und `restore --check`, nicht
diesen gemischt geschriebenen Installationsfall.

**Erforderliche Regression:** Eine tatsaechliche Paketregistrierung muss das
gueltige gemischt geschriebene Dokument erhalten, durch den echten Provider
weiter lesbar bleiben und Persistenzfehler an den Installer weitergeben.
Die parallelen Konfigurationszugriffe von Restore benoetigen ausserdem eine
explizite Serialisierung und entsprechende Tests. Dieses Parallelitaetsrisiko
ist vom deterministischen Case-Konflikt zu unterscheiden.

## Abgleich Der Urspruenglichen Findings

Grundlagen sind der
[urspruengliche Review-Bericht](release-readiness-beta.21-2026-09-07.md),
der [Massnahmenbericht](remediation-beta.21-2026-09-11.md) und
[ADR 0004](../decisions/0004-review-data-integrity-boundaries.md).

| Finding | Abschliessende Bewertung |
| --- | --- |
| F1 - Loeschung ueber verknuepfte Verzeichnisse | Im vereinbarten Rahmen geschlossen: verknuepfte Nachfahren werden ausgeschlossen und vor der Loeschung frisch geprueft; absichtlich verknuepfte Source-Wurzeln bleiben erlaubt. |
| F2 - Doppelte Konfigurationsschluessel | In [ConfigService.cs:72](../../src/Commands/Config/Services/ConfigService.cs#L72) behoben, aber wegen R1 nicht ueber alle Projektwriter geschlossen. |
| F3 - Unterbrochene Downloads | Geschlossen: exklusives Geschwister-Staging, Zeitstempel-/Modusbehandlung und Ersetzung erst nach erfolgreicher Uebertragung. |
| F4 - Verlust globaler Einstellungen | Geschlossen: dokumenterhaltende Schreiboperationen, echte Provider-Validierung und isolierte Roundtrip-Tests. |
| F5 - Uebersehene fehlende Abhaengigkeiten | Der urspruengliche Reader-/Check-Fehler ist geschlossen. Der eigentliche Installationspfad bleibt von R1 betroffen. |
| F6 - Loeschung fremder komprimierter Dateien | Geschlossen: persistierte Ownership steuert Erzeugung, Cleanup und Invalidierung; Tests decken neue Service-Instanzen und den Erhalt fremder Downloads ab. |
| F7 - Fehlender SDK-Generator | Geschlossen: [Sdk.csproj:47](../../src/Sdk/Sdk.csproj#L47) packt den Analyzer ohne Neubau beim No-Build-Pack; die Nachweise des echten Paket-Consumers sind konsistent. |
| F8 - Kollidierende Viewer-IDs | Fuer kanonische Slugs geschlossen: [PhotoPageCatalog.cs:102](../../src/Features/Generate/Infrastructure/PhotoPageCatalog.cs#L102) verwendet eine Kodierung fester Breite und getrennte Namensraeume. |
| F9 - Offenlegung von Share-Links | Im normalen Command-/Standard-HTTP-/Polly-Logging geschlossen; benutzerdefinierte Subscriber und Shell-History sind nicht Bestandteil dieser Aussage. |

Die beiden Smoke-Warnungen sind mit aussagekraeftigen Assertions adressiert:
Veraltete Bildvarianten werden entfernt, waehrend gueltige Hashes erhalten
bleiben; Projekt-, Site- und Logging-Konfigurationsorte werden explizit geprueft.
Die konkreten Paketdiagnosen werden in
[test-release.ps1:641](../../scripts/test-release.ps1#L641) geprueft, statt einen
beliebigen Fehler-Exitcode als Nachweis einer fehlenden Abhaengigkeit anzusehen.

## Linux-Bindekonflikt-Korrektur

Die Korrektur in
[StaticFileServerTests.cs:888](../../tests/Plugins/Serve/StaticFileServerTests.cs#L888)
ist angemessen und keine pauschale Unterdrueckung:

- Code 400 wird nur auf Linux/macOS und nur mit dem exakten Duplicate-Registration-Text fuer den gerade gebundenen Praefix akzeptiert.
- Falscher Text, Port, Pfad und Fehlercode werden in negativen Tests zurueckgewiesen.
- Die temporaer gesetzte UI-Kultur wird in `finally` wiederhergestellt.
- Der echte Konflikttest prueft, dass der urspruengliche Server weiterhin Antworten liefert und der fehlgeschlagene Listener geschlossen wurde.
- Wiederholungsversuche betreffen ausschliesslich die Listener-Akquisition, nicht fehlgeschlagene HTTP-Assertions.

Die vorhandenen TRX-Dateien bestaetigen den Ablauf unabhaengig von der
Zusammenfassung im Massnahmenbericht:

| Linux-Pruefung | Gesamt | Bestanden | Fehlgeschlagen | Uebersprungen |
| --- | ---: | ---: | ---: | ---: |
| Urspruenglicher Gesamtlauf | 1.227 | 1.218 | 1 | 8 |
| Unveraenderte Einzelreproduktion | 1 | 0 | 1 | 0 |
| Korrigierte fokussierte Tests | 7 | 7 | 0 | 0 |
| Korrigierter vollstaendiger Coverage-Lauf | 1.233 | 1.225 | 0 | 8 |

Der urspruengliche Fehler und die Einzelreproduktion betrafen denselben Test:
`Server_StartWithBoundPrefix_ClosesFailedListenerWithoutAffectingOwner`.
Beide meldeten den unerwarteten Bindefehler 400. Sie wurden nicht nachtraeglich
als erfolgreiche Laeufe gezaehlt.

Die nachtraegliche Korrektur aenderte nur die Testdatei, nicht den
Produktionsserver. Sie ist im finalen Serve-Commit `29b2ebe` enthalten.
Eine zukuenftig geaenderte Runtime-Diagnose wuerde als Fehler sichtbar bleiben,
statt durch eine breite 400-Ausnahme verdeckt zu werden. Die konkrete
macOS-Ausfuehrung bleibt ungeprueft.

## Kritische Bewertung Der Nachweise

### Zuordnung Zum Finalen Commit

- Alle **766 versionierten Dateien** sind im gespeicherten Snapshot enthalten. Keine zusaetzliche Commit-Datei fehlt im Snapshot-Inventar.
- Gegenueber den damaligen Quellhashes unterscheiden sich nur der ergaenzte Massnahmenbericht und die korrigierte Serve-Testdatei.
- Der aktuelle Hash dieser Testdatei stimmt mit dem Korrekturbeleg ueberein.
- Damit lassen sich die Linux-Nachweise dem finalen Produktionscode zuordnen; die alte Basis-Commitangabe des Snapshots allein waere dafuer nicht ausreichend gewesen.

### Testabdeckung Ueber Plattformen

Der vorhandene Windows-Release-Lauf umfasst **1.227 Tests: 1.218 bestanden,
0 fehlgeschlagen, 9 uebersprungen**. Dieser vollstaendige Lauf liegt vor den
sechs zusaetzlichen Linux-Klassifikationsfaellen.

Jeder unter Windows uebersprungene Fall bestand laut Einzeltestergebnissen
unter Linux. Jeder der acht Linux-Skips bestand unter Windows. Diese
gegenseitige Abdeckung schliesst die konkreten Skip-Luecken, belegt aber weder
identisches Plattformverhalten noch macOS-/ARM-Unterstuetzung.

Einige fruehere Statuszeilen im
[Massnahmenbericht](remediation-beta.21-2026-09-11.md#L144) bezeichnen Unix-Faelle
noch als nicht ausgefuehrt. Die spaetere WSL-Ergaenzung aktualisiert diese
historischen Zwischenstaende; sie sind nicht als zusaetzliche offene Unix-Luecke
zu zaehlen.

### Pakete Und Laufzeit

- Das tatsaechliche SDK-Archiv enthaelt genau einen Generator-Analyzer.
- Release-Paket, Consumer-Kopie und restauriertes Consumer-Paket haben denselben SHA-256-Hash.
- Die vorhandenen Consumer-Belege pruefen das wirkliche Paket, nicht einen verdeckten ProjectReference-Build mit geerbten Repository-Imports.
- Windows-Packaging/Consumer sowie Windows-/Linux-x64-AOT-Generierung stuetzen die jeweils dokumentierten Aussagen. Sie belegen keine vollstaendige plattformuebergreifende Release-Pipeline.
- Die Linux-AOT-Smoke-Nachweise dokumentieren 22 Seiten, 228 Bildvarianten und die Invalidierung von 56 eigenen Sidecars bei unveraendertem fremdem gzip-Download.

Die dokumentierten lokalen Ergebnisse wurden nicht nur aus Abschlussmeldungen
uebernommen: Geprueft wurden Produktionscode, relevante Regressionstests,
TRX-Einzelfaelle, Snapshot-Hashes, Paketinhalt und Paket-Hashes. Begrenzte
read-only Teilpruefungen wurden eingesetzt; ihre Aussagen wurden gegengeprueft.

## Noch Erforderliche Release-Pruefungen

1. **R1 beheben und den echten Installations-/Konfigurationsvertrag pruefen.** Danach die betroffenen Integrations- und Packaging-Pruefungen wiederholen.
2. **Linux-modulares Packaging vervollstaendigen:** Installation, Deinstallation und Tool-Installation pruefen. Ein erfolgreicher Linux-AOT-Build deckt diesen Pfad nicht ab.
3. **Die angebotenen ARM-/macOS-Artefakte ausfuehren:** `win-arm64`, `linux-arm64` und `osx-arm64`, einschliesslich nativer Abhaengigkeiten und Start/Generierung aus entpackten Archiven. Die [Release-Matrix](../../.github/workflows/release.yml#L175) baut und packt diese Varianten, enthaelt aber keinen gleichwertigen Laufzeit-Smoke je Plattform.
4. **Vor einem entfernten Probelauf den Default-Branch-Schutz verifizieren.** Die Schutzbedingung in [deploy-website.yml:51](../../.github/workflows/deploy-website.yml#L51) fehlt im lokalen `origin/main`-Snapshot. Der tatsaechliche entfernte Stand wurde nicht abgefragt. Ein manueller Release-Dispatch attestiert/signiert weiterhin und bedarf ausdruecklicher Autorisierung; die Voraussetzungen sind auch in [docs/development.md:71](../development.md#L71) beschrieben.
5. **Browser- und Geraeteluecken getrennt behandeln:** Firefox/WebKit und reale Geraete bleiben ungeprueft im aktuellen Remediation-Nachweis. Die fehlende OneDrive-Deadline fuer den gesamten Response-Body bleibt ein gesondertes bekanntes Hang-Risiko; das Download-Staging behebt es nicht.

## Verwendete Lokale Nachweise

Die folgenden Dateien und Verzeichnisse wurden lesend ausgewertet. Sie sind
generierte lokale Artefakte, kein zugesicherter Bestandteil eines Checkouts:

```text
artifacts/remediation-beta21/coverage-release/
artifacts/remediation-beta21/release-pipeline-verified.log
artifacts/wsl-beta21-20260911-7d8eeeb4/source-hashes.json
artifacts/wsl-beta21-20260911-7d8eeeb4/snapshot-verification.json
artifacts/wsl-beta21-20260911-7d8eeeb4/serve-fix-source-verification.json
artifacts/wsl-beta21-20260911-7d8eeeb4/tests/
artifacts/wsl-beta21-20260911-7d8eeeb4/serve-repro/
artifacts/wsl-beta21-20260911-7d8eeeb4/serve-fix-focused/
artifacts/wsl-beta21-20260911-7d8eeeb4/tests-serve-fixed/
artifacts/wsl-beta21-20260911-7d8eeeb4/build-serve-fixed.log
artifacts/wsl-beta21-20260911-7d8eeeb4/tests-serve-fixed.log
artifacts/wsl-beta21-20260911-7d8eeeb4/aot-publish.log
artifacts/wsl-beta21-20260911-7d8eeeb4/aot-smoke-summary.log
artifacts/release-test-20260911-165608/nuget/
artifacts/sdk-consumer-faa12a6e0d5a4d93b054a283d4b038dc/feed/
artifacts/sdk-consumer-faa12a6e0d5a4d93b054a283d4b038dc/packages/
```

## Entscheidung

**Nicht bereit fuer beta.21.**

Die Linux-Bindekonflikt-Korrektur ist ausreichend begruendet, und die meisten
urspruenglichen Findings koennen geschlossen werden. R1 verhindert die finale
Freigabe. Anschliessend bleiben die genannten Plattform- und Packaging-Gates
erforderlich.

Das Review selbst veraenderte keine Dateien und fuehrte keine Builds, Tests,
Installationen, WSL-Sitzungen oder entfernten Aktionen aus. Die nachtraegliche
Ablage fuegt ausschliesslich dieses Dokument hinzu; der urspruengliche Bericht
und der Massnahmenbericht bleiben unveraendert.

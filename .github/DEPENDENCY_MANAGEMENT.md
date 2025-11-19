# 📦 Dependency Management

## Automatische Dependency-Checks

Dieses Projekt nutzt **automatisierte Dependency-Checks** um veraltete Packages zu erkennen.

### 🤖 GitHub Actions Workflow

**Datei:** `.github/workflows/dependency-update-check.yml`

**Zeitplan:**
- ✅ Jeden **Montag um 6:00 UTC** (7:00 CET / 8:00 CEST)
- ✅ Manuell auslösbar über GitHub Actions UI

**Was passiert:**
1. Workflow prüft alle Packages mit `dotnet outdated`
2. Erstellt einen Bericht über veraltete Packages
3. **Erstellt automatisch ein GitHub Issue** wenn Updates verfügbar sind
4. Updates existierende Issues statt neue zu erstellen

### 📋 Issue-Format

Bei verfügbaren Updates wird automatisch ein Issue erstellt:

```
📦 Dependency Updates Available

## 📦 Dependency Update Report

Generated: 2025-01-19T06:00:00Z

```
» Project: Revela.Core
  Package X: 1.0.0 → 1.2.0 (Minor)
  Package Y: 2.0.0 → 3.0.0 (Major - Breaking Changes!)
```

**Action Required:**
1. Review the updates
2. Test locally: `dotnet outdated`
3. Update packages: `dotnet outdated -u`
4. Run tests: `dotnet test`
5. Commit and push
```

---

## 🛠️ Manuelle Prüfung

### Check für Updates

```bash
# Alle Packages prüfen
dotnet outdated

# Nur Major-Updates anzeigen
dotnet outdated --major-only

# Nur Minor-Updates anzeigen
dotnet outdated --minor-only

# Nur Patch-Updates anzeigen
dotnet outdated --patch-only
```

### Updates durchführen

```bash
# Interaktive Update-Auswahl
dotnet outdated -u:prompt

# Alle Patch-Updates automatisch
dotnet outdated -u --version-lock Major

# Alle Minor-Updates automatisch
dotnet outdated -u --version-lock Minor

# ALLE Updates (VORSICHT!)
dotnet outdated -u
```

### Nach Updates testen

```bash
# Restore
dotnet restore

# Build
dotnet build

# Tests
dotnet run --project tests/Core.Tests
dotnet run --project tests/IntegrationTests
```

---

## 📋 Update-Strategie

### ✅ Patch-Updates (x.x.X)
**Immer sicher!** Bug fixes, keine Breaking Changes.

```bash
dotnet outdated -u --version-lock Major
```

**Beispiel:** `1.0.0` → `1.0.1`

---

### 🟡 Minor-Updates (x.X.x)
**Meist sicher!** Neue Features, rückwärtskompatibel.

```bash
dotnet outdated -u --version-lock Minor
```

**Beispiel:** `1.0.0` → `1.1.0`

**Vorsicht bei:**
- `Scriban` - Template Engine (API-Änderungen möglich)
- `NetVips` - Image Processing (Performance-Änderungen)

---

### 🔴 Major-Updates (X.x.x)
**VORSICHT!** Breaking Changes möglich!

```bash
# Nur prüfen, NICHT automatisch updaten!
dotnet outdated --major-only

# Manuell in Directory.Packages.props ändern
# Dann testen!
```

**Beispiel:** `1.0.0` → `2.0.0`

**Immer testen:**
1. ✅ Build erfolgreich
2. ✅ Tests laufen durch
3. ✅ Manuelle Funktionstests

---

## 🔒 Security-Updates

**Priorität: HOCH!**

Bei Security-Advisories:

```bash
# Sofort updaten!
dotnet outdated -u --include-auto-references

# Tests durchführen
dotnet test

# Sofort committen & deployen
git commit -m "security: update vulnerable packages"
```

**Monitoring:**
- GitHub Dependabot Alerts (automatisch aktiviert)
- Wöchentlicher Dependency-Check Workflow
- NuGet.org Security Advisories

---

## 📊 Package-Update-Häufigkeit

| Package Type | Update-Frequenz | Strategie |
|--------------|-----------------|-----------|
| **Security** | Sofort | Automatisch |
| **Core Framework** (.NET) | Monatlich | Minor-Updates |
| **Testing** (MSTest, FluentAssertions) | Monatlich | Minor-Updates |
| **Infrastructure** (NetVips, Scriban) | Quartalsweise | Testen! |
| **Plugins** (SSH.NET, Graph) | Quartalsweise | Optional |

---

## 🎯 Workflow für Updates

### 1. **Issue wird erstellt** (automatisch)
GitHub Actions Workflow erkennt Updates und erstellt Issue.

### 2. **Review durchführen**
```bash
# Lokal prüfen
dotnet outdated

# Release Notes lesen
# - Breaking Changes?
# - Neue Features?
# - Security Fixes?
```

### 3. **Updates durchführen**
```bash
# Patch-Updates (safe)
dotnet outdated -u --version-lock Major

# Restore & Build
dotnet restore
dotnet build
```

### 4. **Tests durchführen**
```bash
# Unit Tests
dotnet run --project tests/Core.Tests

# Integration Tests
dotnet run --project tests/IntegrationTests

# Manuell testen
dotnet run --project src/Cli -- --help
```

### 5. **Commit & Push**
```bash
git add Directory.Packages.props
git commit -m "chore(deps): update dependencies

- Updated X from 1.0.0 to 1.1.0
- Updated Y from 2.0.0 to 2.1.0

All tests passing."

git push
```

### 6. **Issue schließen**
Issue wird automatisch geschlossen oder manuell nach erfolgreichem Update.

---

## 🚫 Packages NICHT auto-updaten

Folgende Packages immer **manuell** prüfen:

1. **Scriban** - Template Engine (Breaking Changes bei Major-Updates)
2. **NetVips** - Image Processing (Performance-Testing nötig)
3. **System.CommandLine** - CLI Framework (API-Änderungen)
4. **NuGet.*** - Plugin System (API-Kompatibilität)

---

## 📚 Ressourcen

- **dotnet-outdated Tool:** https://github.com/dotnet-outdated/dotnet-outdated
- **NuGet Security Advisories:** https://github.com/advisories?query=ecosystem%3Anuget
- **Dependabot:** https://docs.github.com/en/code-security/dependabot

---

## ⚙️ Konfiguration anpassen

**Workflow-Häufigkeit ändern:**

Editiere `.github/workflows/dependency-update-check.yml`:

```yaml
on:
  schedule:
    # Täglich um 6:00 UTC
    - cron: '0 6 * * *'
    
    # Monatlich am 1. um 6:00 UTC
    - cron: '0 6 1 * *'
```

**Issue-Labels ändern:**

```yaml
labels: ['dependencies', 'maintenance', 'your-custom-label']
```

---

## 🎉 Vorteile dieses Setups

✅ **Automatisiert** - Keine manuellen Checks nötig  
✅ **Transparent** - GitHub Issues zeigen alle Updates  
✅ **Flexibel** - Manuell auslösbar wenn nötig  
✅ **Sicher** - Kein Auto-Update ohne Review  
✅ **Dokumentiert** - Alle Änderungen nachvollziehbar  

---

**Last Updated:** 2025-01-19


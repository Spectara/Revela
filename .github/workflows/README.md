# GitHub Actions CI/CD (TODO)

## Planned Workflows

### 1. CI Build & Test

**File:** `.github/workflows/ci.yml`

**Triggers:**
- Push to `main`, `develop`
- Pull requests

**Steps:**
- Checkout
- Setup .NET 10
- Restore
- Build
- Run tests
- Upload test results

### 2. Release & Publish

**File:** `.github/workflows/release.yml`

**Triggers:**
- Tag push (`v*`)

**Steps:**
- Build release
- Pack NuGet packages
- Publish to NuGet.org
- Create GitHub release

### 3. Code Quality

**File:** `.github/workflows/code-quality.yml`

**Triggers:**
- Pull requests

**Steps:**
- Run analyzers
- Check formatting
- Generate coverage report

---

## TODO

This directory is a placeholder. CI/CD workflows will be added in Phase 7.

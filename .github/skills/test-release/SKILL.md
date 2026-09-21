---
name: test-release
description: Runs the end-to-end release pipeline test using scripts/test-release.ps1. Tests build, pack, plugin install, generate all, compress, clean, idempotency, and dotnet tool install. Use when the user wants to test a release, verify the pipeline, check packaging, or validate before publishing.
argument-hint: "[--SkipTests] [--IncludeOneDrive] [--Version 0.0.0-test]"
context: fork
---

# Test Release Pipeline — Revela Project

Run the existing local suite, not a remote workflow. Read the
[Development Guide](../../../docs/development.md#building-releases) for Build vs.
Artifact mode, exact inputs, isolation and host requirements before execution.

## Script

```powershell
.\scripts\test-release.ps1
```

## Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Version` | `0.0.0-test` | Version number for the test build |
| `-SkipTests` | (off) | Skip unit tests for faster iteration |
| `-IncludeOneDrive` | (off) | Also test OneDrive sync (requires network + valid share URL) |
| `-RuntimeIdentifier` | current host | Must match the host OS/architecture; no cross-runtime execution |
| `-ArtifactPath` | absent | Test an existing directory/ZIP/TAR inside the repository without rebuilding the product |
| `-Variant` | Full | Artifact mode: Core, Full or Standalone; Build mode produces Full |
| `-PackageDirectory` | absent | Required external fixture feed for Core artifacts only |

## Common Usage

```powershell
# Full test (recommended before release)
.\scripts\test-release.ps1

# Quick iteration (skip unit tests)
.\scripts\test-release.ps1 -SkipTests

# Test specific version
.\scripts\test-release.ps1 -Version "0.1.0-beta.1"

```

## Evidence And Boundaries

The suite always retains its unique `artifacts/release-test-<timestamp>-<id>/`
directory, including transcripts and input hashes. Delete unneeded task-owned
outputs explicitly; never remove another preview or sample output. There is no
automatic cleanup switch. SDK consumer evidence has its own output directory.

Serialize builds sharing repository outputs. Tool installation uses an isolated
directory, never global tools. `-IncludeOneDrive` fetches a real provider and
requires explicit authorization; omit it for ordinary release checks. Other
public dependency restores can still need network access.

Report the actual exit status, failed/skipped checks and artifact path. A local
pass is not a hosted workflow, other-platform or browser verification result.

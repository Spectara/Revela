# Review Data Integrity Boundaries

- Status: Implemented; locally verified on Windows, Unix/platform gates remain open.
- Date: 2026-09-11
- Scope: F1-F9 remediation in Revela; no release approval.

## Context

The [independent review](../reviews/release-readiness-beta.21-2026-09-07.md)
demonstrated loss of source files, unrelated configuration and unowned compressed
downloads. Existing successful tests did not cover these boundaries.

## Decisions

- The user permits an intentionally linked source root. Cleanup must not follow
  linked descendants and must inspect their current attributes before deletion.
  This is not a guarantee against hostile filesystem mutation between checks.
- Project updates use the real configuration provider to validate the original
  and replacement before writing. Ambiguous case-split updates fail unchanged.
- Global writers retain the JSON document and mutate only their own sections,
  preserving unknown root/nested values and valid case-split sections. Invalid
  originals are errors, not a reason to save defaults over them. Existing default
  creation for a missing file and the public configuration interface remain.
- Compression tracks explicitly created sidecars in a plugin-private
  `.revela-compress.manifest` in the output root. It survives cache cleanup but
  is removed with output cleanup. The record contains only relative paths,
  format version/owner identification and content fingerprints, not local
  absolute paths or credentials. The producer excludes the record from input.
- Compression-specific cleanup never infers ownership from extensions, source
  existence or decompressible bytes. Unknown files, including same-name targets,
  are preserved. Conflicts or invalid ownership data cause explicit failure.
  Recorded files changed by another writer are preserved and reported.
- Compression publishes complete sibling-staged files without overwriting an
  unowned destination and records each completed publication durably. If record
  persistence fails, roll back only files demonstrably created by the current
  operation and retain previous ownership data. A crash between publication and
  registration may leave an unowned file and require manual resolution; do not
  silently adopt it. No whole-run transaction or power-loss recovery is promised.
- Plugin operations share serialized ownership updates. This does not lock out
  arbitrary external writers; generation requires exclusive output ownership.
- Viewer IDs use injective encoding, not a truncated hash or separator
  substitution. Root, gallery, image and numbered-grid namespaces are distinct;
  path routes and membership order remain unchanged. Fixed-width hexadecimal
  encoding of UTF-16 code units is an acceptable reversible local encoding.
- Embedded theme package identity comes from the resource assembly identity,
  following the existing PackageId/AssemblyName convention. Display names remain
  independent; deriving package IDs from names with spaces broke real Restore.
- The user requests project/site/logging paths in `config locations` inside a
  project, retaining useful installation/global output outside a project.

## Alternatives

Root-only JSON extension data loses nested settings; case-insensitive DTO
deserialization can overwrite valid case-split sections. Document preservation
avoids this lossy representation for global writes. Suffix scans or ownership
stored only in the cache cannot distinguish unrelated downloads or survive cache
cleanup. A full pending-publication recovery journal is unnecessary without a
power-loss recovery contract; fail-safe unowned remnants are preferable to
claiming ownership without evidence. Hex IDs are longer than hashes but provide
an injective mapping without collision assumptions.

## Verification

The [remediation plan](../reviews/remediation-beta.21-2026-09-11.md) records
assignment-level red/green checks, parent repairs and final integration evidence.
Acceptance requires actual provider round-trips, real isolated directory links,
partial-response tests, orphaned owned sidecars alongside unrelated downloads,
an external SDK package consumer and generated viewer-reference checks.
Parent build, full Debug/Release coverage, format, candidate packaging, actual
SDK consumer, Windows Native AOT and fresh viewer acceptance passed. Nine cases
remain skipped on this host: eight Unix permission cases and one privileged
file-symlink case. Independent closure review found no remaining blocker in the
repaired scope; other platforms and remote execution remain unverified. See the
plan for failed intermediate checks, parent repairs and exact evidence paths.

## R1 Follow-up: Shared Project Writer

Accepted and implemented on 2026-09-14; this extends the project-update decision
above, without changing its validation policy or the public SDK contract.
The [later review](../reviews/release-readiness-beta.21-2026-09-13.md) found a
separate package writer that bypassed it.

- Package registration sends minimal patches through the existing `IConfigService`.
  Its singleton serializes read, merge, validation, staged replacement and reload,
  including updates from separate transient package services within the same host.
- Null patches delete against the current document inside that operation. A
  deletion-only nested patch does not create missing ancestors or null package
  entries. Valid semantic no-ops preserve bytes and do not trigger a reload;
  original-provider validation still runs. Explicit empty objects remain supported.
- A registration failure after extraction is a failed install, with an explicit
  retained-files diagnostic and no success announcement or next-feed fallback.
  Cancellation remains cancellation through the installer, restore and install
  command. No automatic rollback of extracted files is promised.
- The lock is per service instance, not interprocess or cross-host coordination.
  It does not protect against arbitrary external edits, crashes or power loss.

Keeping an independent package writer would duplicate validation and still race
with ordinary configuration commands. Locking only replacement would retain the
stale-read problem. Automatic extraction rollback was rejected because installation
can replace existing files and registration failure alone does not establish which
files can safely be deleted or restored.

The [R1 remediation record](../reviews/remediation-r1-beta.21-2026-09-14.md)
contains Worker trials, independent findings and parent integration evidence:
Windows and Linux full tests, actual Windows package installs/parallel restore,
and a fresh Linux Native AOT configuration round-trip. Other release/platform
gates remain explicitly separate.

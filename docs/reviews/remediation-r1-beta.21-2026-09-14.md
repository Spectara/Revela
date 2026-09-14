# Beta.21 R1 Remediation

- Status: R1 implemented and locally verified on Windows and Linux x64. Not a release approval.
- Input: [2026-09-13 review](release-readiness-beta.21-2026-09-13.md), preserved unchanged.
- Base: `088d79a6408b1d8fd1a748f1766d0c1c5aac7d10`; current R1 changes are uncommitted.
- Boundary: Revela workspace and its authorized isolated WSL Scout test copy only. No commit, push, tag, deployment or remote workflow in this R1 follow-up.
- Decision: [ADR 0004 R1 follow-up](../decisions/0004-review-data-integrity-boundaries.md#r1-follow-up-shared-project-writer).

## Implementation

`PluginProjectService` delegates tiny add/delete patches to `IConfigService`;
it no longer independently reads or writes project configuration or swallows
persistence exceptions. Existing mixed-case spelling, unrelated settings and
unpinned null entries survive registration.

The shared singleton serializes the whole read/merge/validate/replace/reload
operation. Queued cancellation releases admission correctly; disposal rejects
new work and defers semaphore disposal until admitted operations finish.
Deletion-only nested patches cannot resurrect removed sections. Semantic no-ops
still validate the original but neither rewrite it nor reload configuration.

Extraction and registration are distinct installer phases. Registration failure
returns failure, retains extracted files, logs the partial state and does not
try another feed. Cancellation propagates through the manager, parallel restore
and the public install command. Existing public constructor/DI registration is
retained; an internal installer constructor supports isolated command tests.

The host-owned Packages feature and Commands writer require these changes:
retaining the independent package writer failed validation and concurrent updates;
a plugin-only change cannot control host installation success or source fallback.
The existing SDK interface avoids a new Commands dependency or public contract.
No project schema, template context or second configuration layer was changed.

## Assignment and Review Record

Three sequential, bounded MAI Worker assignments covered the shared writer,
package registration/installer, then the independent review repairs. Model
configuration remained MAI Code 1.1; runtime identity was not exposed. The parent
owned decisions, release-script edits and final gates. No timing/cost comparison
or general model-reliability claim is made.

| Stage | Observed result and repair |
| --- | --- |
| Shared writer | Initial focused run: 21 passed, 2 failed, 3 skipped. It exposed a second read during blocked reload and concurrent replacement. A CA1001 analyzer failure required safe disposal; then 24 passed, 3 skipped. |
| Package registration | Regression: 51 passed, 1 failed, 3 skipped, including the real duplicate flattened package-key failure. Initial repair: 52 passed, 3 skipped; expanded suite: 70 passed, 3 skipped. Intermediate analyzer and log-capture fixture issues were repaired by the Worker. |
| Independent review | Found unlocked removal precheck could resurrect a deleted section, and the public install command swallowed cancellation. These were real implementation gaps despite earlier green tests. |
| Review repair | Deletion regressions: 76 passed, 9 failed, 3 skipped, then 85 passed, 3 skipped. After a local compile repair, command-cancellation regressions failed twice; final focused suite: 87 passed, 0 failed, 3 skipped. |
| Closure review | Source review confirmed both C# fixes. It found the smoke seed matched the default version and noted an unpinned intermediate reinstall. Parent guaranteed a different seed and pinned reinstall; the actual AST expression was checked with both default and candidate versions. |
| Parent smoke correction | Empty local Plugins plus two removed task-owned install directories makes restore create two new local registrations. Pre-populated matching versions would have tested no-ops. Controlled unit/integration barriers, not package scheduling luck, establish deterministic serialization coverage. |
| Final bounded review | Confirmed both remaining smoke-source findings closed and no new R1 implementation defect. Corrected the native-host evidence wording: its second process reapplied an already-true option, so that invocation proves reload/value retention rather than another value-changing write. |

Focused command:

```powershell
dotnet test --project tests/Commands/Commands.csproj --filter "FullyQualifiedName~ConfigServiceTests|FullyQualifiedName~PluginProjectServiceTests|FullyQualifiedName~PackageManagerRegistrationTests|FullyQualifiedName~PluginInstallCommandTests|FullyQualifiedName~RestoreCommandTests"
```

## Parent Verification

SDK 10.0.401 and runtime 10.0.12 were used on both hosts. Evidence is under
`artifacts/r1-20260914/`; both coverage suites have ten TRX reports and ten
Cobertura reports. Counts below were independently aggregated from TRX counters.

| Gate | Result |
| --- | --- |
| Windows build | Full serial Debug rebuild passed. Initial incremental build failed on an invalid generated Calendar reference assembly; rebuild required no source change. |
| Windows coverage | 1,270 total: 1,261 passed, 0 failed, 9 skipped. Eight Unix permission cases and one privileged file-symlink case were skipped. |
| Windows format | Passed after scoped formatting of three R1 test files: JSON annotations and final newlines only. |
| Windows release pipeline | Complete pass with version 0.0.1-beta.21, including Release tests (1,261 passed, 9 skipped), package install/uninstall, generation, compression, image preservation, isolated tool install/version/uninstall and actual SDK consumer. |
| Linux snapshot | All 769 tracked/nonignored source files matched recorded hashes after C# CRLF-to-LF checkout normalization. Raw Windows and expected Linux hashes are in `source-hashes.json`; no Windows build outputs copied. |
| Linux build and coverage | Full serial Release build: 0 warnings/errors. 1,270 tests: 1,262 passed, 0 failed, 8 Windows-only skips. |
| Linux format | Passed after building the Debug source generator required by the default design-time workspace. The first format attempt could not resolve generated config keys; no rule suppression or source change was used. |
| Linux Native AOT | Fresh ELF x64 publish passed. Actual config write changed maxEntries to 21; a second native process reapplied the already-true sortByCount option while retaining 21 and unrelated document data. Structured before/after/reopened assertions passed; the second invocation is a semantic no-op, not another value-changing write. |

The successful Windows pipeline is
`artifacts/release-test-20260914-092953/`, with transcript
`artifacts/r1-20260914/packaging-windows-retry.log` and SDK consumer
`artifacts/sdk-consumer-d2e675f1c3d2495ebdd73b47bd12e16a/`.
It verifies three actual public installer journeys:

1. A different version replaces a lower-case package entry under `Plugins` without adding `plugins`; a fresh provider/restore check reopens it.
2. One restore host installs two missing packages from the local feed and creates both previously absent local entries, retaining an unrelated sentinel; a fresh restore check passes.
3. A provider-valid but ambiguous case-split update fails after extraction with exit 1 and the retained-files diagnostic, no success text, unchanged project hash and retained DLL.

The pipeline also generated 25 pages and 228 variants, and proved valid image
hashes survive obsolete-variant cleanup. No private OneDrive feed was fetched.
The earlier `release-test-20260914-092610` run was interrupted at SDK-consumer
restore after the terminal was reused; its partial log is retained and is not
counted as a complete pass.

Native AOT evidence includes publish/version logs, binary SHA256 and all three
configuration snapshots. Initial probe mistakes are retained: globally passing
PublishAot incorrectly targeted the netstandard generator; using nonexistent
`config statistics --show` failed. The corrected publish uses the project's AOT
setting, and the fresh-process probe uses the existing noninteractive option API.

## Remaining Boundaries

- Same-host serialization is not a multi-process lock, filesystem transaction or crash/power-loss guarantee.
- Registration failure can leave installed files without a saved project declaration. Retention and explicit failure are deliberate; rollback and global/project atomicity are not promised.
- Actual packaged failure coverage uses a real rejected ambiguous update. Unit extraction tests inject persistence IO failure and cancellation through the existing interface; they do not claim packaged disk-full simulation.
- Linux full PowerShell packaging/tool installation remains unrun because Scout has no pwsh. Linux Native AOT is not a substitute for that modular packaging gate. Windows Native AOT was not republished in this R1 follow-up.
- macOS, ARM, remote release/website workflow execution and additional browsers/devices remain outside this verification. No theme/browser behavior changed in R1; old browser evidence is not presented as a fresh R1 run.
- Historical reviews and earlier remediation evidence remain unchanged. Release/tag/publication decisions require separate authorization.
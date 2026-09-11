# Beta.21 Review Remediation

- Status: Implemented and locally verified on Windows and Linux x64; Linux test portability correction verified. Remaining release gates are listed below. No release approval.
- Scope: Revela only. No commits, pushes, tags, deployments or remote workflow runs.
- Input: [independent review](release-readiness-beta.21-2026-09-07.md), preserved as a historical record.
- Method: [reasoned delegation](../subagent-patterns.md#assignment-contract).

## Work Packages

| Order | Findings | Owner | Scope | Acceptance |
| --- | --- | --- | --- | --- |
| 1 | F1, F3, F9 | Worker MAI: OneDrive | OneDrive source plugin and its tests | Real junction/symlink boundary tests preserve external files; interrupted and cancelled downloads preserve prior bytes; synthetic secrets absent from console/HTTP logs. |
| 2 | F2 | Worker MAI: project configuration | ConfigService and configuration tests | Mixed-case updates round-trip through the real configuration provider; invalid replacement leaves the original readable. |
| 3 | F4 | Worker MAI: global configuration | GlobalConfigManager and its tests | Real isolated write/read round-trip retains unrelated root and nested settings. |
| 4 | F6 | Worker MAI: compression | Compression ownership/invalidation and tests | Preserve unrelated gzip/Brotli downloads; remove obsolete plugin-owned sidecars. Ownership design is decided before assignment. |
| 5 | F5 | Worker MAI: restore | Dependency reader/writer contract and host tests | Real host reports a declared missing dependency without downloads; installed dependencies remain recognized. |
| 6 | F7 | Worker MAI: SDK packaging | Generator packaging and external-consumer check | Build a consumer from the actual SDK package without repository-inherited props/targets; generated template conversion must compile. |
| 7 | F8 | Worker MAI: viewer identity | PhotoPageCatalog and adjacent tests | Distinct path encodings, root/home and shared-image memberships yield unique IDs and exact navigation targets. |
| 8 | Serve races | Worker MAI: server lifecycle | StaticFileServer and its tests | No abandoned handlers or cleanup locks; reliable listener allocation; complete solution coverage run passes. |
| 9 | Smoke warnings | Parent | Release test script and command-contract decision | clean images removes injected obsolete variants and preserves valid image hashes. Resolve config locations against an explicit contract, not an assumed feature. |

## Coordination

- Scouts research bounded unresolved behavior read-only; Workers receive exact editable files and one focused acceptance command before implementation.
- Both MAI agents remain pinned to MAI Code 1.1. Runtime model identity is reported only if observable.
- The parent owns architecture, shared files, resource allocation, integration, release notes and the final gate.
- No concurrent mutation of build outputs, generated directories, global configuration, terminal sessions or browser pages. Workers run serially when sharing these resources.
- Tests use isolated synthetic files and in-memory HTTP. No private feeds, real credentials or user photo directories.
- The parent reads each diff and result, resolves contradictory evidence and distinguishes Worker results from parent repairs.
- Independent Reviewer checks the integrated fixes. UX Advocate verifies fresh generated viewer behavior on an assigned isolated preview.

## Final Gate

1. Full build, full tests with coverage/TRX, then format verification.
2. Local release packaging, actual SDK consumer, modular host checks and Windows Native AOT.
3. Fresh isolated site generation plus HTML/asset/link checks and desktop/mobile viewer checks, including no-JS and reduced motion where applicable.
4. Local documentation links, workflow guards and review findings reconciled by ID.
5. Other operating systems/architectures and remote workflow behavior remain explicitly unverified until separately authorized CI execution.

## Progress and Evidence

- Planning: worktree includes the earlier dependency, theme-files and deployment-guard changes; preserve them.
- Implementation authorized on 2026-09-11. Findings F1-F9 and both smoke warnings now have implementations and focused regression evidence; see the final evidence below. Platform-specific cases remain explicitly unverified.
- Four read-only assignments completed: Scout MAI for OneDrive, Scout MAI for configuration, Scout MAI for compression, and Explore for remaining contracts. Worker assignments now proceed serially with parent integration.
- Parent inspected the reported owning code paths and corrected unsafe/incomplete suggestions. The initial planning round performed no executable reproduction; subsequent implementation and parent-owned gates are recorded below.
- MAI model pins were unchanged; runtime identity was not exposed. No model substitution. Preparation/review effort is recorded as four scout assignments plus parent source inspection; no timing/cost comparison is claimed.

## Implementation Assignment Boundaries

Every Worker receives the five-field assignment contract: Goal, Allowed Scope,
Do Not Change, Acceptance, Return. The following records the assignment boundaries;
the final evidence section reports execution and deviations.
All commands below run from the repository root, with exclusive ownership of
build/test outputs for that assignment. Add the regression first and verify that
the current code fails for the reported reason before applying the fix.

### A: OneDrive (F1, F3, F9)

- Allowed production files: `src/Plugins/Source/OneDrive/Services/DownloadAnalyzer.cs`, `Commands/OneDriveSourceCommand.cs`, `Providers/SharedLinkProvider.cs`, and `OneDrivePlugin.cs` within that same plugin.
- Allowed tests: existing `Services/DownloadAnalyzerTests.cs`, `Commands/OneDriveSourceCommandTests.cs`, and `Providers/SharedLinkProviderTests.cs` under `tests/Plugins/Source/OneDrive/`.
- Execute as three serial slices: F1 enumeration/deletion boundary, F3 staged download, F9 console/HTTP confidentiality. Do not edit Calendar or introduce an SDK contract.
- Preserve include/exclude rules, ordinary confirmed cleanup, dry-run, download timestamps, cancellation and existing retry policy. Never test on user directories or real credentials.
- F1: exclude linked child entries; freshly validate containment and every relevant path component immediately before deletion, not cached FileInfo attributes. Test a directory changed into a link after analysis. A check-before-delete is not a hostile-filesystem-race guarantee.
- F3: stream to an exclusively created sibling temporary file; set its timestamp and check cancellation before replacement. Keep original bytes/timestamp on body, timestamp or publication failure. Use a non-buffering HTTP handler: the existing shared response-cloning helper consumes bodies too early to exercise partial streaming.
- F9: assert synthetic share/CDN/token sentinels are absent from console, structured logs and exception output. Prove requests and logging capture actually occurred; removing default HTTP loggers alone does not verify resilience logs.
- Accepted by user: the configured source root may intentionally be a link. Treat it as the chosen boundary; exclude linked descendants and recheck them before deletion. Do not claim protection against hostile concurrent filesystem replacement.
- Focused command: `dotnet test --project tests/Plugins/Source/OneDrive/OneDrive.csproj`.

### B: Project Configuration (F2)

- Allowed files: `src/Commands/Config/Services/ConfigService.cs` and `tests/Commands/Config/ConfigServiceTests.cs`.
- Resolve existing configuration keys using ordinal-ignore-case comparison while preserving their spelling. Keep null removal, recursive object merge and complete array/scalar replacement.
- Validate both the original and the exact replacement bytes using the real Microsoft JSON configuration provider before replacing the file. JSON syntax validation alone misses collisions between colon-delimited keys and nested paths.
- Regression cases: changed mixed-case value, mixed-case removal, sibling preservation, JSONC, arrays, and a flattened-key collision rejected without changing original bytes or live configuration. A malformed existing file must not be silently replaced by defaults.
- Focused command: `dotnet test --project tests/Commands/Commands.csproj --filter FullyQualifiedName~Spectara.Revela.Tests.Commands.Config.ConfigServiceTests`.

### C: Global Configuration (F4)

- Allowed files: `src/Core/Services/GlobalConfigManager.cs` and a new `tests/Core/Services/GlobalConfigManagerTests.cs` if no suitable existing test file is found.
- Use an internal explicit-path test constructor or an existing equivalent, never real AppData/global config. Preserve the production default-path contract.
- Verify real AddFeed/AddPlugin writes, reconstruction of a new manager, removal, unrelated root settings, nested unknown settings, nulls, arrays, and mixed-case known sections.
- Design settled in [ADR 0004](../decisions/0004-review-data-integrity-boundaries.md): preserve the JSON document and coalesce touched, provider-valid split sections losslessly. Do not assume case-insensitive DTO deserialization solves it.
- No claim of global crash/concurrency safety from this preservation fix. If malformed-original handling must change, state that explicitly before implementation.
- Focused command: `dotnet test --project tests/Core/Core.csproj --filter FullyQualifiedName~GlobalConfigManagerTests`.

### D: Compression Ownership (F6)

- Allowed production scope: `src/Plugins/Compress/Services/CompressionService.cs`, `Services/CompressedSiteInvalidator.cs`, `Commands/CompressCommand.cs`, `Commands/CleanCompressCommand.cs`, and `CompressPlugin.cs`. A plugin-private ownership helper is permitted only after its design is settled.
- Reuse the four matching test classes in `tests/Plugins/Compress/Services/` and `Commands/`.
- Fix all destructive paths, not just invalidation: small-file sibling cleanup and producer overwrite can also destroy unrelated files.
- Design settled in ADR 0004: persist explicit ownership outside disposable caches in `.revela-compress.manifest`. Reject conflicting/invalid ownership and preserve unowned files. Keep the record plugin-private, without changing the SDK artifact API.
- Acceptance must cover real production then orphan cleanup after service reconstruction, unrelated gzip/Brotli files, unowned same-name collisions, cancellation, and failed cleanup without losing ownership evidence.
- Focused command: `dotnet test --project tests/Plugins/Compress/Compress.csproj`.

### E: Dependency Contract (F5)

- Candidate owning files: `src/Sdk/Configuration/DependenciesConfig.cs`, `src/Sdk/Configuration/ConfigurationServiceCollectionExtensions.cs`, `src/Features/Packages/Commands/Restore/DependencyScanner.cs`.
- The documented and writer-used root `plugins`/`themes` maps are the contract to preserve. Do not migrate writers to a new `dependencies` nesting just to match the broken reader.
- `theme.name` is the active theme contract; blindly binding the root to an obsolete scalar Theme property is not an adequate fix. Preserve explicit package versions and global/local override behavior with trim-safe binding.
- Reuse `tests/Commands/Packages/RestoreCommandTests.cs` with real reader/options/scanner and command invocation; keep global configuration isolated. Two attempted CLI-test assignments were blocked by eager user-config loading and internal assembly access, so the parent moved the tests to the already authorized Commands test assembly.
- Focused acceptance: `dotnet test --project tests/Commands/Commands.csproj --filter FullyQualifiedName~Restore`. Parent additionally runs the actual packaged host with a declared missing plugin, exact expected failure code and package-specific diagnostic.

### F: SDK Consumer (F7)

- Owning files: `src/Sdk/Sdk.csproj`, existing `src/Sdk/build/Spectara.Revela.Sdk.targets`, `src/Sdk.Generators/Sdk.Generators.csproj`, and `Directory.Build.targets` only if required to prevent duplicate analyzer registration. Parent owns shared build files and release-script integration.
- Include the RID-independent generator as an analyzer in the actual SDK package. Do not introduce a runtime Roslyn dependency. Inspect the generated code's Scriban reference requirement rather than assuming it is supplied by the SDK.
- Acceptance: inspect the actual nupkg and build a minimal attributed consumer using only package references. Disable repository Directory.Build/Directory.Packages inheritance, use a local package feed and outputs under this task's artifacts directory, and call the generated conversion method.
- Acceptance: `scripts/test-sdk-consumer.ps1`, including `-PackageDirectory` against actual release packages. The package consumer explicitly references Scriban. Parent discovered that rebuilding pack concealed a `pack --no-build` failure; the target now queries `GetTargetPath` and verifies an existing analyzer without recompilation.

### G: Viewer Identity (F8)

- Owning file: `src/Features/Generate/Infrastructure/PhotoPageCatalog.cs`; existing `tests/Commands/Generate/Infrastructure/PhotoPageCatalogTests.cs` and the nearest integration test. Parent owns browser-script adjustments if actual expected IDs change.
- Require an injective encoding with disjoint namespaces for root, path and numbered-grid membership. Cover path separators, hyphens, underscores, escape characters, root/home, and names resembling grid suffixes. Hash-only or slash-to-underscore substitution is not a uniqueness proof.
- Preserve page/image routes and image membership/order; only ID construction and references change together. Check common photos in colliding galleries and distinct colliding image slugs.
- Focused command: `dotnet test --project tests/Commands/Commands.csproj --filter FullyQualifiedName~PhotoPageCatalogTests`; parent additionally owns fresh generated HTML and browser navigation/back-target checks.

### H: Serve Lifecycle and Smoke Contracts

- Serve scope: `src/Plugins/Serve/StaticFileServer.cs` and `tests/Plugins/Serve/StaticFileServerTests.cs`.
- Track and await all request handlers before disposal completes; verify file handles are released before deleting test directories. Keep existing HTTP semantics and cancellation behavior.
- Port gate: holding a TcpListener on a port while HttpListener binds it cannot work. Establish port ownership with successful binding of the actual server, with narrowly scoped bind-conflict handling if needed; never retry failed HTTP assertions to hide lifecycle races. Do not replace HttpListener just for the test.
- Focused command: `dotnet test --project tests/Plugins/Serve/Serve.csproj --coverage --coverage-settings coverage.config`; parent must repeat the previously failing full-solution coverage gate. Repeated lucky runs alone do not prove race elimination.
- Parent-owned `scripts/test-release.ps1`: inject one obsolete image variant, assert it is removed, and assert valid variants retain their hashes. Do not expect clean images to erase all valid images.
- Accepted by user: `config locations` additionally reports project, site and local logging configuration paths when inside a project; it remains usable outside a project. Relevant owner: `src/Commands/Config/Revela/ConfigLocationsCommand.cs`.

## Parent Review Decisions

- Accepted the OneDrive scout's non-buffering HTTP fixture warning and fresh deletion checks. User explicitly approved linked-root support with descendant links excluded.
- Accepted actual-provider validation for F2. Did not accept extension-data-only handling for every F4 input without case-split-section tests.
- Compression scout correctly found producer and clean-command deletion paths. Its larger pending-publication journal is an option, not an automatic implementation requirement; justify only the state needed for tested failure modes.
- Corrected Explore's F5 alternatives using the existing documented root-map contract and actual binding registration.
- Rejected Explore's unproven slash-to-underscore ID substitution and held-TcpListener port reservation. Corrected its config-locations ownership to the actual command.
- Independent reviews and parent verification were subsequently completed as described below; the initial four read-only analyses alone were not implementation evidence.

## Final Finding Status

| Finding | Implementation and executed evidence | Remaining limits |
| --- | --- | --- |
| F1 | Explicit reparse exclusion, fresh deletion checks, real directory-junction swap regression and allowed linked-root tests. | Windows file-symlink case skipped without privilege; no hostile path-race guarantee. |
| F2 | Case-aware merge and actual provider validation before replacing; 19 logic tests plus 3 Unix mode cases. | Unix cases compiled but skipped here; ambiguous touched split sections fail unchanged. |
| F3 | Exclusive sibling staging, timestamp-before-commit and original-byte preservation on partial body/cancellation. | Unix mode cases skipped; no multi-file sync transaction. |
| F4 | JSON-preserving global writer, isolated actual save/reopen round-trips, invalid originals fail closed; 36 logic cases. | Two Unix mode cases skipped; no external-writer cache coherence guarantee. |
| F5 | Root package binding, theme.name, exact plugin identity and installed theme-extension handling; 27 Restore cases plus packaged-host positive/negative check. | Version solving is unchanged. Embedded package IDs follow the existing assembly-name convention. |
| F6 | Explicit sidecar ownership across producer/cleanup/invalidator; 84 tests plus AOT preservation of an independent gzip while 56 owned sidecars are invalidated. | Crash publication/registration gaps can leave an unowned file requiring manual resolution. |
| F7 | Analyzer included in SDK nupkg; no-build pack verified against unchanged DLL hash; actual release package consumer builds and renders without repository imports. | Consumer explicitly needs Scriban for generated ScriptObject code. |
| F8 | Injective UTF-16 hex ID encoding; 28 catalog and 26 generation cases, fresh HTML audit and browser navigation. | Browser execution in this round used Edge only. |
| F9 | Host/hash console reference and sanitized command/default HTTP/Polly logs; real-DI synthetic requests and capture positive controls. | Real provider traffic, shell history and custom telemetry subscribers excluded. |
| Serve races | Tracked/drained handlers, actual listener binding, controlled stop-before-accept checks; 55 focused cases under coverage. | Other OS bind-error mappings not executed here. |
| Smoke warnings | Project/site/logging locations required; obsolete-size injection plus dry-run and 228 valid-file hash checks. | Online package discovery can still report no published themes; no private provider smoke runs. |

## Worker and Review Record

All implementation workers retained the configured MAI Code 1.1 pin. Observed
runtime identity was not exposed; no model override was requested. Workers were
serialized for shared terminal/build ownership. Preparation, local repairs and
review effort are described here; timing/cost measurements were not collected.

- F1: behavioral red with linked descendants, then 33 passing and one privileged-file-link skip. One test analyzer repair.
- F3: partial-stream red, then 21 passing; corrected one Windows exception-type expectation. Later privacy and Unix-mode tests expand this file's case count.
- F2: duplicate-key red, then 19 passing; later permission repair adds three Unix-only cases.
- F4: one initial test-visibility/style repair, actual DTO-loss red, then 36 passing; later permission repair adds two Unix-only cases.
- F9: console, HTTP, Polly and command-failure regressions were independently red. Removing HTTP loggers alone left Polly leaks; the worker repaired pipeline-local telemetry. Test analyzer repairs were necessary. Final OneDrive project at that stage: 111 passed, one skipped; later Unix tests add three skips.
- F6: unrelated-download red, then 80 passing after analyzer/fixture repairs. Independent review found primary exceptions could be masked by cleanup; four additional failing tests were repaired, yielding 84 passing.
- F5: two assignments produced no retained edits because the parent's isolated full-host proposal lacked a usable test-access boundary. Parent relocated the tests to Commands. Eight contract regressions were red, then 15 passed. Review found ignored extensions and substring identity matching; 12 more regressions produced 27 passing.
- F7: package consumer initially failed to compile; analyzer packaging made it pass. Parent release integration then found NETSDK1085 with `pack --no-build`; worker changed reference resolution to GetTargetPath. Built-before, built-after and packaged generator hashes were identical, and the actual no-build package consumer passed.
- F8: 13 collision failures before the fix, then 28 catalog passes. One analyzer repair preceded 26 integration passes. Parent normal viewer and full local-target checks passed without changing JS/routes.
- Serve: controlled disposal failures, then cancellation failures, were repaired; 53 passed. Independent review found the stop/accept exception race; an actual stopped-listener test was red, then all 55 passed.
- Config locations: three expected failures before implementation, then seven passing cases with bracket-containing paths and outside-project behavior.
- Independent data-integrity review found Unix mode broadening during staging and compression secondary-exception masking. Workers corrected both; eight Unix-only tests remain unexecuted on Windows.
- Independent integration review also tightened the parent's missing-dependency smoke assertion: arbitrary nonzero exit is no longer accepted.
- Parent's next packaging run found actual theme-extension IDs incorrectly derived from display names with spaces. The worker corrected EmbeddedTheme to use the resource assembly identity and added real embedded-theme regressions: 30 focused theme cases passed. Registry-only or looser Restore matching would not fix this metadata defect.
- Final independent closure review found no remaining blocker within the repaired scope. It was read-only; it did not independently rerun parent checks. UX Advocate separately inspected four fresh screenshots and found no concrete new visual regression, with faint light-scheme controls remaining a cosmetic observation.

## Parent Verification

- Serial full Debug build passed. The first integrated Debug coverage run had **1,225 total, 1,216 passed, 0 failed, 9 skipped**. After two real embedded-theme identity regressions were added, final Release coverage/TRX had **1,227 total, 1,218 passed, 0 failed, 9 skipped**, verified by aggregating all ten TRX files. The skips are eight Unix permission cases and one Windows privileged file-symlink case. The previous Serve full-run failures did not recur.
- Format verification initially found final newlines, JSON test-literal hints and qualified type names in five touched files. Parent applied scoped formatter fixes; verification passed afterward.
- Full candidate pipeline `scripts/test-release.ps1 -Version 0.0.1-beta.21 -RuntimeIdentifier win-x64 -KeepArtifacts` passed after the two packaging repairs. It built/tested/packed 13 packages, installed/reinstalled plugins, exercised real Restore and theme-files checks, generated 25 test pages and 228 variants, tested compression and valid-image preservation, and installed/verified/uninstalled the isolated tool.
- Actual release SDK consumer passed with exactly one packaged analyzer, matching restored-package hash, no repository Directory.Build/Directory.Packages inheritance, no project references/manual analyzer inclusion, and correct generated ScriptObject/rendered values.
- Windows Native AOT `0.0.1-beta.21` published and ran; fresh isolated Showcase generated **22 pages, 14 images, 228 variants** without cache reuse. AOT compression generated and invalidated **56 owned sidecars**, preserving an independent gzip download hash.
- Edge **152.0.4191.66** passed desktop/mobile, light/dark, JS/no-JS and reduced-motion combinations in the existing Lumina acceptance. Fresh DOM parsing checked **908 local references and 76 fragments**, with unique per-page IDs and no missing targets. Four light-scheme desktop/mobile screenshots were independently inspected.
- Execution used installed .NET SDK **10.0.401** / runtime **10.0.12**, allowed by the existing SDK roll-forward policy; package/version pins were not changed in this remediation.

Local evidence (generated, not a guaranteed checkout artifact):

```text
artifacts/remediation-beta21/coverage-debug/      TRX and Cobertura
artifacts/remediation-beta21/coverage-release/    final TRX and Cobertura
artifacts/remediation-beta21/release-pipeline.log           initial no-build pack failure
artifacts/remediation-beta21/release-pipeline-final.log     real theme-identity failure
artifacts/remediation-beta21/release-pipeline-verified.log  completed pipeline
artifacts/release-test-20260911-165608/            candidate packages and CLI
artifacts/sdk-consumer-faa12a6e0d5a4d93b054a283d4b038dc/    actual release consumer
artifacts/releases/standalone-20260911-165912/     Native AOT candidate
artifacts/remediation-beta21/showcase/            fresh AOT output
artifacts/browser-checks/edge/                   fresh screenshots
```

## Remaining Release Gates

- Linux x64 build, Native AOT, Unix permissions and file-symlink cases were executed in the WSL follow-up below. The initial Serve test failure is corrected and the complete Linux coverage run now passes. macOS/ARM execution and the full Linux modular packaging/installation pipeline remain open.
- Firefox/WebKit, real devices, touch/zoom and assistive technologies were not rerun or certified in this remediation.
- No real OneDrive/calendar feed was fetched. Provider completeness, DNS rebinding, byte quotas and hostile filesystem mutation remain outside the verified contract. OneDrive body-wide deadlines are still a separately reported risk, not claimed fixed by staging.
- No remote workflow, attestation, signing, commit, push, tag or publication was performed. Manual Release CI still has attestation/signing side effects even though automatic website deployment is guarded.
- This is a locally verified remediation candidate, not final release approval. The historical independent report remains unchanged for the next independent review.

## WSL Scout Follow-up (2026-09-11)

The user reported approval from the independent follow-up review and requested
testing in WSL Scout. This section records new executed evidence, not a new
independent review or release approval. The initial testing-only run changed no
implementation or tests; the subsequently authorized test correction is recorded
separately below.

### Environment and Source

- WSL2 distribution `Scout`, Ubuntu 26.04 LTS, Linux x86-64, user `scout`.
- Tests ran on the Linux filesystem, not `/mnt/d`, in
	`/home/scout/revela-beta21-20260911-7d8eeeb4/repo`.
- No existing .NET SDK was installed. SDK 10.0.401 / runtime 10.0.12 was installed
	privately beneath the task directory using the official dotnet-install script.
	Ubuntu's libicu78 package was downloaded and extracted there without system
	installation or elevation. HOME, NuGet cache and config/temp paths were isolated.
- Snapshot: 766 tracked/untracked source files from HEAD
	`de7f70af61cbfeed3c41c7acf88a6c04980bf3c6` plus the current worktree changes.
	Windows source hashes and the complete Linux copy were compared after testing:
	no source changes or unexpected differences.
- The initial byte-preserving archive retained Windows C# CRLF endings and hit
	IDE0055 in ImageConfig.cs. Only snapshot C# endings were normalized to LF,
	matching the repository's `*.cs text` Linux-checkout behavior. The 489 affected
	files had no other content changes; the normal build then passed with all
	analyzers enabled. No Windows source or repository rules were modified.

### Executed Checks

```text
dotnet build Spectara.Revela.slnx -c Release -m:1 -p:Version=0.0.1-beta.21 -p:DebugType=embedded
dotnet test --solution Spectara.Revela.slnx -c Release --no-build --report-trx --coverage --coverage-settings coverage.config --coverage-output-format cobertura
```

- Standard Release build: exit 0, zero warnings/errors after checkout-form EOL normalization.
- Full Release coverage/TRX: **1,227 total, 1,218 passed, 1 failed, 8 skipped**,
	test runner exit 2. All ten test hosts reported results.
- **All nine formerly Windows-skipped cases passed:** eight Unix permission
	cases and `DeleteOrphanedFile_FileSwappedToLinkAfterAnalysis_RejectsLink`.
	The eight Linux skips concern Windows-specific read-only/file-sharing failure
	injection and were not counted as passes on Linux.
- Linux-x64 Native AOT publish succeeded with the release script's relevant
	publication flags. The resulting ELF binary reports
	`revela 0.0.1-beta.21 (.NET 10.0.12)` with the embedded-build marker.
- That binary generated a fresh isolated Showcase: **22 pages, 14 source images,
	228 image variants**. It created and invalidated **56 owned compression
	sidecars** while preserving an independent gzip download's hash.

### Initial Linux Test Failure (Resolved)

`Server_StartWithBoundPrefix_ClosesFailedListenerWithoutAffectingOwner` initially failed in
[StaticFileServerTests.cs](../../tests/Plugins/Serve/StaticFileServerTests.cs#L798).
The real duplicate-listener-prefix operation throws `HttpListenerException`
with `NativeErrorCode == 400` on this Linux runtime. The test helper
`IsBindCollision` recognizes other platform error codes but not this one.

A focused rerun of that single test reproduced the same failure: one failed,
zero passed/skipped. This is not an intermittent successful rerun and does not
cancel the failed full-suite result. No blanket acceptance of every 400 error
was added. The remaining owner-availability assertions after the failed check
were not reached in this test; the ordinary Serve cases passed.

The user then authorized the correction below. The original full-suite and
isolated failure reports were retained rather than overwritten.

### Bind-Conflict Correction

- Only `tests/Plugins/Serve/StaticFileServerTests.cs` changed; the production
	server is unchanged. Existing Windows/socket error recognition is retained.
- On Linux/macOS, code 400 is recognized only with the exact managed-runtime
	duplicate-registration message for the prefix being bound. A bare 400,
	unrelated message, different port/path, or wrong error code is rejected.
- Test listener starts temporarily use invariant UI culture for this diagnostic
	comparison and restore the previous culture in `finally`. The real conflicting
	listener test checks restoration on failure and confirms the original server
	still serves its response after the competing listener fails.
- The implementation was grounded in the .NET 10.0.12 managed
	[AddPrefix error path](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Net.HttpListener/src/System/Net/Managed/HttpEndPointListener.cs)
	and its
	[net_listener_already resource](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Net.HttpListener/src/Resources/Strings.resx).
	A future changed diagnostic fails closed rather than broadly retrying all 400s.
- First focused Linux validation after the edit: **7 passed, 0 failed/skipped**,
	including the original real-listener regression and six new classification
	cases. Full Release build: exit 0, zero warnings/errors.
- Final Linux Release coverage/TRX: **1,233 total, 1,225 passed, 0 failed,
	8 skipped**, exit 0. All ten hosts completed, including 61 Serve tests. The
	skips remain Windows-only read-only/file-sharing fault-injection cases.
- Windows tests from the changed file: **51 passed, 0 failed**. Repository
	`dotnet format --verify-no-changes --no-restore` passed.
- The Linux copy received only the updated test file, with the same documented
	checkout-form LF normalization. Prior native binary/generation evidence is
	reused because no production inputs changed.

### Evidence and Limits

Copied evidence is under `artifacts/wsl-beta21-20260911-7d8eeeb4/`:

```text
source.tar, source-hashes.json, snapshot-verification.json
build.log, format-probe.json, build-linux-eol.log
tests.log, tests/                 full Linux TRX and Cobertura
serve-repro.log, serve-repro/     unchanged isolated failure reproduction
serve-fix-focused.log, serve-fix-focused/   corrected focused tests
build-serve-fixed.log
tests-serve-fixed.log, tests-serve-fixed/   corrected full coverage/TRX
aot-publish.log, aot-version.log
aot-generate.log, aot-compress.log, aot-invalidate.log, aot-smoke-summary.log
```

The native binary remains at
`/home/scout/revela-beta21-20260911-7d8eeeb4/standalone/revela`, SHA-256
`68d2283ac13d0f0a0fe690244836a7c7c4b4084593f9ce166472b1e04b27a340`.
This follow-up did not run the complete Linux PowerShell packaging/tool-install
script, another SDK consumer build, browser checks, macOS/ARM artifacts, remote
workflows or real provider downloads. The Linux build/test gate is green after
the correction; this does not substitute for the remaining full release gates.

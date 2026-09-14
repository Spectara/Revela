# Pipeline Alignment Remediation

- Status: Implemented and locally verified; hosted execution is still a separate gate.
- Date: 2026-09-14.
- Decision: [ADR 0005](../decisions/0005-release-artifact-verification.md).
- Scope: Revela pipeline/scripts/documentation only. Existing uncommitted R1 product changes and historical reviews are preserved.
- No commit, push, tag, remote workflow, signing or deployment was executed.

## Changes

CI now verifies formatting after preparing the Debug source generator, collects
coverage from the configured TestResults directory, runs offline workflow
contract tests and the common modular release suite, and uploads test evidence.
Its existing three-OS native AOT canary remains.

The Release package job tests the actual SDK package with an isolated consumer.
Each platform job feeds its final Core, Full and Standalone archives to the same
release test before archive attestation/signing. Build mode creates release-shaped
Full inputs; Artifact mode copies supplied directories/ZIP/TAR inputs and never
restores/builds/publishes/packs the product. Both install Lumina from its package,
not a loose extra DLL. The tool test consumes and hashes the existing package
instead of repacking it. SDK consumer compilation remains an intentional test.

The suite checks exact host/version, native companions, existing Unix execute
permissions, real generation, strict compression/cleanup results and all R1
registration regressions. Core is explicitly tested with an external fixture
feed. Only its parallel restore probe temporarily prioritizes that feed as a
bundled directory; it removes the directory and verifies installed DLL hashes
against the fixture package entries. This is not an out-of-box Core theme claim.

Automatic website deployment consumes `release-identity-<attempt>` from the
successful tag-push Release run. It checks run/attempt, head SHA, tag/version,
published release and tag-resolved commit, then checks out and downloads that
identity. Attempt-specific artifact names avoid immutable-upload collisions.
Manual website dispatch retains its selected source and latest release behavior.
Missing/stale metadata fails closed. NuGet publishing remains explicitly disabled.

## Parent Verification

Windows SDK 10.0.401/runtime 10.0.12; Linux Scout uses the same product toolchain.
PowerShell 7.6.0 was installed privately in the already authorized Scout test
root from an official SHA256-verified archive, without sudo or system changes.
Actionlint 1.7.12 was downloaded and checksum-verified under task artifacts.

| Check | Result and evidence |
| --- | --- |
| Full directory | Worker common suite passed with 13 packages, 25 pages, 14 images/228 variants; SDK/tool hashes and 38 input hashes matched. `artifacts/pipeline-20260914/full-run-03.log`. |
| Core ZIP | Full shared suite passed after the review repairs, including two new parallel registrations and DLL-to-nupkg hashes; 38 input hashes unchanged. `artifacts/release-test-20260914-164346-c7f276bfb8ec4ba1b50c90167abd778d/verification.log`. |
| Full ZIP | Full shared suite passed with no loose theme, no product rebuild and 25 unchanged input hashes. `artifacts/release-test-20260914-164529-9263726169ad417ba8cd041c46e67968/verification.log`. |
| Standalone Linux TAR | Current R1 native executable plus its libvips companion passed exact version, actual config change 19 to 20 with unrelated settings retained, 22 pages/228 variants, clean and regeneration. `artifacts/pipeline-20260914/standalone-linux.log`. |
| Negative artifacts | Wrong version, missing Core feed, missing native companion, unexpected loose theme DLL and nonexecutable Linux TAR all rejected for the intended reason. Unix test did not repair permissions; see `artifacts/pipeline-20260914/nonexecutable-linux.log`. |
| Windows Build mode | Full solution build, 1,270 tests (1,261 passed, zero failed, nine platform skips), publish, no-build pack and complete common suite passed. `artifacts/release-test-20260914-165608-6586ef51f5df4ee6ad0a77068282e27b/verification.log`; SDK consumer `artifacts/sdk-consumer-09e7e770ed354afd90f03cb1551678f3/`. |
| Format | `dotnet format Spectara.Revela.slnx --verify-no-changes --no-restore --verbosity minimal` passed after the integrated build/test run. |
| Workflow fixtures | `node --test scripts/tests/resolve-website-release.test.cjs`: 33 passed. Exercises actual inline producer/resolver, missing/mismatched run metadata, moved/missing/draft releases, manual path, attempt-specific names and gate ordering using synthetic API responses. |
| Syntax | Both changed PowerShell scripts parse. Actionlint reports no new findings; the sole baseline diagnostic is the deliberate disabled NuGet job's constant `if: false`, retained without weakening the publishing restriction. `artifacts/pipeline-20260914/actionlint-final.json`. |

Core/Full archives used the already tested local beta.21 managed executable and
packages; the Linux TAR used the existing current-code R1 Native AOT executable.
They were created locally using the release layouts, not downloaded from an
executed GitHub run. No private OneDrive feed was fetched. Public NuGet restores
for the SDK consumer and build prerequisites were allowed; these are not offline
test claims. The script retains `verification.log`, `input-hashes.json` and
`after-hashes.json`; current behavior retains evidence even without KeepArtifacts.

## Review and Trial Record

Four bounded MAI Worker assignments covered CI, the common artifact suite,
website binding and operational documentation. Pattern Finder and Scout supplied
read-only script/bootstrap references. MAI configuration remained pinned; runtime
model identity was not exposed. Parent owned architecture, workflow integration,
shared resources and final checks. No timing/cost comparison is claimed.

- CI Worker: static assertions initially hit output/encoding checker issues; eight assertions then passed. Debug generator build and format passed. YAML parser was unavailable to the Worker; parent later used Actionlint.
- Artifact Worker: first parser check passed. Full directory acceptance passed on its third execution after fixing version-output and wrapped-filename assertions. These were script defects, not product defects.
- First independent review found four real gaps despite that green run: Standalone suffix rejected, remote-first Core restore, warning-only cleanup checks and repaired executable permissions. Parent fixed all four and ran actual Core/Full archive and Linux native/permission tests.
- Website Worker: 31 offline fixtures passed on the first check. Parent added the producer and CI wiring. Follow-up review found immutable artifact name reuse and one inaccurate retention statement in new documentation. Parent changed producer/consumer to attempt-specific names, corrected documentation and expanded to 33 passing fixtures.
- Full Build-mode acceptance initially failed with CS2012: concurrent writes to the shared generator DLL. Parent used explicit generator prebuild and serialized solution compilation in local scripts, CI and Release. The same full acceptance then passed. No product-source change or assertion relaxation was required.
- An initial Actionlint download used a nonexistent Windows TAR filename; the published ZIP and digest corrected that tool setup. A negative-test invocation initially used incorrect PowerShell splatting and did not reach the script; corrected invocation verified the intended failures. Neither is a product failure or test pass.

## Remaining Gates

- No hosted workflow was executed. Actual GitHub artifact transport, runner images, signing/attestation, Pages permissions and event payload behavior remain unverified.
- Automatic deployment requires the consumer on the default branch and a new Release run containing the identity producer. Older runs without metadata cannot satisfy it; a partial rerun must not reuse another attempt's identity.
- Annotated-tag payload compatibility was not observed through a real GitHub event. Local fixtures verify mismatches fail, not the platform's emitted SHA semantics.
- Windows Core/Full and Linux Standalone were exercised. The complete new modular suite on Linux/macOS/ARM and Standalone Windows/macOS/ARM remains a hosted/platform gate. The ARM Ubuntu image's public inventory lists PowerShell; that is prerequisite evidence, not execution evidence.
- Native AOT version/config/generation checks do not replace browser accessibility/visual tests. No theme/UI behavior changed, and no fresh browser result is claimed.
- Tests accept first-party release inputs inside the repository and reject linked input content. They are not a sandbox for arbitrary hostile executables or archives.
- This pipeline change is independent of the completed R1 repair. No historical review or R1 production behavior was rewritten.
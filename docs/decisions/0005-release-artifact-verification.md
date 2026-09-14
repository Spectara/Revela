# Release Artifact Verification

- Status: Implemented; locally verified. Hosted release/deployment execution remains unverified.
- Date: 2026-09-14
- Scope: Local release scripts, CI, release and website workflows in Revela.

## Context

The local release test injects a loose Lumina DLL and rebuilds the CLI tool
package. The release workflow tests the solution but does not execute its final
platform archives. These are useful checks, but do not establish that users can
run the downloaded artifacts. CI also lacks the required formatting gate and
uploads coverage from the wrong directory.

## Decision

Extend the existing release test with an artifact-input mode. Reuse the same
modular installation, registration, generation and cleanup assertions instead of
maintaining another test suite. Artifact mode stages copies under a unique test
directory, consumes exact existing packages, and never rebuilds the product.
Building an isolated SDK consumer and installing a local tool are tests, not
rebuilding the shipped product. Supplied artifacts and sample inputs must remain
unchanged. Full uses its bundled packages; Core requires an explicit external
fixture feed and does not claim to include a usable theme out of the box.
Standalone tests its native host and supplied native dependencies without plugin
management. Tests must fail on wrong versions, missing assets and command errors.

Release jobs run these checks on copies extracted from their actual archives
before attestation/signing/publication. CI uses the same modular suite and retains
its native-host checks. The existing local build mode remains convenient, but
uses release-shaped inputs and no additional loose theme assembly.

Automatic website deployment selects the source commit and release belonging to
the successful tag-triggered Release run, not independently selected latest
values. An ambiguous or missing matching release fails closed. Manual website
deployment retains current content with the latest available release. Manual
Release runs still do not deploy or create a release; signing/attestation remain
explicit external effects. NuGet publishing stays disabled.

## Alternatives and Limits

Simply adding another build-and-test invocation to Release could test different
bytes. Duplicating the local suite would recreate drift. Replacing the generator
or changing plugin/theme APIs is unnecessary for this pipeline correction.
Hosted runners, signing, Pages permissions and remote event payloads require a
separately authorized workflow run; local fixtures cannot prove those integrations.
No automatic commit, tag, push, deployment or workflow execution is authorized.

## Verification

Local Windows Core/Full ZIP and Linux Standalone TAR tests passed, including
input hashes, SDK/tool package identity, R1 registration and real generation.
The Windows Build-mode gate passed with 1,270 tests (1,261 passed, nine
platform skips), followed by format verification. Thirty-three offline workflow
contract tests passed. Actionlint found only the pre-existing deliberate
`if: false` for disabled NuGet publishing. Independent review repairs included
variant-specific version checks, Unix execute permissions, strict cleanup,
Core restore fixture provenance and attempt-specific release metadata names.

See the [pipeline remediation record](../reviews/remediation-pipeline-2026-09-14.md)
for failed intermediate checks, exact evidence, model trial notes and remaining
platform/hosted verification limits. An implemented workflow is not proof of a
successful remote release or deployment.
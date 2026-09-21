---
name: review-code
description: Reviews C# code against Revela project conventions, .editorconfig rules, and .NET 10 best practices. Covers naming, patterns, async, logging, DI, configuration, commands, testing, and code style. Use when reviewing code, suggesting improvements, or checking for convention violations in the Revela codebase.
argument-hint: "[file-path or scope]"
---

# Code Review — Revela Project

Review reachable behavior and regressions first. Code style follows the existing
authoritative rules; newer syntax alone is not a reason to expand the review.

## Conventions

Load [.editorconfig](../../../.editorconfig) and only the relevant scoped guidance:

- [C# conventions](../../instructions/csharp.instructions.md): naming, async, logging, culture and trim-safe configuration.
- [Plugin conventions](../../instructions/plugins.instructions.md): lifecycle, command registration, typed HTTP clients and diagnostics.
- [Test conventions](../../instructions/tests.instructions.md): MSTest, fixtures, failure assertions and platform differences.
- [Theme conventions](../../instructions/themes.instructions.md): manifests, escaping, URL helpers and browser checks.

## Review Procedure

1. Establish the requested scope, actual working tree and applicable baseline.
   Include new files; do not trust old reviews or test counts as fresh evidence.
2. Follow each changed behavior to its owning code and immediate consumers.
   Check configuration readers/writers, error exit codes and cancellation paths.
3. Check resources and trust boundaries: path containment, original-file
   preservation, artifact ownership, response-body deadlines and credential logs.
   Distinguish trusted author HTML from untrusted data; see the
   [security model](../../../docs/security-model.md).
4. Inspect a discriminating regression test. Framework wiring or a mock that
   bypasses the failing host contract does not establish product behavior.
5. Verify important suspicions with the cheapest authorized check. Do not run
   deployments, fetch private feeds or mutate global state to validate a review.
6. Compare documentation, package consumers and platform assumptions with the
   implementation. A passing managed build alone does not prove AOT, packaging
   or browser behavior.

Remain read-only unless fixes are requested. Prefer root-cause corrections to
suppression proposals; distinguish optional refactoring from actual defects.

## Documentation Consistency

- **README matches code** — verify plugin/theme README documents only features that actually exist in code
- **Website docs match code** — product docs live in `samples/revela-website/source/01 docs/`; check those pages against the code for outdated info
- **CLI options documented** — all `--option` flags in README must exist in the command definition
- **Config examples valid** — JSON examples in docs must match actual config models (property names, types, defaults)
- **Sample projects current** — samples should work with current codebase without errors

## Output Format

For each issue found, report:
- **File + location** (method/property name)
- **Trigger and impact**, with a concrete source or reproduction reference
- **Evidence status**: observed failure, source-confirmed defect or unverified risk
- **Suggested fix** at the owning boundary and the missing regression check

Lead with prioritized findings. State when none were found and identify untested
platforms or unavailable checks. A green build or delegated report is not proof
of correctness outside its checked scope.

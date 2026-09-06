# Import and Restore Hardening

- Status: Implemented
- Date: 2026-09-06
- Scope: Source.Calendar, restore diagnostics, and local release-test tooling

## Context and Goal

The user requested a bounded hardening pass before a future prerelease, without
creating a release or modifying the external rental site. This extends the
[calendar/theme failure boundaries](0002-calendar-and-theme-boundaries.md): source
transport must protect local files and credentials, while restore must handle
invalid local themes without treating them as missing packages.

## Decision and Rationale

- Keep transport and calendar interpretation in separate plugins. Fetch streams a
  complete response to a same-directory temporary file, then replaces the previous
  file. The client deadline covers headers, redirects and body. Cleanup failure
  must not replace caller cancellation or the original error.
- Disable automatic redirects and cookies; validate every redirect and prohibit
  HTTPS downgrade. Retain the existing literal-host checks without claiming DNS
  sandboxing. Remove both application full-URL logs and default factory request
  loggers because path segments, not just query strings, can contain credentials.
- Validate all selected output paths before downloading: remain under the configured
  source root, avoid linked/invalid components and duplicate destinations. Recheck
  before each request. The root is trusted and checks are not a race-proof sandbox.
- Any selected feed failure returns failure; successful sibling downloads remain
  committed. Multi-file transactions and remote data freshness are outside scope.
- Restore continues collecting invalid/missing/installed dependency diagnostics.
  An invalid local theme blocks all installation for that invocation, including
  normal restore. Reinstallation cannot repair a higher-priority corrupt local
  manifest; invalid is not missing. This belongs to the host-owned Packages
  command, not a plugin or a broader catch in the CLI bootstrap.
- Release-tool tests use an isolated `--tool-path`, explicit executable and exact
  expected version. They must not update or remove a user's global installation.
  No version bump, tag, push or publication is part of this pass.

## Alternatives and Trade-offs

Direct file writes can destroy the last good feed on an interrupted response.
HTTP status alone is not a guarantee of successful body transfer. Full exception
logging is convenient but can reveal feed tokens. Blanket catches hide cancellation
and programming errors; only expected failures are translated at owning boundaries.
Global tool install/update followed by uninstall is not an acceptable test of a
local prerelease package on a developer's machine.

## Verification

Focused tests use mock HTTP handlers, real isolated filesystem paths and a real
directory junction/symlink. Production DI tests inspect handler policy and record
Trace-level logs for synthetic path/query credentials. Cases include redirects,
body errors/timeouts, caller cancellation, cleanup failure, output traversal,
collisions and partial-feed failure. Restore tests verify continued inspection,
exit codes and no package-manager/source activity for invalid configuration.

Full .NET build/tests/format, dependency advisory checks and isolated local release
pipeline verification are parent-owned. Configuration or a first passing unit test
is not a release certification.

### Observed Results (2026-09-06)

- Debug build and all 1,010 tests passed, with zero failures/skips. Release build
  and a separate full Release test run also passed all 1,010 tests. Final
  `dotnet format --verify-no-changes --no-restore` and `git diff --check` passed.
- Source.Calendar has 34 focused cases, including actual caller cancellation,
  body-deadline expiry, invalid/missing redirect locations, HTTP factory logging,
  real directory-link containment, duplicate and ancestor/descendant destinations,
  unusable existing parents/leaves, and partial-feed failures. The Windows-specific
  read-only temporary-file case proves cleanup failure does not mask cancellation;
  it is explicitly inconclusive on other OSes, whose deletion semantics differ.
- Seven restore command cases verify invalid/missing/installed classification,
  continued sibling inspection, escaped diagnostics and no package-manager activity
  when invalid themes are present. No private feeds or real booking data were used.
- NuGet's direct/transitive vulnerability query reported no known vulnerable
  packages across the solution at check time. This is an advisory check, not proof
  that dependencies contain no vulnerabilities.
- The final `scripts/test-release.ps1 -SkipTests -KeepArtifacts` run passed for
  `win-x64` with the local version `0.0.0-test`. It published a self-contained CLI,
  packed 13 packages, installed/removed/reinstalled plugins, exercised theme/config/
  restore commands, generated the Showcase, compressed output, cleaned and rebuilt,
  checked incremental generation, and installed/verified/uninstalled the .NET tool
  in an isolated directory. Unit tests were skipped inside this script because
  they were separately executed in both configurations, not omitted from acceptance.
- Successful pipeline artifacts are in `artifacts/release-test-20260906-205418/`.
  The final pipeline reported 1 minute 43 seconds. This measures only that pipeline
  invocation, not total preparation, investigation, review or repair effort.

### Review and Trial Evidence

- Independent review found default HttpClient logger exposure, cleanup exception
  masking, unusable destinations and selected-output ancestor conflicts. Parent
  fixes added non-vacuous regressions; the final source review reported these
  findings resolved. Release cleanup now also preserves its primary failure.
- The first release invocation failed because local publish used `DebugType=none`
  while no-build pack expected PDBs. The script now mirrors the existing release
  workflow's embedded symbols and symbol-free pack. The second invocation reached
  plugin verification but had a stale count of eight; it now checks the five
  explicitly installed external plugins while retaining individual assembly checks
  for them and the two theme extensions. The third full invocation passed. Neither
  failure was relabeled as a successful pipeline.
- The Restore Worker remained configured for MAI Code 1.1; runtime model identity
  was not independently exposed. It edited only RestoreCommand and its new tests.
  The first check could not compile because the parent's assigned test project
  lacked the Packages reference. The Worker returned that scope gap without changing
  project files. Parent added the local ProjectReference, removed an unused import,
  corrected auto-created NSubstitute interface returns to explicit null and one
  expected display name, then all seven cases passed. These are parent integration
  repairs, not Worker first-pass success. A final newline was fixed by scoped format.
- The final Git check found the generic NuGet `Packages` ignore rule also hid the
  new restore test directory. An exact `tests/Commands/Packages/` exception was
  added alongside the existing feature-project exception; `git status` now reports
  the regression file. Passing local tests alone had not established that the new
  tests would be included in a commit or CI checkout.
- Parent import tests also needed options-container setup instead of proxying an
  internal config type, plus analyzer fixes. Interrupted build/test invocations
  were rerun serially and not counted as passes. No model/cost-efficiency claim is
  made; assignment, review and repair effort was not timed.

### Remaining Prerelease Work

- This pass validates Windows x64 packaging, not Linux/macOS execution or an AOT
  release. Run the target-platform pipeline before declaring those downloads ready.
- Authenticated OneDrive/provider integration was deliberately excluded. Literal
  URL safety is not DNS isolation; streaming has a time limit, not an explicit byte
  quota. Trusted configuration and filesystem ownership remain assumptions.
- `config image` and `theme files` can still surface a raw bootstrap error for a
  corrupt selected theme. The scout found no destructive ordering in those paths;
  friendly diagnostics there are a follow-up, not a claimed fix in this pass.
- Previously recorded real Safari/iPhone/assistive-technology gaps and light-mode
  viewer contrast remain separate UX work. No external site or its deployment was
  changed in this round.
- Select the actual prerelease version, review release notes, and obtain explicit
  approval for commit/tag/push/publication separately. Nothing in this test run
  created a release, changed Git history, or updated global .NET tools.

## Revisit When

Untrusted authors or concurrently hostile processes are introduced; DNS-level
isolation or explicit resource quotas are required; valid providers need a different
redirect policy; or a future restore recovery workflow intentionally overrides a
broken local theme. Changes to these boundaries require an explicit decision.
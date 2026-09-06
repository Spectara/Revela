# Calendar and Theme Failure Boundaries

- Status: Implemented
- Date: 2026-09-06
- Scope: Revela Calendar plugin, theme resolution, and template URL context

## Context and Goal

A non-photography site exercised existing plugin and theme contracts without a
Core fork. It exposed misleading diagnostics and insufficient acceptance tests.
The user approved applying these general lessons to Revela; the external site
and its deployment remain out of scope.

## Decision and Rationale

- Calendar generation rejects invalid/missing booking data rather than treating
  it as an empty calendar. Valid empty calendars remain valid. The failing page's
  previous JSON remains untouched; cross-page transactions and provider freshness
  guarantees are not introduced. Unsupported recurrence and timed events fail
  explicitly rather than creating incomplete availability.
- Only themes declaring `page` support require a Photo template. The renderer
  must not probe optional templates just to warn about their absence.
- A selected local manifest that exists but fails loading is an error, not an
  absent theme. Do not silently substitute an installed namesake. Missing local
  manifests still allow normal fallback; discovery can still skip broken themes.
  The shared registry owns this distinction; a plugin-only fix cannot repair it.
- Keep theme asset objects and site shorthand separate, document both, and show
  manifest details in check diagnostics. No configuration schema expansion is needed.
- Add `absolute_variant_url` to the existing template engine, not a new SDK service
  or project setting. `absolute_url(image)` must keep its photo-page meaning. The
  existing helper cannot produce an absolute image-variant address; URI resolution
  handles default relative assets, deployment prefixes, and CDNs. Full page context
  is required; content-image partials continue using `variant_url`.
- Escape dynamic Lumina text/attributes while preserving explicitly HTML-valued
  body/copyright output. Browser acceptance verifies the actual JS/media/viewport
  settings and photo identity, not just requested options or a nonblank page.

## Alternatives and Trade-offs

Globally silencing resolver warnings hides broken required templates. Copying a
placeholder Photo template or adding a rental-specific branch addresses symptoms.
Treating all malformed calendars as empty is unsafe; rejecting valid empty ones
would misrepresent legitimate availability. A full calendaring engine, feed
freshness policy, and booking/payment workflow are separate projects.

## Verification

Focused parser/generation tests, theme warning/diagnostic integration tests, URI
helper tests and real Lumina escaping output tests precede the full .NET gate.
Fresh Showcase generation and standalone browser checks cover page/lightbox/none
and no-JS interaction. Model/helper changes are not considered verified solely
because a Worker reports success.

### Integration Results (2026-09-06)

- Full solution build, 972 tests (zero failures/skips), and
  `dotnet format --verify-no-changes --no-restore` passed. Calendar has 70 focused
  tests; URL helper tests cover gallery/photo contexts, deployment prefixes,
  local-relative/root-relative assets and absolute CDNs. Malformed-theme tests
  exercise both default and explicitly selected viewers.
- Fresh isolated generation produced Showcase (22 pages, 14 images, 228 variants)
  and the documentation site (29 pages, 10 images, 309 variants). The function
  reference was checked at 390px and 1440px, without application JavaScript or
  horizontal overflow. Existing site projects and their outputs were not modified.
- [Standalone browser acceptance](../../scripts/browser/README.md) passed four
  JS/media/viewport combinations in each of Edge 152.0.4191.53, Firefox 155.0 and
  WebKit 26.6. Checks observe settings, selected image resources, all six Canon
  dialog triggers, native close/Escape, JS next-dialog focus restoration, static
  Sony occurrences, canonical photo identity and exact return occurrence.
- The stronger browser check found a real Firefox focus race: the old dialog's
  delayed `close` event could clear the original opener after a switch. Lumina now
  tracks closing dialogs individually until their event arrives; all three engines
  passed with freshly generated assets. No JavaScript was added to the no-JS path.
- Independent technical review found malformed-boundary variants, a ConfigCheck
  exception bypass, ignored temporal properties, check/parser disagreement on
  folding, and insufficient browser identity assertions. Parent repairs added
  regressions and all findings were re-reviewed as resolved. Passing initial tests
  was not sufficient evidence on its own.
- UX screenshot review found no confirmed regression or missing images. It noted
  faint close/next controls on light backgrounds as a low-severity follow-up, not
  a newly demonstrated regression or measured contrast failure. Real Safari/iOS,
  touch/zoom, screen readers, all contexts and every URI combination remain outside
  this bounded automated check. WebKit on Windows is not an Apple-device test.

### Delegation and Repair Evidence

- Both workers remained configured for MAI Code 1.1; runtime identity was not
  independently exposed. The Photo-template worker owned RenderService and its
  integration tests. Its first check failed due to an unavailable test dependency;
  a second check found an unused import. It then passed four focused cases. These
  were Worker implementation errors, not parent repairs or first-pass success.
- The Calendar-generation worker owned its command and new integration tests.
  Its first implementation check passed 51 existing tests, then its added tests
  passed 63. No Worker corrective retries were reported. Parent later fixed the
  new test file's final newline and JSON literal style during format verification.
- Parent owned parser, registry/check translation, URL helpers, escaping, docs,
  browser tooling, integration, and the delayed-close repair. Review-found defects
  in those slices are parent defects. A solution-wide filtered test invocation
  produced unrelated zero-test errors and was replaced with project-scoped runs.
- A leftover parent-started preview locked build DLLs; stopping it resolved the
  build failure. Subsequent previews used a separate runtime copy. One final build
  was interrupted and was rerun successfully; interrupted checks were not counted
  as passes. No private feeds were fetched, no Git history was changed.
- No timing, cost or token comparison was measured. Preparation, repeated checks,
  external review, tooling diagnosis and repairs are part of the parent effort;
  the trial does not establish general MAI reliability or efficiency.

## Revisit When

Required booking formats exceed individual all-day events; local theme fallback
needs an explicitly authorized recovery mode; template contexts change; or browser
tests reveal that native behavior no longer satisfies the agreed viewer contract.
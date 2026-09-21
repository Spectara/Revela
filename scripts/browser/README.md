# Lumina Browser Acceptance Requirements

Status: the standalone Node/Playwright test script was retired on 2026-09-15.
There is currently **no executable automated browser suite** in this directory
or in CI. This document preserves its acceptance requirements for a separate
decision about future browser tests; it does not introduce replacement tooling.
See the [Development Guide](../../docs/development.md#building-releases) for current verification requirements.

## Retained Visitor Journeys

- Lightbox: each trigger opens a single modal containing the selected thumbnail's
  actual image. Images must finish loading with nonzero natural dimensions.
- Close by button and Escape, returning keyboard focus to the initiating trigger.
  Switching to another image must still return focus to the original trigger.
- With JavaScript disabled, supported native open/close behavior remains usable;
  JavaScript-only previous/next controls are hidden rather than left inoperative.
- The `none` viewer has images but no photo links, lightbox buttons or dialogs.
- The `page` viewer navigates through the visible thumbnail to the expected photo,
  preserves the canonical path and uses an image variant for Open Graph metadata.
- Returning from a photo page selects the original gallery occurrence, whose
  fragment target exists. Repeated memberships must not create duplicate IDs.
- Required pages and assets load successfully, controls remain visible, and
  generated layouts have no unintended horizontal overflow.

The previous Showcase fixtures were `galleries/canon-only/` (lightbox),
`galleries/sony-only/` (none), and `galleries/landscapes/` (page). Future tests
should express these scenarios with small deterministic fixtures rather than
requiring those particular sample names.

## Retained Environments

| Viewport    | Color scheme | JavaScript | Reduced motion |
| ----------- | ------------ | ---------- | -------------- |
| 1440 x 1000 | Light        | Enabled    | Disabled       |
| 1440 x 1000 | Dark         | Disabled   | Enabled        |
| 390 x 844   | Light        | Disabled   | Enabled        |
| 390 x 844   | Dark         | Enabled    | Disabled       |

These are the former four combinations, not full combinatorial coverage.
Observe the actual viewport, media settings and JavaScript execution rather than
only requested options. Record the engine/version and conditions actually tested.

## Isolation And Evidence

Use a fresh local generation with owned source/output directories and isolated
browser pages. Do not modify another preview, fetch private feeds or submit remote
forms. Block external requests during visitor checks. Dispose of test contexts
and restore interception/state after checks. Screenshots support visual inspection,
not automatic visual-regression claims without a baseline comparison.

Historical screenshots and results remain historical; they do not establish
current browser correctness. Existing .NET HTML/URL tests do not execute browser
behavior. Until replacement automation is explicitly agreed, theme changes need
manual or available isolated browser checks, with missing checks reported as gaps.
Real touch/zoom, assistive technology and shipping Safari on Apple devices were
not comprehensively covered by the retired script.
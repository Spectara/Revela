# Reasoned Delegation to MAI Workers

- Status: Implemented
- Date: 2026-09-06
- Scope: Revela agent orchestration and explicitly assigned external repositories

## Context and Goal

Test whether a parent agent can explain and decompose complex work well enough for MAI Code 1.1 to implement bounded assignments reliably. The senior/junior analogy means sharing goals, reasons, and responsibility, not requiring obedience or assuming that a subagent learns permanently between calls.

The user approved retaining MAI Code 1.1, reasoned assignments, reusable decision notes, and independent verification. Site-specific product decisions are outside this record.

## Decision and Rationale

- Keep the existing Scout and Worker roles pinned to MAI Code 1.1. Model choice is a controlled condition of the experiment.
- The parent owns product decisions, architecture, decomposition, integration, reusable decision records, and final verification. The Worker owns a coherent bounded change and reversible local implementation details.
- Explain why the approach was chosen and why protected behavior must remain. Workers must report evidence that contradicts an assumption rather than silently expanding scope or following a known-bad instruction.
- Record consequential decisions in the owning repository and link them in assignments. Records are reusable context, not authority over current instructions or evidence.
- Define acceptance before execution, reserve shared mutable resources, and independently inspect results and acceptance gaps. Record parent intervention and model substitution honestly.
- Use the [assignment contract and verification workflow](../subagent-patterns.md#assignment-contract); avoid duplicating the full procedure here.

## Alternatives and Trade-offs

- Unpinning every agent could improve an individual result but would no longer test the intended MAI workflow.
- Prescribing every line would reduce implementation freedom while moving nearly all implementation effort into the parent. Prefer a bounded outcome with reasons and testable criteria.
- Unconstrained autonomous Workers would obscure whether decomposition or execution caused failures and increase integration risk.
- Documenting every detail would create maintenance overhead. Record decisions likely to need explanation later; keep trial notes brief.

## Verification

Configuration checks must confirm unchanged MAI model pins, consistent parent/Worker assignment requirements, reachable decision links, task-appropriate gates, and reviewer access to the required tools.

For each real assignment, record first-check results, retries, clarifications, parent repairs, independent findings, and total effort including preparation and review. Classify assignment gaps separately from execution errors and inadequate checks. No general reliability or cost claim follows from configuration validation or a single passing assignment.

Configuration changes are in place. Final integrated checks passed: five YAML frontmatters parsed with js-yaml, unchanged MAI model pins, UX routing/tool declarations, task-scoped handoff/baseline checks, 39 local file links, and 15 heading anchors. Editor diagnostics reported no errors in the nine affected files, and `git diff --check` passed. Independent review found one contradictory Reviewer handoff that still demanded .NET gates for every fix; the parent corrected it and clarified the UX default-path rule. These are documentation/configuration checks; no .NET build, application tests, or site generation were needed or claimed.

The bounded runtime acceptance is now verified through the isolated-page procedure below. The original editing session rejected UX Advocate through the active parent's allowed-agent list. After the user reselected Dev, dispatch succeeded: Dev-to-UX routing is observed, not just declared. The parent and UX agent subsequently each created, read, and closed an owned test page through an existing shared connection. Implemented refers to this workflow configuration and capability check, not general MAI reliability or completed site UX coverage. Fresh tool-returned page-ID lookup remains a limitation.

### Runtime Follow-up After Agent Reselection

- The first routed UX call stopped because the parent requested tool loading without distinguishing deferred tools from already expanded callable schemas. The parent clarified that only deferred tools require loading; no loading requirement was bypassed.
- The next UX call observed directly callable `open_browser_page` and `run_playwright_code` schemas. It opened one owned `about:blank` page, but the subsequent read/close call returned `Page not found` for the ID just returned by the open tool.
- Parent diagnosis reproduced the page-lookup failure for that ID and for a separate fresh local HTTP preview. Both Playwright and `read_page` failed to look up the fresh HTTP page. No existing shared pages were navigated or modified.
- Cleanup was attempted but not confirmed because page lookup failed. The returned IDs were `520a4a7f-755c-48fb-8bf1-37004081d7ab` (blank UX page) and `2351bdbe-98ce-48c5-908e-da70b9b02a5b` (parent's local preview). Do not describe these pages as successfully closed.
- Classification: the initial loading ambiguity was a parent assignment issue; the reproducible page lookup error is an unresolved runtime/tooling limitation, not evidence of a MAI implementation defect. No additional model overrides or source changes were made to work around it.
- At this stage, acceptance remained open: the UX agent still needed to open one owned page, observe its URL/title, and confirm cleanup. The later procedure below meets that behavioral check without claiming to repair page-ID lookup.

### Runtime Check After Window Reload

- Reloading VS Code did not fix lookup of fresh `open_browser_page` IDs. A new blank page returned ID `0eba8a90-a4d5-44b5-82a8-1fe08b470402`, but Playwright and `read_page` returned `Page not found`. Cleanup of this tool-opened page is unconfirmed, like the earlier failed probes.
- A read-only check of an existing shared page succeeded through both `read_page` and Playwright. Its URL and title were observed without navigation, interaction, or injected state, narrowing the failure to the fresh IDs tested rather than all browser access.
- Working procedure: use the existing page only as the Playwright connection anchor; create an owned `testPage` with `page.context().newPage()`, inspect that test page within the same tool call, and close only it in `finally`. Never navigate, inject state into, or close the shared anchor or its context.
- The parent and then UX Advocate independently returned `{ "url": "about:blank", "title": "", "closed": true }`. Each test page was closed successfully. Existing shared pages were unchanged.
- This verifies Dev-to-UX dispatch and actual browser create/read/close capability through a known shared anchor. It does not verify independent page-ID registration, isolated no-JS contexts, viewport/color/motion coverage, screenshots, or site interaction. Those remain task-specific acceptance checks. Panorama remains unchanged.
- No agent permissions or model pins were changed to obtain this result. The retained connection is an execution detail consistent with ownership and cleanup requirements, not permission to use the user's active page as the test surface.

### Initial Bounded Trial: Baseline Instruction Alignment

- Assignment: align only `AGENTS.md` and `.github/copilot-instructions.md` with the accepted rationale and task-scoped gates. The parent supplied goals/reasons, protected behavior, exact allowed files/resources, and a preselected structural check; the Worker chose wording and placement.
- Model: configured MAI Code 1.1; observed runtime identity unknown. No override was requested or substitution observed.
- First check: unverified terminal output, not a pass. One identical execution retry produced the expected PASS marker. No Worker repair edits or clarifications were reported.
- Worker-reported effort: eight file reads, one patch affecting two files, two terminal invocations. Parent effort additionally included decision/contract preparation, assignment preparation, structural checks, diff inspection, independent review, and correction of the parent-owned Reviewer handoff. Timing, token, and cost measurements were unavailable; no efficiency comparison is claimed.
- Integration: parent diff inspection and independent review found no defect requiring repair in the two Worker-owned files. The handoff correction was outside that assignment. The structural acceptance does not prove generated-site behavior or runtime tooling.
- Classification: terminal/tool-output uncertainty required one check retry; no assignment gap or Worker execution defect was demonstrated. A parent-owned instruction inconsistency was caught by independent review. UX runtime availability was a separate integration gap subsequently resolved for the bounded capability check above; fresh page-ID lookup remains limited.
- Outcome: useful evidence for a small documentation assignment only. Panorama implementation, browser UX, and general senior/MAI reliability have not been tested by this trial.

## Revisit When

- Preparation and repair repeatedly cost more than direct implementation.
- Passing focused checks repeatedly miss integration or usability defects.
- Workers need frequent out-of-scope decisions despite improved assignments.
- Model availability or behavior changes, requiring an explicitly recorded change to the experiment.
- Decision notes become stale or more costly to maintain than the context they preserve.
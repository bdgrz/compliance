# Accessibility and browser support

Decision: [M0-D24](decisions/m0-d24-accessibility-and-browsers.md), 2026-09-22.
This document defines the browser and accessibility acceptance target. It is
not a conformance report for a released client. The owning client stories and
their evidence record implementation progress and known limitations.

## Browser support statement

The supported target is the current and previous major versions of Chrome, Edge,
Firefox, and Safari on desktop. Other browsers, including mobile browsers,
may display the product but are not supported for workflows. They must show a
non-blocking banner naming the supported browsers.

## Accessibility target

Every browser workflow must meet WCAG 2.2 Level AA. The product owner
approves any exception. A known limitation is tracked as a GitHub issue with
the `accessibility` label, and it is listed in the in-product accessibility
statement until fixed.

The normative standard is the [W3C Web Content Accessibility Guidelines
(WCAG) 2.2 Recommendation](https://www.w3.org/TR/WCAG22/).

## Validation strategy

Frontend feature work follows TDD with focused unit tests in the owning client
repository. Unit tests are the current feature and PR test gate. The client
unit suite can render a page in jsdom for axe-core checks; browser and manual
accessibility checks are deferred until the later end-to-end validation phase,
after frontend features work independently.

### Automated

`ui/src/accessibility/axe.ts` runs axe-core with the
WCAG 2.2 AA rule tags (`wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`,
`wcag22aa`) against a rendered page, and runs the document-level rules
(`html-has-lang`, `html-lang-valid`, `document-title`, `meta-viewport`)
against `index.html`. Each new page or workflow adds a case to
`accessibility.test.tsx` that renders it through `@askrjs/askr/testing` and
asserts zero violations. A fixture test proves that the check reports an
injected violation. This runs as part of the client unit suite.

jsdom has no layout engine, so the `color-contrast` and `target-size` rules
are disabled in the automated unit check, and results axe reports as
incomplete are not failures. A later manual pass can cover these and per-route
document titles.

### Later manual review

The later end-to-end validation phase can record:

- keyboard-only operation of every action, with no traps and a logical focus
  order;
- visible, unobscured focus;
- 200% zoom and reflow at 320 CSS px without two-dimensional scrolling;
- contrast of any new text, icon, or component colors (4.5:1 text, 3:1 large
  text and UI components);
- target size of at least 24×24 CSS px;
- a descriptive document title for each route;
- `prefers-reduced-motion` honored for any motion;
- error messages identified in text, tied to their field, and announced;
- a screen-reader walkthrough with NVDA on Firefox or Chrome and VoiceOver
  on Safari.

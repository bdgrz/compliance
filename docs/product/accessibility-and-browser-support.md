# Accessibility and browser support

Decision: [M0-D24](decisions/m0-d24-accessibility-and-browsers.md), 2026-09-22.

## Browser support statement

Compliance supports the current and previous major versions of Chrome, Edge,
Firefox, and Safari on desktop. Other browsers, including mobile browsers,
may display the product but are not supported for workflows, and they show a
non-blocking banner naming the supported browsers.

## Accessibility target

Every browser workflow conforms to WCAG 2.2 Level AA. The product owner
approves any exception. A known limitation is tracked as a GitHub issue with
the `accessibility` label, and it is listed in the in-product accessibility
statement until fixed.

## Test strategy

Every frontend child needs both kinds of evidence before it is done.

### Automated

`src/Compliance.App/ClientApp/src/accessibility/axe.ts` runs axe-core with the
WCAG 2.2 AA rule tags (`wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`,
`wcag22aa`) against a rendered page. Each new page or workflow adds a case to
`accessibility.test.tsx` that renders it through `@askrjs/askr/testing` and
asserts zero violations. A fixture test keeps the check honest by proving
that it reports an injected violation. The suite runs in CI through
`npm run client:check`.

jsdom has no layout engine, so the color-contrast rule is disabled in the
automated run. Contrast is part of the manual pass.

### Manual

The frontend PR links a short record of:

- keyboard-only operation of every action, with no traps and a logical focus
  order;
- visible, unobscured focus;
- 200% zoom and reflow at 320 CSS px without two-dimensional scrolling;
- contrast of any new text, icon, or component colors (4.5:1 text, 3:1 large
  text and UI components);
- target size of at least 24×24 CSS px;
- `prefers-reduced-motion` honored for any motion;
- error messages identified in text, tied to their field, and announced;
- a screen-reader walkthrough with NVDA on Firefox or Chrome and VoiceOver
  on Safari.

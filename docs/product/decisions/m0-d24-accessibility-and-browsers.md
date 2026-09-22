# M0-D24: Accessibility target and supported-browser baseline

Status: accepted product decision, 2026-09-22. Decision owner: Jeff Repanich,
product owner. It closes [M0-D24 #136](https://github.com/bdgrz/compliance/issues/136).

| Question | Decision and rationale |
| --- | --- |
| Standard and conformance | **WCAG 2.2 Level AA** for every first-release browser workflow. It is the current W3C Recommendation and the common procurement bar. The product owner approves any exception. |
| Supported browsers | Current and previous major versions of Chrome, Edge, Firefox, and Safari on desktop. Mobile browsers can view the product but are not a supported workflow target. |
| Acceptance baseline | Keyboard: every action is operable, with no traps and a logical focus order. Focus: always visible and not obscured (WCAG 2.2 2.4.11). Contrast: 4.5:1 for text, 3:1 for large text and UI components. Zoom: usable at 200% and reflow at 320 CSS px without two-dimensional scrolling. Screen readers: names, roles, states, and landmarks are exposed. Reduced motion: honored through `prefers-reduced-motion`. Errors: identified in text, associated with their field, and announced through a live region. Targets: at least 24×24 CSS px (WCAG 2.2 2.5.8). |
| Automated evidence | The client test suite runs axe-core against rendered pages with the WCAG 2.2 AA rule tags. Zero violations is required, and it runs in CI through `npm run client:check`. jsdom has no layout, so color contrast is excluded from the automated run and checked manually. |
| Manual evidence | Each frontend child records a manual pass of keyboard-only operation, visible focus, 200% zoom and reflow, contrast of any new colors, and a screen-reader walkthrough with NVDA on Firefox or Chrome and VoiceOver on Safari. The PR links that record. |
| Unsupported browsers and known limitations | An unsupported browser sees a non-blocking banner naming the supported browsers. A known accessibility limitation is tracked as a GitHub issue with the `accessibility` label in the milestone of the affected story, and it is listed in the in-product accessibility statement until fixed. |

## Consequences

- The backlog contract and the frontend definition of done require this
  baseline (see [accessibility and browser support](../accessibility-and-browser-support.md)).
- Every scheduled frontend child keeps M0-D24 as a blocker, now resolved.
  Backend children omit it.

No remaining uncertainty requires a follow-up discovery issue.

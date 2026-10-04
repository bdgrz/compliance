# M0-D24: Accessibility target and supported-browser baseline

Status: accepted product decision, 2026-09-22. Validation timing updated
2026-10-03 by the product owner. Decision owner: Jeff Repanich,
product owner. It closes [M0-D24 #136](https://github.com/bdgrz/compliance/issues/136).
The normative standard is the [W3C Web Content Accessibility Guidelines
(WCAG) 2.2 Recommendation](https://www.w3.org/TR/WCAG22/).

| Question | Decision and rationale |
| --- | --- |
| Standard and conformance | **WCAG 2.2 Level AA** is the acceptance target for every first-release browser workflow. The product owner approves any exception. This decision defines the target rather than certifying implemented conformance. |
| Supported browsers | Current and previous major versions of Chrome, Edge, Firefox, and Safari on desktop. Mobile browsers can view the product but are not a supported workflow target. |
| Acceptance baseline | Keyboard: every action is operable, with no traps and a logical focus order. Focus: always visible and not obscured (WCAG 2.2 2.4.11). Contrast: 4.5:1 for text, 3:1 for large text and UI components. Zoom: usable at 200% and reflow at 320 CSS px without two-dimensional scrolling. Screen readers: names, roles, states, and landmarks are exposed. Reduced motion: honored through `prefers-reduced-motion`. Errors: identified in text, associated with their field, and announced through a live region. Targets: at least 24×24 CSS px (WCAG 2.2 2.5.8). |
| Current feature validation | Frontend feature work uses red-green TDD with focused unit tests. Component-level axe checks run in the client unit suite. Unit tests are the current feature and PR test gate. |
| Later validation | Manual browser and assistive-technology review in [accessibility and browser support](../accessibility-and-browser-support.md#later-manual-review) is deferred until the later end-to-end validation phase, after frontend features work independently. |
| Unsupported browsers and known limitations | An unsupported browser sees a non-blocking banner naming the supported browsers. A known accessibility limitation is tracked as a GitHub issue with the `accessibility` label in the milestone of the affected story, and it is listed in the in-product accessibility statement until fixed. |

## Consequences

- The backlog contract and frontend definition of done require the accessible
  behavior baseline and unit-level axe checks (see [accessibility and browser support](../accessibility-and-browser-support.md)); manual browser evidence is deferred.
- Every scheduled frontend child keeps M0-D24 as a blocker, now resolved.
  Backend children omit it.

No remaining uncertainty requires a follow-up discovery issue. The browser
banner and accessible workflow behavior are delivered by the affected frontend
stories. Manual browser and assistive-technology validation is reserved for
the later end-to-end phase; this decision does not claim those workflows are
already implemented.

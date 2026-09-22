# M0-D27: Scope of attest engagement support

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D27 #125](https://github.com/bdgrz/compliance/issues/125).

## Decision: collaboration only

The firm's attest team keeps its dedicated audit software for:

- planning;
- tests of controls;
- deviations;
- workpapers and workpaper review;
- report drafting.

The product-brief non-goal "do not replace auditor workpaper systems" stands.

The platform supports attest collaboration only:

- auditor requests and responses (T1-04, T3-03);
- frozen audit populations and sample selections (T3-02);
- evidence delivery.

Our attest team works in-product as `Attest`-role engagement assignees,
behind the M0-D26 wall. External firms receive packages (M0-D17).

## Separation from management's records

The platform holds no attest workpaper, test conclusion, or report draft.
Auditor-identified exceptions are recorded as `AuditTestException`
(M0-D23), preserving the auditor's wording as received. They are not a test
workpaper.

## Retention on offboarding

Attest documentation lives in the attest team's audit software, under that
team's own retention obligations.

The client tenant holds only the collaboration records: requests, responses,
delivered evidence, and packages. Those follow M0-D16 retention and engagement
holds. No firm-level store is created (M0-D25).

Our attest team works in-product, so it has no delivered package by default.
To keep the record it relied on, closing an attest engagement, or offboarding
a client with an attest engagement, requires:

1. **Generate a record package.** An engagement record package in the M0-D17
   package format: every request, response, population and sample version,
   and delivered evidence item for the engagement, with its manifest.
2. **Record delivery.** An attributed decision records that the package was
   delivered into the attest team's audit software.

The engagement hold (M0-D16) blocks disposition until that delivery decision
exists, so offboarding (F1-01) cannot complete without it.

## Consequences

- [T1-03 #26](https://github.com/bdgrz/compliance/issues/26) stays a P2
  hypothesis.
- T1-04, T3-02, and T3-03 deliver to the attest team in-product and to
  external firms through packages.
- [F1-01 #128](https://github.com/bdgrz/compliance/issues/128) and
  [#286](https://github.com/bdgrz/compliance/issues/286) retain no attest
  workpapers. They must:
  - generate the engagement record package and require its delivery decision
    before offboarding completes for any client with an attest engagement;
  - honor the engagement hold until that point.

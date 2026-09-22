# M0-D16: Evidence handling, retention, holds, and disclosure

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D16 #73](https://github.com/bdgrz/compliance/issues/73). Storage-level
hooks are defined in ADR 0006 (M0-A03). This record sets the policy values
those hooks enforce.

## Handling classes

Every artifact carries one `handling_class` from the M0-D08 classification
vocabulary: `public`, `internal`, `confidential`, or `restricted`.

**Never stored:** credentials, secrets, private keys, access tokens, and
session cookies. Uploads are scanned for these. A detected secret sends the
artifact to `quarantined` with reason `secret_detected`.

## Retention and holds

- **Default retention:** 7 years after the end of the period the evidence
  supports. For point-in-time evidence, the period end is its as-of date.
- **`EvidenceHold` kinds:**
  - `engagement`: applies until the report is issued plus 1 year;
  - `legal`: applies until an Org Admin releases it; it has no end date.
- **Disposition eligibility:** an artifact is eligible for disposition only
  when its retention has elapsed **and** it has no active hold.
- **Disposition decision:**
  - requires Org Admin approval, recorded as an EN-04 decision;
  - may be batched;
  - leaves a tombstone with the content hash, size, retention basis, and the
    decision.
- **Automation:** nothing is disposed of automatically.

## Redaction

A redaction creates a new derived artifact (a new content identity) linked to
its original. It never alters the original. Any Contributor may prepare a
redacted derivative; a Compliance Lead approves it for disclosure. The
original remains under its own retention and access rules.

## Inspection and quarantine

- **Scan on upload:** every upload is scanned for malware and secrets before
  it becomes `available`.
- **Artifact states:**
  - `pending_inspection`;
  - `available`;
  - `quarantined`, with reason `malware` or `secret_detected`;
  - `rejected`;
  - `disposed`.
- **Quarantine:** a quarantined artifact can't be downloaded or shared, except
  by an Org Admin who can release it (as a recorded decision) or dispose of it.

## Backup and recovery

Evidence content is in the whole-platform recovery scope accepted in M0-A01:
15-minute RPO and 4-hour RTO. Content identity is verified on restore.

## Auditor sharing

Auditors receive evidence through M0-D17 packages (external firms) or
engagement-scoped access (our attest team, M0-D27). Only `available`,
non-quarantined artifacts, or their approved redacted derivatives, can be
shared, and every disclosure is logged.

## Offboarding

When a client offboards (F1-01), active holds block disposition of held
artifacts. Everything else follows the retention schedule after the
`ClientExport` is delivered.

## Consequences

The following stories adopt these values:

- [R2-12 #55](https://github.com/bdgrz/compliance/issues/55) and its backend
  child [#285](https://github.com/bdgrz/compliance/issues/285);
- [F1-01 #128](https://github.com/bdgrz/compliance/issues/128) and its backend
  child [#286](https://github.com/bdgrz/compliance/issues/286).

# M0-D17: Audit-firm deliverables, formats, and auditor access

Status: accepted product default, 2026-09-22. Decision owner: Jeff Repanich
(product owner). Closes [M0-D17 #74](https://github.com/bdgrz/compliance/issues/74).

No audit firm is engaged yet. These are common-format defaults that the
product builds to. Confirming them with the actual firm is follow-up
[#339](https://github.com/bdgrz/compliance/issues/339), which blocks the Type I
package and outcome backends.

## Formats

| Output | Default |
| --- | --- |
| Control matrix | XLSX and CSV. One row per control version, with stable control identifier, criteria mappings, owner, and test status. |
| Evidence index | XLSX and CSV. One row per artifact version, with stable artifact identifier, content hash, linked control and request, and period. |
| System description | Structured sections per DC 200 headings. Exported as a document plus a section manifest. |
| Population | CSV with a stable row identifier per item. An exported population is one immutable version with its own content hash. Late or corrected rows produce a new population version (an M0-A02 amendment) that references the superseded version and each superseded row. The original version and its hash never change. |
| Sample selection | CSV that references population row identifiers and the exact population version's content hash. A later population version does not move an existing sample. Selecting from the amended population is a new, linked selection. |
| Package | A zipped folder with a `manifest.json` (every file's path, hash, size, and source record version) and deterministic naming: `{engagement}/{section}/{stable_id}_{version}.{ext}`. |

## Management assertion

The management assertion and representation letter are signed by the CEO or
the CISO. Each is recorded as an attributable EN-04 sign-off on an exact
package version. The sequence is:

1. the package is validated;
2. the assertion is signed;
3. the representation letter is signed;
4. the outcome is recorded.

Templates are organization-supplied documents; no product template is
asserted to match a firm's.

## Auditor access

- **Our firm's attest team (M0-D27):** works in-product as `Attest`-role
  engagement assignees, behind the M0-D26 independence wall.
- **External audit firms:** receive packages. No direct product accounts are
  created for them, so [T1-03 #26](https://github.com/bdgrz/compliance/issues/26)
  stays a P2 hypothesis.

## Consequences

The following Type I and Type II stories adopt these defaults:

- [T1-02 #25](https://github.com/bdgrz/compliance/issues/25), [T1-05 #28](https://github.com/bdgrz/compliance/issues/28), [T1-07 #30](https://github.com/bdgrz/compliance/issues/30);
- [T3-02 #41](https://github.com/bdgrz/compliance/issues/41), [T3-05 #44](https://github.com/bdgrz/compliance/issues/44), [T3-06 #45](https://github.com/bdgrz/compliance/issues/45);
- their backend children.

Firm-specific deltas are recorded in #339.

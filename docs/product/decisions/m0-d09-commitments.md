# M0-D09: Service commitments, system requirements, CUECs, and CSOCs

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.
The first client's source artifacts and draft lists are gathered in
[#354](https://github.com/bdgrz/compliance/issues/354).

| Question | Decision and rationale |
| --- | --- |
| Source artifacts | Commitments cite a source artifact: MSA, SLA, security addendum, privacy notice, policy, or other contract. The citation is a typed reference (artifact kind, title, version or date, section locator). Artifact files are governed artifacts (M0-A03), not pasted contract text. |
| First lists | The first lists are drafted by the Compliance Lead (with the readiness advisor for CUECs and CSOCs) in #354, using the existing commitment-draft register. |
| Minimum fields | These keep the existing four kinds: `service_commitment`, `system_requirement`, `user_entity_responsibility` (CUEC), and `subservice_responsibility` (CSOC). Each has an identifier, a statement, a service reference, a source citation, an owner (`Person`), applicable Trust Services categories, and a lifecycle. A CSOC also references the `Provider` that performs it (M0-D11). |
| Approval authority | A commitment is approved by a management approver: an Org Admin or Compliance Lead designated as the program's management approver. Self-approval of one's own draft follows the M0-D03 SoD exception rule. The approval is attributed and versioned. |

## Data-shape rules

- `Commitment`: `program_id`, `kind`, `identifier` (unique per tenant, program, and kind), `statement`, `client_service_id`, `source_citation` {`artifact_kind`, `title`, `version_or_date`, `locator`, `artifact_id?`}, `owner_person_id`, `categories[]`, and `status` (`draft | approved | retired`).
- `categories[]` is validated against the program's **current approved boundary version** when the commitment is created, revised, or approved. The approval records that `boundary_version_id`. When a later approved boundary version drops a category, affected commitments are flagged `category_out_of_scope` for review. They are never silently invalidated, deleted, or rewritten, and earlier approvals keep their original boundary version.
- A `subservice_responsibility` requires `provider_id`. A `user_entity_responsibility` never counts as an internally performed control.
- `CommitmentApproval`: `commitment_version_id`, `boundary_version_id`, `approver_member_id`, `approved_at`, and a `sod_exception_id` (nullable).
- `Commitment` 0..* ↔ 0..* `Control` via versioned `CommitmentControlLink` (owned by R1-13 and referenced from R1-05).

## Canonical model alignment

The `source_citation` shape, `categories[]`, and the `Provider` reference on
CSOCs are alignments requested on M0-D28.

## Follow-up discovery

[#354](https://github.com/bdgrz/compliance/issues/354) collects the artifacts,
drafts the lists, and names the approver. R1-13 does not wait on it.

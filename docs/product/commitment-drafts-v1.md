# R1-13 management-authored draft register

This is partial backend delivery for [#229](https://github.com/bdgrz/compliance/issues/229).
The source commitments and approval authority for the first engagement remain
an organization decision in [M0-D09 #66](https://github.com/bdgrz/compliance/issues/66).
The register cannot assert an approved commitment, a legal interpretation,
readiness, control operation, or an auditor conclusion.

## Contract

- `POST /api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts`
  creates one management-authored draft with a kind, identifier, active
  same-program `service_id`, statement, context, and `source_reference`. Kinds
  are `service_commitment`, `system_requirement`,
  `user_entity_responsibility`, and `subservice_responsibility`. The last two
  represent CUEC and CSOC drafts; they never count as internally performed
  controls. `source_reference` is a user-supplied locator, not imported
  contract text or verified source provenance.
- `PUT .../commitment-drafts/{draft_id}` revises statement, context, and
  source reference with `expected_revision`. Kind, identifier, program, and
  service do not change. Each accepted change retains its author and exact
  revision. A stale revision conflicts with the current revision.
- `GET .../commitment-drafts/{draft_id}`, collection `GET`, and
  `GET .../{draft_id}/revisions/{revision}` expose current and exact history.
  Current `GET` accepts `minimum_revision`; source or projection lag returns
  transient conflict. Collection `GET` checks its tenant event checkpoint
  before returning even an empty page. Limits are 1–200 and malformed or
  cross-program cursors return validation errors.
- Five flat, machine-appropriate MCP tools expose the same create, revise,
  get, list, and exact-revision operations through Portia authorization.
  Static HTTP path segments use kebab-case; interpolated path values, query
  keys, JSON properties, and tool arguments use snake_case.

All operations require active tenant membership and the existing
`program.manage` permission. This narrow interim grant restricts potentially
sensitive contract references while [M0-D03 #60](https://github.com/bdgrz/compliance/issues/60)
and scoped access [#186](https://github.com/bdgrz/compliance/issues/186)
remain open. Other tenant and program references are not disclosed.

The normalized identifier is unique within tenant, program, and kind and
selects the authoritative stream. A duplicate HTTP `POST` conflicts even if
its content matches. Portia replay of the same logical request ID and
original content returns the original registration. Events are capped at
48 KiB of serialized domain content to fit Fitz stream framing.
The service's active state is read before the draft stream is executed. The
aggregate callback checks that state only for a new draft, so a replay still
works after service retirement. Creating a service retirement and a draft in
different streams is not one atomic transaction; #229 still needs the
cross-record consistency policy before an approved commitment can depend on
that relationship.

A new or revised draft says `status: draft`, `source_resolution: unverified`,
`owner_resolution: unresolved`, and `applicability_resolution: unresolved`.

## Review, approval, and effective versions (#450, #466)

- `POST .../commitment-drafts/{draft_id}/reviews` records one independent,
  attributable decision on `expected_revision` with `outcome` `accept` or
  `request_changes` and a `rationale`. Review is HTTP-only; no MCP tool can
  record it. Acceptance verifies `owner_reference`, `applicability`
  (`applicable`/`not_applicable`), `interpretation` (`supported`/`unsupported`,
  with optional `interpretation_note`), and source provenance: the reviewer
  restates the exact current `source_reference` as `source_verified_reference`
  and records `source_evidence` describing what was checked. A mismatched or
  missing verification is rejected. An unsupported interpretation can be
  accepted but stays visible on the version. Accepting a revision that is
  already effective conflicts. An accepted review moves the draft to
  `status: reviewed` and `source_resolution: verified`; any later revision
  resets it to `draft`/`unverified` and voids the accepted review.
- `POST .../commitment-drafts/{draft_id}/approvals` is the separate approval.
  It names `expected_revision`, `accepted_review_decision_id` (the latest
  accepted review of that exact revision), `effective_from` (later than the
  prior version's), a `rationale`, and the `impact_digest` of a complete,
  current preview. It creates an immutable version and is HTTP-only.
- Separation of duties: authors of any revision since the last effective
  version cannot review or approve, and the accepted reviewer cannot approve
  the same revision, unless an approved waiver names the exact scope
  (`record_type: commitment`, record and version ID = draft ID, revision,
  action `review` or `approve`) and that member. Responsibilities can be
  assigned to the exact pending revision through the responsibility API with
  `record_type: commitment` and `version_id` = draft ID. When a revision has
  active `assigned_reviewer` or `policy_approver` assignments, only those
  assignees may review or approve it, and conflicting responsibilities need
  the same exact-scope waiver.
- `GET .../impact-preview?expected_revision=` compares the draft revision with
  the latest effective version and lists dependents: boundaries whose draft or
  approved scope entries reference the commitment, and controls whose approved
  or pending applicability names it (controls may now reference a recorded
  commitment with `subject_type: commitment`). Contexts that cannot reference
  commitments are listed in `unlinked_contexts` with a reason: control
  criterion mappings (they link criteria to controls, which are listed),
  evidence, readiness (commitments are an unassessed family), and risks.
- `GET .../versions`, `.../versions/{version}`,
  `.../effective-version?effective_on=`, and `.../decisions` expose history and
  as-of reads. Decisions carry `stage` (`review` or `approval`). Versions carry
  the accepted review as `decision`, the `approval`, `source_resolution`, and
  `source_evidence`. Versions created before #466 have no approval and an
  `unverified` source. Source/projection lag returns a transient conflict.
- Every version carries `performed_by` (`service_organization`,
  `user_entity`, or `subservice_organization`) and `internally_performed`.
  CUECs and CSOCs are never internally performed.
- Read-only MCP tools: `bdgrz.commitment.impact.preview`,
  `bdgrz.commitment.version.get`, `bdgrz.commitment.version.effective.get`,
  `bdgrz.commitment.version.list`, and `bdgrz.commitment.decision.list`.

The SPA route `/{slug}/programs/{program_id}/commitments` lists drafts by kind
and supports create, revise, history, review, approval with impact preview,
separation-of-duties explanations, and effective-version as-of reads (#230).

Still open: engagement snapshots, and evidence, readiness, and risk links to
commitments.

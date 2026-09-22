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

Every view says `status: draft`, `source_resolution: unverified`,
`owner_resolution: unresolved`, and `applicability_resolution: unresolved`.
Review, approval, effective history, conflict reconciliation, linkage to
other inventories, impact preview, and engagement snapshots remain in #229.

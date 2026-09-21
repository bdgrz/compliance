# R1-05 Control draft backend slice

This slice records organization-authored Control drafts in one tenant and program.
It is partial delivery under [#197](https://github.com/bdgrz/compliance/issues/197),
which remains open for governed applicability, owner assignment, approval,
activation, successors, impact preview, retirement, and deletion rules.

## Contract

- `POST /api/v1/tenants/{tenant_id}/programs/{program_id}/controls` creates a
  draft from an authored identifier, title, objective, description,
  implementation narrative, and expected-evidence descriptions. It returns
  the stable `control_id`, normalized identifier, and revision `1`.
- `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft`
  requires `expected_revision`; a stale revision returns conflict with the
  current revision. Neither operation approves or activates a control.
- `GET` of that draft, collection `GET`, and
  `GET .../draft/revisions/{revision}` expose the current draft and exact
  authored history. `minimum_revision` on current `GET` distinguishes a
  lagging projection with transient conflict. Current `GET` also checks the
  latest source revision, and collection `GET` checks the control-area
  projector checkpoint before returning even an empty page. Both return
  transient conflict while the worker is behind. Results and OpenAPI use
  snake_case. Read-only MCP tools expose these three queries.
  Collection `limit` must be 1–200; malformed or scope-mismatched cursors
  return validation errors.

All five HTTP operations require an active tenant membership with the existing
`program.manage` grant. This deliberately limits draft narratives to current
program administrators while [M0-D03 #60](https://github.com/bdgrz/compliance/issues/60)
and scoped access [#186](https://github.com/bdgrz/compliance/issues/186) remain
open. An owner or general tenant member has no draft read grant from this slice.

The normalized identifier is unique within a tenant and program. It selects
the authoritative Control stream, so a lagging list projection cannot permit
a duplicate. An exact retry of the original create request and content returns
the original registration only when Portia replays the same logical
`request_id`; a second HTTP `POST`, even with identical content, has a new
request ID and conflicts. A client-supplied idempotency key is not part of
this contract. A separate request using the identifier conflicts.
The response explicitly marks owner and applicability `unresolved`. Expected
evidence descriptions describe the planned evidence, not a collection or
effectiveness assertion. No risk coverage, criterion mapping, or source-template
authority is inferred.
Each serialized draft event is capped at 48 KiB to fit the Fitz stream frame
with room for its Portia envelope and metadata. Oversize narratives return
validation before any event is appended.

## Framework dependency

Portia [#61](https://github.com/cntryl/portia/issues/61) rejects nested object
arguments before an MCP request reaches authorization. The `content` contract
is intentionally nested and shared with HTTP, so create and revise tools are
not advertised until Portia supports that shape. No application binder or
alternate write path is added. Portia [#60](https://github.com/cntryl/portia/issues/60)
also tracks commit-time stream concurrency error translation; the domain
enforces expected revisions, while simultaneous append transport behavior
remains a framework dependency.

# R1-07 Risk draft backend slice

This slice records an organization-authored risk scenario for one tenant and
program. It is partial delivery under [#199](https://github.com/bdgrz/compliance/issues/199).
The risk remains `draft_unassessed`; it cannot contribute assessment, treatment,
coverage, readiness, or approval claims.

## Contract

- `POST /api/v1/tenants/{tenant_id}/programs/{program_id}/risks` creates a draft
  from a stable authored `identifier`, `title`, `scenario`, `potential_effect`,
  and optional `source_note`. It returns `risk_id`, normalized identifier, and
  revision `1`.
- `PUT .../risks/{risk_id}/draft` requires `expected_revision` and replaces the
  four narrative fields. A stale revision returns conflict. The identifier is
  immutable.
- `GET .../risks/{risk_id}/draft`, collection `GET`, and
  `GET .../draft/revisions/{revision}` return the current draft, a bounded
  program list, and exact authored history. `minimum_revision` on current GET
  returns transient conflict during source or projector lag. Collection GET
  checks the risk-area projector checkpoint before returning even an empty
  page. List `limit` is 1–200; malformed and cross-program cursors are rejected.
- All five operations also have MCP tools. Both write requests use flat scalar
  arguments so Portia can bind them without the nested-object limitation in
  [Portia #61](https://github.com/cntryl/portia/issues/61). MCP reads are marked
  read-only; revise is marked idempotent.

Every operation requires an active tenant membership with `program.manage`.
This narrow existing grant restricts risk narratives while [M0-D03 #60](https://github.com/bdgrz/compliance/issues/60)
and scoped access [#186](https://github.com/bdgrz/compliance/issues/186) remain
open. Tenant and program IDs are part of the authoritative aggregate identity
and projection keys. Cross-tenant or cross-program data is not disclosed.

The normalized ASCII identifier is unique per tenant and program through a
deterministic Risk stream ID. The original Portia request ID with identical
content can replay; a new create request for the same identifier conflicts.
Events retain the acting member, display, and change time. `source_note` is
the author's statement, not a verified source observation. Serialized domain
events are capped at 48 KiB to fit the Fitz event frame.

## Deferred decisions

[M0-D10 #67](https://github.com/bdgrz/compliance/issues/67) must choose the
assessment method, likelihood and impact scales, appetite, treatment vocabulary,
acceptance authority, and cadence. Later R1-07 work will bind exact boundary,
asset, provider, commitment, and control versions; record RiskAssessment,
RiskTreatment, RiskAcceptance, review and reassessment; and contribute explicit
gaps to readiness. A draft does not silently satisfy those requirements.

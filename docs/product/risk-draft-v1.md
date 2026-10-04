# R1-07 Risk draft backend slice

This slice records an organization-authored risk scenario for one tenant and
program. It is partial delivery under [#199](https://github.com/bdgrz/compliance/issues/199).
This authoring operation creates a `draft_unassessed` narrative; that narrative
alone cannot contribute assessment, treatment, coverage, readiness, or approval
claims. The separate [assessment and governance contract](risk-assessment-v1.md)
defines later governed outcomes. Manual authoring and governance require no
import or external integration.

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
  arguments. MCP reads are marked
  read-only; revise is marked idempotent.

Every operation requires an active tenant membership with `program.manage`.
This grant applies at the program or organization scope under the accepted
[M0-D03 decision](decisions/m0-d03-roles-and-separation-of-duties.md) and scoped
access [#186](https://github.com/bdgrz/compliance/issues/186). Tenant and program
IDs are part of the authoritative aggregate identity
and projection keys. Cross-tenant or cross-program data is not disclosed.

The normalized ASCII identifier is unique per tenant and program through a
deterministic Risk stream ID. The original Portia request ID with identical
content can replay; a new create request for the same identifier conflicts.
Events retain the acting member, display, and change time. `source_note` is
the author's statement, not a verified source observation. Serialized domain
events are capped at 48 KiB to fit the Fitz event frame.

## Assessment policy and remaining links

[M0-D10](decisions/m0-d10-risk-method.md) defines the accepted qualitative 5×5
method, acceptance authority, treatment vocabulary, and annual cadence.
The first client's appetite threshold and advisor confirmation belong to
[#355](https://github.com/bdgrz/compliance/issues/355). Risk assessments,
treatment, acceptance, Person ownership, reviewed control treatments, and
reassessment are described in [risk-assessment-v1.md](risk-assessment-v1.md).
Exact boundary, asset, provider, commitment, and control-version relationships
must be implemented by their owning R1-07 slices before they support a coverage
claim. Readiness rule definitions remain with R1-08. A draft does not silently
satisfy those requirements.

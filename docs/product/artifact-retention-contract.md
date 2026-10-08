# Canonical artifact retention foundation

This backend slice implements the policy and legal-hold authority subset of #285 using M0-D16 and ADR0006. Existing evidence artifacts and staged application import batches are supported sources. A tenant-scoped `artifact_retention` stream refers to an existing source kind, ID and canonical lowercase SHA-256 digest; it never stores a second content copy.

A current personal Org Admin can record one retention basis, place an indefinite legal hold, and release a specific hold with an attributed reason. Every decision records the verified source stream position, member identity, timestamp and next retention revision. Writes require the exact source digest and expected retention revision. Identical historical decision retries append nothing; changed actors, reasons or periods conflict. Replaying a placement after its release does not reactivate the hold. A new hold requires a new hold ID. Reasons are bounded to 2,000 characters. Supported-period amendments are unavailable in this slice.

Evidence basis dates come from the registered artifact's authoritative supported period. Optional supplied dates must match it exactly. Import raw rows have no authoritative supported-period fields: the Org Admin must explicitly supply both period dates and the reason for recording them. The staging date is never treated as the supported-period end. Point-in-time evidence uses the same start and end. The fixed `m0_d16_v1` policy retains content through the seventh anniversary of the supported-period end, inclusive; `retention_elapsed` becomes true on the following UTC calendar date. Seven-year additions beyond year 9999 are rejected.

The policy and hold streams persist independently of the existing immutable source streams. Source identity, original registration event scope, content digest, native evidence period and durable source position are verified before mutation. Content identity and supported evidence period are immutable in the owning source aggregate; concurrent source lifecycle events do not change these facts. Mutation OCC serializes competing retention decisions. Reads verify source and retention positions again before returning and reject overlapping changes. These checks provide a bounded per-request consistency contract, not a transaction or global snapshot across streams.

## Personal HTTP writes

- `POST /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/basis`
- `POST /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds`
- `POST /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases`

The supported `source_kind` values are `evidence_artifact` and `application_import`. Bodies use `expected_content_sha256`, `expected_revision` and `reason`; basis can include `period_start` and `period_end`, and hold placement includes `hold_id`. There are no MCP decision tools. Mutation handlers also require an explicit HTTP invocation and reject direct internal or MCP invocations; read handlers remain transport-independent.

## Admin-only reads and import consumer

- `GET /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}`
- `GET /api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds`
- `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/retention`

These reads are also read-only MCP tools. Assessment includes policy dates, active hold count, history count and bounded blocker codes. Hold history includes placement and release attribution and reasons. History pages default to 50, allow 1–200 records, and use source/tenant/revision-bound cursors. A concurrent policy/source revision makes the cursor stale. `minimum_revision` must be positive when supplied. An existing source without recorded policy returns revision zero with explicit missing-basis/period blockers.

All these operations use the current canonical Org Admin permission `tenant.rbac.manage`, active tenant and active client membership checks. System principals, firm staff and ineligible memberships are denied. The import retention consumer uses this same admin-only authority; Contributor-readable import progress and rejected-report responses do not expose hold existence or reasons.

## Explicit remaining gates

Every assessment returns `disposition_allowed: false`. A released legal hold or elapsed period does not authorize disposal. Engagement-report issued dates and engagement-hold release authority, historical reliance and disposition approval have no canonical implementation in this slice and remain blockers. Evidence assessments also report `storage_hold_enforcement_pending`; this slice does not call byte-store `SetLegalHoldAsync` or enforce these legal holds at the storage boundary. Import assessments report `inline_disposition_unsupported`: raw event rows remain in their source stream. No delete, automatic expiry, event stream truncation, or disposition decision is implemented. #285 and the broader #195 acceptance remain open until those capabilities are delivered.

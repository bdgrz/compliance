# R1-05 Control draft backend slice

This slice records organization-authored Control drafts in one tenant and program.
It is partial delivery under [#197](https://github.com/bdgrz/compliance/issues/197),
which remains open for verified owner assignment, activation, review and
approval, successors, complete impact preview, retirement, and full in-use deletion
rules.

## Contract

- `POST /api/v1/tenants/{tenant_id}/programs/{program_id}/controls` creates a
  draft from an authored identifier, title, objective, description,
  implementation narrative, and expected-evidence descriptions. It returns
  the stable `control_id`, normalized identifier, and revision `1`.
- `PUT /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft`
  requires `expected_revision`; a stale revision returns conflict with the
  current revision. Neither operation approves or activates a control.
- `POST /api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards`
  records a rationale and removes an unlinked current draft. It requires the
  exact `expected_revision`; it is a destructive MCP tool as well as an HTTP
  operation. The event stream retains the attributable discard tombstone, but
  current, exact-revision, and history reads become not found.
- `GET` of that draft, collection `GET`, and
  `GET .../draft/revisions/{revision}` expose the current draft and exact
  authored history. `minimum_revision` on current `GET` distinguishes a
  lagging projection with transient conflict. Current `GET` also checks the
  latest source revision, and collection `GET` checks the control-area
  projector checkpoint before returning even an empty page. Both return
  transient conflict while the worker is behind. Results and OpenAPI use
  snake_case. MCP tools expose create, idempotent revise, destructive discard,
  and these three read-only queries. Create and revise use the same nested
  `content` object as HTTP.
  Collection `limit` must be 1–200; malformed or scope-mismatched cursors
  return validation errors.

All control-draft HTTP operations and their MCP counterparts require an active tenant
membership with the existing `program.manage` grant. This deliberately limits
draft narratives to current program administrators while
[M0-D03 #60](https://github.com/bdgrz/compliance/issues/60) and scoped access
[#186](https://github.com/bdgrz/compliance/issues/186) remain open. An owner or
general tenant member has no draft read grant from this slice.

The normalized identifier is unique within a tenant and program. It selects
the authoritative Control stream, so a lagging list projection cannot permit
a duplicate. An exact retry of the original create request and content returns
the original registration only when Portia replays the same logical
`request_id`; a second HTTP `POST`, even with identical content, has a new
request ID and conflicts. A client-supplied idempotency key is not part of
this contract. A separate request using the identifier conflicts.

`content.owner_reference` is optional, opaque authored attribution. When it is
present, the response reports `owner_resolution: declared_unverified`; it is
not a Member, responsibility assignment, access grant, or verified identity.
`content.applicability` is an optional collection of stable `entry_id` values:

- `application` and `system_instance` entries require
  `unresolved: false` plus a non-empty `governed_record_id`. The handler
  validates the canonical ID in the same tenant before append.
- `risk` and `process` entries require `unresolved: true` and no
  `governed_record_id`, until their owning lifecycle can verify the record.

Missing or unresolved applicability reports `applicability_resolution:
unresolved`; a collection composed only of current governed application or
system-instance entries reports `declared`. Neither state activates a control
or asserts risk treatment, criterion coverage, ownership verification,
evidence collection, or operating effectiveness. A draft with a current
applicability entry cannot be discarded. Review has not landed yet, so this is
a pre-review discard rule; the eventual review lifecycle must add its own
historical-use guard.

The bounded Application change preview reports current direct-Application
applicability entries through `control_draft_references` after a separate
Control-source catch-up check. That observation is not an approved or effective
ControlVersion, activation, coverage conclusion, or complete lifecycle impact;
instance applicability and all approval or retirement consequences remain
explicitly pending in
[application-change-preview-v1.md](application-change-preview-v1.md).

Expected-evidence descriptions describe planned evidence, not a collection or
effectiveness assertion. No risk coverage, criterion mapping, or source-template
authority is inferred. Legacy draft events without `owner_reference` and
`applicability` still hydrate and project as `unresolved`.
Each serialized draft event is capped at 48 KiB to fit the Fitz stream frame
with room for its Portia envelope and metadata. Oversize narratives return
validation before any event is appended.

`ControlDraftDirectoryV2` is an independent current-read projection. It replays
the retained Control stream into `kv://bdgrz/control-draft-directory-v2/projection`
with its own checkpoint, so the discard interpretation never reuses the prior
current-directory checkpoint during a rolling deployment.
Because `ControlDraftDiscarded` is a new event discriminator, production starts
with `Compliance:Controls:DiscardEnabled=false`. Release the new API and
workers as readers first, drain every prior reader, then explicitly enable the
gate before allowing discard writes. Development enables the gate for local
and integration use. This is an event-reader compatibility guard, not a
backup, restore, or storage dependency. After any discard is written, a
rollback must retain a reader build that understands `ControlDraftDiscarded`.

## Framework dependency

Portia 0.5.5 resolves [#61](https://github.com/cntryl/portia/issues/61), so
the nested `content` object now binds before authorization without an
application binder or alternate write path. The integration test covers an
authorized nested create, revision, and discard plus denied write calls. The
aggregate continues to enforce expected revisions for stale edits.

## Initial activation (R1-05a, #197)

- Responsibilities for a control target `record_type=control`, the control ID, the
  `draft_version_id` shown on the draft view (the deterministic initial version ID), and the
  exact draft revision. They are stored on the `ControlDraft` stream, the same way boundary
  responsibilities live on the `SystemBoundary` stream, so an assignment, a review, an approval,
  and a draft revision share one optimistic concurrency boundary. A responsibility is never an
  access grant.
- `POST .../controls/{control_id}/draft/reviews` records an independent review
  (`accept` or `request_changes`) of the exact revision; a later review supersedes the prior one.
  `POST .../draft/approvals` activates the version from the latest accepted review of the
  unchanged revision. Both are HTTP-only and deny unwaived self-review/self-approval (draft author
  or a conflicting work responsibility such as `control_owner`) unless an approved exact-scope
  separation-of-duties waiver (`record_type=control`) is supplied.
- Activation revalidates governed applicability and requires an active client-personnel member
  holding `control_owner` on the exact revision, verified against the member source stream. An
  authored `owner_reference` stays unverified attribution.
- The approved `ControlVersion` is immutable and carries its effective start, exact content,
  verified owner, accepted review, and approver. Current, exact, effective-as-of, and paged
  version/decision reads are available over HTTP and read-only MCP; they are served from the
  control's own stream, so they never lag.
- `ControlReviewed` and `ControlApproved` are new discriminators. Production starts with
  `Compliance:Controls:ActivationEnabled=false`; enable it only after every reader understands
  them, and keep such a reader build for rollback.
- Successors, change impact, non-signing-in Person owners (#433), retirement, and complete
  deletion guards (#434) remain out of scope.

## Ownership, provenance, withdrawal, and full impact (#468, #477)

- Draft content may carry `provenance` with `origin` `organization_authored` (the default when
  absent), `template`, or `supplied`. A template or supplied origin names its `source_name` and
  may add `source_reference` and `source_version`. The approved version reports the origin as
  `content_origin`; provenance never attributes content to a platform actor who did not act.
- `PUT .../controls/{control_id}/draft/owner-person` designates (or, with a null `person_id`,
  clears) a recorded workforce person as owner of the exact pending draft revision. The person
  need not sign in; the signed-in recording actor stays separate. Activation re-reads the person
  from the workforce roster and, when no verified member `control_owner` exists, records
  `owner_resolution=verified_person` with `owner_person_id`. The person's correlated member, at
  designation and at approval, may not review or approve the revision without an exact-scope
  waiver.
- `POST .../controls/{control_id}/proposal-withdrawals` withdraws a pending successor draft or
  retirement proposal at its exact revision. The approved version stays current, its content is
  restored on a new revision so stale reviews and digests cannot apply, and a `withdrawal`
  decision is kept in the decision history.
- The impact preview now has authoritative contributors for `mappings` (active and pending
  criterion mappings naming the control), `risk_treatments` (proposed and accepted control
  treatment assertions), and `readiness` (whether the latest readiness assessment relied on a
  version of the control). `engagements`, `evidence`, and `work` stay `unlinked` because no
  record in those contexts can reference a control in this build. A context naming more than
  200 records reports `pending`, which blocks the decision.
- Person-owner designation and withdrawal emit new discriminators, so they share the
  `Compliance:Controls:LifecycleEnabled` readers-before-writers gate. Enabling it and
  `ActivationEnabled` in production remains a rollout decision.

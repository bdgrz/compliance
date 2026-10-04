# R1-05 Control authoring and lifecycle backend contract

Status: cumulative bounded backend contract under
[#197](https://github.com/bdgrz/compliance/issues/197),
[#468](https://github.com/bdgrz/compliance/issues/468), and
[#477](https://github.com/bdgrz/compliance/issues/477). Authorized users author,
assign owners, review, approve, revise through successors, and retire controls
in one organization and program. Each increment below states its limits;
engagement impact and stronger concurrent source fences remain separate work.
These workflows require no import, connector, or optional collection runtime.
Current delivery and acceptance evidence belong to the linked issues.

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
membership with `program.manage` at the applicable program or organization scope.
The accepted [M0-D03 decision](decisions/m0-d03-roles-and-separation-of-duties.md)
defines roles and duties; [#186](https://github.com/bdgrz/compliance/issues/186)
owns scoped-access delivery. An owner or
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

- `application`, `system_instance`, and `commitment` entries require
  `unresolved: false` plus a non-empty `governed_record_id`. The handler
  validates the canonical ID in the same tenant before append.
- `risk` and `process` entries require `unresolved: true` and no
  `governed_record_id`, until their owning lifecycle can verify the record.

Missing or unresolved applicability reports `applicability_resolution:
unresolved`; a collection composed only of validated governed Application,
SystemInstance, or Commitment entries reports `declared`. Neither state
activates a control
or asserts risk treatment, criterion coverage, ownership verification,
evidence collection, or operating effectiveness. Discard is limited to an
unapproved draft with no retained applicability, no prior review, and no
responsibility ever assigned, including revoked assignments. Approved,
superseded, or retired controls retain their history and use governed retirement
instead of discard.

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
- The later lifecycle and Person-owner increments below extend this initial
  activation contract; its former exclusions do not describe current scope.

## Successors and retirement

`POST .../controls/{control_id}/successors` proposes new content against
`expected_approved_version_id`. `POST .../controls/{control_id}/retirement-proposals`
proposes an effective end and rationale against that same exact approved
version. `GET .../controls/{control_id}/impact-preview?expected_revision=` returns
the contributors and digest described below. Successor approval and
`POST .../controls/{control_id}/retirements` require the complete, unchanged
impact digest and the owning workflow's independent decision. Proposals and
read-only previews are machine-appropriate; approval remains HTTP-only. A
successor or retirement preserves the prior approved content, effective
interval, decisions, and references. The
`Compliance:Controls:LifecycleEnabled` gate applies to these writes.

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
- The impact preview has authoritative contributors for `mappings` (active and pending
  criterion mappings naming the control), `risk_treatments` (proposed and accepted control
  treatment assertions), and `readiness` (whether the latest readiness assessment relied on a
  version of the control).
- The `evidence` contributor includes retained open, fulfilled, and cancelled requests; their
  fulfilment artifacts; every attestation version's support; planned/current/submitted evaluation
  evidence; corrective-action evidence; and closure evidence even after a finding is reopened.
  Artifact IDs are deduplicated while their source relationships and inspected version/digest
  remain in the preview. Unresolved textual support is represented by its canonical owning
  attestation, evaluation, action, or closure. This read does not verify artifact content.
- The `work` contributor includes every pending, approved, and superseded cadence plan;
  every recorded occurrence, including completed historical work; retained evaluations;
  related findings and corrective actions; and retained evidence requests. A cadence plan
  represents its future expected work without generating an infinite list of occurrences.
  Findings resolve through canonical control/occurrence/evaluation IDs or the owning control's
  normalized identifier. Exact control-version links are retained where the source has them;
  a request linked only to the stable control has no exact `version_id`.
- Evidence and work read their authoritative tenant/program streams without actor-filtered
  queues or a date horizon. Source revisions and retained relationships enter the digest, so
  a source mutation invalidates an earlier preview when approval recomputes it. Each whole
  context combines its sources before applying the 200-record bound; overflow reports
  `pending` and blocks both successor and retirement decisions. `engagements` remains
  `unlinked` because that owning source is absent from this build.
- Impact confirmation re-reads independent source streams before the Control decision append.
  The owning Control revision is checked at append, but independent source revisions are not
  atomically fenced through that write. Concurrent source mutation between confirmation and
  append remains the stronger atomic linked-source contract in
  [#519](https://github.com/bdgrz/compliance/issues/519), separate from these evidence/work
  contributors. The contributors preserve the existing recomputed-digest contract; sequential
  mutation tests prove that recheck and do not prove the later interleaving safe.
- Person-owner designation and withdrawal emit new discriminators, so they share the
  `Compliance:Controls:LifecycleEnabled` readers-before-writers gate. Enabling it and
  `ActivationEnabled` in production remains a rollout decision.

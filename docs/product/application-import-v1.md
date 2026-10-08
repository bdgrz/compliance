# Application import v1: bounded staging and whole-batch acceptance

Status: bounded staging, reconciliation preview, personal whole-batch acceptance,
trusted worker execution/recovery, progress and rejected-item reports for EN-05 / R1-10b.
Acceptance currently supports resolved batches with no complete-source omissions;
omissions still block until governed retirement and complete downstream impact exist.
Staging and six read queries are MCP tools. Correlation, acceptance and cancellation
are personal HTTP-only decisions. This optional path creates source observations;
it grants no connector authority, reviewed scope or governed field adoption.
Current delivery evidence and remaining retirement gates belong to
[EN-05 backend #195](https://github.com/bdgrz/compliance/issues/195).
The technical decision is
[ADR 0005](../architecture/decisions/0005-import-reconciliation-and-background-processing.md).

Accepted ADR 0005 (2026-09-22) makes acceptance all-or-nothing behind a
durable batch visibility barrier. `AcceptApplicationImportRow`,
`partially_accepted`, and per-row effect acceptance are rejected design
alternatives, not supported operations. Their earlier description is retained
in repository history. The target role policy allows Contributors to stage
and preview, while Compliance Leads or Org Admins accept or cancel a whole
batch (M0-D03). The staging grant is delivered. Correlation decisions require
the same inventory-management authority as acceptance and cancellation.

## Common rules

- Static HTTP path segments below use `kebab-case`; interpolated path values,
  query names, JSON property names, and enum values use `snake_case`. The
  Portia request record uses `TenantId` from
  `{tenant_id}`; an extra body `tenant_id` is ignored by the current generated
  binder and cannot redirect the tenant scope. Each route requires the
  existing API-user authentication policy and a Portia request authorizer.
- `batch_id`, `submission_id`, `row_id`, and `application_id` are opaque UUIDs.
  `source_key`, `source_namespace`, and `source_record_id` are tenant
  declarations, not verified external authority. A source claim is identified
  by tenant, source key, namespace, object kind `application`, and exact source
  record ID. Names are never match keys.
- Authorization requires active tenant membership. Staging and batch, row,
  preview, progress and report reads permit `application_import.stage` or
  `application_inventory.manage`; correlation, acceptance and cancellation require the latter. A nonmember or
  wrong-tenant
  batch returns 404 before metadata or row counts are read; an active member
  without the grant receives 403. Import management authority does not grant
  access to restricted governed Application content. The import-staging grant
  is assigned to Org Admin, Compliance Lead, and Contributor on new and
  historical tenant registrations through the independent
  `ApplicationImportGrantBackfillV1` workload. It grants no ordinary inventory
  writes, correlation, acceptance or cancellation.
- Success responses use Portia's current result mapping: 200 for a value and
  204 for an empty command result. `Page<T>` has `items` and `next_cursor`.
  `limit` defaults to 50 and accepts 1–200. An invalid cursor or a cursor from
  another tenant or batch returns 400; with an empty source ledger, row and preview
  cursors for the same batch and lifecycle revision are interchangeable because they share one indexed
  row query. Cursors bind tenant, batch, and lifecycle revision; a correlation or
  cancellation between pages returns transient 409 and requires paging to restart.
  Historical unversioned cursors remain usable at staged revision 1 with an empty
  source ledger; after a lifecycle or source transition, preview requires restarting. All
  listed GETs can report 409 with `transient: true`
  when their requested `minimum_revision` is ahead of the source or projection.
- OpenAPI 3.1 must list each mapped operation, parameters, schema, 200/204,
  400/401/403/404/409/413/415/500 results as applicable, and the API-user
  security requirement. The current global Portia JSON-body cap is 10 MiB.
  An overlarge request returns 413; non-JSON media returns 415. The host's
  existing problem body and `Portia-Transient` header apply.

## Write operations

| HTTP operation | Portia request/result | Success and purpose | MCP |
| --- | --- | --- | --- |
| `POST /api/v1/tenants/{tenant_id}/application-imports` | `StageApplicationImport` → `ApplicationImportRegistration` | 200; store one immutable bounded observation and return `batch_id`, `revision`, `content_sha256` | `bdgrz.application_import.stage` (idempotent, bounded JSON) |
| `POST /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations` | `CancelApplicationImport` → no value | 204 before acceptance or while accepting and before commit, rolling back every pending effect; 409 after commit or terminal failure | none; HTTP-only |

| `POST /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations` | `CorrelateApplicationImportRow` → no value | 204; record an attributable link-existing or create-new choice without changing inventory | none; HTTP-only |

| `POST /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances` | `AcceptApplicationImport` → no value | 204; freeze every resolved valid row under `expected_batch_revision`; worker commits all effects together | none; HTTP-only |

Acceptance rejects stale batch/linked-target revisions, invalid or duplicate rows,
unresolved correlations and complete-source omissions. Replaying the frozen plan's
original expected revision or its current lifecycle revision adds no decision or
effect. Source observations never overwrite governed fields on linked applications.

`StageApplicationImport` body:

```json
{
  "submission_id": "018f0ed4-5d29-7e91-86bb-31a137fd6a6d",
  "source_key": "tenant_maintained_application_list",
  "source_namespace": "primary",
  "coverage": "partial",
  "rows": [
    {
      "source_record_id": "app_0042",
      "name": "Payroll",
      "purpose": "Payroll administration",
      "owner_reference": "Finance operations"
    }
  ]
}
```

`coverage` is `partial` or `declared_complete`. A complete declaration is
attributed to the caller; it does not verify the source. The body has 1–200
rows. `source_key` and `source_namespace` are nonblank and at most 128
characters each; `source_record_id` must be nonblank and at most 256 for later
acceptance; `name` must be nonblank and at most 200; `purpose` must be nonblank
and at most 2,000; nullable `owner_reference` is at most 2,000. Reject
surrounding whitespace in source identity fields; compare
them exactly and case-sensitively. Trim display and description fields before
canonical hashing and retain the supplied values for provenance. Duplicate
`source_record_id` values remain staged as separate rows with a blocking
validation finding. Values exceeding the stated field caps are rejected before
an event is written; blank row fields are retained with findings. The staged
event's serialized snake_case payload must also fit 48 KiB, leaving more than
11 KiB for Portia's envelope, metadata, stream address and Fitz framing under
Fitz's 61,247-byte event limit. A 200-row submission of individually legal
but long values can therefore return 400. Raw
rows and rejected-row reports follow the seven-year evidence retention policy
in the accepted [M0-D16 decision](decisions/m0-d16-evidence-handling.md).
Staging does not yet implement disposition. The server computes
`content_sha256`; a client cannot
assert it. Repeating the same tenant/source/namespace/submission ID with identical
canonical content returns the original registration. Different content with
that ID returns 409. The response is:

```json
{
  "batch_id": "018f0ed4-5d29-7e91-86bb-31a137fd6a6e",
  "revision": 1,
  "content_sha256": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
}
```

`CancelApplicationImport` takes `expected_batch_revision` and a nonblank
`reason`. Cancellation before commit retains the batch, rows, and attributed terminal decision.
New cancellation decisions are serialized on the ledger for the exact tenant,
source key, and namespace, in a separate stream within `application_imports`.
The staged batch remains immutable; historical cancellations on its original
stream remain terminal and readable. Staging replay returns the lifecycle
revision, and batch/row/preview reads check both the raw batch and the ledger
before accepting projected revision and state. The existing projector also
consumes the ledger's cancellation and correlation events.

Upgrade API and worker together. Older API hosts can write cancellation to the
original batch stream and cannot satisfy ledger-only lifecycle fencing. Worker
execution is registered as the per-tenant `ApplicationImportExecutionV1` reactor;
it consumes only the durable plan seal from the exact authoritative source stream.
A trusted system actor cannot initiate acceptance. It rechecks tenant activity,
replays exact frozen effects and writes one source-ledger commit. Process interruption
or transient failure leaves the checkpoint pending; resumption adds no duplicate
applied effects. Inactive tenants return transient conflict and resume after reactivation.
A permanent effect/verification rejection records a terminal `failed` marker under
the matching frozen plan. Pending events remain invisible audit records and their
reservations settle only after durable rollback. Cancellation and commit serialize
on the same ledger; a late failure cannot overwrite a committed or canceled batch.

## Pre-acceptance correlation

`CorrelateApplicationImportRow` takes `expected_batch_revision`, `decision`
(`link_existing` or `create_new`), nullable `application_id` and
`expected_application_revision`, and a nonblank `reason` of at most 2000 characters.
A link requires the exact existing, active tenant application and its current
revision. A create-new decision requires both target fields to be null; the ledger
records a deterministic target ID derived from the batch and row. Invalid and
duplicate rows cannot be correlated, and canceled batches reject new decisions.

The source ledger records the member, display, reason, time, and batch revision.
An identical replay preserves that attribution and adds no event. A changed choice
requires the current batch revision and advances it, invalidating older preview
pages. Linking never overwrites governed application fields, and choosing a new
application does not create it. Acceptance must recheck each target and revision
from authoritative streams.

Preview rows expose nullable `correlation` with the recorded decision, target,
expected target revision, attribution, and decision revision. A linked target that
is now missing, retired, or revised adds `correlation_target_changed`. A correlation choice alone grants no source authority. Preview consumes durable
source claims, compares accepted source observations, and rechecks claimed targets.
A missing, retired, or revised claimed target adds `source_claim_target_changed`
and is `conflicting` until a valid attributed correlation to the same target
rechecks its current revision. Source identities cannot be rebound.

### Committed source-claim foundation

The internal source ledger derives claims from durable commit markers and their
frozen plans. Each claim retains the exact source record ID, target application,
accepted field observation, batch, submitter, approver, and commit time. Stream
order selects the latest accepted observation; caller timestamps do not reorder
claims. Pending plans, unsaved commits, and cancellations establish no claim.

A repeated source record uses its committed target as a link-existing plan row.
Its accepted target revision is rechecked against the authoritative application;
later governed changes require a fresh attributed correlation to the same target.
Correlation and replay reject rebinding a committed source ID to another target.
Unknown source IDs still require explicit correlation, and identity remains exact,
case-sensitive, and scoped to the tenant/source key/namespace.

Source-field comparison reports changes against the last accepted observation,
using the staging contract's trimmed display fields. It never overwrites governed
fields or treats a changed observation as a reviewed revision. Complete-source
omissions can be enumerated internally as proposals; partial coverage returns no
omissions. Acceptance planning rejects complete-source omissions until governed
retirement plans and effects exist. No retirement is inferred or applied here.

Public preview classifies staged observations using durable claims and separately
pages complete-source omissions. Retirement effects and complete downstream impact
remain undelivered and block omission-bearing acceptance. Worker execution,
progress and rejected-item reports below support the non-retirement path.

## Read operations and MCP

| HTTP operation | Portia request/result | Read-only MCP discriminator |
| --- | --- | --- |
| `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}?minimum_revision={n}` | `GetApplicationImport` → `ApplicationImportView` | `bdgrz.application_import.get` |
| `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows?limit={n}&cursor={opaque}&minimum_revision={n}` | `ListApplicationImportRows` → `Page<ApplicationImportRowView>` | `bdgrz.application_import.rows.list` |
| `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview?limit={n}&cursor={opaque}&minimum_revision={n}` | `PreviewApplicationImport` → `Page<ApplicationImportPreviewRow>` | `bdgrz.application_import.preview` |
| `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/preview/missing?limit={n}&cursor={opaque}&minimum_revision={n}` | `PreviewMissingApplicationImportRows` → `Page<ApplicationImportMissingRow>` | `bdgrz.application_import.missing.preview` |
| `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/progress?minimum_revision={n}` | `GetApplicationImportProgress` → `ApplicationImportProgress` | `bdgrz.application_import.progress` |
| `GET /api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rejected-report?minimum_revision={n}` | `GetApplicationImportRejectedReport` → `ApplicationImportRejectedReport` | `bdgrz.application_import.rejected_report` |

The six read queries are registered as `ReadOnly` with Portia MCP, and staging
is registered as an `Idempotent` command. Portia 0.5.3 fixes the MCP binder so
object and array arguments are parsed from their raw JSON before source-generated
request binding. The application verifies the bounded `rows` array through a real
broker call, including tenant authorization. Correlation, acceptance and cancellation are HTTP-only. Tool
arguments use the same snake_case input names and return the same result fields.
No MCP tool makes an acceptance decision for a staged row or performs a personal
sign-off. Discovery shows the stage command and the six read tools. Unauthorized
calls must be denied before projection data is read.

`ApplicationImportView` includes `tenant_id`, `batch_id`, `submission_id`,
`source_key`, `source_namespace`, `coverage`, `content_sha256`, `revision`, `state`, `submitted_by_member_id`,
`submitted_by_display`, `submitted_at`, `row_count`, `invalid_count`,
`pending_count`, `applied_count`, `skipped_count`, `failed_count`, and
`last_progress_at`. States are `preview_ready`, `accepting`, `committed`,
`canceled` and `failed`. Accepting projects all planned rows as pending; commit
projects all rows as applied together; cancellation/failure clears pending and
leaves zero applied. Failure counts the whole rejected batch. `partially_accepted`
is unsupported.
Counts are scoped to that batch and never published before authorization or a
freshness check.

`ApplicationImportRowView` includes `tenant_id`, `batch_id`, `row_id`,
`row_number`, `source_record_id`, original `name`, `purpose`, and
`owner_reference`, validation findings, `processing_state`, and nullable
`application_id`. The first slice reports only `processing_state: staged`,
with a null `application_id`, and retains the attributable submission time on
its batch.
Cancellation retains the original staged rows and its attributed batch
decision. Pre-acceptance correlation decisions are recorded on the same source
ledger, retaining their event history.
`ApplicationImportPreviewRow` adds `match_state`, candidate Application IDs,
changed field names, `acceptance_blockers`, and nullable attributed `correlation`.
The public preview emits:

- `unmatched`: a valid source ID has no committed claim or correlation;
  `correlation_required` blocks acceptance planning.
- `new`: a valid source ID has an explicit correlation but no committed claim.
  It is new to this source, including when linked to an existing application.
- `unchanged` or `changed`: the source ID has a committed claim; changed fields
  compare trimmed `name`, `purpose`, and `owner_reference` against the last
  accepted source observation, independently of governed application fields.
- `conflicting`: a linked or claimed target is missing, retired, or revised,
  or a recorded choice attempts to rebind a committed source identity.
- `duplicate` or `invalid`: staged validation findings take precedence over
  matching, and remain acceptance blockers.

Candidates come only from the exact source claim or attributed correlation;
there is no inventory scan or name match. Preview never creates, revises, retires,
accepts, or silently correlates an application. A canceled batch adds
`batch_canceled`. Complete-source omissions add
`missing_source_retirement_unavailable` to staged preview rows until governed
retirement plans exist. Partial coverage never infers omissions. The separate
missing-row preview exposes synthetic `missing_from_source` proposals; they are
never automatic deletion or retirement. No claim of whole-batch acceptability
follows from an individual row having no blockers; public acceptance is still
unavailable.

Preview reads immutable staged rows and the authoritative source ledger, verifies
projected payloads against staging, and retries with transient 409 if an undurable
commit or a source-ledger change prevents a consistent read. Preview continuation
cursors bind both the batch lifecycle revision and durable source-ledger position,
including changes from other batches of the same source. Row reads can consume
preview cursors, but their returned cursors bind only raw-row lifecycle state.
Once the source ledger has advanced, preview requires its own continuation cursor;
a valid older row/legacy cursor returns transient 409 and requires restarting
preview. With an empty source ledger, existing row/preview cursor sharing remains
available. Malformed or cross-tenant/batch cursors remain 400. Target streams are
rechecked on each page; the future acceptance command must recheck the complete
plan and exact targets. Preview paging is not a frozen target snapshot.

### Missing-row proposals

`GET .../preview/missing` and its read-only MCP tool page the exact durable source
claims absent from a `declared_complete` batch. Partial coverage returns an empty
page. Presence uses the exact staged source ID even if that row has other validation
findings; invalid fields do not turn a present ID into an omission. The operation
uses the same active-membership and inventory-management/import-staging grants as
other import reads. It exposes source observations and target lifecycle freshness,
not restricted program relationships or downstream impact details.

`ApplicationImportMissingRow` has no staged `row_id`. Its stable `source_claim_id`
is derived from the tenant/source ledger and exact source ID; `batch_id` identifies
the batch being previewed, and `last_observed_batch_id` identifies the accepted
observation. `name`, `purpose`, `owner_reference`, and `observation_committed_at`
come from that accepted source observation. `application_id` retains the claimed
target. `expected_application_revision` is its accepted revision (1 for a committed
create); `current_application_revision` is null for a missing target. `match_state`
is `missing_from_source`, including when its target now conflicts.

Each proposal retains `missing_source_retirement_unavailable` and
`retirement_impact_unavailable` until an exact governed retirement plan and the
required downstream-impact preview exist. Missing, retired, or revised targets add
`source_claim_target_changed`; a canceled batch adds `batch_canceled`. These rows
cannot be supplied to the staged-row correlation command. A source claim or a
previewed omission alone never authorizes retirement. Existing application-change
preview is advisory and incomplete; it cannot be treated as a complete import
retirement impact gate. References and their history must remain through retirement.

Limits default to 50 and accept 1–200. Claims are ordered by exact source ID with
ordinal comparison. Missing-row cursors are separate from staged row/preview
cursors and bind tenant, batch, lifecycle revision, durable source-ledger position,
and continuation offset. Malformed/cross-scope/out-of-range cursors return 400;
a changed lifecycle or any other-batch source-ledger advance returns transient 409.
Source state is rechecked after the target reads, including on empty pages, so
undurable commits and concurrent source changes cannot yield mixed omission pages.
Targets are checked per page; future acceptance must freeze and recheck every
retirement and its impact. Staged row counts and the existing preview schema are
unchanged. No events, active changes, or retirement effects are emitted here.

## Error and verification contract

| Situation | Result |
| --- | --- |
| Malformed UUID, invalid field/coverage/decision, too many rows, unsupported cursor | 400 validation problem |
| No authenticated Bdgrz identity | 401 |
| Member lacks interim inventory grant or tenant is suspended | 403 |
| Unknown tenant to nonmember, missing batch/row, or wrong-tenant ID | 404 with no existence/count disclosure |
| Submission ID reused with changed content, stale expected revision, acceptance with an invalid or unresolved row, another acceptance in progress for the source, cancellation after commit, or state transition conflict | 409 |
| Requested revision not yet projected or worker intent still pending where a completed result was requested | 409, `transient: true` where retry can resolve it |
| Body exceeds Portia JSON limit / unsupported media type | 413 / 415 |

Focused tests must prove exact content replay and changed-content conflict;
duplicate source IDs; no visible Application before the batch commits;
idempotent effect replay after a worker crash mid-accept, rolling forward to
one commit; cancellation before commit leaving nothing visible, and 409 after
commit; declared-complete missing rows proposed as tombstones and applied only
on acceptance; cross-tenant non-disclosure for batch IDs, rows, counts and MCP;
source/projection lag; and standalone/split API-worker parity.
OpenAPI, Native AOT, formatting, and the full applicable suites remain gates
for implementation, not evidence supplied by this contract draft. A real
simultaneous append race may still surface as a transport 500 until
[Portia #60](https://github.com/cntryl/portia/issues/60) supplies supported
commit-time conflict mapping; implementation must not mask it with an
application catch or claim the 409 race contract prematurely.

## Durable execution progress and rejected-item report

Progress reads the durable source plan and exact target effects rather than waiting
for a directory checkpoint. It returns `planned_count`, `durable_effect_count` and
`applied_count`, lifecycle `revision`/`state`, accepted/committed timestamps and bounded
`failure_code`/`failed_row_id`. Durable target count is a monotonic observation of
one exact frozen plan, not a simultaneous global target snapshot. Only a durable
commit reports applied rows. An end-of-read source fence rejects lifecycle/source
changes with transient 409. A failed batch may retain durable audit effects while
its applied count stays zero.

The rejected report contains at most the 200 retained staged rows. Each row has
`row_id`, `row_number`, exact `source_record_id` and bounded reason codes. Findings
include immutable validation, unresolved correlation and stale/missing/retired
linked-target blockers. A failed or canceled batch reports every row as unapplied;
no rejected report is partial acceptance. `batch_findings` includes complete-source
retirement/impact and another-source-acceptance blockers. It returns no governed
target fields or exception text. Current target observations are advisory; acceptance
and execution revalidate authoritative invariants. Both reads use the ordinary
import-read grants, enforce tenant identity/minimum revision, and retain no extra
raw copies. Staged rows and the derived report follow the existing seven-year
retention boundary; this change introduces no disposition or retention override.

Current feature proof is focused/full unit tests. Live HTTP/MCP transport, split-host
restart and integrated product validation remain in the dedicated E2E milestone.

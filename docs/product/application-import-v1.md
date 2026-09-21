# Application import v1 contract draft

Status: proposed for M0-A06 / EN-05 / R1-10b review, 2026-09-20. The bounded
stage HTTP route and three batch/row/preview HTTP and read-only MCP queries
are implemented as a first slice. The stage MCP tool is pending
[Portia #61](https://github.com/cntryl/portia/issues/61) for nested-array
binding; acceptance and cancellation remain proposed. This
contract covers bounded tenant-supplied rows only; it grants no source
authority or reviewed scope.
The technical decision is
[ADR 0005](../architecture/decisions/0005-import-reconciliation-and-background-processing.md).

The [EN-05 backlog acceptance](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay)
and [backend child #195](https://github.com/bdgrz/compliance/issues/195)
require failed or canceled imports to leave no partial active records. The
row-acceptance and cancellation operations below are a **conditional
candidate**, not approved for implementation or backend acceptance until the
product owner records how `partially_accepted` after a row failure satisfies
or changes that requirement. Staging and read contracts can be reviewed
independently.

## Common rules

- All HTTP paths and query names, JSON property names and enum values below
  use `snake_case`. The Portia request record uses `TenantId` from
  `{tenant_id}`; an extra body `tenant_id` is ignored by the current generated
  binder and cannot redirect the tenant scope. Each route requires the
  existing API-user authentication policy and a Portia request authorizer.
- `batch_id`, `submission_id`, `row_id`, and `application_id` are opaque UUIDs.
  `source_key`, `source_namespace`, and `source_record_id` are tenant
  declarations, not verified external authority. A source claim is identified
  by tenant, source key, namespace, object kind `application`, and exact source
  record ID. Names are never match keys.
- Initial authorization uses the existing Application inventory boundary:
  active tenant membership and `program.manage`. A nonmember or wrong-tenant
  batch returns 404 before metadata or row counts are read; an active member
  without the grant receives 403. `program.manage` is an interim grant, not a
  final restricted-inventory policy.
- Success responses use Portia's current result mapping: 200 for a value and
  204 for an empty command result. `Page<T>` has `items` and `next_cursor`.
  `limit` defaults to 50 and accepts 1–200. An invalid cursor or a cursor from
  another tenant or batch returns 400; row and preview cursors for the same
  batch are interchangeable because they share one indexed row query. All
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
| `POST /api/v1/tenants/{tenant_id}/application_imports` | `StageApplicationImport` → `ApplicationImportRegistration` | 200; store one immutable bounded observation and return `batch_id`, `revision`, `content_sha256` | Pending `bdgrz.application_import.stage` (idempotent, bounded JSON); Portia 0.5.2 cannot bind the `rows` array |
| `POST /api/v1/tenants/{tenant_id}/application_imports/{batch_id}/rows/{row_id}/acceptances` | `AcceptApplicationImportRow` → `ApplicationImportRowReceipt` | 200; record one explicit decision and return intent state and causal `decision_id` | none; consequence-acceptance is HTTP-only |
| `POST /api/v1/tenants/{tenant_id}/application_imports/{batch_id}/cancellations` | `CancelApplicationImport` → no value | 204 only before any accepted row intent; otherwise 409 | none |

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
but long values can therefore return 400. Raw row
retention and deletion policy is unresolved under EN-06 and M0-A07; staging
does not claim production retention acceptance. The server computes
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

`AcceptApplicationImportRow` body:

```json
{
  "expected_batch_revision": 4,
  "resolution": "link_existing",
  "target_application_id": "018f0ed4-5d29-7e91-86bb-31a137fd6a6f",
  "expected_application_revision": 2,
  "supersedes_decision_id": null,
  "rationale": "Same operated application under an older label"
}
```

`resolution` is `create`, `link_existing`, or `skip`. `skip` records a decision
without a source-claim reservation or Application effect; a later submission
may resolve that source ID. `target_application_id`
and `expected_application_revision` are required only for `link_existing`;
they are forbidden for the other resolutions. `rationale` is required for
`link_existing` and `skip`. `create` uses a deterministic application ID from
the exact source-claim tuple, stable across batches; it cannot overwrite
another tenant's record. A source-claim binding serializes competing
correlations before an Application effect is attempted. A stale batch
or Application revision returns 409 with the current revision in the detail.
Once a source claim is bound, another batch cannot `create` it again; its
preview reports the existing binding and any changed source fields.
`link_existing` only attaches an attributed source observation to the exact
target; it does not replace the governed Application's name, purpose, owner,
classification, or scope. Adopting changed source fields requires a separate
authorized, reviewed Application revision with its own expected revision.
Which fields and source authority can support that adoption remains a product
decision under M0-D28 and M0-D05. A pending source-claim reservation tied to
another target is a visible conflict, not an automatic rematch.
Invalid, duplicate, already applied, or ambiguously matched rows cannot be
applied. An outstanding effect cannot be replaced. A row in `needs_resolution`
may receive a new decision only with `supersedes_decision_id` identifying its
exact prior decision, current `expected_batch_revision`, a new target and
expected target revision when applicable, and a rationale. The prior decision
and failure remain in immutable history. The worker may mark
`needs_resolution` only after an authoritative target Application stream read
finds no prior effect ID and a current revision strictly beyond the prior
expected revision. A timeout or lagging projection cannot prove this. The
source-claim reservation transitions to the new decision only after that
terminal nonapplied proof. Supersession by `skip` records a reviewed
release/unbound source-claim transition, retaining the old reservation in
history. Otherwise the old intent remains pending. A
repeated identical decision returns the same receipt. The receipt
contains `decision_id`, `batch_id`, `row_id`, `revision`, and `state` (`pending`,
`applied`, or `skipped`); it does not claim an Application is active while its
reactor intent remains pending. The row status query supplies the eventual
`application_id` and any recoverable failure. Decision IDs and effect IDs are
stable across reactor replay, independent of Portia reaction request IDs.

`CancelApplicationImport` body has `expected_batch_revision` and a nonblank
`reason`. The authoritative batch aggregate rejects cancellation with 409
after **any** accepted row intent, including a pending effect. Cancellation
before acceptance leaves no active Application. A terminal `failed` batch
also has zero accepted intents. A row failure after earlier accepted effects
keeps the batch `partially_accepted` with a retryable or `needs_resolution`
row; whether this satisfies #195 requires the product decision above.

## Read operations and MCP

| HTTP operation | Portia request/result | Read-only MCP discriminator |
| --- | --- | --- |
| `GET /api/v1/tenants/{tenant_id}/application_imports/{batch_id}?minimum_revision={n}` | `GetApplicationImport` → `ApplicationImportView` | `bdgrz.application_import.get` |
| `GET /api/v1/tenants/{tenant_id}/application_imports/{batch_id}/rows?limit={n}&cursor={opaque}&minimum_revision={n}` | `ListApplicationImportRows` → `Page<ApplicationImportRowView>` | `bdgrz.application_import.rows.list` |
| `GET /api/v1/tenants/{tenant_id}/application_imports/{batch_id}/preview?limit={n}&cursor={opaque}&minimum_revision={n}` | `PreviewApplicationImport` → `Page<ApplicationImportPreviewRow>` | `bdgrz.application_import.preview` |

The three read queries are registered as `ReadOnly` with Portia MCP. Portia
0.5.2 `McpToolRegistration.Bind<TRequest>` calls `JsonValue.Create` on the
`rows` JSON array and fails before Portia authorization or the stage handler.
After Portia supports object arrays in tool arguments, register the stage command as
`Idempotent`. Tool arguments use the same snake_case input names and return the
same result fields. No MCP tool makes an acceptance decision for a staged row
or performs a personal sign-off. Discovery currently shows three read tools;
it must show the fourth after the binder fix. Unauthorized calls must be denied
before projection data is read.

`ApplicationImportView` includes `tenant_id`, `batch_id`, `submission_id`,
`source_key`, `source_namespace`, `coverage`, `content_sha256`, `revision`, `state`, `submitted_by_member_id`,
`submitted_by_display`, `submitted_at`, `row_count`, `invalid_count`,
`pending_count`, `applied_count`, `skipped_count`, `failed_count`, and
`last_progress_at`. The first slice emits only `preview_ready`, with zero
processing counts. `staging`, `accepting`, `partially_accepted`, `accepted`,
`canceling`, `canceled`, and `failed_retryable` are proposed future states.
Counts are scoped to that batch and never published before authorization or a
freshness check.

`ApplicationImportRowView` includes `tenant_id`, `batch_id`, `row_id`,
`row_number`, `source_record_id`, original `name`, `purpose`, and
`owner_reference`, validation findings, `processing_state`, and nullable
`application_id`. The first slice reports only `processing_state: staged`,
with a null `application_id`, and retains the attributable submission time on
its batch.
Decision and observation times, `needs_resolution`, and decision history are
proposed future additions that must retain every superseded intent and terminal
nonapplied result.
`ApplicationImportPreviewRow` adds `match_state` (`unmatched`, `unchanged`,
`changed`, `duplicate`, `ambiguous`, `missing_from_source`, or `invalid`),
candidate Application IDs, changed field names, and `acceptance_blockers`.
Only `unmatched`, `duplicate`, and `invalid` are emitted in the first slice;
the other match states require source claims and accepted observations.
`unmatched` means no accepted source-claim binding, not that no governed
Application exists. In the first slice, no source-claim store exists, so every
otherwise valid row is provisionally `unmatched` with a
`source_claims_unavailable` acceptance blocker. No Application inventory
scan, name match, or completeness assertion is made. Candidates are suggestions only; the future acceptance command rechecks the exact
target and revisions from authoritative streams. A complete-source omission
will be a synthetic preview row with no staged `row_id` and cannot be accepted;
its stable source-claim ID identifies the prior observation. It is never an
automatic deletion or retirement. The future matching preview must check that
the Application inventory and source-claim projections have caught up before
claiming a complete match; otherwise it reports a retryable 409. The current
preview only checks the import batch source against its batch/row projection.

## Error and verification contract

| Situation | Result |
| --- | --- |
| Malformed UUID, invalid field/coverage/decision, too many rows, unsupported cursor | 400 validation problem |
| No authenticated Bdgrz identity | 401 |
| Member lacks interim inventory grant or tenant is suspended | 403 |
| Unknown tenant to nonmember, missing batch/row, or wrong-tenant ID | 404 with no existence/count disclosure |
| Submission ID reused with changed content, stale expected revision, unresolved or duplicate acceptance, pending prior effect, cancellation after an accepted intent, or state transition conflict | 409 |
| Requested revision not yet projected or worker intent still pending where a completed result was requested | 409, `transient: true` where retry can resolve it |
| Body exceeds Portia JSON limit / unsupported media type | 413 / 415 |

Focused tests must prove exact content replay and changed-content conflict;
duplicate source IDs; no active Application before explicit row acceptance;
idempotent create/link retry after worker failure; stale target revisions;
authoritative nonapplied proof followed by a superseding human decision, with
every prior decision retained; no supersession on timeout or projection lag;
declared-complete missing rows without deletion; cancellation before first
acceptance and 409 after any accepted intent; cross-tenant non-disclosure for batch IDs, rows,
counts and MCP; source/projection lag; and standalone/split API-worker parity.
OpenAPI, Native AOT, formatting, and the full applicable suites remain gates
for implementation, not evidence supplied by this contract draft. A real
simultaneous append race may still surface as a transport 500 until
[Portia #60](https://github.com/cntryl/portia/issues/60) supplies supported
commit-time conflict mapping; implementation must not mask it with an
application catch or claim the 409 race contract prematurely.

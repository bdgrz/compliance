# R1-11a Manual workforce person backend slice

This slice records a workforce `Person` manually for one tenant. It is partial
delivery under [#219](https://github.com/bdgrz/compliance/issues/219) and follows
[M0-D06](decisions/m0-d06-workforce-source.md): with no HRIS integration, the
manually maintained roster is the authoritative source, and each entry is
attributed to the member who recorded it. A person need not be a platform member
(M0-D03). This slice gives application owners (M0-D05) and later responsibilities
a governed person to reference.

## Contract

- `POST /api/v1/tenants/{tenant_id}/people` records a person with a
  `display_name` (1–200 characters) and an optional `work_email` (a single
  address). The request ID becomes the `person_id`, so an identical retry
  succeeds and a retry with different content conflicts.
- `PUT .../people/{person_id}` requires `expected_revision` and replaces both
  fields. A stale revision returns conflict.
- `GET .../people/{person_id}` returns the current record, including
  `source_kind` (`manual`) and a `last_changed_by` actor snapshot.
  `minimum_revision` returns a transient conflict during source or projector
  lag. The collection `GET` is ordered by display name, checks the `people`
  projector checkpoint before returning even an empty page, and accepts a
  `limit` of 1–200 and a cursor.
- All four operations have MCP tools. Reads are marked read-only and revise is
  marked idempotent.

Every operation requires an active tenant membership with `workforce.manage`.
`WorkforceGrantBackfillReactor` grants it to the built-in tenant administration
and compliance management roles for every existing and new tenant, which matches
the M0-D06 default of Org Admin and Compliance Lead. Firm staff are denied
without an accepted engagement. Denial across tenants does not disclose that
the person exists.

## Storage

Each person owns a tenant-scoped `people/{person_id}` event stream
(`PersonRecorded`, `PersonRevised`). `PersonDirectoryV1` projects the current
row into Fitz, keyed by person, with a display-name index for listing.

## Work relationships

`WorkRelationship` records a person's M0-D06 minimum worker attributes under a
stable source worker ID:

- `POST /api/v1/tenants/{tenant_id}/work-relationships` takes `person_id`,
  `source_worker_id`, `worker_type` (`employee`, `contractor`,
  `external_collaborator`), `lifecycle_status` (`pending`, `active`,
  `on_leave`, `ended`), `start_date`, and optional `end_date`, `department`,
  `manager_person_id`, and `sponsor_person_id`. The relationship ID is derived
  from the tenant and the normalized worker ID, so a worker ID identifies one
  relationship: an identical retry succeeds, and the same worker ID for another
  person or with other content conflicts. Email is never an identifier.
- The person, manager, and sponsor must be recorded people of the tenant. An
  `ended` relationship needs an end date on or after its start date, nobody
  manages or sponsors themselves, and an external collaborator needs an
  accountable internal sponsor.
- `PUT .../work-relationships/{relationship_id}` requires `expected_revision`
  and replaces the terms; the person and worker ID never change. Recording a
  leaver is a revision to `ended` with an end date.
- `GET` of one relationship returns the restricted `manager_person_id`. The list
  (ordered by worker ID, lag-checked like people) returns it as null with
  `restricted_fields_redacted: true`.
- MCP tools mirror all four operations. Every operation requires
  `workforce.manage` (Org Admin and Compliance Lead by default).

Each relationship owns a `work-relationships/{relationship_id}` stream, and
`WorkRelationshipDirectoryV1` projects it.

## Not yet delivered

- Source precedence and reconciliation between sources, which needs a second
  source (HRIS import waits on [#347](https://github.com/bdgrz/compliance/issues/347)).
- The employment status reason and personal contact fields, and a separate
  restricted-field grant; today lists redact the manager and single reads
  require `workforce.manage`.
- Missing, duplicate, conflicting, stale, and access-only roster observations,
  resolving observations, correlation to platform membership, and HRIS import.

## Joiner, mover, and leaver observations (#221, partial)

`WorkforceObservationsV1` compares each accepted `work-relationships` version
with the previous one. A new or rehired relationship is a `joiner`, a change to
`ended` is a `leaver`, and a change of worker type, department, or sponsor is a
`mover` (manager-only changes are restricted and produce none). Observations are
open compliance work only; they never grant or revoke access.
`GET .../workforce-observations` (optional `kind`, `limit`, `cursor`,
lag-checked) and an MCP tool list them.

## Non-human identities (#223, partial)

`ServiceIdentity` (`service-identities/{id}` stream, `ServiceIdentityDirectoryV1`)
records a display name, identity kind (`workload`, `service`, `automation`,
`bot`, `integration`), approved purpose, optional environment, lifecycle
(`active`, `disabled`, `retired`), exactly one owner (`owner_kind` `person` or
`team`, validated against a recorded person or active team), and a `review_by`
date in the future and at most one year out. HTTP and MCP record, revise, get,
and list operations require `workforce.manage`. Reads evaluate `unowned` with
`unowned_reasons` (`review_expired`, `owner_relationship_ended`,
`owner_not_on_roster`, `owner_team_deleted`); `unowned_only` filters a list
page. Not yet delivered: expiry date, non-manual sources, and account
correlation.

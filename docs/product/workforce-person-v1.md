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

## Not yet delivered

- `WorkRelationship` (worker type, lifecycle status, start and end dates,
  manager, and department), stable source worker IDs and their uniqueness,
  source precedence, and reconciliation.
- Restricted workforce fields and field-level redaction. This slice stores no
  restricted field.
- Joiner, mover, and leaver observations, correlation to platform membership,
  and HRIS import (waits on [#347](https://github.com/bdgrz/compliance/issues/347)).

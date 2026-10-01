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
  relationship: with both restricted field-read grants, an identical retry
  succeeds, and the same worker ID for another person or with other content
  conflicts. Email is never an identifier.
- The person, manager, and sponsor must be recorded people of the tenant. An
  `ended` relationship needs an end date on or after its start date, nobody
  manages or sponsors themselves, and an external collaborator needs an
  accountable internal sponsor.
- `PUT .../work-relationships/{relationship_id}` requires `expected_revision`
  and replaces the terms; the person and worker ID never change. Recording a
  leaver is a revision to `ended` with an end date.
- `GET` of one relationship returns the restricted `manager_person_id` only to
  an actor with the `workforce.manager_chain` field grant (Tenant Administration
  by default); otherwise it is null with `restricted_fields_redacted: true`. The
  list (ordered by worker ID, lag-checked like people) always redacts it.
- MCP tools mirror all four operations. Every operation requires
  `workforce.manage` (Org Admin and Compliance Lead by default).
- Retrying an already recorded worker ID also requires both existing field-read
  grants: `workforce.manager_chain` and `workforce.personal_details`. Missing
  either grant yields the same response for every otherwise valid restricted-value
  guess: `Forbidden`, including null guesses and retries by the original actor
  after a lost network response. This protects initial manager
  and employment-status-reason equality from being disclosed through success or
  conflict. First creation still requires only `workforce.manage`, including
  creation with restricted values; the read policy is independent of this write
  permission. Identical authorized retries preserve the first event's attribution,
  even after later relationship revisions.

The retry gate runs against the executor's hydrated relationship before initial
terms are compared. A first creation committed before hydration is gated as a
retry; a competing creation after empty hydration is rejected by the existing
destination stream concurrency check, and its later retry requires both grants.
Field grants are evaluated at the existing permission-read boundary; that
observation governs this operation even if a grant is revoked concurrently.

Each relationship owns a `work-relationships/{relationship_id}` stream, and
`WorkRelationshipDirectoryV1` projects it.

## Not yet delivered

- HRIS import (waits on [#347](https://github.com/bdgrz/compliance/issues/347)).

Source observations and attributable reconciliation are available without an
import through the [source-governance contract](workforce-source-governance.md).
Personal contact and employment-status reason are captured and restricted under
[#515](https://github.com/bdgrz/compliance/issues/515).

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
page. Expiry, manually entered provider-source observations, and explicit
provider-account classification against service identities are also available.

## Membership correlation, reconciliation, and resolution (#461, #219)

`PUT .../people/{person_id}/membership-correlation` (`expected_revision`,
optional `user_id`) attributably links a roster person to one tenant client
member, or clears the link. It advances the person revision
(`PersonMembershipCorrelated`) and is returned as `correlated_user_id`. It never
grants, revokes, or changes access, and firm staff cannot be correlated.

`GET .../workforce-reconciliation-observations` (optional `kind`, `status`,
`limit`, `cursor`) evaluates the caught-up roster against memberships:

- `missing` / `no_work_relationship`: a person without a work relationship.
- `duplicate` / `member_correlated_to_several_people` or `shared_work_email`.
  Email is a duplicate signal, never an identifier.
- `conflicting` / `ended_worker_retains_access`: every relationship ended but the
  correlated member is not suspended.
- `stale` / `end_date_passed`, `start_date_passed`, or
  `correlated_member_missing`.
- `access_only` / `member_not_on_roster`: an active client member correlated with
  no person.

Observation IDs derive from the underlying facts (including relationship
revisions), so a closure stays attached until those facts change.
`PUT .../workforce-observations/{observation_id}/resolution` (`resolution`
`resolved` or `dismissed`, `note`) closes either a reconciliation observation or a
joiner/mover/leaver observation. Each closure is a
`workforce-observation-resolutions/{id}` stream projected by
`WorkforceObservationResolutionsV1`; lists overlay `status` and the attributed
`resolution`, and are transiently conflicted while that projection lags.

The canonical record's `source_kind: manual` describes its entry origin.
Separate immutable source observations record HRIS authority and IdP
corroboration, preview conflicts, and retain an attributed reconciliation decision
bound to an exact canonical revision. An observation does not apply its values.
See [workforce source governance](workforce-source-governance.md).

## Roster freeze fence (#479)

A roster freeze captures both roster projection checkpoints after its catch-up
check and re-reads them after paging. If either projection advanced during the
read, the freeze fails as a transient conflict without writing, so a snapshot
never mixes versions or includes a change made after the check.

## Service identity expiry (#462, partial)

Service identities take an optional `expires_on`. An active identity cannot
already be expired, and reads report `expired` for a non-retired identity on or
after that date. Provider-source observations corroborate externally observable
identity facts. `ClassifyAccessPrincipal` explicitly correlates an accepted
provider account or service principal to a governed `ServiceIdentityId`, with
attribution and classification history. This link never grants platform access.

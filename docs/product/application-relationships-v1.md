# Application relationships v1 (#977)

Inventory managers can record, list, and remove directed relationships between
Applications in the same tenant. The relationship belongs to its source
Application and points to one target Application. V1 supports `depends_on` and
`replaces`; neither type implies an approved successor, a lifecycle decision,
or approved scope.

## API

- `POST /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}`
  adds a relationship. Its JSON body contains `expected_relationship_revision`,
  `target_application_id`, and `relationship_type`.
- `DELETE` on the same resource removes that relationship and requires
  `expected_relationship_revision` as a query parameter.
- `GET /api/v1/tenants/{tenant_id}/applications/{application_id}/relationships`
  returns the current visible relationships. It accepts `limit`, `cursor`, and
  optional `minimum_revision` query parameters.

Mutation requests require Application inventory manage authorization. The
source must be active and visible to the actor, and the target must exist and
be visible in the same tenant. Missing, restricted, and cross-tenant resources
are returned as not found. A denied request does not append a source event.

Successful mutations acknowledge `relationship_id`,
`relationship_revision`, and `state` (`active` or `removed`). The authenticated
member is recorded on each add and removal. Repeating an operation with the
same relationship ID and payload returns its canonical acknowledgement. A
relationship ID cannot be reused with different content. A source may have at
most one active relationship for a given type and target. Removal retains a
tombstone, so an old retry cannot change a later relationship created with a
new ID.

## Revision and paging rules

Relationship add and remove events are stored in the source Application stream
with a relationship revision independent of Application metadata revision.
Concurrent changes use source-stream optimistic concurrency. Reads use the
canonical source stream rather than Fitz metadata projections.

Lists default to 50 items and accept limits through 200. Only active
relationships whose target is visible are included. The opaque continuation
cursor is bound to the tenant, source Application, and observed relationship
revision; a cursor from an older revision returns a retryable conflict and the
client restarts paging.

`minimum_revision` is a nonnegative read-your-own-writes lower bound. If the current
canonical relationship revision meets or exceeds it, the endpoint returns that
current snapshot. If the source is behind, the endpoint returns a retryable
409 with both the requested minimum and current revision. It does not return a
lower snapshot or reconstruct an exact historical revision.

## Boundaries

V1 does not approve successor relationships, calculate complete downstream
impact, govern retirement or merging, traverse a relationship graph, enforce
cycle rules, or maintain incoming indexes. Those capabilities remain under the
parent application lifecycle issue, #464.

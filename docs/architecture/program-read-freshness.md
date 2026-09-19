# Program and service read freshness

`GET /api/v1/tenants/{tenant_id}/programs/{program_id}` and
`GET /api/v1/tenants/{tenant_id}/client-services/{service_id}` accept an
optional positive `minimum_revision` query parameter. A read at or above that
revision succeeds. A lower projected revision returns a conflict so a client
can retry after its command while a separate worker catches up.

When the read model has not yet projected creation, the handler reads the
tenant-scoped event stream. An existing source record returns conflict;
a missing source record returns not-found. If the requested revision is ahead
of the source stream, conflict identifies that the source has not reached it.
The no-parameter behavior remains the current eventually consistent read.
Authorization runs before either read, so this check cannot disclose a record
to a user outside its tenant. The contract does not wait indefinitely or claim
that unrelated projections are current.

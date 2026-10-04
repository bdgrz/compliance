# Program and client service revision reads

Scope: bounded backend history contract for R1-01 and R1-02. Revision reads
support the same manually authored records used by the normal program and
service workflows; no source integration is required. Current delivery and
acceptance evidence belong to their owning stories in [backlog.md](backlog.md).

The backend exposes immutable Program and ClientService revision records through
authorized HTTP GET and read-only MCP tools. Exact program reads use
`/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}` and
`bdgrz.program.revision.get`. Exact service reads use
`/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions/{revision}`
and `bdgrz.client-service.revision.get`. Numeric revisions start at 1.

The existing revision lists accept optional `minimum_program_revision` and `minimum_service_revision` query parameters. The same fields are available on their MCP requests. A positive anchor checks the tenant-scoped projected head and, if it is behind, the event-sourced aggregate. An absent source returns 404, a source or projection behind the requested revision returns 409, and a nonpositive revision returns 400. Exact reads use their path revision as the freshness anchor, so an unprojected revision cannot appear as an ordinary missing record. Once projected, each exact record retains its original content and actor snapshot after later revisions.

Static HTTP path segments use kebab-case, including `client-services`;
interpolated path values and query names remain snake_case. This route migration
replaces the prior inconsistent `client_services` exact-read path. Portia
OpenAPI nullable UUID default generation was tracked in
[Portia #57](https://github.com/cntryl/portia/issues/57). Contract and broker test
sources cover these operations; their existence does not substitute for current
execution evidence at the reviewed implementation head.

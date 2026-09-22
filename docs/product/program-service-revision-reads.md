# Program and client service revision reads

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
OpenAPI nullable UUID default generation is tracked at cntryl/portia#57. Live
OpenAPI and broker tests cover these operations.

# System instance aggregate cutover

An Application owns metadata revisions. Each SystemInstance now owns a tenant-scoped
`system-instances/{systemInstanceId}` stream, an immutable `ApplicationId`, and its own
revision. Declaring a second instance or revising application metadata does not use
the other stream's optimistic concurrency token. A new declaration changes the
application's derived `has_system_instances` view but creates no application revision.

## V1 HTTP and MCP contract

The existing `expected_application_revision` field remains required on declaration.
It now means the parent application source must have reached **at least** that
revision. A metadata revision beyond the supplied value does not reject the
instance declaration. This intentionally changes the earlier equality rule; callers
that require a current metadata snapshot must read the application separately.

`minimum_application_revision` on instance reads still checks only application
metadata source and projection freshness. Exact instance reads additionally accept
optional `minimum_instance_revision`; the instance view exposes `revision`.
Lists check the versioned inventory projector's tenant cursor for pending application
or instance events before returning an empty or partial page. The application's
`has_system_instances` field is eventually projected; use the instance list for a
complete relationship read.

## Replay and deployment

The `ApplicationDirectoryV2` projection rebuilds in a new Fitz namespace from the
ordered tenant event cursor. It consumes old `SystemInstanceDeclared` application
events and new `SystemInstanceRegistered` instance events. Old application history
keeps its original instance rows and revision numbers. The new instance view retains
the old actor, timestamp, source, and `legacy_application_revision`; new views leave
that provenance field null. Projector data and checkpoint commit atomically, so
restart replays only uncommitted events.

Drain old API writers before switching traffic to the new write path. Old binaries
cannot honor the new stream boundary. During V2 backfill, new declarations return a
retryable conflict if historical instance events remain behind its checkpoint; the
tenant-scoped identity lookup then prevents a legacy ID from being reused under a
different application. Read clients retry transient conflicts until the V2 worker
has caught up. Do not delete the old application event streams or the old projection
namespace during this cutover.

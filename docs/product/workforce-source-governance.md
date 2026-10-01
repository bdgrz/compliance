# Governed workforce source observations

This backend capability addresses the source-governance work in
[#219](https://github.com/bdgrz/compliance/issues/219) and
[#462](https://github.com/bdgrz/compliance/issues/462). A member records canonical
facts they observed in an external source and explicitly correlates them to an
existing workforce record. CSV parsing, import jobs, connectors, and
client-specific mapping remain separate work after source discovery in #347.

## Source identity and facts

`source` contains `source_kind`, `source_system` (1–200 characters),
`source_record_id` (1–500), and `source_revision` (1–200). The source's object ID
and revision are opaque, case-sensitive values; email never identifies a worker.
The tenant and trimmed source tuple determine `observation_id`. Repeating the
same source revision, correlation, observation time, and facts succeeds;
changing them conflicts. A later source observation uses a new source revision
or capture identifier.

Every observation names `target_kind`, `target_id`, the checked
`expected_target_revision`, and `observed_at`, which cannot be in the future.
Exactly one payload is present in `facts`:

| Target | Sources | Facts |
| --- | --- | --- |
| `person` | `hris`, `idp` | `person`: display name and work email |
| `work_relationship` | `hris`, `idp` | `work_relationship`: existing WorkRelationshipTerms |
| `service_identity` | `provider` | `service_identity`: display name, identity kind, environment, lifecycle, expiry |

Personal contact is excluded. Provider observations do not supply NHI owner,
approved purpose, or review date: these remain governed organization facts.
Bounded but incomplete or contradictory source facts may be recorded as evidence;
they cannot be accepted while they differ from the canonical record.

## HTTP and MCP

All operations are under
`/api/v1/tenants/{tenant_id}/workforce-source-observations` and require an active
client member with `workforce.manage`.

| Operation | HTTP | MCP tool |
| --- | --- | --- |
| Record observation | `POST` collection | `bdgrz.workforce.source.record` |
| Read observation and provenance | `GET /{observation_id}` | `bdgrz.workforce.source.get` |
| List provenance | `GET` collection | `bdgrz.workforce.source.list` |
| Preview reconciliation | `GET /{observation_id}/preview` | `bdgrz.workforce.source.preview` |
| Record decision | `PUT /{observation_id}/decision` | `bdgrz.workforce.source.reconcile` |

Lists accept paired `target_kind` and `target_id` filters, `limit` (1–200), and
`cursor`. Detail reads accept `minimum_revision`. A lagging source projection
returns a transient conflict, including when a list would otherwise be empty.
Preview and decision also require caught-up correlated canonical projections.

## Authority and reconciliation

M0-D06 makes HRIS authoritative for people and employment facts, with IdP facts
corroborating it and a manual roster as the fallback. Provider observations
corroborate NHI identity facts. A preview labels that authority, returns current
and observed canonical revisions, and names differing public fields without
applying values. Restricted discrepancies are a boolean; their names and values
are not returned.

These authority labels guide an explicit decision. They do not automatically
arbitrate competing observations, select a global winning source, or overwrite a
canonical record.

A decision requires `expected_revision`, `expected_target_revision`, `outcome`
(`accepted` or `dismissed`), and an attributed note of 1–1000 characters. To accept,
the observed facts must match the current canonical record. For example, an HRIS
name that disagrees with the manual person is previewed as a conflict; the member
must deliberately revise the person, preview again, and accept against the new
revision. An IdP disagreement can instead be dismissed with a reason for
retaining the authoritative facts. A changed decision conflicts; an identical
retry succeeds and preserves original attribution.

The durable decision binds one checked canonical revision. Existing canonical
`source_kind: manual` continues to describe entry origin; the source observation
provides accepted external provenance. Single-record reads and previews expose
whether acceptance still applies to the current canonical revision. A later or
concurrent canonical change makes the earlier decision historic. This does not
claim an atomic transaction across source and canonical streams, and never grants
or revokes platform or provider access.

## Privacy and storage

Lists and previews always redact manager and employment-status reason. A detail
read requires the existing `workforce.manager_chain` and
`workforce.personal_details` field grants to disclose their respective values;
otherwise they are null with `restricted_fields_redacted: true`.

Recording or retrying a work-relationship observation, previewing it, and recording
or retrying its decision require **both** existing restricted field read grants.
This applies even when either restricted value is null: equality and conflict
responses would otherwise disclose whether guessed values match. The stored
target kind also controls retry authorization. Person and provider observation
operations retain the `workforce.manage` requirement.

Each observation owns `workforce-source-observations/{observation_id}` with
`WorkforceSourceObserved` and `WorkforceSourceReconciled` events. The per-tenant
`WorkforceSourcesV1` projector retains immutable facts, recorder attribution, and
the decision in `workforce-sources-v1`. Existing event types and canonical
projections are unchanged. Deployment adds the projector; existing tenants start
with no source observations and require no backfill. Broker and split-host
acceptance remains a separate verification gate when Docker is available.

## Frontend source review (#484)

The workforce Sources section lists immutable source identity and each decision's
checked canonical revision. Lists do not claim that an earlier acceptance applies
to the current canonical record. Each person, work relationship, and service
identity detail offers source recording with an explicit target and revision.
Source facts are editable before recording and immutable afterward. Provider
facts include credential expiry and exclude governed owner, purpose, and review
date. Recording retains the chosen observation time across explicit retries.

A source detail displays only facts disclosed by its API read, then previews the
current canonical revision and public conflict names. Accept is available only
for matching facts and requires a decision note. Conflicts link to explicit
canonical correction; dismiss retains canonical facts with an attributed reason.
A stale decision blocks both actions until the comparison is refreshed. Projection
lag offers a retry. Current versus historical accepted provenance and the original
decision actor remain visible without offering edits to a decided observation.

Source detail displays independently disclosed restricted facts. A redacted canonical detail
does not offer the full canonical replacement editor or a source form seeded from
unknown private values. Full relationship edits require all terms to be available
so a public edit cannot silently replace an unread manager or status reason with
null. This UI safeguard does not change backend write permissions.

This is the manual source-observation slice of #484. Existing roster finding
resolution and NHI expiry views remain available. HRIS parsing/import and source
shape decisions remain #347; provider account classification/correlation UI
belongs to #115/#273; integrated browser acceptance remains #453. No automatic
inter-source arbitration, import connector, account query, or access mutation is
added by this slice, so #484 remains open for those outstanding criteria.

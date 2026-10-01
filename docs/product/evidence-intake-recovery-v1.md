# Evidence intake recovery

This increment of [EN-06 backend #196](https://github.com/bdgrz/compliance/issues/196)
repairs request retry recovery in the existing internal `EvidenceIntake` service
and prevents incomplete inspection from fulfilling an evidence request. It does
not deliver the full governed artifact capability.

## Durable registration and verdict

After validating metadata and storing verified content, intake commits the
initial `EvidenceArtifactRegistered` event before invoking the inspector. A scan
exception leaves a durable `pending_inspection` artifact. Repeating the upload
of those bytes resumes inspection of that artifact. The initial metadata,
collector, capture context, and registration time remain unchanged.

Intake commits `EvidenceArtifactInspected` before applying its required storage
effect. A malware verdict requires quarantine; a secret or invalid verdict
requires purge. A failed or refused effect returns an error. The recorded
verdict remains authoritative on retry: intake reapplies the storage effect
without scanning quarantined content through an ordinary read or replacing a
rejected verdict with a later clean result. Already verified quarantine and an
already absent object are valid acknowledgements of the respective effect.

The existing `Duplicate` result identifies a pre-existing artifact. Its pending
inspection or outstanding storage effect may progress during that retry.
An available artifact retains its original inspection verdict on retry.
The never-clean default inspector continues to leave content pending.

Fitz event append uses the artifact stream's expected revision. A concurrent
verdict append conflicts before that stale operation applies a storage effect.
The event store and object store are separate resources; the durable verdict
and repeatable storage effect do not constitute a transaction between them.
Recovery here is driven by another capture request, not a background reactor.

## Evidence request fulfilment

The existing request authorizer and owner or program manager rule still apply.
Fulfilment additionally requires the canonical artifact to be `available`.
Pending, quarantined, rejected, disposed, or unknown states return `Conflict`
without changing the request's current state or revision. A relation to an evidence
request does not grant artifact delivery access.

## Remaining #196 acceptance

There is no public upload or artifact download operation in this increment.
Independent artifact authorization and durable delivery issuance, derived
lineage, production storage, the selected malware and secret scan engine,
recoverable reactors, and full R2-03 first-consumer proof remain open.

Portia Storage.Aws 0.6.2 is released, but its tagged `IObjectStorage` contract
lacks the quarantine and hold operations required by ADR0006. Its composition
requires an AWS SDK client while the ADR excludes AWS SDK types and bespoke S3
implementation from Compliance. This increment preserves the existing package
versions and development content store.

Focused unit tests cover scanner and event append failures, immutable retry
provenance, refused and interrupted storage effects, recorded verdict replay,
and concurrent inspection. Broker fixtures cover durable pending registration
and retry in fresh standalone and split API/worker scopes using the development
store. Those fixtures do not prove production S3, encryption, or a scan engine.

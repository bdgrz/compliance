# Evidence intake recovery

The internal recovery increments of
[EN-06 backend #196](https://github.com/bdgrz/compliance/issues/196) preserve
inspection progress across capture retries and recover recorded storage effects
without another upload. Incomplete inspection cannot fulfil an evidence request.
These increments do not deliver the full governed artifact capability.

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
rejected verdict with a later clean result. Already verified quarantine and
deletion reported as `Deleted` or `NotFound` under the port contract are valid
acknowledgements of the respective effect.

The existing `Duplicate` result identifies a pre-existing artifact. Its pending
inspection or outstanding storage effect may progress during that retry.
An available artifact retains its original inspection verdict on retry.
The never-clean default inspector continues to leave content pending.

Fitz event append uses the artifact stream's expected revision. A concurrent
verdict append conflicts before that stale operation applies a storage effect.
The event store and object store are separate resources; the durable verdict
and repeatable storage effect do not constitute a transaction between them.

## Recovery without another upload

[Backend child #586](https://github.com/bdgrz/compliance/issues/586) adds a
tenant-scoped reactor over recorded `EvidenceArtifactInspected` events. Its
independent `EvidenceArtifactInspectionEffectsV1` workload starts from tenant
artifact history, including verdicts committed before the workload was
registered. This is native evidence handling
for a manual upload; no optional integration or collection automation is needed.

Before a storage effect, recovery validates the concrete source stream and
trigger identity, hydrates the canonical artifact, and constructs the content
reference from its registration's tenant, digest and byte length. Capture retries
use the same reconciliation behavior. Trigger payloads do not supply storage
authority or replace the current source state.

A current malware quarantine requires the quarantine effect. A successful move,
or a verified matching quarantine after a refused move, acknowledges it. A
current secret or invalid rejection requires deletion. `Deleted` or `NotFound`
acknowledges deletion under the existing port contract; `NotFound` means no
content matching that reference and does not assert stronger physical erasure.
A `Held` deletion result, an unresolved refusal, cancellation or an exception
fails the applicable reaction. Recovery does not rescan, release quarantine,
make content available, replace a verdict, or change registration provenance.

The runner saves its checkpoint only after a successful reaction. A failed
effect remains retryable from saved progress in a fresh worker scope. An effect
that succeeded before a crash or checkpoint failure may be repeated; existing
verified quarantine and port-reported absent content permit that retry to finish.
Duplicate processing and replay retain the original artifact history.

A failed verdict blocks later events for this tenant's artifact-effects workload.
Current Portia worker defaults allow ten consecutive failed passes with backoff
capped at one minute, then propagate a workload failure to the hosted worker.
Saved progress remains retained; resumption requires resolving the cause and
recovering or restarting the worker. This increment adds no skip, dead-letter,
hold override or unlimited-retry policy.

Recovery uses the artifact's current state at the storage decision. A quarantine
release already committed before that observation makes an older malware
trigger nonapplicable. Available and pending artifacts receive no storage effect.
Malformed canonical identity or an unrecognized applicable verdict fails closed.
The source, content store and checkpoint remain separate resources: a concurrent
source transition after the observation is an explicit consistency boundary.
There is no exactly-once storage or atomic release-and-effect guarantee.

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
pending-inspection scheduling, and full R2-03 first-consumer proof remain open.

Portia 0.7.0's tagged `IObjectStorage` contract
lacks the quarantine and hold operations required by ADR0006. Its composition
requires an AWS SDK client while the ADR excludes AWS SDK types and bespoke S3
implementation from Compliance. This increment preserves the existing package
versions and development content store.

Focused unit tests cover scanner and event append failures, immutable retry
provenance, refused and interrupted storage effects, recorded verdict replay,
and concurrent inspection. Reactor units use the real Portia runner with
in-memory events and saved checkpoints to cover fresh-instance recovery,
duplicate/replayed effects, tenant isolation and stale triggers after release.
Focused and full backend unit tests, formatting/build, adversarial review and
final-head CI are the current delivery gate. Broker and consumer integration
acceptance is deferred; these units do not prove production S3, encryption,
or a scan engine.

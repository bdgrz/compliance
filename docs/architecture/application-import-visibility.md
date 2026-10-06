# Application import visibility

This implements the initial-observation commit barrier for #195 under ADR 0005 and
ADR 0007. Public personal acceptance, worker execution, source claims,
reconciliation, missing-row retirement, and rejected-item reports remain delivery
gates. The frozen row v1 contract and its digest remain unchanged.

## Authoritative state

The source ledger seals an immutable plan before target effects are written.
Pending target effects retain the raw observation and the submitter and approver
snapshots; they do not declare an application or advance its governed revision.
Manual metadata changes and retirement remain reserved until their batch settles.

`ApplicationImportCommitted` records every row's exact target event ID and physical
stream version. Commit requires durable staging, a durable plan seal, and every
durable exact target effect at its frozen governed revision. Ledger replay rejects
missing, foreign, duplicate, and mismatched proofs. A commit cannot be canceled.
An unsaved marker cannot authorize visibility or release a target reservation.

Aggregate-backed consumers resolve each pending effect against its exact source
ledger. Only durable matching proofs activate new applications at governed revision
1; linked observations preserve existing governed metadata. Name, purpose, and
owner values use manual-declaration normalization while raw observations remain
retained. Later metadata edits, owner assignments, restrictions, and retirement
survive raw replay and visibility resolution. Exact committed effect retries add
no events; missing committed effects are never recreated.

## Directory projection and reads

The application projector verifies the complete durable source marker and every
target proof before writing. A tenant projection transaction writes every new
application, its initial history, and the import visibility revision together with
the Portia checkpoint. Linked observations do not replace inventory or add governed
metadata revisions. Aborting the transaction exposes no new inventory or history.
The import directory projects committed lifecycle revision and applied progress.

The registered application read behavior captures the directory checkpoint before
handling, scans the bounded tenant backlog for unprojected commits, and checks the
checkpoint and backlog again afterward. Lag, a changed checkpoint, or an exhausted
scan returns a retryable conflict. Application inventory, history, instance,
boundary-reference, change-preview, scope, expectation, population, and coverage
queries use this behavior. Application/history/instance cursors include the import
visibility revision after the first commit; an earlier cursor requires paging to
restart. Before any import commit, existing directory cursors retain their format.

Aggregate-backed authorization, references, inventory activity, import correlation,
and previews use the same application visibility resolver. Readiness and work
queues consume their separately governed and fenced source records; initial
application imports create no instances, scope decisions, ownership, approved
boundaries, or readiness evidence. Retirement import plans will require a separate
compatible effect design and renewed consumer coverage before activation.

## Validation

Focused and full backend unit tests are the current acceptance gate. Live transport,
projection interruption, and recovery scenarios belong to the dedicated future
E2E milestone. No Docker Compose or integration execution is part of this slice.
Scenarios #810–#813 cover governed consumers, projector interruption, paging, and
cross-tenant commit rejection; they are Later/Todo with no Sprint assignment.

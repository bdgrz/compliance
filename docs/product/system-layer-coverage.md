# System layer coverage

Reconciled 2026-10-17 against `src/Compliance.Common` and `src/Compliance.Core`. The inventories are per-type: each C# type belongs in its own matching file under the owning feature directory. Presence means the source file exists; it does not mean a workflow is behaviorally complete, registered, authorized, exposed, or released.

## Current source

| Layer | Coded types | Inventory |
| --- | ---: | --- |
| Request/command contracts | 230 inventoried contracts, plus 5 new evidence contracts | [Existing commands](component-inventory.md#existing-commands) and [command list](command-list.md) |
| Query contracts | 271 (216 existing + 55 backfilled contracts) | [Existing queries](component-inventory.md#existing-queries) and [required query/view-model backfill](query-and-view-model-list.md) |
| Domain events | 236 | [Existing events](component-inventory.md#existing-events) |
| Aggregates | 68 | [Existing aggregates](component-inventory.md#existing-aggregates) |
| Models, views, and supporting contracts | 490 (436 existing + 54 response views) | [Existing models](component-inventory.md#existing-models-and-supporting-contracts) and [required query/view-model backfill](query-and-view-model-list.md) |

All source links in those existing inventories resolve to files. The command list separates existing contracts from additional candidates and internal operations. The component inventory contains the proposed future queries, events, aggregate/model candidates, reactors, and projectors.

## Remaining source types

The former broad proposal list mixed required work with hypotheses and unresolved policy scope. It is superseded as an implementation plan by the pruned, story-linked [system command list](command-list.md). Five evidence command contracts are present per file: `CaptureEvidenceArtifact`, `RecordEvidenceArtifactInspection`, `IssueArtifactDownload`, `LinkEvidenceSupport`, and `ReleaseEvidenceArtifactQuarantine`, each under `src/Compliance.Common/Features/Evidence/`. They are contracts only; handlers, persistence transitions, and API or worker registration remain outstanding as described in the command list.

The pruned plan retains 127 feature-backed command additions. Five have contract files and 122 still need request-contract types. Excluded P2 hypotheses, unsettled BO scope, and unvalidated Privacy scope are not counted as required product commands.

The proposed component inventory contains 74 query names, 107 event names, and 72 aggregate/model names that are not present in source. These are proposal-only candidates, not confirmed product requirements, so they must not be counted as missing required code. The feature backlog and accepted decisions determine which should survive the same pruning pass. A generic payload type would not constitute a usable query, view model, event, or domain model.

## Validation

The seven existing source inventories (commands, queries, events, aggregates, models, reactors, and projectors) were checked by resolving every linked `.cs` path. The new evidence command contracts compiled in the Release solution build with zero warnings and errors; formatting verification passed. Candidate counts are name-level coverage only and do not imply runtime wiring.

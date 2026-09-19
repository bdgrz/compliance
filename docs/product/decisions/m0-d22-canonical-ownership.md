# M0-D22: Ownership of shared identity and inventory concepts

Status: backend modeling decision, 2026-09-19. Decision owner: product owner and
tech lead under the delegated product-default rule. M0-D06 still owns the
organization's authoritative workforce source and NHI ownership policy.

| Concept | Owner and first delivery | Consumers and boundary |
| --- | --- | --- |
| `Application` and `SystemInstance` | Application inventory, R1-10 and its first application/system-instance slice | R2-06 access governance references an exact instance; a campaign's review inclusion does not create or own the instance. `ReviewedSystem` is a legacy name, not a second entity. |
| `Person` and `WorkRelationship` | Workforce assurance, R1-11a | R2-06 correlates observed accounts to a person and uses accepted workforce lifecycle and manager facts. It does not import a competing person roster. A platform member is a separate access relationship. |
| `ServiceIdentity` and its accountable owner relationship | Workforce assurance, R1-11c | This is the first governed NHI record. R2-06b classifies and correlates observed provider accounts; a group or role never becomes an NHI. Owner authority, purpose, and review interval remain subject to M0-D06. |
| Client `Service` | Program and scope, R1-02 | A service is a client offering or operated service system, distinct from an application and from a firm `ServiceEngagement`. Boundary versions reference a stable service ID. R1-02 may begin with an explicit unresolved service reference while the governed service register is delivered. |
| `Location` | Technology and information inventory, R1-12 | A location has a stable identity and type, such as physical site or hosting region. Boundary, components, information, flows, providers, and descriptions reference it; no context silently re-creates it. |
| Operational `Process` | Technology and information inventory, R1-12 | A process is a governed system activity or data-handling flow used in scope and description. A written `Procedure` is policy content owned by R2-02; it is not another process inventory record. |
| Incident reference before T2-07 | Risk assessment, R1-07, owns an attributable external `IncidentReference` with source ID, occurrence time, summary, and provenance | The reference can trigger reassessment but does not claim an incident workflow or operating-period evidence. T2-07 later owns the governed incident and may correlate it without rewriting earlier risk history. |
| Control-to-risk relationship | Risk assessment and treatment, R1-07 | R1-05 owns a control and its applicability. R1-07 owns the versioned assertion that a specific control version treats a specific risk, including treatment rationale and review. A control does not silently acquire risk coverage. |

The canonical entity model already distinguishes `Person`, `ServiceIdentity`,
source `Account`, and `SystemInstance`. These choices follow that model and
remove duplicate ownership. Cross-context references use tenant ID, stable
record ID, and an exact version when a decision or snapshot relies on one.
Before an owning inventory exists, a boundary may keep a typed unresolved
reference with its owner and rationale. It must not claim that an arbitrary
UUID is a governed inventory record; the backend currently rejects that claim.

No physical migration is implied by these names. Existing data and APIs, if
any, require a history-preserving adapter or migration before a rename. The
delivering stories must add authorization, provenance, reconciliation, and
cross-tenant proof when their records arrive.

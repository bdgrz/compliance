# Autonomous delivery cycle

Status: adopted for backlog triage on 2026-09-28. The [GitHub Project](https://github.com/orgs/bdgrz/projects/1) and issue bodies hold live delivery state; this document defines how to keep that state useful.

## Milestone exits

- **R1.0 — Program and access foundation:** finish scoped access grants, member deprovisioning, the Program backend baseline, and the approved boundary baseline. Each child needs its own merged acceptance proof.
- **R1.1 — Governed manual source records:** deliver the manual criteria, application, workforce, control, risk, technology, commitment, and provider records needed to describe the first program. Prove snapshots through the workforce consumer. Real client samples inform content but do not block the manual data shape.
- **R1.2 — Integrated readiness workflow:** connect the source records to an accessible program journey, governed scope and review, reproducible readiness assessment, and owned gap plan. Imported data and optional connectors do not block the manual path. Readiness is a management decision, never an auditor opinion.
- **R2, T1, T2, T3, F1:** retain their published product exits. Their open issues are planned work, not current iteration work.

M0 design decisions are accepted and its milestone is closed. A later fact-gathering issue remains in Discovery until the named customer, advisor, or auditor evidence exists. A milestone does not imply that every issue with its old story prefix must finish in the same PR.

## Project fields

Every open issue belongs to the project and has a priority and one **Delivery queue** value:

| Queue | Meaning | Selection rule |
| --- | --- | --- |
| Active | One capability bundle is being implemented or verified. | Finish its acceptance and PR lifecycle before starting another. |
| Ready | An implementation issue with known inputs and no open prerequisite. | Select the lowest Run order after Active completes. |
| Blocked | A current R1 implementation issue waiting on a named upstream issue or external platform capability. | Move to Ready only after checking the actual dependency and remaining acceptance. |
| Later | Approved future-stage backend work or frontend work parked during the backend-first pass. | Re-triage when its consuming workflow enters the near horizon. |
| Discovery | A real-world fact, professional decision, measurement, or unvalidated P2 hypothesis. | Record who or what supplies the evidence; do not treat it as implementation-ready. |
| Outcome | Product or enabler parent that tracks integrated acceptance. | Keep open until its children and end-to-end outcome pass. |

**Status** is Todo, In Progress, or Done. Only the Active capability is In Progress. **Run order** is a short, dependency-aware lookahead, not a fixed roadmap; update it after each merge or material discovery. Priority is urgency for the product release, not permission to bypass a blocker.

## One delivery loop

1. Refresh the issue, its dependencies and comments, open PRs, the current branch, and the project fields. Preserve unrelated worktrees and uncommitted changes. If a dependency is already closed, inspect its acceptance evidence before removing the edge.
2. Pick one Ready capability. Before coding, state its user or operator outcome, exact acceptance, upstream contract, non-goals, and which existing children the PR can close. If an issue only describes a test or technical layer, fold its proof into the owning capability issue. Split an issue only to break a real dependency cycle or make an independently useful result closable.
3. Write a focused failing behavior test, implement the domain and authorized HTTP/MCP path, and prove tenant isolation, replay, lag/recovery, and standalone/split-host behavior where applicable. Personal proofs, attestations, approvals, and sign-offs remain HTTP-only.
4. Run the focused gate during development, then the full applicable local gate once the bundle is reviewable. Open a PR to `develop`, review the exact head, fix findings, and require the repository checks and required native AOT qualification before squash merge.
5. Read back the merged commit and each linked issue. Close only acceptance that actually passed. Record remaining gaps on partial issues, update the product parent if the integrated outcome advanced, move completed project items to Done, and promote the next unblocked issue to Ready. Keep Active to one bundle.

Jev can classify issue shape, spot likely blockers, and challenge a proposed bundle. Its probabilities are triage signals. GitHub issue state, product decisions, code, tests, and exact-head CI determine closure and readiness. Recheck Jev disagreements against those sources; do not turn a model judgment into an automatic issue closure.

## Backlog hygiene

- Do not require a full future engagement, connector, or artifact workflow to close a useful manual baseline. Put the later acceptance on its owning issue and keep incomplete contexts explicit and fail closed.
- Do not leave a circular dependency between a shared primitive and its first consumer. Implement and prove them in one capability bundle when their acceptance is inseparable.
- Keep issue bodies current when scope moves. Link the receiving issue and state what evidence remains. Close a superseded tracking issue as superseded, without claiming its test or product behavior has shipped.
- Review the six queue counts and the number of open issues after each capability merge. A successful PR should retire an accepted backlog slice or name the exact unmet gate; PR count alone is not progress.

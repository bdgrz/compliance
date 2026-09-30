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

### Jev-assisted triage

`scripts/backlog-jev.mjs` runs these judgments over the live backlog. The script owns the dependency graph, thresholds, and ranking; Jev answers only the semantic questions. It reads GitHub and never edits it. Load the TypeSafe key only into the command's environment:

```sh
TYPESAFE_API_KEY="$(tr -d '[:space:]' < ~/.config/typesafe/jev.key)" node scripts/backlog-jev.mjs triage --milestone R1
TYPESAFE_API_KEY="$(tr -d '[:space:]' < ~/.config/typesafe/jev.key)" node scripts/backlog-jev.mjs criteria 209 211
TYPESAFE_API_KEY="$(tr -d '[:space:]' < ~/.config/typesafe/jev.key)" node scripts/backlog-jev.mjs preflight 437
```

- Run `triage` in step 5 after each merge. It reports Blocked issues whose recorded blockers are all closed, a Ready ranking by release value and downstream fanout, issues to split before starting, possibly satisfied issues, bundle candidates, parents whose children are all closed, and whether Discovery issues block the manual path.
- Promote to Ready only when every recorded blocker is closed and the unrecorded-blocker probability is below 0.5. At 0.5 or above, read the cited text and either record the missing dependency or clear the flag.
- Split an issue before starting it when Jev classifies it `split_needed` with confidence of at least 0.7. Each part must be independently closable.
- Before closing an issue, or when it has absorbed partial PRs, run `criteria`. It extracts acceptance criteria from the issue body and judges each against merged PR descriptions and progress comments. Use its remaining list for the gap comment or the split; a `close candidate` verdict still requires reading the linked evidence.
- Before opening a PR, run `preflight <issue>` on the branch. Path rules pick the client and .NET gates, and Jev decides whether C# hunks need `Category=BrokerIntegration` tests (`check-backend.sh full`). For each acceptance criterion it names the one changed test that proves it, or `none`; read each match before relying on it. A `none` on a compound criterion that several tests prove together is expected; a `none` on a single claim is a coverage gap to close before pushing. It also flags changed files that may not serve the issue; confirm a flag before moving work out, because borderline files can change verdict between runs.
- When a UI needs data or an action the API lacks, run `api-gap "<need> :: <current client workaround>"` instead of building a lasting client-side workaround. Jev names the owning operation, judges from its real response fields whether it already serves the need, and recommends whether an idiomatic REST API should add a subresource collection, query filter, representation field, or command. Record each recommended change as a backend issue with the evidence; keep a client workaround only until that issue lands.

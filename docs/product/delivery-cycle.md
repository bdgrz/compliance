# Autonomous delivery cycle

Status: adopted for backlog triage on 2026-09-28, reconciled with accepted
product direction on 2026-10-04. The [GitHub Project](https://github.com/orgs/bdgrz/projects/1)
and issue bodies hold live priorities, dependencies, queues, and completion;
this document defines exits and the execution rules that keep that state useful.
The [product brief](product-brief.md) defines business outcomes, and
[triage](triage.md) records their dependency rationale rather than a live work
queue.

## Milestone exits

- **R1.0 — Program and access foundation:** scoped access grants, member deprovisioning, the Program backend baseline, and the approved boundary baseline work with attributable history. Each child needs its own merged acceptance proof.
- **R1.1 — Governed manual source records:** deliver the manual criteria, application, workforce, control, risk, technology, commitment, and provider records needed to describe the first program. Prove snapshots through the workforce consumer. Real client samples inform content but do not block the manual data shape.
- **R1.2 — Integrated readiness workflow:** connect the source records to an accessible program journey, governed scope and review, reproducible readiness assessment, and owned gap plan. Imported data and optional connectors do not block the manual path. Readiness is a management decision, never an auditor opinion.
- **R2, T1, T2, T3, F1:** retain their published product exits. Their issue prefixes describe product horizons; current queue fields and verified prerequisites determine whether a specific capability is selected for delivery.

Every milestone inherits the [manual-first product contract](backlog.md#manual-first-product-contract):
users can complete its primary workflows without external integrations or
optional source-collection automation. Later automation may reduce setup,
maintenance, or operating effort, but it does not become a prerequisite for an
existing feature.

Native validation, calculations, projections, review rules, and workflow
transitions continue to work on manually supplied records. Optional automation
uses the same governed acceptance and review rules; disabling it cannot remove
the user's equivalent operation.

M0 design decisions are the accepted baseline. A later fact-gathering issue
remains in Discovery until the named customer, advisor, or auditor evidence
exists. It gates only its recorded consuming acceptance, rather than reopening
all of M0. A milestone does not imply that every issue with its old story
prefix must finish in the same PR.

Periodic policy, risk, provider, and management reviews use ordinary recurring
control occurrences, frozen evidence, attributed sign-off, and findings for
requested corrective action ([M0-D19](decisions/m0-d19-periodic-reviews.md)).
Attest collaboration includes requests, responses, populations, samples, and
delivered evidence; workpapers and auditor test conclusions remain in audit
software ([M0-D27](decisions/m0-d27-attest-scope.md)). Closing an attest
engagement or offboarding its organization requires its record package and an
attributed delivery decision before the engagement hold can clear.

## Organization and commercial delivery gates

One verified account can create and manage several organizations and delegate
organization administration to client personnel. Each organization keeps its
own data boundary and hosted billing configuration. These core responsibilities
use explicit organization grants; professional Advisor/Attest work continues
to require accepted service-engagement assignments and independence checks.

The [business operations scope](business-operations.md) supplies three
actionable outcomes alongside the SOC 2 journey:

- **BO-01 — Delegated organization management:** extend the R1-04/R1-15
  organization foundations with clear delegation and handover while preserving
  authorization, attribution, and the organization's records.
- **BO-02 — Hosted organization billing:** complete billing acceptance before
  onboarding a paying SaaS organization. Self-hosting is free; the hosted base
  fee applies to each organization, and the client or authorized MSP manages
  its billing.
- **BO-03 — Organization add-ons:** complete selection, authority, entitlement,
  and billing acceptance before selling the respective integration, service,
  or additional automation.

These scope IDs are documentation groupings. Their owning issues must record
the required policies, acceptance, dependencies, and selected delivery queue;
the IDs do not create a new live milestone. The complete manual SOC 2 workflow
has its product acceptance gates, while paid SaaS has its additional commercial
gates. Free self-hosted use does not require a hosted billing workflow.

## Project fields

Every open issue belongs to the project and has a priority and one **Delivery queue** value:

| Queue | Meaning | Selection rule |
| --- | --- | --- |
| Active | An independent capability bundle is being implemented or verified in its own branch/worktree. Multiple bundles may be Active concurrently. | Keep each lane isolated; start another Ready bundle when a lane has capacity and its dependencies are closed. |
| Ready | An implementation issue with known inputs and no open prerequisite. | Select the lowest Run order when a lane opens. |
| Blocked | An implementation issue waiting on a named upstream issue or external platform capability. | Move to Ready only after checking the actual dependency and remaining acceptance. |
| Later | Approved future-stage backend work or frontend work parked during the backend-first pass. | Re-triage when its consuming workflow enters the near horizon. |
| Discovery | A real-world fact, professional decision, measurement, or unvalidated P2 hypothesis. | Record who or what supplies the evidence; do not treat it as implementation-ready. |
| Outcome | Product or enabler parent that tracks the complete user outcome across owning repositories. | Keep open until its children are complete; end-to-end validation belongs to a later phase. |

**Status** is Todo, In Progress, or Done. Every Active capability is In Progress; other queues remain Todo until their work starts. **Run order** is a short, dependency-aware launch order sized to keep available lanes fed, not a fixed roadmap; update it after each merge or material discovery. Priority is urgency for the product release, not permission to bypass a blocker.

## One delivery loop

1. Refresh the issue, its dependencies and comments, open PRs, the current branch, and the project fields. Preserve unrelated worktrees and uncommitted changes. Give each Active lane its own branch/worktree. If a dependency is already closed, inspect its acceptance evidence before removing the edge.
2. Fill available lanes from Ready in Run order. Before coding, state each bundle's user or operator outcome, exact acceptance, upstream contract, non-goals, and every existing child the PR can close. Keep related backend, web, iOS, and Android issues under the same product outcome when they share a contract, but deliver code through separate PRs in each owning repository. If an issue only describes a test or technical layer, fold its proof into the owning capability issue. Split an issue only to break a real dependency cycle or make an independently useful result closable.
3. Follow the same TDD loop in backend and client repositories: write a focused failing unit test for the behavior, implement the smallest change, and get that test green. Personal proofs, attestations, approvals, and sign-offs remain HTTP-only.
4. Run the focused test and full unit-test suite before opening a PR. Adversarially review the exact PR head, refine or refactor in response to findings, and rerun the focused test and unit-test suite. Require repository CI on the final head before squash merge. End-to-end validation is deferred until backend and frontend features work independently.
5. Read back each merged commit and every linked issue. Close only acceptance that actually passed. Record remaining gaps on partial issues, update the product parent if the integrated outcome advanced, move completed project items to Done, and refill open lanes from the next unblocked Ready items.

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
- Before opening a PR, run `preflight <issue>` on the branch. Path rules identify relevant repository checks; Jev's test judgments are review signals and never expand the current fast loop beyond focused and full unit tests. For each acceptance criterion, preflight names the one changed unit test that proves it, or `none`; read each match before relying on it. A `none` on a compound criterion that several tests prove together is expected; a `none` on a single claim is a coverage gap to close before pushing. It also flags changed files that may not serve the issue; confirm a flag before moving work out, because borderline files can change verdict between runs.
- When a UI needs data or an action the API lacks, run `api-gap "<need> :: <current client workaround>"` instead of building a lasting client-side workaround. Jev names the owning operation, judges from its real response fields whether it already serves the need, and recommends whether an idiomatic REST API should add a subresource collection, query filter, representation field, or command. Record each recommended change as a backend issue with the evidence; keep a client workaround only until that issue lands.

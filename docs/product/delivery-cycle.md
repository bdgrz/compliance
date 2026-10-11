# Autonomous delivery cycle

Status: adopted 2026-09-28; execution policy reconciled 2026-10-11.
[Project 2 — Incremental product delivery](https://github.com/orgs/bdgrz/projects/2)
and linked issues hold current sprint scope, dependencies and acceptance.
[Legacy Project 1](https://github.com/orgs/bdgrz/projects/1) preserves the broader
backlog and its acceptance; it is not the active execution queue. The
[product brief](product-brief.md) and [triage](triage.md) retain product direction
and dependency rationale. [Sprint planning](sprint-planning.md) defines the
current commitments, forecasts and measurement baseline.

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

## Project fields and selection

Project 2 separates **Level** (Epic, Story or Task), **Commitment** (Committed,
Stretch, Forecast or Unscheduled), **Readiness**, relative **Size**, and remaining
work **Mix**. The field meanings are defined in [sprint planning](sprint-planning.md#fields-and-sizing). Status is Todo, In Progress or Done; Sprint and Order express the
current sequence. A date or high priority does not make an item ready.

- A story describes one observable user outcome. Its tasks may belong to separate repositories; tasks and epics do not count as completed stories.
- **Ready** means acceptance and inputs are known, dependencies are accepted, and one small first task can be selected. **Sequenced** names a predecessor that must finish first. Needs refinement and blocked items retain their exact missing scope or input; they cannot fill an active slot or ready stretch slot.
- Committed stories define the selected sprint scope. Stretch can be promoted only after every original commitment is accepted. Forecasts remain conditional and can move after refinement; no unfinished item carries over automatically.
- Backend, web and native clients own their code and tests. Link API gaps and client consumers; keep one owning repository per PR. Working implementation needs acceptance evidence rather than rebuilding.

## One bounded delivery loop

1. Read the live issue, direct dependencies, relevant acceptance evidence, open PRs and checkout state. Keep one clean checkout per repository and WIP = 1 across backend and clients. Preserve unrelated local work; do not create worktrees or additional implementation lanes.
2. Architect, coder and reviewer work sequentially on the same bounded task. Before In Progress, record the observable gap, owning issue/repository, allowed files and change, exclusions, direct dependencies, focused acceptance check and stopping point. Read only the contracts, files and tests needed for that brief.
3. Cap discovery at 20 minutes. A second workflow, another repository, unplanned contract change, unrelated refactor or unresolved scope requires a stop and re-scope before implementation. Record unrelated findings in the backlog. A dependency gates only the acceptance it directly blocks; do not follow its entire epic.
4. For a behavior change, write and run a focused failing unit test, make the smallest change that passes it, then run the owning repository's full unit suite and applicable format/build checks. Evidence-only or documentation tasks do not manufacture a failing test. Check their actual evidence, links and consistency instead. Personal proofs, decisions and sign-offs retain their existing HTTP-only rules.
5. Open one focused PR. Review the immutable head for contract, authority, tenant isolation, replay and failure gaps. Resolve findings, rerun affected checks and repeat review after changes. Require hosted CI on the final head, then squash merge and read back the merged commit and issue acceptance.
6. Close only proved acceptance; leave remaining gaps explicit. Update the story and project fields, stop at the task boundary, and hand off in at most 10 lines: outcome, commit, evidence, remaining acceptance and next action. Select the next ready task only after that readback.

Ordinary issue/topic PRs target the owning repository's `develop` and deliver a
functional slice or keep unfinished behavior behind a default-off flag. A flag
does not bypass authorization or count as a completed user outcome. A genuinely
multi-story feature may use `feat/<feature>` only after fixing its included
stories, acceptance and exit; review/check both child PRs and the final feature
PR. Sprints need no branches or aggregate PRs.

Current backend/client acceptance uses focused and full unit evidence. A directly
required configured integration check can supplement that evidence, as with
actual delivered-invitation acceptance. Broad browser/E2E, native-client and
release validation remain separately scoped; do not silently make them gates
for unrelated implementation or count them as completed.

## Jev-assisted decisions

Use TypeSafe System One's Jev model for bounded triage, criteria and PR-preflight
questions when it can resolve scope or ordering quickly. Supply the actual issue,
accepted decisions and relevant evidence. Load the API key only into the command
environment from `~/.config/typesafe/jev.key`; never print it or copy it to a file.

The former `scripts/backlog-jev.mjs` wrapper is absent from this repository.
Use the direct API workflow rather than documenting commands that cannot run;
do not rebuild tooling inside a feature task. Jev does not mutate GitHub. Record
decisions and dependencies with `gh` and read them back.

Jev probabilities are advisory review signals. GitHub issues, accepted product
decisions, code, tests, independent review and exact-head CI govern readiness
and closure. Inspect disagreements against those sources. A model confidence
threshold does not authorize closure, a new dependency chain or expanded scope.

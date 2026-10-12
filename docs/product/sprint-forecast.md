# Remaining-backlog sprint forecast

Planning record: [#974](https://github.com/bdgrz/compliance/issues/974), October 12, 2026.
[Project 2](https://github.com/orgs/bdgrz/projects/2) is the live execution queue.
The [complete issue map](sprint-forecast.csv) accounts for all **337 original open issues**
(328 backend, 9 web), plus planning #974, the two inspected web pilot tasks #56/#57,
and import-retention prerequisite #975: **341 rows**, with no duplicate issue identities.

## Commitment and forecast

Only [web #56](https://github.com/bdgrz/compliance-web/issues/56) and
[web #57](https://github.com/bdgrz/compliance-web/issues/57) are the new delivery-pilot
commitments. Both are S / Verification tasks with exact files, commands and stops.
The current planning task #974 is tracked separately and adds no product throughput.
No application code is selected by this organization pass.

The other **307 leaf rows are Forecast**; **31 multi-sprint rollups** stay on the
roadmap without an independent Sprint. There are 85 candidate Story rows and 225
Task rows, including the planning/pilot/prerequisite tasks. Those are planning roles,
not 85 newly scoped or estimated features. Existing backend tasks and product parents
are not two delivered outcomes. Broad E2E/browser work comprises 155 original rows
and is excluded from Story throughput.

Each dated forecast is an ordered goal/scope bucket, **not a promise that every row
will finish in those 14 days**. Later buckets contain unrefined legacy acceptance
and must be split or resized before commitment. Keep the next selection small:
at most three comparable outcomes, with roughly 20% allowance for findings/rework.
Expand only after measured delivery supports it. Conditional features can be dropped
from a commitment without erasing their backlog acceptance.

The first six iteration IDs, dates and accepted-item assignments are preserved.
Their goal names are refreshed for remaining work before Sprint 1 starts; original
goals and acceptance stay in the [historical baseline](sprint-planning.md#calendar-and-accepted-scope).
Accepted-early work does not reserve future capacity or supply a calendar velocity sample.
All iterations last 14 days, Wednesday through the second Tuesday. These dates are
calendar placeholders for an uncalibrated rolling forecast, **not a project completion ETA**.

## Ordered goals

Numbers below count remaining leaf rows only; parent rollups are listed separately.
Proof and external-input counts are context, not additional feature commitments.

| Sprint | Calendar | Goal | Candidate Stories | Supporting/input/proof tasks |
| --- | --- | --- | ---: | ---: |
| 1 | 2026-10-14–2026-10-27 | Manage member access | 3 | 3 |
| 2 | 2026-10-28–2026-11-10 | Govern systems and relationships | 6 | 1 |
| 3 | 2026-11-11–2026-11-24 | Approve program scope | 4 | 5 |
| 4 | 2026-11-25–2026-12-08 | Govern criteria, controls and risks | 5 | 2 |
| 5 | 2026-12-09–2026-12-22 | Attach governed artifacts | 1 | 2 |
| 6 | 2026-12-23–2027-01-05 | Import applications safely | 3 | 20 |
| 7 | 2027-01-06–2027-01-19 | Import existing readiness work | 5 | 11 |
| 8 | 2027-01-20–2027-02-02 | Complete readiness and validate R1 | 3 | 119 |
| 9 | 2027-02-03–2027-02-16 | Own recurring work and policies | 6 | 2 |
| 10 | 2027-02-17–2027-03-02 | Capture evidence and perform controls | 2 | 6 |
| 11 | 2027-03-03–2027-03-16 | Evaluate controls and resolve findings | 5 | 0 |
| 12 | 2027-03-17–2027-03-30 | Review system access | 5 | 2 |
| 13 | 2027-03-31–2027-04-13 | Accept professional engagements | 4 | 3 |
| 14 | 2027-04-14–2027-04-27 | Govern evidence and enter Type I | 2 | 11 |
| 15 | 2027-04-28–2027-05-11 | Prepare the Type I handoff | 5 | 8 |
| 16 | 2027-05-12–2027-05-25 | Deliver the Type I package | 3 | 3 |
| 17 | 2027-05-26–2027-06-08 | Start the observation period | 2 | 2 |
| 18 | 2027-06-09–2027-06-22 | Operate and review the period | 4 | 4 |
| 19 | 2027-06-23–2027-07-06 | Freeze the period and answer samples | 3 | 3 |
| 20 | 2027-07-07–2027-07-20 | Complete the examination and continue | 4 | 4 |
| 21 | 2027-07-21–2027-08-03 | Navigate the client portfolio | 4 | 3 |
| 22 | 2027-08-04–2027-08-17 | Reuse setup and export client records | 1 | 4 |
| 23 | 2027-08-18–2027-08-31 | Offboard without losing held records | 0 | 2 |
| 24 | 2027-09-01–2027-09-14 | Delegate management and hosted billing | 2 | 3 |
| 25 | 2027-09-15–2027-09-28 | Manage organization add-ons | 1 | 1 |
| 26 | 2027-09-29–2027-10-12 | Automate a selected cloud source | 1 | 1 |
| 27 | 2027-10-13–2027-10-26 | Collect a private source through an agent | 1 | 0 |

## First bounded actions

1. Finish this planning record and read back the board.
2. Execute web #56 alone: map/run existing organization/program grant and access tests;
   record the exact client subset proved or the first missing criterion. No new UI,
   no backend-authority claim from API stubs, and no #20/#94 closure.
3. Execute web #57 alone: map/run existing suspension/reinstatement and open-work feedback
   tests; stop at the evidence or first gap. No deprovision/reassignment scope or #96 closure.
4. Reconcile those results, then select one real gap or existing acceptance check.
   Prefer a bounded #464 relationship list/add/remove slice for the next inventory
   feature after its backend contract check, followed by its separate web consumer.

Do not re-run the previously exhausted #952 investigation as the default next task.
Its canonical uniqueness/payload-binding problem remains L - split / Needs refinement;
#953 also needs refinement after #952 is accepted. Their Sprint 2 placement reserves
a refinement horizon, not implementation readiness or a relaxed identity contract.

## Dependencies and split boundaries

- **Manual first:** imports follow equivalent usable manual records. Customer source
  samples gate their exact format/content claims, not the manual application data shape.
- **Readiness:** complete #489/#14/#97 and web #28 forecast to Sprint 8, after artifact
  and relevant import contracts. Sprint 4 covers bounded criteria/control/risk work.
- **Artifacts:** #196 is Blocked on the required Portia-owned storage operations; public
  upload, independent download authority, inspector and production storage must become
  separate bounded tasks. No bespoke S3 adapter or storage redesign joins a feature.
- **Retained import bytes:** Sprint 5 #975 is the bounded #285 enforcement prerequisite
  before full #195 acceptance in Sprint 6. #831 metadata does not prove seven-year
  byte retention or active-hold enforcement. If #975 is unfinished, reselect #195 full
  acceptance to the Sprint 14 governance horizon; do not claim completion or carry it
  silently. #91/#908 retain the wider Sprint 6–14 range.
- **Delivered imports:** #195 already records initial-observation acceptance/recovery
  (#826) and reconciliation previews (#816/#818/#837). Reconcile stale #213/web #21
  descriptions before choosing code; do not rebuild accepted whole-batch behavior.
- **Professional work:** designated authority and engagement acceptance (#269) precede
  runtime walls and advisory consumers. #135/#277 whole acceptance forecasts to
  Sprint 15 because actual auditor-request consumers (#300) are required; Sprint 13
  retains earlier engagement/advisor work. Avoid a #269↔#277 activation cycle.
- **Actual inputs:** #338/#347/#348/#351/#352/#354/#355/#356, provider exports #353,
  audit formats #339, ratification #343, retention #346, and usage/connector measurements
  #340/#342 retain named-owner evidence requirements. Jev supplies reversible product
  defaults; it cannot fabricate client facts, legal rights, professional authority or
  deployment approval. #349/#492/#642–#645 are bounded product decisions, not blanket
  permission to construct the app. IT owns production SMTP and digest-failure operations.
- **Optional scope:** direct auditor access (#26/#547), capacity planning (#130), cloud
  collection (#137/#342), and private agents (#138) remain conditional. Governed packages
  and equivalent manual workflows remain usable without them.
- **Type II:** Sprints 17–20 forecast application capabilities. Actual observation-period
  completion waits for the approved dates, occurrences and real evidence. Coding a close
  command does not complete a real SOC 2 observation period or examination.

The Planning note field on every mapped issue records its direct boundary. Before
activation, reconcile current acceptance, accepted children and contracts; create or
select one owning-repository task for a real gap. R2 parents with delivered backend
children often need a web-owned task or evidence reconciliation, not a new backend.

## Validation tranches

Sprint 8 contains **115 configured proof rows** across platform, identity, organization,
program/scope, applications, workforce, controls/risks and providers, plus #468 rollout, #489 readiness, #948 fixture diagnosis and web #24. It is a conditional validation inventory, not a commitment
to run the entire matrix in that sprint. Activate one coherent cluster at a time; split
the bucket when its bounded scope exceeds observed capacity. Each declared standalone/
split-host, tenant, replay and failure case remains preserved in its issue.

Import checks are divided into staging, correlation and frozen plans (Sprint 6),
then pending effects and commit visibility (Sprint 7). Artifact/intake configured checks
follow the first evidence consumer (Sprint 10); policy/operation/evaluation/access checks
follow their implementations (Sprint 14). Passes do not replace missing feature acceptance.
Legacy SystemInstance checks cannot prove #952/#953 canonical identity or editing.
Inspect skips, logs and cleanup as well as pass totals.

These 155 existing validation issues do not constitute complete T1/T2/T3 or professional
workflow validation. Derive those exit checks from the actual accepted capabilities; do
not invent a fixed final validation scope now. Backend/web unit acceptance remains the
current phase; broad browser checks stay web-owned and separately selected.

## Existing PR placement

| Open PR | Consuming forecast | Required next step |
| --- | --- | --- |
| [backend #648](https://github.com/bdgrz/compliance/pull/648) | Sprint 8, #649 manager invariant | Reconcile draft host-mode proof and exact-head checks; not a gate for unrelated manual features. |
| [backend #899](https://github.com/bdgrz/compliance/pull/899) | Sprint 13, #269 engagement construction | Preserve old sprint stack; reconcile onto authorized base before exact-head review. Real #343 inputs remain production evidence. |
| [backend #898](https://github.com/bdgrz/compliance/pull/898) | Sprints 13–15, #277 independence/consumers | Preserve partial work; reconcile #269 predecessor and actual #275/#300 consumers. |
| [backend #931](https://github.com/bdgrz/compliance/pull/931) | Sprint 1 maintenance, excluded from product throughput | Independently review test-runner compatibility and CI on develop. |

No PR was retargeted, merged or discarded by this planning pass. Ordinary issue PRs
target the owning repository's develop branch; no new sprint branch is created.

## Roadmap rollups

No independent Sprint is assigned to these coordinators. Their child windows are
visible in Planning note and the complete CSV. They never add accepted-Story throughput.

| Rollup | Conditional child window |
| --- | --- |
| [#10](https://github.com/bdgrz/compliance/issues/10) [R1-04] Invite the compliance team and assign responsibilities | Sprints 1–4,8,13 |
| [#19](https://github.com/bdgrz/compliance/issues/19) [R2-05] Review control design, implementation, and evidence | Sprints 11 |
| [#20](https://github.com/bdgrz/compliance/issues/20) [R2-06] Reconcile application access and complete the initial review | Sprints 12 |
| [#38](https://github.com/bdgrz/compliance/issues/38) [T2-08] Automate selected inventory, access, and evidence collection | Sprints 26–27 |
| [#47](https://github.com/bdgrz/compliance/issues/47) [R1-09] Bring existing readiness work into the program | Sprints 6–7 |
| [#48](https://github.com/bdgrz/compliance/issues/48) [R1-10] Establish the application inventory and review scope | Sprints 2–8 |
| [#54](https://github.com/bdgrz/compliance/issues/54) [R2-11] Manage accountable compliance work | Sprints 9 |
| [#91](https://github.com/bdgrz/compliance/issues/91) [EN-05] Import with preview, reconciliation, and safe replay | Sprints 6–14 |
| [#92](https://github.com/bdgrz/compliance/issues/92) [EN-06] Store governed artifacts with immutable content identity | Sprints 5–10 |
| [#128](https://github.com/bdgrz/compliance/issues/128) [F1-01] Onboard, suspend, and offboard client organizations | Sprints 22–23 |
| [#492](https://github.com/bdgrz/compliance/issues/492) [Decisions] Product decisions and external inputs blocking backlog work | Sprints 1–27 |
| [#629](https://github.com/bdgrz/compliance/issues/629) [EPIC-01] Start and assess a SOC 2 readiness program | Sprints 1–8 |
| [#630](https://github.com/bdgrz/compliance/issues/630) [EPIC-02] Establish organization scope and governed source records | Sprints 1–8 |
| [#631](https://github.com/bdgrz/compliance/issues/631) [EPIC-03] Operate controls and resolve readiness gaps | Sprints 9–14 |
| [#632](https://github.com/bdgrz/compliance/issues/632) [EPIC-04] Prepare for and hand off a SOC 2 Type I examination | Sprints 15–16 |
| [#633](https://github.com/bdgrz/compliance/issues/633) [EPIC-05] Operate a SOC 2 Type II observation period | Sprints 17–18 |
| [#634](https://github.com/bdgrz/compliance/issues/634) [EPIC-06] Complete the Type II examination and continue the program | Sprints 19–20 |
| [#635](https://github.com/bdgrz/compliance/issues/635) [EPIC-07] Serve multiple clients with advisory and attest work | Sprints 13–23 |
| [#636](https://github.com/bdgrz/compliance/issues/636) [EPIC-08] Maintain trusted platform and delivery foundations | Sprints 1–14; ongoing platform maintenance |
| [#637](https://github.com/bdgrz/compliance/issues/637) [EPIC-09] Resolve product decisions and validate assumptions | Sprints 1–27; named decisions/facts only |
| [#638](https://github.com/bdgrz/compliance/issues/638) [EPIC-10] Delegate organization management and establish commercial operations | Sprints 24–25 |
| [#901](https://github.com/bdgrz/compliance/issues/901) [Epic] Publish an accountable, independently reviewed control | Sprints 1–4 |
| [#902](https://github.com/bdgrz/compliance/issues/902) [Epic] Describe my program and the systems it covers | Sprints 2–8 |
| [#903](https://github.com/bdgrz/compliance/issues/903) [Epic] Govern policies and make attributable risk decisions | Sprints 4–11 |
| [#904](https://github.com/bdgrz/compliance/issues/904) [Epic] Run compliance work and retain its evidence | Sprints 9–14 |
| [#905](https://github.com/bdgrz/compliance/issues/905) [Epic] Assess readiness and own a concrete gap plan | Sprints 4,8,14 |
| [#906](https://github.com/bdgrz/compliance/issues/906) [Epic] Prepare and deliver examination evidence | Sprints 15–20 |
| [#907](https://github.com/bdgrz/compliance/issues/907) [Epic] Work with professional teams under explicit engagement boundaries | Sprints 13–23 |
| [#908](https://github.com/bdgrz/compliance/issues/908) [Epic] Import source records with a trustworthy preview and safe replay | Sprints 6–14 |
| [#909](https://github.com/bdgrz/compliance/issues/909) [Epic] Administer and operate several organizations | Sprints 1,21–25 |
| [#928](https://github.com/bdgrz/compliance/issues/928) [Epic] Deliver manual Application and canonical SystemInstance records | Sprints 2 |

## Replanning and audit

At every actual Tuesday close, record accepted original commitments, measured work
and elapsed time, blocked/review/rework time (or unknown), scope changes and explicit
carryover decisions. After two or three comparable 14-day cycles, recalibrate future
bucket sizes and dates from accepted outcome throughput. Forecasts may compress, split
or move; do not divide 337 mixed issues by a PR rate or present the last calendar date
as a completion estimate.

Keep WIP = 1 across repositories, sequential architect → coder → reviewer, one clean
checkout per repo, exact-head review/CI, squash merge and readback. Discovery is capped
at 20 minutes. A second workflow, unplanned contract or another repo triggers re-scope.
Stop when the bounded acceptance is proved; unrelated discoveries return to the backlog.

Jev jev-1.13.0 supported reuse of upcoming dates, conditional forecast assignment,
validation by consuming horizon and named-consumer external gates. Its second check
supported publication only with explicit uncalibrated capacity and no ETA claim.
Independent reviews corrected #953 readiness, #135/#277 completion ordering, readiness
after artifacts/imports, and actual-byte retention before full import acceptance.
Live issue text, code/contracts, unit tests, review and CI remain authoritative.

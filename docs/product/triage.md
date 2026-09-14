# Product backlog triage

Status: working product-owner review, updated 2026-09-14

This review prevents the backlog from becoming a catalog of plausible features. A story may describe a sensible capability and still be the wrong thing to build now.

The [shared domain model](domain-model.md) and each story's domain slice and
implementation subtasks are delivery gates, not separate backlog features.
They ensure that platform access, responsibilities, controls, evidence,
reviews, readiness, and engagement snapshots form one process rather than
parallel subsystems.

The rationale for added, refined, and intentionally deferred capabilities is
recorded in the dated [SOC 2 product gap analysis](gap-analysis.md).

The 2026-09-14 backlog design review found that the P0 set could not meet the
R1 and R2 milestone exits, that half of the P0 stories still embedded
unresolved validation, and that several shared primitives had no owner or two
owners. The backlog therefore now has an **M0 - Design and discovery**
milestone of discovery and architecture-decision issues, six approved shared
enablers in R1, and outcome-level delivery slices for the largest P0 stories.
Their contract is defined in [backlog.md](backlog.md#product-backlog-contract).

## Evidence levels

- Confirmed: directly supported by the target customer statement in this product conversation.
- Derived: logically supports the stated readiness to Type I to Type II journey, but its detailed workflow must be validated before implementation.
- Hypothesis: plausible convenience or expansion. Do not schedule it until customer use, advisor feedback, auditor feedback, or measured operating cost supplies evidence.

Confirmed at this point:

- the target is a small compliance team;
- the team is currently in SOC 2 readiness with consultants and expects Type I next;
- the product must carry one program from readiness through SOC 2 Type I and Type II;
- controls, policies, evidence, and access reviews are core jobs;
- an owned application inventory and actual-versus-expected review of human and NHI access are core jobs;
- an authoritative human roster and accountable NHI ownership are required to
  make that review meaningful;
- the team needs one accountable view of what must happen next rather than
  another task spreadsheet;
- existing readiness material must be adopted without starting over;
- consultant review and feedback are part of the current workflow;
- the backlog must express business objectives, requirements, and acceptance criteria as API-to-UI user stories.

Everything more specific is either derived or hypothetical until validated.

## Priority policy

- P0 - prove now: required for the first product release to complete readiness and make a Type I entry decision.
- P1 - build next: required to carry the proven program through Type I and Type II, or a readiness dependency that still needs validation.
- P2 - validate first: do not schedule until evidence justifies the capability and its proposed workflow.

Milestone and priority are intentionally different. A Type II story can be essential to the eventual product and still be P1 because the readiness workflow must be proven first.

## Issue-by-issue triage

| Story | Priority | Evidence | Triage rationale |
| --- | --- | --- | --- |
| R1-01 | P0 | Confirmed | The staged readiness to Type I to Type II journey is the product promise. |
| R1-02 | P0 | Derived | A stable system boundary is required to make readiness status meaningful. |
| R1-03 | P0 | Derived | The product needs traceable criteria, but source and permitted use still require validation. |
| R1-04 | P0 | Confirmed | A small compliance team requires delegated responsibilities and least privilege. |
| R1-05 | P0 | Confirmed | Controls are a directly stated core job. |
| R1-06 | P0 | Derived | Mappings make criteria coverage explainable; the review workflow must be validated. |
| R1-07 | P0 | Derived | Promoted 2026-09-14: the R1 exit, R1-08, and R2-09 require an assessed risk register, and risk assessment (CC3) is tested in Type I. The method and authority are resolved in M0-D10. |
| R1-08 | P0 | Confirmed | The product must begin with readiness and show an owned route forward. |
| R1-09 | P0 | Confirmed | The live readiness engagement already has material that must be adopted without restarting. |
| R1-10 | P0 | Confirmed | Access governance requires a known, owned application and reviewed-system universe. |
| R1-11 | P0 | Confirmed | Human and NHI access review requires an authoritative roster and accountable identity ownership. |
| R1-12 | P0 | Derived | The boundary and system description require material technology, information, and data-flow inventory beyond applications. |
| R1-13 | P0 | Derived | Commitments, requirements, CUECs, and CSOCs explain what controls and the described system are intended to achieve. |
| R1-14 | P0 | Derived | Promoted 2026-09-14: the R1 exit, R1-08, and R2-09 require provider oversight, and vendor management (CC9.2) is tested in Type I. The due-diligence set and subservice treatment are resolved in M0-D11. |
| R2-01 | P0 | Derived | Ownership and cadence turn control definitions into actionable work. |
| R2-02 | P0 | Confirmed | Policies are a directly stated core job. |
| R2-03 | P0 | Confirmed | Evidence is a directly stated core job. |
| R2-04 | P0 | Derived | The product must distinguish describing a control from performing it. |
| R2-05 | P0 | Derived | Reproducible design and implementation evaluation is required before Type I readiness can be trusted. |
| R2-06 | P0 | Confirmed | User access reviews are a directly stated core job. |
| R2-07 | P0 | Derived | Readiness gaps need accountable resolution rather than status-only tracking. |
| R2-08 | P0 | Confirmed | Consultant collaboration is happening now; the mechanism remains open until the team validates it. |
| R2-09 | P0 | Derived | A readiness product needs a deliberate, explainable Type I entry decision. |
| R2-10 | P0 | Derived | Promoted 2026-09-14: the R2 exit and R2-09 require policy communication, and acknowledgement and training evidence (CC1/CC2) are tested in Type I. The audience and training workflow are resolved in M0-D12. |
| R2-11 | P0 | Confirmed | A small team needs one accountable daily work view without a second source of workflow truth. |
| R2-12 | P1 | Derived | Sensitive audit evidence needs lifecycle and disclosure governance; exact retention and hold policy requires validation (M0-D16). Until it is delivered, R1-08 and R2-09 show evidence governance as an explicit, acknowledged gap. |
| T1-01 | P1 | Confirmed | Supporting Type I after readiness is an explicit product outcome. |
| T1-02 | P1 | Derived | A governed system description is central to Type I and Type II, but its exact sections and export need auditor input. |
| T1-03 | P2 | Hypothesis | Direct auditor accounts are not yet requested and may conflict with the audit firm workflow. |
| T1-04 | P1 | Derived | Audit requests are expected, but collaboration and delivery preferences need validation. |
| T1-05 | P1 | Derived | An indexed package and reconciled management outputs provide an auditor-independent handoff; exact formats need validation. |
| T1-06 | P1 | Derived | Type I observations need to survive into the Type II operating plan. |
| T1-07 | P1 | Derived | The program needs an attributable transition from Type I to Type II. |
| T2-01 | P1 | Confirmed | Supporting a Type II period is an explicit product outcome. |
| T2-02 | P1 | Derived | Recurring operations are necessary for Type II, but scheduling details require real control examples. |
| T2-03 | P1 | Derived | Complete occurrence populations are central to sustained evidence, but sampling details need audit input. |
| T2-04 | P1 | Derived | Continuous visibility supports intervention during the observation period. |
| T2-05 | P1 | Derived | Recurring access reviews extend a confirmed job into the Type II period. |
| T2-06 | P2 | Hypothesis | A separate governance-review feature may duplicate the general control calendar. |
| T2-07 | P1 | Derived | Significant-change and incident impact must feed Type II history and the system description without replacing source systems. |
| T2-08 | P2 | Hypothesis | Inventory, access, population, and evidence sources and savings must be observed before connector work starts. |
| T2-09 | P2 | Hypothesis | Management review may be an ordinary recurring control rather than a dedicated product surface. |
| T3-01 | P1 | Confirmed | Supporting the Type II examination is part of the explicitly stated journey. |
| T3-02 | P1 | Derived | Reconciled source-backed populations are required for sampling, but their definitions and formats need auditor input. |
| T3-03 | P1 | Derived | Type II sampling extends the request workflow and must be validated with a real examination. |
| T3-04 | P1 | Derived | Examination exceptions need traceable remediation and next-period ownership. |
| T3-05 | P1 | Derived | A period package offers an auditor-independent delivery path and retained record. |
| T3-06 | P1 | Derived | The team needs to distinguish its sign-off from the auditor result. |
| T3-07 | P1 | Derived | Roll-forward supports the product promise of one continuing program rather than annual reconstruction. |

## M0 discovery and architecture triage

Discovery and architecture issues carry the priority of the most urgent work
they block. They are sequenced before implementation of the stories they block,
not before all product work.

| Item | Priority | Blocks | Triage rationale |
| --- | --- | --- | --- |
| M0-D01 | P0 | R1-01, R1-02, R1-03, T1-01, T2-01 | Engagement type, categories, services, and dates shape scope and every milestone date. |
| M0-D02 | P0 | R1-03, R1-06 | Criteria content cannot be stored or exported until its source and permitted use are known. |
| M0-D03 | P0 | EN-01, EN-04, R1-04, R2-01, R2-05, R2-11 | Roles, scopes, and small-team separation-of-duties exceptions govern every authorization decision. |
| M0-D04 | P0 | R1-09 | Mid-readiness adoption depends on the real consultant and internal source material. |
| M0-D05 | P0 | R1-10, R2-06 | The application and reviewed-system universe must be grounded in the real list. |
| M0-D06 | P0 | R1-11, R2-06, R2-10 | Access, policy, and training populations require an authoritative roster and NHI ownership. |
| M0-D07 | P0 | R2-06 | Access-review rules must be proven against real provider exports. |
| M0-D08 | P0 | R1-12 | The minimum technology and information inventory depends on the first boundary. |
| M0-D09 | P0 | R1-13 | Commitments, CUECs, and CSOCs come from real source artifacts and an approval authority. |
| M0-D10 | P0 | R1-07 | The risk method and acceptance authority must be agreed before assessments are comparable. |
| M0-D11 | P0 | R1-14 | Vendor materiality and due diligence determine the provider workflow. |
| M0-D12 | P0 | R2-10 | Audience, acknowledgement, and training evidence determine campaign rules. |
| M0-D13 | P0 | R2-05 | Evaluation procedures and independence determine the Type I quality gate. |
| M0-D14 | P0 | R2-08 | The consultant's preferred collaboration model is still unvalidated. |
| M0-D15 | P0 | R2-11 | The work queue must reflect the team's real daily operating needs. |
| M0-D16 | P1 | R2-12 | Evidence handling rules are needed before governance is built; R2-12 remains P1. |
| M0-D17 | P1 | T1-02, T1-03, T1-05, T1-07, T3-02, T3-05, T3-06 | Audit-firm formats and collaboration model drive Type I and Type II outputs. |
| M0-D18 | P1 | — | Establishes the baselines and targets for the brief's success measures. |
| M0-D19 | P2 | T2-06, T2-09 | Dedicated governance and management review surfaces remain hypotheses. |
| M0-D20 | P2 | T2-08 | Connector work waits for measured manual effort and source discovery. |
| M0-D21 | P1 | T2-07 | Compliance-facing change and incident facts must not replace operational systems. |
| M0-D22 | P0 | R1-02, R1-05, R1-07, R1-10, R1-11, R1-12, R2-06 | Resolves double-owned and missing identity and inventory concepts. |
| M0-D23 | P0 | EN-04, R1-07, R1-08, R1-14, R2-05, R2-07, R2-09, T2-04, T2-09 | Resolves overlapping gap, finding, exception, risk-acceptance, review, and readiness ownership. |
| M0-D24 | P0 | Every delivery story and first delivery slice | Defines the release-wide accessibility and supported-browser acceptance baseline. |
| M0-A01 | P0 | EN-02, EN-03, M0-A02, M0-A05, M0-A06 | No persistence exists; versioning and effective history underpin every story. |
| M0-A02 | P0 | EN-03 | Seven snapshot types need one provable freezing and amendment model. |
| M0-A03 | P0 | EN-06 | Evidence and file handling need safe storage, inspection, and per-artifact access. |
| M0-A04 | P0 | EN-01 | Authorization combines membership, grants, responsibility, state, and separation of duties. |
| M0-A05 | P0 | R1-08, R2-11 | Readiness and work projections must reconcile to source records with an as-of time. |
| M0-A06 | P0 | EN-05 | Every import and collection shares preview, acceptance, and replay semantics. |

## Enabler triage

Enablers are approved exceptions to the story-only backlog. Each is P0 because
several P0 stories depend on it, and each is proven through its first
consuming story rather than released independently.

| Enabler | Depends on | First consumer | Triage rationale |
| --- | --- | --- | --- |
| EN-01 Authorization, organization isolation, and actor attribution | M0-A04, M0-D03 | R1-01 | Every story requires server-enforced authorization and attribution. |
| EN-02 Versioned records, effective history, and change-impact preview | M0-A01 | R1-02 | Nine P0 stories require drafts, immutable versions, successors, and impact preview. |
| EN-03 Immutable snapshots, content identity, and amendments | M0-A01, M0-A02 | R1-11d | Workforce, population, readiness, and engagement snapshots share one mechanism. |
| EN-04 Attributable review and approval decisions | EN-01, M0-D03, M0-D23 | R1-02 | Approval of exact versions is needed before R2-05, which previously owned review. |
| EN-05 Import with preview, reconciliation, and safe replay | M0-A06, EN-01 | R1-10b | Six stories import governed records; R1-09 previously owned the only import model. |
| EN-06 Governed artifact storage and content identity | M0-A03, EN-01 | R2-03 | Evidence, policy files, provider reports, and imports share artifact identity and access. |

## Delivery slices

These P0 stories contain several independently valuable outcomes and are
delivered as outcome slices tracked as GitHub sub-issues. Slices inherit the
parent's priority, evidence level, milestone, domain slice, and definition of
done.

| Story | Slices |
| --- | --- |
| R1-04 | R1-04a member activation; R1-04b scoped access; R1-04c teams and optional IdP group mapping; R1-04d suspension, deprovisioning, and orphaned work; R1-04e separation-of-duties conflicts |
| R1-09 | R1-09a controls, mappings, and owners; R1-09b policies and evidence files; R1-09c scope, inventories, risks, providers, and findings; R1-09d safe repeat import and readiness reconciliation |
| R1-10 | R1-10a applications and reviewed systems; R1-10b import and reconciliation; R1-10c access-review scope decisions; R1-10d relationships, change, and retirement |
| R1-11 | R1-11a roster and source precedence; R1-11b joiners, movers, leavers, and conflicts; R1-11c NHI ownership; R1-11d workforce snapshots and restricted fields |
| R2-05 | R2-05a evaluation plan; R2-05b design, implementation, and evidence conclusions; R2-05c independent review; R2-05d deviations and retest |
| R2-06 | R2-06a population import; R2-06b human and NHI classification; R2-06c expectations and variance; R2-06d frozen campaign and decisions; R2-06e remediation, verification, and completion |
| R2-11 | R2-11a projected work queues; R2-11b assignment and escalation; R2-11c reminders and digests |

## Triage result

| Priority | Count | Scheduling meaning |
| --- | ---: | --- |
| P0 | 25 | Candidate first product release, delivered through dependency-ordered stories and slices. |
| P1 | 20 | Product path after readiness proof or after required validation. |
| P2 | 4 | Explicitly unscheduled hypotheses. |

| Evidence | Count |
| --- | ---: |
| Confirmed | 15 |
| Derived | 30 |
| Hypothesis | 4 |

Counts above are user stories. Supporting issues are counted separately:

| Issue type | Count | Priority mix |
| --- | ---: | --- |
| M0 product discovery | 24 | 18 P0, 4 P1, 2 P2 |
| M0 architecture decision | 6 | 6 P0 |
| Enabler | 6 | 6 P0 |
| Delivery slice | 29 | 29 P0 |

## Recommended P0 delivery sequence

The sequence follows the GitHub issue dependencies. Items in the same step have
no dependency on one another and can proceed in parallel.

0. Resolve the P0 discovery and architecture decisions: M0-D01 through M0-D15, M0-D22 through M0-D24, and M0-A01 through M0-A06. M0-D24 is a global readiness gate; each other blocked item waits only for its own blockers, so discovery and delivery otherwise overlap.
1. Foundation enablers: EN-01, EN-02, and EN-03.
2. Remaining enablers, program, and team: EN-04, EN-05, EN-06, R1-01, and R1-04 (R1-04a first, then R1-04b through R1-04e).
3. Boundary, criteria, workforce, policies, and evidence: R1-02, R1-03, R1-11 (R1-11a first), R2-02, and R2-03.
4. Controls, risks, applications, commitments, and policy communication: R1-05, R1-07, R1-10 (R1-10a first), R1-13, and R2-10.
5. Mappings, technology inventory, providers, ownership and cadence, evaluation, and access review: R1-06, R1-12, R1-14, R2-01, R2-05 (R2-05a through R2-05c), and R2-06 (R2-06a first, R2-06b after R1-11).
6. Adopt existing readiness work as its owning stories land: R1-09a (controls, mappings, and owners) after R1-06, and R1-09b (policies and evidence) after R2-02 and R2-03.
7. Readiness, control performance, and accountable work: R1-08, R2-04, and R2-11.
8. Findings and consultant collaboration: R2-07 and R2-08, then R2-05d, R1-09c (scope, inventories, risks, providers, and findings), and R1-09d (repeat import and readiness reconciliation).
9. The Type I entry decision: R2-09.

Adoption of the live readiness engagement therefore begins with the control
inventory in step 6 rather than at program setup. Earlier record families can
be entered manually through their owning stories.

Slice further only into outcome slices recorded as sub-issues of the owning
story. Do not split the product backlog into API, persistence, worker, and UI
issues.

No story in the sequence is ready merely because its visible workflow is
understood. Its blocking discovery and architecture decisions must be
incorporated, and its upstream identities and records, downstream readiness and
snapshot effects, authorization rules, history, failure behavior, and
API-to-UI acceptance evidence must also be explicit in the issue.

## Validation queue before P2 work

Each queue item is tracked as an M0 discovery issue.

1. Ask the readiness consultants whether they prefer indexed handoffs, secure links, direct product access, or an existing audit platform for work-in-progress review (M0-D14).
2. Ask the audit firm whether it wants portal access, exported packages, existing audit software integration, or a combination (M0-D17).
3. Observe whether periodic policy, risk, vendor, and management reviews fit the general control-performance workflow before creating dedicated recurring product surfaces (M0-D19).
4. Validate the first application, technology, information, and workforce inventories; provider exports; NHI classifications; expected-access rules; and effective grant paths using the real readiness engagement (M0-D05, M0-D06, M0-D07, M0-D08).
5. Validate commitments, requirements, CUECs, CSOCs, the risk method, material-provider threshold, policy audience and training sources, and evidence-handling rules with the consultant (M0-D09, M0-D10, M0-D11, M0-D12, M0-D16).
6. Measure manual inventory, population, access, and evidence effort by source before prioritizing HRIS, Auth0, Entra, GitHub, cloud, MDM, or other connectors (M0-D20).
7. Validate required control-matrix, system-description, assertion, representation-letter, population, sample, evidence-index, package, and delivery formats with the audit firm (M0-D17).
8. If Privacy is selected, build and validate a separate personal-information-lifecycle backlog before claiming complete category support (M0-D01).

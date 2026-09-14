# Product backlog triage

Status: working product-owner review

This review prevents the backlog from becoming a catalog of plausible features. A story may describe a sensible capability and still be the wrong thing to build now.

The [shared domain model](domain-model.md) and each story's domain slice and
implementation subtasks are delivery gates, not separate backlog features.
They ensure that platform access, responsibilities, controls, evidence,
reviews, readiness, and engagement snapshots form one process rather than
parallel subsystems.

The rationale for added, refined, and intentionally deferred capabilities is
recorded in the dated [SOC 2 product gap analysis](gap-analysis.md).

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
| R1-07 | P1 | Derived | Risk assessment and treatment support defensible control selection; the first method and authority need consultant validation. |
| R1-08 | P0 | Confirmed | The product must begin with readiness and show an owned route forward. |
| R1-09 | P0 | Confirmed | The live readiness engagement already has material that must be adopted without restarting. |
| R1-10 | P0 | Confirmed | Access governance requires a known, owned application and reviewed-system universe. |
| R1-11 | P0 | Confirmed | Human and NHI access review requires an authoritative roster and accountable identity ownership. |
| R1-12 | P0 | Derived | The boundary and system description require material technology, information, and data-flow inventory beyond applications. |
| R1-13 | P0 | Derived | Commitments, requirements, CUECs, and CSOCs explain what controls and the described system are intended to achieve. |
| R1-14 | P1 | Derived | Material provider oversight is necessary, but the first due-diligence set and subservice treatment require validation. |
| R2-01 | P0 | Derived | Ownership and cadence turn control definitions into actionable work. |
| R2-02 | P0 | Confirmed | Policies are a directly stated core job. |
| R2-03 | P0 | Confirmed | Evidence is a directly stated core job. |
| R2-04 | P0 | Derived | The product must distinguish describing a control from performing it. |
| R2-05 | P0 | Derived | Reproducible design and implementation evaluation is required before Type I readiness can be trusted. |
| R2-06 | P0 | Confirmed | User access reviews are a directly stated core job. |
| R2-07 | P0 | Derived | Readiness gaps need accountable resolution rather than status-only tracking. |
| R2-08 | P0 | Confirmed | Consultant collaboration is happening now; the mechanism remains open until the team validates it. |
| R2-09 | P0 | Derived | A readiness product needs a deliberate, explainable Type I entry decision. |
| R2-10 | P1 | Derived | Policy approval and workforce communication are distinct; the first audience, acknowledgement, and training workflow needs validation. |
| R2-11 | P0 | Confirmed | A small team needs one accountable daily work view without a second source of workflow truth. |
| R2-12 | P1 | Derived | Sensitive audit evidence needs lifecycle and disclosure governance; exact retention and hold policy requires validation. |
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

## Triage result

| Priority | Count | Scheduling meaning |
| --- | ---: | --- |
| P0 | 22 | Candidate first product release, implemented in thin vertical slices. |
| P1 | 23 | Product path after readiness proof or after required validation. |
| P2 | 4 | Explicitly unscheduled hypotheses. |

| Evidence | Count |
| --- | ---: |
| Confirmed | 15 |
| Derived | 30 |
| Hypothesis | 4 |

## Recommended P0 delivery sequence

1. Program, boundary, team, and criteria: R1-01 through R1-04.
2. Bring the live readiness work into the product: R1-09.
3. Establish workforce context and the application and reviewed-system universe: R1-11 and R1-10.
4. Establish material technology, information, commitments, requirements, and shared responsibilities: R1-12 and R1-13.
5. Controls, mappings, and readiness gaps: R1-05, R1-06, and R1-08.
6. Owned control operation and evaluation: R2-01, R2-04, and R2-05.
7. Directly requested policies and evidence: R2-02 and R2-03.
8. Inventory actual human and NHI access, approve expectations, and complete the initial review: R2-06.
9. Operate from one accountable work view: R2-11.
10. Consultant feedback, gap closure, and the Type I entry decision: R2-08, R2-07, and R2-09.

Each sequence item should be sliced further only as implementation subtasks inside the owning user story. Do not split the product backlog into API, persistence, worker, and UI issues.

No story in the sequence is ready merely because its visible workflow is
understood. Its upstream identities and records, downstream readiness and
snapshot effects, authorization rules, history, failure behavior, and
API-to-UI acceptance evidence must also be explicit in the issue.

## Validation queue before P2 work

1. Ask the readiness consultants whether they prefer indexed handoffs, secure links, direct product access, or an existing audit platform for work-in-progress review.
2. Ask the audit firm whether it wants portal access, exported packages, existing audit software integration, or a combination.
3. Observe whether periodic policy, risk, vendor, and management reviews fit the general control-performance workflow before creating dedicated recurring product surfaces.
4. Validate the first application, technology, information, and workforce inventories; provider exports; NHI classifications; expected-access rules; and effective grant paths using the real readiness engagement.
5. Validate commitments, requirements, CUECs, CSOCs, the risk method, material-provider threshold, policy audience and training sources, and evidence-handling rules with the consultant.
6. Measure manual inventory, population, access, and evidence effort by source before prioritizing HRIS, Auth0, Entra, GitHub, cloud, MDM, or other connectors.
7. Validate required control-matrix, system-description, assertion, representation-letter, population, sample, evidence-index, package, and delivery formats with the audit firm.
8. If Privacy is selected, build and validate a separate personal-information-lifecycle backlog before claiming complete category support.

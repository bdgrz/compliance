# Product backlog triage

Status: product-priority baseline from the 2026-09-14 design review, reconciled
with accepted decisions and business direction on 2026-10-04. Live priorities,
dependencies, completion, and queues are in the [GitHub Project](https://github.com/orgs/bdgrz/projects/1)
and issue bodies; the [delivery cycle](delivery-cycle.md) defines milestone
exits and queue rules. Tables and counts below explain product sequencing and
the historical story set, not the current amount of open work.

This review prevents the backlog from becoming a catalog of plausible features. A story may describe a sensible capability and still be the wrong thing to build now.

The [shared domain model](domain-model.md) and each story's domain slice and
implementation subtasks are delivery gates, not separate backlog features.
They ensure that platform access, responsibilities, controls, evidence,
reviews, readiness, and engagement snapshots form one process rather than
parallel subsystems.

The rationale for added, refined, and intentionally deferred capabilities is
recorded in the dated [SOC 2 product gap analysis](gap-analysis.md).

The 2026-09-14 design review established the M0 decisions, shared enablers,
and outcome-level delivery slices. The M0 records are the accepted design
baseline. The 2026-09-28 reset split R1
into R1.0 program and access foundation, R1.1 governed manual source records,
and R1.2 integrated readiness. The [backlog contract](backlog.md#product-backlog-contract)
defines product scope; milestone and queue fields in GitHub select delivery.

The 2026-09-14 product direction widened to a small SOC 2 firm that
provides both advisory and attest services to many clients. Client
organizations are the only tenant, firm staff and client users both sign in,
and the first release stays tenant-ready rather than firm-complete: the tenancy
foundations (M0-D25, M0-A07, EN-01, and R1-15) are P0, and firm operations are
planned in **F1 - Multi-client firm operations**.

The 2026-10-04 business direction confirms that one account can create and
manage many organizations for itself or clients and delegate most organization
management to client personnel. Every organization remains its own tenant and
hosted billing unit. Self-hosting is free; hosted SaaS uses a fixed base fee per
organization plus optional integrations, services, and additional automations.
The [business operations scope](business-operations.md) defines delegated
management (BO-01), hosted billing (BO-02), and add-on management (BO-03).
Paid-hosting gates are distinct from the manual SOC 2 release gates; these
business objectives acquire live delivery queues through their owning issues.

Existing backend and frontend children keep separate acceptance evidence.
Future children are created only for independently closable work. Backend
dependencies use upstream backend capabilities rather than UI-dependent product
parents. Independent capability bundles may be Active in parallel when each
has a separate branch/worktree and closed prerequisites; a high priority does
not override an open dependency. An unvalidated P2 hypothesis remains in
Discovery.

Use one PR for a reviewable capability that may satisfy several dependent
issue children. Keep separate acceptance evidence for each child and close only
the criteria that passed. Avoid PRs that deliver only one test or one technical
layer. The owning issues and PRs record delivery evidence and remaining gaps.

## Evidence levels

- Confirmed: directly supported by the target customer statement in this product conversation.
- Derived: supports the stated readiness to Type I to Type II journey through an accepted product default; actual customer facts or professional inputs are validated where the owning acceptance requires them.
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
- the first client has no existing readiness material, so its records are
  authored in their owning product workflows; later-client import support stays
  P1 until representative material is available ([M0-D04](decisions/m0-d04-readiness-material.md), #338);
- every feature must remain usable through manual user actions when external
  integrations and optional source-collection automation are absent or disabled;
  those capabilities enhance program setup, maintenance, and operation rather
  than gate them (see the [manual-first product contract](backlog.md#manual-first-product-contract));
- consultant review and feedback are part of the current workflow;
- one account can create and manage multiple organizations, with management
  delegated to client personnel through organization-specific responsibilities;
- the organization is the hosted monetization unit; self-hosting is free and
  hosted SaaS charges a base fee plus optional add-ons;
- the backlog must express business objectives, requirements, and acceptance criteria as API-to-UI user stories.

Accepted [product decisions](product-brief.md#product-decisions) supply the
governing defaults for detailed workflows. Customer-specific facts, numerical
targets, and professional ratification remain with their named follow-up
issues; they do not reopen every M0 decision or block unrelated manual work.

## Priority policy

- P0 - prove now: required for the first product release to complete readiness and make a Type I entry decision.
- P1 - build next: required to carry the proven program through Type I and Type II, or a readiness dependency that still needs validation.
- P2 - validate first: do not schedule until evidence justifies the capability and its proposed workflow.

Milestone and priority are intentionally different. A Type II story can be essential to the eventual product and still be P1 because the readiness workflow must be proven first.

## Issue-by-issue triage

The priorities and evidence labels describe the original product rationale,
with accepted scope changes noted. They do not report live issue status or
replace the current R1.0, R1.1, and R1.2 delivery queues.

| Story | Priority | Evidence | Triage rationale |
| --- | --- | --- | --- |
| R1-01 | P0 | Confirmed | The staged readiness to Type I to Type II journey is the product promise. |
| R1-02 | P0 | Derived | A stable system boundary is required to make readiness status meaningful. |
| R1-03 | P0 | Derived | Traceable criteria use the accepted M0-D02 catalog of identifiers and original summaries; licensed overlays require the supplier's confirmed rights in #351. The base catalog does not wait on that overlay. |
| R1-04 | P0 | Confirmed | A small compliance team requires delegated responsibilities and least privilege. |
| R1-05 | P0 | Confirmed | Controls are a directly stated core job. |
| R1-06 | P0 | Derived | Mappings make criteria coverage explainable through versioned rationale and attributable review under the accepted decision model. Actual control coverage remains a program-specific judgment. |
| R1-07 | P0 | Derived | Promoted 2026-09-14: the R1 exit, R1-08, and R2-09 require an assessed risk register, and risk assessment (CC3) is tested in Type I. The method and authority are resolved in M0-D10. |
| R1-08 | P0 | Confirmed | The product must begin with readiness and show an owned route forward. |
| R1-09 | P1 | Derived | Demoted 2026-09-22 by [M0-D04](decisions/m0-d04-readiness-material.md): the first client has no existing readiness material, and import work waits until the canonical data shape and storage internals are settled. Sample collection is tracked in #338. |
| R1-10 | P0 | Confirmed | Access governance requires a known, owned application and reviewed-system universe. |
| R1-11 | P0 | Confirmed | Human and NHI access review requires an authoritative roster and accountable identity ownership. |
| R1-12 | P0 | Derived | The boundary and system description require material technology, information, and data-flow inventory beyond applications. |
| R1-13 | P0 | Derived | Commitments, requirements, CUECs, and CSOCs explain what controls and the described system are intended to achieve. |
| R1-14 | P0 | Derived | Promoted 2026-09-14: the R1 exit, R1-08, and R2-09 require provider oversight, and vendor management (CC9.2) is tested in Type I. The due-diligence set and subservice treatment are resolved in M0-D11. |
| R1-15 | P0 | Confirmed | Verified users create organizations themselves and can manage several; scoped delegation preserves each organization's records and billing identity. Organization administration is core scope, while professional engagement assignments remain F1 scope. |
| R2-01 | P0 | Derived | Ownership and cadence turn control definitions into actionable work. |
| R2-02 | P0 | Confirmed | Policies are a directly stated core job. |
| R2-03 | P0 | Confirmed | Evidence is a directly stated core job. |
| R2-04 | P0 | Derived | The product must distinguish describing a control from performing it. |
| R2-05 | P0 | Derived | Reproducible design and implementation evaluation is required before Type I readiness can be trusted. |
| R2-06 | P0 | Confirmed | User access reviews are a directly stated core job. |
| R2-07 | P0 | Derived | Readiness gaps need accountable resolution rather than status-only tracking. |
| R2-08 | P0 | Confirmed | M0-D14 fixes in-product collaboration: our firm's advisors use accepted engagement assignments; consultants hired directly by the client use scoped, expiring guest membership and exact-version draft handoffs. Feedback does not grant approval. |
| R2-09 | P0 | Derived | A readiness product needs a deliberate, explainable Type I entry decision. |
| R2-10 | P0 | Derived | Promoted 2026-09-14: the R2 exit and R2-09 require policy communication, and acknowledgement and training evidence (CC1/CC2) are tested in Type I. The audience and training workflow are resolved in M0-D12. |
| R2-11 | P0 | Confirmed | A small team needs one accountable daily work view without a second source of workflow truth. |
| R2-12 | P1 | Derived | M0-D16 fixes retention, hold, redaction, and disclosure defaults. R1-08 and R2-09 show any undelivered evidence governance as an explicit, acknowledged gap until its owning issue proves the capability. |
| T1-01 | P1 | Confirmed | Supporting Type I after readiness is an explicit product outcome. |
| T1-02 | P1 | Derived | A governed system description is central to Type I and Type II, but its exact sections and export need auditor input. |
| T1-03 | P2 | Hypothesis | Direct auditor accounts are not yet requested and may conflict with the audit firm workflow. |
| T1-04 | P1 | Derived | Requests and responses bind to exact support under M0-D17/M0-D27: external firms receive packages, while our attest assignees collaborate in-product. Actual-firm format deltas are #339. |
| T1-05 | P1 | Derived | An indexed package and reconciled management outputs provide an auditor-independent handoff; exact formats need validation. |
| T1-06 | P1 | Derived | Type I observations need to survive into the Type II operating plan. |
| T1-07 | P1 | Derived | The program needs an attributable transition from Type I to Type II. |
| T2-01 | P1 | Confirmed | Supporting a Type II period is an explicit product outcome. |
| T2-02 | P1 | Derived | Versioned cadence produces the expected occurrence population; users perform and review each occurrence with frozen evidence. Policy, risk, provider, and management reviews use this same workflow under M0-D19. |
| T2-03 | P1 | Derived | Complete occurrence populations are central to sustained evidence, but sampling details need audit input. |
| T2-04 | P1 | Derived | Continuous visibility supports intervention during the observation period. |
| T2-05 | P1 | Derived | Recurring access reviews extend a confirmed job into the Type II period. |
| T2-06 | P2 | Hypothesis | Original dedicated-surface hypothesis superseded by [M0-D19](decisions/m0-d19-periodic-reviews.md): periodic policy, risk, and vendor reviews are ordinary recurring controls covered by R2-01 and T2-02, with evidence and sign-off. |
| T2-07 | P1 | Derived | Significant-change and incident impact must feed Type II history and the system description without replacing source systems. |
| T2-08 | P2 | Hypothesis | Inventory, access, population, and evidence sources and savings must be observed before connector work starts. [M0-D20](decisions/m0-d20-integration-sources.md) defers connectors; #342 measures effort and ranks them. |
| T2-09 | P2 | Hypothesis | Original dedicated-surface hypothesis superseded by [M0-D19](decisions/m0-d19-periodic-reviews.md): management review is a recurring control occurrence with frozen readiness/monitoring evidence, an attributed sign-off, and requested actions tracked as R2-07 findings. |
| T3-01 | P1 | Confirmed | Supporting the Type II examination is part of the explicitly stated journey. |
| T3-02 | P1 | Derived | Reconciled source-backed populations are required for sampling, but their definitions and formats need auditor input. |
| T3-03 | P1 | Derived | Type II sampling extends the request workflow and must be validated with a real examination. |
| T3-04 | P1 | Derived | Examination exceptions need traceable remediation and next-period ownership. |
| T3-05 | P1 | Derived | A period package offers an auditor-independent delivery path and retained record. |
| T3-06 | P1 | Derived | The team needs to distinguish its sign-off from the auditor result. |
| T3-07 | P1 | Derived | Roll-forward supports the product promise of one continuing program rather than annual reconstruction. |
| F1-01 | P1 | Derived | Lifecycle adopts M0-D16, M0-D25, and M0-D27. Attest workpapers remain external; engagement closure/offboarding requires the collaboration record package and attributed delivery into the attest software before its hold can clear. Advisory-material handover remains #346. |
| F1-02 | P1 | Confirmed | Managing all clients from one platform is the stated firm need; it follows per-client readiness and work. |
| F1-03 | P2 | Hypothesis | Capacity planning may belong in an existing resource tool; validate before scheduling. |
| F1-04 | P1 | Derived | Reusable methodology scales a small firm through versioned, attributed templates under M0-D02 and M0-D25. Licensed text remains restricted to the confirmed supplier's rights. |
| F1-05 | P1 | Derived | Consultants serving many clients need one queue; it extends the per-client queue in R2-11. |
| F1-06 | P1 | Derived | Client sign-in through each client's identity provider follows M0-A07; firm staff and a first client can start on the firm's provider. |
| F1-07 | P1 | Confirmed | The firm provides both advisory and attest services, so every client service must be an explicit, accepted engagement. |
| F1-08 | P1 | Confirmed | M0-D26 fixes the strict advisory/attest wall. The quality-management partner ratifies initial rule-set content in #343 before the professional service depends on it. |

## M0 discovery and architecture triage

This is a registry of the accepted design inputs and their consuming stories.
Its priority labels preserve the original discovery rationale. Implementation
adopts the recorded decisions; a closed discovery decision is not a request to
discover the same policy again. Only a named remaining fact or changed policy
creates a new prerequisite in the owning issue.

| Item | Original priority | Consuming stories | Governing rationale |
| --- | --- | --- | --- |
| M0-D01 | P0 | R1-01, R1-02, R1-03, T1-01, T2-01 | A continuing program separates planned stages and target dates from engagements and confirmed dates. All five categories are selected; Privacy lifecycle support is separately gated by #349. |
| M0-D02 | P0 | R1-03, R1-06 | The base catalog ships identifiers and original summaries; licensed text requires a supplied overlay under confirmed rights in #351. Catalog editions remain immutable. |
| M0-D03 | P0 | EN-01, EN-04, R1-04, R2-01, R2-05, R2-11 | Roles, scopes, and small-team separation-of-duties exceptions govern every authorization decision. |
| M0-D04 | P0 | R1-09 | The first client authors records manually because it has no existing readiness material. Later-client imports wait on representative material in #338 and scheduled import scope ([record](decisions/m0-d04-readiness-material.md)). |
| M0-D05 | P0 | R1-10, R2-06 | Manual application and concrete system records have stable identities, ownership, scope, and source declarations. Real records populate the accepted shape without requiring a connector. |
| M0-D06 | P0 | R1-11, R2-06, R2-10 | Access, policy, and training populations require an authoritative roster and NHI ownership. |
| M0-D07 | P0 | R2-06 | Users attest immutable observed access snapshots; direct assignments, calculated effective paths, classifications, and approved expectations remain distinct. Representative provider exports inform the owning source-validation follow-up. |
| M0-D08 | P0 | R1-12 | The accepted minimum inventory captures material technology, information, and flows through owned manual records; first-client entry is ordinary product use. |
| M0-D09 | P0 | R1-13 | Commitments, CUECs, and CSOCs come from real source artifacts and an approval authority. |
| M0-D10 | P0 | R1-07 | The accepted qualitative 5×5 method, versioned appetite, and time-bounded acceptance rules make assessments comparable; actual appetite and advisor confirmation are #355. |
| M0-D11 | P0 | R1-14 | Vendor materiality and due diligence determine the provider workflow. |
| M0-D12 | P0 | R2-10 | Audience, acknowledgement, and training evidence determine campaign rules. Decided 2026-09-22 ([record](decisions/m0-d12-policy-acknowledgement-training.md)). |
| M0-D13 | P0 | R2-05 | Evaluation procedures and independence determine the Type I quality gate. Decided 2026-09-22 ([record](decisions/m0-d13-control-evaluation.md)). |
| M0-D14 | P0 | R2-08 | Accepted in-product advisor/guest collaboration, exact-version draft sharing, and feedback distinct from approval ([record](decisions/m0-d14-advisor-collaboration.md)). |
| M0-D15 | P0 | R2-11 | The work queue must reflect the team's real daily operating needs. Decided 2026-09-22 ([record](decisions/m0-d15-work-queue.md)). |
| M0-D16 | P1 | R2-12 | Evidence handling rules are needed before governance is built; R2-12 remains P1. Decided 2026-09-22 ([record](decisions/m0-d16-evidence-handling.md)). |
| M0-D17 | P1 | T1-02, T1-03, T1-05, T1-07, T3-02, T3-05, T3-06 | Accepted common-format defaults and package delivery for external firms; actual-firm deltas belong to #339 ([record](decisions/m0-d17-audit-deliverables.md)). |
| M0-D18 | P1 | — | Defines the four business measures and their attributed event sources; #340 establishes the measured baseline and targets ([record](decisions/m0-d18-success-measures.md)). |
| M0-D19 | P2 | R2-01, T2-02, R2-07; former T2-06/T2-09 | Accepted ordinary recurring control occurrences for governance and management reviews; requested corrective actions are findings ([record](decisions/m0-d19-periodic-reviews.md)). |
| M0-D20 | P2 | T2-08 | Connector work waits for measured manual effort and source discovery. Decided 2026-09-22 ([record](decisions/m0-d20-integration-sources.md)). |
| M0-D21 | P1 | T2-07 | Compliance-facing change and incident facts must not replace operational systems. Decided 2026-09-22 ([record](decisions/m0-d21-significant-change.md)). |
| M0-D22 | P0 | R1-02, R1-05, R1-07, R1-10, R1-11, R1-12, R2-06 | Resolves double-owned and missing identity and inventory concepts. |
| M0-D23 | P0 | EN-04, R1-07, R1-08, R1-14, R2-05, R2-07, R2-09, T2-04, T2-09 | Resolves overlapping gap, finding, exception, risk-acceptance, review, and readiness ownership. |
| M0-D24 | P0 | Every scheduled frontend child, delivery story, and first delivery slice | Defines accessible behavior and supported-browser requirements; client unit tests verify component accessibility, while manual browser review is deferred to the later end-to-end phase. Backend children omit this blocker. |
| M0-D25 | P0 | EN-01, R1-15, M0-D26, F1-01, F1-04, F1-07 | Defines the tenant boundary, firm-staff affiliation, firm-owned material, organization creation authority, and tenant vocabulary. |
| M0-D26 | P1 | F1-07, F1-08 | Independence rules for a firm that both advises and examines require professional review. Decided 2026-09-22 ([record](decisions/m0-d26-independence.md)). |
| M0-D27 | P1 | T1-04, T3-02, T3-03, F1-01 | Accepted attest collaboration only: workpapers/tests/report drafts stay in audit software; the platform retains requests, responses, populations, samples, evidence, and record-package delivery ([record](decisions/m0-d27-attest-scope.md)). |
| M0-D28 | P0 | EN-01, EN-05, R1-04, R1-10, R1-11, R1-12, R1-15, R2-06 | Establishes the canonical entity and relationship vocabulary from direct public sources whose use is acceptable for the Apache-2.0 product; unusable or unclear sources are excluded. |
| M0-A01 | P0 | EN-02, EN-03, M0-A02, M0-A05, M0-A06 | Accepted event-history, versioning, tenant-isolation, and whole-platform recovery boundary; Portia #70 and DevOps retain the separate production-recovery delivery gate. |
| M0-A02 | P0 | EN-03 | Accepted 2026-09-22 in ADR 0004 and ADR 0008: one freezing, content-identity, amendment, regeneration, and bounded-population model that each snapshot type adopts in its consuming story. |
| M0-A03 | P0 | EN-06 | Evidence and file handling need safe storage, inspection, and per-artifact access. |
| M0-A04 | P0 | EN-01 | Authorization combines membership, grants, responsibility, state, and separation of duties. |
| M0-A05 | P0 | F1-02, R1-08, R2-11 | Accepted ADR 0007: tenant-scoped asynchronous projections, never-stale anchored and derived reads, and cross-client reads denied by default; readiness, work, and portfolio semantics remain with their owning stories. |
| M0-A06 | P0 | EN-05 | Accepted in ADR 0005: every import and collection shares all-or-nothing acceptance behind a batch visibility barrier, preview, and replay semantics; the spike moved to EN-05 #195. |
| M0-A07 | P0 | EN-01, R1-15, F1-06 | Tenant context, slug-based browser routes with `tenant_id` APIs, the reserved-route registry, and client identity federation. |

## Enabler triage

Enablers are approved exceptions to the story-only backlog. Their original P0
classification reflects shared dependencies, and each is proven through an
owning consumer. EN-05 serves scheduled import/collection enhancements; its
completion never gates the equivalent manual data-entry workflow. Live
enabler scheduling and remaining acceptance stay in GitHub.

| Enabler | Depends on | First consumer | Triage rationale |
| --- | --- | --- | --- |
| EN-01 Authorization, tenant isolation, and actor attribution | M0-A04, M0-A07, M0-D03, M0-D25 | R1-01 | Every story requires server-enforced authorization, attribution, and tenant isolation. |
| EN-02 Versioned records, effective history, and change-impact preview | M0-A01 | R1-02 | Nine P0 stories require drafts, immutable versions, successors, and impact preview. |
| EN-03 Immutable snapshots, content identity, and amendments | M0-A01, M0-A02 | R1-11d | Workforce, population, readiness, and engagement snapshots share one mechanism. |
| EN-04 Attributable review and approval decisions | EN-01, M0-D03, M0-D23 | R1-02 | Approval of exact versions is needed before R2-05, which previously owned review. |
| EN-05 Import with preview, reconciliation, and safe replay | M0-A06, EN-01 | R1-10b | Six stories import governed records; R1-09 previously owned the only import model. |
| EN-06 Governed artifact storage and content identity | M0-A03, EN-01 | R2-03 | Evidence, policy files, provider reports, and imports share artifact identity and access. |

## Delivery slices

These stories contain several independently valuable outcomes and are
delivered as outcome slices tracked as GitHub sub-issues. This table records
the original slice families rather than every later implementation child.
Each slice carries its owning issue's current priority, milestone, dependency,
domain slice, and acceptance; import enhancements can be deferred while the
manual portion proceeds.

| Story | Slices |
| --- | --- |
| R1-04 | R1-04a member activation; R1-04b scoped access; R1-04c teams and optional IdP group mapping; R1-04d suspension, deprovisioning, and orphaned work; R1-04e separation-of-duties conflicts |
| R1-09 | R1-09a controls, mappings, and owners; R1-09b policies and evidence files; R1-09c scope, inventories, risks, providers, and findings; R1-09d safe repeat import and readiness reconciliation |
| R1-10 | R1-10a applications and reviewed systems; R1-10b import and reconciliation; R1-10c access-review scope decisions; R1-10d relationships, change, and retirement |
| R1-11 | R1-11a roster and source precedence; R1-11b joiners, movers, leavers, and conflicts; R1-11c NHI ownership; R1-11d workforce snapshots and restricted fields |
| R2-05 | R2-05a evaluation plan; R2-05b design, implementation, and evidence conclusions; R2-05c independent review; R2-05d deviations and retest |
| R2-06 | R2-06a manually attested population snapshot (imports later); R2-06b human and NHI classification; R2-06c expectations and variance; R2-06d frozen campaign and decisions; R2-06e remediation, verification, and completion |
| R2-11 | R2-11a projected work queues; R2-11b assignment and escalation; R2-11c reminders and digests |

## Triage result

These aggregate counts are retained from the September triage snapshot. They
include the dedicated T2-06/T2-09 hypotheses now covered by recurring controls
and are not recomputed from the amended evidence rationales above. They are
not counts of open, delivered, or Ready issues. Subsequent children,
business-operation scope, and changed priorities are tracked in GitHub.

| Priority | Count | Scheduling meaning |
| --- | ---: | --- |
| P0 | 26 | Candidate first product release, delivered through dependency-ordered stories and slices. |
| P1 | 27 | Product path after readiness proof, firm operations, or required validation. |
| P2 | 5 | Explicitly unscheduled hypotheses. |

| Evidence | Count |
| --- | ---: |
| Confirmed | 18 |
| Derived | 35 |
| Hypothesis | 5 |

Counts above are user stories. Supporting issues are counted separately:

| Issue type | Count | Priority mix |
| --- | ---: | --- |
| M0 product discovery | 28 | 20 P0, 6 P1, 2 P2 |
| M0 architecture decision | 7 | 7 P0 |
| Enabler | 6 | 6 P0 |
| Delivery slice | 29 | 29 P0 |

## Recommended P0 delivery sequence

This is a logical dependency reference for the product journey. Current
execution uses R1.0/R1.1/R1.2 and the GitHub Run order described in the
[delivery cycle](delivery-cycle.md). Items in the same step may proceed in
isolated lanes only after their actual owning prerequisites and acceptance
inputs are verified; this sequence alone does not make them Ready.

0. Adopt the accepted M0 decisions and architecture records in each consuming story. Verify any named remaining customer-fact, professional-policy, or platform-delivery prerequisite in that story's issue. The frontend adopts M0-D24; canonical identities adopt M0-D28. No new all-program M0 discovery pass is required.
1. Foundation enablers: EN-01 (including tenant isolation), EN-02, and EN-03.
2. Remaining manual-workflow enablers and the tenant boundary: EN-04, EN-06, and R1-15. EN-05 accompanies a scheduled import enhancement, independently of manual delivery.
3. Program and team inside the client organization: R1-01 and R1-04 (R1-04a first, then R1-04b through R1-04e).
4. Boundary, criteria, workforce, policies, and evidence: R1-02, R1-03, R1-11 (R1-11a first), R2-02, and R2-03.
5. Controls, risks, applications, commitments, and policy communication: R1-05, R1-07, R1-10 (R1-10a first), R1-13, and R2-10.
6. Mappings, technology inventory, providers, ownership and cadence, evaluation, and access review: R1-06, R1-12, R1-14, R2-01, R2-05 (R2-05a through R2-05c), and R2-06 (R2-06a first, R2-06b after R1-11).
7. Readiness, control performance, and accountable work: R1-08, R2-04, and R2-11.
8. Findings and consultant collaboration: R2-07 and R2-08, then R2-05d. Our firm's Advisor assignments also require accepted service-engagement support; the external-consultant guest path does not confer those professional roles.
9. The Type I entry decision: R2-09.

For the first client, every record family is authored through its owning
story. No existing material is adopted (M0-D04). R1-09 and other import
enhancements follow their consuming manual workflow only when representative
material exists and their own P1 delivery scope is selected.

Slice further only into outcome slices recorded as sub-issues of the owning
story. Do not split the product backlog into API, persistence, worker, and UI
issues.

No story in the sequence is ready merely because its visible workflow is
understood. Its blocking discovery and architecture decisions must be
incorporated, and its upstream identities and records, downstream readiness and
snapshot effects, authorization rules, history, failure behavior, and
API-to-UI acceptance evidence must also be explicit in the issue.

## F1 delivery sequence

F1 is not part of the first release. It can proceed alongside T1 once its
dependencies land:

1. Firm engagements after R1-04, R1-15, EN-04, M0-D25, and M0-D26: F1-07.
2. Independence walls and client identity federation: F1-08 and F1-06.
3. Templates after R1-05 and R2-02, and the client portfolio after R1-08 and R2-11: F1-04 and F1-02.
4. Cross-client work and the client lifecycle: F1-05 and F1-01.
5. Capacity planning only if validated: F1-03.

## Validation queue before P2 work

The accepted M0 records separate product defaults from remaining customer
facts. The following follow-ups describe evidence to collect, not live issue
status. Check each issue before scheduling work or changing its dependency.

| Evidence or decision | Owner and follow-up | Delivery effect |
| --- | --- | --- |
| Actual audit firm, dates, and boundary services | Compliance Lead and management, [#348](https://github.com/bdgrz/compliance/issues/348) under M0-D01 | Populate governed records; no invented confirmed dates. Basic program and boundary modeling proceeds with explicit unknowns. |
| Privacy lifecycle coverage | Product owner and readiness advisor, [#349](https://github.com/bdgrz/compliance/issues/349) under M0-D01 | Privacy is selected in the accepted first-engagement scope. Define and validate its lifecycle backlog before claiming support or completing the package while Privacy remains selected. |
| Actual audit-firm output formats | Compliance Lead and audit firm, [#339](https://github.com/bdgrz/compliance/issues/339) under M0-D17 | Record deltas to the accepted package defaults for the owning examination outputs. External firms receive packages; portal access remains a P2 hypothesis. |
| Measured operating value | Product owner and first client, [#340](https://github.com/bdgrz/compliance/issues/340) under M0-D18 | Establish the first 30-day baseline and numerical targets for the four defined measures. |
| Representative source material and import shapes | Later client/import owner, [#338](https://github.com/bdgrz/compliance/issues/338); workforce source owner, [#347](https://github.com/bdgrz/compliance/issues/347); access reviewer, [#353](https://github.com/bdgrz/compliance/issues/353) | Inform optional imports and actual-source validation through their owning issues; preserve the complete manual workflow. |
| Licensed criteria overlay supplier and scope | Catalog supplier and product owner, [#351](https://github.com/bdgrz/compliance/issues/351) under M0-D02 | Gate licensed text use; identifiers and original summaries remain usable without an overlay. |
| First client's commitments and risk appetite | Compliance Lead, management, and advisor, [#354](https://github.com/bdgrz/compliance/issues/354) and [#355](https://github.com/bdgrz/compliance/issues/355) | Enter owned source-backed declarations and the approved appetite; missing facts remain visible under accepted M0-D09/M0-D10 rules. |
| Reactor-raised finding defaults | Product owner with R2-07/R2-05 delivery owners, [#274](https://github.com/bdgrz/compliance/issues/274) and [#279](https://github.com/bdgrz/compliance/issues/279) | Ratify or replace the implemented medium severity, 30-day deadline, and member fallback ownership before treating them as accepted program policy; preserve the bounded implementation limitation in the owning control contracts. |
| Measured source effort and connector value | Product owner and source owner, [#342](https://github.com/bdgrz/compliance/issues/342) under M0-D20 | Rank integration enhancements after observing manual work. T2-08 remains a P2 hypothesis until its source and business case are validated. |
| Professional independence rule-set ratification | Firm quality-management partner, [#343](https://github.com/bdgrz/compliance/issues/343) under M0-D26 | Ratify rule content before advisory/attest service acceptance depends on it. |
| Advisory-material retention and handover | Firm leadership, [#346](https://github.com/bdgrz/compliance/issues/346) under M0-D25 | Resolve the named F1-01 retention input. Attest workpapers stay external under M0-D27; their collaboration record package and attributed delivery are already required. |
| Organization administration and paid-hosting policies | Product owner, organization administrators, and billing stakeholders; [BO-01, BO-02, BO-03](business-operations.md) | Define and schedule delegated management, billing, and add-on acceptance. Hosted commercial gates do not become prerequisites for self-hosted manual SOC 2 operation. |

Ordinary recurring controls cover policy, risk, vendor, and management reviews
(M0-D19). Re-file a dedicated capability only when observed use demonstrates a
specific gap. An accepted workflow is revisited through its recorded decision
and owning issue when evidence changes it, rather than by retaining the
original discovery question as open.

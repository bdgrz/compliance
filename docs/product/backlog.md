# Compliance product backlog

Status: product-owner baseline, restructured 2026-09-14 with M0 discovery, shared enablers, delivery slices, and multi-client tenancy

This backlog carries a small compliance team through SOC 2 readiness, a Type I examination, a Type II observation period, and a Type II examination. It implements the product direction in [product-brief.md](product-brief.md).

The platform is also tenant-ready for a small SOC 2 firm that provides advisory and attest services to many client organizations. Each client organization is a tenant; the tenancy foundations are P0 in M0 and R1, and firm operations are planned in F1.

The shared language, identity boundaries, record relationships, and
cross-story invariants are defined in [domain-model.md](domain-model.md). That
model is part of every issue created from this backlog.

The canonical vocabulary and standards mappings are defined in
[canonical-entity-model.md](canonical-entity-model.md). Public-source and reuse
requirements are defined in
[source-reference-policy.md](source-reference-policy.md). Both are part of
every issue that creates, imports, maps, or exposes canonical domain data.

The product coverage decisions behind this version are recorded in
[gap-analysis.md](gap-analysis.md).

## Product-backlog contract

Every product-backlog item is a user story that delivers a business outcome. Infrastructure, schema, API, background processing, and UI tasks may be implementation subtasks, but they are not separate product-backlog items. Backend and frontend children are delivery tracking under that story, not new business outcomes.

Three kinds of non-story issue are explicit exceptions:

- Product discovery issues (`M0-D..`) record a product decision or validation that a story needs before it is ready. They produce decisions, not code.
- Architecture decision issues (`M0-A..`) record an accepted ADR and a thin technical spike for a concern the domain model depends on.
- Enablers (`EN-..`) deliver a shared platform primitive that several stories would otherwise define inconsistently or too late. An enabler is not independently releasable; it is done only when its first consuming story uses it end to end, and consuming stories must not build feature-local substitutes. New enablers require the same product-owner approval as the six listed below.

A story that contains several independently valuable outcomes may be divided into delivery slices tracked as GitHub sub-issues. Each product slice retains an API-to-UI outcome whose acceptance criteria come from the parent story. When a validated feature story or product slice enters delivery, create an initial backend child and a frontend child. If downstream records are required to finish backend acceptance, add a later backend delivery child under the same story rather than making the baseline depend on its own consumers. Give every child the parent's milestone and record its applicable GitHub dependencies explicitly, because sub-issues do not inherit dependency relationships. Do not schedule children for an unvalidated P2 hypothesis.

Each backend child owns its authorized HTTP API and machine-appropriate MCP surface, Portia handlers and guards, event-sourced domain behavior, Fitz projections and reactors, and non-UI acceptance evidence. It inherits domain, security, and product-decision dependencies; omit UI-only M0-D24. When an upstream feature or enabler has backend children, depend on the applicable backend child or children rather than its product parent so later UI work cannot block backend completion. The frontend child owns the browser workflow, accessible interaction, loading, empty, error, retry, and forbidden states, and integration with the delivered API. It depends on all applicable backend children and M0-D24, plus any decision that specifically changes frontend behavior and the frontend child of an upstream browser workflow. Shared enablers receive a backend child; give an enabler a frontend child only when it has its own user-facing workflow. No frontend child redefines backend policy or duplicates canonical records.

Close each child only when its own acceptance evidence is complete. Keep the product parent open until all backend and frontend children and the integrated product outcome pass. A later product slice still depends on its first slice directly or transitively, in addition to any slice-specific blockers. Backfill the initial pair for active stories first; create children for other validated stories when scheduled.

Bundle dependent backend children into a PR when they form one reviewable capability and share contracts, domain records, or acceptance tests. List every covered child and its specific acceptance evidence in the PR. During implementation, use focused Release tests with Portia generation, the .NET AOT analyzer, and test conventions. Run the full applicable local gate once when the bundle is ready, then push its final head for exact-head CI and Native AOT on both architectures. Close only the children whose backend criteria passed. Do not split a capability into PRs for individual tests or layers. The initial R1-15, EN-01, and R1-01 bundle uses #152, #157, and #158. The separately reviewable fail-closed Portia composition slice is [EN-01a backend #324](https://github.com/bdgrz/compliance/issues/324); it blocks #157 and carries the same non-UI dependencies.

Keep the merged implementation trail in the [backend delivery ledger](backend-delivery-ledger.md). When a child or discovery issue is already complete, use a synthetic documentation PR to record its exact implementation or decision commit, acceptance evidence, and known limits. That PR improves tracking; it does not close an open child or substitute for missing backend proof.

Every story is a vertical slice from authorized API behavior through the usable browser experience. Its backend and frontend children track separate delivery and may close at different times; completing either child alone does not complete the story.

Every story must include:

- a named user and valuable outcome;
- a business objective;
- product requirements and business rules;
- observable acceptance criteria;
- server-enforced authorization;
- scoping to exactly one client organization (tenant), with no cross-tenant disclosure;
- usable loading, empty, error, retry, and forbidden states;
- traceable activity and historical behavior where the action matters to an audit;
- accessible UI and documented API behavior;
- focused automated acceptance evidence;
- a `Public references` section with direct, versioned links and the applicable
  use classification from `source-reference-policy.md`, or an explicit
  `No external normative source; product decision` statement linked to its
  internal decision record.

## Domain-coherence contract

Every story issue must include its `Domain slice` and `Implementation subtasks` from
this document. Those sections keep each vertical slice connected to the same
program, authorization, work, evidence, review, history, readiness, and
engagement model.

The domain slice identifies the records the story owns, the upstream records it
uses, and the downstream workflow it changes. It does not prescribe tables,
endpoints, event shapes, or component hierarchies.

The implementation subtasks are a cohesive delivery checklist inside the user
story, not independent backlog issues. A subtask is not complete merely because
one application layer exists. The story remains incomplete until the authorized
API-to-UI workflow and its effects on history and readiness work together.

No story may introduce a second representation of member identity, external
directory identity, ownership, evidence, review, finding, activity, or snapshot
when the shared domain model already owns that concept.

No story or subtask may use or copy a definition, schema, enum, example,
registry, test corpus, or semantic model merely because it is publicly
readable. The exact source must be approved by the standards-use register,
required notices must be retained, and any source with unclear or unacceptable
rights is excluded from the model and implementation.

## Priorities

- P0 - prove now: required for the first product release to complete readiness and make a Type I entry decision.
- P1 - build next: required to carry the proven program through Type I and Type II, or a readiness dependency that still needs validation.
- P2 - validate first: a useful product hypothesis that must not be scheduled until customer, advisor, auditor, or observed-workflow evidence justifies it.

## Definition of ready

A story is ready when its user, business objective, domain slice, terms, rules,
authorization, examples, acceptance criteria, dependencies, and explicit
exclusions are understood well enough to implement without inventing product
policy. Every domain term must agree with [domain-model.md](domain-model.md), and
any unresolved product decision that affects the story must be resolved or
explicitly excluded. Concretely, every M0 discovery or architecture issue that
blocks the story is resolved and incorporated into the story, and every enabler
it depends on is available or delivered with its first slice.
Every public reference is direct, accessible without credentials, versioned,
checked against the standards-use register, and accompanied by any required
copyright, attribution, patent, or redistribution decision.

## Definition of done

A story is done when its full API-to-UI workflow meets the acceptance criteria; allowed and denied behavior is tested; changes and decisions are traceable; period and snapshot behavior is correct; relevant failure states are recoverable; and the result works in the supported standalone and split-host deployments.

A backend child is done when its authorized HTTP and machine-appropriate MCP contracts, Portia authorization and guards, domain records, Fitz-backed projections and reactors, and applicable non-UI acceptance criteria are verified. Verification covers allowed and denied behavior, tenant isolation, concurrent and replayed work, projection lag, recoverable failure, and standalone and split API-worker deployment. Login, email verification, invitation acceptance, acknowledgements, attestations, approvals, and personal sign-offs remain HTTP-only. Focused and applicable full .NET and broker tests, formatting, and required exact-head CI checks must pass before the reviewed PR is merged. Link the merged PR and verification evidence from the backend child.

A frontend child is done when its accessible browser workflow consumes the authorized API, handles loading, empty, error, retry, and forbidden states, and passes focused browser acceptance and applicable repository checks. Link its merged PR and evidence from the frontend child. Close the product parent only after both delivery children and the integrated story acceptance pass.

## M0 - Design and discovery

Business outcome: every product and architecture decision that blocks a P0 story is recorded with rationale, owner, and date; shared domain ownership conflicts are resolved; blocked stories have been refined to incorporate those decisions; and no P0 story still depends on an unresolved validation. This milestone produces decisions, ADRs, and thin technical spikes, not business implementation.

Discovery and architecture issues do not deliver product behavior. Each lists the stories and enablers it blocks; those items are not ready until they incorporate the decision.

### M0-D01 Confirm the first engagement's scope, Trust Services categories, and target dates

Priority: P0

Type: Product discovery

Area: audit

Decision needed: What engagement are we preparing for first, which Trust Services categories and services are in scope, and which dates are targets versus confirmed?

The delegated product defaults and the remaining organization-owned facts are
separated in [the M0-D01 decision record](decisions/m0-d01-program-targets.md).
Program dates remain optional targets; no target becomes a confirmed auditor
date or a milestone due date without attributable evidence. The issue remains
open until management and the audit firm supply the first engagement facts.

Questions to answer:

- [ ] Confirm the path is readiness, then Type I, then Type II, and record the target Type I as-of date and intended Type II observation period.
- [ ] Decide which categories beyond Security are in scope (Availability, Processing Integrity, Confidentiality, Privacy) and the rationale.
- [ ] If Privacy is selected, decide whether a separate personal-information-lifecycle backlog is required before the product claims support.
- [ ] Identify the audit firm and which engagement dates it has confirmed.
- [ ] List the services in the first system boundary.
- [ ] Set due dates on the R1, R2, and T1 milestones once targets are agreed.

Involve: Compliance lead, readiness consultant, audit firm.

Blocks: R1-01, R1-02, R1-03, T1-01, T2-01

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Product brief open decisions (first engagement, categories, dates, optional categories); R1-03 subtask on engagement categories.

### M0-D02 Decide the criteria content source, edition, and permitted use

Priority: P0

Type: Product discovery

Area: controls

Decision needed: Which SOC 2 criteria content can the product store, display, map, and export, and from which authorized source?

Questions to answer:

- [ ] Confirm the edition: 2017 Trust Services Criteria with 2022 revised points of focus.
- [ ] Confirm AICPA permitted use for storing and displaying criteria text in the product, exports, and packages, or whether only identifiers plus customer-supplied text are allowed.
- [ ] Decide whether points of focus are modeled and mappable or reference-only.
- [ ] Identify who supplies the initial catalog file (for example, the consultant's workbook) and its identifiers.
- [ ] Decide how a later edition is introduced without changing existing engagements.
- [ ] Confirm the terms permit the firm to use the same criteria content across multiple client organizations and in client-facing exports and templates.

Involve: Compliance lead, readiness consultant; legal review of AICPA terms if needed.

Blocks: F1-04, R1-03, R1-06

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Product brief open decision on criteria source and permitted use; R1-03 requirements.

### M0-D03 Decide platform roles, separation of duties, and team membership rules

The platform operator is the platform super administrator for platform operations, held by an explicitly configured platform user. It is separate from tenant RBAC roles. The current implementation does not automatically grant tenant membership or client business-record access. Cross-tenant client-data authority remains a pending access, audit, and separation-of-duties decision.

Priority: P0

Type: Product discovery

Area: workspace

Decision needed: Which built-in roles, access scopes, and separation-of-duties exceptions does a small compliance team need, and who can hold responsibilities?

Questions to answer:

- [ ] Confirm the first built-in role catalog and the actions each role permits.
- [ ] Define the scope hierarchy (organization, program, engagement, shared resource) for grants.
- [ ] Decide the acceptable small-team self-review or self-approval exceptions and who approves them.
- [ ] Decide whether the first release needs one workspace or several collaboration workspaces per organization.
- [ ] Decide invitation behavior for Auth0 and Entra, and whether IdP group mapping is required for the first release.
- [ ] Decide whether responsibilities (for example, control owner) can be assigned to people who never sign in, and how their work is attributed.
- [ ] Multiple client organizations per deployment are now required (M0-D25). Define separate role catalogs or scopes for firm staff and client personnel, and whether a firm staff member's roles are granted per client organization or through engagement assignment.

Involve: Compliance lead, organization administrator, readiness consultant.

Blocks: EN-01, EN-04, R1-04, R2-01, R2-05, R2-11

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-04 validation subtask; domain model open decisions on roles, self-review, workspaces, IdP groups, invitations; R2-05 independence rules.

### M0-D04 Inventory existing readiness material and its import formats

Priority: P0

Type: Product discovery

Area: product

Decision needed: What readiness material already exists, in what shape, and which record families must be imported first to adopt the product mid-engagement?

Questions to answer:

- [ ] Collect sample files for controls, mappings, policies, evidence, owners, risks, vendors, gaps, and consultant findings.
- [ ] Record each source's stable identifiers, or the lack of them.
- [ ] Decide how owners named in source files (names, emails) match members or workforce people.
- [ ] Decide how consultant-authored content is attributed without fabricating platform actors.
- [ ] Rank record families by adoption value to set the R1-09 slice order.
- [ ] Decide which material is acceptable to re-enter manually instead of importing.

Involve: Compliance lead, readiness consultant.

Blocks: R1-09

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-09 validation subtask.

### M0-D05 Validate the application inventory and reviewed-system boundaries

Priority: P0

Type: Product discovery

Area: applications

Decision needed: What is the real application universe, which source is authoritative, and how do applications split into concrete reviewed systems?

Questions to answer:

- [ ] Obtain the current application list and name its authoritative, corroborating, and discovery-only sources.
- [ ] Agree on the minimum inventory fields and ownership expectations (system owner, access owner).
- [ ] For the first applications (for example, AWS accounts and GitHub organizations), define the reviewed-system boundary: tenant, organization, account, or environment.
- [ ] Define access-review inclusion and exclusion criteria and who approves them.
- [ ] Decide alias, duplicate, and retirement rules using real examples.

Involve: Compliance lead, system owners, readiness consultant.

Blocks: R1-10, R2-06

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-10 validation subtask; R2-06 validation subtask (application list).

### M0-D06 Decide the authoritative workforce source and NHI ownership rules

Priority: P0

Type: Product discovery

Area: workforce

Decision needed: Which source is authoritative for people, employment status, and managers, and how is non-human identity (NHI) ownership governed?

Questions to answer:

- [ ] Identify the authoritative people source (HRIS, payroll, spreadsheet) and any corroborating sources, with precedence.
- [ ] Agree on the minimum worker attributes and the privacy boundary for sensitive fields.
- [ ] Define joiner, mover, and leaver observation rules, including contractors and external collaborators.
- [ ] Decide who may own an NHI (person or team), the required purpose and review date, and how ownership is reviewed.
- [ ] Decide the accepted manual fallback when no system source exists.

Involve: Compliance lead, HR or people operations, engineering leadership.

Blocks: R1-11, R2-06, R2-10

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-11 validation subtask; R2-06 validation subtask (identity roster); R2-10 joiner-mover-leaver behavior.

### M0-D07 Validate the first access-review population, providers, and expectations

Priority: P0

Type: Product discovery

Area: access-review

Decision needed: What does the first real access review need to ingest, calculate, and decide so the R2-06 rules are grounded in actual provider data?

Questions to answer:

- [ ] Choose the first reviewed systems and collect sample exports from each provider.
- [ ] Catalog the principal kinds (account, group, role, service principal, workload identity) and entitlement shapes present.
- [ ] Define how nested groups, role assumption, and effective access are calculated for those providers.
- [ ] Define human and NHI classification rules and how ambiguous or shared accounts are handled.
- [ ] Write the initial access expectations, including the AWS no-IAM-user expectation and privileged entitlements.
- [ ] Confirm with the consultant which reviewer-assignment and remediation-verification evidence the auditor accepts.

Involve: Compliance lead, system owners, access reviewers, readiness consultant.

Blocks: R2-06

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R2-06 validation subtask.

### M0-D08 Define the minimum technology, information, and data-flow inventory

Priority: P0

Type: Product discovery

Area: inventory

Decision needed: What level of component, information-asset, location, classification, and data-flow detail do the first boundary and system description need?

Questions to answer:

- [ ] List the material component categories for the first boundary (cloud accounts, environments, networks, endpoint classes, repositories, data stores).
- [ ] Define the information classification scheme and retention expectations to record.
- [ ] Decide the granularity of data flows and their required protection expectations.
- [ ] Identify the sources (cloud console, MDM, repository host) and whether each is authoritative or discovery-only.

Involve: Compliance lead, engineering leadership, readiness consultant.

Blocks: R1-12

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-12 validation subtask.

### M0-D09 Identify service commitments, system requirements, CUECs, and CSOCs

Priority: P0

Type: Product discovery

Area: commitments

Decision needed: Which commitments, requirements, and complementary controls apply to the first engagement, from which source artifacts, and who approves them?

Questions to answer:

- [ ] Collect source artifacts: MSAs, SLAs, the security addendum, privacy notices, and policies.
- [ ] Draft the first list of service commitments and system requirements with owners.
- [ ] Draft the first list of CUECs and CSOCs with the consultant.
- [ ] Agree on the minimum fields and the approval authority.

Involve: Compliance lead, legal or contracts owner, readiness consultant.

Blocks: R1-13

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-13 validation subtask.

### M0-D10 Select the risk assessment method and acceptance authority

Priority: P0

Type: Product discovery

Area: risk

Decision needed: Which assessment method, scales, appetite, cadence, and acceptance authority will the first risk assessment use?

Questions to answer:

- [ ] Choose a qualitative or quantitative method and the likelihood and impact scales.
- [ ] Define materiality, risk appetite, and tolerance thresholds.
- [ ] Define the treatment vocabulary and the time-bounded acceptance authority.
- [ ] Set the periodic reassessment cadence and trigger events.
- [ ] Confirm the method with the readiness consultant.

Involve: Compliance lead, management approver, readiness consultant.

Blocks: R1-07

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-07 validation subtask.

### M0-D11 Define vendor materiality, due diligence, and subservice treatment

Priority: P0

Type: Product discovery

Area: providers

Decision needed: Which providers are material, what due diligence each needs, and how subservice organizations are treated in the boundary?

Questions to answer:

- [ ] Define the material-provider threshold and classify the current vendor list against it.
- [ ] Define the initial due-diligence evidence set and review cadence.
- [ ] Define which assurance-report fields to capture, including coverage gaps and bridge-letter use.
- [ ] Decide carve-out or inclusive treatment for each subservice organization, with its CSOCs.

Involve: Compliance lead, procurement or finance owner, readiness consultant.

Blocks: R1-14

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R1-14 validation subtask.

### M0-D12 Decide policy audiences, acknowledgement, and training evidence

Priority: P0

Type: Product discovery

Area: policies

Decision needed: Who must acknowledge which policies and complete which training, and what evidence proves it?

Questions to answer:

- [ ] Define the audience rule for each policy.
- [ ] Agree on acknowledgement language and reminder cadence.
- [ ] Identify required security-awareness training and its delivery source (LMS or manual).
- [ ] Define the exception policy and the joiner, mover, and leaver behavior for campaigns.
- [ ] Confirm the evidence format the auditor accepts for completion.

Involve: Compliance lead, policy owners, HR or people operations.

Blocks: R2-10

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R2-10 validation subtask.

### M0-D13 Define control evaluation procedures and tester independence

Priority: P0

Type: Product discovery

Area: controls

Decision needed: How are control design and implementation evaluated reproducibly before Type I, and who may perform and review evaluations?

Questions to answer:

- [ ] Collect the consultant's evaluation procedures for a representative set of controls.
- [ ] Define the assertions, inspected items, and result vocabulary for design, implementation, and evidence sufficiency.
- [ ] Define when a deviation requires a finding, corrective action, exception, or retest.
- [ ] Define tester competence and independence rules and the small-team exceptions (with M0-D03).

Involve: Compliance lead, readiness consultant.

Blocks: R2-05

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R2-05 validation subtask.

### M0-D14 Agree on the readiness-consultant collaboration workflow

Priority: P0

Type: Product discovery

Area: workspace

Decision needed: How do the readiness consultants want to review work in progress and return feedback?

Questions to answer:

- [ ] Ask whether the consultants prefer indexed handoffs, secure scoped links, direct product access, or their own platform.
- [ ] Define what 'consultant validated' means and how it differs from internal approval.
- [ ] Decide whether draft material may be shared, and under which approval.
- [ ] Define how feedback is returned and attributed, and whether sharing can be revoked.
- [ ] Our firm's advisors now work as platform members assigned to client organizations (M0-D25). Decide whether external consultants engaged directly by a client still need the handoff workflow.

Involve: Compliance lead, readiness consultant.

Blocks: R2-08

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R2-08 validation subtask; triage validation queue item 1.

### M0-D15 Define the daily work queue, reminders, and escalation

Priority: P0

Type: Product discovery

Area: workspace

Decision needed: What does the small team need from one accountable work view on day one?

Questions to answer:

- [ ] Identify the minimum source workflows the first queue must include.
- [ ] Define the priority and materiality ordering rules.
- [ ] Define the assignment actions (assign, claim, delegate, escalate) and who may perform them.
- [ ] Define reminder, digest, and escalation expectations, and whether email is required.

Involve: Compliance lead, control owners.

Blocks: R2-11

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R2-11 validation subtask.

### M0-D16 Decide evidence handling, retention, hold, and disclosure rules

Priority: P1

Type: Product discovery

Area: evidence

Decision needed: Which handling classes, retention periods, holds, redaction, and disposition rules apply to evidence and related artifacts?

Questions to answer:

- [ ] Define the handling classes and what may be stored at all (for example, never credentials).
- [ ] Set retention periods and the rules for engagement holds and legal holds.
- [ ] Define the redaction workflow and the disposition authority.
- [ ] Define the content-inspection or malware-scanning boundary and quarantine behavior.
- [ ] Set backup and recovery expectations for evidence.
- [ ] Confirm what the auditor expects to be shared, and how.

Involve: Compliance lead, security owner, legal.

Blocks: F1-01, R2-12

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: R2-12 validation subtask; product brief open decision on retention, legal hold, backup, and recovery.

### M0-D17 Confirm audit firm deliverables, formats, and auditor access

Priority: P1

Type: Product discovery

Area: audit

Decision needed: Which outputs, formats, and collaboration model does the audit firm require for Type I and Type II?

Questions to answer:

- [ ] Collect the required control matrix, evidence index, and system-description section formats.
- [ ] Collect the population, sample, and selection formats with stable identifiers, plus the treatment of late or corrected rows.
- [ ] Confirm the package, workbook, portal, archive, and naming expectations.
- [ ] Confirm the management assertion and representation-letter sequence, signers, and templates.
- [ ] Ask whether the auditor wants direct product access, exported packages, or their own platform.
- [ ] For clients the firm examines, our own attest team is the audit firm. Separate the formats our attest team needs (M0-D27) from those external audit firms require for advisory clients.

Involve: Compliance lead, management approver, audit firm.

Blocks: T1-02, T1-03, T1-05, T1-07, T3-02, T3-05, T3-06

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Validation subtasks in T1-02, T1-03, T1-05, T1-07, T3-02, T3-05, and T3-06; triage validation queue items 2 and 7.

### M0-D18 Establish success-measure baselines and targets

Priority: P1

Type: Product discovery

Area: product

Decision needed: What are today's baselines for the brief's candidate success measures, and which targets define product success?

Questions to answer:

- [ ] Measure the current time to identify missing, stale, rejected, or overdue audit work.
- [ ] Measure the current median time to answer an evidence request.
- [ ] Measure how much parallel spreadsheet or drive tracking the team does today.
- [ ] Agree on the target values and how each will be measured in the product.

Involve: Compliance lead, product owner.

Blocks: nothing directly; informs product measures

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Product brief: 'M0 must establish baselines and targets'.

### M0-D19 Validate whether governance and management reviews fit the control workflow

Priority: P2

Type: Product discovery

Area: audit

Decision needed: Are periodic policy, risk, vendor, and management reviews ordinary recurring controls, or do they need dedicated product surfaces?

Questions to answer:

- [ ] Observe the first real policy, risk, and vendor reviews using the general control-occurrence workflow.
- [ ] Observe one management compliance review and record what the general workflow could not support.
- [ ] Recommend keeping, merging, or closing T2-06 and T2-09.

Involve: Compliance lead, management approver.

Blocks: T2-06, T2-09

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Validation subtasks in T2-06 and T2-09; triage validation queue item 3.

### M0-D20 Discover inventory, access, and evidence integration sources

Priority: P2

Type: Product discovery

Area: automation

Decision needed: Which integrations would save meaningful work, and what must each source guarantee before we build a connector?

Questions to answer:

- [ ] Measure manual inventory, population, access, and evidence effort by source.
- [ ] For each candidate source, answer the ten integration questions in gap-analysis.md (business question, authority, identifiers, completeness, matching, proposals, least privilege, freshness, retained snapshots, manual fallback).
- [ ] Rank connectors by measured savings.

Involve: Compliance lead, engineering.

Blocks: T2-08

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: T2-08 validation subtask; gap analysis 'Next discovery: inventory data integrations'; triage validation queue item 6.

### M0-D21 Define compliance-facing significant change and incident facts

Priority: P1

Type: Product discovery

Area: risk

Decision needed: Which change and incident facts must Compliance hold to assess impact and describe the Type II period, without replacing source systems?

Questions to answer:

- [ ] Define the minimum change and incident fields and the source links to ITSM or the incident tool.
- [ ] Define the materiality threshold for 'significant change'.
- [ ] Define the restricted-detail boundary and which conclusions are shareable with the auditor.
- [ ] Define how a significant change affects the system description and closure.

Involve: Compliance lead, engineering leadership, security owner.

Blocks: T2-07

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: T2-07 validation subtask.

### M0-D22 Resolve ownership of shared identity and inventory concepts

Backend modeling decision recorded 2026-09-19 in
[M0-D22 canonical ownership](decisions/m0-d22-canonical-ownership.md).
Organization-specific workforce and NHI authority remain with M0-D06.

Priority: P0

Type: Product discovery

Area: product

Decision needed: Several concepts have two owners or none. Decide one owner and delivering story for each before implementation.

Questions to answer:

- [ ] ReviewedSystem: domain-model.md lists it under both Application inventory and External access governance. Choose one owning context.
- [ ] AccessSubject versus Person: R2-06 imports its own access-subject roster with employment status and manager, duplicating R1-11's workforce roster. Decide whether R2-06 uses R1-11's roster, or define the difference.
- [ ] NHI records: R1-11 owns NHI-owner relationships, yet AccessSubject is created by R2-06, which comes later. Decide where an NHI is first created.
- [ ] Missing entities: Service, Location, and Process or Procedure are referenced by the boundary, commitments, providers, and system description but never defined. Choose their owning context and delivering story.
- [ ] Incidents: R1-07 reassessment uses incidents, but IncidentReference is only defined in T2-07. Decide whether a minimal incident reference is needed in R1 or R2.
- [ ] Control-to-risk relationship: choose R1-05 (control applicability) or R1-07 (risk treatment) as the owner.
- [ ] Update the affected issue bodies and domain-model.md with the decisions.

Involve: Product owner, tech lead.

Blocks: R1-02, R1-05, R1-07, R1-10, R1-11, R1-12, R2-06

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Backlog design review 2026-09-14; domain-model.md bounded-context table; R1-05, R1-07, R1-11, R2-06, T2-07 domain slices.

### M0-D23 Define the assurance vocabulary and readiness ownership

Accepted product default, 2026-09-19:
[assurance vocabulary and readiness ownership](decisions/m0-d23-assurance-vocabulary.md).
Gap, provider coverage gap, control evaluation deviation, and finding remain
distinct linked records; waiver and auditor test exception have different
names; R1-07 owns risk acceptance; EN-04 shares a decision shape while each
workflow owns its state; R1-08 owns readiness rules. Firm-specific approver and
exception policy stays with M0-D03; engagement-specific thresholds stay with
their owning decisions.

Priority: P0

Type: Product discovery

Area: audit

Decision needed: Assurance terms overlap across stories. Define one vocabulary and one owner for readiness rules before building gaps, findings, reviews, or readiness.

Questions to answer:

- [ ] Gap (R1-08), Finding (R2-07), deviation (R2-05), and provider coverage gap (R1-14): decide whether these are one record with kinds or distinct records, and how they relate.
- [ ] 'Exception' currently means both an approved waiver (access expectation, separation of duties, occurrence) and an auditor-found test exception (T3-04). Rename one.
- [ ] Risk acceptance is owned by both R1-07 (RiskTreatment acceptance) and R2-07 (RiskAcceptance). Choose one owner.
- [ ] Review and approval: R2-05 owns Review and ReviewDecision, yet R1-02, R1-05, R1-06, R1-13, and R2-02 need approval earlier, and the domain model forbids a universal Review aggregate. Define the boundary of the shared decision primitive (EN-04).
- [ ] Readiness: R1-08 (ReadinessAssessment), R2-09 (ReadinessSnapshot), T2-04 (projection definitions), and T2-09 (ManagementReviewSnapshot) split one concern. Name the single rules owner and how the others reuse it.
- [ ] Update the affected issue bodies and domain-model.md with the decisions.

Involve: Product owner, tech lead, readiness consultant.

Blocks: EN-04, R1-07, R1-08, R1-14, R2-05, R2-07, R2-09, T2-04, T2-09

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Backlog design review 2026-09-14.

Public references: No external normative source; product decision recorded in
[M0-D23](decisions/m0-d23-assurance-vocabulary.md).

### M0-D24 Define the accessibility target and supported-browser baseline

Priority: P0

Type: Product discovery

Area: product

Decision needed: Which accessibility standard, conformance level, assistive-technology combinations, and browser versions must every first-release browser workflow support?

Questions to answer:

- [ ] Select the accessibility standard and conformance target, including any documented exceptions and approval authority.
- [ ] Name the supported desktop and mobile browsers and the version-support policy.
- [ ] Choose the keyboard, focus, contrast, zoom, screen-reader, reduced-motion, and error-announcement acceptance baseline.
- [ ] Define the automated and manual evidence required for a story to satisfy the accessible-UI contract.
- [ ] Decide how unsupported browsers and known accessibility limitations are communicated and tracked.

Involve: Product owner, design, engineering, compliance lead, and representative users or an accessibility specialist.

Blocks: frontend children for every story and first product delivery slice. It does not block backend children.

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] The product-backlog contract, test strategy, browser support statement, and affected story acceptance criteria reflect the decision.
- [ ] Every scheduled frontend child records M0-D24 as a blocker; backend children omit it. Later product slices depend on their first slice directly or transitively.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Product brief open decision on accessibility targets and supported browsers; backlog design review 2026-09-14.

### M0-D25 Define the client tenancy boundary, firm-staff affiliation, and data ownership

Platform operators are super administrators for platform operations, including organization lifecycle and a paginated platform tenant portfolio. The current implementation does not automatically grant tenant membership or client business-record access. Cross-tenant client-data authority remains a pending decision.

Priority: P0

Type: Product discovery

Area: tenancy

Decision needed: Each client organization is a tenant and there is no firm entity. Decide what that means for membership, record ownership, and anything that must live outside a tenant.

Questions to answer:

- [ ] Confirm the client organization is the only tenant boundary and that every business record belongs to exactly one organization.
- [ ] Define which content is platform-level rather than tenant-owned (criteria catalog editions, firm templates, the firm-staff directory) and how tenants reference it by version without copying client data back out.
- [ ] Define membership affiliation (client personnel or firm staff) and where a firm staff member's practice designation (advisory, attest) is recorded without a firm entity.
- [ ] Decide where firm-owned material lives inside a client organization (advisory working notes, future attest documentation), who can see it, and how it is retained when the client leaves.
- [ ] Decide who may create, suspend, and offboard organizations (platform operators) and how each client's first administrator is bootstrapped.
- [ ] Decide who may create an organization in the sign-in flow (select or create): only platform operators, as R1-15 currently requires, or also self-service users.
- [ ] Settle the vocabulary: the product proposes `tenant_id` in APIs while the domain model says organization. Choose one term, or define tenant as the technical name of a client organization everywhere.
- [ ] Decide whether firm staff reach client records only through engagement assignment or also through standing access.
- [ ] Record the triggers for revisiting the no-firm-entity decision, for example hosting more than one firm or firm-level retention obligations that cannot live inside client tenants.

Involve: Firm leadership, product owner, tech lead.

Blocks: M0-D26, EN-01, F1-01, F1-04, F1-07, R1-15

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Multi-client firm decision 2026-09-14: clients are the only tenant, firm staff and client users both sign in.

### M0-D26 Define independence rules for advisory and attest services

Priority: P1

Type: Product discovery

Area: tenancy

Decision needed: The firm both advises and examines clients. Decide which independence requirements the platform must enforce and which remain firm quality-management procedures.

Questions to answer:

- [ ] Identify the governing requirements with the firm's independence or quality-management partner, including the AICPA Code of Professional Conduct independence rules for nonattest services and the firm's system of quality management.
- [ ] Classify each advisory service (readiness assessment, control design, implementation, operating controls on the client's behalf, vCISO) as compatible, conditionally compatible, or impairing for a client the firm also examines.
- [ ] Define look-back and cooling-off periods and whether they apply per client, per person, or per engagement.
- [ ] Define staff separation: who may serve both practices and which advisory material attest staff may see.
- [ ] Define what engagement acceptance must document (independence evaluation, management-responsibility acknowledgement for nonattest services, approver).
- [ ] Decide how an advisory client that later requests an examination is evaluated.

Involve: Firm leadership, independence or quality-management partner, legal counsel.

Depends on: M0-D25

Blocks: F1-07, F1-08

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Multi-client firm decision 2026-09-14: advisory and attest services with independence walls.

### M0-D27 Decide the scope of attest engagement support in the platform

Priority: P1

Type: Product discovery

Area: audit

Decision needed: Should the platform support the firm's own SOC 2 examinations (planning, tests of controls, sampling, workpapers, report drafting), or should the attest team keep dedicated audit software and use the platform only to collaborate with clients?

Questions to answer:

- [ ] Inventory the attest team's current audit software, workpaper review, and documentation retention obligations.
- [ ] Decide whether tests of controls, deviations, and report drafting are in product scope and how they stay separate from management's records.
- [ ] Decide how attest documentation is retained when a client offboards, given that client organizations are the only tenant (M0-D25).
- [ ] Decide how the attest team receives populations, samples, and evidence (in-product access or package handoff), and update T1-03, T1-04, T3-02, and T3-03 accordingly.
- [ ] Decide whether the product-brief non-goal of not replacing auditor workpaper systems still stands.

Involve: Firm attest leadership, independence or quality-management partner, product owner.

Blocks: F1-01, T1-03

Done when:

- [ ] The decision, rationale, decision owner, and date are recorded in the issue.
- [ ] Each blocked story's requirements, domain slice, and acceptance criteria reflect the decision, or the open point is explicitly excluded from that story.
- [ ] Remaining uncertainty is captured as a follow-up discovery issue rather than left implicit.

Source: Multi-client firm decision 2026-09-14; product-brief and gap-analysis non-goals on auditor workpapers.

### M0-D28 Approve the canonical entity and relationship model with usable public sources

Priority: P0

Type: Product discovery

Area: product

Decision needed: Which provider-neutral entities, relationships, identifiers,
cardinalities, and semantic distinctions define Compliance data, and which
public standards may safely inform an Apache-2.0 implementation?

Questions to answer:

- [ ] Define every canonical entity and first-class relationship needed by the
  first release, including organization, person, work relationship, platform
  user, membership, application, system instance, device, compute instance,
  account, service identity, group, group member, role, entitlement, access
  assignment, information asset, provider, and provenance records.
- [ ] For each entity and relationship, record its canonical meaning, stable
  identity, required and optional attributes, allowed reference types,
  cardinalities, time semantics, lifecycle vocabulary, and invariants without
  deciding aggregate, transaction, storage, API, or service boundaries.
- [ ] Distinguish person from employee/work relationship, platform user from
  membership and external account, application from system instance, physical
  device from compute instance, group from `GroupMember`, role from group and
  responsibility, direct assignment from effective access, and source
  observation from governed fact.
- [ ] Map each supported source shape to canonical entities without making a
  provider-specific schema canonical or losing the original source identity.
- [ ] Give every standards-derived statement a direct, versioned public
  reference and a use classification from
  [source-reference-policy.md](source-reference-policy.md).
- [ ] Verify the implementation, copyright, attribution, redistribution,
  trademark, and material patent terms for the exact source and version. Exclude
  any information whose use is not affirmatively acceptable for this
  Apache-2.0 product.
- [ ] Update `canonical-entity-model.md`, `domain-model.md`, all blocked issue
  bodies, delivery slices, and implementation subtasks to use the approved
  vocabulary and links.

Involve: Product owner, domain lead, tech lead, security lead, and counsel for
any source whose rights are not explicit.

Blocks: EN-01, EN-05, R1-04, R1-10, R1-11, R1-12, R1-15, R2-06

Done when:

- [ ] The canonical entity and relationship catalog is complete for every
  blocked story, internally consistent, and approved with rationale, owner, and
  date.
- [ ] Every entity and relationship has public semantic references or is
  explicitly labeled an original product decision.
- [ ] Every active reference appears in the approved-source register with exact
  version and use terms; excluded sources contribute no model or implementation
  information.
- [ ] The model explicitly defers aggregate, transaction, persistence, API, and
  service-boundary design.
- [ ] Each blocked story and first delivery slice incorporates the approved
  terms, references, mappings, and conformance tests in its domain slice,
  acceptance criteria, and implementation subtasks.

Public references:

- [Canonical entity model](canonical-entity-model.md)
- [Public source reference and standards-use policy](source-reference-policy.md)
- The policy's approved reference register contains the exact public standards,
  versions, and use terms; no unregistered source is normative for this issue.

Source: Canonical-model design review 2026-09-14; M0-D22 shared identity and
inventory conflicts; R1-04, R1-10, R1-11, R1-12, R1-15, and R2-06 domain slices.

### M0-A01 ADR: Persistence, versioning, and effective-dated history

Priority: P0

Type: Architecture decision

Area: architecture

Context: The repository uses Portia and Fitz event streams. The domain model
requires stable identities, immutable approved versions, successor proposals,
effective intervals, optimistic concurrency, and distinct occurred, effective,
covered, and observed times.

Questions to answer:

- [x] Document what Portia and Fitz provide for durable state, event logs, queries, and transactions in [ADR 0003](../architecture/decisions/0003-event-sourced-history-and-effective-versions.md).
- [x] Choose event-sourced rather than relational-temporal or hybrid authoritative storage, with rationale in ADR 0003.
- [x] Choose opaque UUID identities, immutable approved versions, and distinct occurred, effective, covered, and observed times.
- [x] Choose optimistic stream concurrency with a transient conflict contract.
- [x] Define organization isolation through immutable tenant realms and tenant-scoped projections.
- [x] Define versioned events and projections, readers-before-writers migration, whole-platform recovery scope, a 15-minute RPO, and a 4-hour RTO. Portia/Fitz owns recovery plumbing; DevOps owns timed operational proof.
- [x] Choose logical tenant partitions in the event store; physical per-tenant restore is out of scope, while export and deletion remain application workflows.

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Blocks: M0-A02, M0-A05, M0-A06, EN-02, EN-03

Done when:

- [x] The accepted ADR, including options considered and consequences, is committed under `docs/architecture/decisions/`.
- [x] Thin standalone and split API/worker probes prove durable source restart, local-volume restore, and projection replay in PRs #321 and #328.
- [x] The blocked architecture records and enablers reference the accepted decision; production recovery delivery stays in [cntryl/portia#70](https://github.com/cntryl/portia/issues/70).

Source: domain-model.md 'History, snapshots, and time'; backlog design review.

### M0-A02 ADR: Snapshots, content identity, and amendments

Priority: P0

Type: Architecture decision

Area: architecture

Context: Seven snapshot types (workforce, population, readiness, Type I baseline, period open, period close, management review) must be immutable, reproducible, and amendable through linked records.

Questions to answer:

- [ ] Choose how snapshots are represented: references to immutable versions plus content hashes, or materialized copies.
- [ ] Define canonical serialization and hashing for content identity.
- [ ] Define amendment records and how downstream impact is identified.
- [ ] Define how deterministic regeneration (equivalent package manifests) is guaranteed.
- [ ] Set size and performance expectations for large access and audit populations.

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Depends on: M0-A01

Blocks: EN-03

Done when:

- [ ] The ADR, including options considered and consequences, is accepted and committed under `docs/architecture/decisions/`.
- [ ] A thin spike proves the decision in both standalone and split API/worker host modes.
- [ ] Each blocked enabler or story is updated to reference the decision.

Source: domain-model.md snapshot rules; T1-01, T3-01, T1-05 acceptance criteria.

Technical evidence: [ADR 0008](../architecture/decisions/0008-snapshot-manifest-regeneration.md)
defines a proposed retained-source regeneration operation for the first
program-scope consumer. It recomputes the retained v1 manifest after Portia
tenant authorization rather than reading a snapshot projection or mutable
current sources. The operation limits each amendment lineage to eight links and
each regeneration to nine snapshot hydrations. This evidence does not accept
the ADR or settle the remaining snapshot types, workforce consumer,
size/performance, package, signing, retention, or recovery requirements; M0-A02
and EN-03 remain open.

### M0-A03 ADR: Evidence and artifact storage, inspection, and access

Priority: P0

Type: Architecture decision

Area: architecture

Context: Evidence, policy files, provider reports, and packages need immutable content identity, safe handling, and per-artifact authorization.

Questions to answer:

- [ ] Choose the blob store and content-addressed layout, with encryption at rest.
- [ ] Set the upload size limits and resumable upload behavior.
- [ ] Define content validation, malware inspection, and quarantine.
- [ ] Choose the download authorization model (proxied or short-lived signed URLs) and delivery logging.
- [ ] Define storage-level hooks for redacted derivatives, retention, holds, and disposition.
- [ ] Confirm deduplication never crosses organizations.
- [ ] Define per-organization storage partitioning and encryption keys, and per-organization export and disposition.

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Blocks: EN-06

Done when:

- [ ] The ADR, including options considered and consequences, is accepted and committed under `docs/architecture/decisions/`.
- [ ] A thin spike proves the decision in both standalone and split API/worker host modes.
- [ ] Each blocked enabler or story is updated to reference the decision.

Source: R2-03, R2-12, T1-05 requirements.

### M0-A04 ADR: Authorization and organization isolation

Priority: P0

Type: Architecture decision

Area: architecture

Context: Authorization combines active membership, a scoped grant, responsibility, resource state, and separation of duties, and must filter lists, counts, search, notifications, and exports.

Questions to answer:

- [ ] Choose the evaluation model and where it is enforced for commands and queries.
- [ ] Define how restricted records are excluded from lists, counts, search, notifications, and exports.
- [ ] Define field-level restrictions for workforce and evidence data.
- [ ] Choose a policy engine or an in-code policy approach that is compatible with Native AOT.
- [ ] Define logging of denied actions and cross-organization isolation tests.
- [ ] Choose the architecture test that fails when an endpoint lacks an explicit policy.
- [ ] Define how authorization evaluates a platform user with memberships in several organizations, how firm-staff access to a client is granted and revoked through engagement assignment, and how independence compartments (M0-D26) are enforced.

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Blocks: EN-01

Done when:

- [ ] The ADR, including options considered and consequences, is accepted and committed under `docs/architecture/decisions/`.
- [ ] A thin spike proves the decision in both standalone and split API/worker host modes.
- [ ] Each blocked enabler or story is updated to reference the decision.

Source: domain-model.md 'Responsibility and separation of duties'; R1-04 and R2-11 acceptance criteria.

### M0-A05 ADR: Read models, projections, and as-of calculations

Priority: P0

Type: Architecture decision

Area: architecture

Context: Readiness, the work queue, and every status count must reconcile to source records, show an as-of time, and be reproducible.

Questions to answer:

- [ ] Choose the projection strategy (synchronous or asynchronous) and consistency guarantees.
- [ ] Define as-of calculation identity and reproducibility for historical readiness.
- [ ] Define the stale-data and calculation-failure states shown to users.
- [ ] Define authorization-aware projections.
- [ ] Define background work over Fitz with parity between standalone and split API/worker hosts.
- [ ] Define organization-scoped projections, and how a later cross-client portfolio or work queue is authorized without leaking restricted client data.

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Depends on: M0-A01

Blocks: F1-02, R1-08, R2-11

Done when:

- [ ] The ADR, including options considered and consequences, is accepted and committed under `docs/architecture/decisions/`.
- [ ] A thin spike proves the decision in both standalone and split API/worker host modes.
- [ ] Each blocked enabler or story is updated to reference the decision.

Source: R1-08, R2-11, T2-04 acceptance criteria.

Decision: accepted 2026-09-22 in [ADR 0007](../architecture/decisions/0007-read-models-projections-and-as-of-calculations.md).
Tenant-scoped asynchronous Portia projections with transactional checkpoints,
retained-source replay, authorization before any read, and a retryable
transient conflict instead of a stale success for anchored or derived reads.
Cross-client reads are denied by default. The dedicated standalone and split
API/worker spike is `ProjectionReadConsistencyE2ETests`. Readiness rules and
calculation states stay with R1-08, work-queue semantics with M0-D15 and
R2-11, and cross-client portfolio authority with F1-02 under M0-A04, M0-D25,
and M0-D26.

### M0-A06 ADR: Import, reconciliation, and background processing

Status: accepted 2026-09-22 as [ADR 0005](../architecture/decisions/0005-import-reconciliation-and-background-processing.md). By product-owner decision, the thin standalone/split-host spike moved to EN-05 backend [#195](https://github.com/bdgrz/compliance/issues/195).

Priority: P0

Type: Architecture decision

Area: architecture

Context: Every import or collection needs preview, explicit acceptance, partial-failure semantics, provenance, tombstones, and safe replay.

Questions to answer:

- [ ] Define the staged import model: upload, parse, validate, preview, accept, cancel.
- [ ] Define atomic versus explicitly accepted-subset semantics.
- [ ] Define idempotency keys, source identity, and replay detection.
- [ ] Define missing-row and tombstone reconciliation states.
- [ ] Define worker job orchestration, retries, progress reporting, and large-file handling across host modes.
- [ ] Ensure every job, message, retry, and progress report carries and verifies its organization context.

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Depends on: M0-A01

Blocks: EN-05

Done when:

- [ ] The ADR, including options considered and consequences, is accepted and committed under `docs/architecture/decisions/`.
- [ ] A thin spike proves the decision in both standalone and split API/worker host modes.
- [ ] Each blocked enabler or story is updated to reference the decision.

Source: domain-model.md cross-story integration rules; R1-09, R1-10, R1-11, R2-06 requirements.

### M0-A07 ADR: Tenant identity, federation, and tenant context

Technical default recorded 2026-09-19 in
[the client-tenancy ADR](../architecture/decisions/0001-client-tenancy-and-operator-provisioning.md):
the URL tenant ID is authoritative even when a JSON body supplies another,
tenant streams and projections retain that ID, platform grants never come from
IdP claims, and OIDC continuation does not silently link a second identity
from an existing session. A broker with client-specific connections is the
chosen expansion direction. Client slug naming and log retention, the reviewed
identity-linking flow, and production federation rollout remain open.

Priority: P0

Type: Architecture decision

Area: architecture

Context: Client organizations become tenants. Firm staff hold memberships in several organizations, and client users may authenticate through their own identity providers. The application currently trusts one OIDC authority and has no tenant context. Proposed direction: after sign-in, a user selects one of their organizations (or, if authorized, creates one). Browser routes identify the organization by a unique, URL-friendly slug (for example `/acme-corp/controls`); every organization-scoped API identifies it by an opaque, immutable `tenant_id` path parameter (for example `/api/v1/tenants/{tenant_id}/controls`). The ADR confirms or amends this direction.

Questions to answer:

- [ ] Confirm the proposed routing: slug-based browser routes and `tenant_id` path parameters on organization-scoped APIs; the server verifies membership for the `tenant_id` on every request and never trusts an organization identifier in a request body.
- [ ] Implement the slug rules defined in R1-15 with one reserved-route registry shared by server validation and the SPA router, plus an automated check that fails when a new top-level server or client route is missing from the registry or equals an existing organization slug.
- [ ] Define slug changes: redirect history for members, and a rule that a retired slug is never reassigned to another organization so old links cannot land in a different client's tenant.
- [ ] Define slug resolution without enumeration: the browser resolves slug to `tenant_id` from the signed-in user's own membership list, and an unknown slug and a slug the user cannot access produce the same not-found result.
- [ ] Decide whether slugs may identify clients by name, given client confidentiality in URLs, browser history, logs, and referrer headers, and set the referrer policy accordingly.
- [ ] Choose the opaque `tenant_id` format (non-sequential) and confirm it is the only organization identifier in API contracts, logs, jobs, and events.
- [ ] Choose the approach for many client identity providers: a broker with per-organization connections (for example Auth0 Organizations or Entra External ID) or multiple trusted issuers.
- [ ] Define the platform user that binds issuer plus subject once and holds memberships in several organizations, and how one person with two identities is handled.
- [ ] Decide how organization context propagates through Fitz messages, background jobs, caches, logs, and telemetry.
- [ ] Decide whether any token claim may carry organization membership or roles, or whether the platform remains the only authority.
- [ ] Define the automated cross-tenant leak test strategy (APIs, search, counts, artifacts, projections, jobs, notifications, exports).

Involve: Tech lead, engineers; product owner for trade-offs that affect product rules.

Blocks: EN-01, F1-06, R1-15

Done when:

- [ ] The ADR, including options considered and consequences, is accepted and committed under `docs/architecture/decisions/`.
- [ ] A thin spike proves the decision in both standalone and split API/worker host modes.
- [ ] Each blocked enabler or story is updated to reference the decision.

Source: Multi-client firm decision 2026-09-14; README authentication section.

## R1 - Readiness program scoped

Business outcome: the team has an agreed system boundary; authoritative workforce context; application, technology, and information inventories; service commitments and system requirements; criteria; roles; controls; risks; providers; and an owned gap plan. The shared platform primitives (authorization, versioned records, review decisions, snapshots, import, and artifact storage) are proven through their first consuming stories. Nothing in this milestone claims audit readiness or an auditor opinion.

Shared enablers are delivered in this milestone and proven through their first consuming stories.

### R1-01 Start a SOC 2 program and see the path to Type II

Priority: P0

Area: program

User story: As a compliance lead, I want to start a SOC 2 program with our current stage and target journey so that the team shares one plan from readiness through Type I and Type II.

Business objective: replace an informal collection of tasks with a visible program, target dates, accountable team, and staged definition of progress.

Requirements:

- Capture organization, program name, current stage, target audit path, target dates, readiness advisor, and audit firm when known.
- Show the stage sequence and the outcome required to advance.
- Preserve the program across multiple audit engagements and periods.
- Distinguish a target date from a confirmed auditor date.
- Create the program inside exactly one client organization; a program never spans organizations.

Domain slice:

- Owns the `Program`, its stage plan, target dates, and continuing identity.
- Uses the owning `Organization` and the initiating member's active access grant.
- Establishes the program scope inherited by controls, work, engagements, and
  readiness views without creating separate copies of the program.

Acceptance criteria:

- [ ] Given an authorized compliance lead, when a program is created, then the team can see its current stage, next stage, target dates, and unresolved setup work.
- [ ] Given a saved program, when its target dates or advisor change, then the current plan updates and the prior values remain traceable.
- [ ] Given an incomplete setup, the product identifies the exact decisions required before the readiness assessment can begin.
- [ ] A user without program-administration rights cannot create or alter the program.
- [ ] A program and everything it owns are visible only within its client organization, including to firm staff who serve other clients.

Implementation subtasks:

- [ ] Define program identity, stage-transition rules, date semantics, and traceable plan revisions in the shared domain model.
- [ ] Deliver authorized create, view, and revise behavior through the API, persistence, and browser workflow, including all required failure states.
- [ ] Project unresolved setup work from real downstream records rather than a second checklist or manually assigned completion status.
- [ ] Prove the complete flow, denied behavior, revision history, and standalone/split-host parity with focused acceptance tests.

### R1-02 Define the system boundary and intended audit scope

Priority: P0

Area: audit scope

User story: As a compliance lead, I want to define what services, people, technology, data, locations, and third parties are in scope so that the team assesses the right system.

Business objective: prevent wasted work and misleading coverage caused by an ambiguous or shifting audit boundary.

Requirements:

- Describe services and relate the applicable commitments, people, applications,
  system components, information, data flows, processes, locations, and external
  dependencies from their governed inventories.
- Select intended Trust Services categories and readiness, Type I, or Type II context.
- Record inclusions, exclusions, assumptions, subservice organizations, and unresolved questions.
- Support internal review and advisor validation without treating either as an audit opinion.

Domain slice:

- Owns the versioned `SystemBoundary`, its inclusions, exclusions, assumptions,
  questions, review, and approval decisions.
- Uses the program, intended engagement stage, and referenced commitments,
  workforce, applications, components, information, data flows, locations, and providers.
- Supplies versioned scope references to criteria selection, controls, risks,
  evidence, engagements, and impact analysis.
- Sequencing: a boundary version may record services, people, technology, information, and providers as explicit unresolved references before their governed inventories exist. R1-10 through R1-14 replace those references with governed relationships, and each replacement goes through this story's impact preview and review.
- Owns the client `Service` identity and uses the governed `SystemInstance`, `Person`, `ServiceIdentity`, `Location`, and operational `Process` identities assigned in M0-D22; unresolved references cannot claim a governed record exists.

Acceptance criteria:

- [ ] The team can create, review, and approve a versioned system-boundary statement.
- [ ] Every inclusion, exclusion, and assumption has an owner and rationale.
- [ ] Later boundary changes identify affected controls, evidence, risks, vendors, and engagements before approval.
- [ ] Historical engagements retain the exact boundary version that applied to them.

Implementation subtasks:

- [ ] Define boundary versions, structured scope relationships, review decisions, and impact-analysis invariants without reducing the boundary to an unversioned document.
- [ ] Deliver authorized author, review, approve, and revise behavior through the API and accessible browser workflow.
- [ ] Connect boundary changes to affected controls, evidence, risks, vendors, readiness, and engagement snapshots before approval.
- [ ] Prove version selection, impact preview, denied review, concurrent revision, and historical snapshot behavior end to end.

Backend delivery is split between the [R1-02a boundary baseline](https://github.com/bdgrz/compliance/issues/162)
and [R1-02b downstream impact completion](https://github.com/bdgrz/compliance/issues/246).
The baseline exposes an incomplete preview and blocks successor approval while
downstream contexts are absent. The second child depends on the owning control,
evidence, risk, provider, readiness, and engagement records. Boundary-consuming
backend children use the baseline without requiring the second child first.
The [frontend child](https://github.com/bdgrz/compliance/issues/178) depends on
both backend slices. The product story remains open for the complete workflow.

### R1-03 Select a traceable SOC 2 criteria catalog

Priority: P0

Area: criteria

User story: As a compliance lead, I want to select the authorized criteria edition used by our engagement so that our readiness work is based on a known source rather than a stale spreadsheet.

Business objective: make coverage defensible and avoid false confidence from incomplete, unlicensed, or incorrectly identified framework content.

Requirements:

- Record catalog source, edition, provenance, permitted use, stable criterion identifiers, and import time.
- Let the team choose the categories and criteria intended to be in scope.
- Require an explicit support and gap review for each selected optional category;
  selecting Availability, Processing Integrity, Confidentiality, or Privacy
  cannot by itself make the program or product appear ready.
- Preserve a stable catalog snapshot for each engagement.
- Visibly distinguish source criteria from organization-authored guidance and mappings.
- Treat a catalog edition as platform-level content shared across client organizations; each organization's criteria selection, guidance, and mappings remain tenant-owned.

Domain slice:

- Owns `CriteriaCatalogEdition`, `Criterion`, category, source provenance,
  permitted-use metadata, and program criteria selection.
- Uses the program's intended scope and imports accepted through a provenance-aware import batch.
- Supplies stable criterion identities and editions to mappings, readiness rules,
  engagement snapshots, and packages.

Acceptance criteria:

- [ ] An authorized user can import or select a catalog and review its provenance before using it.
- [ ] Missing identifiers, duplicate criteria, and incompatible revisions are rejected with actionable explanations.
- [ ] A newer catalog never silently changes an existing engagement.
- [ ] Users can browse and filter the selected criteria by identifier, category, and scope status.
- [ ] A selected optional category exposes missing category-specific controls, workflows, evidence, or product support as gaps rather than silently treating the shared model as complete.
- [ ] One organization's criteria selection, guidance, and mappings are never visible to or changed by another organization using the same catalog edition.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D01 and M0-D02 before finalizing this story's rules.
- [ ] Define catalog-edition identity, criterion identity, import validation, permitted-use metadata, explicit selection, and product-support gap rules.
- [ ] Deliver authorized import or selection, preview, browse, and scope behavior through the API and browser, including incompatible-revision failures.
- [ ] Bind mappings and engagement snapshots to exact catalog editions while keeping organization guidance separate from source text.
- [ ] Prove duplicate and missing identifier handling, edition stability, authorization, filtering, and snapshot preservation end to end.

### R1-04 Invite the compliance team and assign responsibilities

Priority: P0

Area: collaboration

User story: As a compliance lead, I want to invite our small team through our identity provider and assign appropriate responsibilities so that work can be delegated without giving everyone administrative access.

Business objective: create accountable collaboration with least privilege and practical separation of duties.

Requirements:

- Support compliance lead, control owner, evidence contributor, reviewer, access reviewer, management approver, and external advisor responsibilities.
- Allow one person to hold multiple responsibilities while making conflicts visible.
- Scope access to the organization, program, engagement, and assigned work as appropriate.
- Define behavior for changed group membership, deprovisioned users, and orphaned assignments.
- Distinguish client personnel from firm staff; a firm staff member may hold memberships in several client organizations, each with its own grants and responsibilities.

Domain slice:

- Owns platform `Member`, `ExternalIdentity`, `Team`, `TeamMembership`,
  built-in `AccessRole`, scoped `AccessGrant`, and revocation history.
- Keeps authentication identity, platform membership, platform teams, access
  roles, and record-specific responsibilities distinct. Issuer plus subject is
  the provider identity; email and display claims are not identifiers.
- Uses external provider claims only through explicit identity or group
  mappings. It never treats an external directory principal being audited as a
  platform member without a deliberate correlation.
- Supplies active actors and authorization decisions to every program workflow;
  deprovisioning blocks new access while preserving attribution and surfacing
  orphaned responsibilities.
- `Member` is an organization-local membership of a platform user. One external identity binds to one platform user, who may hold memberships in several organizations (M0-D25, M0-A07).

Acceptance criteria:

- [ ] An administrator can invite or activate an externally authenticated user and explain the access being granted.
- [ ] Each user sees only the programs, records, and actions allowed by their responsibilities.
- [ ] Assignment conflicts are surfaced before approval or review work is accepted.
- [ ] Removing access takes effect immediately, preserves authorship history, and exposes work requiring reassignment.
- [ ] A firm staff member's grants and responsibilities in one client organization never apply in another.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D03 and M0-D28 before finalizing this story's rules; use the canonical identity entities and the approved public references without deciding aggregate boundaries.
- [ ] Define member and identity-binding lifecycles, explicit group mappings, team membership, access grants, revocation, actor attribution, and responsibility boundaries.
- [ ] Deliver provider-authenticated activation, member/team administration, scoped authorization, reassignment warnings, and access explanations through the API and browser.
- [ ] Enforce every allow and deny decision on the server, including direct grants, team grants, removed provider groups, suspension, deprovisioning, and separation-of-duties conflicts.
- [ ] Prove identity replacement, immediate revocation, historical attribution, orphaned-work recovery, forbidden UI states, and standalone/split-host parity end to end.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R1-04a Activate provider-authenticated members and explain their access

Outcome: As an organization administrator, I can invite or activate an externally authenticated user, bind their provider identity, and grant a built-in role with a clear explanation of the access it gives.

Acceptance criteria:

- [ ] An administrator can invite or activate an externally authenticated user and explain the access being granted.
- [ ] The provider identity is keyed by issuer plus subject; an email or display-name change does not create a new member.
- [ ] Replacing or adding a provider identity preserves the member's authorship and assignments.
- [ ] Pending and active member lifecycle states are visible, with their loading, empty, error, and forbidden states.

Not in this slice:

- Teams and IdP group mapping (R1-04c).
- Suspension and deprovisioning (R1-04d).

#### R1-04b Scope each member's access to organization, program, and engagement

Outcome: As a member, I see only the programs, records, and actions my access grants allow, and the server enforces the same boundary.

Acceptance criteria:

- [ ] Each user sees only the programs, records, and actions allowed by their responsibilities.
- [ ] Every grant records its scope, source, grantor, effective interval, and revocation.
- [ ] Out-of-scope commands and queries are denied on the server, and the UI shows a usable forbidden state.

Not in this slice:

- Separation-of-duties conflicts (R1-04e).

Depends on: R1-04a

#### R1-04c Organize members into teams and map identity-provider groups when required

Outcome: As an organization administrator, I can manage platform teams that receive grants and responsibilities, and map IdP groups to them explicitly if M0-D03 requires it for the first release.

Acceptance criteria:

- [ ] A team grant applies to its current members and stops applying when a member leaves the team.
- [ ] A team can receive access or responsibility but is never recorded as the actor of an action.
- [ ] If IdP group mapping is in scope: the mapping is explicit and reviewable, and removing a provider group removes the derived grant.

Not in this slice:

- Mapping external groups that are under access review (R2-06).

Depends on: R1-04a

#### R1-04d Suspend or deprovision members and recover orphaned work

Outcome: As an organization administrator, I can suspend or deprovision a member so their access ends immediately, while their history is preserved and their open work is exposed for reassignment.

Acceptance criteria:

- [ ] Removing access takes effect immediately, preserves authorship history, and exposes work requiring reassignment.
- [ ] A suspended or deprovisioned member's historical actions still show the correct attribution.

Not in this slice:

- Queue-based reassignment actions (R2-11b).

Depends on: R1-04a

#### R1-04e Surface separation-of-duties conflicts before assignments are accepted

Outcome: As a compliance lead, I can see when one person holds conflicting responsibilities, and the product blocks or records an approved exception before approval or review work is accepted.

Acceptance criteria:

- [ ] Assignment conflicts are surfaced before approval or review work is accepted.
- [ ] One person may hold several responsibilities, and conflicts are visible.
- [ ] Approved small-team exceptions record their approver, rationale, and interval, following M0-D03.

Not in this slice:

- Review decision semantics (EN-04).

Depends on: EN-04, R1-04a

### R1-05 Build the control inventory and implementation narratives

Priority: P0

Area: controls

User story: As a compliance lead, I want to document the controls our organization actually performs so that readiness is evaluated against real operating practices rather than generic templates.

Business objective: establish one reusable, understandable control catalog that can mature across readiness, Type I, and Type II.

Requirements:

- Capture stable identifier, title, objective, description, implementation narrative, expected evidence, current status, and applicable applications, reviewed systems, risks, or processes.
- Support draft, active, superseded, and retired controls.
- Preserve versions and reuse active controls across engagements.
- Allow templates as a starting point while making organization-authored content clear.
- Preview the impact of changing applicability, mappings, ownership, cadence, or expected evidence before a successor version becomes effective.
- Allow deletion only for a never-used draft with no retained relationships; supersede or retire every control that has entered the compliance record.

Domain slice:

- Owns `Control`, immutable `ControlVersion`, lifecycle, implementation
  narrative, expected evidence definition, and applicability relationships.
- R1-07 owns the versioned assertion that a control version treats a risk;
  control applicability alone does not establish risk coverage (M0-D22).
- Uses program scope, members, systems, processes, and source-aware templates.
- Supplies approved versions to criteria mappings, responsibilities, cadence,
  occurrences, evidence expectations, readiness, and engagement snapshots.
- Creating a control creates a draft. Updating an active or historically used
  control creates a successor version; retirement stops future use without
  erasing prior relationships or activity.

Example:

- An active access-review control applies quarterly to AWS and GitHub. Changing
  its owner, cadence, expected evidence, or applicable systems creates a
  proposed successor and impact preview. Existing occurrences and the Type I
  snapshot continue to reference the old version. Deleting the active control
  is not offered; after approval it may be superseded or retired.

Acceptance criteria:

- [ ] A control owner can understand what must happen, why, where, and what evidence is expected without reading implementation details.
- [ ] A draft control can be reviewed before activation.
- [ ] Activating, superseding, or retiring a control preserves its earlier use and relationships.
- [ ] Duplicate identifiers and removal of an in-use control fail safely with an explanation.
- [ ] A proposed change identifies affected mappings, responsibilities, future work, evidence expectations, readiness, and engagements before approval.
- [ ] Deletion is available only for an unused draft and cannot remove a control merely because the current user can no longer see its historical relationships.

Implementation subtasks:

- [ ] Define stable control identity, draft creation and editing, activation, successor-version, effective-date, supersession, retirement, narrowly permitted deletion, required narrative, expected-evidence semantics, and in-use protections.
- [ ] Deliver authorized create, review, activate, supersede, retire, browse, and inspect behavior through the API and browser.
- [ ] Deliver authorized successor proposal, relationship editing, impact preview, approval, and unused-draft deletion through the same API and browser workflow.
- [ ] Connect control versions to applications, reviewed systems, scope, risks, mappings, responsibilities, occurrences, evidence, readiness, and frozen engagements without duplicating those records.
- [ ] Prove lifecycle transitions, identifier conflicts, template provenance, forbidden actions, historical relationships, and accessible failure recovery end to end.

### R1-06 Map controls to criteria and explain applicability

Priority: P0

Area: controls

User story: As a compliance lead, I want to explain which controls address each in-scope criterion and why anything is not applicable so that our coverage can be reviewed instead of assumed.

Business objective: create transparent, reviewable coverage that reveals gaps and avoids double-counting.

Requirements:

- Support many-to-many relationships between controls and criteria.
- Capture rationale, author, review state, and review date for each mapping.
- Capture not-applicable rationale and approval.
- Distinguish working, internally reviewed, advisor-reviewed, and superseded mappings.

Domain slice:

- Owns `ControlCriterionMapping` as a version-aware, attributable assertion with
  rationale, applicability, review state, and relevant decisions.
- Uses an exact control version and criterion catalog edition within program or engagement scope.
- Feeds explainable coverage and gap calculations without changing either the
  source criterion or organization-authored control.

Acceptance criteria:

- [ ] Every in-scope criterion shows its mapped controls or an explicit unresolved gap.
- [ ] Draft mappings cannot make validated coverage appear complete.
- [ ] Coverage calculations do not increase merely because duplicate mappings exist.
- [ ] Mapping changes preserve the engagement history they affected.

Implementation subtasks:

- [ ] Define mapping identity, many-to-many uniqueness, applicability rationale, review-state transitions, and version compatibility.
- [ ] Deliver authorized propose, review, approve, supersede, browse, and explain behavior through the API and browser.
- [ ] Integrate mappings with coverage, gaps, advisor feedback, imports, and engagement snapshots while keeping draft and reviewed assertions distinct.
- [ ] Prove duplicate resistance, not-applicable approval, forbidden review, edition changes, coverage reconciliation, and historical preservation end to end.

### R1-07 Assess scoped risks and choose treatment

> [!NOTE]
> Promoted from P1 to P0 on 2026-09-14. The R1/R2 milestone exits and the P0 readiness stories R1-08 and R2-09 depend on this capability, and it covers SOC 2 Security criteria a Type I auditor routinely tests (risk assessment CC3, vendor oversight CC9.2, policy communication CC1/CC2).

Priority: P0

Area: risk

User story: As a compliance lead, I want to assess material risks and approve
how each will be treated so that controls and accepted exposure reflect the
actual scoped service system.

Business objective: make control selection and readiness decisions traceable to
a consistent, reviewed risk assessment rather than an unowned list of concerns.

Requirements:

- Define the approved qualitative or quantitative assessment method, scales,
  risk appetite or thresholds, approvers, and effective period.
- Capture risk source, scenario, affected services, people, applications,
  components, information, providers, likelihood and impact rationale, inherent
  risk, existing controls, and assessment date.
- Choose avoid, mitigate, transfer, or time-bounded accept treatment with owner,
  target state, due date, expected evidence, and approval authority.
- Record target or residual risk only after considering the exact controls and
  treatment state; preserve uncertainty and differing reviewer conclusions.
- Reassess when relevant scope, commitments, assets, providers, controls, or
  incidents change and at an approved periodic cadence.
- Surface overdue assessments, treatment gaps, expired acceptance, and risk
  above approved tolerance in readiness without claiming a calculated score is
  an audit conclusion.

Domain slice:

- Owns stable `Risk`, versioned `RiskAssessment`, explicit assessment method,
  `RiskTreatment`, its time-bounded `RiskAcceptance` decision, review decision,
  and reassessment state.
- Owns the reviewed control-version-to-risk-treatment relationship and an
  attributable external incident reference for pre-T2-07 reassessment (M0-D22).
- Uses the program boundary, commitments, workforce context, applications,
  system components, information assets, providers, criteria, controls,
  incidents, findings, evidence, and responsibilities.
- Produces control justification, accountable treatment work, readiness effects,
  management review input, and exact engagement-snapshot references.

Acceptance criteria:

- [ ] The team can explain the method and rationale behind inherent, target, and residual risk without relying on color alone.
- [ ] Each material risk has an owner, current assessment, treatment decision, due date or accepted interval, and review state.
- [ ] Acceptance above the configured authority or beyond its expiry is blocked and appears as a readiness gap.
- [ ] Treatment cannot appear complete until required action and evidence are independently reviewed.
- [ ] Reassessment preserves prior conclusions and identifies changed scope, controls, assumptions, and treatment.
- [ ] Unauthorized users cannot alter assessment methods, approve acceptance, or hide material risk.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D10 before finalizing this story's rules.
- [ ] Define risk identity, assessment versions, method version, inherent and residual semantics, treatment, acceptance, reassessment triggers, review, and expiry invariants.
- [ ] Deliver authorized identify, assess, relate, treat, accept, review, reassess, browse, and compare behavior through the API and browser.
- [ ] Connect risks to boundary, commitments, assets, providers, controls, evidence, findings, accountable work, readiness, management review, and snapshots without duplicating those records.
- [ ] Prove changed methods, incomplete assessments, unauthorized or expired acceptance, treatment verification, concurrent reassessment, history, and readiness reconciliation end to end.

### R1-08 Complete the readiness assessment and own the gap plan

Priority: P0

Area: readiness

User story: As a compliance lead, I want to assess the scoped program and turn every material gap into owned work so that the team has a credible plan toward Type I.

Business objective: make readiness actionable and measurable rather than a subjective percentage.

Requirements:

- Assess boundary completeness; workforce, application, technology, and
  information inventories; commitments and requirements; criteria coverage;
  control design and implementation evaluation; ownership; policy approval and
  communication; evidence expectations and governance; risks; providers;
  access expectations; and access-review needs.
- Classify gaps by severity, affected scope, owner, due date, and intended resolution.
- Show readiness by underlying records and unresolved assumptions.
- Support advisor comments and validation status.

Domain slice:

- Owns versioned readiness rules, `ReadinessAssessment`, accountable `Gap`,
  and as-of calculation identity; it does not own the source records it evaluates.
- Uses scope, workforce, application and asset inventories, reviewed systems,
  commitments, requirements, criteria, mappings, control design and operation,
  formal evaluations, responsibilities, policies and communication, governed
  evidence, access expectations and reviews, risks, providers, findings, and
  advisor feedback.
- Produces drillable readiness views and an owned gap plan that later supplies
  the Type I entry decision.

Acceptance criteria:

- [ ] Every readiness status drills into the records and rules behind it.
- [ ] No criterion is considered addressed solely because it has a draft mapping.
- [ ] Every material gap has an owner, target date, and next action or approved risk treatment.
- [ ] Unknown, unowned, missing-from-source, or unresolved-scope applications and reviewed systems remain visible readiness gaps.
- [ ] The team can filter the gap plan by owner, severity, criterion, control, and target stage.
- [ ] The product does not claim that completing the internal assessment guarantees audit success.
- [ ] Evidence-governance capabilities that are not yet delivered (retention, hold, redaction, disclosure; R2-12) appear as explicit readiness gaps rather than being omitted or treated as satisfied.

Implementation subtasks:

- [ ] Define assessment inputs, explainable rules, as-of semantics, gap identity, severity and ownership, and explicit unknown or draft treatment.
- [ ] Deliver authorized assessment, drill-down, filtering, gap assignment, advisor annotation, and recalculation behavior through the API and browser.
- [ ] Reconcile every readiness result to source records and route new or resolved gaps through the shared finding, remediation, evidence, and review workflows.
- [ ] Prove stale, missing, draft, rejected, unresolved, corrected, and unauthorized cases plus historical assessment reproducibility end to end.

### R1-09 Bring existing readiness work into the program

Priority: P0

Area: program

User story: As a compliance lead already working through readiness with
consultants, I want to bring our existing boundary, inventories, commitments,
controls, policies, evidence, mappings, owners, risks, providers, and findings
into Compliance so that we can adopt the product without restarting the engagement.

Business objective: shorten time to value and make the live readiness engagement the proving ground for the product.

Requirements:

- Support reviewed bulk import for the boundary, workforce, applications,
  reviewed systems, technology and information assets, commitments,
  requirements, controls, mappings, policy metadata and files, evidence
  metadata and files, owners, risks, providers, gaps, and consultant findings.
- Preserve source filename or record identifier, import batch, importer, import time, and unresolved transformation warnings.
- Validate references, required fields, duplicates, unknown owners, unsupported files, and ambiguous mappings before acceptance.
- Let the user accept a valid subset only through an explicit choice with a retained rejected-item report.
- Make repeat imports safe by identifying unchanged, changed, new, and conflicting records.

Domain slice:

- Owns `ImportBatch`, source and content identity, staged transformations,
  validation results, explicit acceptance, conflict resolutions, and rejected-item report.
- Uses the existing platform identities and owning domain workflows for scope,
  workforce, inventories, commitments, controls, mappings, policies, evidence,
  responsibilities, risks, providers, gaps, and external authorship.
- Produces ordinary domain records with provenance; imported records never form
  a parallel model or bypass their normal lifecycle, authorization, and review rules.
- Uses the shared import pipeline enabler (EN-05) for import batches, staging, validation, preview, atomic acceptance, and replay. This story owns the readiness-material mappings and the adoption experience, delivered in record-family slices once each owning story exists.

Acceptance criteria:

- [ ] The team can preview counts, relationships, warnings, and errors before any imported record becomes active.
- [ ] A failed or canceled import cannot leave an apparently complete partial program.
- [ ] Every imported record can be traced to its source and import batch.
- [ ] Repeating the same import does not silently duplicate controls, relationships, evidence, or findings.
- [ ] Conflicting changes require an explicit resolution and preserve both the source value and the accepted result.
- [ ] After import, the readiness assessment reconciles to the accepted records and clearly shows what still remains outside the product.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D04 and define source identifiers, normalization, member matching, external-author treatment, conflicts, and atomic acceptance rules.
- [ ] Deliver authorized upload, parse, preview, correct, accept, cancel, and retry behavior through the API, any required worker processing, and browser.
- [ ] Route accepted items through the owning contexts, including identity and responsibility resolution, lifecycle checks, provenance, evidence content identity, and readiness recalculation.
- [ ] Prove replay safety, partial and interrupted failure, accepted subsets, unresolved references, duplicate prevention, denied imports, and source-to-result traceability end to end.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R1-09a Import existing controls, mappings, and owners with preview and atomic acceptance

Outcome: As a compliance lead, I can bring the consultant-built control inventory, criteria mappings, and control owners into Compliance and preview everything before any record becomes active.

Acceptance criteria:

- [ ] The team can preview counts, relationships, warnings, and errors before any imported record becomes active.
- [ ] A failed or canceled import cannot leave an apparently complete partial program.
- [ ] Every imported record can be traced to its source and import batch.
- [ ] A valid subset is accepted only through an explicit choice, with a retained rejected-item report.
- [ ] Unknown owners and ambiguous mappings are resolved or rejected before acceptance; imported records follow the normal control and mapping lifecycle.

Not in this slice:

- Policies and evidence (R1-09b).
- Scope, inventories, risks, providers, and findings (R1-09c).
- Repeat imports (R1-09d).

Depends on: R1-04, R1-05, R1-06

#### R1-09b Import existing policies and evidence with their files

Outcome: As a compliance lead, I can import existing policy and evidence metadata and files with preserved provenance and content identity.

Acceptance criteria:

- [ ] The team can preview counts, relationships, warnings, and errors before any imported record becomes active.
- [ ] Unsupported, oversized, or corrupt files are rejected before acceptance with an actionable report.
- [ ] Every imported artifact keeps its source filename or identifier, import batch, importer, import time, and content identity.
- [ ] Imported policies enter the normal policy lifecycle and are never implicitly approved.

Not in this slice:

- Repeat imports (R1-09d).

Depends on: R1-09a, R2-02, R2-03

#### R1-09c Import existing scope, inventories, risks, providers, and findings

Outcome: As a compliance lead, I can import the existing boundary, workforce and system inventories, commitments, risks, providers, gaps, and consultant findings through their owning workflows.

Acceptance criteria:

- [ ] The team can preview counts, relationships, warnings, and errors before any imported record becomes active.
- [ ] Every imported record can be traced to its source and import batch.
- [ ] Consultant findings retain external-author provenance and are never fabricated as platform activity.

Not in this slice:

- Repeat imports (R1-09d).

Depends on: R1-02, R1-07, R1-09a, R1-10, R1-11, R1-12, R1-13, R1-14, R2-07

#### R1-09d Repeat imports safely and reconcile readiness to accepted records

Outcome: As a compliance lead, I can re-import updated consultant material without duplicates and see readiness reconcile to what has been accepted.

Acceptance criteria:

- [ ] Repeating the same import does not silently duplicate controls, relationships, evidence, or findings.
- [ ] Conflicting changes require an explicit resolution and preserve both the source value and the accepted result.
- [ ] After import, the readiness assessment reconciles to the accepted records and clearly shows what still remains outside the product.

Depends on: R1-08, R1-09a

### R1-10 Establish the application inventory and review scope

Priority: P0

Area: application inventory

User story: As a compliance lead, I want one governed inventory of the software
we believe the organization uses so that every application is owned, connected
to the compliance boundary, and deliberately included in or excluded from
access review.

Business objective: establish the known universe for controls, policies,
evidence collection, and access governance instead of discovering important or
unowned applications during the audit.

Requirements:

- Capture stable application identity, name, aliases, business purpose, owner,
  access owner, vendor or internal ownership, lifecycle, criticality, data
  sensitivity, authentication method, SSO status, and known user population.
- Support manual entry and reviewed import while preserving whether an
  application was declared, imported, or discovered, from which source, at what
  time, and with what unresolved confidence or ownership questions.
- Represent each concrete tenant, organization, account, or environment as a
  reviewed system with its own source identifier and access boundary.
- Own each such `SystemInstance`; access governance references it and never
  creates a competing instance record (M0-D22).
- Link applications and reviewed systems to the system boundary, system
  components, information assets, data flows, vendors or subservice
  organizations, commitments, processes, controls, policies, evidence sources,
  and access-review cadence.
- Record an explicit access-review inclusion, exclusion, or unresolved decision
  with owner, rationale, reviewer, effective date, and review date.
- Reconcile imports as new, changed, missing, duplicate, aliased, or retired;
  absence from a later source never silently deletes or de-scopes an application.
- Retiring an application preserves prior relationships, populations,
  campaigns, evidence, and engagement snapshots and identifies open work that
  must be resolved or reassigned.

Domain slice:

- Owns `Application`, aliases, inventory provenance, lifecycle, ownership,
  classification, access-review scope decision, and one or more `ReviewedSystem` records.
- Uses the organization, program boundary, vendors, processes, controls,
  policies, evidence sources, and platform members or teams responsible for the application.
- Supplies the reviewed-system universe to access imports, expectations,
  campaigns, automation, readiness, system descriptions, and engagement snapshots.

Acceptance criteria:

- [ ] The team can manually add or import applications, preview validation and matching, and resolve duplicates or aliases before acceptance.
- [ ] Every active application shows an owner, business purpose, classification, concrete reviewed systems, and access-review scope status or an explicit unresolved gap.
- [ ] Each inclusion or exclusion decision is attributable, time-aware, reviewable, and visible in readiness rather than hidden in import configuration.
- [ ] Unknown, missing-from-source, unowned, duplicate, and retired applications remain distinguishable and cannot silently disappear from scope.
- [ ] A user can navigate from an application to its boundary, vendor, controls, policies, evidence sources, access populations, and campaigns subject to authorization.
- [ ] Changing or retiring an application previews affected controls, policies, evidence collection, review campaigns, open work, readiness, and frozen engagements before approval.
- [ ] Unauthorized users cannot discover restricted applications or change inventory, ownership, classification, or review scope.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D05 and M0-D28 and define minimum inventory fields, ownership, application-versus-system-instance boundaries, aliases, lifecycle, source confidence, and scope-decision rules from approved public references.
- [ ] Define stable application and reviewed-system identity, source-aware import and reconciliation, explicit inclusion or exclusion, relationship, impact, retirement, and narrowly permitted unused-draft deletion semantics.
- [ ] Deliver authorized add, import, preview, match, reconcile, classify, own, scope, relate, revise, retire, browse, and inspect behavior through the API and browser.
- [ ] Connect applications to boundary, vendors, controls, policies, evidence, external access governance, work, readiness, automation, and engagement snapshots without creating duplicate system records.
- [ ] Prove duplicate and alias handling, missing source rows, ownership gaps, scope decisions, restricted visibility, relationship impact, retirement history, import replay, and standalone/split-host parity end to end.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R1-10a Record applications and their concrete reviewed systems with ownership and classification

Outcome: As a compliance lead, I can manually record each application and its concrete tenants, accounts, or environments, with owners, purpose, and classification.

Acceptance criteria:

- [ ] Every active application shows an owner, business purpose, classification, and concrete reviewed systems, or an explicit unresolved gap.
- [ ] A reviewed system has its own source identifier and access boundary and is never reused for an unrelated application.
- [ ] Unauthorized users cannot discover restricted applications or change inventory, ownership, or classification.

Not in this slice:

- Import (R1-10b).
- Review-scope decisions (R1-10c).
- Retirement and impact (R1-10d).

#### R1-10b Import and reconcile the application inventory from a source list

Outcome: As a compliance lead, I can import an application list, match it to existing records, and resolve duplicates, aliases, and missing rows before acceptance.

Acceptance criteria:

- [ ] The team can import applications, preview validation and matching, and resolve duplicates or aliases before acceptance.
- [ ] Unknown, missing-from-source, unowned, duplicate, and retired applications remain distinguishable and cannot silently disappear from scope.
- [ ] Each application preserves whether it was declared, imported, or discovered, from which source, and when.

Depends on: R1-10a

#### R1-10c Decide and review access-review scope for each reviewed system

Outcome: As a compliance lead, I can record an attributable inclusion, exclusion, or unresolved access-review decision for each application and reviewed system.

Acceptance criteria:

- [ ] Each inclusion or exclusion decision is attributable, time-aware, reviewable, and visible in readiness rather than hidden in import configuration.
- [ ] Every active application shows its access-review scope status or an explicit unresolved gap.

Depends on: R1-10a

#### R1-10d Relate, change, or retire applications with impact preview

The bounded backend change-preview contract is documented in
[application-change-preview-v1.md](application-change-preview-v1.md). It reports
known boundary and direct current Control-draft references with explicit pending
contexts; governed successor approval and retirement remain open.

Outcome: As a compliance lead, I can relate applications to the boundary, vendors, controls, policies, and evidence sources, and change or retire them only after seeing the impact.

Acceptance criteria:

- [ ] A user can navigate from an application to its boundary, vendor, controls, policies, evidence sources, access populations, and campaigns, subject to authorization.
- [ ] Changing or retiring an application previews affected controls, policies, evidence collection, review campaigns, open work, readiness, and frozen engagements before approval.
- [ ] Retirement preserves prior relationships and snapshots.

Depends on: R1-10a

### R1-11 Establish the authoritative workforce and identity-owner roster

Priority: P0

Area: workforce assurance

User story: As a compliance lead, I want a reconciled roster of employees,
contractors, external collaborators, and accountable NHI owners so that access,
policy, training, and joiner-mover-leaver controls are evaluated against the
people and relationships that actually apply.

Business objective: replace email matching and stale spreadsheets with a
source-aware workforce context while keeping HR, platform membership, provider
accounts, and audited access as distinct concepts.

Requirements:

- Capture stable source identity, name, worker type, lifecycle status, manager,
  organizational attributes needed for review, relevant start and end dates,
  source, observation time, and data sensitivity.
- Support manual entry and reviewed import from one or more authoritative or
  corroborating sources with an explicit precedence and reconciliation policy.
- Distinguish a person from a Compliance member, external login, access subject,
  and provider directory principal; correlations are explicit and reviewable.
- Identify joiners, movers, leavers, missing people, duplicates, conflicting
  records, stale observations, and people present only in an access source.
- Record accountable human or team ownership, approved purpose, environment,
  lifecycle, and review date for each in-scope NHI without classifying groups or
  roles as people or NHIs.
- Own governed `Person`, `WorkRelationship`, and `ServiceIdentity` records;
  access governance correlates observed accounts to them (M0-D22).
- Freeze the applicable roster used by an access review, policy campaign,
  training population, control evaluation, or audit population.
- Minimize sensitive workforce data and prevent a source observation from
  automatically granting platform access, revoking provider access, or deciding
  a review item.

Domain slice:

- Owns `Person`, source references, employment or engagement lifecycle facts,
  manager relationships, reconciliation decisions, `WorkforceSnapshot`, and
  governed NHI-owner relationships.
- Uses organization boundaries, source-aware imports, platform actors for
  decisions, and existing `AccessSubject` records for explicit correlation.
- Supplies authoritative context and frozen populations to responsibilities,
  policy acknowledgement, training, access expectations and campaigns,
  controls, evidence, readiness, audit populations, and engagement snapshots.

Acceptance criteria:

- [ ] The team can import or manually record a roster, preview source precedence and conflicts, and accept only an attributable reconciled result.
- [ ] Each person and NHI owner shows its source, freshness, lifecycle, relationships, and unresolved conflicts without using email as the stable identifier.
- [ ] Joiners, movers, leavers, unmatched access principals, ownerless NHIs, missing source data, and stale observations remain visibly unresolved.
- [ ] A frozen workforce snapshot remains stable after later roster changes and resolves every included row to its accepted source facts.
- [ ] Workforce source changes do not silently alter Compliance membership, provider access, review decisions, or historical authorship.
- [ ] Sensitive fields and roster discovery are restricted to authorized users while the minimum review context remains usable.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D06 and M0-D28 before finalizing this story's rules; use `Person`, `WorkRelationship`, `ServiceIdentity`, and their first-class relationships with approved public references.
- [ ] Define person and source identity, lifecycle observations, manager and owner relationships, correlation, reconciliation, conflict, freshness, snapshot, and retention rules.
- [ ] Deliver authorized manual entry, import, preview, match, reconcile, classify, own, browse, freeze, and inspect behavior through the API, bounded processing where needed, and browser.
- [ ] Connect workforce context to platform responsibility without merging identities and to policy, training, access, evidence, control, readiness, population, and snapshot workflows.
- [ ] Prove conflicting and missing sources, identity replacement, ambiguous matches, stale and partial imports, NHI ownership gaps, authorization, replay, snapshot stability, and end-to-end reconciliation.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R1-11a Record and import the workforce roster with source precedence

Outcome: As a compliance lead, I can record or import employees, contractors, and external collaborators from authoritative and corroborating sources, and accept an attributable reconciled result.

Acceptance criteria:

- [ ] The team can import or manually record a roster, preview source precedence and conflicts, and accept only an attributable reconciled result.
- [ ] Each person shows their source, freshness, lifecycle, and relationships without using email as the stable identifier.
- [ ] Workforce source changes do not silently alter Compliance membership, provider access, review decisions, or historical authorship.

Not in this slice:

- NHI ownership (R1-11c).
- Snapshots and field restrictions (R1-11d).

#### R1-11b Surface joiners, movers, leavers, and unresolved roster conflicts

Outcome: As a compliance lead, I can see joiners, movers, leavers, duplicates, conflicts, and stale observations as unresolved items to act on.

Acceptance criteria:

- [ ] Joiners, movers, leavers, missing source data, and stale observations remain visibly unresolved.
- [ ] People present only in an access source are surfaced for deliberate treatment.

Depends on: R1-11a

#### R1-11c Record accountable owners and purpose for non-human identities

Outcome: As a compliance lead, I can record the accountable human or team owner, approved purpose, environment, lifecycle, and review date for each in-scope NHI.

Acceptance criteria:

- [ ] Each NHI owner shows its source, freshness, lifecycle, relationships, and unresolved conflicts.
- [ ] Ownerless NHIs remain visibly unresolved.
- [ ] Groups and roles are never classified as people or NHIs.

Not in this slice:

- Classifying observed provider principals (R2-06b).

Depends on: R1-11a

#### R1-11d Freeze workforce snapshots and restrict sensitive workforce fields

Outcome: As a compliance lead, I can freeze the roster used by a review, campaign, evaluation, or population, and sensitive workforce fields stay least-privilege.

Acceptance criteria:

- [ ] A frozen workforce snapshot remains stable after later roster changes and resolves every included row to its accepted source facts.
- [ ] Sensitive fields and roster discovery are restricted to authorized users while the minimum review context remains usable.

Depends on: EN-03, R1-11a

### R1-12 Inventory scoped technology and information assets

Priority: P0

Area: system inventory

User story: As a compliance lead, I want to inventory the material technology
and information that make up our scoped service so that the boundary, risks,
controls, and system description are based on known assets and data flows rather
than only an application list.

Business objective: prevent cloud environments, endpoints, repositories, data
stores, sensitive information, or material data movement from being omitted
from readiness and audit narratives.

Requirements:

- Capture material cloud accounts or subscriptions, infrastructure
  environments, network boundaries, managed endpoint classes, repositories,
  data stores, and other system components at the level needed for SOC 2 scope.
- Capture governed information sets or data classes with purpose, owner,
  classification, customer or personal-information relevance, locations,
  retention expectation, and applicable commitments.
- Describe material data flows between people, applications, components,
  providers, and locations, including protection expectations and versioned
  boundary relevance.
- Own governed `Location` and operational `Process` identities used by scope,
  descriptions, and risk; written procedures remain policy content (M0-D22).
- Link components, information, and flows to applications, reviewed systems,
  services, providers, risks, controls, policies, evidence sources, and scope.
- Support manual entry and reviewed import or discovery with stable source
  identifiers, confidence, freshness, aliases, missing-source behavior, and
  explicit inclusion, exclusion, or unresolved decisions.
- Preserve retirement and prior engagement use; absence from a source never
  silently deletes, merges, or removes an asset from scope.
- Remain an audit-relevant inventory rather than replacing CMDB, MDM, cloud,
  repository, or data-governance systems.

Domain slice:

- Owns `SystemComponent`, `InformationAsset`, versioned `DataFlow`, inventory
  provenance, classification, lifecycle, ownership, scope decision, and
  reconciliation history.
- Uses the approved boundary, applications and reviewed systems, providers,
  commitments, platform responsibilities, and source-aware imports.
- Supplies exact inventory relationships to risks, controls, policies,
  evidence, readiness, system-description sections, change impact, automation,
  audit populations, and engagement snapshots.

Acceptance criteria:

- [ ] The team can see which material component, information, and data-flow categories are complete, unresolved, excluded, stale, or missing from expected sources.
- [ ] Every active in-scope record has an accountable owner, classification, lifecycle, source or declaration, and explicit boundary relationship.
- [ ] Duplicate, aliased, nested, shared, and missing-source records require review and cannot silently change scope or ownership.
- [ ] A user can navigate from an asset or flow to the services, applications, providers, risks, controls, policies, evidence, and description sections it affects.
- [ ] Changing classification, flow, lifecycle, or scope previews downstream readiness and engagement impact before approval.
- [ ] Restricted information and topology are protected without making required audit relationships unverifiable.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D08 and M0-D28 before finalizing this story's rules; use the canonical application, system-instance, device, compute, network, resource, information, and provider entities with approved public references.
- [ ] Define stable identities, source matching, ownership, classification, lifecycle, flow versioning, scope decisions, reconciliation, retirement, and impact rules.
- [ ] Deliver authorized add, import, preview, match, classify, relate, scope, revise, retire, browse, and visualize behavior through the API, bounded processing where needed, and browser.
- [ ] Connect inventory records to applications, boundary, providers, commitments, risks, controls, policies, evidence, access scope, readiness, description, populations, and snapshots without duplicate asset models.
- [ ] Prove missing and partial sources, aliases, conflicting classification, restricted visibility, flow revision, retirement, replay, historical engagement use, and end-to-end readiness reconciliation.

### R1-13 Record service commitments, system requirements, and user responsibilities

Priority: P0

Area: commitments and requirements

User story: As a compliance lead, I want to record the promises, requirements,
and customer or provider responsibilities that govern our service so that scope,
controls, and the system description explain what the system is intended to do.

Business objective: prevent controls and audit narratives from drifting away
from customer commitments, contracts, policies, architecture, and shared-
responsibility assumptions.

Requirements:

- Capture customer-facing service commitments and contractual, legal, policy,
  architectural, or other system requirements with source, owner, rationale,
  applicability, effective interval, and version history.
- Record complementary user-entity controls (CUECs) and complementary
  subservice-organization controls (CSOCs) as responsibilities distinct from the
  organization's own controls.
- Link each applicable record to services, scope, criteria, risks,
  applications, reviewed systems, components, information, providers, controls,
  policies, and system-description sections as appropriate.
- Review and approve exact versions; preserve changed, superseded, withdrawn,
  and historically applicable records.
- Identify conflicting, unowned, unsourced, expired, unmet, or unmapped
  commitments and requirements as readiness questions or gaps.
- Keep contract authoring and customer attestation outside Compliance while
  preserving source references and approved compliance interpretation.

Domain slice:

- Owns `ServiceCommitment`, `SystemRequirement`, `UserEntityResponsibility`,
  `SubserviceResponsibility`, immutable versions, applicability relationships,
  review decisions, and effective history.
- Uses the system boundary, services, inventories, providers, criteria,
  controls, policies, risks, source artifacts, and platform responsibilities.
- Supplies approved versions to control justification, readiness, system
  description, provider review, management assertions, audit packages, and
  engagement snapshots.

Acceptance criteria:

- [ ] The team can trace every approved commitment or requirement to its source, owner, exact version, applicability, interpretation, and review decision.
- [ ] CUECs and CSOCs are distinguishable from organization controls and cannot count as internally performed merely because they are documented.
- [ ] Conflicts, expired sources, missing owners, unsupported interpretations, and unmet or unmapped requirements remain visible in readiness.
- [ ] A proposed change shows affected scope, criteria, controls, policies, risks, providers, description sections, and engagements before approval.
- [ ] Historical snapshots and delivered packages retain the exact commitments, requirements, CUECs, and CSOCs that applied.
- [ ] The product never presents imported contractual text or a generated interpretation as legal advice or auditor approval.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D09 before finalizing this story's rules.
- [ ] Define stable identities, source and version provenance, applicability, interpretation, conflict, review, effective-date, supersession, withdrawal, and impact rules.
- [ ] Deliver authorized capture or import, relate, review, approve, revise, supersede, browse, and inspect behavior through the API and browser.
- [ ] Connect approved records to boundary, inventories, providers, criteria, controls, policies, risks, readiness, system description, assertions, packages, and snapshots.
- [ ] Prove conflicting sources, expired and unmapped records, forbidden approval, concurrent revision, impact preview, historical retrieval, and package stability end to end.

### R1-14 Evaluate vendors and subservice organizations

> [!NOTE]
> Promoted from P1 to P0 on 2026-09-14. The R1/R2 milestone exits and the P0 readiness stories R1-08 and R2-09 depend on this capability, and it covers SOC 2 Security criteria a Type I auditor routinely tests (risk assessment CC3, vendor oversight CC9.2, policy communication CC1/CC2).

Priority: P0

Area: provider oversight

User story: As a compliance lead, I want to evaluate material vendors and
subservice organizations and track any coverage gaps so that third-party
dependencies are deliberately accepted, mitigated, or removed from the scoped
service system.

Business objective: make provider reliance, due diligence, shared
responsibilities, and unresolved exceptions visible to control owners,
management, and the auditor.

Requirements:

- Capture each provider, material service, owner, criticality, systems and data
  access, processing or hosting locations, contract and review dates, lifecycle,
  and relationship to the system boundary.
- Record subservice-organization treatment, including carve-out or inclusive
  method, relevant services, CUECs, CSOCs, and rationale where applicable.
- Conduct a versioned assessment using the approved due-diligence requirements
  and preserve evidence, reviewer, conclusion, exceptions, and next review.
- Record assurance reports, certifications, questionnaires, testing, or other
  evidence with exact issuer, scope, period, opinion or conclusion source,
  exceptions, complementary controls, and coverage gaps.
- Treat expired reports, uncovered periods, missing evidence, material
  exceptions, contract gaps, renewal, and termination as explicit work or
  accepted risk, including bridge-letter handling when used.
- Link providers and assessments to applications, components, information,
  data flows, commitments, risks, controls, policies, evidence, changes, and
  engagement snapshots without replacing procurement.

Domain slice:

- Owns stable `Provider`, service relationships, classification, lifecycle,
  boundary treatment, versioned `ProviderAssessment`, reviewer conclusion,
  `ProviderCoverageGap`, renewal or termination review, and next-review state.
- Uses commitments and requirements, CUECs and CSOCs, inventories, risks,
  controls, evidence artifacts, findings, corrective actions, and platform
  responsibilities.
- Produces risk-treatment, readiness, system-description, management-review,
  audit-request, package, and engagement-snapshot inputs.

Acceptance criteria:

- [ ] Every material provider has an owner, approved boundary treatment, current assessment or explicit gap, and next review date.
- [ ] Assurance evidence shows exact scope and covered period; a stale report or bridge letter cannot silently imply current or complete coverage.
- [ ] Provider exceptions, missing complementary controls, uncovered services, and contract gaps produce owned remediation or authorized time-bounded acceptance.
- [ ] Renewal, material service change, and termination identify affected systems, data, controls, evidence, responsibilities, and scope before approval.
- [ ] Subservice organizations and their CUECs or CSOCs remain distinguishable from ordinary vendors and the organization's own controls.
- [ ] Restricted contracts and reports are least-privilege while approved audit-facing conclusions remain deliberately shareable.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D11 before finalizing this story's rules.
- [ ] Define provider identity, service relationships, assessment versions, evidence coverage, exception, boundary treatment, review, renewal, termination, and acceptance rules.
- [ ] Deliver authorized record, import, classify, assess, review, relate, remediate, accept risk, renew, terminate, browse, and inspect behavior through the API and browser.
- [ ] Connect providers to inventories, commitments, risks, controls, policies, evidence, findings, accountable work, readiness, description, management review, packages, and snapshots.
- [ ] Prove stale and partial assurance, uncovered periods, missing CSOCs, restricted content, change impact, denied acceptance, reassessment history, and end-to-end readiness reconciliation.

### R1-15 Provision a client organization and its first administrators

Priority: P0

Area: tenancy

User story: As a firm operator, I want to create a client organization, bootstrap its first client administrator, and assign our firm staff so that each client's compliance program starts inside an isolated tenant.

Business objective: make every client's records, members, and evidence isolated from the start so the platform can serve many clients without retrofitting tenancy.

Requirements:

- Create a client organization with stable identity, legal and display name, lifecycle (provisioning, active, suspended), and platform-operator attribution.
- Invite the first client administrator and add firm-staff memberships with an explicit affiliation of client personnel or firm staff.
- Let a platform user who belongs to several organizations choose the active organization, and show it unmistakably throughout the product.
- Suspend an organization, blocking access for its members and assigned firm staff while preserving its records.
- Leave client onboarding templates, offboarding, export, and disposition to F1-01.
- After sign-in, send a user with one organization directly to it, a user with several to an organization selector, and a user with none to a no-access or pending-invitation page; offer creation only to users authorized to create organizations (M0-D25).
- Give each organization a unique, URL-friendly slug used in browser routes, and an opaque, immutable `tenant_id` used by every organization-scoped API, following M0-A07.
- Validate every slug on the server, and mirror the validation in the browser, using these rules:
  - Length: 4 to 63 characters.
  - Characters: lowercase letters `a`–`z`, digits `0`–`9`, and hyphens. Input is trimmed and lowercased before validation; any other character is rejected, never silently transliterated.
  - Shape: starts with a letter, ends with a letter or digit, and contains no consecutive hyphens (`^[a-z](?:[a-z0-9]|-(?=[a-z0-9])){3,62}$`).
  - Uniqueness: unique across the current and retired slugs of every organization; a retired slug is never assigned to another organization.
  - Reserved routes: must not equal any entry in the reserved-route registry, which lists every top-level server path segment and client route. Initial entries: `api`, `auth`, `health`, `healthz`, `openapi`, `login`, `logout`, `signup`, `callback`, `select`, `tenants`, `organizations`, `new`, `create`, `account`, `settings`, `admin`, `portfolio`, `work`, `my-work`, `invitations`, `invite`, `help`, `support`, `docs`, `status`, `static`, `assets`, `www`, and `app`.
  - Registry ownership: adding a top-level route requires adding it to the registry first; the change fails if an existing organization already uses that slug.
  - Not an identifier: a slug must not match the `tenant_id` format.
- Allow an authorized slug change that keeps redirect history for members and never reassigns a retired slug to another organization.

Domain slice:

- Owns `Organization` as the tenant boundary, its lifecycle, and platform-operator provisioning decisions.
- Uses platform users, external identities, and the tenant context and isolation enforced by EN-01.
- Supplies the organization boundary to membership, programs, and every other context; no business record exists outside an organization except explicitly platform-level content.

Acceptance criteria:

- [ ] Only an authorized platform operator can create, suspend, or reactivate a client organization.
- [ ] A new organization has exactly the administrators and firm-staff memberships that were explicitly granted.
- [ ] A user with memberships in two organizations never sees, counts, searches, or receives notifications about records from the organization that is not active.
- [ ] Suspending an organization blocks access immediately for its members and assigned firm staff and preserves all records and history.
- [ ] The active organization is visible on every page, and switching it cannot carry cached or unsaved data across tenants.
- [ ] Browser routes use the organization slug, and every organization-scoped API call uses the `tenant_id`; the server rejects a request whose `tenant_id` the caller is not a member of.
- [ ] An unknown slug and a slug the user cannot access produce the same not-found result, so organization slugs cannot be enumerated.
- [ ] A slug that is shorter than 4 or longer than 63 characters, uses characters other than lowercase letters, digits, and single inner hyphens, does not start with a letter, matches the `tenant_id` format, or equals a reserved route is rejected on the server with an explanation naming the rule.
- [ ] Adding a top-level server or client route that is missing from the reserved-route registry, or that equals an existing organization slug, fails an automated check.
- [ ] After a slug change, old links redirect members to the organization, and the retired slug can never be assigned to a different organization.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D25, M0-D28, and M0-A07 and define canonical organization, platform-user, membership, identity, lifecycle, operator-authority, bootstrap, and affiliation rules from approved public references.
- [ ] Deliver authorized create, invite-first-administrator, add-firm-staff, switch-organization, suspend, and reactivate behavior through the API and browser.
- [ ] Prove cross-tenant isolation across APIs, client caches, search, counts, and notifications, plus suspension, bootstrap failure, denied operator actions, and standalone/split-host parity end to end.

### EN-01 Enforce server-side authorization, tenant isolation, and actor attribution

Priority: P0

Type: enabler

Area: workspace

Why: Every story requires server-enforced authorization and attributable actions, and every client organization is now a tenant. Without one shared mechanism, each slice would invent its own checks, actor model, and tenant filtering.

Scope:

- An organization (tenant) context resolved for every request, job, and message from the authenticated platform user and an explicit active-organization selection, following M0-A07.
- An authorization evaluation pipeline for commands and queries, following the M0-A04 decision.
- ActorReference for members and named system processes, with a display snapshot.
- Filtering for lists and counts, and consistent forbidden or not-found Problem Details.
- A test harness for allow and deny matrices.

Out of scope:

- Member invitation, team administration, and role UX (R1-04).
- Record-specific responsibilities (R2-01).

Acceptance criteria:

- [ ] Every business endpoint under `/api/v1` declares an explicit authorization policy, and an architecture test fails when one is missing.
- [ ] A platform user with memberships in several organizations acts in exactly one resolved organization per request, and every query, write, artifact access, projection, job, and notification is scoped to it.
- [ ] An automated cross-tenant leak suite covering APIs, search, counts, exports, artifacts, background jobs, and notifications runs in CI.
- [ ] Cross-organization access is denied and cannot be distinguished from a missing resource.
- [ ] Historical actor attribution survives rename, identity replacement, and deprovisioning.
- [ ] Restricted records are absent from lists and counts, not merely hidden in the UI.
- [ ] System-process actions cannot be mistaken for member actions.
- [ ] Behavior is identical in standalone and split API/worker host modes.

Implementation subtasks:

- [ ] Incorporate the canonical platform-user, membership, role, permission, assignment, and actor-reference semantics approved in M0-D28.
- [ ] Implement the request context and evaluation pipeline.
- [ ] Implement ActorReference persistence and display snapshots.
- [ ] Add allow/deny test helpers and the architecture test.
- [ ] Prove the pipeline through R1-01's create, view, and revise flow.
- [ ] Prove allowed and denied behavior, failure states, history, and standalone/split-host parity with focused tests.

First consumer: R1-01

Depends on: M0-A04, M0-A07, M0-D03, M0-D25, M0-D28

Blocks: EN-04, EN-05, EN-06, F1-08, R1-01, R1-04, R1-15, R2-11

### EN-02 Provide versioned records, effective history, and change-impact preview

Priority: P0

Type: enabler

Area: architecture

Why: Boundary, controls, policies, inventories, commitments, risks, and providers all require drafts, immutable approved versions, successor proposals, effective dates, and impact preview.

Scope:

- Draft editing, immutable approved versions, and successor proposals with effective dates.
- Retrieval of the version effective on a date.
- Optimistic concurrency with actionable conflicts.
- An impact-preview contract that owning contexts contribute affected records to.
- A deletion guard that allows deleting only never-used drafts.

Out of scope:

- Workflow-specific review and approval rules (EN-04 and the owning stories).
- Snapshot freezing (EN-03).

Acceptance criteria:

- [ ] An approved version cannot be changed in place through any API.
- [ ] The version effective on any selected date is retrievable.
- [ ] A concurrent edit returns a conflict that identifies the newer version.
- [ ] An impact preview lists affected records contributed by each registered context before approval.
- [ ] Deletion is refused for any record that was ever approved or referenced.

Implementation subtasks:

- [ ] Implement the version and effective-interval primitives from M0-A01.
- [ ] Implement the concurrency and conflict contract.
- [ ] Implement the impact-preview contribution contract.
- [ ] Prove it through R1-02's boundary versioning flow.
- [ ] Prove allowed and denied behavior, failure states, history, and standalone/split-host parity with focused tests.

First consumer: R1-02

Depends on: M0-A01

Blocks: F1-04, R1-02, R1-05, R1-07, R1-10, R1-12, R1-13, R1-14, R2-02

### EN-03 Freeze immutable snapshots with content identity and amendments

Priority: P0

Type: enabler

Area: architecture

Why: Workforce, population, readiness, baseline, period, and management-review snapshots need one shared, provable freezing mechanism.

Scope:

- Snapshot creation from exact record versions, following the M0-A02 decision.
- Content identity (canonical hash) and verification.
- Amendments linked to the original snapshot, with downstream impact.
- As-of retrieval of snapshot contents.

Out of scope:

- Deciding what each snapshot contains (owning stories).

Acceptance criteria:

- [ ] A frozen snapshot is unchanged by later source changes, and its content identity verifies.
- [ ] An amendment is attributable, links to its original, and never mutates it.
- [ ] Regenerating the same snapshot contents yields an equivalent content identity.
- [ ] A partial freeze failure cannot produce an apparently complete snapshot.
- [ ] A snapshot references only records from its own organization, plus explicitly versioned platform-level content such as a criteria catalog edition.

Implementation subtasks:

- [ ] Implement snapshot and amendment primitives from M0-A02.
- [ ] Implement the canonical hashing and verification.
- [ ] Prove it through R1-11's workforce snapshot slice.
- [ ] Prove allowed and denied behavior, failure states, history, and standalone/split-host parity with focused tests.

First consumer: R1-11d workforce snapshot

Depends on: M0-A01, M0-A02

Blocks: R1-08, R1-11d, R2-06, R2-09

### EN-04 Record attributable review and approval decisions with separation of duties

M0-D23's [decision](decisions/m0-d23-assurance-vocabulary.md) makes the
immutable decision shape reusable while each subject workflow owns its review
request, allowed outcomes, state transition, and history. A decision in one
workflow never approves a different record.

Priority: P0

Type: enabler

Area: controls

Why: Boundary, controls, mappings, commitments, policies, evaluations, and findings all require approval of an exact version with separation-of-duties checks. The domain model forbids a universal Review aggregate, so this delivers a decision primitive that each workflow's own state machine uses.

Scope:

- A decision record bound to the exact input version, actor, time, rationale, and outcome.
- Separation-of-duties evaluation and approved small-team exceptions (M0-D03).
- Reviewer assignment hooks into responsibilities.

Out of scope:

- Generic workflow states or a universal review queue.
- Workflow-specific outcomes (owning stories).

Acceptance criteria:

- [ ] A decision always identifies the exact version it applies to and cannot be moved to another version.
- [ ] Self-review is blocked on the server unless an approved exception applies, and the denial is explained.
- [ ] Decisions are immutable; a later decision supersedes and never overwrites.
- [ ] Each consuming workflow keeps its own lifecycle states.

Implementation subtasks:

- [ ] Implement the decision primitive and separation-of-duties evaluation.
- [ ] Integrate with EN-01 authorization and EN-02 versions.
- [ ] Prove it through R1-02 boundary approval.
- [ ] Prove allowed and denied behavior, failure states, history, and standalone/split-host parity with focused tests.

First consumer: R1-02

Depends on: M0-D03, M0-D23, EN-01

Blocks: F1-07, F1-08, R1-02, R1-04e, R1-05, R1-06, R1-13, R2-02, R2-05, R2-07

### EN-05 Import with preview, reconciliation, and safe replay

Priority: P0

Type: enabler

Area: architecture

Why: The criteria catalog, readiness material, application inventory, workforce roster, technology inventory, and access populations all need the same staged import semantics.

Scope:

- Import batches with source and content identity.
- Staged parsing, validation, preview, all-or-nothing acceptance behind a durable batch visibility barrier, cancellation, and retry, following accepted M0-A06 (ADR 0005).
- The M0-A06 standalone/split API/worker spike, transferred here by product-owner decision on 2026-09-22.
- Rejected-item reports and replay detection (unchanged, changed, new, conflicting, missing).
- Background processing with progress across host modes.

Out of scope:

- Per-source mappings and business validation (owning stories).

Acceptance criteria:

- [ ] Nothing imported becomes active before explicit acceptance.
- [ ] A failed or canceled import leaves no partial active records.
- [ ] Replaying the same source creates no duplicates and reports unchanged rows.
- [ ] Missing rows produce explicit reconciliation states and never silent deletion.
- [ ] Progress and failures are visible and recoverable in standalone and split hosts.
- [ ] Import batches, staged rows, and rejected-item reports belong to one organization and can never be accepted into another.

Implementation subtasks:

- [ ] Implement the canonical external-identifier, observation, source, and correlation semantics approved in M0-D28; reject source fields or schemas that are absent from the approved public-reference register.
- [ ] Implement batch, staging, and preview primitives.
- [ ] Implement replay and reconciliation classification.
- [ ] Implement worker execution and progress.
- [ ] Prove it through R1-10b application import.
- [ ] Prove allowed and denied behavior, failure states, history, and standalone/split-host parity with focused tests.
- [ ] Prove the ADR 0005 barrier in standalone and split API/worker hosts, including a worker crash and restart mid-accept (roll forward) and a cancellation before commit (nothing visible).

First consumer: R1-10b application import

Depends on: M0-A06, M0-D28, EN-01

Blocks: R1-03, R1-09, R1-10, R1-11, R1-12, R2-06

### EN-06 Store governed artifacts with immutable content identity

Priority: P0

Type: enabler

Area: evidence

Why: Evidence, policy documents, provider reports, and imported files need one artifact store with content identity, validation, quarantine, and per-artifact authorization.

Scope:

- Upload, content hashing, validation, and quarantine, following M0-A03.
- Artifact-level authorization for download and preview.
- Derived-artifact relationships (for example, redactions) that never replace the source.

Out of scope:

- Evidence requests and support relationships (R2-03).
- Retention, hold, and disposition policy (R2-12).

Acceptance criteria:

- [ ] An artifact's content can never be replaced silently; a correction is a new artifact.
- [ ] Unsupported, oversized, interrupted, or failed-inspection uploads never appear successful.
- [ ] Permission to view a related record does not grant access to a restricted artifact.
- [ ] Downloads are authorized and recorded.
- [ ] Artifacts, derived artifacts, and content-identity deduplication never cross organizations.

Implementation subtasks:

- [ ] Implement the storage adapter and content identity.
- [ ] Implement validation, inspection, and quarantine.
- [ ] Implement authorized download.
- [ ] Prove it through R2-03 evidence upload.
- [ ] Prove allowed and denied behavior, failure states, history, and standalone/split-host parity with focused tests.

First consumer: R2-03

Depends on: M0-A03, EN-01

Blocks: R1-14, R2-02, R2-03, R2-08

## R2 - Control environment implemented

Business outcome: required controls and policies are implemented and evaluated; policies are communicated and acknowledged; evidence is captured with provenance and its handling status is explicit, with any undelivered evidence-governance capability (retention, hold, redaction, disclosure) shown as an acknowledged gap; actual human and NHI access is reconciled with approved expectations; accountable work and gaps are visible; and the team can make an evidence-backed Type I entry decision.

### R2-01 Assign control ownership and operating cadence

Priority: P0

Area: controls

User story: As a compliance lead, I want every active control assigned to an accountable owner with a clear cadence so that required work does not depend on personal memory.

Business objective: convert the control catalog into an executable compliance operating plan.

Requirements:

- Assign owner, backup owner, reviewer, operating cadence, expected evidence, and effective date.
- Support event-driven, ad hoc, and recurring controls in user language.
- Separate design, implementation, operation, and evidence-review status.
- Preserve assignment and cadence changes.

Domain slice:

- Owns effective `Responsibility` assignments and approved `ControlCadence` for
  a specific control version and scope.
- Uses active platform members or teams, access grants, separation-of-duties
  rules, expected evidence, and control lifecycle.
- Produces accountable work and the expected occurrence plan without treating
  access roles as assignments or rewriting historical performers.

Acceptance criteria:

- [ ] Active controls without required ownership or cadence are visible as blockers.
- [ ] Owners see their current and upcoming responsibilities.
- [ ] Changing ownership reassigns open work explicitly without changing historical authorship.
- [ ] Retiring a control stops future work and leaves existing work and evidence readable.

Implementation subtasks:

- [ ] Define owner, backup, reviewer, cadence, effective-interval, team-assignment, conflict, and orphaned-work invariants.
- [ ] Deliver authorized assignment and cadence preview, approval, revision, and work-view behavior through the API and browser.
- [ ] Connect approved cadence to occurrences and responsibilities to the shared work queue, authorization decisions, evidence expectations, and readiness blockers.
- [ ] Prove reassignment, team membership change, deprovisioning, schedule revision, retirement, conflicts, history, and denied behavior end to end.

### R2-02 Maintain approved policies and review cycles

Priority: P0

Area: policies

User story: As a policy owner, I want to draft, review, approve, publish, and periodically revisit policies so that the team can show which governance was effective at any point in time.

Business objective: replace disconnected documents with an accountable, versioned policy lifecycle tied to the control environment.

Requirements:

- Capture owner, purpose, audience, review cadence, content or source file, effective date, and applicability to applications, reviewed systems, controls, criteria, risks, vendors, processes, or organizational scope.
- Support draft, in review, approved, superseded, and retired states.
- Record review comments and approval decisions.
- Flag policies missing from required control coverage or overdue for review.
- Preview the downstream impact of applicability or content changes before a successor version becomes effective.
- Allow deletion only for a never-used draft with no retained relationships; supersede or retire every approved or historically used policy.

Domain slice:

- Owns `Policy`, immutable `PolicyVersion`, source document identity, lifecycle,
  effective interval, policy review, and approval decisions.
- Uses platform responsibilities, related controls, evidence artifacts, and approved review cadence.
- Produces the effective policy record used by readiness and engagement
  snapshots without claiming that document approval proves control operation.
- Owns version-aware `PolicyApplicability` relationships to applications,
  reviewed systems, controls, criteria, risks, vendors, processes, and scope;
  those relationships support navigation and impact analysis but do not count
  as control coverage or operating evidence.

Example:

- An approved Access Control Policy version applies to the AWS and GitHub
  reviewed systems and supports several access controls. Revising its content
  or applicability creates a successor for review. The earlier Type I snapshot
  and package retain the exact prior version and relationships. Neither policy
  version proves that a quarterly access review occurred.

Acceptance criteria:

- [ ] Only an approved version can be represented as the current policy.
- [ ] The team can retrieve the version effective on a selected date.
- [ ] Superseding a policy preserves prior approvals and audit relationships.
- [ ] A policy document alone cannot make an operating control appear performed.
- [ ] A proposed policy or applicability change identifies affected controls, systems, owners, review work, readiness, and engagements before approval.
- [ ] Deletion is available only for an unused draft; a policy used by a control, decision, snapshot, handoff, or package can only be superseded or retired.

Implementation subtasks:

- [ ] Define policy identity, draft editing, immutable approved versions, effective dates, applicability relationships, approval authority, review cadence, supersession, retirement, and narrowly permitted deletion.
- [ ] Deliver authorized draft, upload or author, review, approve, publish, supersede, retire, and retrieve-by-date behavior through the API and browser.
- [ ] Deliver authorized relationship editing, successor proposal, impact preview, approval, and unused-draft deletion through the same API and browser workflow.
- [ ] Connect policies to applications, reviewed systems, controls, criteria, risks, vendors, processes, evidence, work queues, readiness, requests, and frozen snapshots while keeping policy state separate from control coverage and performance.
- [ ] Prove exact-version approval, overdue review, concurrent edits, rejected documents, historical retrieval, forbidden actions, and downstream readiness effects end to end.

### R2-03 Request and capture trustworthy evidence

Priority: P0

Area: evidence

User story: As a control owner, I want to submit files, links, and notes with clear provenance so that reviewers can understand what the evidence supports and when it applied.

Business objective: make evidence findable, attributable, period-aware, and safe enough to support an examination.

Requirements:

- Capture title, description, type, source, collector, capture time, covered period, sensitivity, and relationships.
- Preserve file identity and prevent silent replacement.
- Support evidence requests with owner, due date, instructions, and expected support.
- Allow explicit reuse while warning about period or freshness mismatch.

Domain slice:

- Owns `EvidenceArtifact`, immutable content identity, sensitivity, provenance,
  `EvidenceRequest`, and contextual `EvidenceSupport` relationships.
- Uses active members and responsibilities, controls or other supported records,
  applicable periods, collection or import sources, and sharing authorization.
- Supplies exact artifacts and support assertions to review, readiness,
  occurrences, access campaigns, auditor requests, and packages.

Acceptance criteria:

- [ ] A contributor can submit evidence and see upload or validation progress and outcome.
- [ ] Unsupported, oversized, interrupted, duplicate, or unauthorized submissions fail safely without a false success.
- [ ] Reviewers can locate evidence by control, request, source, owner, period, and status.
- [ ] Historical links survive correction, replacement, and later-period reuse.

Implementation subtasks:

- [ ] Define artifact identity, content hashing, provenance, sensitivity, covered-period, request, reuse, correction, and support-relationship rules.
- [ ] Deliver authorized request, upload or link, validate, inspect, relate, correct, reuse, and retrieve behavior through the API, any bounded background processing, and browser.
- [ ] Apply artifact and relationship authorization everywhere evidence appears; record source, collector or system actor, failures, review context, and sharing history.
- [ ] Prove duplicate, oversized, interrupted, unauthorized, stale, outside-period, corrected, and reused evidence behavior plus package retrievability end to end.

### R2-04 Perform a control and attest to the result

Priority: P0

Area: controls

User story: As a control owner, I want to record each control performance, outcome, and support so that the organization can demonstrate implementation rather than only describe design.

Business objective: establish attributable operating evidence for the Type I point in time and later Type II occurrences.

Requirements:

- Capture performer, performed time, covered period, result, notes, and supporting evidence.
- Support complete, failed, not applicable, and skipped results with appropriate rationale.
- Apply the expected evidence and instructions from the control while preserving instance-specific context.
- Correct completed attestations by revision, not silent replacement.

Domain slice:

- Owns a `ControlOccurrence`, its expected period or triggering event, and
  versioned `ControlAttestation` with result and performer attribution.
- Uses the exact control version, effective responsibility and cadence, expected
  evidence definition, and contextual evidence-support relationships.
- Produces review submissions, exceptions or gaps, occurrence populations, and
  readiness effects without changing the control definition.

Acceptance criteria:

- [ ] An owner can complete the performance only when required information and support are present.
- [ ] Failed, skipped, or not-applicable results create the required gap or review path.
- [ ] The product distinguishes performance from independent review.
- [ ] A reviewer can reconstruct the exact attestation and evidence that existed at completion.

Implementation subtasks:

- [ ] Define occurrence identity, expected-versus-ad-hoc semantics, result states, required rationale and support, submission, correction, and performer rules.
- [ ] Deliver authorized view, perform, attach support, attest, submit, and correct behavior through the API and accessible browser workflow.
- [ ] Route submissions into independent review and failed, skipped, missing, or not-applicable outcomes into the shared gap or exception path and readiness model.
- [ ] Prove exact control and evidence versions, self-action restrictions, concurrent submission, correction history, population reconciliation, and denied behavior end to end.

### R2-05 Review control design, implementation, and evidence

Priority: P0

Area: controls and evidence

User story: As a compliance reviewer, I want to evaluate each control's design
and implementation using a reproducible procedure and exact evidence so that
weak or unproven controls are corrected before management or an auditor relies
on them.

Business objective: create an explainable Type I quality gate that distinguishes
control design, implementation, and evidence sufficiency from control operation
over a Type II period.

Requirements:

- Define the evaluation objective and procedure, assertions being tested,
  population or item inspected when applicable, expected support, and required
  tester competence or independence.
- Evaluate design suitability, implementation as of the relevant date, and
  evidence sufficiency as distinct conclusions; do not imply Type II operating
  effectiveness from a readiness or Type I evaluation.
- Bind the evaluation to exact boundary, commitment, requirement, risk,
  criterion, control, implementation narrative, asset, provider, policy, and
  evidence versions.
- Record work performed, item or sample inspected, deviation, result, reviewer,
  rationale, comments, and time.
- Provide a review queue with scope, owner, age, materiality, failed procedures,
  deviations, and missing support.
- Enforce separation-of-duties rules and approved small-team exceptions.
- Preserve the evaluation plan, execution, submission, decision, and
  remediation across resubmission and retest.

Domain slice:

- Owns versioned `ControlEvaluationPlan`, `ControlEvaluation`, procedure result,
  `ControlEvaluationDeviation`, assigned review, immutable `ReviewDecision`, comments,
  separation-of-duties evaluation, retest, and approved exception to that policy.
- Uses the exact control design, implementation, occurrence, attestation, or
  evidence submission being reviewed plus member access and responsibility.
- Produces distinct design, implementation, and evidence conclusions, accepted
  state, requested rework, rejection, finding or exception, and explainable
  readiness effects without mutating the evaluated inputs.

Acceptance criteria:

- [ ] The user can reproduce what was evaluated, which exact versions and items were inspected, what procedure was performed, and how each conclusion was reached.
- [ ] Design, implementation, evidence sufficiency, and Type II operating effectiveness cannot be conflated in the API, UI, readiness calculation, or export.
- [ ] Deviations and failed procedures produce an owned finding, corrective action, approved exception, or unresolved result rather than disappearing inside reviewer notes.
- [ ] Accepted, rejected, and change-requested decisions produce clear next states, including retest when required.
- [ ] Rejection reopens the correct work and never deletes the submitted evidence.
- [ ] Unauthorized self-review is blocked by the server and explained in the UI.
- [ ] Readiness status reflects the latest valid review without hiding prior decisions.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D13 and M0-D03 and define plan versions, assertion and procedure results, applicable populations or inspected items, conclusions, deviations, retest, review assignment, and independence rules.
- [ ] Deliver authorized plan, assign, perform, document, submit, inspect, comment, accept, reject, request-change, remediate, and retest behavior through the API and browser.
- [ ] Bind evaluations to exact scope, inventory, commitment, risk, criterion, control, policy, provider, and evidence versions and connect decisions to work, findings, readiness, descriptions, and snapshots.
- [ ] Prove insufficient and conflicting evidence, failed procedures, deviation handling, design-versus-operation distinctions, self-review denial, concurrent revisions, rework, retest, small-team exceptions, and historical reproducibility end to end.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R2-05a Plan a reproducible control design and implementation evaluation

Outcome: As a compliance reviewer, I can define a versioned evaluation plan: objective, procedure, assertions, inspected items, expected support, and tester independence, all bound to exact record versions.

Acceptance criteria:

- [ ] An evaluation plan is versioned and bound to the exact boundary, commitment, risk, criterion, control, policy, provider, and evidence versions it evaluates.
- [ ] Tester competence and independence requirements are recorded and checked when work is assigned.

Not in this slice:

- Performing the evaluation (R2-05b).

#### R2-05b Perform and document design, implementation, and evidence conclusions

Outcome: As a compliance reviewer, I can perform the planned procedure and record separate design, implementation, and evidence-sufficiency conclusions with the items I inspected.

Acceptance criteria:

- [ ] The user can reproduce what was evaluated, which exact versions and items were inspected, what procedure was performed, and how each conclusion was reached.
- [ ] Design, implementation, evidence sufficiency, and Type II operating effectiveness cannot be conflated in the API, UI, readiness calculation, or export.

Depends on: R2-05a

#### R2-05c Independently review evaluations with separation of duties

Outcome: As an assigned reviewer, I can accept, reject, or request changes to an evaluation, and self-review is prevented unless an approved exception applies.

Acceptance criteria:

- [ ] Accepted, rejected, and change-requested decisions produce clear next states, including retest when required.
- [ ] Rejection reopens the correct work and never deletes the submitted evidence.
- [ ] Unauthorized self-review is blocked by the server and explained in the UI.
- [ ] Readiness status reflects the latest valid review without hiding prior decisions.

Depends on: R2-05b

#### R2-05d Route deviations to findings and retest

Outcome: As a compliance lead, I see every deviation or failed procedure become an owned finding, corrective action, approved exception, or unresolved result, and retests preserve history.

Acceptance criteria:

- [ ] Deviations and failed procedures produce an owned finding, corrective action, approved exception, or unresolved result rather than disappearing inside reviewer notes.
- [ ] Retest preserves the original plan, execution, decision, and remediation.

Depends on: R2-05c, R2-07

### R2-06 Reconcile application access and complete the initial review

Priority: P0

Area: access review

User story: As an access reviewer, I want to compare the complete point-in-time
access population for our reviewed applications with approved expectations and
verify required removals or changes so that the Type I environment has
demonstrably appropriate access.

Business objective: replace disconnected account exports and intuition with a
complete, explainable reconciliation of the access that exists, the access the
organization expects, the decisions reviewers made, and the fixes it verified.

Requirements:

- Select applications and reviewed systems from the governed inventory and
  identify the system owner, access owner, required population sources, review
  cadence, and privileged or otherwise sensitive entitlements.
- Use the governed R1-11 person and service-identity roster with its accepted
  lifecycle, manager or owner, source identifiers, capture time, and unresolved
  identity questions; reconcile provider observations without creating a
  competing roster (M0-D22).
- Classify access subjects explicitly as human or non-human identities (NHIs).
  Human subjects include employees, contractors, and external collaborators;
  NHIs include workloads, services, integrations, automation, and bots and
  require an accountable human or team owner, approved purpose, environment,
  lifecycle, and authentication or credential model.
- Import the actual external accounts, groups, roles, entitlements, resources,
  direct and nested memberships, and direct or inherited access grants for each
  reviewed system through validated source snapshots.
- Preserve every provider object identifier and grant path and surface unlinked,
  duplicate, shared, inactive, orphaned, privileged, and non-human principals
  rather than matching them silently by email or display name.
- Keep groups and roles as external access structures rather than classifying
  them as human or NHI subjects. Treat provider-suggested classifications as
  proposals and preserve ambiguous, generic, shared, ownerless, dormant, and
  mixed-use accounts for deliberate resolution.
- Record approved access expectations, including expected grants, prohibited
  grants or account types, zero-tolerance expectations, required access,
  rationale, approver, effective interval, and expiring exceptions.
- Preview normalization, identity correlation, group expansion, effective-access
  calculation, source rejection, and expected-versus-observed variance before
  accepting the population.
- Freeze the launched population, reviewers, instructions, and deadline.
- Require every in-scope reviewed system to have an accepted population or an
  explicit approved exception; a missing export cannot appear as zero access.
- Record keep, modify, revoke, and unable-to-determine decisions with rationale.
- Use expectations to focus review without automatically deciding any item.
- Track required changes through independent verification.

Domain slice:

- Uses governed `Application` and `ReviewedSystem` records and owns
  `AccessSubject`, `DirectoryPrincipal`, external group membership, `Entitlement`,
  `ExternalAccessGrant`, `PopulationSnapshot`, `AccessReviewCampaign`,
  `AccessExpectation`, `AccessReviewItem`, `AccessDecision`, remediation, and verification.
- A population item is a frozen external principal, entitlement, resource, and
  reviewed-system relationship with its direct or inherited grant path.
  Accounts, groups, roles, service principals, and workload identities use
  source object identifiers; email is descriptive only. Accounts and service
  principals may correlate to explicitly classified human or NHI access
  subjects, while groups and roles remain access structures.
- Keeps external principals and groups separate from platform members and
  teams. Any correlation is explicit and cannot grant platform access or rewrite
  historical activity.
- Uses platform members, teams, access grants, and responsibilities only for
  campaign administration, assignment, decision attribution, and authorization.
- Produces reviewed population evidence, gaps or exceptions, verified
  remediation, control support, and readiness effects.

Example:

- The approved expectation for an AWS account states that no IAM users may
  exist because workforce and workload access must use federated identities and
  roles. If a source snapshot contains an IAM user, Compliance identifies the
  observed prohibited principal and related grants as a variance. A reviewer
  must decide whether it requires removal, represents an approved time-bounded
  exception, or needs investigation. The campaign remains unresolved until the
  AWS-side change is independently verified or the exception is approved. A
  failed or missing AWS import is not interpreted as proof that zero IAM users exist.

Acceptance criteria:

- [ ] Invalid, duplicate, incomplete, or ambiguous import rows are shown before acceptance.
- [ ] Every in-scope application and reviewed system is reconciled to an accepted source snapshot or an explicit approved exception; missing source data never appears as zero access.
- [ ] Every accepted account, group, role, entitlement, membership, effective grant, and grant path can be traced to its provider object and source snapshot.
- [ ] Unlinked humans, inactive workers, shared accounts, service or workload identities, nested groups, privileged access, and ambiguous correlations remain visible for deliberate treatment.
- [ ] Every reviewed account or service principal has an explicit human, NHI, or unresolved classification; each NHI has an accountable owner and purpose or remains an actionable gap.
- [ ] Groups and roles remain distinguishable from human and NHI subjects, including when they convey inherited or assumable access.
- [ ] Expected, unexpected, prohibited, missing, and unresolved access are explainable from approved expectations but do not pre-decide a review outcome.
- [ ] Every population item has an attributable decision or explicit unresolved status.
- [ ] Bulk decisions require preview and shared rationale.
- [ ] A campaign cannot complete while required remediation is unverified unless an approved exception exists.
- [ ] The final campaign snapshot includes population, decisions, remediation, evidence, and history.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D07, M0-D05, M0-D06, and M0-D28 before finalizing this story's rules; every external semantic source must be in the approved public-reference register.
- [ ] Define person, work-relationship, account, and service-identity lifecycle and correlation; `Group` and direct `GroupMember` relationships; external roles and entitlements; direct access assignments and explainable effective-access paths; resource identity; and optional platform-membership correlation using the canonical model.
- [ ] Define source-snapshot completeness, normalization, correlation, group expansion, effective access, expectation and prohibition, variance, frozen campaign, assignment, bulk decision, remediation, independent verification, exception, and completion invariants.
- [ ] Deliver authorized roster and access-source import, preview, correction and acceptance, expectation authoring and approval, campaign launch, variance review, per-item and bulk decision, remediation, verification, and final snapshot through the API, bounded worker processing where needed, and browser.
- [ ] Feed campaign work into the shared work experience and its accepted or unresolved result into evidence, control support, findings, readiness, and engagement snapshots.
- [ ] Prove employee, contractor and collaborator humans; service, workload, integration, automation and bot NHIs; groups and roles; ambiguous and corrected classification; ownerless NHIs; direct, nested and inherited access; prohibited and zero-tolerance expectations; missing exports; duplicate grants; source changes; member deprovisioning; partial failure; unauthorized review; unverified remediation; and snapshot history end to end.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R2-06a Import and accept a point-in-time access population for a reviewed system

Outcome: As an access reviewer, I can import the actual accounts, groups, roles, entitlements, memberships, and grant paths for a reviewed system as a validated source snapshot.

Acceptance criteria:

- [ ] Invalid, duplicate, incomplete, or ambiguous import rows are shown before acceptance.
- [ ] Every in-scope application and reviewed system is reconciled to an accepted source snapshot or an explicit approved exception; missing source data never appears as zero access.
- [ ] Every accepted account, group, role, entitlement, membership, effective grant, and grant path can be traced to its provider object and source snapshot.
- [ ] Groups and roles remain distinguishable from human and NHI subjects, including when they convey inherited or assumable access.

Not in this slice:

- Classification (R2-06b).
- Expectations (R2-06c).
- Campaign decisions (R2-06d).

#### R2-06b Classify human and non-human principals and resolve their ownership

Outcome: As an access reviewer, I can explicitly classify each account or service principal as human, NHI, or unresolved, and correlate it to the workforce roster and NHI owners.

Acceptance criteria:

- [ ] Every reviewed account or service principal has an explicit human, NHI, or unresolved classification; each NHI has an accountable owner and purpose or remains an actionable gap.
- [ ] Unlinked humans, inactive workers, shared accounts, service or workload identities, nested groups, privileged access, and ambiguous correlations remain visible for deliberate treatment.
- [ ] Provider hints only propose a classification; a change preserves the prior decision.

Depends on: R1-11, R2-06a

#### R2-06c Approve access expectations and explain variance

Outcome: As an access-review owner, I can approve expected, required, and prohibited access (for example, no AWS IAM users) and see observed variance explained without any item being pre-decided.

Acceptance criteria:

- [ ] Expected, unexpected, prohibited, missing, and unresolved access are explainable from approved expectations but do not pre-decide a review outcome.
- [ ] Expectations record rationale, approver, effective interval, and expiring exceptions.

Depends on: R2-06a

#### R2-06d Launch a frozen review campaign and record access decisions

Outcome: As an access-review owner, I can launch a campaign that freezes its population, reviewers, instructions, and deadline, and reviewers can record keep, modify, revoke, or unable-to-determine decisions.

Acceptance criteria:

- [ ] Every population item has an attributable decision or explicit unresolved status.
- [ ] Bulk decisions require preview and shared rationale.
- [ ] The launched population, reviewers, instructions, and deadline are frozen.

Not in this slice:

- Remediation and verification (R2-06e).

Depends on: R2-06a

#### R2-06e Remediate, verify, and complete the campaign snapshot

Outcome: As an access-review owner, I can track required changes to independent verification and complete the campaign with a final snapshot.

Acceptance criteria:

- [ ] A campaign cannot complete while required remediation is unverified, unless an approved exception exists.
- [ ] The final campaign snapshot includes population, decisions, remediation, evidence, and history.
- [ ] Provider-side changes and platform-side verification remain distinct facts.

Depends on: R2-06d

### R2-07 Resolve findings, exceptions, and corrective actions

Priority: P0

Area: remediation

User story: As a compliance lead, I want deficiencies and exceptions converted into owned corrective work so that known gaps cannot disappear behind a readiness score.

Business objective: close material gaps or make their accepted risk explicit before the Type I entry decision.

Requirements:

- Capture source, description, severity, affected scope, owner, due date, root cause, and status.
- Link corrective actions, controls, criteria, evidence, access decisions, risks, and vendors.
- Support time-bounded risk acceptance with approver and rationale.
- Preserve reopening and closure history.

Domain slice:

- Owns `Finding`, `CorrectiveAction`, closure `ReviewDecision`, and required
  `Verification` with source wording preserved. It links approved `Waiver` and
  R1-07 `RiskAcceptance` decisions without owning a second acceptance authority.
- Uses affected scope, criteria, controls, evidence, access decisions, risks,
  vendors, responsibilities, and engagement or period references.
- Produces accountable remediation work and readiness or roll-forward effects;
  risk acceptance never masquerades as remediation or permanent closure.

Acceptance criteria:

- [ ] Closing a finding requires resolution evidence and authorized review.
- [ ] Expired exceptions return to an actionable state.
- [ ] Readiness views distinguish remediated, accepted, overdue, and unresolved items.
- [ ] Changing severity, ownership, or due date is attributable and visible.

Implementation subtasks:

- [ ] Define source, severity, lifecycle, ownership, due date, root cause, corrective action, verification, reopening, closure, and expiring risk-acceptance rules.
- [ ] Deliver authorized create or convert, relate, assign, investigate, remediate, accept risk, verify, close, reopen, and inspect behavior through the API and browser.
- [ ] Connect corrective work to the shared work queue, evidence and review workflows, readiness, engagement findings, and next-period commitments.
- [ ] Prove expired exceptions, missing closure evidence, source-text protection, reassignment, unauthorized approval, reopening, history, and downstream status reconciliation end to end.

### R2-08 Collaborate with a readiness advisor on work in progress

Priority: P0

Area: collaboration

User story: As a compliance lead currently working with readiness consultants, I want to share selected work in progress and capture their feedback in context so that misunderstandings and gaps are corrected before the Type I audit begins.

Business objective: make the live consultant-led readiness process faster and traceable without forcing the consultant into an unvalidated collaboration model.

Requirements:

- Let the compliance lead assemble a bounded set of draft or approved controls, policies, evidence, gaps, and questions for consultant review.
- Support the collaboration method selected with the consultants, such as an indexed handoff, secure scoped link, or time-bounded product access.
- Capture consultant requests, comments, recommendations, and validation status against the affected records.
- Distinguish consultant-provided wording from internal interpretation and decisions.
- Record what was shared, when, by whom, through which method, and what feedback was received.
- Firm advisors assigned to the client work as scoped platform members; the handoff workflow remains for external consultants engaged directly by the client (M0-D14).

Domain slice:

- Owns bounded `AdvisorHandoff`, content selection and manifest,
  `AdvisorFeedback`, external-author provenance, internal response, and delivery history.
- Uses exact versions of selected controls, policies, evidence, gaps, and
  questions plus the chosen handoff or scoped platform-access mechanism.
- Produces attributable feedback, requests, validation state, and assigned work
  without letting external text or access mutate internal source records.

Acceptance criteria:

- [ ] The compliance lead can preview exactly what will be shared before delivery.
- [ ] The consultant can review only intentionally shared material through the agreed workflow.
- [ ] Feedback becomes attributable, actionable work without letting an external collaborator silently alter source records.
- [ ] Internal approval, consultant validation, and future auditor conclusions remain distinct.
- [ ] Repeated handoffs identify changes instead of duplicating all previously shared material.
- [ ] If direct product access is not validated, the story can be completed through a secure handoff and feedback-capture workflow.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D14 and define selection, draft visibility, external authorship, validation meaning, delivery, revocation, and change-comparison rules.
- [ ] Deliver authorized assemble, preview, share or export, receive or record feedback, respond, assign, and revoke behavior through the API, any bounded package processing, and browser.
- [ ] Reuse platform membership and scoped access grants if direct access is selected; otherwise preserve external-author and delivery provenance without fabricating platform actors.
- [ ] Connect feedback to owning records, requests, work queues, gaps, review, and readiness; prove least privilege, repeated handoffs, exact versions, failures, and history end to end.

### R2-09 Make and record the Type I entry decision

Priority: P0

Area: readiness

User story: As the management approver, I want an explainable readiness review and sign-off so that we deliberately decide whether to begin the Type I examination.

Business objective: replace an informal go-live decision with an attributable review of scope, gaps, evidence, and accepted risks.

Requirements:

- Summarize scope; workforce, application, technology, and information
  inventories; commitments and requirements; control design and implementation
  evaluations; policy approval and communication; evidence review and
  governance; access expectations and review; risks; providers; findings; and exceptions.
- Drill from every status into the underlying records and calculation rules.
- Freeze the reviewed snapshot and unresolved items.
- Support approve, defer, and approve-with-exceptions decisions.

Domain slice:

- Owns the immutable `ReadinessSnapshot` and attributable `TypeIEntryDecision`,
  including unresolved-item acknowledgements and approved waivers. The snapshot
  binds the R1-08 readiness rule and assessment versions instead of defining a
  second readiness calculation.
- Uses the program boundary, workforce and system inventories, commitments and
  requirements, criteria, control environment and evaluations, policies and
  communication campaigns, governed evidence, access expectations and
  campaigns, risks, providers, findings, and their exact review states.
- Produces an approved transition or a new accountable work plan; it does not
  issue an auditor opinion or change its input records.

Acceptance criteria:

- [ ] No draft, stale, rejected, missing, or unverified material appears complete.
- [ ] All material unresolved items are explicitly acknowledged in the decision.
- [ ] The decision records approvers, rationale, time, and snapshot identity.
- [ ] Later changes do not silently alter the historical decision.
- [ ] The product states that internal approval is not an auditor opinion.
- [ ] Evidence-governance capabilities that are not yet delivered (R2-12) appear as explicitly acknowledged unresolved items in the decision rather than being omitted or treated as satisfied.

Implementation subtasks:

- [ ] Define readiness-snapshot contents, calculation identity, blocker rules, acknowledgement, required approvers, and approve/defer/approve-with-exceptions transitions.
- [ ] Deliver authorized preview, drill-down, sign-off, defer, and exception acknowledgement through the API and accessible browser workflow.
- [ ] Bind the decision to exact source versions and route deferrals or conditions into the shared finding, responsibility, and work model while preserving later changes separately.
- [ ] Prove incomplete and stale inputs, unresolved acknowledgements, separation of duties, concurrent changes, denied approval, immutable history, and transition effects end to end.

### R2-10 Publish policies and verify acknowledgement and training

> [!NOTE]
> Promoted from P1 to P0 on 2026-09-14. The R1/R2 milestone exits and the P0 readiness stories R1-08 and R2-09 depend on this capability, and it covers SOC 2 Security criteria a Type I auditor routinely tests (risk assessment CC3, vendor oversight CC9.2, policy communication CC1/CC2).

Priority: P0

Area: workforce assurance

User story: As a policy owner, I want to distribute each approved policy to its
applicable audience and verify acknowledgement and required training so that we
can demonstrate that governance was communicated and acted upon.

Business objective: distinguish approving a policy from proving that the right
workforce population received, acknowledged, and completed the exact material
required of them.

Requirements:

- Create a campaign from an exact approved policy or training requirement,
  frozen workforce population, applicable dates, owner, instructions, due date,
  and approved exceptions.
- Determine the audience from explicit rules and an accepted workforce snapshot;
  additions, departures, and role changes after launch remain explainable.
- Record delivery, acknowledgement, training assignment and completion,
  attestation, source, time, evidence, reminders, exceptions, and failed or
  missing delivery for each person.
- Accept reviewed completion records from an external LMS or manual evidence
  without pretending Compliance delivered the training content.
- Prevent a later policy or course version from satisfying an earlier campaign
  silently and prevent acknowledgement from proving comprehension or unrelated
  control operation.
- Route overdue, refused, failed, excused, and missing-source results into
  accountable work, review, and readiness.

Domain slice:

- Owns `PolicyDistributionCampaign`, `TrainingRequirement`, frozen audience,
  attributable acknowledgement, completion observation, reminder state,
  approved exception, and campaign result.
- Uses exact approved policy versions, workforce snapshots, responsibilities,
  source-aware completion evidence, and applicable control and criteria relationships.
- Produces reviewed communication and training evidence, gaps, audit
  populations, readiness effects, and stable engagement-snapshot records without
  becoming an LMS.

Acceptance criteria:

- [ ] The team can explain why every person was included, excluded, added, or removed from a campaign and which workforce snapshot supplied that decision.
- [ ] Each acknowledgement or completion identifies the exact policy or training version, person, source, time, and evidence.
- [ ] Missing delivery, overdue work, source failure, declined acknowledgement, and approved exceptions remain distinct and drillable.
- [ ] Policy supersession does not rewrite a launched or completed campaign and can deliberately trigger a successor campaign.
- [ ] Campaign totals reconcile to individually inspectable people and results and can supply a frozen audit population.
- [ ] Restricted workforce details and training records are least-privilege while appropriate aggregate and audit evidence remain available.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D12 and M0-D06 before finalizing this story's rules.
- [ ] Define campaign identity, version binding, audience rules and freeze, delivery, acknowledgement, completion observation, reminder, exception, reconciliation, and successor rules.
- [ ] Deliver authorized assemble, preview, launch, deliver or import, acknowledge, remind, reconcile, review, close, and inspect behavior through the API, bounded processing where needed, and browser.
- [ ] Connect campaigns to workforce, policies, training sources, evidence, controls, work queues, findings, readiness, populations, packages, and snapshots without duplicate person or policy records.
- [ ] Prove audience changes, wrong versions, missing and partial sources, duplicate completion, failed delivery, overdue and exception states, authorization, replay, and population reconciliation end to end.

### R2-11 Manage accountable compliance work

Priority: P0

Area: work management

User story: As a compliance contributor, I want one prioritized view of the
work assigned to me and my team so that due, blocked, returned, and unowned
compliance work is acted on without maintaining another task spreadsheet.

Business objective: turn the connected compliance record into a usable daily
operating system while preserving each source workflow as the authority for
completion and status.

Requirements:

- Present my, team, unassigned, due, overdue, blocked, returned, awaiting-review,
  and externally waiting work across controls, policies, training, evidence,
  access campaigns, risk and provider reviews, findings, requests, approvals,
  and remediation.
- Show business priority, materiality, source workflow, applicable program or
  engagement, owner, reviewer, due date, blocked reason, and next valid action.
- Let authorized users assign, reassign, claim, delegate, or escalate work while
  preserving responsibility history and separation-of-duties constraints.
- Complete or change source work only through that workflow's rules; the work
  view cannot independently mark a control, decision, request, or remediation complete.
- Provide configurable in-product reminders and digests with deduplication,
  quiet behavior, delivery result, and direct links; do not require email for
  the core work queue to function.
- Surface orphaned work immediately when membership, teams, responsibilities,
  scope, or source records change.
- Scope the queue to the active client organization; the cross-client queue for firm staff is delivered by F1-05.

Domain slice:

- Owns the `AccountableWorkItem` projection, user preferences for work views and
  notifications, delivery attempts, acknowledgement, and escalation history.
- Uses responsibilities, due dates, states, allowed actions, priority, and
  materiality from each explicit source workflow plus current platform access.
- Produces consistent navigation, assignment changes, reminders, operating
  measures, and readiness drill-down without becoming a second task or status store.

Acceptance criteria:

- [ ] Every displayed work item resolves to one authoritative source record and its current allowed action; counts reconcile after source changes.
- [ ] A contributor can understand what is required, why it matters, when it is due, what blocks it, and where to act without opening a parallel tracker.
- [ ] Assignment and escalation obey authorization and separation of duties, preserve history, and expose orphaned work.
- [ ] Completing source work removes or advances the projected item; changing only the projection can never create false completion.
- [ ] Reminder retries, duplicate suppression, disabled preferences, and delivery failures do not alter the underlying due state or hide overdue work.
- [ ] Restricted work is absent rather than discoverable to unauthorized users, including through counts, notifications, and search.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D15 before finalizing this story's rules.
- [ ] Define projection identity, source-state mapping, assignment and delegation, orphaning, due and blocked semantics, notification preference, delivery, deduplication, and reconciliation rules.
- [ ] Deliver authorized personal and team queues, filters, assignment actions, source navigation, reminder preferences, digest, acknowledgement, and escalation through the API, bounded processing, and browser.
- [ ] Integrate source workflows through stable identities and explicit allowed actions; derive counts and measures from those records without introducing generic completion state.
- [ ] Prove stale projections, concurrent source changes, revoked access, team changes, self-review conflicts, orphaning, duplicate and failed reminders, restricted search, and standalone/split-host parity end to end.

Delivery slices: this story is delivered through the following outcome slices, tracked as GitHub sub-issues. The parent's requirements, domain slice, and definition of done apply to every slice, and the parent is complete only when all slices are done.

#### R2-11a See my and my team's work projected from source workflows

Outcome: As a compliance contributor, I can see my, my team's, and unassigned work across source workflows, with what is required, why, when it is due, and where to act.

Acceptance criteria:

- [ ] Every displayed work item resolves to one authoritative source record and its current allowed action; counts reconcile after source changes.
- [ ] A contributor can see what is required, why it matters, when it is due, what blocks it, and where to act without opening a parallel tracker.
- [ ] Completing source work removes or advances the projected item; changing only the projection can never create false completion.
- [ ] Restricted work is absent, not merely hidden, for unauthorized users, including in counts and search.

Not in this slice:

- Assignment actions (R2-11b).
- Reminders and digests (R2-11c).

#### R2-11b Assign, claim, reassign, and escalate work with separation of duties

Outcome: As an authorized user, I can assign, claim, delegate, reassign, or escalate work while responsibility history and separation of duties are preserved.

Acceptance criteria:

- [ ] Assignment and escalation obey authorization and separation of duties, preserve history, and expose orphaned work.
- [ ] Orphaned work appears immediately when membership, teams, responsibilities, scope, or source records change.

Depends on: R2-11a

#### R2-11c Receive in-product reminders and digests

Outcome: As a contributor, I can receive configurable in-product reminders and digests with direct links, without email being required.

Acceptance criteria:

- [ ] Reminder retries, duplicate suppression, disabled preferences, and delivery failures do not alter the underlying due state or hide overdue work.
- [ ] Restricted work never appears in notifications to unauthorized users.

Depends on: R2-11a

### R2-12 Govern evidence access, retention, and disclosure

Priority: P1

Area: evidence governance

User story: As a compliance lead, I want sensitive evidence retained, shared,
redacted, held, and disposed of according to explicit policy so that supporting
an audit does not create an unmanaged archive of credentials, personal data, or
customer information.

Business objective: protect sensitive evidence throughout its useful life while
preserving immutable identity, engagement reliance, and demonstrable delivery history.

Requirements:

- Apply a reviewed handling classification and retention rule to evidence,
  packages, imports, workforce snapshots, provider reports, and auditor responses.
- Enforce artifact-level access and explicit external disclosure independently
  from permission to view the supported control or request.
- Support engagement or legal holds that prevent disposition while recording
  scope, authority, reason, effective interval, and release.
- Create redacted or transformed derivatives with their own content identity,
  provenance, approver, and relationship to the protected source; never replace
  the source silently.
- Preview authorized disposition, identify active holds or historical reliance,
  require approval, record the result, and ensure unavailable content cannot be
  mistaken for present evidence.
- Record every external delivery and revocation where supported, while
  acknowledging that downloaded copies cannot be technically recalled.
- Quarantine files that fail content validation or security inspection and keep
  failure visible without making the artifact usable.

Domain slice:

- Owns `EvidenceHandlingPolicy`, classification assignment, retention schedule,
  `EvidenceHold`, disclosure decision, redacted derivative relationship,
  quarantine state, authorized disposition, and delivery history.
- Uses immutable evidence and package identities, source provenance, sensitivity,
  engagement references, findings, requests, platform authorization, and
  applicable commitments or system requirements.
- Produces enforceable artifact availability and sharing decisions for every
  review, request, package, export, and historical view without altering the
  business state the evidence supports.

Acceptance criteria:

- [ ] Every governed artifact has an explainable classification, effective retention rule, access policy, source identity, and current availability state.
- [ ] Users who may view a control cannot infer, preview, download, export, or share a restricted artifact without separate authorization.
- [ ] A hold blocks disposition and survives ordinary retention expiry until an authorized release is recorded.
- [ ] Redacted derivatives and later corrections preserve their source relationships, approvals, and distinct content identities.
- [ ] Disposition cannot remove content still required by a frozen or delivered engagement record without an explicit permitted policy and visible impact.
- [ ] Quarantined, missing, disposed, and access-denied content cannot appear as accepted or successfully packaged evidence.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D16 before finalizing this story's rules.
- [ ] Define policy and assignment versions, artifact-level authorization, hold precedence, derivative identity, quarantine, disposition, delivery, availability, and historical-reliance rules.
- [ ] Deliver authorized classify, restrict, hold, release, derive, review, disclose, revoke where possible, preview-disposition, dispose, and inspect-history behavior through the API, bounded processing, and browser.
- [ ] Apply the same decisions to evidence, imports, provider reports, workforce snapshots, responses, exports, and packages and expose availability to readiness and validation without duplicating artifacts.
- [ ] Prove malicious or invalid content, restricted metadata, stale policy, active holds, redaction errors, partial disposition, downloaded-copy warnings, denied sharing, package validation, and historical traceability end to end.

## T1 - SOC 2 Type I supported

Business outcome: the team can establish the point-in-time engagement, approve
the system description and management representations, support auditor requests,
provide a reproducible handoff, preserve the examined snapshot, record the
auditor-supplied result, and create a Type II operating plan.

### T1-01 Establish the Type I engagement and point-in-time snapshot

Priority: P1

Area: audit

User story: As a compliance lead, I want to establish the Type I date and freeze the applicable scope and control versions so that everyone works from the same examination baseline.

Business objective: prevent later operational changes from rewriting what was represented at the Type I point in time.

Requirements:

- Capture engagement, audit firm, Type I date, categories, approved boundary, and management contacts.
- Snapshot applicable workforce, applications, reviewed systems, system
  components, information and data flows, commitments, requirements, criteria,
  controls, mappings, evaluations, policies and communication results, access
  expectations and campaigns, risks, providers, evidence governance, and known findings.
- Identify unresolved scope differences before locking the baseline.
- Allow authorized amendments only as new attributable versions.

Domain slice:

- Owns `TypeIEngagement`, immutable `TypeIBaseline`, and attributable
  `BaselineAmendment`.
- Uses the approved boundary, workforce and inventory snapshots, commitments,
  requirements, criteria edition and selection, control and policy versions,
  evaluations, risks, providers, findings, evidence-handling decisions,
  management contacts, and audit firm.
- Supplies the single point-in-time source for the system description, auditor
  requests, Type I package, observations, and outcome.

Acceptance criteria:

- [ ] The team previews the complete baseline and blockers before confirmation.
- [ ] Confirming the baseline gives it a stable identity and time.
- [ ] Later source changes do not alter the frozen baseline.
- [ ] An amendment explains the reason, approver, changed scope, and affected package material.

Implementation subtasks:

- [ ] Define engagement identity, Type I date, baseline contents, blocker and scope-difference rules, confirmation, and amendment impact semantics.
- [ ] Deliver authorized engagement setup, baseline preview, drill-down, confirmation, and amendment behavior through the API and browser.
- [ ] Resolve baseline items to exact source versions and reuse the shared snapshot model for every downstream Type I workflow without cloning mutable alternatives.
- [ ] Prove incomplete previews, concurrent source changes, denied confirmation, amendments, package impact, historical stability, and standalone/split-host parity end to end.

### T1-02 Author, approve, and maintain the system description

Priority: P1

Area: audit

User story: As a compliance lead, I want to assemble, approve, and maintain the
system description from the Type I point in time through the Type II period so
that management and the auditor use one consistent narrative of the system
being examined.

Business objective: make the system narrative complete, reviewable, and tied to the same boundary and point-in-time records as the controls.

Requirements:

- Cover services, commitments and requirements, components, infrastructure,
  software, people, procedures, data, boundaries, control environment, and
  relevant changes using governed source relationships where they exist.
- Record complementary user-entity controls, subservice organizations, complementary subservice controls, and exclusions where applicable.
- Support structured section completeness, draft, review, approval, version
  comparison, report-ready export, and exact source-version reconciliation.
- Bind an approved point-in-time version to Type I, carry an attributable
  successor during Type II, identify significant in-period changes, and bind the
  final approved period version to the Type II close and management assertion.
- Keep the system description distinct from the control catalog.

Domain slice:

- Owns `SystemDescription`, immutable versions, structured sections, review
  submissions, approval decisions, and export identity.
- Uses the exact Type I baseline or Type II period, approved boundary,
  workforce and system inventories, referenced controls, providers, people,
  procedures, information, data flows, commitments, requirements, and relevant changes.
- Supplies the approved narrative and reconciliation state to management
  assertions and audit packages while remaining distinct from the boundary,
  inventory, and control catalog.

Acceptance criteria:

- [ ] Required sections show complete, incomplete, and not-applicable states with rationale.
- [ ] Reviewers can comment and approve the exact version.
- [ ] The approved description is bound to the Type I baseline.
- [ ] A Type II version identifies its covered period, significant changes, and exact close snapshot and cannot imply unchanged operation when source records changed.
- [ ] Stale, missing, or contradictory source relationships are visible before approval and package generation.
- [ ] Later edits create a new version and cannot alter a delivered package silently.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 before finalizing this story's rules.
- [ ] Define version, section completeness, source binding, contradiction and staleness, not-applicable, review, approval, Type I baseline, Type II period, assertion, and close-snapshot rules.
- [ ] Deliver authorized authoring, source linking and refresh, section status, review, approval, version comparison, period-change view, and export through the API and browser.
- [ ] Reuse platform responsibilities and review decisions, preserve governed source relationships, and feed the exact approved version into assertions, requests, populations, packages, and snapshots.
- [ ] Prove incomplete and contradictory sections, source-version changes, significant period changes, forbidden approval, concurrent edits, exact-version comments, export failure, and delivered-package stability end to end.

### T1-03 Give the auditor least-privilege engagement access

Priority: P2

Area: auditor collaboration

User story: As a compliance lead, I want to give the auditor access to the approved Type I material and request workflow so that collaboration is efficient without exposing unrelated or mutable workspace data.

Business objective: reduce insecure file sharing and duplicate uploads while preserving control over audit scope.

Requirements:

- Invite externally authenticated auditors into an explicit engagement scope.
- Support time-bounded access and immediate revocation.
- Share approved records by default and require explicit selection for work in progress.
- Record views, downloads, requests, comments, and access changes.
- For clients the firm itself examines, attest-staff access is governed by M0-D27 and F1-08; this story covers external auditors of advisory clients.

Domain slice:

- Uses platform `Member`, `ExternalIdentity`, scoped `AccessGrant`, and actor
  attribution for an auditor who actually signs in; it does not create a second auditor-user model.
- Owns engagement-scoped sharing selections, effective access interval, external
  visibility, and access activity for the Type I engagement.
- Uses exact approved or explicitly shared draft records and produces auditor
  requests and attributable activity without granting source-record mutation.

Acceptance criteria:

- [ ] The auditor cannot view another program, engagement, or unshared draft.
- [ ] The auditor can inspect and download shared support but cannot mutate source records.
- [ ] The UI makes external visibility clear before material is shared.
- [ ] Revoked access fails immediately while prior activity remains attributable.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 and define scope, draft-sharing, time-bound grant, download, comment, and revocation rules.
- [ ] Deliver provider-authenticated auditor activation and engagement-scoped sharing through the existing membership and authorization API and browser experience.
- [ ] Enforce read, download, request, and comment permissions server-side on every referenced artifact while preserving visibility and actor history after revocation.
- [ ] Prove cross-program denial, unshared-draft denial, immediate revocation, expired grants, exact-version access, activity attribution, and safe external UI states end to end.

### T1-04 Respond to auditor requests and samples

Priority: P1

Area: auditor collaboration

User story: As a compliance contributor, I want auditor requests assigned, answered, reviewed, and resolved in context so that the team can respond without parallel email tracking.

Business objective: shorten response time and preserve exactly what was requested and delivered.

Requirements:

- Capture request, affected scope, priority, owner, due date, discussion, and status.
- Link existing records or submit new evidence without duplicating content.
- Review responses internally before external delivery when policy requires it.
- Preserve request and response versions.

Domain slice:

- Owns `AuditorRequest`, assignment, discussion, versioned `AuditResponse`,
  internal review decision, and external delivery record.
- Uses the Type I baseline, scoped auditor visibility, existing evidence-support
  relationships, new evidence artifacts, and platform responsibilities.
- Produces exact delivered responses and response-time history for the Type I
  package without copying existing evidence content.

Acceptance criteria:

- [ ] Contributors see the requests they own and the exact material requested.
- [ ] The auditor sees only responses intentionally delivered.
- [ ] Reopened requests preserve prior responses and explanation.
- [ ] Overdue, blocked, submitted, accepted, and resolved states are explainable and filterable.

Implementation subtasks:

- [ ] Define request source, lifecycle, assignment, response version, evidence linking, internal-review policy, external delivery, reopen, and due-date rules.
- [ ] Deliver authorized create or receive, assign, discuss, assemble, review, deliver, reopen, and filter behavior through the API and browser.
- [ ] Reuse evidence identity, support relationships, work queues, scoped external access, review decisions, and delivery history rather than creating audit-only copies.
- [ ] Prove missing and unauthorized evidence, rejected responses, late and reopened requests, concurrent edits, exact delivery visibility, reporting reconciliation, and history end to end.

### T1-05 Produce and validate the Type I package

Priority: P1

Area: audit package

User story: As a compliance lead, I want a reproducible indexed Type I package so that the auditor can trace scope, controls, policies, and evidence without an improvised folder tree.

Business objective: make handoff complete, bounded, and verifiable before external delivery.

Requirements:

- Build from the frozen Type I baseline and explicitly selected content.
- Produce a human-readable index and open machine-readable tables for the
  approved scope and boundary, inventories, commitments and requirements,
  criteria-to-control matrix, control design and implementation evaluations,
  policy register and communication results, evidence index, access-review
  results, risks, provider assessments, findings, system description, requests,
  responses, and delivery history.
- Include a management-assertion review input bound to the exact system
  description, criteria, controls, known exceptions, and package while keeping
  any auditor-provided form and final approval distinct.
- Detect missing, rejected, stale, broken, unauthorized, or inconsistent material.
- Support an auditor-agreed workbook, portal, or archive delivery without making
  a proprietary format the only retained record.
- Record package identity, creator, time, contents, content identities,
  validation and override results, delivery recipient, and amendments.

Domain slice:

- Owns immutable `AuditPackage`, `PackageManifest`, validation result, explicit
  sharing selection, generation attempt, and delivery record.
- Uses only the frozen Type I baseline and exact selected versions, artifacts,
  evaluations, communication and access-review results, system description,
  findings, management-assertion input, and authorized overrides.
- Produces a reproducible bounded handoff without becoming another source of
  truth for the included records.

Acceptance criteria:

- [ ] Every index entry resolves to the included record or artifact.
- [ ] Validation errors block a clean package; authorized overrides require rationale.
- [ ] Package generation reports progress and cannot claim success after partial failure.
- [ ] Regenerating the same snapshot produces an equivalent manifest.
- [ ] Delivered packages remain retrievable exactly as delivered.
- [ ] The control matrix, evidence index, and other tabular outputs reconcile to the same records as the human-readable index and manifest.
- [ ] Management-authored, auditor-authored, and product-calculated content are labeled and cannot be mistaken for one another.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 before finalizing this story's rules.
- [ ] Define manifest identity, output schemas, deterministic ordering, content identity, source authorship, authorization filtering, validation, override, generation, retention, amendment, and delivery rules.
- [ ] Deliver authorized preview, validate, generate, monitor, download, deliver, amend, and retrieve behavior through the API, bounded worker processing, and browser.
- [ ] Resolve every manifest entry to the shared record or artifact identity and record the member or system actor for each attempt and delivery.
- [ ] Prove missing, stale, rejected, unauthorized, inconsistent tables, interrupted, retried, and partially generated cases; equivalent regeneration; least privilege; attribution distinctions; amendments; and exact retrieval end to end.

### T1-06 Track Type I observations and audit findings

Priority: P1

Area: audit findings

User story: As a compliance lead, I want auditor observations and findings linked to the examined records and remediation plan so that nothing is lost between Type I and Type II.

Business objective: turn examination feedback into accountable improvement of the operating program.

Requirements:

- Capture source, description, classification, affected criteria and controls, owner, due date, response, and status.
- Distinguish auditor-provided wording from internal interpretation.
- Link corrective action and support.
- Preserve discussion, status, and closure history.

Domain slice:

- Uses the shared `Finding`, `CorrectiveAction`, evidence, review, and external-
  author provenance model rather than creating Type I audit-only findings.
- Binds each observation to the exact Type I baseline records and preserves
  auditor wording separately from internal analysis.
- Produces accountable Type II remediation commitments and readiness effects.

Acceptance criteria:

- [ ] Each item retains its exact source and relevant Type I snapshot relationships.
- [ ] Internal changes cannot silently alter auditor-provided wording.
- [ ] Closure requires evidence and authorized review.
- [ ] Open items flow into the Type II operating plan with visible priority and ownership.

Implementation subtasks:

- [ ] Define Type I source and baseline references, classification, external wording, response, corrective-action, closure, and roll-forward rules within the shared finding model.
- [ ] Deliver authorized capture or import, inspect, relate, assign, respond, remediate, review, close, and reopen behavior through the API and browser.
- [ ] Preserve auditor authorship and reuse evidence, responsibilities, work queues, review, readiness, and next-period planning without duplicating findings.
- [ ] Prove immutable source wording, denied edits and closure, missing evidence, reopen history, baseline traceability, and Type II plan continuity end to end.

### T1-07 Record the Type I outcome and approve the Type II plan

Priority: P1

Area: program transition

User story: As a management approver, I want to record the Type I result and approve the observation-period plan so that the team enters Type II with known controls, cadence, owners, and remediation commitments.

Business objective: preserve the Type I conclusion and make the transition to sustained operation deliberate.

Requirements:

- Review and approve management's assertion against the exact Type I baseline,
  approved system description, selected criteria, controls, known exceptions,
  and delivered package using auditor-provided wording where required.
- Retain the final management representation letter requested by the auditor,
  its source, signers, signed artifact, date, and relationship to the engagement.
- Record examination status, issued-report reference, relevant dates,
  auditor-supplied opinion summary or report artifact, and management review.
- Carry forward the approved control baseline, policy versions, risks, vendors, findings, and changes.
- Define the intended Type II observation period and required control occurrences.
- Identify changes required before the period begins.

Domain slice:

- Owns attributable Type I management sign-off, assertion approval,
  representation-letter record, `TypeIOutcome`, report reference, management
  review, and approved `TypeIIOperatingPlan`.
- Uses the Type I baseline and package, auditor-supplied conclusion, control
  responsibilities and cadence, changes, risks, and open corrective actions.
- Produces the proposed opening state for the Type II period without treating a
  product readiness status as the auditor's conclusion.

Acceptance criteria:

- [ ] The recorded result links to the exact Type I baseline and delivered package.
- [ ] Management approval identifies the exact assertion wording, system description, package, known exceptions, approvers, and time.
- [ ] An auditor-provided representation letter and issued report retain external provenance and cannot be edited into platform-authored conclusions.
- [ ] Only authorized management can approve transition into the Type II plan.
- [ ] Open findings and changed controls retain ownership and do not disappear during roll-forward.
- [ ] The product distinguishes uploaded auditor conclusions from product-generated readiness status.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 before finalizing this story's rules.
- [ ] Define assertion-version binding, representation-letter provenance and signed artifact, outcome-source provenance, report reference, management review, operating-plan contents, completeness, approval, and transition rules.
- [ ] Deliver authorized assertion review and approval, representation-letter retention, outcome recording, plan assembly, gap drill-down, review, approval, and transition preview through the API and browser.
- [ ] Resolve every plan entry to shared controls, responsibilities, cadence, evidence expectations, findings, and approved changes without copying them into an unrelated planning model.
- [ ] Prove changed assertion inputs, hidden exceptions, wrong or missing signers, external-source protection, inconsistent plans, missing ownership or cadence, denied approval, outcome-source distinction, open-remediation continuity, and immutable Type I history end to end.

## T2 - SOC 2 Type II period operated

Business outcome: recurring controls, evidence, access reviews, policy and
provider work, significant-change assessment, and management oversight are
operated throughout the observation period; the system description stays
current and source populations remain complete and explainable.

### T2-01 Start the Type II observation period from the approved baseline

Priority: P1

Area: observation period

User story: As a compliance lead, I want to start a Type II period from the approved Type I baseline so that sustained operation begins with explicit scope, controls, dates, and known changes.

Business objective: create a stable period boundary while allowing controlled, attributable evolution.

Requirements:

- Capture period dates, audit firm, categories, boundary, workforce and system
  inventory snapshots, commitments and requirements, provider scope, control
  and policy versions, owners, cadences, access expectations, evidence-handling
  policy, approved system-description starting version, and opening findings.
- Preview required occurrences and known gaps before start.
- Record planned changes and controls that begin or end during the period.
- Prevent overlapping or contradictory active periods unless explicitly supported.

Domain slice:

- Owns `TypeIIPeriod`, immutable opening snapshot, period boundaries, approved
  operating plan, and attributable period amendments.
- Uses the approved Type II plan, exact boundary, inventories, commitments,
  requirements, providers, criteria selection, controls, responsibilities,
  cadence, policies, evidence expectations and handling, approved system
  description, and opening findings.
- Establishes the period scope for occurrences, evidence, access campaigns,
  readiness monitoring, populations, closing, and examination.

Acceptance criteria:

- [ ] Starting the period creates a stable opening snapshot and operating plan.
- [ ] Every required control has an owner, cadence, and expected occurrence population.
- [ ] Unresolved opening gaps are visible and acknowledged.
- [ ] Later changes preserve effective dates and never rewrite earlier occurrences.

Implementation subtasks:

- [ ] Define period identity, boundaries, opening snapshot, expected population, overlap, planned-change, effective-date, approval, and amendment rules.
- [ ] Deliver authorized period preview, blocker drill-down, start, inspect, and controlled-change behavior through the API and browser.
- [ ] Resolve the opening plan to shared records and establish one period identity consumed by scheduling, evidence, access reviews, readiness, close, and packages.
- [ ] Prove overlaps, missing ownership or cadence, unresolved acknowledgement, denied start, later changes, historical occurrences, and standalone/split-host parity end to end.

### T2-02 Operate a recurring control calendar

Priority: P1

Area: control operations

User story: As a control owner, I want an accurate calendar and work queue of control occurrences so that I perform every required activity during the observation period.

Business objective: prevent missed or duplicate occurrences and make the expected population knowable before sampling.

Requirements:

- Generate owned occurrences from approved cadence and effective dates.
- Support recurring, event-driven, and approved ad hoc work.
- Show upcoming, due, overdue, completed, skipped, and not-applicable states.
- Preserve the expected population across schedule edits, retries, and restarts.

Domain slice:

- Owns the expected `ControlOccurrence` population and schedule-generation
  provenance for a Type II period; actual performance remains in the shared control workflow.
- Uses approved control versions, effective cadence and responsibilities, period
  boundaries, changes, and deterministic occurrence identity.
- Supplies the shared work queue, evidence workflow, readiness projection, and
  examination population without creating a separate calendar task aggregate.

Acceptance criteria:

- [ ] Owners can see what is due, why it is due, and the covered period.
- [ ] Repeated scheduling cannot create duplicate required occurrences.
- [ ] A cadence change previews added, retained, and removed future work before approval.
- [ ] Historical and active occurrences remain separate and traceable.

Implementation subtasks:

- [ ] Define deterministic occurrence identity, recurring/event/ad-hoc generation, due and covered-period semantics, effective changes, cancellation, and replay rules.
- [ ] Deliver authorized calendar and work-queue queries, schedule-change preview and approval, and occurrence navigation through the API, bounded scheduler work, and browser.
- [ ] Reuse control occurrences, responsibilities, performance, evidence, review, gaps, and readiness rather than introducing independent calendar completion state.
- [ ] Prove duplicate prevention across retries and restarts, time boundaries, schedule edits, team and owner changes, historical separation, failed generation, and host-mode parity end to end.

### T2-03 Collect and review evidence for every required occurrence

Priority: P1

Area: evidence

User story: As a compliance reviewer, I want each required control occurrence supported and reviewed within its applicable period so that the Type II population is complete and defensible.

Business objective: maintain examination-ready evidence continuously instead of reconstructing it at period end.

Requirements:

- Apply the control-performance, evidence-submission, and review workflows to every required occurrence.
- Derive completeness from expected versus performed and accepted occurrences.
- Explain reused evidence and period applicability.
- Keep rejected and corrected submissions in history.

Domain slice:

- Owns no duplicate evidence or review model; it composes existing control
  occurrences, attestations, evidence support, review decisions, and exceptions.
- Uses the Type II expected occurrence population and each artifact's provenance,
  content identity, freshness, and covered period.
- Produces an explainable control population completeness projection and review
  workload used by readiness and later sampling.

Acceptance criteria:

- [ ] Every expected occurrence resolves to completed and accepted, validly not applicable, approved exception, or unresolved gap.
- [ ] Old or outside-period evidence cannot make a current occurrence complete without an explicit valid rule.
- [ ] Review queues prioritize overdue and material work.
- [ ] Population totals reconcile to their underlying occurrence records.

Implementation subtasks:

- [ ] Define completeness and period-applicability rules, accepted terminal states, reused-evidence explanation, correction behavior, and population reconciliation.
- [ ] Deliver authorized occurrence support and review workflows plus population drill-down through the shared APIs and connected browser experience.
- [ ] Feed missing, rejected, stale, or exception-backed occurrences into shared work, findings, and readiness while retaining all submissions and decisions.
- [ ] Prove every terminal state, outside-period and reused evidence, corrections, review priority, denied access, exact totals, and immutable history end to end.

### T2-04 Monitor readiness and intervene before gaps age

Priority: P1

Area: readiness monitoring

User story: As a compliance lead, I want an explainable view of current and forecasted readiness so that I can intervene before missed controls or stale evidence threaten the examination.

Business objective: reduce late surprises during the observation period.

Requirements:

- Summarize expected and completed occurrences, evidence review, access campaigns, policies, risks, vendors, findings, and exceptions.
- Show due-soon, overdue, stale, rejected, failed, and unassigned work.
- Forecast upcoming workload from approved cadence.
- Drill every status into the affected business records.

Domain slice:

- Owns versioned readiness and workload `Projection` definitions and as-of
  results; it reuses R1-08 rule outputs and does not own another copy of
  readiness, control, evidence, access, policy, or finding state.
- Uses occurrences, review decisions, access campaigns, policies, risks,
  vendors, findings, exceptions, responsibilities, and approved cadence.
- Produces role- and scope-aware overview and work views whose counts always
  resolve to authorized source records.

Acceptance criteria:

- [ ] Counts reconcile with the underlying filtered records and show an as-of time.
- [ ] Draft, unreviewed, stale, and exception-backed work remains visibly distinct.
- [ ] Team members can filter their actionable work by date, severity, control, and status.
- [ ] Status changes are explainable and never depend on hidden manual overrides.

Implementation subtasks:

- [ ] Define each measure, status, forecast, as-of time, authorization filter, materiality rule, and source-record reconciliation.
- [ ] Deliver authorized overview, filtering, drill-down, work navigation, and stale-data or calculation-failure states through the API and browser.
- [ ] Build the work experience as a projection over domain-owned responsibilities and activities, never as a second independently editable task system.
- [ ] Prove counts and filters against underlying records, per-member and team visibility, unassigned work, status transitions, forecast changes, denied drill-down, and calculation failures end to end.

### T2-05 Run recurring application access reviews

Priority: P1

Area: access review

User story: As an access-review owner, I want repeatable campaigns with preserved populations and verified remediation so that periodic access controls operate throughout Type II.

Business objective: demonstrate consistent access governance across the observation period.

Requirements:

- Reuse application, reviewed-system, access-subject, entitlement, access-expectation, reviewer, and instruction definitions from prior campaigns.
- Reconcile the current application inventory and require a new accepted source snapshot or approved exception for every in-scope reviewed system.
- Import new point-in-time human and NHI principals, groups, roles, memberships, and direct or inherited grant paths without altering earlier campaigns.
- Compare populations and approved expectations to highlight added, removed, changed, missing, prohibited, privileged, expired, and unresolved access.
- Apply the complete decision, remediation, exception, and verification workflow.

Domain slice:

- Reuses `ReviewedSystem`, `DirectoryPrincipal`, `Entitlement`,
  `ExternalAccessGrant`, population, campaign, decision, remediation, and verification primitives from the initial review.
- Reuses `Application`, `AccessSubject`, external group membership, grant path,
  and `AccessExpectation` rather than rebuilding them per campaign.
- Owns recurring campaign cadence, new population snapshots, explicit
  cross-snapshot comparison, and period association.
- Produces occurrence evidence and readiness effects for the Type II period
  without carrying prior decisions into the new population.

Acceptance criteria:

- [ ] Each campaign retains its own source population, configuration, decisions, and evidence.
- [ ] Every currently in-scope reviewed system has an accepted current source snapshot or approved exception; missing collection cannot appear as zero access.
- [ ] Human and NHI classification, ownership, grant paths, and expectation changes remain visible and reviewable.
- [ ] Population changes are informative and never pre-decide review outcomes.
- [ ] Required remediation is tracked to verified completion or approved exception.
- [ ] Campaign cadence and completion contribute accurately to observation-period readiness.

Implementation subtasks:

- [ ] Define campaign-template reuse, application-scope reconciliation, new snapshot identity, human and NHI subject correlation, principal and grant-path correlation, expectation comparison, cadence, and non-inheritance of decisions.
- [ ] Deliver authorized repeat-import preview, campaign creation, population comparison, assignment, decision, remediation, verification, and completion through the shared API and browser workflows.
- [ ] Preserve external-versus-platform identity boundaries and connect each campaign to shared evidence, control occurrences, work, findings, exceptions, readiness, and period snapshots.
- [ ] Prove added, removed, changed, missing, prohibited, expired, and ambiguous grants; provider identifier continuity; human and NHI ownership changes; missing system snapshots; prior-history isolation; unverified remediation; and exact period contribution end to end.

### T2-06 Complete periodic policy, risk, and vendor reviews

Priority: P2

Area: governance reviews

User story: As a governance owner, I want scheduled review work for policies, risks, and material vendors so that governance artifacts remain current throughout the observation period.

Business objective: show continued oversight rather than point-in-time document existence.

Requirements:

- Generate review work from approved cadence and effective dates.
- Capture reviewer, assessment, decision, changes, evidence, and next review date.
- Reopen affected control or remediation work when material changes are found.
- Preserve the reviewed version and prior decisions.

Domain slice:

- Reuses policy, risk, vendor, responsibility, evidence, review-decision,
  exception, and corrective-action primitives.
- Owns expected governance review occurrences and each review's exact subject
  version, assessment, decision, evidence, and next-review outcome.
- Produces shared work and readiness effects without inventing separate policy,
  risk, and vendor task systems.

Acceptance criteria:

- [ ] Owners see upcoming and overdue governance reviews in the same work experience.
- [ ] A completed review identifies the exact policy, risk, or vendor version reviewed.
- [ ] Material changes identify affected controls, scope, and evidence.
- [ ] Skipped or overdue reviews remain visible and require resolution or approved exception.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D19 and define subject-specific decision and material-change rules.
- [ ] Deliver authorized scheduling, work queue, exact-version review, evidence, decision, next-date, and exception behavior through shared APIs and the browser.
- [ ] Route material decisions into control and scope impact analysis, corrective work, evidence expectations, and readiness while preserving prior versions and decisions.
- [ ] Prove due, overdue, skipped, changed, denied, corrected, and exception-backed reviews across each subject type and reconcile them to period status end to end.

### T2-07 Assess system changes and incidents for compliance impact

Priority: P1

Area: change and incident oversight

User story: As a compliance lead, I want significant changes and incidents evaluated against scope and controls so that the Type II record explains how the environment evolved.

Business objective: prevent material operational change from invalidating controls or disappearing from the audit narrative.

Requirements:

- Capture source, time, description, owner, affected systems, severity, and current status.
- Correlate earlier attributable R1-07 incident references without rewriting
  their risk-assessment history (M0-D22).
- Assess impact on scope, risks, vendors, controls, evidence expectations, and system description.
- Decide whether the active system description needs a successor version and
  preserve the significant-change narrative applicable to the Type II period.
- Create review or remediation work where needed.
- Preserve incident and change chronology and supporting evidence.

Domain slice:

- Owns compliance-facing `SignificantChange` and `IncidentReference`, chronology,
  restricted details, impact assessment, and resulting decision.
- Uses source provenance, affected systems, scope, risks, vendors, controls,
  evidence expectations, system-description versions, and period boundaries.
- Produces related review, corrective action, exception, and authorized
  audit-facing conclusions without becoming the operational incident or change-management system.

Acceptance criteria:

- [ ] Material items cannot close until control and scope impact has been reviewed.
- [ ] Affected records show the relationship and resulting decision.
- [ ] Period reporting distinguishes planned change, incident response, and unresolved exception.
- [ ] The active Type II system description identifies evaluated significant changes and cannot remain approved with an unresolved material contradiction.
- [ ] Restricted incident details remain protected while audit-relevant conclusions can be shared deliberately.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D21 before finalizing this story's rules.
- [ ] Deliver authorized capture or import, relate, assess, restrict, share conclusions, create follow-up work, and close behavior through the API and browser.
- [ ] Reuse shared source provenance, responsibilities, evidence, reviews, findings, scope impact, readiness, period history, and package selection.
- [ ] Prove planned-change versus incident semantics, restricted-field authorization, missing impact review, denied closure, later corrections, chronology, and package visibility end to end.

### T2-08 Automate selected inventory, access, and evidence collection

Priority: P2

Area: automation

User story: As a compliance lead, I want selected workforce, application,
technology, identity, source-control, and cloud facts collected on an approved
schedule so that inventories, access populations, and routine evidence stay
current without losing provenance or human oversight.

Business objective: reduce repetitive collection effort only after the manual workflow and evidence expectations are trusted.

Requirements:

- Treat automation as an optional adapter over the functional domain. Workforce,
  inventory, external-access, population, evidence, reconciliation, and review
  workflows must operate end to end through manual entry or import before this
  story is scheduled; no earlier story may depend on a connector runtime.
- Choose a source only after documenting the inventory, population, or evidence
  question it answers and whether it is authoritative, corroborating, or
  discovery-only.
- Let the team connect the selected workforce, identity, application,
  source-control, cloud, device, or other provider with least-privilege access.
- Let the team choose scope, collection definition, schedule, owner, freshness
  and completeness rules, and target inventory, population, evidence request, or control.
- Preserve provider identifiers, source boundaries, capture and observation
  times, pagination or continuation state, raw snapshot identity, normalized
  facts, tombstones or missing records, and collection result.
- Preview new, changed, missing, duplicate, ambiguous, and rejected records and
  require an attributable reconciliation decision before changing governed inventory.
- Require ordinary evidence, population, or inventory review before automated
  material counts as accepted; retain a manual import fallback.
- Package each source connector as a disposable executable with a versioned,
  language-neutral process contract. A cloud worker or customer-hosted agent
  launches one executable for one collection run, provides its bounded job
  input, consumes canonical records from its output as they are produced, and
  treats successful process exit as the end of the run. Diagnostics use a
  separate channel from record output.
- Keep deployment location out of the connector contract. The supervising
  worker owns scheduling, credentials, resource limits, durable staging,
  cancellation, and communication with the control plane, whether that worker
  runs in the product cloud or as an agent in the customer's environment.

Domain slice:

- Owns `ProviderConnection`, secret reference, approved `CollectionDefinition`,
  schedule, `CollectionRun`, source snapshot, result, and revocation history.
- Uses platform member and access-grant authorization for connection management,
  but collected Auth0 or Entra principals remain external directory data rather than platform members.
- Produces proposed workforce, application, component, information, provider,
  external-access, audit-population, or evidence records with provenance, then
  enters the same reconciliation, support, review, finding, and readiness
  workflows as manual input.

Acceptance criteria:

- [ ] The UI explains requested provider access before connection and supports test, disable, and revoke.
- [ ] Successful collections enter the same inventory, population, evidence, reconciliation, and review workflows as equivalent manual submissions.
- [ ] The complete manual workflow remains usable when the connector runtime is unavailable, disabled, or has never been deployed.
- [ ] Permission denial, rate limiting, partial results, stale data, and provider removal are visible and cannot appear complete.
- [ ] Repeated collection does not create duplicate evidence for the same source snapshot.
- [ ] Missing source records require explicit tombstone or reconciliation treatment and cannot silently delete, merge, retire, or de-scope governed records.
- [ ] Each current inventory or population view explains its source coverage, last complete observation, outstanding conflicts, and accountable owner.
- [ ] Users never see provider credential material after configuration.

Implementation subtasks:

- [ ] Run the inventory-integration discovery in the gap analysis before scheduling this P2 story; measure the first manual workflow and validate source authority, stable identifiers, completeness, matching, tombstones, freshness, and business value.
- [ ] After the functional domain and manual workflows are proven, define and
  version the executable process protocol, including bounded job input,
  canonical streaming output, diagnostics, completion and failure semantics,
  cancellation, and compatibility negotiation.
- [ ] Implement the same connector-execution contract in cloud workers and
  customer-hosted agents without giving connectors control-plane or
  deployment-specific responsibilities.
- [ ] Define least-privilege scopes, credential custody, source and raw-snapshot identity, collection boundaries, pagination, normalization, reconciliation, freshness, disable, and revocation rules.
- [ ] Deliver authorized connection consent, test, scope and schedule configuration, run status, disable, and revoke behavior through the API, bounded worker execution, and browser.
- [ ] Normalize results into existing workforce, inventory, external-access, population, or evidence primitives with provider identifiers, system-actor attribution, idempotency, partial-failure detail, preview, and ordinary human review.
- [ ] Prove denied and revoked credentials, pagination gaps, rate limiting, partial and stale results, tombstones, ambiguous matches, retries, duplicate snapshots, secret non-disclosure, cross-organization isolation, reconciliation history, and manual-workflow parity end to end.

Delivery slices: this story is delivered through the following outcome slices,
tracked as GitHub sub-issues. The parent's requirements, domain slice, and
definition of done apply to every slice, and the parent is complete only when
all slices are done.

#### T2-08a Collect and reconcile a selected cloud-reachable source

Outcome: As a compliance lead, I can run an approved connector through a cloud
worker and reconcile its collected facts through the same workflow as a manual
submission.

Acceptance criteria:

- [ ] At least one source whose equivalent manual domain workflow is already
  functional can be configured, tested, collected, previewed, reconciled, and
  reviewed end to end.
- [ ] A disposable connector executable receives one bounded collection job and
  streams versioned canonical records separately from diagnostics; successful
  exit commits the staged run, while failure, cancellation, or partial output
  cannot appear complete or advance durable source state.
- [ ] The cloud worker owns connector selection, credential delivery, resource
  limits, durable staging, cancellation, and run status without granting the
  connector control-plane responsibilities.
- [ ] Disabling or removing the connector runtime leaves the equivalent manual
  workflow fully usable.

Depends on: M0-D20, M0-D24

#### T2-08b Collect and reconcile a private source through a customer-hosted agent

Outcome: As a compliance lead, I can collect an approved source reachable only
inside the customer's environment without changing the connector or weakening
the ordinary reconciliation and review workflow.

Acceptance criteria:

- [ ] A customer-hosted agent launches the same connector artifact with the same
  versioned process contract used by the cloud worker.
- [ ] The agent owns its outbound control-plane communication, local credential
  resolution, artifact verification, resource limits, cancellation, and durable
  delivery; the connector contains no cloud-versus-customer deployment logic.
- [ ] Disconnects, retries, duplicate delivery, agent or connector upgrades,
  revoked credentials, and incomplete runs remain visible and cannot create a
  completed source snapshot.
- [ ] The customer can disable the agent and continue the equivalent workflow
  through manual entry or import.

Depends on: T2-08a

### T2-09 Conduct periodic management compliance reviews

Priority: P2

Area: management oversight

User story: As a management approver, I want periodic reviews of the observation-period program so that leadership acknowledges trends, exceptions, risk, and required intervention before the examination.

Business objective: demonstrate active oversight and timely decisions during the period.

Requirements:

- Create a review from a stable readiness snapshot.
- Summarize control population, overdue work, evidence issues, access reviews, findings, accepted risk, vendors, incidents, and changes.
- Record discussion, decisions, actions, approvers, and time.
- Track actions through completion.

Domain slice:

- Owns immutable `ManagementReviewSnapshot`, agenda or discussion record,
  `ManagementDecision`, approval, and resulting action assignments.
- Binds exact R1-08 readiness assessment and T2-04 monitoring measure versions.
  Uses the shared readiness projection and exact underlying control, evidence,
  access, finding, risk, vendor, incident, and change records as of a known time.
- Produces accountable work and period evidence without creating a parallel
  management-only readiness or task model.

Acceptance criteria:

- [ ] The review identifies the exact as-of snapshot and unresolved material items.
- [ ] Management can approve, request action, or defer with rationale.
- [ ] Assigned actions appear in accountable work queues.
- [ ] Later data changes do not silently change the historical review.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D19 and define snapshot, agenda, quorum or approver, decision, action, and deferral rules.
- [ ] Deliver authorized assemble, preview, conduct, decide, approve, assign, and inspect behavior through shared APIs and the browser.
- [ ] Bind the review to exact source records and route actions through shared responsibilities, findings or corrective work, evidence, and readiness.
- [ ] Prove incomplete and changed source data, denied approval, deferral, action tracking, later corrections, immutable review history, and period-package inclusion end to end.

## T3 - SOC 2 Type II examination supported

Business outcome: the observation period is frozen; complete source-backed
populations and samples are traceable; management assertions, auditor work, and
product projections remain distinct; the examination is supported; the result
is recorded; and the program rolls forward without losing history.

### T3-01 Close the observation period and freeze the examination snapshot

Priority: P1

Area: audit

User story: As a compliance lead, I want to close the Type II period with an immutable snapshot so that the auditor and team evaluate the same complete population.

Business objective: establish a stable examination boundary without hiding late or incomplete work.

Requirements:

- Preview the period, expected populations, missing work, unresolved exceptions, boundary changes, and package blockers.
- Require management approval to close.
- Freeze applicable workforce and inventory snapshots, commitments,
  requirements, approved system description, criteria, controls and versions,
  occurrences and evaluations, policies and communication results, evidence and
  handling decisions, reviews, access expectations and campaigns, risks,
  providers, findings, changes, incidents, and source datasets needed for populations.
- Handle authorized post-close corrections as amendments.

Domain slice:

- Owns immutable `TypeIIPeriodCloseSnapshot`, close validation and management
  decision, unresolved-item acknowledgements, and `PeriodCloseAmendment`.
- Uses the period opening snapshot and every exact in-period occurrence,
  performance, artifact, review, access campaign, change, finding, risk, and provider record.
- Supplies the single examination boundary for populations, samples, requests,
  packages, exceptions, outcome, and roll-forward.

Acceptance criteria:

- [ ] The close preview reconciles expected and actual populations.
- [ ] Unresolved material items are acknowledged rather than omitted.
- [ ] Closing records approver, time, rationale, and stable snapshot identity.
- [ ] Later source changes cannot alter the snapshot.
- [ ] Amendments are attributable and identify affected examination material.

Implementation subtasks:

- [ ] Define close contents, expected-versus-actual reconciliation, blockers, acknowledgements, approval, immutable identity, and amendment impact rules.
- [ ] Deliver authorized close preview, drill-down, approval, freeze, inspect, and amend behavior through the API, bounded snapshot processing, and browser.
- [ ] Resolve all entries to shared source identities and versions and make the close snapshot the only source for downstream examination workflows.
- [ ] Prove missing and duplicate populations, concurrent changes, unresolved acknowledgements, denied approval, partial freeze failure, amendments, later source changes, and host-mode parity end to end.

### T3-02 Demonstrate complete audit populations

Priority: P1

Area: populations

User story: As an auditor collaborator, I want to inspect complete,
reconcilable populations for each control being sampled so that selections are
made from the full relevant universe rather than a convenient subset.

Business objective: eliminate manual population reconstruction and establish confidence that samples were not selected from an incomplete set.

Requirements:

- Define each population's business purpose, source universe, covered period,
  inclusion and exclusion rules, expected completeness evidence, authorized
  fields, and relationship to controls.
- Support control-occurrence populations and source-backed populations such as
  hires, terminations, access changes, production changes, incidents,
  vulnerabilities, vendors, tickets, or other auditor-requested events without
  requiring Compliance to own those operational workflows.
- Reconcile source totals and boundaries to included, excluded, duplicate,
  missing, rejected, and unresolved rows with stable source identities and
  collection provenance.
- Relate exact population rows to applicable control performances, decisions,
  exceptions, and accepted evidence without fabricating those relationships.
- Export stable human-readable and machine-readable populations without
  granting broader sensitive-data or evidence access.
- Preserve the exact population used for each request or sample; later source
  corrections require an attributable amendment or successor population.

Domain slice:

- Owns `AuditPopulationDefinition`, immutable `AuditPopulation`, stable row and
  export identity, reconciliation result, and amendments derived from the
  frozen Type II close snapshot and exact source snapshots.
- Uses control occurrences or source-backed business-event rows, actual
  performances, workforce and system inventories, collection provenance,
  review decisions, accepted evidence support, exceptions, and effective changes.
- Supplies frozen, reconcilable population items to sample requests without
  granting access to unrelated evidence or creating another occurrence store.

Acceptance criteria:

- [ ] Population totals and source boundaries reconcile to individually inspectable rows and their source or collection snapshot.
- [ ] Included, excluded, missing, duplicate, rejected, unresolved, canceled, not-applicable, and exception-backed rows remain distinguishable as applicable.
- [ ] An exported population identifies its source snapshot and calculation time.
- [ ] Partial collection, pagination gaps, changed queries, or missing source evidence cannot appear as a complete population.
- [ ] Later corrections do not silently alter a population already used for sampling.
- [ ] Restricted columns and linked artifacts remain protected in inspection and export without changing population identity or totals.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 before finalizing this story's rules.
- [ ] Define source universe, definition version, inclusion, exclusion, reconciliation, effective-date, row identity, missing and duplicate classification, source snapshot, completeness, calculation time, amendment, freeze, and export rules.
- [ ] Deliver authorized population define or import, generate, inspect, filter, reconcile, export, amend, and freeze-for-sample behavior through the API, bounded processing, and browser.
- [ ] Resolve population rows to exact source facts and shared occurrence, performance, workforce, inventory, access, decision, evidence, and exception records where applicable while enforcing field and artifact authorization.
- [ ] Prove source and pagination gaps, changed definitions, every row classification, exact totals, restricted fields and evidence, export failure, source amendments, sample binding, and historical stability end to end.

### T3-03 Fulfill Type II sample and evidence requests

Priority: P1

Area: auditor collaboration

User story: As a compliance contributor, I want Type II samples and follow-up requests tied to the frozen populations so that responses are fast, complete, and traceable.

Business objective: support efficient examination work while preserving internal review and exact delivery history.

Requirements:

- Let the auditor or compliance lead identify requested population items and supporting material.
- Assign owners, due dates, internal reviewers, and external delivery status.
- Link existing evidence without copying and allow supplemental evidence with provenance.
- Preserve discussion and every delivered response version.

Domain slice:

- Reuses `AuditorRequest`, assignment, response, evidence support, review
  decision, and delivery primitives from Type I with Type II sample references.
- Owns stable `SampleSelection` relationships to exact frozen population items
  and preserves auditor-supplied selection provenance.
- Produces exact delivered response history and examination metrics without
  copying population items or existing evidence artifacts.

Acceptance criteria:

- [ ] Each sample resolves to the exact frozen population item.
- [ ] Contributors can see what remains missing before submission.
- [ ] Only internally approved responses become externally visible when review is required.
- [ ] Reopened requests preserve prior deliveries and reasons.
- [ ] Request status and response-time reporting reconcile with the underlying history.

Implementation subtasks:

- [ ] Define sample-selection source, frozen item reference, request lifecycle, ownership, supplemental evidence, review policy, delivery, reopen, and timing rules.
- [ ] Deliver authorized sample capture or import, request assignment, response assembly, review, delivery, follow-up, reopen, and reporting through shared APIs and the browser.
- [ ] Reuse platform responsibilities, scoped external visibility, evidence identity, support relationships, review decisions, work queues, and delivery records.
- [ ] Prove invalid or amended population references, missing support, denied visibility, rejected and reopened responses, concurrent edits, exact delivery history, and metric reconciliation end to end.

### T3-04 Resolve examination exceptions and remediation commitments

Priority: P1

Area: audit findings

User story: As a compliance lead, I want examination exceptions connected to their occurrences, root causes, responses, and remediation so that the audit record and next-period plan remain honest.

Business objective: prevent audit exceptions from becoming disconnected notes or disappearing after report delivery.

Requirements:

- Preserve auditor-provided exception wording separately from internal analysis.
- Link affected samples, populations, controls, evidence, changes, incidents, and risks.
- Capture management response, root cause, corrective action, owner, due date, and status.
- Carry open commitments into the next program period.

Domain slice:

- Reuses the shared finding, external-author, corrective-action, evidence,
- Reuses the shared finding, external-author, corrective-action, evidence,
  review, R1-07 risk-acceptance, and verification model. Its distinct
  `AuditTestException` preserves auditor wording and links to those records.
- Binds each exception to exact frozen samples, population items, control
  occurrences, evidence, changes, incidents, risks, and close snapshot.
- Produces management responses, package content, and next-period commitments
  without changing auditor wording or the examined records.

Acceptance criteria:

- [ ] Internal users cannot silently alter auditor-authored text.
- [ ] Resolution requires evidence and authorized review.
- [ ] Accepted risk and planned remediation remain distinct.
- [ ] Open and closed items appear accurately in handoff and roll-forward views.

Implementation subtasks:

- [ ] Define examination source, immutable wording, affected-record references, management response, root cause, corrective action, risk acceptance, closure, and roll-forward rules.
- [ ] Deliver authorized capture or import, inspect, relate, respond, assign, remediate, accept risk, review, close, and reopen behavior through shared APIs and the browser.
- [ ] Preserve auditor authorship and reuse work queues, evidence, review, readiness, package, and roll-forward relationships rather than creating an audit-only exception store.
- [ ] Prove source-text protection, frozen-record references, denied closure, missing evidence, risk-versus-remediation distinctions, reopen history, package accuracy, and next-period continuity end to end.

### T3-05 Produce and validate the Type II package

Priority: P1

Area: audit package

User story: As a compliance lead, I want a reproducible indexed Type II package for the closed period so that the examination record can be delivered and retained without an improvised archive.

Business objective: provide a complete, verifiable period package that connects scope, populations, samples, evidence, decisions, and exceptions.

Requirements:

- Generate from the frozen period snapshot and explicit sharing selection.
- Produce a human-readable index and open machine-readable tables for the
  approved system description; scope and boundary; workforce, application,
  technology, information, and provider inventories; commitments and
  requirements; criteria-to-control matrix; control evaluations and complete
  audit populations; occurrences; samples; evidence and review; access
  expectations and campaigns; policy communication and training; risks;
  findings; significant changes and incident impact; requests; responses;
  exceptions; remediation; and management responses.
- Include management-assertion review inputs bound to the exact system
  description, period, criteria, controls, populations, exceptions, and package
  while keeping auditor-provided forms and final approval distinct.
- Validate completeness, period applicability, review state, authorization, and content identity.
- Support an auditor-agreed workbook, portal, or archive delivery without making
  a proprietary format the only retained record.
- Retain the delivered package, manifest, validation and override results,
  recipient, delivery history, and amendments.

Domain slice:

- Reuses immutable `AuditPackage`, manifest, validation, generation-attempt,
  override, retention, sharing, and delivery primitives from Type I.
- Uses only the frozen Type II close snapshot, its derived populations and
  samples, and exact selected examination records and artifacts.
- Produces the retained examination handoff without becoming a mutable or
  independent representation of the period.

Acceptance criteria:

- [ ] Every index entry resolves and every included artifact matches its manifest identity.
- [ ] Validation errors block clean delivery; authorized overrides require rationale.
- [ ] Partial or interrupted generation cannot appear successful.
- [ ] Regenerating the same snapshot produces an equivalent manifest.
- [ ] Access to delivered packages is least-privilege and recorded.
- [ ] Population, sample, control-matrix, evidence-index, and request tables reconcile to the same records as the human-readable index and manifest.
- [ ] Management-authored, auditor-authored, and product-calculated content are labeled and cannot be mistaken for one another.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 before finalizing this story's rules.
- [ ] Extend the shared package definition and output schemas for period, source inventories, population, sample, request, response, exception, management-response, assertion input, and amendment content while preserving deterministic identity, authorship, and authorization rules.
- [ ] Deliver authorized preview, validate, generate, monitor, override, download, deliver, and retrieve behavior through shared APIs, bounded worker processing, and browser.
- [ ] Resolve every manifest entry to exact shared records and artifacts, enforce their sharing policy, and record member or system actors for attempts and delivery.
- [ ] Prove completeness, period applicability, table reconciliation, attribution distinctions, restricted content, partial and interrupted generation, retry, equivalent regeneration, least privilege, amendments, and exact retained retrieval end to end.

### T3-06 Record management sign-off and the Type II outcome

Priority: P1

Area: management oversight

User story: As a management approver, I want to review the final period record, known exceptions, and auditor result so that the organization has an attributable conclusion and set of commitments.

Business objective: close the engagement deliberately and distinguish management assertions, auditor conclusions, and product-calculated readiness.

Requirements:

- Present the frozen snapshot, approved system description, delivered package,
  unresolved items, exceptions, accepted risks, and management responses.
- Review and approve management's assertion against those exact inputs using
  auditor-provided wording where required.
- Retain the final management representation letter requested by the auditor,
  its source, required signers, signed artifact, date, and engagement relationship.
- Record required approvers, decision, comments, time, issued-report reference,
  and auditor-supplied opinion or result artifact.
- Store the outcome supplied by the organization without generating an audit opinion.
- Preserve final commitments and due dates.

Domain slice:

- Owns immutable `TypeIISignoff`, management assertion and decision,
  externally sourced `TypeIIOutcome`, report reference, and final commitments.
- Uses the exact close snapshot, delivered package, unresolved items,
  examination exceptions, accepted risks, and management responses.
- Produces the approved conclusion and roll-forward input while keeping
  management assertions, auditor conclusions, and product projections distinct.

Acceptance criteria:

- [ ] Approvers review the exact immutable snapshot and package.
- [ ] Management approval identifies the exact assertion wording, system description, population and exception state, package, approvers, and time.
- [ ] The auditor-provided representation letter, tests and results, exceptions, and issued report retain external provenance and cannot be edited into platform-authored conclusions.
- [ ] Unresolved material items cannot be hidden from sign-off.
- [ ] The recorded outcome clearly identifies its source and is not inferred by the product.
- [ ] Later changes make the sign-off historical rather than modifying it.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D17 before finalizing this story's rules.
- [ ] Define required approvers, assertion version and exact input binding, representation-letter provenance and signed artifact, management decision, outcome provenance, report reference, commitment, and historical-supersession rules.
- [ ] Deliver authorized sign-off preview, assertion review and approval, representation-letter retention, drill-down, decision, comments, outcome recording, report linking, and commitment assignment through the API and browser.
- [ ] Reuse platform actors, responsibilities, findings, risk acceptance, work queues, packages, and snapshot history while preserving the three distinct conclusion sources.
- [ ] Prove changed assertion inputs, hidden or unresolved item prevention, wrong or missing signers, denied approval, external-source protection, source attribution, concurrent amendment, immutable sign-off, final commitments, and downstream rollover end to end.

### T3-07 Roll the program into continuous compliance and the next period

Priority: P1

Area: program transition

User story: As a compliance lead, I want to roll controls, schedules, risks, vendors, and open commitments into the next period so that compliance remains an operating program rather than an annual reconstruction.

Business objective: retain institutional knowledge, reduce next-audit setup effort, and expose whether program health is improving.

Requirements:

- Carry forward current approved records while preserving prior engagement snapshots.
- Preview changed scope, controls, cadence, ownership, policies, risks, vendors, integrations, and findings.
- Create the next period only after explicit review and approval.
- Compare selected readiness and operating measures across periods.

Domain slice:

- Owns `RollForwardPlan`, explicit carry-forward selections and changes,
  approval decision, next-period proposal, and comparison definition.
- Uses current approved program records, the completed engagement snapshot and
  outcome, open commitments, effective responsibilities and cadence, and integration state.
- Produces the next period through existing program and period workflows; it
  does not copy historical occurrences, decisions, evidence relationships, or completed records.

Acceptance criteria:

- [ ] The preview distinguishes carried-forward, changed, retired, and unresolved items.
- [ ] Open remediation and commitments retain owner, due date, and source engagement.
- [ ] New-period schedules do not duplicate prior occurrences.
- [ ] Historical trends reconcile to each underlying period.
- [ ] The team can begin the next cycle without altering the completed audit record.

Implementation subtasks:

- [ ] Define eligible carry-forward records, change classification, open-commitment continuity, schedule boundary, comparison measures, approval, and safe replay rules.
- [ ] Deliver authorized roll-forward preview, inspect, select, revise, approve, create-next-period, and compare behavior through the API, bounded processing, and browser.
- [ ] Reuse stable program identities and current approved versions, preserve source engagement references, and route the proposal through ordinary period-start validation and work generation.
- [ ] Prove carried, changed, retired, unresolved, and ineligible records; duplicate occurrence prevention; denied approval; partial failure and retry; exact trends; and completed-record immutability end to end.

## F1 - Multi-client firm operations

Business outcome: the firm onboards, operates, and offboards client organizations from one deployment; firm staff see and work their client portfolio without cross-client disclosure; reusable templates are applied with provenance; client users can sign in through their own identity providers; every client service is recorded as an accepted advisory or attest engagement; and independence walls prevent attest work where advisory services impaired independence. Attest workpaper support stays out of this milestone until M0-D27 decides its scope.

These stories make the platform serve many client organizations for a firm that provides both advisory and attest services. The tenant-ready foundations they rely on (R1-15, EN-01, M0-D25, and M0-A07) are delivered in M0 and R1.

### F1-01 Onboard, suspend, and offboard client organizations

Priority: P1

Area: tenancy

User story: As a firm operator, I want to onboard a client from our standard setup and offboard a departing client with a complete export and governed retention so that client relationships start consistently and end without data leakage or lost obligations.

Business objective: make the client lifecycle repeatable and defensible, including the end of the relationship.

Requirements:

- Onboard a client with a selected template set, initial members, firm-staff engagement assignments, and starting program stage.
- Suspend and reactivate a client with a recorded reason.
- Offboard a client through an explicit decision covering final export, access revocation, retention and hold evaluation, and scheduled disposition.
- Produce a documented export of the client's management-owned records, evidence, history, and delivered packages.
- Retain firm-owned material that must survive offboarding (for example attest documentation) separately from client-owned records, following M0-D25 and M0-D27.
- Record every lifecycle decision with actor, rationale, and effective time.

Domain slice:

- Owns organization lifecycle decisions, `ClientExport`, and the offboarding disposition plan.
- Uses R1-15 provisioning, F1-04 templates, memberships, R2-12 evidence governance, and F1-07 service engagements.
- Produces an isolated, exportable, and eventually disposed tenant without deleting anything under hold or retention obligation.

Acceptance criteria:

- [ ] Onboarding records which template versions were applied and creates only draft client records.
- [ ] Offboarding revokes all client and firm-staff access to the organization at the recorded effective time.
- [ ] The client export reconciles to the organization's records and artifacts and lists anything withheld with the reason.
- [ ] Records under an engagement hold, legal hold, or retention obligation are not disposed of, and the reason is visible.
- [ ] Disposition removes the organization's content from every store, index, projection, and backup according to the approved plan and records the result.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D25, M0-D16, and M0-D27 and define lifecycle, export contents, firm-retained material, and disposition rules.
- [ ] Deliver authorized onboard, suspend, reactivate, export, offboard, and dispose behavior through the API, bounded worker processing, and browser.
- [ ] Prove export completeness, interrupted export, holds and retention carve-outs, revoked access, disposition across stores, and denied operator actions end to end.

### F1-02 See the client portfolio across organizations

Priority: P1

Area: tenancy

User story: As a firm engagement lead, I want one view of every client I am authorized for, with stage, readiness, upcoming deadlines, overdue work, and open findings, so that I can intervene before any client falls behind.

Business objective: manage the firm's client base from one place without opening each client or maintaining a firm spreadsheet.

Requirements:

- Summarize each authorized client organization: service engagements, program stage, target dates, readiness, overdue and blocked work, open findings, and access-review status.
- Include only organizations where the viewer holds an active membership or engagement assignment.
- Open a client from the portfolio by explicitly switching the active organization.
- Show each summary's as-of time and calculation source, and never aggregate restricted client details across organizations.

Domain slice:

- Owns the client portfolio projection and its authorization rules.
- Uses per-organization readiness (R1-08), accountable work (R2-11), service engagements (F1-07), and memberships.
- Produces oversight and navigation only; it never stores client state.

Acceptance criteria:

- [ ] A staff member sees exactly the clients they are authorized for, and each summary reconciles to that client's own views at the stated as-of time.
- [ ] Revoking an assignment removes the client from the portfolio immediately.
- [ ] Portfolio search, export, and notifications never disclose unauthorized clients or restricted client details.
- [ ] Opening a client from the portfolio switches the active organization visibly.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D25 and M0-A05 and define portfolio measures, authorization, and as-of semantics.
- [ ] Deliver the portfolio overview, filters, drill-down, and organization switch through the API and browser.
- [ ] Prove per-staff visibility, revocation, reconciliation to client views, stale calculations, and cross-tenant leak tests end to end.

### F1-03 Plan firm staff assignments and capacity across clients

Priority: P2

Area: tenancy

User story: As a firm leader, I want to see and plan staff assignments and workload across client engagements so that deadlines are met without overloading consultants.

Business objective: learn whether capacity planning belongs in the platform before building it.

Requirements:

- Show each staff member's client engagement assignments, upcoming due work, and engagement deadlines.
- Propose and approve assignment changes that respect independence walls.
- Do not become a resource-management, scheduling, or timekeeping system.

Domain slice:

- Owns staff assignment proposals and the capacity projection.
- Uses service engagements (F1-07), accountable work (R2-11, F1-05), and independence rules (F1-08).

Acceptance criteria:

- [ ] An assignment proposal that would breach an independence wall is blocked with an explanation.
- [ ] Workload totals reconcile to the underlying client work items.
- [ ] A workload view never reveals client details the viewer is not authorized to see.

Implementation subtasks:

- [ ] Before scheduling this P2 story, confirm with firm leadership that capacity planning belongs in the platform rather than an existing resource tool.
- [ ] Deliver authorized assignment overview, proposal, approval, and workload drill-down through the API and browser.
- [ ] Prove independence blocks, reconciliation, and restricted visibility end to end.

### F1-04 Maintain a reusable template library and apply it to clients

Priority: P1

Area: firm methodology

User story: As a firm practice lead, I want to maintain versioned control, policy, evidence-request, and risk templates and apply them to client organizations so that every client starts from our methodology while each program remains the client's own.

Business objective: scale the firm's methodology across clients without copying one client's data into another.

Requirements:

- Maintain platform-level templates with versions, authorship, approval, and change notes.
- Apply a template version to one client organization, creating client-owned draft records with template provenance; applying a template never activates records.
- When a template changes, show which clients used the prior version and let each client adopt, adapt, or decline the successor through its own review.
- Prevent client-authored content or identifiers from entering templates except through an explicit, reviewed, de-identified contribution.
- Respect criteria-content licensing when templates include criteria mappings (M0-D02).

Domain slice:

- Owns platform-level `Template`, `TemplateVersion`, template approval, and `TemplateApplication` provenance.
- Uses EN-02 versioning and the owning client workflows for controls (R1-05), mappings (R1-06), risks (R1-07), and policies (R2-02).
- Produces tenant-owned drafts through each owning workflow; templates never own client state.

Acceptance criteria:

- [ ] Applying a template creates drafts in exactly one organization, each traceable to the template version.
- [ ] A template update never changes an existing client record; it produces a reviewable proposal for each affected client.
- [ ] No client identifier, evidence, or client-authored text enters a template without an explicit reviewed contribution.
- [ ] Client users cannot see which other clients use a template.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D25 and M0-D02 and define template scope, versioning, application, successor proposals, and contribution rules.
- [ ] Deliver authorized template authoring, approval, application, change impact, and client adoption through the API and browser.
- [ ] Prove provenance, non-propagation to existing records, contribution review, licensing restrictions, and cross-tenant isolation end to end.

### F1-05 Work one queue across all my client organizations

Priority: P1

Area: work management

User story: As a firm consultant, I want my assigned work from every client in one queue so that I can prioritize across clients without switching organizations to discover what is due.

Business objective: let a small firm run many client programs without missed work hiding inside individual tenants.

Requirements:

- Aggregate my accountable work items from every organization where I hold an active membership or engagement assignment, labeled by client.
- Acting on an item opens its source workflow in that client organization through an explicit organization switch.
- Group reminders and digests by client and respect each client's restrictions.

Domain slice:

- Extends the `AccountableWorkItem` projection (R2-11) with cross-organization aggregation for the viewing user.
- Uses memberships, engagement assignments (F1-07), and portfolio authorization (F1-02).

Acceptance criteria:

- [ ] Every item shows its client and resolves to one source record in that client organization.
- [ ] Counts reconcile to each client's own queue for the same user.
- [ ] Losing access to one client removes its items immediately without affecting other clients.
- [ ] Restricted work in one client is never disclosed through the cross-client queue, counts, or digests.

Implementation subtasks:

- [ ] Define cross-organization aggregation, authorization, ordering, and digest grouping rules.
- [ ] Deliver the cross-client queue, filters, and source navigation through the API and browser.
- [ ] Prove revocation, reconciliation, restricted work, and cross-tenant leak tests end to end.

### F1-06 Let client users sign in through their own identity providers

Priority: P1

Area: identity

User story: As a client administrator, I want our people to sign in through our own identity provider so that access follows our joiner-mover-leaver process and nobody manages separate credentials.

Business objective: let each client govern its own people's authentication while the firm keeps one platform.

Requirements:

- Configure one or more identity-provider connections per client organization following M0-A07.
- Restrict which connections may authenticate into which organization; firm staff keep using the firm's identity provider.
- Keep issuer plus subject as the identity key and never grant membership from a claim without an explicit mapping.
- Test, disable, and rotate a connection without losing member history.

Domain slice:

- Owns per-organization identity-provider connection configuration and lifecycle.
- Uses platform users, external identities, memberships (R1-04), and tenant context (EN-01).

Acceptance criteria:

- [ ] A user authenticated through one client's connection cannot reach any other organization unless separately a member.
- [ ] Disabling a connection blocks new sign-ins through it immediately while preserving attribution.
- [ ] A misconfigured or unreachable provider fails safely with an actionable error.
- [ ] Connection secrets are never displayed after configuration.

Implementation subtasks:

- [ ] Incorporate the decision recorded in M0-A07 and define connection, discovery, mapping, and rotation rules.
- [ ] Deliver authorized connection setup, test, disable, rotation, and sign-in routing through the API and browser.
- [ ] Prove wrong-tenant sign-in denial, disabled connections, identity replacement, secret non-disclosure, and host-mode parity end to end.

### F1-07 Record client engagements and accept them after independence checks

Priority: P1

Area: firm engagements

User story: As a firm engagement partner, I want every service we provide to a client recorded as an advisory or attest engagement with scope, period, and team, and accepted only after an independence and conflict check, so that we never start work we are not permitted to perform.

Business objective: make the firm's client services explicit, because independence walls, the portfolio, and staff access all depend on them.

Requirements:

- Record engagement type (for example readiness advisory, continuous compliance, SOC 2 Type I examination, SOC 2 Type II examination), client organization, scope, period, engagement lead, and assigned staff.
- Keep the firm's service engagement distinct from the client's Type I and Type II audit engagements, whose auditor may be an external firm.
- Require an attributable acceptance decision recording the independence evaluation, conflicts, management-responsibility acknowledgement for nonattest services, and approver, following M0-D26.
- Grant firm-staff access to client records through engagement assignment rather than standing firm membership.
- Preserve engagement amendments and closure history.

Domain slice:

- Owns `ServiceEngagement`, engagement type, team assignment, acceptance decision, amendment, and closure.
- Uses client organizations, memberships and access grants (R1-04), EN-04 decisions, and independence rules (F1-08).
- Supplies engagement context to independence enforcement, the portfolio, assignments, the client's Type I and Type II engagements, and offboarding.

Acceptance criteria:

- [ ] No firm staff member can access a client's records without an active engagement assignment or an approved exception.
- [ ] An engagement cannot start without a recorded acceptance decision and independence evaluation.
- [ ] The approver sees every advisory and attest engagement for the client with its period and services before deciding.
- [ ] Amending or closing an engagement preserves history and changes staff access at the effective time.

Implementation subtasks:

- [ ] Incorporate the decisions recorded in M0-D25, M0-D26, and M0-D27 and define engagement types, acceptance, assignment-based access, amendment, and closure rules.
- [ ] Deliver authorized create, evaluate, accept, assign, amend, and close behavior through the API and browser.
- [ ] Prove assignment-based access, missing acceptance, overlapping engagements, amendment, closure, and denied approval end to end.

### F1-08 Enforce independence walls between advisory and attest work

Priority: P1

Area: independence

User story: As the firm's independence partner, I want the platform to prevent attest work where our advisory services impaired independence and to separate advisory and attest teams so that the firm's examination reports remain defensible.

Business objective: turn the firm's independence policy into enforced, auditable platform behavior rather than a manual checklist.

Requirements:

- Record the nonattest services performed for each client with period, staff, and whether they involved management functions, following M0-D26.
- Block acceptance of an attest engagement, or a staff assignment to one, when recorded services or assignments breach the approved rules or cooling-off periods; allow only exceptions the rules permit, with documented approval.
- Separate advisory-only and attest-only material within a client organization so each team sees only what the rules allow.
- Prevent attest staff from authoring or approving the client's management records such as controls, policies, evidence, and assertions.
- Retain an independence evaluation for each attest engagement that can be produced for firm quality review or peer review.
- Re-evaluate affected attest engagements when services, assignments, or periods change.

Domain slice:

- Owns versioned `IndependenceRuleSet`, `NonattestServiceRecord`, `IndependenceEvaluation`, staff-assignment restrictions, and advisory and attest access compartments.
- Uses service engagements (F1-07), memberships and grants (R1-04), EN-01 authorization, and EN-04 decisions.
- Produces enforced authorization constraints and retained independence evidence; it does not replace the firm's system of quality management.

Acceptance criteria:

- [ ] An attest engagement or staff assignment that breaches the approved rules cannot be accepted, and the explanation cites the rule and the conflicting service or assignment.
- [ ] Attest staff cannot create, edit, or approve client management records, enforced on the server.
- [ ] Advisory-only and attest-only material is inaccessible to the other team, including through search, counts, exports, and notifications.
- [ ] Every evaluation, exception, and rule-set change is attributable and retained.
- [ ] A later change to services or assignments triggers re-evaluation of affected attest engagements.

Implementation subtasks:

- [ ] Incorporate the decision recorded in M0-D26 and define rule-set versions, service classification, cooling-off periods, compartments, exceptions, and re-evaluation.
- [ ] Deliver authorized service recording, evaluation, exception approval, compartment administration, and evaluation history through the API and browser.
- [ ] Prove blocked acceptance and assignment, compartment isolation, attest-staff write denial, rule-set changes, re-evaluation, and history end to end.

## GitHub issue index

The GitHub issue is the tracking record for each created item; its dependencies
(`blocked by`) are maintained with GitHub issue relationships and summarized
here. M0-D24 ([#136](https://github.com/bdgrz/compliance/issues/136)) remains
a UI blocker on existing product parents and first product delivery slices.
Every scheduled frontend child records it directly; backend children omit it.
That global blocker is not repeated in every row below. The parent issue index
continues to describe product dependencies, while delivery-child tracking is
listed separately below it.

### Active feature delivery children

| Product story | Backend children | Frontend child | Milestone |
| --- | --- | --- | --- |
| R1-15 [#127](https://github.com/bdgrz/compliance/issues/127) | [#152](https://github.com/bdgrz/compliance/issues/152) | [#176](https://github.com/bdgrz/compliance/issues/176) | R1 |
| R1-01 [#7](https://github.com/bdgrz/compliance/issues/7) | [#158](https://github.com/bdgrz/compliance/issues/158) | [#177](https://github.com/bdgrz/compliance/issues/177) | R1 |
| R1-02 [#8](https://github.com/bdgrz/compliance/issues/8) | Baseline [#162](https://github.com/bdgrz/compliance/issues/162), downstream impact [#246](https://github.com/bdgrz/compliance/issues/246) | [#178](https://github.com/bdgrz/compliance/issues/178) | R1 |
| EN-01 [#87](https://github.com/bdgrz/compliance/issues/87) | Fail-closed composition [#324](https://github.com/bdgrz/compliance/issues/324), full enabler [#157](https://github.com/bdgrz/compliance/issues/157) | — | R1 |

Shared enablers EN-01, EN-02, and EN-04 retain backend children #324, #157,
#160, and #161. Their first consuming feature supplies the browser workflow; these
enablers have no separate frontend child. The active backend dependency chain
uses these children and #152, #158, and the #162 baseline, rather than their UI-dependent
product parents. Frontend children #176 through #178 depend on their
corresponding backend children and M0-D24, and record other applicable product
and UI dependencies directly in GitHub.

### Product issue index

| Key | Issue | Milestone | Priority | Depends on |
| --- | --- | --- | --- | --- |
| M0-D01 | [#58](https://github.com/bdgrz/compliance/issues/58) | M0 | P0 | — |
| M0-D02 | [#59](https://github.com/bdgrz/compliance/issues/59) | M0 | P0 | — |
| M0-D03 | [#60](https://github.com/bdgrz/compliance/issues/60) | M0 | P0 | — |
| M0-D04 | [#61](https://github.com/bdgrz/compliance/issues/61) | M0 | P0 | — |
| M0-D05 | [#62](https://github.com/bdgrz/compliance/issues/62) | M0 | P0 | — |
| M0-D06 | [#63](https://github.com/bdgrz/compliance/issues/63) | M0 | P0 | — |
| M0-D07 | [#64](https://github.com/bdgrz/compliance/issues/64) | M0 | P0 | — |
| M0-D08 | [#65](https://github.com/bdgrz/compliance/issues/65) | M0 | P0 | — |
| M0-D09 | [#66](https://github.com/bdgrz/compliance/issues/66) | M0 | P0 | — |
| M0-D10 | [#67](https://github.com/bdgrz/compliance/issues/67) | M0 | P0 | — |
| M0-D11 | [#68](https://github.com/bdgrz/compliance/issues/68) | M0 | P0 | — |
| M0-D12 | [#69](https://github.com/bdgrz/compliance/issues/69) | M0 | P0 | — |
| M0-D13 | [#70](https://github.com/bdgrz/compliance/issues/70) | M0 | P0 | — |
| M0-D14 | [#71](https://github.com/bdgrz/compliance/issues/71) | M0 | P0 | — |
| M0-D15 | [#72](https://github.com/bdgrz/compliance/issues/72) | M0 | P0 | — |
| M0-D16 | [#73](https://github.com/bdgrz/compliance/issues/73) | M0 | P1 | — |
| M0-D17 | [#74](https://github.com/bdgrz/compliance/issues/74) | M0 | P1 | — |
| M0-D18 | [#75](https://github.com/bdgrz/compliance/issues/75) | M0 | P1 | — |
| M0-D19 | [#76](https://github.com/bdgrz/compliance/issues/76) | M0 | P2 | — |
| M0-D20 | [#77](https://github.com/bdgrz/compliance/issues/77) | M0 | P2 | — |
| M0-D21 | [#78](https://github.com/bdgrz/compliance/issues/78) | M0 | P1 | — |
| M0-D22 | [#79](https://github.com/bdgrz/compliance/issues/79) | M0 | P0 | — |
| M0-D23 | [#80](https://github.com/bdgrz/compliance/issues/80) | M0 | P0 | — |
| M0-D24 | [#136](https://github.com/bdgrz/compliance/issues/136) | M0 | P0 | — |
| M0-D25 | [#123](https://github.com/bdgrz/compliance/issues/123) | M0 | P0 | — |
| M0-D26 | [#124](https://github.com/bdgrz/compliance/issues/124) | M0 | P1 | M0-D25 |
| M0-D27 | [#125](https://github.com/bdgrz/compliance/issues/125) | M0 | P1 | — |
| M0-D28 | [#139](https://github.com/bdgrz/compliance/issues/139) | M0 | P0 | — |
| M0-A01 | [#81](https://github.com/bdgrz/compliance/issues/81) | M0 | P0 | — |
| M0-A02 | [#82](https://github.com/bdgrz/compliance/issues/82) | M0 | P0 | M0-A01 |
| M0-A03 | [#83](https://github.com/bdgrz/compliance/issues/83) | M0 | P0 | — |
| M0-A04 | [#84](https://github.com/bdgrz/compliance/issues/84) | M0 | P0 | — |
| M0-A05 | [#85](https://github.com/bdgrz/compliance/issues/85) | M0 | P0 | M0-A01 |
| M0-A06 | [#86](https://github.com/bdgrz/compliance/issues/86) | M0 | P0 | M0-A01 |
| M0-A07 | [#126](https://github.com/bdgrz/compliance/issues/126) | M0 | P0 | — |
| EN-01 | [#87](https://github.com/bdgrz/compliance/issues/87) | R1 | P0 | M0-A04, M0-A07, M0-D03, M0-D25, M0-D28 |
| EN-01a backend | [#324](https://github.com/bdgrz/compliance/issues/324) | R1 | P0 | M0-A04, M0-A07, M0-D03, M0-D25, M0-D28; blocks EN-01 backend #157 |
| EN-02 | [#88](https://github.com/bdgrz/compliance/issues/88) | R1 | P0 | M0-A01 |
| EN-03 | [#89](https://github.com/bdgrz/compliance/issues/89) | R1 | P0 | M0-A01, M0-A02 |
| EN-04 | [#90](https://github.com/bdgrz/compliance/issues/90) | R1 | P0 | M0-D03, M0-D23, EN-01 |
| EN-05 | [#91](https://github.com/bdgrz/compliance/issues/91) | R1 | P0 | M0-A06, M0-D28, EN-01 |
| EN-06 | [#92](https://github.com/bdgrz/compliance/issues/92) | R1 | P0 | M0-A03, EN-01 |
| R1-01 | [#7](https://github.com/bdgrz/compliance/issues/7) | R1 | P0 | M0-D01, EN-01, R1-15 |
| R1-02 | [#8](https://github.com/bdgrz/compliance/issues/8) | R1 | P0 | M0-D01, M0-D22, EN-02, EN-04, R1-01 |
| R1-03 | [#9](https://github.com/bdgrz/compliance/issues/9) | R1 | P0 | M0-D01, M0-D02, EN-05, R1-01 |
| R1-04 | [#10](https://github.com/bdgrz/compliance/issues/10) | R1 | P0 | M0-D03, M0-D28, EN-01, R1-15 |
| R1-04a | [#93](https://github.com/bdgrz/compliance/issues/93) | R1 | P0 | M0-D03, M0-D28, EN-01, R1-15 |
| R1-04b | [#94](https://github.com/bdgrz/compliance/issues/94) | R1 | P0 | R1-04a |
| R1-04c | [#95](https://github.com/bdgrz/compliance/issues/95) | R1 | P0 | R1-04a |
| R1-04d | [#96](https://github.com/bdgrz/compliance/issues/96) | R1 | P0 | R1-04a |
| R1-04e | [#97](https://github.com/bdgrz/compliance/issues/97) | R1 | P0 | EN-04, R1-04a |
| R1-05 | [#11](https://github.com/bdgrz/compliance/issues/11) | R1 | P0 | M0-D22, EN-02, EN-04, R1-02, R1-04 |
| R1-06 | [#12](https://github.com/bdgrz/compliance/issues/12) | R1 | P0 | M0-D02, EN-04, R1-03, R1-05 |
| R1-07 | [#13](https://github.com/bdgrz/compliance/issues/13) | R1 | P0 | M0-D10, M0-D22, M0-D23, EN-02, R1-02 |
| R1-08 | [#14](https://github.com/bdgrz/compliance/issues/14) | R1 | P0 | M0-A05, M0-D23, EN-03, R1-06, R1-07, R1-10, R1-11, R1-12, R1-13, R1-14 |
| R1-09 | [#47](https://github.com/bdgrz/compliance/issues/47) | R1 | P0 | M0-D04, EN-05 |
| R1-09a | [#98](https://github.com/bdgrz/compliance/issues/98) | R1 | P0 | M0-D04, EN-05, R1-04, R1-05, R1-06 |
| R1-09b | [#99](https://github.com/bdgrz/compliance/issues/99) | R1 | P0 | R1-09a, R2-02, R2-03 |
| R1-09c | [#100](https://github.com/bdgrz/compliance/issues/100) | R1 | P0 | R1-02, R1-07, R1-09a, R1-10, R1-11, R1-12, R1-13, R1-14, R2-07 |
| R1-09d | [#101](https://github.com/bdgrz/compliance/issues/101) | R1 | P0 | R1-08, R1-09a |
| R1-10 | [#48](https://github.com/bdgrz/compliance/issues/48) | R1 | P0 | M0-D05, M0-D22, M0-D28, EN-02, EN-05, R1-02 |
| R1-10a | [#102](https://github.com/bdgrz/compliance/issues/102) | R1 | P0 | M0-D05, M0-D22, M0-D28, EN-02, EN-05, R1-02 |
| R1-10b | [#103](https://github.com/bdgrz/compliance/issues/103) | R1 | P0 | R1-10a |
| R1-10c | [#104](https://github.com/bdgrz/compliance/issues/104) | R1 | P0 | R1-10a |
| R1-10d | [#105](https://github.com/bdgrz/compliance/issues/105) | R1 | P0 | R1-10a |
| R1-11 | [#49](https://github.com/bdgrz/compliance/issues/49) | R1 | P0 | M0-D06, M0-D22, M0-D28, EN-05, R1-04 |
| R1-11a | [#106](https://github.com/bdgrz/compliance/issues/106) | R1 | P0 | M0-D06, M0-D22, M0-D28, EN-05, R1-04 |
| R1-11b | [#107](https://github.com/bdgrz/compliance/issues/107) | R1 | P0 | R1-11a |
| R1-11c | [#108](https://github.com/bdgrz/compliance/issues/108) | R1 | P0 | R1-11a |
| R1-11d | [#109](https://github.com/bdgrz/compliance/issues/109) | R1 | P0 | EN-03, R1-11a |
| R1-12 | [#50](https://github.com/bdgrz/compliance/issues/50) | R1 | P0 | M0-D08, M0-D22, M0-D28, EN-02, EN-05, R1-02, R1-10 |
| R1-13 | [#51](https://github.com/bdgrz/compliance/issues/51) | R1 | P0 | M0-D09, EN-02, EN-04, R1-02 |
| R1-14 | [#52](https://github.com/bdgrz/compliance/issues/52) | R1 | P0 | M0-D11, M0-D23, EN-02, EN-06, R1-02, R1-10, R1-13 |
| R1-15 | [#127](https://github.com/bdgrz/compliance/issues/127) | R1 | P0 | M0-A07, M0-D25, M0-D28, EN-01 |
| R1-15 backend | [#152](https://github.com/bdgrz/compliance/issues/152) | R1 | P0 | M0-A07, M0-D25, M0-D28, EN-01; PR #151 credited, split-host proof pending |
| R2-01 | [#15](https://github.com/bdgrz/compliance/issues/15) | R2 | P0 | M0-D03, R1-04, R1-05 |
| R2-02 | [#16](https://github.com/bdgrz/compliance/issues/16) | R2 | P0 | EN-02, EN-04, EN-06, R1-04 |
| R2-03 | [#17](https://github.com/bdgrz/compliance/issues/17) | R2 | P0 | EN-06, R1-04 |
| R2-04 | [#18](https://github.com/bdgrz/compliance/issues/18) | R2 | P0 | R2-01, R2-03 |
| R2-05 | [#19](https://github.com/bdgrz/compliance/issues/19) | R2 | P0 | M0-D03, M0-D13, M0-D23, EN-04, R1-05, R2-03 |
| R2-05a | [#110](https://github.com/bdgrz/compliance/issues/110) | R2 | P0 | M0-D03, M0-D13, M0-D23, EN-04, R1-05, R2-03 |
| R2-05b | [#111](https://github.com/bdgrz/compliance/issues/111) | R2 | P0 | R2-05a |
| R2-05c | [#112](https://github.com/bdgrz/compliance/issues/112) | R2 | P0 | R2-05b |
| R2-05d | [#113](https://github.com/bdgrz/compliance/issues/113) | R2 | P0 | R2-05c, R2-07 |
| R2-06 | [#20](https://github.com/bdgrz/compliance/issues/20) | R2 | P0 | M0-D05, M0-D06, M0-D07, M0-D22, M0-D28, EN-03, EN-05, R1-10, R1-11 |
| R2-06a | [#114](https://github.com/bdgrz/compliance/issues/114) | R2 | P0 | M0-D05, M0-D06, M0-D07, M0-D22, M0-D28, EN-03, EN-05, R1-10, R1-11 |
| R2-06b | [#115](https://github.com/bdgrz/compliance/issues/115) | R2 | P0 | R1-11, R2-06a |
| R2-06c | [#116](https://github.com/bdgrz/compliance/issues/116) | R2 | P0 | R2-06a |
| R2-06d | [#117](https://github.com/bdgrz/compliance/issues/117) | R2 | P0 | R2-06a |
| R2-06e | [#118](https://github.com/bdgrz/compliance/issues/118) | R2 | P0 | R2-06d |
| R2-07 | [#21](https://github.com/bdgrz/compliance/issues/21) | R2 | P0 | M0-D23, EN-04, R1-08 |
| R2-08 | [#22](https://github.com/bdgrz/compliance/issues/22) | R2 | P0 | M0-D14, EN-06, R1-08 |
| R2-09 | [#23](https://github.com/bdgrz/compliance/issues/23) | R2 | P0 | M0-D23, EN-03, R1-08, R2-02, R2-05, R2-06, R2-07, R2-10 |
| R2-10 | [#53](https://github.com/bdgrz/compliance/issues/53) | R2 | P0 | M0-D06, M0-D12, R1-11, R2-02 |
| R2-11 | [#54](https://github.com/bdgrz/compliance/issues/54) | R2 | P0 | M0-A05, M0-D03, M0-D15, EN-01, R2-01 |
| R2-11a | [#119](https://github.com/bdgrz/compliance/issues/119) | R2 | P0 | M0-A05, M0-D03, M0-D15, EN-01, R2-01 |
| R2-11b | [#120](https://github.com/bdgrz/compliance/issues/120) | R2 | P0 | R2-11a |
| R2-11c | [#121](https://github.com/bdgrz/compliance/issues/121) | R2 | P0 | R2-11a |
| R2-12 | [#55](https://github.com/bdgrz/compliance/issues/55) | R2 | P1 | M0-D16, R2-03 |
| T1-01 | [#24](https://github.com/bdgrz/compliance/issues/24) | T1 | P1 | M0-D01, R2-09 |
| T1-02 | [#25](https://github.com/bdgrz/compliance/issues/25) | T1 | P1 | M0-D17, T1-01 |
| T1-03 | [#26](https://github.com/bdgrz/compliance/issues/26) | T1 | P2 | M0-D17, M0-D27, T1-01 |
| T1-04 | [#27](https://github.com/bdgrz/compliance/issues/27) | T1 | P1 | R2-03, T1-01 |
| T1-05 | [#28](https://github.com/bdgrz/compliance/issues/28) | T1 | P1 | M0-D17, T1-01, T1-02 |
| T1-06 | [#29](https://github.com/bdgrz/compliance/issues/29) | T1 | P1 | R2-07, T1-01 |
| T1-07 | [#30](https://github.com/bdgrz/compliance/issues/30) | T1 | P1 | M0-D17, T1-05, T1-06 |
| T2-01 | [#31](https://github.com/bdgrz/compliance/issues/31) | T2 | P1 | M0-D01, T1-07 |
| T2-02 | [#32](https://github.com/bdgrz/compliance/issues/32) | T2 | P1 | R2-01, T2-01 |
| T2-03 | [#33](https://github.com/bdgrz/compliance/issues/33) | T2 | P1 | R2-05, T2-02 |
| T2-04 | [#34](https://github.com/bdgrz/compliance/issues/34) | T2 | P1 | M0-D23, R1-08, T2-01 |
| T2-05 | [#35](https://github.com/bdgrz/compliance/issues/35) | T2 | P1 | R2-06, T2-01 |
| T2-06 | [#36](https://github.com/bdgrz/compliance/issues/36) | T2 | P2 | M0-D19, T2-01 |
| T2-07 | [#37](https://github.com/bdgrz/compliance/issues/37) | T2 | P1 | M0-D21, T1-02, T2-01 |
| T2-08 | [#38](https://github.com/bdgrz/compliance/issues/38) | T2 | P2 | M0-D20, M0-D24 |
| T2-08a | [#137](https://github.com/bdgrz/compliance/issues/137) | T2 | P2 | M0-D20, M0-D24 |
| T2-08b | [#138](https://github.com/bdgrz/compliance/issues/138) | T2 | P2 | T2-08a |
| T2-09 | [#39](https://github.com/bdgrz/compliance/issues/39) | T2 | P2 | M0-D19, M0-D23, T2-04 |
| T3-01 | [#40](https://github.com/bdgrz/compliance/issues/40) | T3 | P1 | T2-03, T2-05, T2-07 |
| T3-02 | [#41](https://github.com/bdgrz/compliance/issues/41) | T3 | P1 | M0-D17, T3-01 |
| T3-03 | [#42](https://github.com/bdgrz/compliance/issues/42) | T3 | P1 | T1-04, T3-02 |
| T3-04 | [#43](https://github.com/bdgrz/compliance/issues/43) | T3 | P1 | R2-07, T3-03 |
| T3-05 | [#44](https://github.com/bdgrz/compliance/issues/44) | T3 | P1 | M0-D17, T1-05, T3-01, T3-02 |
| T3-06 | [#45](https://github.com/bdgrz/compliance/issues/45) | T3 | P1 | M0-D17, T3-04, T3-05 |
| T3-07 | [#46](https://github.com/bdgrz/compliance/issues/46) | T3 | P1 | T3-06 |
| F1-01 | [#128](https://github.com/bdgrz/compliance/issues/128) | F1 | P1 | M0-D16, M0-D25, M0-D27, F1-04, F1-07, R1-15, R2-12 |
| F1-02 | [#129](https://github.com/bdgrz/compliance/issues/129) | F1 | P1 | M0-A05, F1-07, R1-08, R1-15, R2-11 |
| F1-03 | [#130](https://github.com/bdgrz/compliance/issues/130) | F1 | P2 | F1-02, F1-08 |
| F1-04 | [#131](https://github.com/bdgrz/compliance/issues/131) | F1 | P1 | M0-D02, M0-D25, EN-02, R1-05, R2-02 |
| F1-05 | [#132](https://github.com/bdgrz/compliance/issues/132) | F1 | P1 | F1-02, R2-11 |
| F1-06 | [#133](https://github.com/bdgrz/compliance/issues/133) | F1 | P1 | M0-A07, R1-04, R1-15 |
| F1-07 | [#134](https://github.com/bdgrz/compliance/issues/134) | F1 | P1 | M0-D25, M0-D26, EN-04, R1-04, R1-15 |
| F1-08 | [#135](https://github.com/bdgrz/compliance/issues/135) | F1 | P1 | M0-D26, EN-01, EN-04, F1-07 |

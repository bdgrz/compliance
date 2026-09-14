# Compliance product backlog

Status: product-owner baseline

This backlog carries a small compliance team through SOC 2 readiness, a Type I examination, a Type II observation period, and a Type II examination. It implements the product direction in [product-brief.md](product-brief.md).

The shared language, identity boundaries, record relationships, and
cross-story invariants are defined in [domain-model.md](domain-model.md). That
model is part of every issue created from this backlog.

The product coverage decisions behind this version are recorded in
[gap-analysis.md](gap-analysis.md).

## Product-backlog contract

Every issue is a user story that delivers a business outcome. Infrastructure, schema, API, background processing, and UI tasks may be implementation subtasks, but they are not separate product-backlog items.

Every story is a vertical slice from authorized API behavior through the usable browser experience. Completing only the API, worker, persistence, or UI does not complete the story.

Every story must include:

- a named user and valuable outcome;
- a business objective;
- product requirements and business rules;
- observable acceptance criteria;
- server-enforced authorization;
- usable loading, empty, error, retry, and forbidden states;
- traceable activity and historical behavior where the action matters to an audit;
- accessible UI and documented API behavior;
- focused automated acceptance evidence.

## Domain-coherence contract

Every issue must include its `Domain slice` and `Implementation subtasks` from
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
explicitly excluded.

## Definition of done

A story is done when its full API-to-UI workflow meets the acceptance criteria; allowed and denied behavior is tested; changes and decisions are traceable; period and snapshot behavior is correct; relevant failure states are recoverable; and the result works in the supported standalone and split-host deployments.

## R1 - Readiness program scoped

Business outcome: the team has an agreed system boundary; authoritative
workforce context; application, technology, and information inventories;
service commitments and system requirements; criteria; roles; controls; risks;
providers; and an owned gap plan. Nothing in this milestone claims audit
readiness or an auditor opinion.

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

Implementation subtasks:

- [ ] Validate the first engagement's categories and any category-specific workflow needs, especially the personal-information lifecycle if Privacy is selected.
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

Acceptance criteria:

- [ ] An administrator can invite or activate an externally authenticated user and explain the access being granted.
- [ ] Each user sees only the programs, records, and actions allowed by their responsibilities.
- [ ] Assignment conflicts are surfaced before approval or review work is accepted.
- [ ] Removing access takes effect immediately, preserves authorship history, and exposes work requiring reassignment.

Implementation subtasks:

- [ ] Validate the first built-in role catalog, scope hierarchy, team behavior, IdP-group expectations, invitation behavior, and acceptable small-team conflicts with the target user.
- [ ] Define member and identity-binding lifecycles, explicit group mappings, team membership, access grants, revocation, actor attribution, and responsibility boundaries.
- [ ] Deliver provider-authenticated activation, member/team administration, scoped authorization, reassignment warnings, and access explanations through the API and browser.
- [ ] Enforce every allow and deny decision on the server, including direct grants, team grants, removed provider groups, suspension, deprovisioning, and separation-of-duties conflicts.
- [ ] Prove identity replacement, immediate revocation, historical attribution, orphaned-work recovery, forbidden UI states, and standalone/split-host parity end to end.

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

Priority: P1  
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
  `RiskTreatment`, review decision, time-bounded acceptance, and reassessment state.
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

- [ ] Validate the initial assessment method, scales, materiality, appetite, review cadence, treatment vocabulary, and acceptance authority with the current readiness engagement.
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

- Owns the `ReadinessAssessment`, accountable `Gap`, assessment rules, and
  as-of calculation identity; it does not own the source records it evaluates.
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

Acceptance criteria:

- [ ] The team can preview counts, relationships, warnings, and errors before any imported record becomes active.
- [ ] A failed or canceled import cannot leave an apparently complete partial program.
- [ ] Every imported record can be traced to its source and import batch.
- [ ] Repeating the same import does not silently duplicate controls, relationships, evidence, or findings.
- [ ] Conflicting changes require an explicit resolution and preserve both the source value and the accepted result.
- [ ] After import, the readiness assessment reconciles to the accepted records and clearly shows what still remains outside the product.

Implementation subtasks:

- [ ] Validate the first real source files and define source identifiers, normalization, member matching, external-author treatment, conflicts, and atomic acceptance rules.
- [ ] Deliver authorized upload, parse, preview, correct, accept, cancel, and retry behavior through the API, any required worker processing, and browser.
- [ ] Route accepted items through the owning contexts, including identity and responsibility resolution, lifecycle checks, provenance, evidence content identity, and readiness recalculation.
- [ ] Prove replay safety, partial and interrupted failure, accepted subsets, unresolved references, duplicate prevention, denied imports, and source-to-result traceability end to end.

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

- [ ] Validate the current real application list and define minimum inventory fields, ownership, application-versus-reviewed-system boundaries, aliases, lifecycle, source confidence, and scope-decision rules.
- [ ] Define stable application and reviewed-system identity, source-aware import and reconciliation, explicit inclusion or exclusion, relationship, impact, retirement, and narrowly permitted unused-draft deletion semantics.
- [ ] Deliver authorized add, import, preview, match, reconcile, classify, own, scope, relate, revise, retire, browse, and inspect behavior through the API and browser.
- [ ] Connect applications to boundary, vendors, controls, policies, evidence, external access governance, work, readiness, automation, and engagement snapshots without creating duplicate system records.
- [ ] Prove duplicate and alias handling, missing source rows, ownership gaps, scope decisions, restricted visibility, relationship impact, retirement history, import replay, and standalone/split-host parity end to end.

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

- [ ] Validate the first authoritative roster, required worker attributes, source precedence, privacy boundary, joiner-mover-leaver rules, NHI ownership, and accepted manual fallback with the target team.
- [ ] Define person and source identity, lifecycle observations, manager and owner relationships, correlation, reconciliation, conflict, freshness, snapshot, and retention rules.
- [ ] Deliver authorized manual entry, import, preview, match, reconcile, classify, own, browse, freeze, and inspect behavior through the API, bounded processing where needed, and browser.
- [ ] Connect workforce context to platform responsibility without merging identities and to policy, training, access, evidence, control, readiness, population, and snapshot workflows.
- [ ] Prove conflicting and missing sources, identity replacement, ambiguous matches, stale and partial imports, NHI ownership gaps, authorization, replay, snapshot stability, and end-to-end reconciliation.

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

- [ ] Validate the minimum component, information, location, classification, retention, and data-flow detail required for the first real boundary and system description.
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

- [ ] Validate the first engagement's commitments, requirements, CUECs, CSOCs, source artifacts, minimum fields, and approval authority with the target team and advisor.
- [ ] Define stable identities, source and version provenance, applicability, interpretation, conflict, review, effective-date, supersession, withdrawal, and impact rules.
- [ ] Deliver authorized capture or import, relate, review, approve, revise, supersede, browse, and inspect behavior through the API and browser.
- [ ] Connect approved records to boundary, inventories, providers, criteria, controls, policies, risks, readiness, system description, assertions, packages, and snapshots.
- [ ] Prove conflicting sources, expired and unmapped records, forbidden approval, concurrent revision, impact preview, historical retrieval, and package stability end to end.

### R1-14 Evaluate vendors and subservice organizations

Priority: P1  
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
  coverage gap, renewal or termination review, and next-review state.
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

- [ ] Validate the material-provider threshold, initial due-diligence set, review cadence, assurance-report fields, bridge-letter use, and subservice treatment with the current readiness engagement.
- [ ] Define provider identity, service relationships, assessment versions, evidence coverage, exception, boundary treatment, review, renewal, termination, and acceptance rules.
- [ ] Deliver authorized record, import, classify, assess, review, relate, remediate, accept risk, renew, terminate, browse, and inspect behavior through the API and browser.
- [ ] Connect providers to inventories, commitments, risks, controls, policies, evidence, findings, accountable work, readiness, description, management review, packages, and snapshots.
- [ ] Prove stale and partial assurance, uncovered periods, missing CSOCs, restricted content, change impact, denied acceptance, reassessment history, and end-to-end readiness reconciliation.

## R2 - Control environment implemented

Business outcome: required controls and policies are implemented and evaluated;
policy communication and evidence are governed; actual human and NHI access is
reconciled with approved expectations; accountable work and gaps are visible;
and the team can make an evidence-backed Type I entry decision.

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
  deviation, assigned `Review`, immutable `ReviewDecision`, comments,
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

- [ ] Validate the first real evaluation procedures and define plan versions, assertion and procedure results, applicable populations or inspected items, conclusions, deviations, retest, review assignment, and independence rules.
- [ ] Deliver authorized plan, assign, perform, document, submit, inspect, comment, accept, reject, request-change, remediate, and retest behavior through the API and browser.
- [ ] Bind evaluations to exact scope, inventory, commitment, risk, criterion, control, policy, provider, and evidence versions and connect decisions to work, findings, readiness, descriptions, and snapshots.
- [ ] Prove insufficient and conflicting evidence, failed procedures, deviation handling, design-versus-operation distinctions, self-review denial, concurrent revisions, rework, retest, small-team exceptions, and historical reproducibility end to end.

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
- Import an authoritative access-subject roster where available, preserving
  employment or engagement status, manager or owner, source identifiers,
  capture time, and unresolved identity questions.
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

- [ ] Validate the real application list, workforce or identity roster, and first provider exports, including the AWS no-IAM-user expectation, before finalizing normalization and campaign rules.
- [ ] Define human and NHI access-subject identity and lifecycle, NHI ownership and purpose, provider principal kinds, explicit classification and correction, provider object identity, group and role relationships, direct and inherited grant paths, entitlement and resource identity, external access-grant uniqueness, and optional platform-member correlation.
- [ ] Define source-snapshot completeness, normalization, correlation, group expansion, effective access, expectation and prohibition, variance, frozen campaign, assignment, bulk decision, remediation, independent verification, exception, and completion invariants.
- [ ] Deliver authorized roster and access-source import, preview, correction and acceptance, expectation authoring and approval, campaign launch, variance review, per-item and bulk decision, remediation, verification, and final snapshot through the API, bounded worker processing where needed, and browser.
- [ ] Feed campaign work into the shared work experience and its accepted or unresolved result into evidence, control support, findings, readiness, and engagement snapshots.
- [ ] Prove employee, contractor and collaborator humans; service, workload, integration, automation and bot NHIs; groups and roles; ambiguous and corrected classification; ownerless NHIs; direct, nested and inherited access; prohibited and zero-tolerance expectations; missing exports; duplicate grants; source changes; member deprovisioning; partial failure; unauthorized review; unverified remediation; and snapshot history end to end.

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

- Owns `Finding`, `Exception`, `CorrectiveAction`, `RiskAcceptance`, closure
  `ReviewDecision`, and required `Verification` with source wording preserved.
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

- [ ] Validate the consultant's preferred workflow and define selection, draft visibility, external authorship, validation meaning, delivery, revocation, and change-comparison rules.
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
  including unresolved-item acknowledgements and approved exceptions.
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

Implementation subtasks:

- [ ] Define readiness-snapshot contents, calculation identity, blocker rules, acknowledgement, required approvers, and approve/defer/approve-with-exceptions transitions.
- [ ] Deliver authorized preview, drill-down, sign-off, defer, and exception acknowledgement through the API and accessible browser workflow.
- [ ] Bind the decision to exact source versions and route deferrals or conditions into the shared finding, responsibility, and work model while preserving later changes separately.
- [ ] Prove incomplete and stale inputs, unresolved acknowledgements, separation of duties, concurrent changes, denied approval, immutable history, and transition effects end to end.

### R2-10 Publish policies and verify acknowledgement and training

Priority: P1  
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

- [ ] Validate the first policy audience, acknowledgement language, required training, reminder cadence, exception policy, external LMS evidence, and joiner-mover-leaver behavior.
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

- [ ] Validate the target team's daily queue, minimum source workflows, priority and materiality rules, assignment actions, reminders, digests, and escalation expectations.
- [ ] Define projection identity, source-state mapping, assignment and delegation, orphaning, due and blocked semantics, notification preference, delivery, deduplication, and reconciliation rules.
- [ ] Deliver authorized personal and team queues, filters, assignment actions, source navigation, reminder preferences, digest, acknowledgement, and escalation through the API, bounded processing, and browser.
- [ ] Integrate source workflows through stable identities and explicit allowed actions; derive counts and measures from those records without introducing generic completion state.
- [ ] Prove stale projections, concurrent source changes, revoked access, team changes, self-review conflicts, orphaning, duplicate and failed reminders, restricted search, and standalone/split-host parity end to end.

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

- [ ] Validate the first handling classes, storage and inspection boundary, retention periods, engagement and legal hold rules, redaction workflow, disposition authority, and auditor-sharing expectations.
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

- [ ] Validate required sections, Type I as-of and Type II period presentation, significant-change treatment, source reconciliation, and preferred export with advisor or auditor input.
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

- [ ] Validate that direct auditor access is wanted before scheduling this P2 story; define scope, draft-sharing, time-bound grant, download, comment, and revocation rules.
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

- [ ] Validate the audit firm's minimum control matrix, evidence index, system-description, assertion-input, workbook, archive, naming, and delivery formats before implementation.
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

- [ ] Validate the assertion and representation-letter sequence, required signers, supplied templates, report references, and handoff timing with the audit firm.
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
  results; it does not own another copy of control, evidence, access, policy, or finding state.
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

- [ ] Validate that these reviews fit the common occurrence and review experience before scheduling this P2 story; define subject-specific decision and material-change rules.
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

- [ ] Validate the minimum compliance-facing change and incident facts, source links, materiality, restricted-detail boundary, description effect, impact review, chronology, and closure rules without replacing operational source systems.
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
- [ ] Permission denial, rate limiting, partial results, stale data, and provider removal are visible and cannot appear complete.
- [ ] Repeated collection does not create duplicate evidence for the same source snapshot.
- [ ] Missing source records require explicit tombstone or reconciliation treatment and cannot silently delete, merge, retire, or de-scope governed records.
- [ ] Each current inventory or population view explains its source coverage, last complete observation, outstanding conflicts, and accountable owner.
- [ ] Users never see provider credential material after configuration.

Implementation subtasks:

- [ ] Run the inventory-integration discovery in the gap analysis before scheduling this P2 story; measure the first manual workflow and validate source authority, stable identifiers, completeness, matching, tombstones, freshness, and business value.
- [ ] Define least-privilege scopes, credential custody, source and raw-snapshot identity, collection boundaries, pagination, normalization, reconciliation, freshness, disable, and revocation rules.
- [ ] Deliver authorized connection consent, test, scope and schedule configuration, run status, disable, and revoke behavior through the API, bounded worker execution, and browser.
- [ ] Normalize results into existing workforce, inventory, external-access, population, or evidence primitives with provider identifiers, system-actor attribution, idempotency, partial-failure detail, preview, and ordinary human review.
- [ ] Prove denied and revoked credentials, pagination gaps, rate limiting, partial and stale results, tombstones, ambiguous matches, retries, duplicate snapshots, secret non-disclosure, cross-organization isolation, reconciliation history, and manual-workflow parity end to end.

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
- Uses the shared readiness projection and exact underlying control, evidence,
  access, finding, risk, vendor, incident, and change records as of a known time.
- Produces accountable work and period evidence without creating a parallel
  management-only readiness or task model.

Acceptance criteria:

- [ ] The review identifies the exact as-of snapshot and unresolved material items.
- [ ] Management can approve, request action, or defer with rationale.
- [ ] Assigned actions appear in accountable work queues.
- [ ] Later data changes do not silently change the historical review.

Implementation subtasks:

- [ ] Validate that management review is not merely an ordinary recurring control before scheduling this P2 story; define snapshot, agenda, quorum or approver, decision, action, and deferral rules.
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

- [ ] Validate the audit firm's first population definitions, source-total and completeness evidence, required columns, stable identifiers, selection format, and treatment of late or corrected rows.
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
  review, risk-acceptance, and verification model for examination exceptions.
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

- [ ] Validate the audit firm's period, control matrix, population, sample, evidence index, system-description, assertion-input, workbook, archive, naming, and delivery formats before implementation.
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

- [ ] Validate the assertion and representation-letter sequence, required signers, supplied templates, report references, and final handoff timing with the audit firm.
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

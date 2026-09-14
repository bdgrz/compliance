# Compliance domain model

Status: working product and domain contract, updated 2026-09-14

This document defines the shared language and relationships used by the
Compliance product backlog. It is a product model, not a database schema or a
commitment to separate services. The bounded contexts below should begin as
cohesive modules inside the existing application.

## Modeling rules

- Model the compliance process, not a collection of independent CRUD screens.
- Keep definitions, performances, evidence, reviews, decisions, and snapshots
  distinct even when the UI presents them together.
- Give every business record a stable platform identity. Names, email
  addresses, filenames, and provider display values are attributes, not keys.
- Preserve effective history. A correction creates a revision, amendment, or
  superseding record instead of changing completed audit history in place.
- Treat every status as a projection from inspectable business records and
  rules. A status is never an unexplained manual flag.
- Keep authorization and work responsibility distinct. Permission to perform
  an action does not by itself assign the action to someone.
- Preserve source provenance for imported, collected, advisor-authored, and
  auditor-authored material.
- Use explicit domain workflows. A generic task list, activity stream, or audit
  log may summarize work, but it does not own the underlying business state.

## Cohesive process spine

The primary product flow is:

```text
Organization and membership
  -> Program and system boundary
  -> Workforce, technology, information, and provider inventories
  -> Service commitments, system requirements, and risk assessment
  -> Criteria edition and engagement scope
  -> Control definition and criteria mapping
  -> Responsibility and operating cadence
  -> Control occurrence or other assurance activity
  -> Evidence submission and support relationship
  -> Independent review and decision
  -> Gap, exception, remediation, or accepted result
  -> Explainable readiness view
  -> Frozen engagement or period snapshot
  -> Indexed handoff or audit package
  -> Next engagement or period
```

Every product story must identify where it enters this spine, which upstream
records it relies on, and which downstream records or decisions it affects.

## Bounded contexts

These are conceptual ownership boundaries, not projects, deployments, or
microservices.

| Context | Owns | Does not own |
| --- | --- | --- |
| Platform access | Organizations, members, teams, identity bindings, access roles, access grants | External accounts and entitlements being audited |
| Program and scope | Programs, stages, system boundaries, inclusions, exclusions, engagement plans | Criteria source content or control operation |
| Workforce assurance | People, employment or engagement lifecycle facts, managers, workforce populations, NHI ownership | Platform access or provider accounts |
| Application inventory | Applications, concrete reviewed-system instances, ownership, classification, lifecycle, review scope | Observed accounts, permissions, and campaign decisions |
| Technology and information inventory | Material system components, information sets, data stores and flows, classifications, ownership, lifecycle, source reconciliation | Operational configuration management or data processing |
| Commitments and requirements | Service commitments, system requirements, CUECs, CSOCs, applicability and effective history | Source contract management or proof that controls operated |
| Criteria | Catalog editions, criteria, categories, selected scope, mapping provenance | Organization-authored controls |
| Control environment | Controls, control versions, mappings, responsibilities, cadence, occurrences | Evidence content or external directory populations |
| Policies | Policies, policy versions, reviews, approvals, effective periods | Proof that a control operated |
| Evidence | Evidence artifacts, content identity, provenance, requests, support relationships, review state | The control or decision that evidence supports |
| Risk and provider oversight | Risk assessments and treatments, vendors, subservice organizations, assurance reviews and boundary treatment | Procurement transactions or auditor opinions over providers |
| Assurance work | Reviews, decisions, findings, exceptions, corrective actions, work projections, risk acceptance, verification | Authentication, source workflows, or provider directory state |
| External access governance | Reviewed systems, external principals, entitlements, access grants, population snapshots, campaigns | Platform membership and platform authorization |
| Engagements and handoffs | Type I baselines, Type II periods, populations, samples, requests, packages, amendments, outcomes | Mutable source records after they have been snapshotted |

Cross-context references use stable identities and explicit versions or
snapshots when history matters. One context must not silently mutate another
context's aggregate.

### Unresolved ownership

The 2026-09-14 backlog design review found concepts that this model and the
backlog assign to more than one owner, or to none. They remain open decisions
and must be resolved before the stories that depend on them are ready:

| Concern | Conflict | Decision |
| --- | --- | --- |
| `ReviewedSystem` | Listed under both Application inventory and External access governance | M0-D22 |
| `AccessSubject` and `Person` | R2-06 imports an access-subject roster with employment status and manager, duplicating the workforce roster in R1-11 | M0-D22 |
| NHI records | Workforce assurance owns NHI ownership, but the NHI `AccessSubject` is first created by access governance | M0-D22 |
| Service, location, and process | Referenced by the boundary, commitments, providers, and system description, but not defined by any context | M0-D22 |
| Incident reference | Used by risk reassessment in readiness, but defined only for the Type II period | M0-D22 |
| Control-to-risk relationship | Could belong to control applicability or to risk treatment | M0-D22 |
| Gap, finding, deviation, and coverage gap | Readiness gaps, findings, evaluation deviations, and provider coverage gaps overlap | M0-D23 |
| "Exception" | Means both an approved waiver and an auditor-identified test exception | M0-D23 |
| Risk acceptance | Owned by both risk treatment and findings | M0-D23 |
| Review and approval | Needed by many early workflows, while a universal `Review` aggregate is forbidden | M0-D23, EN-04 |
| Readiness rules | Split across the readiness assessment, readiness snapshot, readiness projection, and management review | M0-D23 |
| Accessibility and browser support | Every story requires accessible browser behavior, but the conformance target, assistive-technology baseline, and supported-browser policy are undecided | M0-D24 |

When a decision is made, update this document, the affected stories, and the
issue, and remove the row.

## Identity has three planes

Compliance must keep these planes separate even when they describe the same
human being.

```text
External identity provider       Compliance platform        Access-governance data
Auth0 or Entra subject     ->     Member and access grants    Access subject or account
IdP group or claim         ->     Team or role mapping        Group, role, or entitlement
Authenticates the caller          Governs Compliance          Describes access under review
```

### Authentication identity

An `ExternalIdentity` is a binding between a provider identity and a platform
member.

- Its stable provider key is issuer plus subject.
- Email, display name, and claim values are descriptive and may change.
- A member may acquire a replacement or additional provider identity without
  losing platform authorship or assignments.
- Authentication proves who is calling. It does not, by itself, grant access to
  an organization or program.

### Platform membership and authorization

An `Organization` is the initial security and ownership boundary. A `Program`
belongs to an organization. The first release need not expose multiple
workspaces, but the model must not merge organization membership with a single
program.

A `Member` is an organization-local relationship for a person who can use
Compliance. Its lifecycle is pending, active, suspended, or deprovisioned.
Deprovisioning blocks new access immediately while preserving authorship,
decisions, comments, and historical assignments.

A `Team` is a platform-owned group of members used for access and assignment.
It is not an Auth0 or Entra group and is not a group being reviewed in an
external access campaign.

An `AccessRole` is a stable bundle of platform permissions. The first release
uses a small built-in catalog rather than user-defined permissions:

- organization administrator;
- compliance lead;
- contributor;
- reviewer;
- management approver;
- read-only advisor.

An `AccessGrant` gives a member or team an access role within an organization,
program, engagement, or deliberately shared resource. A grant records its
source, effective interval, grantor, and revocation.

An IdP group or claim may later be mapped to a platform team or access grant.
Such a mapping is explicit, source-aware, and reviewable. Provider claim values
must not become implicit platform primary keys.

### Responsibility and separation of duties

A `Responsibility` assigns accountable work such as control owner, evidence
contributor, assigned reviewer, access reviewer, corrective-action owner, or
policy approver. It records scope and effective interval.

Access roles answer what a member may do. Responsibilities answer what the
member is expected to do. Authorization evaluates both where appropriate:

```text
active membership
  + scoped access grant
  + relevant responsibility
  + resource state
  + separation-of-duties policy
  = allowed or denied action
```

Changing a team, grant, or responsibility must identify open work that becomes
unassigned. It never changes the recorded performer, reviewer, or approver of
historical work.

### Actors

An `ActorReference` attributes a consequential action to an active member or a
named system process. A team can receive access or responsibility but cannot be
the actor that performed or approved an action.

Attribution retains the stable member or system identity and the relevant
display snapshot. Removing access, renaming a person, or replacing an identity
provider cannot make historical activity anonymous or falsely attribute it to
someone else.

Imported advisor or auditor authorship that did not occur through an
authenticated platform session is represented through source provenance and an
external-author reference. It is not fabricated as platform activity.

## Workforce assurance

A `Person` is an organization-level subject whose employment or engagement
facts help the team evaluate controls and access. It is not a Compliance
`Member`, an authentication identity, or a provider `DirectoryPrincipal`.

A person keeps a stable source-aware identity, worker type, lifecycle status,
manager, organization attributes needed for review, relevant start or end
dates, and observation history. Sensitive workforce fields are minimized and
authorized separately. Conflicting sources remain visible until an attributable
reconciliation decision identifies the accepted value and rationale.

A `WorkforceSnapshot` freezes the people universe used for a policy campaign,
training population, access review, control evaluation, or audit population.
Joiner, mover, and leaver facts may trigger compliance work, but imported facts
do not silently grant platform access, revoke provider access, or decide a
review outcome.

An NHI is still an `AccessSubject`, not a person. Its accountable human or team
owner, approved purpose, environment, lifecycle, and review date are governed
relationships. Provider observations can propose an NHI or owner correlation;
an authorized user accepts or rejects it.

## Application inventory

An `Application` is software the organization believes it uses or depends on.
It records business purpose, owner, vendor or internal ownership, lifecycle,
criticality, data sensitivity, authentication method, known user population,
and whether and why access review applies. Discovery source and confidence are
preserved so that declared, imported, discovered, duplicate, unknown, and
retired applications can be reconciled rather than silently merged.

A `ReviewedSystem` is a concrete tenant, organization, account, environment, or
other access boundary for an application. One application may have multiple
reviewed systems, and one reviewed system must not be reused for unrelated
applications merely because the provider is the same.

The inventory links applications and reviewed systems to the program boundary,
vendors or subservice organizations, data and process scope, controls, evidence
sources, system owners, access owners, review cadence, and explicit inclusion or
exclusion decisions. An application cannot silently disappear from review scope
because an import or integration stops returning it.

## Technology and information inventory

A `SystemComponent` is a material technology resource needed to explain or
control the scoped system, such as a cloud account or subscription,
infrastructure environment, network boundary, managed endpoint class,
repository, or data store. A component records stable source identities,
ownership, type, location or environment, criticality, lifecycle, and scope
decision. It may relate to several applications but is not another application.

An `InformationAsset` is a governed information set or data class, not every
individual file or database row. It records purpose, owner, classification,
customer or personal-information relevance, locations, retention expectation,
and applicable commitments or controls. A versioned `DataFlow` describes a
material movement between actors, applications, components, providers, or
locations and identifies protection expectations.

These records are audit-relevant inventory projections. Cloud, MDM, CMDB,
repository, and data-governance systems remain the operational sources. Manual,
imported, and discovered records preserve provenance, matching, rejected rows,
missing-source behavior, and reconciliation decisions.

## Commitments and system requirements

A `ServiceCommitment` records a promise made to customers or users. A
`SystemRequirement` records a contractual, legal, policy, architectural, or
other requirement that governs how the service system should operate. Each has
a stable identity, source reference, owner, applicability, effective interval,
and version history.

A `UserEntityResponsibility` represents a complementary user-entity control
(CUEC) expected of customers or other user entities. A
`SubserviceResponsibility` represents a relevant complementary subservice-
organization control (CSOC) or other dependency. These remain distinct from the
organization's controls and from evidence that anyone performed them.

Commitments and requirements may relate to services, scope, criteria, risks,
applications, reviewed systems, components, information assets, providers,
controls, and policies. The approved applicable versions supply structured
source material to the system description and engagement snapshots.

## Risk and provider oversight

A `Risk` describes a scoped uncertainty and its potential effect. A versioned
`RiskAssessment` records context, likelihood and impact rationale, inherent
risk, existing controls, chosen response, target or residual risk, owner,
reviewer, and review date. The scoring method is explicit and versioned; labels
or numbers from different methods are not silently compared.

A `RiskTreatment` is an accountable avoid, mitigate, transfer, or accept
decision. Acceptance is time-bounded and authorized. Treatment work links to
controls, policies, provider decisions, or corrective actions and is verified
before the risk is represented as addressed.

A `Provider` records a vendor or subservice organization and its material
services, systems and data access, owner, criticality, contract and review
dates, and boundary treatment. A `ProviderAssessment` binds due diligence,
assurance artifacts such as SOC reports or bridge letters, coverage periods,
exceptions, compensating controls, reviewer conclusions, and remediation to the
exact provider and service relationship reviewed. Procurement remains the
source of commercial transactions.

## External systems and access governance

A `ReviewedSystem` records the source identity and access boundary whose
principals and entitlements are examined. Its application, owner, sensitivity,
review scope, and review cadence come from the application inventory.

An `AccessSubject` is the organization-level human or non-human identity (NHI)
whose access may span several reviewed systems. A human subject may represent
an employee, contractor, or external collaborator and may come from an
authoritative people roster. An NHI may represent a workload, service,
integration, automation, or bot and must have an accountable human or team
owner, approved purpose, environment, lifecycle, and authentication or
credential model. A subject is not a platform member and does not receive
Compliance access merely because the two records are correlated.

A `DirectoryPrincipal` is an observed provider object that can receive, convey,
or participate in access. Provider-neutral principal kinds begin with account,
group, role, service principal, and workload identity. An account or service
principal may correlate to a human or NHI access subject; a group or role is an
access structure and does not become a human or NHI merely because members or
assumers use it. Its identity is the reviewed system plus the source object's
immutable identifier; email and display name are attributes.

Human-versus-NHI classification is explicit, attributable, and reviewable.
Provider hints can propose a classification but cannot silently decide it.
Ambiguous, shared, generic, dormant, ownerless, and mixed-use principals remain
visible. A change of classification preserves the prior decision and identifies
affected expectations, campaigns, and findings.

An `Entitlement` describes access available within a reviewed system, including
a role, group membership, license, permission set, repository role, or similar
grantable access. External groups and roles retain their source identifiers and
relationships. Direct, group-derived, nested-group, and other inherited grant
paths remain distinguishable.

An `ExternalAccessGrant` is the reviewable relationship:

```text
directory principal + entitlement + resource + reviewed system
```

An access review population contains frozen snapshots of access subjects,
directory principals, group and role relationships, effective access grants,
and their grant paths—not live platform members. A member and an access subject
or directory principal may be explicitly correlated, but none owns or replaces
the others.

An `AccessExpectation` is an approved, time-bounded assertion about access that
should or should not exist. It identifies the applicable subject or governed
population, reviewed system, entitlement or access profile, rationale, source,
approver, effective interval, and any exception. Expectations can highlight
expected, unexpected, missing, expired, privileged, or unresolved access, but
they never pre-decide a review item. A human reviewer remains accountable for
the campaign decision.

Human access expectations may rely on employment or engagement status,
manager, job function, and approved business need. NHI expectations may rely on
workload purpose, accountable owner, environment, permitted authentication or
credential mechanism, least-privilege entitlements, and expiry or review date.
An application may prohibit an entire provider principal kind—for example, no
AWS IAM user principals—while permitting federated roles used by separately
classified human and NHI subjects.

A `PopulationSnapshot` records the source, capture time, import or collection
run, included and rejected items, normalization decisions, and content
identity. Launching an `AccessReviewCampaign` freezes its population,
instructions, reviewers, due date, and applicable rules.

Each `AccessReviewItem` receives an attributable keep, modify, revoke, or
unable-to-determine `AccessDecision`. Required change produces a
`RemediationAction`; completion requires separate `Verification` or an approved
exception. Provider-side change and platform-side verification remain distinct
facts.

## Primitive categories

| Category | Purpose | Initial examples |
| --- | --- | --- |
| Definition | States what should exist or happen | Criterion, Control, Policy, ReviewedSystem, Entitlement |
| Version | Preserves approved content over time | BoundaryVersion, ControlVersion, PolicyVersion, CatalogEdition |
| Relationship | Makes a supported assertion between records | ControlCriterionMapping, EvidenceSupport, ScopeInclusion |
| Responsibility | Assigns accountable work | ControlOwner, AssignedReviewer, RemediationOwner |
| Activity or occurrence | Records work that happened or is expected | ControlOccurrence, PolicyReview, AccessReviewCampaign |
| Artifact | Carries content or a reference to content | EvidenceArtifact, PolicyDocument, ImportFile, AuditPackage |
| Request | Asks for bounded work or support | EvidenceRequest, AdvisorRequest, AuditorRequest |
| Decision | Records an authorized outcome and rationale | ReviewDecision, AccessDecision, RiskAcceptance, ReadinessDecision |
| Exception and remediation | Carries a known gap to resolution | Finding, Exception, CorrectiveAction, Verification |
| Snapshot | Freezes applicable state for later reliance | PopulationSnapshot, TypeIBaseline, TypeIIPeriodClose |
| Provenance | Explains where data came from | SourceReference, ImportBatch, CollectionRun, content identity |
| Attribution | Explains who or what acted and when | ActorReference, external author, occurred time, effective time |

Application, AccessSubject, DirectoryPrincipal, external group membership,
ExternalAccessGrant, grant path, and AccessExpectation use these same
categories; they are not an isolated access-review data model.

The exact state machine belongs to the workflow that owns the record. Do not
create a universal `Entity`, `Activity`, `Task`, `Status`, or `Review` aggregate
whose generic states erase these distinctions.

An `AccountableWorkItem` is a read model over work owned by those explicit
workflows. It provides a consistent assignee, team, due date, urgency, blocked
reason, and destination for navigation, notification, and escalation. Completing
or reassigning work invokes the source workflow; it cannot independently claim
that a control, review, request, or remediation is complete.

## Shared platform primitives

Some mechanisms are shared by every bounded context. They are delivered once,
as approved enablers in [backlog.md](backlog.md), and consuming stories must
not build feature-local substitutes. Each is grounded in an architecture
decision recorded in M0.

| Primitive | Enabler | Architecture decision | Model rules it enforces |
| --- | --- | --- | --- |
| Authorization, organization isolation, and `ActorReference` | EN-01 | M0-A04 | Server-enforced authorization; attribution to members or named system processes; restricted records absent from lists and counts |
| Versions, effective intervals, and impact preview | EN-02 | M0-A01 | Drafts, immutable approved versions, successor proposals, effective history, and never-used-draft deletion |
| Snapshots, content identity, and amendments | EN-03 | M0-A01, M0-A02 | Frozen snapshots unaffected by later changes; amendments linked to their originals |
| Attributable decisions and separation of duties | EN-04 | — | A decision binds the exact input version, actor, time, and rationale. Each workflow keeps its own state machine; this is not a universal `Review` aggregate |
| Import batches, reconciliation, and replay | EN-05 | M0-A06 | Preview, explicit acceptance, partial-failure semantics, provenance, tombstones, and safe replay |
| Artifact storage and content identity | EN-06 | M0-A03 | Immutable content identity, quarantine, derived artifacts, and per-artifact access |

Read models such as readiness and accountable work follow M0-A05: they are
projections that reconcile to source records and show their as-of time.

## Core record relationships

### Program and engagement

- An organization owns members, teams, access grants, and programs.
- A program carries the continuing control environment through readiness,
  Type I, Type II, and later periods.
- An engagement or period references stable versions and snapshots from the
  program; it does not clone mutable records without provenance.
- A stage transition is an attributable decision against a stable readiness or
  outcome snapshot.
- A `SystemDescription` is a management-authored, versioned description of the
  scoped service system. It uses approved boundary, inventory, commitment,
  provider, control, and change records but is not a generated replacement for
  management narrative or the control catalog.
- Type I binds an approved description to the point-in-time baseline. Type II
  carries a successor through the observation period, identifies significant
  changes, and binds its approved version to the period close and management
  assertion.
- Management assertions, representation letters, service-auditor tests and
  results, and the issued report retain separate authorship and provenance. The
  product never derives an auditor opinion from readiness status.

### Controls and operation

- A `Control` is the organization's defined response to one or more risks or
  criteria: what must happen, why, where, by whom, how often, and what evidence
  is expected. It has stable identity and one or more versions.
- Creating a control creates a draft. Activating it requires review of the exact
  version plus its applicability, mappings, responsibility, cadence, and
  expected evidence.
- An active or historically used control is never edited in place. A change
  creates a proposed successor version with an effective date and an impact
  preview covering mappings, responsibilities, future occurrences, evidence
  expectations, readiness, and open or frozen engagements.
- Only a never-used draft with no retained business relationships may be
  deleted. Every other removal is retirement or supersession. Retirement stops
  future applicability and work without deleting prior versions, occurrences,
  evidence, decisions, or snapshots.
- A mapping relates a specific control version to a criterion edition and
  records rationale and review state.
- Responsibilities and cadence apply for an effective interval.
- A control occurrence represents one expected or actual performance for a
  point in time or covered period.
- An attestation records the performer, result, time, and submitted support.
- An independent review records a decision against the exact submission.
- Failed, skipped, rejected, or missing work creates an explainable gap or
  exception path; it does not disappear from readiness calculations.

### Policies

- A `Policy` is a governed statement of organizational intent, responsibility,
  or required behavior. It has stable identity, owner, purpose, audience,
  review cadence, lifecycle, and one or more immutable approved versions.
- A policy version may contain authored content or identify an immutable source
  artifact. It records its effective interval and exact approval decision.
- `PolicyApplicability` relationships associate an exact policy version with
  applications or reviewed systems, controls, criteria, risks, vendors,
  processes, and organizational scope where useful for navigation and impact
  analysis. Direct policy-to-criterion association never counts as control
  coverage, and a policy never proves that a control operated.
- Editing a draft does not alter an approved version. Updating an approved
  policy creates a proposed successor that must be reviewed and approved before
  becoming effective. Supersession preserves the version and relationships that
  applied to earlier engagements.
- Only a never-used draft with no retained business relationships may be
  deleted. An approved or historically used policy is superseded or retired.
  Retirement stops future applicability and review work while preserving audit
  history and packages.
- A `PolicyDistributionCampaign` binds an exact approved policy version to a
  frozen audience and delivery period. Each acknowledgement or exception is
  attributable; a later policy version never rewrites an earlier campaign.
- A `TrainingRequirement` may reference externally delivered course content.
  Compliance owns the applicable population, completion observation, exception,
  evidence, and control relationship, not the learning content or LMS workflow.

### Evidence

- An evidence artifact has stable identity, immutable content identity,
  provenance, sensitivity, capture time, and applicable period.
- An evidence support relationship explains what the artifact supports and why.
- Reusing an artifact creates another support relationship; it does not copy or
  silently reinterpret the artifact.
- Evidence acceptance is a review decision in context. An artifact is not
  universally sufficient merely because another reviewer accepted it elsewhere.
- Evidence access, disclosure, retention, engagement hold, redaction, and
  disposition are explicit decisions. A redacted file is a derived artifact
  linked to its source; it never replaces the original content identity.
- Retention expiry does not delete evidence that remains bound to an active
  engagement hold, delivered package, unresolved request, finding, or legal
  obligation. Authorized disposition preserves metadata and the decision while
  making unavailable content unmistakable.

### Findings and decisions

- A finding retains its source wording separately from internal analysis.
- Corrective actions are explicit, owned work linked to affected records.
- Risk acceptance is a time-bounded authorized decision, not a closed finding.
- Closure requires resolution evidence and review. Reopening preserves the
  earlier closure decision.

### Audit populations and samples

- An `AuditPopulationDefinition` states the source universe, covered period,
  inclusion and exclusion rules, expected completeness evidence, and fields
  authorized for audit use.
- An immutable `AuditPopulation` binds that definition to a close snapshot or
  other stable source snapshots and records reconciliation totals, generation
  time, provenance, and amendments.
- Population rows may reference control occurrences or externally sourced
  hires, terminations, access changes, production changes, incidents,
  vulnerabilities, vendors, tickets, or other events. Compliance does not need
  to own the operational workflow that created them.
- Each row has a stable identity and an explicit included, excluded, duplicate,
  missing, or unresolved classification. A missing or partial source never
  appears complete.
- A `SampleSelection` records who selected exact frozen population rows and
  when. Supplemental evidence and auditor conclusions do not modify either the
  row or the population used for selection.

## History, snapshots, and time

Every consequential record distinguishes as needed:

- when the action occurred;
- when it became effective;
- which period it covers;
- when the platform observed or imported it;
- which actor or external source supplied it;
- which revision or snapshot a later decision relied on.

A current-state audit log alone is insufficient. Business history lives in
explicit versions, submissions, decisions, effective intervals, amendments,
and snapshots. An activity feed is a derived navigation aid.

A snapshot contains stable references plus enough immutable identity to prove
what was relied on. Later source changes do not alter it. Authorized correction
after a freeze is an amendment linked to the original snapshot and identifies
its downstream impact.

## Cross-story integration rules

- Platform authorization is enforced by the server for every command and query;
  client route guards are only a navigation aid.
- Every work assignment resolves to an active member or team and exposes
  orphaned work after membership changes.
- Every access campaign starts from the governed application inventory and
  accounts for every in-scope reviewed system. Missing or failed source data is
  unknown, never evidence of zero accounts or zero access.
- Every observed access relationship remains distinct from the approved
  expectation and human review decision applied to it.
- Every status and count drills into the records and calculation time behind it.
- Every import or collection has a preview, explicit acceptance, partial-failure
  semantics, provenance, and safe replay behavior.
- Every source is identified as authoritative, corroborating, or discovery-only
  for a stated business question. A collected fact proposes or supports governed
  state; it never silently becomes truth outside its approved authority.
- Missing rows, tombstones, pagination gaps, and failed or stale collection are
  explicit reconciliation states. None can silently delete, merge, retire, or
  de-scope a governed record or prove that a population is complete.
- Every access campaign identifies its application and reviewed-system universe,
  the authoritative-subject and access sources received or missing, the expected
  access baseline, and the actual access paths under review.
- Every externally visible handoff has an explicit content selection, preview,
  authorization check, delivery record, and retained manifest.
- Every audit population states its source universe, rules, completeness proof,
  reconciliation, stable row identities, and frozen selection relationship.
- Every output labels management-authored, auditor-authored, imported, and
  product-calculated material and preserves the source rather than inferring an
  auditor conclusion.
- Evidence and sensitive inventory or workforce fields retain artifact- or
  field-appropriate access through indexes, work queues, counts, exports,
  notifications, packages, and historical views.
- Every consequential decision identifies the exact input revision or snapshot,
  actor, time, rationale, and resulting state.
- Every workflow contributes to the same accountable work experience and
  readiness model without surrendering ownership of its domain state.
- Standalone and split API/worker deployments expose the same business
  behavior, authorization, history, and failure semantics.

## Initial open decisions

These remain product decisions rather than implementation guesses. Each is
tracked as an M0 discovery issue:

| Decision | Tracked in |
| --- | --- |
| Whether one organization needs more than one collaboration workspace | M0-D03 |
| Which built-in access roles are required and which actions each permits | M0-D03 |
| Which small-team self-review exceptions are acceptable and who approves them | M0-D03 |
| Whether IdP group mapping is required for the first release | M0-D03 |
| How invitations work for providers that do not support application-managed invitations | M0-D03 |
| The authoritative workforce source, minimum worker attributes, privacy boundary, and joiner, mover, or leaver observation rules | M0-D06 |
| The minimum system-component, information-asset, classification, and data-flow inventory needed for the first approved boundary and system description | M0-D08 |
| Which customer commitments, system requirements, CUECs, and CSOCs apply and who approves them | M0-D09 |
| The risk scoring or qualitative method, risk appetite, acceptance authority, and material-vendor threshold | M0-D10, M0-D11 |
| The first vendor-assessment evidence set and treatment of SOC report coverage gaps, bridge letters, exceptions, and subservice organizations | M0-D11 |
| Retention, deletion, legal hold, backup, and recovery rules for identity and evidence records | M0-D16, M0-A01, M0-A03 |
| Which external principal and entitlement shapes are required by the first real access-review population | M0-D07 |
| Which source is authoritative for people, employment status, managers, and non-human identity ownership | M0-D06 |
| How detailed initial access expectations must be and whether reusable access profiles emerge from the first real campaigns | M0-D07 |
| How nested groups and provider-specific effective-access calculations should be represented for the first reviewed applications | M0-D07 |
| Whether advisors or auditors use scoped platform membership or a handoff-only workflow | M0-D14, M0-D17 |
| The audit firm's required population definitions, reconciliation fields, sample identifiers, package shape, and representation-letter workflow | M0-D17 |
| Whether an optional Trust Services category requires category-specific workflows beyond the shared control and evidence model | M0-D01 |
| The ownership conflicts and release-wide UI baseline listed under [Unresolved ownership](#unresolved-ownership) | M0-D22, M0-D23, M0-D24 |
| How persistence, snapshots, artifact storage, authorization, projections, and imports realize this model | M0-A01 through M0-A06 |

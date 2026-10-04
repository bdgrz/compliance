# Product documentation

Status: reconciled product baseline, 2026-10-04.

## The product in one paragraph

Compliance gives a small organization one trustworthy operating record for its
SOC 2 program, from readiness through Type I and Type II. Teams define scope,
own and operate controls, maintain policies and evidence, review access,
resolve gaps, and support an examination without rebuilding the story in
spreadsheets and folders. Every feature has a complete manual workflow.
Integrations and optional automations reduce effort. One account can manage
many organizations for itself or clients and delegate management to client
personnel. Self-hosting is free; hosted SaaS charges each organization a fixed
base fee plus selected add-ons for integrations, services, and additional
automations. Later firm capabilities add portfolio work, templates, accepted
professional engagements, and enforced independence.

## Read in this order

| Document | What it establishes |
| --- | --- |
| [Product brief](product-brief.md) | Customer, business thesis, objectives, commercial model, journey, and release boundaries |
| [Business operations](business-operations.md) | Multiple organizations per account, client management delegation, organization billing, add-on acceptance, and commercial readiness decisions |
| [Domain model](domain-model.md) | Process spine, cohesive ownership, relationships, and cross-workflow rules |
| [Canonical entity model](canonical-entity-model.md) | Approved identity, workforce, inventory, access, and provenance vocabulary |
| [Product decisions](decisions/) | Accepted M0 decisions and subsequent bounded product decisions |
| [Backlog](backlog.md) | Story users, outcomes, requirements, owners, dependencies, and acceptance evidence |
| [Delivery cycle](delivery-cycle.md) | Release exits, scheduling, implementation boundaries, and delivery evidence |
| [Triage](triage.md) and [gap analysis](gap-analysis.md) | Dated planning rationale, coverage gaps, and links to follow-up work |
| [Source reference policy](source-reference-policy.md) | Permitted source use, exact editions, and excluded material |

Feature contract documents below refine particular stories. They describe
bounded behavior and remaining scope; their presence is not proof that an
entire story or milestone has shipped.

## Objectives and owning capabilities

The [brief](product-brief.md#product-objectives) defines observable proof for
each objective. The backlog supplies story-level acceptance; cross-cutting
objectives apply to every owning story.

| Objective | Owning capabilities and scope | Acceptance boundary |
| --- | --- | --- |
| O1 — Governed program and scope | R1-01/02 program and boundary; R1-03 criteria; R1-05/06 controls and mappings; R1-07/14 risks and providers; R1-10/11/12 inventories; R1-13 commitments | R1.0–R1.2 records are governed, versioned, attributable, and connected; unresolved scope stays visible |
| O2 — Owned, evidenced work | R1-04 responsibility and membership; R2 control operation/evaluation, evidence, access review, policy communication, findings, advisor feedback, and attention work | Required R2 workflows complete with attributable decisions, governed evidence, and verified remediation |
| O3 — Explainable readiness | R1-08 readiness rules; R2-09 Type I entry and the owning work/gap stories | Exact rule/input versions, missing and stale states, owned gaps, and a management-authored entry decision |
| O4 — Examination and continuity | T1 baselines, requests, description, management sign-off, packages and outcomes; T2 recurring operations and source populations; T3 period close, samples, examination and rollover | Frozen records, completeness support, exact-version decisions, author distinctions, indexed packages, and history-preserving amendments |
| O5 — Complete manual operation | Every story, including BO and F1; optional EN-05 imports and M0-D20 enhancements use the same owning workflows | Primary outcomes remain achievable with no integrations or optional collection automations enabled |
| O6 — Trust and isolation | EN-01 authorization; EN-02 history; EN-03 snapshots; EN-04 decisions; EN-06 artifact storage; R2-11 accountable work; every command, query, projection, notification and handoff | Server-enforced permissions and tenant scope, sensitive-field protection, provenance, and history |
| O7 — Professional firm operations | F1 portfolio, cross-client work, templates, service engagements, collaboration and independence | Authorized client-specific practice assignments, no cross-client disclosure, enforced advisory/attest separation, and required own-attest record delivery |
| O8 — Multiple organizations and delegated management | R1-04 member/role administration, R1-15 creation/routing; BO-01 delegation | Explicit organization-specific grants, preserved records, authorized switching, and accepted handover policy |
| O9 — Organization-level commercial value | Free self-hosted manual platform; BO-02 hosted billing; BO-03 optional add-ons | Paid SaaS onboarding requires approved billing policy and behavior; sale of each add-on requires its offering and owning capability acceptance |

R1's first-client scope is authored through its owning workflows; the client
has no existing readiness material to import (M0-D04). Optional import and
connector work do not block that manual outcome. BO commercial acceptance is
independent of the first-client SOC 2 release; F1 professional scope does not
delay core organization creation or administration.

## Capability contracts

| Capability | Detailed contract documents |
| --- | --- |
| Program setup and boundary | [Setup work](r1-01-setup-work.md), [service revision reads](program-service-revision-reads.md) |
| Criteria and commitments | [Criteria catalog](criteria-catalog-v1.md), [commitment drafts](commitment-drafts-v1.md) |
| Workforce and identity context | [Workforce person](workforce-person-v1.md), [source governance](workforce-source-governance.md) |
| Applications and concrete systems | [Application change preview](application-change-preview-v1.md), [optional application import](application-import-v1.md), [system-instance aggregate](system-instance-aggregate.md), [restricted visibility](decisions/restricted-application-visibility.md) |
| Controls | [Control definition](control-draft-v1.md), [operation](control-operation-v1.md), [evaluation](control-evaluation-v1.md) |
| Risks and provider oversight | [Risk drafts](risk-draft-v1.md), [risk assessment](risk-assessment-v1.md), [provider register](provider-register-v1.md), [provider assurance](provider-assurance-v1.md) |
| Evidence and membership lifecycle | [Evidence intake/recovery](evidence-intake-recovery-v1.md), [member deprovisioning](decisions/member-deprovisioning.md) |
| Recurring reviews, changes, and incidents | [Periodic review rules](decisions/m0-d19-periodic-reviews.md), [compliance change/incident facts](decisions/m0-d21-significant-change.md) |
| Client experience requirements | [Accessibility and browser support](accessibility-and-browser-support.md) |
| Organization management and commercial operations | [Business operations](business-operations.md) |

## Decisions, remaining facts, and delivery status

M0 product and architecture decisions are accepted. Gathering first-client
facts, receiving representative external files, implementing an accepted
contract, or defining commercial policy does not reopen them. When the product
owner changes a decision, update its record, affected contracts, story
acceptance, and linked issue together; retain explicit supersession history.

Use the documents as follows:

- The brief and business-operations document own business objectives and the
  confirmed organization/commercial direction. Accepted decisions own specific
  product rules; the canonical catalog owns names and relationships, and
  architecture decisions own implementation constraints.
- The backlog and feature contracts refine those rules into bounded delivery
  scope. A dated implementation note does not weaken the required product
  behavior or claim acceptance of undelivered scope.
- GitHub issues and the `Compliance — SOC 2 product journey` project own live
  status, dependencies, scheduling, and completion. Counts and plans in triage
  or gap analysis are historical unless explicitly refreshed with a date.
- First-client follow-ups include measures/baselines (#340), workforce source
  facts (#347), auditor formats (#339), Privacy lifecycle coverage (#349), and
  offboarding retention (#346). Their owning decisions and issues define the
  exact next action. Later-client import discovery is #338.
- Implemented defaults awaiting product confirmation are also explicit:
  [control-operation findings](control-operation-v1.md#product-defaults-awaiting-confirmation)
  and [evaluation findings](control-evaluation-v1.md#product-defaults-awaiting-confirmation)
  use severity, deadline, and fallback-owner defaults whose ratification or
  replacement belongs to #274/#279 and the product owner.
- BO-01/02/03 name new documented product scope. Scheduling them requires
  linked delivery issues and the policy decisions in
  [business operations](business-operations.md#readiness-decisions); this
  reconciliation does not create those issues or change live project state.

## Claims and boundaries

Readiness status supports management judgment; it is not an audit opinion or a
guarantee of SOC 2 compliance. Management-authored, auditor-authored, imported,
and product-calculated records remain distinct. The attest boundary is
collaboration and record handoff: workpapers, audit testing documentation, and
report drafts remain in attest software (M0-D27). Privacy lifecycle coverage
requires its additional validated stories before complete Privacy support is
claimed.

The backend owns domain behavior, authorization, HTTP APIs, and machine-safe
MCP contracts. Web and mobile clients own their experiences in separate
repositories. Human acknowledgements, approvals, acceptance, and sign-offs
remain attributed human actions under the delivery contracts. No product
document assigns client implementation to the backend repository.

# SOC 2 product gap analysis

Status: historical product-owner baseline, 2026-09-13, with dated design-review
addenda. Reconciled with accepted product direction on 2026-10-04. Historical
counts and findings explain how the story set arose; they do not report live
delivery, open issues, or current queue positions. The [product brief](product-brief.md),
[accepted decisions](product-brief.md#product-decisions), and
[business operations scope](business-operations.md) govern current direction;
GitHub issues and the project govern delivery state.

This analysis compares the Compliance backlog with the business journey of a
small service organization moving from readiness through SOC 2 Type I and Type
II. It is a product coverage review, not a readiness opinion, control mapping,
or substitute for advice from the readiness consultant or service auditor.

The reference frame used in the September review was the AICPA SOC 2 resource
set: the 2017 Trust
Services Criteria with revised 2022 points of focus, the 2018 Description
Criteria with revised 2022 implementation guidance, the management resources,
and the illustrative report and representation letters listed in the
[AICPA SOC resource library](https://www.aicpa-cima.com/topic/audit-assurance/audit-and-assurance-greater-than-soc-2).
The authoritative guide describes SOC 2 as an assertion-based examination of a
service organization's system description and controls relevant to the selected
Trust Services categories. The product therefore has to preserve management's
records and assertions while keeping the service auditor's tests, results, and
opinion externally authored.

## Executive result

The September backlog already covered the central process spine: scope, criteria,
controls, policies, evidence, application access, readiness, Type I, Type II,
requests, populations, samples, packages, findings, and roll-forward. It does
not need to be replaced.

That review found seven missing user outcomes and seven material refinements:

| Disposition | Gap | Backlog action |
| --- | --- | --- |
| Add | Authoritative workforce context for joiners, movers, leavers, contractors, and accountable NHI owners | R1-11 |
| Add | Governed inventory of material infrastructure, devices, data stores, information sets, and data flows | R1-12 |
| Add | Traceable service commitments, system requirements, CUECs, and customer responsibilities | R1-13 |
| Refine and split | Risk assessment was combined with vendor oversight and too shallow for either job | Refine R1-07; add R1-14 |
| Add | Controlled policy distribution, acknowledgement, and required training completion | R2-10 |
| Add | One accountable work view across all source workflows | R2-11 |
| Add | Evidence access, disclosure, retention, hold, redaction, and disposition | R2-12 |
| Refine | Control review did not define a reproducible design and implementation evaluation | R2-05 |
| Refine | The system-description story stopped at the Type I baseline | T1-02, T2-07, T3-01, T3-05, T3-06 |
| Refine | Audit populations were limited to scheduled control occurrences | T3-02 and T3-03 |
| Refine | Auditor packages did not name the complete management and PBC output contract | T1-05, T1-07, T3-05, T3-06 |
| Refine | Significant-change and incident impact was treated as optional even though Type II history and the system description depend on it | T2-07 promoted to P1 |
| Refine | Automation covered access and evidence but not governed inventory or workforce sources | T2-08, still P2 until source discovery |
| Refine | Milestone exits did not require people, assets, obligations, governed evidence, or explicit management deliverables | All milestone descriptions |

Those changes produced a 49-story baseline at that review point. They did not create separate API, UI,
schema, connector, or worker issues; those remain implementation concerns inside
the owning business story.

## Current product boundaries

The accepted decisions refine the historical findings into the following
business and product rules. Detailed capability acceptance stays in the
backlog and its owning contracts.

| Rule | Observable outcome and owner |
| --- | --- |
| Complete manual operation is the base product | Authorized people can start, enter or attach facts, operate, review, correct, and maintain every primary compliance workflow without an enabled integration or optional collection automation. Every story inherits the [manual-first contract](backlog.md#manual-first-product-contract). Native calculations and workflow rules continue to apply. |
| One account can manage many organizations | Verified users create organizations, manage their own or client programs, and delegate most organization management to client personnel. Each organization remains its own tenant, with explicit administration grants and independent professional engagement assignments ([M0-D25](decisions/m0-d25-client-tenancy.md), BO-01). |
| The organization is the monetization unit | Self-hosting is free. Hosted SaaS charges a fixed base fee per organization plus optional integrations, services, and additional automations. BO-02 defines hosted billing; BO-03 defines add-on management. Client and authorized MSP billing paths serve the same organization. |
| Commercial and program exits are explicit | The SOC 2 journey proves manual program operation. Paid SaaS onboarding additionally requires billing acceptance, and each sold add-on requires its own entitlement and billing acceptance. [Business operations](business-operations.md) records these outcomes without implying a live delivery milestone or queue. |
| Governance reviews use the control workflow | Policy, risk, provider, and management reviews are ordinary recurring control occurrences with frozen supporting versions and attributed sign-off. Requested management actions become findings; separate T2-06/T2-09 product surfaces are superseded ([M0-D19](decisions/m0-d19-periodic-reviews.md)). |
| Attest workpapers remain external | The product supports examination collaboration and received outputs. The firm's tests, workpapers, conclusions, and report drafts stay in its audit software. Closing the attest engagement or offboarding requires the collaboration record package and attributed delivery before its hold can clear ([M0-D27](decisions/m0-d27-attest-scope.md)). |

Remaining evidence collection is named in the accepted records and
[triage validation queue](triage.md#validation-queue-before-p2-work). It refines
customer facts, targets, source shapes, or professional rules; it does not
turn an accepted decision back into unbounded discovery.

## Coverage retained from the existing backlog

The following areas were already expressed as coherent user outcomes and need
no new subsystem:

- continuing program identity and stage transitions;
- versioned system boundary and criteria catalog;
- application inventory and concrete reviewed systems;
- control definitions, versions, mappings, ownership, cadence, performance,
  evidence, review, and exceptions;
- versioned policies and applicability relationships;
- human and NHI access populations, actual-versus-expected decisions,
  remediation, and verification;
- readiness assessment, consultant feedback, and Type I entry decision;
- Type I and Type II snapshots, auditor requests, packages, findings, outcomes,
  and roll-forward;
- direct auditor access as an explicitly unvalidated P2 option rather than a
  dependency on auditor adoption.

## Added product outcomes

### 1. Workforce and accountable identity ownership

Access review cannot be trusted without an authoritative view of who currently
works for or with the organization, when that relationship changed, who manages
the person, and who owns each NHI. This roster remains distinct from Compliance
members and from provider accounts. It supplies context for joiner, mover,
leaver, background-check, training, policy-acknowledgement, and access-control
work without turning Compliance into an HRIS.

### 2. Technology and information inventory

An application list does not describe the complete service system. The scoped
system can also include cloud accounts and subscriptions, infrastructure,
networks, managed endpoints, repositories, data stores, material information
sets, locations, and data flows. Compliance governs the audit-relevant inventory,
ownership, classification, lifecycle, and relationships while source systems
remain authoritative for operational configuration.

### 3. Commitments, requirements, and user responsibilities

Controls and the system description must be grounded in the promises and
requirements the service is intended to meet. The backlog now gives customer
commitments, contractual or policy requirements, CUECs, and relevant CSOCs
stable, versioned identities and relationships instead of burying them in prose.

### 4. Vendor and subservice-organization oversight

Vendor oversight is not merely a field on a risk. The refined backlog covers
the material service, data and system access, owner, due diligence, contractual
requirements, assurance reports and coverage gaps, carve-out or inclusive
treatment, CUECs or CSOCs, review, renewal, termination, exceptions, and
remediation. It does not attempt to become a procurement system.

### 5. Policy communication and workforce assurance

Approval proves which policy was authorized; it does not prove that the target
audience received it, acknowledged it, or completed required training. The new
story records the exact policy or training version, applicable population,
delivery source, completion, exceptions, and evidence. An LMS may continue to
deliver training.

### 6. Accountable work management

Owners need one place to see due, overdue, blocked, returned, and unassigned
work across controls, policies, evidence, access reviews, findings, vendor
reviews, requests, and approvals. The work view is a projection over those
real workflows; it is not a generic task database that can contradict them.

### 7. Evidence governance

Evidence may contain credentials, personal information, customer data, or
security-sensitive details. The product needs explicit access, sharing,
retention, engagement hold, redacted derivative, disposition, and delivery
history. These rules complement immutable evidence identity rather than
permitting silent mutation or deletion.

## Refined audit and reporting contract

Compliance prepares and preserves management's work. It does not issue the SOC
2 report or manufacture the service auditor's conclusions.

### Auditor-facing working outputs

The product must be able to provide, subject to explicit sharing policy:

- a scope and boundary report with included and excluded services, locations,
  people, technology, information, and third parties;
- an application, reviewed-system, technology, information, workforce, vendor,
  and subservice-organization inventory as applicable;
- the criteria-to-control matrix, including exact control versions, rationale,
  owners, frequency, systems, and expected evidence;
- design and implementation evaluation results for Type I readiness;
- the approved system description and its section-completeness report;
- the policy register, effective versions, approvals, acknowledgements,
  training completion, exceptions, and review status;
- the risk register and treatment status;
- control occurrence, access-review, and other source-backed populations with
  inclusion rules, exclusions, reconciliation, provenance, and stable row IDs;
- sample-response bundles tied to exact frozen population rows;
- an evidence index with provenance, covered period, support relationships,
  review state, sensitivity, and authorized artifact access;
- request, response, delivery, exception, remediation, and management-response
  registers;
- significant-change and incident-impact summaries suitable for the audit,
  without disclosing unrelated restricted operational detail;
- deterministic package manifests, content identities, validation results,
  delivery history, and amendment history.

Exports provide a human-readable index and open machine-readable tables where
useful, under the accepted defaults in [M0-D17](decisions/m0-d17-audit-deliverables.md).
The actual audit firm's workbook, naming, sample, and delivery deltas are
confirmed in [#339](https://github.com/bdgrz/compliance/issues/339). External
firms receive packages; direct product access remains a P2 hypothesis rather
than a prerequisite for auditor adoption.

### Management-authored report inputs

For both Type I and Type II, the product must bind management review and approval
to the exact system description, scope, criteria, controls, known exceptions,
and package being represented. It may assemble a reviewable assertion worksheet
or auditor-provided template, but only authorized management can approve it.

The product also retains the final management representation letter supplied by
the audit firm and the signed version with provenance. It must not present
platform-generated wording as an auditor-approved representation.

### Auditor-owned outputs

The product retains the following received, externally authored outputs with
source provenance:

- the independent service auditor's report and opinion;
- auditor-communicated exceptions, observations, and report wording;
- the final representation-letter form requested by the auditor;
- the issued SOC 2 report and any bridge letter.

The auditor's test procedures, test conclusions, workpapers, workpaper reviews,
and report drafts remain in the auditor's dedicated software. Communicated
exceptions retain their received wording in `AuditTestException`; they do not
become an internal test workpaper or a platform-authored audit conclusion
([M0-D27](decisions/m0-d27-attest-scope.md)). The platform retains collaboration
records and freezes the engagement record package for delivery when our
attest engagement closes or its organization offboards. Its attributed
delivery decision into the attest software must exist before the engagement
hold can clear.

This boundary follows the report responsibilities reflected by the AICPA's
[illustrative SOC 2 materials and management representation resources](https://www.aicpa-cima.com/topic/audit-assurance/audit-and-assurance-greater-than-soc-2).

## Generalized population model

A Type II population is not always the list of scheduled control occurrences.
Auditors may request the complete population of hires, terminations, access
changes, production changes, incidents, vulnerabilities, vendors, tickets, or
other events from which they select samples.

The backlog therefore treats an `AuditPopulation` as a frozen, reconciled view
of a defined source universe. Each row keeps a stable source identity,
observation time, inclusion or exclusion result, and links to the relevant
control performance and evidence. A sample binds to that row. The product does
not need to own the operational workflow that produced the row.

## Trust Services category boundary

Security is the common category. Availability, Processing Integrity,
Confidentiality, and Privacy are selected according to the service commitments
and engagement scope described by the AICPA's
[Trust Services Criteria](https://www.aicpa-cima.com/resources/download/2017-trust-services-criteria-with-revised-points-of-focus-2022).

The shared control, evidence, risk, asset, obligation, and population workflows
can support category-specific controls. The product must not, however, imply
that selecting a category makes the organization ready:

- Availability may require service-level commitments, capacity, backup,
  recovery, and resilience controls and evidence.
- Processing Integrity may require complete, valid, accurate, timely, and
  authorized processing controls and source populations.
- Confidentiality may require information classification, retention, and
  disposal commitments and controls.
- Privacy introduces a broader personal-information lifecycle. Privacy is
  selected in the accepted first-engagement scope, so notice, choice and consent, collection,
  use, retention, disclosure, quality, access, and complaint workflows require
  a separate validated backlog before the product claims complete support.
  [#349](https://github.com/bdgrz/compliance/issues/349) owns that backlog and
  gates the Type I package while Privacy remains selected (M0-D01).

Category-specific delivery follows the approved engagement scope and the
validated lifecycle backlog. Selecting a category records intent; it does not
prove that the corresponding product capability or client controls exist.

## Intentional non-products

The gap review does not justify building replacements for:

- an identity provider, HRIS, payroll, LMS, MDM, CMDB, cloud console, source
  control system, vulnerability scanner, SIEM, ITSM, incident-response system,
  procurement platform, or auditor workpaper system;
- the service auditor's testing methodology, workpapers, or opinion;
- broad multi-framework crosswalks, a trust center, security questionnaires,
  marketplace, white-labeling, or autonomous AI decisions.

The original billing exclusion is superseded by the commercial direction
confirmed on 2026-10-04: self-hosting is free, while hosted SaaS is billed per
organization through a fixed base fee plus optional add-ons for integrations,
services, and additional automations. Organization billing may be administered
by the organization or its authorized MSP. See the [commercial model](product-brief.md#commercial-model)
and [BO-02/BO-03](business-operations.md) for hosted billing and add-on
objectives, acceptance, and remaining policies. Commercial delivery timing is
selected in owning GitHub issues; it is not implied by a historical story count.

Compliance lets people record, upload, or link the minimum trustworthy facts from those
systems, govern the compliance decision made from them, and preserve the
resulting evidence and history. Optional integrations later supply equivalent
source observations through the same governed acceptance path.

## Integration discovery after manual operation

Integration discovery observes how each inventory is populated and kept
current through the manual workflow before selecting enhancements. Accepted
[M0-D20](decisions/m0-d20-integration-sources.md) defers connectors in R1/R2
and ranks them from measured effort in #342. This is a source-specific
discovery path, not a prerequisite for manual inventory operation. For every
candidate source we need to learn:

1. which business question the source answers;
2. whether it is authoritative, corroborating, or discovery-only;
3. its stable identifiers, scope boundaries, timestamps, pagination, and
   deletion or tombstone behavior;
4. how complete populations are proven and partial collection is detected;
5. how records match without silently merging identities or systems;
6. how changes become reviewable proposals rather than automatic truth;
7. the least privilege, credential custody, rate-limit, retry, and revocation
   model;
8. required freshness, reconciliation cadence, and accountable owner;
9. which raw snapshots and normalized facts must be retained for audit proof;
10. how the integration makes the complete user-operated workflow easier to
   start, maintain, or operate, and how that workflow remains usable while the
   integration is unavailable or disabled.

T2-08 records this outcome but stays P2 until the first real application list,
workforce roster, access exports, evidence requests, and audit population needs
show which integrations save meaningful work.

## Addendum: backlog design review, 2026-09-14

A design review before business implementation found that the story
coverage above remains sound, but that the backlog could not yet be delivered
as written. The following historical changes retained the then-current
49-story business scope. Accepted M0 records now supply the policies that were
unresolved at that review; the table records the reason those decisions were
created rather than a present request to reopen them.

| Finding | Backlog action |
| --- | --- |
| R1 and R2 milestone exits, R1-08, and R2-09 required risk, provider, and policy-communication records owned by P1 stories | Promoted R1-07, R1-14, and R2-10 to P0. R2-12 stays P1; R1-08, R2-09, and the R2 exit now treat undelivered evidence governance as an explicit, acknowledged gap |
| Half of the P0 stories embedded unresolved validation, so none could meet the definition of ready | Added **M0 - Design and discovery** with 24 product discovery items; each story's validation subtask now incorporates the decision from its M0 item |
| The domain model depends on persistence, snapshot, artifact, authorization, projection, and import behavior that has no recorded architecture | Added six M0 architecture-decision issues (M0-A01 through M0-A06) |
| Shared primitives had no owner or were owned by a story sequenced after its consumers | Added six approved enablers (EN-01 through EN-06) in R1 |
| Several concepts had two owners or none, and assurance vocabulary overlapped | Created M0-D22 and M0-D23; their accepted ownership and vocabulary now govern the [domain model](domain-model.md#ownership-decisions) |
| Every story required accessible UI, but no accessibility target or supported-browser policy was tracked | Added the global M0-D24 readiness gate for every delivery story and first delivery slice |
| The largest P0 stories bundled several independently valuable outcomes, and the P0 sequence contained dependency cycles | Divided seven stories into 29 outcome slices and replaced the sequence with a dependency-ordered one in [triage.md](triage.md#recommended-p0-delivery-sequence) |

At that design-review point the baseline contained 49 user stories, 29 delivery
slices, 6 enablers, and 30 M0 discovery or architecture-decision items. Created-item dependencies are
maintained as GitHub issue relationships and summarized in the
[issue index](backlog.md#github-issue-index). M0-D24 was later created as
issue #136 and linked as a blocker on every delivery story and first delivery
slice.

## Addendum: multi-client firm and tenancy, 2026-09-14

The product direction widened from one organization preparing for SOC 2 to a
small SOC 2 firm that provides advisory and attest services to many clients.
The decisions were: the firm provides both services with independence walls;
firm staff and client users both sign in; client organizations are the only
tenant, with no firm entity; and the first release is tenant-ready while firm
operations follow later.

| Gap | Backlog action |
| --- | --- |
| No tenant boundary, tenant context, or cross-tenant isolation was defined, and the application trusts a single identity provider | Added M0-D25 (tenant boundary, affiliation, firm-owned material, creation authority, vocabulary) and M0-A07 (tenant context, slug routes with `tenant_id` APIs, reserved routes, identity federation); extended EN-01 to tenant isolation; tenant questions added to M0-A01, M0-A03 through M0-A06, M0-D02, M0-D03, M0-D14, and M0-D17 |
| No story created or suspended a client organization, or let a user work in several | Added P0 story R1-15 with concrete slug rules and the sign-in selection flow; R1-01 and R1-04 now depend on it |
| Existing stories assumed one organization | Added tenant requirements or acceptance criteria to R1-01, R1-03, R1-04, R2-08, R2-11, and T1-03 |
| Firm operations were absent: client lifecycle, portfolio, templates, cross-client work, client identity federation, service engagements, and independence | Added milestone **F1 - Multi-client firm operations** with F1-01 through F1-08 (F1-03 remains a P2 hypothesis) |
| A firm that both advises and examines must protect independence, and the product excluded auditor workpapers | Created M0-D26 and M0-D27. Their accepted decisions retain the strict independence wall and support attest collaboration while keeping workpapers external; rule-set ratification is #343 |

The tenancy discovery items were numbered M0-D25 through M0-D27 because M0-D24
was already assigned to the accessibility baseline. At that review point the baseline contained 58
user stories, 29 delivery slices, 6 enablers, and 34 M0 discovery or
architecture-decision issues before the canonical-model review below.

## Addendum: canonical entity model and public-source licensing, 2026-09-14

The identity and inventory review showed that provider terms such as user,
employee, account, group, role, application, reviewed system, server, and device
cannot safely serve as an unqualified shared model. It also showed that a public
standards URL does not by itself grant rights to copy its text, schemas,
taxonomies, or examples into an Apache-2.0 product.

| Gap | Backlog action |
| --- | --- |
| Identity, workforce, access, application, infrastructure, and provenance terms lacked a complete provider-neutral entity and relationship catalog | Created M0-D28 and `canonical-entity-model.md`; the accepted catalog now defines the semantic spine and cardinalities, while aggregate, storage, API, and service designs remain with owning capabilities |
| Existing stories could independently redefine the same source objects | Made M0-D28 a blocker for EN-01, EN-05, R1-04, R1-10, R1-11, R1-12, R1-15, and R2-06 and added canonical-model subtasks to each |
| Issues cited internal prose or standards-body landing pages without versioned public sources or reuse terms | Added `source-reference-policy.md` and made a `Public references` section part of every issue's backlog and readiness contract |
| Public readability was being conflated with Apache-2.0 compatibility | Added an approved-source register, an excluded-source register, required notice handling, and fail-closed exclusion of any source whose intended use is not affirmatively acceptable |

DMTF CIM and Redfish are excluded from the active model pending rights review.
Redfish-Publications has a BSD-3-Clause copyright notice, but the DMTF IPR
statement describes possible RAND patent terms for implementers. Device and
physical-component concepts use RFC 8348 where it applies; other inventory
semantics remain original product decisions. No DMTF schema or derived mapping
is approved for this Apache-2.0 product.

The completed 2026-09-14 design baseline contained 58 user stories, 29 delivery
slices, 6 enablers, and 35 M0 discovery or architecture-decision issues. Later
accepted decisions, delivery children, and business-operation scope are
recorded separately; this count is not an inventory of live open work.

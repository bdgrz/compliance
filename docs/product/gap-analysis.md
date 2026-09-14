# SOC 2 product gap analysis

Status: product-owner baseline, 2026-09-13

This analysis compares the Compliance backlog with the business journey of a
small service organization moving from readiness through SOC 2 Type I and Type
II. It is a product coverage review, not a readiness opinion, control mapping,
or substitute for advice from the readiness consultant or service auditor.

The reference frame is the AICPA's current SOC 2 resource set: the 2017 Trust
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

The existing backlog already covers the central process spine: scope, criteria,
controls, policies, evidence, application access, readiness, Type I, Type II,
requests, populations, samples, packages, findings, and roll-forward. It does
not need to be replaced.

The review found seven missing user outcomes and seven material refinements:

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

These changes produce 49 user stories. They do not create separate API, UI,
schema, connector, or worker issues; those remain implementation concerns inside
the owning business story.

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

Exports should provide a human-readable index and open machine-readable tables
where useful. The audit firm's preferred workbook, portal, naming, and sample
formats remain discovery inputs rather than hard-coded product assumptions.

### Management-authored report inputs

For both Type I and Type II, the product must bind management review and approval
to the exact system description, scope, criteria, controls, known exceptions,
and package being represented. It may assemble a reviewable assertion worksheet
or auditor-provided template, but only authorized management can approve it.

The product also retains the final management representation letter supplied by
the audit firm and the signed version with provenance. It must not present
platform-generated wording as an auditor-approved representation.

### Auditor-owned outputs

The following remain externally authored and are stored or referenced with
source provenance:

- the independent service auditor's report and opinion;
- the auditor's tests of controls and results;
- auditor-authored deviations, observations, and report wording;
- the final representation-letter form requested by the auditor;
- the issued SOC 2 report and any bridge letter.

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
- Privacy introduces a broader personal-information lifecycle. If Privacy is
  selected for the first engagement, notice, choice and consent, collection,
  use, retention, disclosure, quality, access, and complaint workflows require
  a separate validated backlog before the product claims complete support.

No category-specific module will be scheduled until the actual engagement
scope and consultant or auditor expectations establish the need.

## Intentional non-products

The gap review does not justify building replacements for:

- an identity provider, HRIS, payroll, LMS, MDM, CMDB, cloud console, source
  control system, vulnerability scanner, SIEM, ITSM, incident-response system,
  procurement platform, or auditor workpaper system;
- the service auditor's testing methodology, workpapers, or opinion;
- broad multi-framework crosswalks, a trust center, security questionnaires,
  billing, marketplace, white-labeling, or autonomous AI decisions.

Compliance should ingest or link the minimum trustworthy facts from those
systems, govern the compliance decision made from them, and preserve the
resulting evidence and history.

## Next discovery: inventory data integrations

The next product discovery should identify how each inventory is populated and
kept current before choosing connector architecture. For every candidate source
we need to learn:

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
10. which manual import remains available when the integration is unavailable.

T2-08 records this outcome but stays P2 until the first real application list,
workforce roster, access exports, evidence requests, and audit population needs
show which integrations save meaningful work.

## Addendum: backlog design review, 2026-09-14

A design review before any business implementation found that the story
coverage above remains sound, but that the backlog could not yet be delivered
as written. The following changes were made; the 49 user stories are unchanged
in scope.

| Finding | Backlog action |
| --- | --- |
| R1 and R2 milestone exits, R1-08, and R2-09 required risk, provider, and policy-communication records owned by P1 stories | Promoted R1-07, R1-14, and R2-10 to P0. R2-12 stays P1; R1-08, R2-09, and the R2 exit now treat undelivered evidence governance as an explicit, acknowledged gap |
| Half of the P0 stories embedded unresolved validation, so none could meet the definition of ready | Added **M0 - Design and discovery** with 24 product discovery items; each story's validation subtask now incorporates the decision from its M0 item |
| The domain model depends on persistence, snapshot, artifact, authorization, projection, and import behavior that has no recorded architecture | Added six M0 architecture-decision issues (M0-A01 through M0-A06) |
| Shared primitives had no owner or were owned by a story sequenced after its consumers | Added six approved enablers (EN-01 through EN-06) in R1 |
| Several concepts had two owners or none, and assurance vocabulary overlapped | Recorded as open decisions M0-D22 and M0-D23 and listed under [Unresolved ownership](domain-model.md#unresolved-ownership) |
| Every story required accessible UI, but no accessibility target or supported-browser policy was tracked | Added the global M0-D24 readiness gate for every delivery story and first delivery slice |
| The largest P0 stories bundled several independently valuable outcomes, and the P0 sequence contained dependency cycles | Divided seven stories into 29 outcome slices and replaced the sequence with a dependency-ordered one in [triage.md](triage.md#recommended-p0-delivery-sequence) |

The backlog now contains 49 user stories, 29 delivery slices, 6 enablers, and
30 M0 discovery or architecture-decision items. Created-item dependencies are
maintained as GitHub issue relationships and summarized in the
[issue index](backlog.md#github-issue-index); M0-D24 and its global dependency
links remain explicitly pending there.

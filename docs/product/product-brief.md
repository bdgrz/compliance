# Compliance product brief

Status: working draft

The product's controlled vocabulary, identity boundaries, and cross-workflow
relationships are defined in [domain-model.md](domain-model.md). The user-story
delivery source is [backlog.md](backlog.md). The dated product-coverage review
and intentional exclusions are recorded in [gap-analysis.md](gap-analysis.md).

## Product thesis

Compliance gives a founder or operator preparing for a SOC 2 audit one trustworthy workspace to define controls, operate them, manage policies and evidence, complete access reviews, close findings, and answer an auditor without reconstructing the story from spreadsheets and folders.

## First customer

The first customer is a small compliance team preparing its own organization for SOC 2. One person may lead most of the program, but the work also involves control owners, managers, employees, a readiness advisor, and an external auditor.

This is not initially a product for a large enterprise GRC department. It should remain direct enough for a founder or compliance lead while supporting real delegation, review, management approval, and separation of duties.

## Current real-world context

The first customer is already in SOC 2 readiness with consultants and expects to move next into a SOC 2 Type I audit. Controls, policies, evidence, mappings, and findings may already exist in documents, spreadsheets, shared folders, and consultant work products.

The product must therefore be adoptable mid-readiness. It cannot assume a clean start or require the team to recreate completed work. The first proving ground is the live readiness engagement: bring the existing program into Compliance, use it to close readiness gaps with the consultants, and make the Type I entry decision from the resulting record.

## Job to be done

When progressing from SOC 2 readiness through Type I and Type II, help our small team understand what applies, implement and operate owned controls, keep policies and evidence current, perform access reviews, resolve gaps, and support the auditor so that we always know what is ready, what is missing, and why.

## Current workflow hypothesis

The likely alternative is a mixture of spreadsheets, shared drives, policy documents, tickets, identity-provider exports, screenshots, calendar reminders, and email with an advisor or auditor. That workflow tends to lose relationships and history:

- criteria, controls, policies, and evidence drift apart;
- recurring control work is easy to miss;
- evidence lacks clear ownership, collection time, or applicable period;
- access-review decisions and remediation become separate artifacts;
- readiness status is manually reconstructed and difficult to trust;
- auditor requests create repeated searches and duplicate uploads.

These are hypotheses to validate during M0, not facts to encode without review.

## Desired outcomes

The product should let the first customer:

1. answer what is incomplete or at risk without maintaining a parallel spreadsheet;
2. retrieve the support for any control or audit request in minutes;
3. see who owns the next action and when it is due;
4. demonstrate who performed, reviewed, changed, and approved compliance work;
5. maintain an owned inventory of applications and the concrete systems in which access exists;
6. distinguish human and non-human identities (NHIs), compare actual access with approved expectations, and complete an access review through verified remediation;
7. hand an auditor a bounded, indexed, reproducible package;
8. reuse the program for the next period without rewriting its history.

## Product principles

- Evidence over assertion. A green status must be explainable from underlying work and evidence.
- History is part of the product. Corrections create new history rather than silently rewriting completed audit work.
- Drafts are honest. Unvalidated mappings and incomplete materials are visible as drafts, never represented as authoritative.
- The user owns the program. The application supports judgment and review; it does not issue an audit opinion.
- Secure by default. Least privilege, workspace isolation, safe evidence handling, and delegated identity apply to every slice.
- Small-team simple. The default path is direct and usable by one operator, while roles support real collaboration.
- Automation stays observable. Imported or collected evidence retains its source, capture time, status, and failures.
- Framework content is governed. Criteria provenance, version, permitted use, and mapping review are explicit.

## Capability map

The initial product journey is:

1. Define the audit and system boundary.
2. Establish the authoritative workforce context used to evaluate human and NHI ownership.
3. Inventory applications, concrete reviewed systems, material technology, and information assets.
4. Record the service commitments, system requirements, and user responsibilities that shape scope and controls.
5. Load the applicable criteria.
6. Assess risks and evaluate vendors and subservice organizations.
7. Define and map controls.
8. Associate controls and policies with the systems, risks, obligations, assets, and scope they govern.
9. Assign ownership and cadence.
10. Maintain, publish, and evidence governing policies and required workforce acknowledgement or training.
11. Operate and evaluate controls and collect governed evidence.
12. Inventory human and NHI access, groups, roles, memberships, and grant paths.
13. Compare actual access with approved expectations and complete review campaigns and remediation.
14. See accountable work, review evidence, and resolve gaps.
15. Maintain the system description and significant-change record.
16. Support auditor requests, complete populations, samples, management sign-off, and outcomes.
17. Produce an indexed audit package and roll the program forward.
18. Automate repeatable inventory, access, and evidence collection after the manual workflow is trusted.

## Product journey

The platform should carry the same compliance program through three stages without forcing the team to rebuild its records:

1. Readiness: define scope, understand criteria, build the control environment, close design and implementation gaps, and decide when to begin Type I.
2. SOC 2 Type I: demonstrate control design and implementation at the selected point in time, support auditor requests, record the outcome, and turn findings into the Type II operating plan.
3. SOC 2 Type II: operate controls throughout the observation period, preserve complete populations and samples, resolve exceptions, support the examination, and roll the program into the next period.

The control catalog, policies, evidence relationships, people, risks, vendors, and history should carry forward across stages. Each audit engagement gets a stable snapshot of the records that applied to it.

## First-release boundary

The first usable release should let the current small team bring its existing
readiness work into the product, complete the live consultant-led readiness
assessment, and establish its Type I plan using workforce context, application,
technology, and information inventories, commitments and requirements,
controls, policies, evidence, human and NHI access populations, approved access
expectations, review campaigns, risks, vendors, findings, and feedback already
available to the team. Manual entry, file upload, and reviewed bulk import are
acceptable. Provider integrations and broad framework coverage are not required
before the core workflow is trustworthy.

## Explicit early non-goals

- issuing an audit opinion or guaranteeing SOC 2 compliance;
- replacing the external auditor or readiness advisor;
- replacing operational systems such as an HRIS, LMS, CMDB, MDM, ITSM,
  vulnerability scanner, SIEM, procurement system, or audit workpaper system;
- storing passwords or becoming an identity provider;
- supporting every compliance framework in the first release;
- building integrations before the manual workflow and domain model are validated;
- enterprise sales, billing, marketplace, or white-label capabilities;
- prescriptive AI-generated controls or mappings presented without human review.

## Candidate success measures

M0 must establish baselines and targets, but the product should ultimately measure:

- time to identify all missing, stale, rejected, or overdue audit work;
- median time to answer an auditor evidence request;
- percentage of controls with a current owner, cadence, operation, and accepted evidence;
- number and age of overdue control and remediation tasks;
- percentage of access-review decisions with verified completion where action was required;
- number of package validation errors at auditor handoff;
- amount of parallel spreadsheet or drive tracking still required.

## Open product decisions

- readiness, Type I, or Type II as the first engagement;
- Trust Services categories in scope;
- target audit and observation-period dates;
- exact system boundary and subservice-organization treatment;
- the service commitments, system requirements, CUECs, and CSOCs applicable to
  the first engagement;
- source, edition, and permitted use of SOC 2 criteria content;
- single-workspace versus multi-workspace needs for the first release;
- required roles and acceptable self-review exceptions for a small team;
- authoritative sources for the application inventory and human identity roster;
- the first reviewed applications, source export formats, NHI classifications, and effective-access rules;
- the minimum technology, information, data-flow, workforce, and NHI-owner
  inventories needed for the first system description and control evaluation;
- the risk methodology, material-vendor threshold, and minimum vendor-review evidence;
- which prohibited or required access expectations apply to each reviewed system;
- evidence retention, deletion, legal-hold, backup, and recovery expectations;
- accessibility target and supported browsers;
- first cloud provider and first evidence sources to automate;
- how the readiness advisor and auditor want to review work in progress;
- the auditor's required control matrix, population, sample, package, assertion,
  representation-letter, and portal or workbook formats;
- whether optional Availability, Processing Integrity, Confidentiality, or
  Privacy categories are in scope; Privacy requires additional validated
  lifecycle stories before complete product support is claimed.

## Release story

| Milestone | User-visible outcome |
| --- | --- |
| R1 - Readiness program scoped | The team has an agreed boundary; workforce, application, technology, and information inventories; commitments; criteria; controls; risks; vendors; and an owned gap plan. |
| R2 - Control environment implemented | Required controls and policies are implemented and evaluated, policy communication and evidence are governed, actual human and NHI access is reviewed, and accountable gaps support a Type I entry decision. |
| T1 - SOC 2 Type I supported | The team can freeze the point-in-time scope, approve the system description and management representations, answer requests, provide a reproducible handoff, record the result, and create the Type II plan. |
| T2 - SOC 2 Type II period operated | The team operates recurring controls, evidence, access reviews, governance reviews, and significant-change assessment while maintaining complete source populations and the system description. |
| T3 - SOC 2 Type II examination supported | The period is frozen, complete populations and samples are traceable, management and auditor outputs remain distinct, the examination is supported, and the program rolls forward. |

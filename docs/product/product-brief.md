# Compliance product brief

Status: accepted product direction, updated 2026-10-04

Start with the [product documentation guide](README.md) for the objective and
capability map. The product's controlled vocabulary, identity boundaries, and cross-workflow
relationships are defined in [domain-model.md](domain-model.md). The user-story
delivery source is [backlog.md](backlog.md). The dated product-coverage review
and intentional exclusions are recorded in [gap-analysis.md](gap-analysis.md).

## Business overview

**Business thesis:** build software that reduces the coordination and
reconstruction effort involved in running a small organization's SOC 2 program.
The initial operating customer is a small organization already preparing for
Type I. The strategic expansion is to let a small SOC 2 firm run multiple
client programs in the same product after the single-client workflow proves its
value. The business earns trust by giving teams one explainable operating record
that remains useful without integrations or optional collection automation.
The organization is the unit of value and, in the hosted SaaS offering, the
unit of monetization. One platform account can create and manage many
organizations for its own use, on behalf of clients, or with most organization
management delegated to client personnel.

| Business question | Current direction | Status |
| --- | --- | --- |
| Who uses it first? | A small organization's compliance lead, with control owners, management, a readiness advisor, and an external auditor participating in the work. | Current first-customer profile. |
| What costly problem do we address? | Scope, control work, evidence, access reviews, decisions, and audit requests are spread across documents, spreadsheets, shared drives, tickets, and email; teams repeatedly reconstruct status and support. | Workflow pain is a product hypothesis to validate with the first engagement. |
| What value should the customer receive? | Less coordination and duplicate effort; clearer ownership and next actions; faster retrieval of support; and a traceable record for management decisions and auditor handoff. | Intended value; numeric targets are not set. |
| How does the business expand? | Prove the full program workflow for one client organization, then add client portfolio, service engagement, templates, and independence capabilities for a small SOC 2 firm. | Product sequence is defined through F1; the client-organization tenant model is defined in M0-D25. |
| How is it monetized? | Self-hosting is free. Hosted SaaS is billed per organization through a fixed platform base fee plus optional add-ons for integrations, services, and additional automations. | Confirmed commercial model, 2026-10-04; fee amounts and billing intervals remain to be defined. |
| Who manages an organization? | One account can create and manage multiple organizations for itself or clients, or hand most day-to-day organization management to the client. | Confirmed account and organization management model, 2026-10-04. |
| Who sets up and manages billing? | An organization can set itself up and manage its billing. In an MSP scenario, the firm sets up the client organization and configures its billing; the client organization can also manage its own billing. | Both administration paths preserve the organization as the monetization unit. |

The current strategic wedge is the client program, not a general-purpose
enterprise GRC platform or an audit opinion product. The future firm workflow
is a way to serve more client programs and preserve service independence; it
does not make client organizations share a data boundary.

The business outcomes will be evaluated using the first-client measures in
[M0-D18](decisions/m0-d18-success-measures.md): time to act on attention-state
work, evidence-request response time, parallel spreadsheet or drive tracking,
and overdue work. Baselines and targets belong to follow-up #340.

## Organization management model

A platform account can create, belong to, and manage many organizations. An
organization may represent the account holder's own business or a client they
serve. The account holder can manage the organization directly, manage it on
the client's behalf, or hand most day-to-day management to client personnel.
Client management and continued provider involvement can coexist through the
responsibilities granted within that organization.

Each organization keeps its own compliance program, records, administration,
and hosted billing configuration as management responsibilities change.
Organization administration and billing administration can be handled by the
client, its MSP, or through delegated responsibilities between them. The
organization remains the unit of value, isolation, and SaaS monetization.

This account model underpins the product. F1 adds portfolio, template, service
engagement, and independence capabilities to make a firm's multi-client work
easier; individual organization creation and delegation are part of the core
organization model.

Organization administration is granted explicitly within each organization.
Creating or administering a client's organization does not grant Advisor or
Attest access. Those professional practice roles require accepted service
engagements and eligible assignments. See [business operations](business-operations.md)
for the administration, delegation, billing, and add-on acceptance requirements.

## Commercial model

The organization is where compliance work creates value and where hosted
monetization happens. A person or firm managing several organizations works
with separate organizations, each with its own billing configuration and
selected add-ons. Billing can be managed on the client's behalf or delegated to
the client along with its other organization management responsibilities.

| Offering | Pricing model | Setup and billing administration |
| --- | --- | --- |
| Self-hosted | Free. | The organization or its MSP operates its deployment. |
| Hosted SaaS, direct organization | Fixed platform base fee per organization, plus selected add-ons. | The organization sets itself up and manages its billing. |
| Hosted SaaS, organization managed for a client | The same organization-level base fee and add-on model. | The firm or account holder sets up the organization and configures its billing; client personnel can take on most organization management and manage billing themselves. |

The base platform provides the complete manually operated compliance workflow.
Add-ons for integrations, services, and additional automations make the program
easier to start, maintain, and operate. Every core feature remains usable when
no integration or optional automation is enabled.

The commercial structure is decided. Fee amounts, billing intervals, and the
specific contents and prices of add-ons remain to be defined. Payment,
cancellation, and service-restriction policies also require decisions before
paid hosted onboarding. [BO-02 and BO-03](business-operations.md#delivery-scope)
define that delivery scope; they do not change the first-client manual SOC 2
release gate.

## Product thesis

Compliance gives a founder or operator preparing for a SOC 2 audit one trustworthy workspace to define controls, operate them, manage policies and evidence, complete access reviews, close findings, and answer an auditor without reconstructing the story from spreadsheets and folders.

The same platform lets a small SOC 2 firm run that workspace for many clients: each client organization is an isolated tenant, firm staff and client personnel collaborate inside it, and the firm can later manage its whole client portfolio from one place.

## Product objectives

**North-star outcome:** a small compliance team can carry one SOC 2 program
from readiness through Type I and Type II in a single, trustworthy record,
using complete user-operated workflows even when no external integrations or
optional collection automations are enabled. The team can explain what is in
scope, what work is complete, what remains, who owns the next action, and why.
The product supports management's decisions; it does not declare compliance or
issue an auditor's opinion.

| ID | Objective | Observable proof | Delivery horizon |
| --- | --- | --- | --- |
| O1 | Define a governed program and an explainable system scope. | The team can approve a versioned boundary and maintain the workforce, application, technology, information, commitment, criteria, risk, and provider records needed to describe it. Unknown or unresolved facts stay visible. | R1.0 and R1.1 establish the records; R1.2 connects them to readiness. |
| O2 | Turn applicable requirements into owned, evidenced work. | Controls have accountable owners and cadence; performances, evidence, reviews, access decisions, findings, and remediation retain their source, actor, status, and history. | R2. |
| O3 | Make readiness an explainable management decision. | An as-of assessment identifies its rule version and source records, exposes missing or stale inputs, and turns gaps into owned actions. Management records the Type I entry decision separately from any auditor conclusion. | R1.2 and R2. |
| O4 | Support examinations and carry the program forward without rebuilding its history. | Type I and Type II scope, populations, samples, requests, evidence, packages, management sign-offs, auditor-authored outcomes, and amendments bind to exact versions and remain distinguishable by author. | T1, T2, and T3. |
| O5 | Make every feature fully operable by people without external integrations or optional collection automations. | Authorized users can set up, enter or attach information, operate, review, resolve exceptions, and maintain each workflow through the product. No primary outcome requires a connected source, import job, or collection runtime. Optional automation can make these tasks easier to start, maintain, and operate, while native validation, calculations, projections, and workflow rules continue to work. | Applies to every milestone. |
| O6 | Protect the trust boundary around client work. | Every client record is tenant-scoped, authorization is enforced by the server, and changes, evidence, decisions, and derived status retain attribution and provenance. No client, count, or restricted detail crosses an unauthorized boundary. | Applies to every milestone. |
| O7 | Let a small SOC 2 firm serve multiple clients without cross-client exposure or compromised independence. | Firm staff work through client-specific assignments, see only their authorized portfolio, apply versioned templates with provenance, and encounter enforced advisory/attest separation. | F1, after the client workflow is proven. |
| O8 | Let one account create and manage organizations for itself or clients, and delegate management to client personnel. | Each organization retains its records and history while explicit administration grants change; an account can switch among its authorized organizations without gaining access to others. Core administration grants do not create professional practice access. | R1-04 and R1-15 establish membership and creation; BO-01 completes delegated management. |
| O9 | Deliver a free self-hosted platform and monetize hosted value at the organization. | Self-hosted use supports the complete manual program. Each hosted organization has a fixed base subscription and selected optional add-ons, with authorized client or provider billing administration and inspectable charges. | BO-02 before paid hosted onboarding; BO-03 before selling the respective add-ons. |

These objectives are product outcomes, not a second delivery board. The
[backlog](backlog.md) defines the story-level user, requirements, and
acceptance evidence; the [delivery cycle](delivery-cycle.md) defines milestone
exits and execution rules, while GitHub holds live Run order. An objective is
achieved only when its mapped owning capabilities and stories have passed
their acceptance evidence. Feature counts or readiness percentages alone do
not prove that outcome.

## First customer

The first customer is a small compliance team preparing its own organization for SOC 2. One person may lead most of the program, but the work also involves control owners, managers, employees, a readiness advisor, and an external auditor.

This is not initially a product for a large enterprise GRC department. It should remain direct enough for a founder or compliance lead while supporting real delegation, review, management approval, and separation of duties.

## Firm operating model

The platform is designed for a small SOC 2 firm that provides two kinds of service:

- advisory services, such as readiness assessments, control design and implementation support, continuous compliance, and vCISO work, where each client's SOC 2 examination is performed by an external audit firm or, subject to independence rules, by our attest practice;
- attest services, in which our own attest team examines a client's SOC 2 system description and controls.

Because the firm provides both, independence walls are a product requirement. The platform must record which services the firm performs for each client, prevent attest work where advisory services impaired independence, and keep advisory and attest teams and material appropriately separated. The governing rules require professional review (M0-D26). M0-D27 sets the attest boundary: requests, responses, populations, samples, evidence, and packages live here; audit workpapers, testing documentation, and report drafts remain in the firm's attest software. Closing or offboarding an own-attest engagement requires its record package and attributed delivery into that software, with the engagement hold retained until delivery.

Each client organization is the only tenant; there is no separate firm entity. Firm staff are platform users with memberships in the client organizations they serve, and client personnel sign in to their own organization. What lives outside a tenant, such as templates and the firm-staff directory, is defined in M0-D25.

The first release is tenant-ready rather than firm-complete. Tenant isolation, client-organization provisioning, and slug-based organization routes (M0-D25, M0-A07, EN-01, and R1-15) are required now. Firm operations, including the client portfolio, templates, cross-client work, client identity federation, service engagements, and independence enforcement, are planned in F1.

## Current real-world context

The first customer is a small client organization already in SOC 2 readiness
with consultants and expects to move next into a SOC 2 Type I audit. The
accepted [M0-D04 decision](decisions/m0-d04-readiness-material.md) confirms
that this client has no existing readiness material to import. Its records
will be authored through their owning workflows in Compliance. Supporting a
later client that brings existing records is a separate, deferred outcome.

Under the firm operating model, that live engagement is the first client
organization served by the platform.

The product must therefore be adoptable mid-readiness: it must let a team
record its current program state, work, and available evidence without
assuming that compliance work has not started. For the first client, those
records are entered through the normal product workflows; later import or
connection features may reduce that effort when a client brings existing
material. The first proving ground is the live readiness engagement: use the
record in Compliance to close readiness gaps with the consultants and make the
Type I entry decision.

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

These remain workflow hypotheses to validate through first-client use. M0's
design decisions are accepted; baseline measurement and engagement-specific
facts remain follow-up work rather than reasons to reopen those decisions.

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
- Secure by default. Least privilege, tenant isolation, safe evidence handling, and delegated identity apply to every slice. No client's records, counts, or existence are disclosed to another client.
- Small-team simple. The default path is direct and usable by one operator, while roles support real collaboration.
- Manual operation is first-class. Every feature can be started, operated, reviewed, and maintained through explicit user actions without an external integration or optional source-collection automation. This path follows the same authority, validation, authorization, provenance, and history rules as every other path.
- Integrations and automation are enhancements. They reduce the work of starting, maintaining, or operating a program; they do not gate a feature or replace its governed user workflow. Imported or collected material retains its source, capture time, status, and failures and enters the same review path as manually supplied material.
- Native product behavior remains available. Validation, calculations, projections, workflow transitions, and other behavior implemented by Compliance do not depend on external integrations or optional collection automations.
- Framework content is governed. Criteria provenance, version, permitted use, and mapping review are explicit.

## Capability map

The initial product journey is:

0. Create an organization, invite its personnel, and explicitly grant or delegate management; one account may repeat this for its own organizations and clients. Professional practice participation is added through accepted engagements in F1.
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
18. Set up hosted organization billing and select optional integrations, services, and additional automations that make program setup, maintenance, and operation easier. The complete manual workflow remains available without add-ons; self-hosted use is free.

For the firm, the later capabilities are:

19. Onboard, suspend, and offboard client organizations with governed export and retention.
20. See and work the client portfolio across organizations without cross-client disclosure.
21. Apply the firm's templates to clients with provenance.
22. Record every client service as an accepted advisory or attest engagement and enforce independence walls.

## Product journey

The platform should carry the same compliance program through three stages without forcing the team to rebuild its records:

1. Readiness: define scope, understand criteria, build the control environment, close design and implementation gaps, and decide when to begin Type I.
2. SOC 2 Type I: demonstrate control design and implementation at the selected point in time, support auditor requests, record the outcome, and turn findings into the Type II operating plan.
3. SOC 2 Type II: operate controls throughout the observation period, preserve complete populations and samples, resolve exceptions, support the examination, and roll the program into the next period.

The control catalog, policies, evidence relationships, people, risks, vendors, and history should carry forward across stages. Each audit engagement gets a stable snapshot of the records that applied to it.

## First-release boundary

The first usable release should let the current small team complete the
consultant-led readiness workflow and establish its Type I plan using workforce
context, application, technology, and information inventories, commitments and
requirements, controls, policies, evidence, human and NHI access populations,
approved access expectations, review campaigns, risks, vendors, findings, and
feedback. The first client authors governed records in their owning workflows
and attaches evidence through the product's evidence workflow; bulk import is
not needed for this client. Import and connector work wait for a later client
to provide representative source material and for its owning backlog slice to
be scheduled (M0-D04, M0-D20). Provider integrations and broad framework
coverage do not precede a trustworthy manual workflow.

The release runs inside an isolated client organization reached through its
slug-based routes. More than one client organization may exist, but firm
portfolio, templates, cross-client work, client identity federation, and
independence enforcement are not required until F1.

## Explicit early non-goals

- generating an audit opinion from product data, deriving attest conclusions from readiness status, or guaranteeing SOC 2 compliance;
- replacing an advisory client's external audit firm or the firm's attest workpaper software; the product provides governed collaboration and record handoff (M0-D27);
- replacing operational systems such as an HRIS, LMS, CMDB, MDM, ITSM,
  vulnerability scanner, SIEM, procurement system, or audit workpaper system;
- storing passwords or becoming an identity provider;
- supporting every compliance framework in the first release;
- building integrations before the manual workflow and domain model are validated;
- enterprise sales, marketplace, or white-label capabilities;
- separate firm-level tenant or subscription boundaries; the organization is the tenant and hosted monetization unit;
- prescriptive AI-generated controls or mappings presented without human review.

## Success measures and release evidence

The first-client measures are defined in [M0-D18](decisions/m0-d18-success-measures.md):
time to act on missing, stale, rejected, or overdue work; evidence-request
response time; parallel spreadsheet or drive tracking; and the overdue work
backlog. Instrumentation is based on attributed domain activity, not page
views. The first 30-day baseline and numeric targets belong to follow-up #340;
targets must not be invented in feature stories.

Each milestone also has its own acceptance evidence in the backlog. For
example, R2 proves owned control work and verified access remediation, T1 and
T3 prove validated examination packages, and F1 proves client portfolio and
independence behavior. These are capability gates, not global telemetry goals.
Additional measures such as control completeness, package validation errors,
client onboarding time, and prevented independence conflicts remain candidates
for their owning milestone; adopt them only with a precise definition, data
owner, collection point, and review purpose.

## Product decisions

The M0 discovery decisions behind this brief are recorded under
[`decisions/`](decisions/) and
[`../architecture/decisions/`](../architecture/decisions/). A story that
depends on a decision implements its recorded data-shape rules; a change to a
decision updates its record, the affected stories, and the issue.

| Question | Decided in |
| --- | --- |
| First engagement type, Trust Services categories in scope, target audit and observation-period dates, and optional categories (Privacy requires additional validated lifecycle stories before complete support is claimed) | [M0-D01](decisions/m0-d01-program-targets.md) |
| System boundary and subservice-organization treatment | [M0-D01](decisions/m0-d01-program-targets.md), [M0-D11](decisions/m0-d11-vendors.md) |
| Service commitments, system requirements, CUECs, and CSOCs for the first engagement | [M0-D09](decisions/m0-d09-commitments.md) |
| Source, edition, and permitted use of SOC 2 criteria content | [M0-D02](decisions/m0-d02-criteria-content.md) |
| Client organizations per deployment and workspaces per client | [M0-D03](decisions/m0-d03-roles-and-separation-of-duties.md), [M0-D25](decisions/m0-d25-client-tenancy.md) |
| Required roles and acceptable self-review exceptions for a small team | [M0-D03](decisions/m0-d03-roles-and-separation-of-duties.md) |
| Authoritative sources for the application inventory and human identity roster | [M0-D05](decisions/m0-d05-application-inventory.md), [M0-D06](decisions/m0-d06-workforce-source.md) |
| First reviewed applications, source export formats, NHI classifications, access expectations, and effective-access rules | [M0-D07](decisions/m0-d07-access-review-population.md) |
| Minimum technology, information, data-flow, workforce, and NHI-owner inventories | [M0-D08](decisions/m0-d08-technology-inventory.md), [M0-D06](decisions/m0-d06-workforce-source.md) |
| Risk methodology, material-vendor threshold, and minimum vendor-review evidence | [M0-D10](decisions/m0-d10-risk-method.md), [M0-D11](decisions/m0-d11-vendors.md) |
| Evidence retention, deletion, legal hold, and artifact recovery | [M0-D16](decisions/m0-d16-evidence-handling.md), [ADR 0006](../architecture/decisions/0006-artifact-content-storage-spike.md); platform recovery in [ADR 0003](../architecture/decisions/0003-event-sourced-history-and-effective-versions.md) |
| First cloud provider and evidence sources to automate | [M0-D20](decisions/m0-d20-integration-sources.md) |
| How the readiness advisor and auditor review work in progress | [M0-D14](decisions/m0-d14-advisor-collaboration.md), [M0-D17](decisions/m0-d17-audit-deliverables.md) |
| The auditor's control matrix, population, sample, package, assertion, representation-letter, and portal or workbook formats | [M0-D17](decisions/m0-d17-audit-deliverables.md) |
| Tenant boundary, firm-staff affiliation, firm-owned material, organization creation, and tenant vocabulary | [M0-D25](decisions/m0-d25-client-tenancy.md) |
| Independence rules for a firm that provides advisory and attest services | [M0-D26](decisions/m0-d26-independence.md) |
| Attest collaboration and required record delivery; workpapers, testing documentation, and report drafts remain external | [M0-D27](decisions/m0-d27-attest-scope.md) |
| Free self-hosting, organization-level hosted base fee and optional add-ons, and delegated organization and billing administration | [Business operations](business-operations.md); confirmed product direction, 2026-10-04 |
| Tenant resolution, slug and `tenant_id` routing, reserved routes, and client identity federation | [ADR 0009](../architecture/decisions/0009-tenant-identity-federation-and-context.md) |

## Release story

| Milestone | User-visible outcome |
| --- | --- |
| M0 - Design and discovery | Every product and architecture decision blocking the first release is recorded, shared domain ownership is unambiguous, the canonical entity model is approved from usable public references, excluded sources contribute no product information, and blocked stories are refined. No business behavior ships in this milestone. |
| R1 - Readiness program scoped | The team has an agreed boundary; workforce, application, technology, and information inventories; commitments; criteria; controls; risks; vendors; and an owned gap plan. |
| R2 - Control environment implemented | Required controls and policies are implemented and evaluated, policies are communicated and acknowledged, governed evidence is captured with provenance, actual human and NHI access is reviewed, and accountable gaps support a Type I entry decision. Undelivered evidence governance remains a delivery gap and does not count as completion of R2. |
| T1 - SOC 2 Type I supported | The team can freeze the point-in-time scope, approve the system description and management representations, answer requests, provide a reproducible handoff, record the result, and create the Type II plan. |
| T2 - SOC 2 Type II period operated | The team operates recurring controls, evidence, access reviews, governance reviews, and significant-change assessment while maintaining complete source populations and the system description. |
| T3 - SOC 2 Type II examination supported | The period is frozen, complete populations and samples are traceable, management and auditor outputs remain distinct, the examination is supported, and the program rolls forward. |
| F1 - Multi-client firm operations | The firm onboards and offboards clients, works its client portfolio and cross-client queue, applies templates with provenance, lets client users sign in through their own identity providers, and enforces independence between advisory and attest engagements. |

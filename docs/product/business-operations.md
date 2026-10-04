# Organization management and commercial operations

Status: confirmed business direction, 2026-10-04. Product owner: Jeff Repanich.
The acceptance requirements below define delivery scope; they are not claims
that subscription, delegation, or add-on behavior is implemented.

## Business outcome

The organization is the unit of customer value, data isolation, and hosted
monetization. One account can create and manage many organizations for itself
or clients, and delegate most management to client personnel. An organization
keeps its program and history as management responsibilities change.

Self-hosting is free. Hosted SaaS has a fixed platform base fee per organization,
plus optional add-ons for integrations, services, and additional automations.
The base platform provides the complete manually operated compliance workflow.
Enabling an integration or optional automation makes that work easier to start,
maintain, or operate; it is not a prerequisite for any core feature.

## Confirmed rules

| Area | Product rule |
| --- | --- |
| Account and organizations | A platform account can create, belong to, and manage multiple organizations. Each action resolves to the organization the actor is authorized to administer. |
| Direct setup | An organization can create its own workspace and configure its hosted billing. The creator receives the initial administration authority defined by M0-D25. |
| Client setup | A firm or account holder can create an organization for a client, configure its hosted billing, and invite client personnel. The client can take on most management, including billing administration. |
| Delegation | Administrative responsibilities are granted explicitly within the organization. Client and provider involvement can coexist; transferring responsibilities preserves organization identity, records, and history. |
| Billing unit | Each hosted organization has its own base subscription, billing configuration, and selected add-ons. Managing several organizations does not create a firm subscription or merge their charges. |
| Manual baseline | No integration, import job, optional collection runtime, or purchased add-on is required to perform the core compliance workflows. Native validation, calculations, projections, security controls, and workflow rules remain available. |
| Professional access | Organization administration does not grant Advisor or Attest access. Those roles require accepted service engagements and eligible assignments under M0-D25 and M0-D26. |
| Platform operations | Operator authority covers the tenant lifecycle, administrator roster, and usage metadata. It does not grant access to client business records. |

Billing administration identifies who may manage the organization's billing.
It does not by itself settle who legally pays, whether an MSP consolidates
payment, or which payment instruments are supported. Those are readiness
decisions below. A paid service add-on does not imply acceptance of a
professional engagement or eligibility to perform attest work.

## Delivery scope

BO is a product scope grouping in the [backlog](backlog.md), not a newly created
GitHub milestone or an instruction to interrupt the current delivery queue.
The stories support objectives O8 and O9 in the [product brief](product-brief.md).
Current implementation status and scheduling belong to GitHub issues and the
product journey project.

### BO-01 — Delegate organization management to client personnel

**User:** an account holder or provider administering an organization for a client.

**Outcome:** the client can take on most day-to-day organization management,
with any continued provider involvement explicitly authorized.

**Prerequisites:** R1-04 membership and role administration, R1-15 organization
creation and routing, EN-01 authorization, and the handover policy decisions
below. F1 portfolio and professional practice workflows are separate outcomes.

**Acceptance evidence:**

- The same account can create and administer more than one organization and
  select only organizations to which it has current access.
- Authorized administration can invite client personnel and assign or revoke
  administration for a specific organization. The client can perform the
  delegated management through its own account.
- The handover records the organization, actor, recipients, grants, effective
  time, and the consent or acceptance required by the approved policy.
- The organization's program, immutable identity, evidence, billing
  configuration, and historical attribution persist through handover.
- Provider access after handover matches the explicit remaining grants;
  revocation ends the corresponding access without removing earlier authorship.
- Administration cannot implicitly create Advisor/Attest access or bypass
  independence, tenant isolation, or platform-operator boundaries.
- The workflow requires no external integration or optional automation.

### BO-02 — Set up and administer an organization's hosted billing

**User:** an authorized organization or provider billing administrator.

**Outcome:** a hosted organization has an understandable base subscription and
can manage its billing through direct or client-managed administration.

**Prerequisites:** organization identity and authorization, delegation where
used, and approved pricing, payer, payment, and subscription lifecycle policies.
This story gates claiming paid SaaS onboarding; it does not gate the current
first-client manual SOC 2 release.

**Acceptance evidence:**

- Direct setup and setup on a client's behalf attach billing to the chosen
  organization, with the fixed base fee, approved billing interval, applicable
  selected add-ons, and responsible billing contacts visible before commitment.
- Billing administration is explicit and can be assigned to client personnel
  or an authorized provider. It does not grant compliance-record access.
- An administrator can inspect the organization's subscription state, charges,
  invoices or equivalent charge records, and payment status under the approved
  commercial policy. Billing from organizations the actor is not authorized
  to administer is inaccessible; each selected organization remains a separate
  billing context.
- Billing changes preserve the actor, effective date, approved price or terms
  version, and resulting state. Retries cannot produce duplicate subscriptions
  or charges for the same accepted action.
- Payment failure, cancellation, renewal, service restrictions, and recovery
  follow approved policy with clear customer-visible state and history.
  Billing changes never silently delete compliance records or rewrite history.
- Self-hosted operation has no platform subscription fee and supports the
  complete manual compliance workflow.

### BO-03 — Select and manage organization add-ons

**User:** an authorized administrator selecting an integration, service, or
additional automation for one organization.

**Outcome:** the organization can choose enhancements with clear price,
capability, authorization, and operational consequences.

**Prerequisites:** BO-02 for paid hosted add-ons, an approved offering and price,
and the owning capability's source, security, failure, and manual-path contracts.
Each add-on is releasable only when its own delivery scope is complete.

**Acceptance evidence:**

- An authorized administrator can inspect what an add-on supplies, its price
  and billing effect, its prerequisites, and who may configure or operate it
  before enabling it for a specific organization.
- Selection and effective activation are separate inspectable facts where
  configuration or service acceptance remains incomplete. Purchase alone does
  not claim that a connection is healthy or that professional work is accepted.
- Changes retain actor, organization, selected offering/version, effective
  date, and charge or entitlement consequences under approved policy.
- Integration or automation results enter the same governed review and
  provenance path as manually supplied information; incomplete or failed
  collection does not claim a complete population or overwrite source authority.
- Disabling an enhancement preserves governed records and historical
  provenance. People can continue through the complete manual workflow without
  fabricated collections, actors, evidence, or review decisions.
- A service add-on requiring professional work follows engagement acceptance,
  independence, and collaboration boundaries before granting practice access.

## Readiness decisions

The business structure above is settled. These policies need explicit product
decisions before the dependent story can be accepted. They are not new M0
discovery blockers for the manual compliance journey.

| Decision needed | Accountable owner | Required before | Reviewable result |
| --- | --- | --- | --- |
| Handover consent, acceptance, residual provider privileges, and safeguards when the last organization administrator would lose access | Product owner, with authorization/operations review | BO-01 completion | Approved grant and handover state transitions, including recovery and denied-action cases |
| Billing administration permissions and whether they are independent from organization administration | Product owner, with authorization review | BO-02 completion | Permission matrix for setup, billing read, billing change, delegation, and revocation |
| Base fee amount, currency, billing interval, and applicable commercial terms | Product owner, with commercial review | Paid hosted onboarding | Versioned offering and customer-visible price/terms |
| Payer responsibility, payment methods, invoicing, taxes, and whether an MSP can consolidate payment across separate organization subscriptions | Product owner, with finance review | BO-02 completion | Direct and provider-managed payer flows with organization attribution |
| Renewal, cancellation, refunds/proration, nonpayment restrictions, recovery, and retained access/export | Product owner, with finance/operations review | BO-02 completion | Approved subscription lifecycle and data-preservation rules; distinguish billing restrictions from operator-controlled tenant lifecycle |
| Add-on catalog, pricing, entitlements, dependency rules, activation/cancellation effects, and service commitments | Product owner and the owning capability lead | Sale of each add-on | Versioned offering, accepted capability contract, and customer-visible activation/failure behavior |

Numeric acquisition, onboarding, commercial conversion, or retention goals are
not yet adopted. Define a measure, owner, collection point, and target when the
commercial scope is scheduled. Compliance-program outcome measures remain in
[M0-D18](decisions/m0-d18-success-measures.md), with first-client baselines and
targets in #340.

## Related product contracts

- [Product brief](product-brief.md): business thesis and objectives O1–O9.
- [M0-D03](decisions/m0-d03-roles-and-separation-of-duties.md): client roles,
  responsibility, and separation of duties.
- [M0-D25](decisions/m0-d25-client-tenancy.md): tenant, membership, operator,
  and professional engagement boundaries.
- [M0-D26](decisions/m0-d26-independence.md) and
  [M0-D27](decisions/m0-d27-attest-scope.md): professional independence,
  collaboration, and required attest record delivery.
- [Manual-first product contract](backlog.md#manual-first-product-contract):
  feature-wide acceptance requirements.

**Public references:** No external normative source; product decision confirmed
by the product owner on 2026-10-04. The account-management analogy describes
the desired experience; it imports no external platform's terms or permissions.

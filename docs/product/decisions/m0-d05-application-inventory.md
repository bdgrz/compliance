# M0-D05: Application inventory and reviewed-system boundaries

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.

| Question | Decision and rationale |
| --- | --- |
| Application universe and source | The governed application inventory in Compliance is authoritative for scope. Any external list (IdP app catalog, spreadsheet, SaaS-spend tool) is a corroborating or discovery-only source. The first client's actual source and list are gathered in [#352](https://github.com/bdgrz/compliance/issues/352). No import or connector is built now (product direction, 2026-09-22). |
| Minimum fields and ownership | Every active application has a governed name, application kind, lifecycle, criticality, a **system owner**, and an **access owner**. Owners are workforce `Person` references (they need not be platform members, per M0-D03). |
| Reviewed-system boundary | Access is reviewed per `SystemInstance`, never per application. The first instance kinds are `aws_account`, `github_organization`, `identity_provider_tenant`, and `saas_tenant` (one per workspace or site, for example Slack and Jira). |
| Inclusion and exclusion | An instance is in access-review scope when it is inside the approved boundary and grants access to in-scope data, production infrastructure, source code, or identity administration. Exclusion requires a reason. The scope decision is approved by the Compliance Lead, and the approver may not be the instance's access owner (SoD per M0-D03). |
| Alias, duplicate, retirement | An application has one canonical name and 0..* aliases unique within the tenant. Duplicates are resolved by an attributed merge that retires one record with `merged_into`. Retirement is effective-dated, requires a reason, and never deletes history or a snapshot reference. |

## Data-shape rules

- `Application` 1 — 0..* `SystemInstance`; each `SystemInstance` belongs to exactly one application and one tenant.
- `SystemInstance.instance_kind` ∈ `aws_account | github_organization | identity_provider_tenant | saas_tenant | environment | other`, and `other` requires a description.
- `SystemInstance` provider identifiers (for example an AWS account ID or a GitHub org login) are unique per tenant and kind among active instances.
- `AccessReviewScopeDecision`: `system_instance_id` (exact version), `decision` (`included | excluded`), `reason`, `approver_person_id`, `decided_at`. It is effective-dated, so a new decision supersedes rather than edits.
- `ApplicationAlias`: `application_id`, `alias` (normalized, unique per tenant).
- `ApplicationRetirement`: `effective_at`, `reason`, and `merged_into` (nullable).

## Canonical model alignment

These rules use `Application` and `SystemInstance` from the canonical entity
model. The instance-kind vocabulary and the owner roles (system owner, access
owner) are alignments requested on M0-D28.

## Follow-up discovery

[#352](https://github.com/bdgrz/compliance/issues/352) records the first
client's authoritative list source and applications.

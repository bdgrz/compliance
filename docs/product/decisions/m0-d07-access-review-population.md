# M0-D07: First access-review population, providers, and expectations

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.
Imports and integrations are deferred (product direction, 2026-09-22). This
record fixes the normalized population shape; collecting real exports is the
follow-up [#353](https://github.com/bdgrz/compliance/issues/353).

| Question | Decision and rationale |
| --- | --- |
| First reviewed systems | Each AWS account; the GitHub organization; the identity-provider tenant (admins and privileged roles); and admin-level access in core SaaS (Slack, Jira). Sample exports are gathered in #353, not built into connectors. |
| Principal kinds and entitlements | Principal kinds: `user_account`, `group`, `role`, `service_principal`, `workload_identity`, `access_token`, `app_installation`. Entitlement kinds: `membership`, `role_assignment`, `permission_set_assignment`, `permission`, `repository_access`, `admin_privilege`. |
| Effective access | The population stores **direct assignments** as observed. Effective access is a derived, versioned calculation per population snapshot: nested group membership is expanded transitively (cycles rejected), and role assumption produces an effective path that records every hop. Direct and effective access are never conflated. |
| Human vs NHI | An account correlated to a workforce `Person` is human. An account correlated to a governed `ServiceIdentity` is NHI. An uncorrelated account is `unclassified` and is a review exception. A shared account (one account, several people) is `shared` and must have an accountable owner and a justification. It is never silently treated as human. |
| Initial expectations | AWS: no IAM users (all human access through IAM Identity Center); administrator permission sets are privileged. GitHub: organization owners and admin-level repository access are privileged; outside collaborators require an expiry. IdP: super-admin and privileged admin roles are privileged. SaaS: workspace or site admins are privileged. Privileged entitlements require named reviewer approval each campaign. |
| Reviewer and remediation evidence | A reviewer is the access owner of the system instance, or a delegate, and may not review their own access (M0-D03 SoD). Remediation is complete only when a later population snapshot shows the entitlement removed or changed. A ticket alone is not verification. The advisor's confirmation of auditor acceptance is part of #353. |

## Data-shape rules

- `AccessPopulationSnapshot`: `system_instance_id` (exact version), `observed_at`, `source` (a manual attestation until imports exist), and `content_hash`. It is immutable once accepted (M0-A02).
- `Account`: `system_instance_id`, `provider_subject_id` (unique per instance), `principal_kind`, `display_name`, `status`, `classification` (`human | nhi | shared | unclassified`), and a correlation to a `Person` or `ServiceIdentity`.
- `AccessAssignment` (direct): `account_id`, `entitlement_id`, and `granted_via` (`direct | group | role`), within a snapshot.
- `EffectiveAccess` (derived): `account_id`, `entitlement_id`, the `path[]` of assignment hops, and `calculation_id`.
- `AccessExpectation`: `system_instance_id`, `rule_kind` (`forbidden_principal_kind | privileged_entitlement | requires_expiry`), `parameters`, and an approver. It is versioned.

## Canonical model alignment

This uses `Account`, `Group`, `GroupMember`, `Role`, `Entitlement`, and
`AccessAssignment` from the canonical model. The classification vocabulary
(`shared`, `unclassified`) and the derived `EffectiveAccess` path shape are
alignments requested on M0-D28.

## Follow-up discovery

[#353](https://github.com/bdgrz/compliance/issues/353) collects sanitized
sample exports and the advisor's evidence confirmation. It blocks R2-06a
backend [#273](https://github.com/bdgrz/compliance/issues/273).

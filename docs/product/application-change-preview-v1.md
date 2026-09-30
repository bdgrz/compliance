# Application change preview v1

Status: implemented bounded read slice for R1-10d backend #217. This is an
advisory preview of tenant-authored Application content. It does not persist a
proposal, approve a revision, retire an Application, or assert complete impact.

`POST /api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews`
uses Portia's API-user authentication and an active tenant membership. It
requires both `application_inventory.manage` for the Application and
`program.manage` because the response now discloses Control-draft relationship
metadata. The machine-appropriate `bdgrz.application.change.preview` MCP tool
is registered `ReadOnly`; all arguments are flat scalars. These tenant-wide
grants are interim while scoped inventory access and record restrictions are
decided in R1-04b and M0-D05.

The JSON body has `expected_application_revision` (positive), `change_kind`
(`revise` or `retire`), and, for `revise`, `name`, `purpose`, and optional
`owner_reference`. Names and descriptions use the same bounds as the existing
manual Application revision. A `retire` preview rejects revised fields. The
operation emits no event and changes no Application or boundary record.

The 200 result contains `tenant_id`, `application_id`,
`application_revision`, `change_kind`, a list of changed content fields with
`before` and `after`, up to 200 current draft and approved or historical
`boundary_references`, up to 200 current direct-Application
`control_draft_references`, `pending_contexts`, and `complete`. A control
reference contains the stable control, program, applicability-entry, and
current draft revision identifiers plus the authored subject and rationale; it
does not contain full draft content. `complete` is always `false` in this
slice. `boundary_references_over_limit` and
`control_draft_references_over_limit` are added when their respective result
sets exceed 200, rather than silently presenting a truncated set as complete.

`control_draft_references` include only non-discarded, currently indexed
`application` applicability entries. They do not establish an approved or
effective ControlVersion, activation, retirement, historical relationship,
coverage, effectiveness, or a complete lifecycle impact. A concrete
`system_instance` reference is intentionally not fanned out through its owning
Application in this slice; `system_instance_control_draft_references` remains
pending so that omission is visible. The other pending contexts include
vendors, `approved_control_versions_and_lifecycle_impact`, policies, evidence
sources, access populations, review campaigns, open work, readiness, and
engagements. Historical discarded drafts are not in either reverse index; their
source event streams retain them.

The handler hydrates the tenant Application aggregate, checks its exact
revision against the Fitz Application projection, and checks both the boundary
and Control reverse-projection checkpoints against their corresponding tenant
source streams before and after reading references. It rechecks the Application
source revision after the projection reads. A source write detected during the
read returns a retryable 409. A missing or wrong-tenant Application returns 404
without disclosing another tenant. Invalid request fields return 400. A stale
expected revision returns 409. This advisory observation is not an atomic
cross-stream snapshot, freeze, or digest that could be used for approval; a
later write can still race a completed preview.

The existing `ReviseApplication` command still applies a manual change
directly. A governed successor, retirement, and in-use deletion gate require
the M0-D05 inventory rules, scoped access, and complete contributions from the
owning downstream contexts. Those later operations must not infer clearance
from this preview. Backend child #217 and product parent #105 remain open.

## Retirement and access-review scope (bundle for #215 and #217)

The preview also returns up to 200 projected `system_instance_references`, and adds
`system_instance_references_over_limit` when there are more. It rejects a retired Application
with a 409.

`POST .../applications/{application_id}/retirements` (`bdgrz.application.retire`) records an
effective-dated `ApplicationRetired` with a reason and an optional active `merged_into_application_id`.
`POST .../system-instances/{system_instance_id}/retirements` (`bdgrz.system_instance.retire`)
retires one registered instance. Retirement keeps all revisions, instances, and scope history.
A retired record rejects later revisions, new instances, and new scope decisions. Directory and
revision views carry `lifecycle` and `retirement`.

`POST .../system-instances/{system_instance_id}/access-review-scope-decisions` is an HTTP-only
Compliance Lead approval. It requires `application_inventory.manage` plus organization-wide
`program.manage`. It appends an `included` or `excluded` decision with a reason,
`effective_from`, and an optional `review_by`, bound to the exact instance revision and the
expected decision count. A new decision supersedes the previous one. It cannot be backdated
before the previous decision. The member who registered the instance cannot approve its scope
without an active exact-scope `system_instance_access_review_scope` waiver.
`GET .../access-review-scope` (`bdgrz.system_instance.access_review_scope.get`) returns the
decision in effect at an optional `as_of` time (`unresolved` when none applies) and the full
history, read from the source stream.

Still open: the access-owner SoD rule (this needs a member-to-person link), a Fitz scope
projection and readiness visibility, scope for legacy application-stream instances, general
application-to-application relationships, a governed successor or approval gate, and complete
downstream impact.

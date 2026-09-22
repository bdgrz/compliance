# Application change preview v1

Status: implemented bounded read slice for R1-10d backend #217. This is an
advisory preview of tenant-authored Application content. It does not persist a
proposal, approve a revision, retire an Application, or assert complete impact.

`POST /api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews`
uses Portia's API-user authentication and the current tenant membership plus
`program.manage` inventory authorizer. The machine-appropriate
`bdgrz.application.change.preview` MCP tool is registered `ReadOnly`; all
arguments are flat scalars. This grant is interim while scoped inventory access
and record restrictions are decided in R1-04b and M0-D05.

The JSON body has `expected_application_revision` (positive), `change_kind`
(`revise` or `retire`), and, for `revise`, `name`, `purpose`, and optional
`owner_reference`. Names and descriptions use the same bounds as the existing
manual Application revision. A `retire` preview rejects revised fields. The
operation emits no event and changes no Application or boundary record.

The 200 result contains `tenant_id`, `application_id`,
`application_revision`, `change_kind`, a list of changed content fields with
`before` and `after`, up to 200 current draft and approved or historical
`boundary_references`, `pending_contexts`, and `complete`. `complete` is always
`false` in this slice. The `boundary_references_over_limit` pending context is
added when more than 200 boundary references exist, rather than silently
presenting a truncated set as complete. The other pending contexts are vendors,
controls, policies, evidence sources, access populations, review campaigns,
open work, readiness, and engagements. Historical discarded drafts are not in
the reverse index; the boundary event stream retains them.

The handler hydrates the tenant Application aggregate, checks its exact
revision against the Fitz Application projection, and checks the boundary
reverse projection's checkpoint against the tenant boundary source before
reading references. It rechecks the Application source revision after the
projection reads. A missing or wrong-tenant Application returns 404 without
disclosing another tenant. Invalid request fields return 400. A stale expected
revision returns 409; projection lag or a concurrent source change returns a
retryable 409. This advisory observation is not an atomic freeze or a digest
that could be used for approval.

The existing `ReviseApplication` command still applies a manual change
directly. A governed successor, retirement, and in-use deletion gate require
the M0-D05 inventory rules, scoped access, and complete contributions from the
owning downstream contexts. Those later operations must not infer clearance
from this preview. Backend child #217 and product parent #105 remain open.

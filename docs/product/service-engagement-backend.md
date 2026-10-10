# Staff directory and client service-engagement drafts

M0-D25's platform directory records canonical staff UserId and practice outside
client tenants. Current operators can register an existing platform user and
record staff activation/deactivation with attribution and a reason. The source
reference contains general staff provenance, never client business facts.
Canonical user and staff identity are unique and immutable; changing OIDC or
other provider identity preserves UserId through the existing identity system.
No directory value designates a partner, ratifies rules or grants client access.
The directory is bounded to one thousand staff records.

Client service-engagement drafts are separate from Type I/Type II audit
engagements. A currently active client member with the ordinary Org Admin grant
can create, amend, close and read drafts, propose/withdraw staff, read draft
history, and read the active platform roster to choose canonical identities.
Platform operator status and standing firm_staff membership confer no such
client authority. The directory remains platform metadata; drafts and their
immutable revisions remain solely in the client stream.

All preacceptance staffing, including the proposed engagement lead, is
**tentative**. The API calls it a staff proposal and returns
`proposal_state: proposed` or `withdrawn`, with
`professional_access_granted: false`. Creating or canceling a draft, amending
its proposed lead or withdrawing tentative staff never becomes permanent
professional assignment history. No client access is granted by a draft or
proposal. Actual accepted/active assignments must pass the lifetime
independence wall in their owning acceptance/assignment consumer; those
professional operations are separate from this draft contract.

The six independence operations remain available. This draft capability adds:

| Operation | HTTP route | MCP tool |
| --- | --- | --- |
| Register canonical staff | `PUT /api/v1/platform/firm-staff/{staff_member_id}` | `bdgrz.firm-staff.register` |
| Change staff activity | `PUT /api/v1/platform/firm-staff/{staff_member_id}/status` | `bdgrz.firm-staff.status.set` |
| Read platform staff directory | `GET /api/v1/platform/firm-staff` | `bdgrz.firm-staff.list` |
| Read active roster in authorized client context | `GET /api/v1/tenants/{tenant_id}/firm-staff` | `bdgrz.firm-staff.assignable.list` |
| Create draft | `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}` | `bdgrz.service-engagement.create` |
| Amend draft | `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/draft` | `bdgrz.service-engagement.amend` |
| Propose staff | `PUT /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}` | `bdgrz.service-engagement.staff.propose` |
| Withdraw proposal | `POST /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals` | `bdgrz.service-engagement.staff.withdraw` |
| Close draft | `POST /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure` | `bdgrz.service-engagement.close` |
| Read current draft | `GET /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}` | `bdgrz.service-engagement.get` |
| List client drafts | `GET /api/v1/tenants/{tenant_id}/service-engagements` | `bdgrz.service-engagement.list` |
| Read immutable draft revisions | `GET /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/history` | `bdgrz.service-engagement.history.get` |

Every mutation requires the current `expected_sequence`. Client draft changes,
service additions and evaluation previews share the same authoritative client
ledger sequence. Directory registration/status changes use their platform
sequence. Directory rereads detect concurrent source changes after retaining a
draft; a transient conflict asks the caller to reload. A retained proposal
remains tentative and cannot create access even if staff was disabled during
the operation. Canonical-user directory lag fails closed at registration.

Client draft count, staff per draft and immutable revisions per draft are each
bounded to one hundred. Events cannot exceed 48 KiB, and history is never
truncated to make a write pass. Retries preserve original intent and actor;
reusing a request identity for another mutation conflicts. Closed drafts cannot
be reopened or edited. A draft amendment cannot rewrite its practice identity,
and the proposed lead must remain selected until another lead is recorded.

Partner-duty designation and personal HTTP-only acceptance require the real
professional authority decision recorded on #269. The public acceptance
operation is separate from this draft capability. Actual firm ratification
evidence remains #343; the directory or draft UI cannot generate it or remove
that gate.

## Personal client management acknowledgement

An active owning-client Org Admin can submit `AcknowledgeEngagementManagement` over HTTP
and read its retained history. Submission requires an authenticated personal HTTP invocation;
MCP, direct queued calls and system actors are refused. The operation is not registered as an
MCP tool. The read is available to the same client administration authority over HTTP and MCP.

An acknowledgement records the canonical user and attributed client member, exact draft
revision, complete immutable service-record identities, personal statement and timestamp.
The ledger's shared sequence rejects concurrent new facts, amendments or stale lists; identity
retries return the original record and changed intent conflicts. Histories are bounded to one
hundred acknowledgements without truncation. Replay preserves the same source and attribution
fences. Actual historical Attest assignees cannot acknowledge client management responsibilities.

This is the client's own management decision. It does not ratify independence rules, designate
an engagement partner, accept an engagement, or grant professional access. The separate
acceptance operation must independently verify designated partner authority, current source
facts and this exact acknowledgement.

## Accepted-state domain boundary

The HTTP-only `POST /api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance`
operation accepts only the tenant, engagement and expected client sequence. It does not accept
caller-supplied partner, authority, directory, boundary or rule evidence, and it is not registered
as an MCP tool. A dedicated firm-professional authorizer requires an authenticated personal HTTP
request, an active tenant, a current canonical platform user and one active firm-staff identity.
The handler then resolves trusted evidence for the exact actor and engagement, rechecks that the
actor is the currently designated partner and that the directory snapshot is still current, and
requires the applicable ratified rules. No partner duty designation or production ratification is
inferred from platform operator, directory practice or client administration authority.

Production composition currently installs a fail-closed evidence reader: until an authoritative
partner-duty and ratified-rule provider is configured, acceptance returns a transient conflict
without appending an event. Synthetic fixture proofs are test evidence, never real professional
ratification.

Acceptance recomputes policy from the complete immutable retained service history. An impaired
or unclassified Attest service cannot be overridden; conditional compatibility requires a
recorded partner evaluation. The acceptance snapshot retains its rule version, considered
services and result, management acknowledgement, approver, review-task authority and source
revisions, time, and actual accepted team. A draft may optionally select an examination boundary;
when it does, acceptance requires the matching approved version and approval decision. Unscoped
drafts accept with no boundary proof; unselected, half-present or mismatched proofs are refused.
Later boundary successors cannot rewrite the historical binding.

The approving partner remains separately attributed. Approval alone does not create an actual
team assignment or grant client business access. The approver is checked against opposite
actual assignment history; explicitly proposed team members become actual history only at
acceptance. A broader rule about professional involvement outside assignments remains a firm
ratification detail for issue #343.

Actual assignments create permanent client-specific person history by canonical user and staff
identity. Client closure and assignment revocation preserve it through aliases and later
engagements. The client-administration endpoint and MCP tool
`bdgrz.service-engagement.actual-staff.revoke` remove one current accepted assignment with an
expected client sequence and attributed reason. Both use the existing active-tenant, current
client-membership, and `tenant.rbac.manage` authorization; neither platform operators nor
professional staff receive client authority from this operation. Removing the accepted lead
closes the engagement and revokes the entire team; removing another assignee revokes only that
assignment and leaves the engagement accepted. Client management acknowledgements refuse actual
historical Attest actors even if they have a client-role identity. Immutable accepted history
retains original approvals and separately attributed revocation reason and time.

Current client administration grants allow acceptance metadata/history reads and closure. The
actual-assignment revocation endpoint and MCP tool are described above.

`IsEligibleForProfessionalAccess` remains a deny-only prerequisite: callers supply the ratified
rules version frozen into the accepted engagement, the current directory revision, and an
effective UTC instant. The frozen acceptance snapshot governs that engagement; later unratified
catalog drafts do not silently replace its rules. Runtime authorization must also prove an active
tenant, current staff-directory identity/status, and the exact accepted assignment, then apply the
independence wall. The predicate refuses before the recorded acceptance/assignment, outside the
engagement period, after closure/removal, and when retained service, assignment, or directory
reevaluation receipts invalidate the accepted snapshot.

One narrow professional read consumer is supported: `ListReadinessAnnotations` allows a
canonical assigned advisory professional to read annotation bodies without client membership or a
client program grant when the tenant is active and the existing Program is correctly scoped to
it, the current firm-staff identity is active, and the exact accepted advisory assignment remains
eligible. It uses the ratified rule version frozen in that acceptance, current directory revision,
and effective UTC time; the general predicate does not grant access to other resources. The
readiness annotation authorizer also applies the complete retained Attest compartment wall, which
continues to block that client's advisory notes after Attest closure or assignment removal.

For that consumer, each authorization hydrates the current engagement, staff directory and
assignment history. A closure, assignment removal, changed directory revision or retained source
or directory reevaluation receipt denies the next read. Cross-stream changes are not atomic with
the annotation request, and no cross-stream freshness or concurrency guarantee is claimed. Other
professional-access authorizers and compartment search/count/export/notification consumers
remain unfinished.

Engagement and acceptance histories remain bounded to one hundred revisions, and complete
actual assignment history to one thousand records. New oversized events or histories are
refused without truncating evidence. Acceptance cannot predate any retained engagement/staff-proposal decision, its exact acknowledgement,
complete service facts, authored rules, approved boundary or verified authority/directory sources;
backdated draft successors cannot hide later historical timestamps. Revocations cannot predate
the latest recorded lifecycle decision. Replay recomputes the policy and source/identity fences from retained facts.

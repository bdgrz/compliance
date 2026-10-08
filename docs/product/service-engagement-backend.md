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
professional authority decision recorded on #269. No public acceptance or
ratification operation is supplied by this draft capability. Actual firm
ratification evidence remains #343; the directory or draft UI cannot generate
it or remove that gate.

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
an engagement partner, accept an engagement, or grant professional access. A later professional
acceptance must independently verify designated partner authority, current source facts and
this exact acknowledgement. No public professional acceptance operation exists yet.

## Accepted-state domain boundary

The ledger has an internal acceptance command that consumes verified named-partner review
proof, current active directory snapshots and designated-duty revision, a ratified rule
snapshot, and the exact personal client acknowledgement. This command is deliberately absent
from public HTTP and MCP write contracts. No partner duty designation or production ratification
is inferred from platform operator, directory practice or client administration authority.
The production verified-partner authorizer and cross-source acceptance handler remain unfinished;
synthetic fixture proofs are test evidence, never real professional ratification.

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
engagements. Removing the accepted lead closes the engagement and revokes the entire team;
other removals revoke the named assignment. Client management acknowledgements refuse actual
historical Attest actors even if they have a client-role identity. Immutable accepted history
retains original approvals and separately attributed revocation reason and time.

Current client administration grants allow acceptance metadata/history reads and the existing
closure operation consumes accepted state when it exists. Actual removal is internal pending
its professional lifecycle contract. No public acceptance writer or professional access grant
is introduced. `IsEligibleForProfessionalAccess` is only a deny-only prerequisite: callers must
supply trusted current rule and directory versions and an effective UTC instant, then apply
ordinary grants and active directory/tenant conditions independently. It refuses before the
recorded acceptance/assignment, outside the engagement period, after closure/removal, and when
new service facts or changed supplied source versions invalidate the accepted snapshot. Runtime
professional-access authorizers and compartment search/count/export/notification consumers
remain unfinished.

Engagement and acceptance histories remain bounded to one hundred revisions, and complete
actual assignment history to one thousand records. New oversized events or histories are
refused without truncating evidence. Revocations cannot predate the latest recorded lifecycle
decision. Replay recomputes the policy and source/identity fences from retained facts.

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

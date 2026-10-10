# Existing advisory readiness note read consumer

PR #506 and issue #275 delivered `ReadinessAnnotationView`: attributed immutable
advisor feedback on a gap of one exact readiness assessment. The body-bearing
public read is `ListReadinessAnnotations` through its handler, Fitz read model
and readiness directory. Assessment, assessment summary and gap responses do
not contain annotation bodies or annotation counts.

A request-specific `ReadinessAnnotationCompartmentAuthorizer` protects that
list as `AdvisoryWorkingNotes`. Client personnel still need exact current
owning-client membership, tenant/program ownership, an active tenant and the
ordinary program-read grant. For a canonical user without a current client
membership, the authorizer allows only the assigned-advisor path: an active
tenant, a Program owned by that tenant, exactly one active `advisory`
firm-staff directory identity and an active accepted advisory engagement with
the same current staff/user assignment. That assignment must match its frozen
ratified rule version and current directory revision and remain inside the
engagement period. No broad `IProgramReadRequest` marker or generic client
access grant is introduced.

The reusable scoped `ClientCompartmentIndependenceGuard` runs for both paths. It
consumes the canonical actor's complete retained actual assignment history and
the existing pure `IndependenceCompartments.IsBlockedByWall` policy. Invalid
tenant, user or compartment inputs deny. Any retained actual Attest assignment
for this client blocks this client's advisory feedback through HTTP and MCP,
including after closure or assignment removal; actual Advisory history and
another client's Attest history do not cause that denial. The helper grants no
client or professional authority.

Production-composed tests persist readiness assessments and annotations through
the aggregate executor, then run the canonical Fitz readiness projection and
checkpoint. They cover assigned-advisor reads without client membership or a
program grant, current assignment and directory status, tenant/program scope,
preserved shared assessment/gap responses, ordinary client authorization,
membership privacy, and denial by the Attest wall. Test assignment evidence is
explicitly synthetic.

This is one current consumer, not a generic search/export service or a new
wire label. The authorization hydrates the current accepted engagement,
directory and assignment history for each request; a closure, removal, changed
directory revision or retained source/directory reevaluation receipt denies the
next read. Changes across those independent streams are not atomic with the
annotation request, and no cross-stream freshness or concurrency guarantee is
claimed. #275 handoff selection, professional/guest handoff access and
revocation remain unfinished. #300 discussion and `attest_internal` sources
are not implemented; there is no corresponding public consumer in this slice.
Their read/search/count/export/notification acceptance remains open. No public
professional acceptance or real rule ratification is introduced here.

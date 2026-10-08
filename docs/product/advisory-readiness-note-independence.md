# Existing advisory readiness note read consumer

PR #506 and issue #275 delivered `ReadinessAnnotationView`: attributed immutable
advisor feedback on a gap of one exact readiness assessment. The body-bearing
public read is `ListReadinessAnnotations` through its handler, Fitz read model
and readiness directory. Assessment, assessment summary and gap responses do
not contain annotation bodies or annotation counts.

A request-specific `ReadinessAnnotationCompartmentAuthorizer` now protects that
list as `AdvisoryWorkingNotes`. It first checks an attributable canonical user
and exact current owning-client membership, retaining `NotFound` for absent,
suspended, deprovisioned or mismatched membership. The existing program-read
authorizer still independently requires tenant/program ownership, active tenant
and ordinary read grants. No broad `IProgramReadRequest` marker is introduced.

The reusable scoped `ClientCompartmentIndependenceGuard` consumes the canonical
actor's complete retained actual assignment history and the existing pure
`IndependenceCompartments.IsBlockedByWall` policy. Invalid tenant, user or
compartment inputs deny. A history of actual Attest assignment blocks this
client's advisor feedback through HTTP and MCP, including after closure; actual
Advisory history and another client's Attest history do not cause that denial.
The helper grants no client or professional authority.

Production-composed tests persist an actual readiness assessment and annotation
through the aggregate executor, then run the canonical Fitz readiness projection
and checkpoint. Four tests reproduced successful note disclosure before the
specific authorizer; fifteen focused cases afterward include preserved shared
assessment/gap responses, ordinary grant denial, membership privacy and invalid
input denial. Test assignment evidence is explicitly synthetic.

This is one current consumer, not a generic search/export service or a new
wire label. #275 handoff selection, professional/guest handoff access and
revocation remain unfinished. #300 discussion and `attest_internal` sources
are not implemented; there is no corresponding public consumer in this slice.
Their read/search/count/export/notification acceptance remains open. Concurrent
new acceptance and note access are not claimed to be atomic across independent
streams; the future public acceptance adapter retains that integration boundary.
No public professional acceptance or real rule ratification is introduced.

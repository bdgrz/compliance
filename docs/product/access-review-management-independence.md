# Access-review management independence

The supplemental client-management authorizer checks canonical, retained actual
Attest assignment history before access-review management mutations. Existing
membership, tenant activity, grants, restricted visibility, source revision,
reviewer assignment and separation-of-duties checks remain mandatory.

`IAccessReviewMutationRequest` explicitly covers population opening, fact entry,
acceptance and principal classification; expectation proposal, approval and
exception recording; campaign launch and completion; remediation recording,
verification and exceptions; and individual and bulk access decisions.
The assigned reviewer's access decision approves a client management record,
even when its transport is personal HTTP and its ordinary grant is supplied by
the campaign assignment. Personal transport does not exempt that mutation from
the Attest wall. Existing personal decision operations remain HTTP-only.

Population, expectation, principal, variance, coverage and campaign reads, and
population/bulk-decision previews remain ordinary authorized reads. An actual
Advisory assignment does not impose the Attest management-write denial. Attest
history in a different client does not deny this client's operations. Ending an
actual Attest assignment does not erase its retained canonical person history.

Focused tests use production `AddCompliance` and the request bus, shared retained
source events, and explicitly synthetic internal acceptance evidence. They
exercise HTTP/MCP management denial after client or guest relinking, retained
closed history, ordinary client and Advisory writes, different-client history,
assigned individual/bulk decision denials, allowed reads/previews and ordinary
assigned reviewer decisions. Source setup and current membership/permission
dependencies use explicit test doubles; these tests do not qualify deployed
routes, broker workloads or real professional acceptance.

This is an access-review consumer under #277. It grants no professional access,
ratification or partner authority, changes no campaign placement decision in
#646, and introduces no public acceptance writer. Other management families,
professional compartments and acceptance/write concurrency retain their owning
scope. The supplemental guard observes authoritative history at dispatch; it
does not establish an atomic transaction across future professional acceptance
and unrelated client management streams.

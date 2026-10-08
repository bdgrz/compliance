# Client management write independence wall

M0-D26 and the canonical domain model prohibit actual Attest assignees from
authoring or approving that client's management records. The wall follows the
canonical platform user across later client/guest membership and retains the
full actual assignment history after assignment removal or engagement closure.
Tentative staffing proposals do not enter that history.

`IClientManagementMutationRequest` explicitly identifies a management write.
`ClientManagementMutationAuthorizer` supplements every existing assignable
request authorizer. It first requires an authenticated canonical Bdgrz user and
current owning-client membership, preserving `NotFound` for absent, suspended
or deprovisioned membership. Its `ClientManagementIndependenceGuard` reads the
authoritative client independence stream and refuses any actual historical
Attest assignment for that canonical user. The guard grants no membership,
permission, program authority or professional access.

The first consumer subset contains 34 request types:

- control draft creation, revision, successor/proposal withdrawal, review,
  approval, owner designation, discard, retirement proposal and retirement;
- policy draft creation/revision/discard, successor, review, approval,
  retirement proposal/approval and review confirmation;
- evidence request opening, cancellation and fulfilment;
- operating-plan proposal/approval and occurrence opening, personal attestation,
  correction and review;
- control evaluation starting, procedure-step recording, submission, review and
  deviation disposition, and corrective-action completion.

The occurrence and evidence-fulfilment writes deliberately use ordinary read
permissions plus operating responsibility checks. Consequently permission names
cannot classify record intent. Read, list, preview and history requests do not
inherit the mutation marker; shared management records retain their ordinary
membership, resource ownership and grant requirements. Same-client actual
Advisory history and another client's actual Attest history do not trigger this
write wall.

The shared `IControlOperationRequest` interface also carries evaluation-plan
version reads and work-item queue commands; it is deliberately unmarked.
Concrete management mutations carry the marker individually. Personal work-item
claim/assignment/delegation/escalation cannot be classified solely from that
shared interface because the queue includes allowed personal policy
acknowledgements. Their source-specific management actions need a separate wall.

Inventory, workforce, access-review and remaining program mutation families
need their own explicit write annotations and acceptance evidence. Advisory
working-note and attest-internal discussion filters, search/count/export and
notification consumers also remain separate work. This marker does not apply
to internal projection/reactor effects, nor does it introduce professional duty
designation, a public acceptance route or real rule ratification.

The guard evaluates authoritative retained history at request authorization.
It does not claim an atomic transaction spanning independence acceptance and
other client record streams. A future public acceptance adapter must address
concurrent assignment/record decisions before exposing professional authority.

Production-composed unit tests exercise the HTTP/MCP invocation contexts,
canonical identity after client/guest relinking, retained closed history,
ordinary membership/grant denials and preserved shared control reads. Test
history uses explicitly synthetic verified proof and ratified rule fixtures
through the internal accepted-state engine; it is never real professional
ratification evidence.

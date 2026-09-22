# Backend delivery ledger

Status: updated on 2026-09-22.

This ledger keeps the GitHub backlog honest while backend work is delivered in
capability-sized pull requests. A pull request may cover several dependent
backend children when the children share contracts, domain records, or proof.
The pull request lists each covered child and its evidence. The child issue
stays open until its own backend acceptance criteria pass.

## Status rules

- **Complete** means the child or decision issue is closed and its merged
  implementation or decision record, focused evidence, and remaining limits are
  linked below.
- **Partial** means a merged pull request delivered a useful slice. The child
  remains open because one or more acceptance criteria, isolation proofs,
  deployment proofs, or product decisions are still missing.
- **Queued** means the child is the next dependency-ordered implementation
  target. It is not represented by a merged product implementation yet.

The product parent remains open for UI work. Login, email verification,
invitation acceptance, acknowledgements, attestations, approvals, and personal
sign-offs remain HTTP-only even when their surrounding capability has MCP
operations.

## Completed backend and discovery records

These are the only closed P0 records in the repository at the time of this
ledger. This synthetic documentation PR records the implementation trail; it
does not reopen or close issues.

| Record | Merged implementation or decision | Evidence and limits |
| --- | --- | --- |
| [M0-D22 #79](https://github.com/bdgrz/compliance/issues/79) | [PR #163](https://github.com/bdgrz/compliance/pull/163), merge `221b682296910200bb29f13863a5097d27cf45bf` | [Canonical ownership decision](decisions/m0-d22-canonical-ownership.md), affected issue updates, and exact-head CI are recorded in the issue closure comment. Organization-specific workforce and NHI authority remain with M0-D06. |
| [M0-D23 #80](https://github.com/bdgrz/compliance/issues/80) | [PR #166](https://github.com/bdgrz/compliance/pull/166), merge `97c77fe850745ca37e1c7176d678ae42a7fb137b` | [Assurance vocabulary decision](decisions/m0-d23-assurance-vocabulary.md), ten dependent issue updates, and exact-head CI are recorded in the issue. Firm-specific approval and professional evaluation rules remain open in #60 and #70. |
| [AUTH-01 #142](https://github.com/bdgrz/compliance/issues/142) | [PR #144](https://github.com/bdgrz/compliance/pull/144), merge `2b3bb8b375c741455160ddbad70aa0c240564c56`; session reflection in [PR #150](https://github.com/bdgrz/compliance/pull/150) | Development and OIDC identity registration, provider identity resolution, signed HTTP session cookies, and logout are backend-only. The issue records focused acceptance coverage; no browser UI is implied by this row. |
| [AUTH-02 #143](https://github.com/bdgrz/compliance/issues/143) | [PR #149](https://github.com/bdgrz/compliance/pull/149), merge `e46157ff647db6ef6f092cd3b954b83cc5f022ab` | Normalized email ownership, idempotent reservation, challenge hashing, verification transitions, owner-scoped reads, and HTTP routes are covered. Delivery is `MockEmailChallengeDelivery`; no external email was sent and a production adapter remains a follow-up. |
| [R1-15a #238](https://github.com/bdgrz/compliance/issues/238) | [PR #256](https://github.com/bdgrz/compliance/pull/256), merge `01590b1fc1f86657f3c6096bdcf382232720a577` | Operator-only tenant inventory uses Fitz 1.4.1 primary paging without request-path writes. Portia authorization, snake_case HTTP and read-only MCP contracts, cross-tenant non-disclosure, standalone/split broker proof, focused tests, exact-head CI, and post-merge main CI are recorded in the issue closure comment. Product parent #237 and frontend child #239 remain open. |

## Merged capability bundles that remain partial

These bundles are valuable backend progress, but none is a substitute for the
child issue's complete acceptance evidence.

| Capability bundle | Merged pull requests | Open child records and remaining gate |
| --- | --- | --- |
| R1-15 tenancy, membership, and split-host proof | [#151](https://github.com/bdgrz/compliance/pull/151), [#153](https://github.com/bdgrz/compliance/pull/153), [#154](https://github.com/bdgrz/compliance/pull/154), [#155](https://github.com/bdgrz/compliance/pull/155), [#164](https://github.com/bdgrz/compliance/pull/164) | [R1-15 backend #152](https://github.com/bdgrz/compliance/issues/152), [EN-01 #157](https://github.com/bdgrz/compliance/issues/157), and [R1-01 #158](https://github.com/bdgrz/compliance/issues/158) still need the documented M0-D25, M0-A07, M0-D28, cross-tenant, and complete split-host proof. |
| Membership invitation lifecycle | [#185](https://github.com/bdgrz/compliance/pull/185), [#208](https://github.com/bdgrz/compliance/pull/208), [#263](https://github.com/bdgrz/compliance/pull/263), merge `bd63bad10a4cd6a3dff155ef2d3eb28d57f130c8` | [R1-04a backend #183](https://github.com/bdgrz/compliance/issues/183) remains open for the Portia delivery crash window, identity replacement/recovery, provider adapter, policy/source decisions, and child-specific completion proof. Personal invitation acceptance remains HTTP-only; email delivery is mocked. |
| Boundary versioning, immutable history, projection consistency, snapshots, and retained-source replay | [#174](https://github.com/bdgrz/compliance/pull/174), [#175](https://github.com/bdgrz/compliance/pull/175), [#240](https://github.com/bdgrz/compliance/pull/240), [#242](https://github.com/bdgrz/compliance/pull/242), [#243](https://github.com/bdgrz/compliance/pull/243), [#244](https://github.com/bdgrz/compliance/pull/244), [#245](https://github.com/bdgrz/compliance/pull/245), [#247](https://github.com/bdgrz/compliance/pull/247), [#248](https://github.com/bdgrz/compliance/pull/248), [#318](https://github.com/bdgrz/compliance/pull/318), [#320](https://github.com/bdgrz/compliance/pull/320), [#321](https://github.com/bdgrz/compliance/pull/321) | [M0-A05 #85](https://github.com/bdgrz/compliance/issues/85) remains open for ADR acceptance after M0-A01, a dedicated projection-derived-read spike in standalone and split hosts, and product-owned calculation and cross-client rules. [EN-02 #160](https://github.com/bdgrz/compliance/issues/160) remains open for complete reuse across owning contexts, complete impact and deletion evidence, and inherited recovery acceptance. PR #321 proves retained-source Program recovery and production projector replay only; it does not prove portable backup/restore, recovery targets, a global calculation snapshot, or cross-client authorization. |
| Application inventory, source revisions, and change impact | [#249](https://github.com/bdgrz/compliance/pull/249), [#250](https://github.com/bdgrz/compliance/pull/250), [#252](https://github.com/bdgrz/compliance/pull/252), [#258](https://github.com/bdgrz/compliance/pull/258), [#259](https://github.com/bdgrz/compliance/pull/259), [#261](https://github.com/bdgrz/compliance/pull/261) | [R1-10a #211](https://github.com/bdgrz/compliance/issues/211), [EN-05 #195](https://github.com/bdgrz/compliance/issues/195), [R1-10b #213](https://github.com/bdgrz/compliance/issues/213), and [R1-10d #217](https://github.com/bdgrz/compliance/issues/217) retain verified ownership, classification authority, source identifiers, restricted discovery, import provenance, authorization, failure recovery, and complete projection proof. #261 carries the tenant-declared classification through Portia, HTTP/MCP, Fitz current/history views, and previews while keeping it `classification_unverified`; #258 adds pre-acceptance HTTP cancellation; #259 upgrades Portia 0.5.3 and exposes the bounded stage MCP command. |
| Control, commitment, and risk drafts | [#251](https://github.com/bdgrz/compliance/pull/251), [#253](https://github.com/bdgrz/compliance/pull/253), [#254](https://github.com/bdgrz/compliance/pull/254) | [R1-05 #197](https://github.com/bdgrz/compliance/issues/197), [R1-13 #229](https://github.com/bdgrz/compliance/issues/229), and [R1-07 #199](https://github.com/bdgrz/compliance/issues/199) retain owner, applicability, review, activation, cross-record consistency, and risk acceptance gaps called out by the merged PRs. |
| Operator portfolio and platform tenancy | [#256](https://github.com/bdgrz/compliance/pull/256) | [R1-15 #237](https://github.com/bdgrz/compliance/issues/237) remains open for the product parent and [#239](https://github.com/bdgrz/compliance/issues/239) remains open for frontend delivery; backend child #238 is closed. |

## Next grouped implementation loop

This M0 projection-consistency bundle records ADR 0007 and makes the existing
application boundary-reference lag result explicitly transient through its
authorized HTTP and read-only MCP contracts in split API/worker mode. The
cohosted standalone test proves outsider non-disclosure, not lag recovery. It
advances [M0-A05 #85](https://github.com/bdgrz/compliance/issues/85) and
[EN-02 #160](https://github.com/bdgrz/compliance/issues/160) without claiming
a readiness calculation, a work queue, or cross-client authority.

The M0-A02 bundle advances [M0-A02 #82](https://github.com/bdgrz/compliance/issues/82)
with a proposed retained-source manifest-regeneration default. Its Portia
query hydrates a frozen program-scope snapshot, verifies the retained v1
identity and amendment linkage, then recomputes the canonical manifest rather
than reading a snapshot projection or current source. It exposes one
tenant-scoped snake_case HTTP operation and a read-only MCP tool. Focused
tests and the standalone/split broker flow cover source integrity, outsider
and second-tenant non-disclosure, worker-stopped snapshot-directory lag,
supported worker replay, and immutable original identity after an amendment.
It does not accept the ADR or close #82 or [EN-03 #194](https://github.com/bdgrz/compliance/issues/194): the seven snapshot types, workforce consumer,
size/performance, package/signing/retention, and snapshot-specific recovery
requirements remain open.

The next unblocked authorization bundle is tracked by
[EN-01a backend #324](https://github.com/bdgrz/compliance/issues/324). It advances
[M0-A04 #84](https://github.com/bdgrz/compliance/issues/84),
[M0-A07 #126](https://github.com/bdgrz/compliance/issues/126),
[R1-15 backend #152](https://github.com/bdgrz/compliance/issues/152),
[EN-01 backend #157](https://github.com/bdgrz/compliance/issues/157), and
[R1-01 backend #158](https://github.com/bdgrz/compliance/issues/158). It makes
the shared Portia composition fail closed, protects OIDC continuation in the
Portia pipeline, and restricts tenant lifecycle reactions to the system actor.
It does not settle field restrictions, cross-client firm access, federation
expansion, the durable denied-action logging contract, or each later feature's
tenant-isolation evidence; those records remain open.

Keep the artifact, authorization, and import work visible as decision-bound
slices. The next unblocked evidence bundle is M0-A04 and M0-A07: extend the
existing Portia tenant-context and authorization evidence without treating it
as acceptance of firm-access, separation-of-duties, federation, revocation, or
log-retention policy. M0-A03 still needs a selected production content store,
and EN-05 still needs an accepted multi-record failure/acceptance rule. Do not
create feature-local substitutes for those decisions.

## Synthetic documentation PR policy

When a child or discovery issue is already complete, create a small synthetic
documentation PR rather than manufacturing new behavior. Record the exact
merged PR or decision commit, acceptance evidence, known limits, and links to
the closed issue. Synthetic PRs improve discoverability and tracking; they do
not close an open child and do not replace missing backend proof.

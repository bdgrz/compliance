# Backend delivery ledger

Status: reviewed against `main` at `2acb5f1` on 2026-09-21.

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
| Boundary versioning, snapshots, and shared interval rules | [#240](https://github.com/bdgrz/compliance/pull/240), [#242](https://github.com/bdgrz/compliance/pull/242), [#243](https://github.com/bdgrz/compliance/pull/243), [#244](https://github.com/bdgrz/compliance/pull/244), [#245](https://github.com/bdgrz/compliance/pull/245), [#247](https://github.com/bdgrz/compliance/pull/247), [#248](https://github.com/bdgrz/compliance/pull/248) | [EN-02 #160](https://github.com/bdgrz/compliance/issues/160), [R1-02a #162](https://github.com/bdgrz/compliance/issues/162), [EN-03 #194](https://github.com/bdgrz/compliance/issues/194), and [R1-10a #211](https://github.com/bdgrz/compliance/issues/211) remain open until their complete authorized API/MCP, replay, lag, and split-host criteria pass. |
| Application inventory, source revisions, and change impact | [#249](https://github.com/bdgrz/compliance/pull/249), [#250](https://github.com/bdgrz/compliance/pull/250), [#252](https://github.com/bdgrz/compliance/pull/252), [#258](https://github.com/bdgrz/compliance/pull/258), [#259](https://github.com/bdgrz/compliance/pull/259) | [EN-05 #195](https://github.com/bdgrz/compliance/issues/195), [R1-10b #213](https://github.com/bdgrz/compliance/issues/213), and [R1-10d #217](https://github.com/bdgrz/compliance/issues/217) retain import provenance, authorization, failure recovery, and complete projection proof. #258 adds pre-acceptance HTTP cancellation; #259 upgrades Portia 0.5.3 and exposes the bounded stage MCP command. |
| Control, commitment, and risk drafts | [#251](https://github.com/bdgrz/compliance/pull/251), [#253](https://github.com/bdgrz/compliance/pull/253), [#254](https://github.com/bdgrz/compliance/pull/254) | [R1-05 #197](https://github.com/bdgrz/compliance/issues/197), [R1-13 #229](https://github.com/bdgrz/compliance/issues/229), and [R1-07 #199](https://github.com/bdgrz/compliance/issues/199) retain owner, applicability, review, activation, cross-record consistency, and risk acceptance gaps called out by the merged PRs. |
| Operator portfolio and platform tenancy | [#256](https://github.com/bdgrz/compliance/pull/256) | [R1-15 #237](https://github.com/bdgrz/compliance/issues/237) remains open for the product parent and [#239](https://github.com/bdgrz/compliance/issues/239) remains open for frontend delivery; backend child #238 is closed. |

## Next grouped implementation loop

The next implementation bundle is the dependency-ordered R1-10a application
inventory follow-up, grouped with any shared authorization or source-authority
contracts it needs. Keep #126, #123, and #139 visible as blockers until their
organization-specific or canonical-model questions are resolved. The existing
application history, import, and bounded impact slices remain useful evidence,
but their children stay open until the issue-specific acceptance gates pass.

## Synthetic documentation PR policy

When a child or discovery issue is already complete, create a small synthetic
documentation PR rather than manufacturing new behavior. Record the exact
merged PR or decision commit, acceptance evidence, known limits, and links to
the closed issue. Synthetic PRs improve discoverability and tracking; they do
not close an open child and do not replace missing backend proof.

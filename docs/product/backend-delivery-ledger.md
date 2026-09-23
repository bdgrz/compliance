# Backend delivery ledger

Status: reviewed on 2026-09-23. Issue counts are a point-in-time readback.

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

## Current backend queue

The live backlog has 78 open backend records: 31 in R1, 21 in R2, 6 in T1,
6 in T2, 7 in T3, and 7 in F1. Three backend children are closed: [R1-15a
#238](https://github.com/bdgrz/compliance/issues/238), [EN-01a
#324](https://github.com/bdgrz/compliance/issues/324), and [AUTH-02a
#360](https://github.com/bdgrz/compliance/issues/360). These counts describe
tracking, not delivered coverage; partial implementations remain open.

| Order | Reviewable capability | Backend records and exit gate |
| --- | --- | --- |
| 1 | Identity and tenant foundation | Merged [AUTH-02a #360](https://github.com/bdgrz/compliance/issues/360) supplies recoverable production email verification. [R1-15 #152](https://github.com/bdgrz/compliance/issues/152), [EN-01 #157](https://github.com/bdgrz/compliance/issues/157), and [R1-01 #158](https://github.com/bdgrz/compliance/issues/158) then complete verified self-service, in-product operator authority, firm-staff denial, tenant authorization, and program scope. [R1-04a–e #183](https://github.com/bdgrz/compliance/issues/183) follow for member activation, scoped grants, teams, deprovisioning, and separation of duties. |
| 2 | Governed R1 source records | Complete version/history and boundary work [#160](https://github.com/bdgrz/compliance/issues/160), [#161](https://github.com/bdgrz/compliance/issues/161), [#162](https://github.com/bdgrz/compliance/issues/162), and [#246](https://github.com/bdgrz/compliance/issues/246), then criteria [#209](https://github.com/bdgrz/compliance/issues/209), application/system instances [#211](https://github.com/bdgrz/compliance/issues/211), manual workforce [#219](https://github.com/bdgrz/compliance/issues/219), technology [#227](https://github.com/bdgrz/compliance/issues/227), commitments [#229](https://github.com/bdgrz/compliance/issues/229), and providers [#231](https://github.com/bdgrz/compliance/issues/231). Use separate `SystemInstance` aggregate ownership from #211. |
| 3 | R1 assurance decisions | Finish application scope/change [#215](https://github.com/bdgrz/compliance/issues/215)/[#217](https://github.com/bdgrz/compliance/issues/217), workforce changes/NHI/snapshot [#221](https://github.com/bdgrz/compliance/issues/221)/[#223](https://github.com/bdgrz/compliance/issues/223)/[#225](https://github.com/bdgrz/compliance/issues/225), snapshot enabler [#194](https://github.com/bdgrz/compliance/issues/194), controls [#197](https://github.com/bdgrz/compliance/issues/197)/[#198](https://github.com/bdgrz/compliance/issues/198), risks [#199](https://github.com/bdgrz/compliance/issues/199), and the owned readiness gap plan [#200](https://github.com/bdgrz/compliance/issues/200). Governed artifact storage [#196](https://github.com/bdgrz/compliance/issues/196) needs the external [Portia S3 adapter #69](https://github.com/cntryl/portia/issues/69) before evidence consumers close. |
| 4 | R2 operation and review | Deliver the 21 R2 backend children as dependent control/policy/evidence, access-review, work, finding, advisor, and Type I entry capabilities. Start the access population at manually attested [#273](https://github.com/bdgrz/compliance/issues/273); use [F1-07 engagement assignments #269](https://github.com/bdgrz/compliance/issues/269) before advisor access [#275](https://github.com/bdgrz/compliance/issues/275). |
| 5 | Examination periods and firm operations | Complete T1's six, then T2's six, then T3's seven backend children against exact frozen versions and delivery history. The seven F1 backend children can advance once their R1/R2 records exist; firm-staff access requires an accepted engagement assignment and independence rules, not standing tenant membership. |

Accepted import semantics remain in [EN-05 #195](https://github.com/bdgrz/compliance/issues/195),
but it is P1 and blocks only its import frontend and [R1-10b application
import #213](https://github.com/bdgrz/compliance/issues/213). M0-D05–D08 put
manual governed records and attested snapshots first. HRIS and provider import
implementation slices should be created after real source shapes in
[#347](https://github.com/bdgrz/compliance/issues/347) and
[#353](https://github.com/bdgrz/compliance/issues/353) are validated. Do not
make first-release R1/R2 backend children wait for an unscheduled connector.

For each bundle, implement authorized HTTP and machine-appropriate MCP,
Portia guards, event-sourced domain behavior, Fitz projections/reactors, and
standalone/split-host proof. Close a backend child only after its own allowed,
denied, cross-tenant, replay/concurrency, lag, and recovery acceptance passes
with merged exact-head CI and Native AOT. Personal email proof, invitation
acceptance, acknowledgements, attestations, approvals, and sign-offs are
HTTP-only. Keep the product parent open for UI and integrated acceptance.

## Earlier completed backend and discovery records

These rows preserve the earlier merged implementation trail. They are not an
exhaustive inventory of accepted M0 decisions; the linked issue closures and
decision records are authoritative.

| Record | Merged implementation or decision | Evidence and limits |
| --- | --- | --- |
| [M0-D22 #79](https://github.com/bdgrz/compliance/issues/79) | [PR #163](https://github.com/bdgrz/compliance/pull/163), merge `221b682296910200bb29f13863a5097d27cf45bf` | [Canonical ownership decision](decisions/m0-d22-canonical-ownership.md), affected issue updates, and exact-head CI are recorded in the issue closure comment. Organization-specific workforce and NHI authority remain with M0-D06. |
| [M0-D23 #80](https://github.com/bdgrz/compliance/issues/80) | [PR #166](https://github.com/bdgrz/compliance/pull/166), merge `97c77fe850745ca37e1c7176d678ae42a7fb137b` | [Assurance vocabulary decision](decisions/m0-d23-assurance-vocabulary.md), ten dependent issue updates, and exact-head CI are recorded in the issue. The later M0-D03 role decision is recorded separately. |
| [AUTH-01 #142](https://github.com/bdgrz/compliance/issues/142) | [PR #144](https://github.com/bdgrz/compliance/pull/144), merge `2b3bb8b375c741455160ddbad70aa0c240564c56`; session reflection in [PR #150](https://github.com/bdgrz/compliance/pull/150) | Development and OIDC identity registration, provider identity resolution, signed HTTP session cookies, and logout are backend-only. The issue records focused acceptance coverage; no browser UI is implied by this row. |
| [AUTH-02 #143](https://github.com/bdgrz/compliance/issues/143) | [PR #149](https://github.com/bdgrz/compliance/pull/149), merge `e46157ff647db6ef6f092cd3b954b83cc5f022ab` | Normalized email ownership, idempotent reservation, challenge hashing, verification transitions, owner-scoped reads, and HTTP routes are covered. That PR used `MockEmailChallengeDelivery`; the later production adapter is recorded under #360. |
| [AUTH-02a #360](https://github.com/bdgrz/compliance/issues/360) | [PR #363](https://github.com/bdgrz/compliance/pull/363), merge `d3087cbe7d06fe8d7686b3a50563825ea17afe4c` | Recoverable SMTP verification delivery and challenge retry have exact-head and post-merge CI proof in the closure comment. A live SMTP relay and production deployment were not exercised. |
| [EN-01a #324](https://github.com/bdgrz/compliance/issues/324) | [PR #325](https://github.com/bdgrz/compliance/pull/325), merge `da2ffde6cb950b19a97877367982576480a3bfb4` | Shared Portia composition fails closed with `RequireAuthorization` validation before Fitz startup; full handler registration, developer/OIDC/system-actor paths, Native AOT builds, and exact-head plus post-merge main CI are recorded in the PR and closure comment. [EN-01 #157](https://github.com/bdgrz/compliance/issues/157) remains open for first-consumer authorization and actor-attribution proof; its product decisions are accepted. |
| [R1-15a #238](https://github.com/bdgrz/compliance/issues/238) | [PR #256](https://github.com/bdgrz/compliance/pull/256), merge `01590b1fc1f86657f3c6096bdcf382232720a577` | Operator-only tenant inventory uses Fitz 1.4.1 primary paging without request-path writes. Portia authorization, hyphenated HTTP path segments with snake_case interpolated/query/JSON values, and read-only MCP contracts, cross-tenant non-disclosure, standalone/split broker proof, focused tests, exact-head CI, and post-merge main CI are recorded in the issue closure comment. Product parent #237 and frontend child #239 remain open. |

## Merged capability bundles that remain partial

These bundles are valuable backend progress, but none is a substitute for the
child issue's complete acceptance evidence.

| Capability bundle | Merged pull requests | Open child records and remaining gate |
| --- | --- | --- |
| R1-15 tenancy, membership, and split-host proof | [#151](https://github.com/bdgrz/compliance/pull/151), [#153](https://github.com/bdgrz/compliance/pull/153), [#154](https://github.com/bdgrz/compliance/pull/154), [#155](https://github.com/bdgrz/compliance/pull/155), [#164](https://github.com/bdgrz/compliance/pull/164), [#325](https://github.com/bdgrz/compliance/pull/325), [#361](https://github.com/bdgrz/compliance/pull/361), [#363](https://github.com/bdgrz/compliance/pull/363), [#364](https://github.com/bdgrz/compliance/pull/364) | [R1-15 backend #152](https://github.com/bdgrz/compliance/issues/152), [EN-01 #157](https://github.com/bdgrz/compliance/issues/157), and [R1-01 #158](https://github.com/bdgrz/compliance/issues/158) remain open. #361 adds verified self-service and firm-staff denial; #363 adds recoverable email challenge delivery; #364 adds the operator roster and an activation fence. #152 still depends on EN-01 acceptance; #157 needs broader actor, authorization, leak, and host-mode proof; #158 needs its Program-specific exit evidence. |
| Membership invitation lifecycle | [#185](https://github.com/bdgrz/compliance/pull/185), [#208](https://github.com/bdgrz/compliance/pull/208), [#263](https://github.com/bdgrz/compliance/pull/263), merge `bd63bad10a4cd6a3dff155ef2d3eb28d57f130c8` | [R1-04a backend #183](https://github.com/bdgrz/compliance/issues/183) remains open for identity replacement/recovery policy and proof, plus child-specific completion evidence. Accepted M0-D03 requires application-managed invitation email, not a provider-side invitation API; M0-A07 assigns lost-identity replacement, revocation, and recovery to #183. This change supplies a recoverable production SMTP adapter with at-least-once delivery; personal invitation acceptance remains HTTP-only. |
| Boundary versioning, immutable history, projection consistency, snapshots, and recovery replay | [#174](https://github.com/bdgrz/compliance/pull/174), [#175](https://github.com/bdgrz/compliance/pull/175), [#240](https://github.com/bdgrz/compliance/pull/240), [#242](https://github.com/bdgrz/compliance/pull/242), [#243](https://github.com/bdgrz/compliance/pull/243), [#244](https://github.com/bdgrz/compliance/pull/244), [#245](https://github.com/bdgrz/compliance/pull/245), [#247](https://github.com/bdgrz/compliance/pull/247), [#248](https://github.com/bdgrz/compliance/pull/248), [#318](https://github.com/bdgrz/compliance/pull/318), [#320](https://github.com/bdgrz/compliance/pull/320), [#321](https://github.com/bdgrz/compliance/pull/321), [#328](https://github.com/bdgrz/compliance/pull/328) | [M0-A01 #81](https://github.com/bdgrz/compliance/issues/81) accepts the application persistence and recovery boundary. The accepted [M0-A05 #85](https://github.com/bdgrz/compliance/issues/85) records projection-derived read semantics; product-owned calculations and cross-client rules remain in consuming backend work. [EN-02 #160](https://github.com/bdgrz/compliance/issues/160) remains open for complete reuse across owning contexts plus complete impact and deletion evidence. PR #321 proves retained-source Program recovery and application-projector replay under test. PR #328 extends that evidence with a controlled Docker tar restore into a distinct fresh local volume after source-volume loss, including archive checksum/manifest and pinned-image proof. Neither PR proves production backup/restore controls, measured achievement of the 15-minute RPO or 4-hour RTO, a global calculation snapshot, or cross-client authorization; whole-platform recovery delivery and its timed DevOps proof are tracked in [cntryl/portia#70](https://github.com/cntryl/portia/issues/70). |
| Application inventory, source revisions, and change impact | [#249](https://github.com/bdgrz/compliance/pull/249), [#250](https://github.com/bdgrz/compliance/pull/250), [#252](https://github.com/bdgrz/compliance/pull/252), [#258](https://github.com/bdgrz/compliance/pull/258), [#259](https://github.com/bdgrz/compliance/pull/259), [#261](https://github.com/bdgrz/compliance/pull/261) | [R1-10a #211](https://github.com/bdgrz/compliance/issues/211), [EN-05 #195](https://github.com/bdgrz/compliance/issues/195), [R1-10b #213](https://github.com/bdgrz/compliance/issues/213), and [R1-10d #217](https://github.com/bdgrz/compliance/issues/217) retain verified ownership, classification authority, source identifiers, restricted discovery, import provenance, authorization, failure recovery, and complete projection proof. #261 carries the tenant-declared classification through Portia, HTTP/MCP, Fitz current/history views, and previews while keeping it `classification_unverified`; #258 adds pre-acceptance HTTP cancellation; #259 upgrades Portia 0.5.3 and exposes the bounded stage MCP command. |
| Control, commitment, and risk drafts | [#251](https://github.com/bdgrz/compliance/pull/251), [#253](https://github.com/bdgrz/compliance/pull/253), [#254](https://github.com/bdgrz/compliance/pull/254) | [R1-05 #197](https://github.com/bdgrz/compliance/issues/197), [R1-13 #229](https://github.com/bdgrz/compliance/issues/229), and [R1-07 #199](https://github.com/bdgrz/compliance/issues/199) retain owner, applicability, review, activation, cross-record consistency, and risk acceptance gaps called out by the merged PRs. |
| Operator portfolio and platform tenancy | [#256](https://github.com/bdgrz/compliance/pull/256) | [R1-15 #237](https://github.com/bdgrz/compliance/issues/237) remains open for the product parent and [#239](https://github.com/bdgrz/compliance/issues/239) remains open for frontend delivery; backend child #238 is closed. |

## Earlier implementation notes

The following notes preserve the 2026-09-22 implementation context. The
current queue and accepted decisions above supersede their sequencing.

This M0 projection-consistency bundle records ADR 0007 and makes the existing
application boundary-reference lag result explicitly transient through its
authorized HTTP and read-only MCP contracts in split API/worker mode. The
cohosted standalone test proves outsider non-disclosure, not lag recovery. It
advances [M0-A05 #85](https://github.com/bdgrz/compliance/issues/85) and
[EN-02 #160](https://github.com/bdgrz/compliance/issues/160) without claiming
a readiness calculation, a work queue, or cross-client authority.

The earlier M0-A02 bundle advanced [M0-A02 #82](https://github.com/bdgrz/compliance/issues/82)
with proposed retained-source manifest regeneration. It recomputes a frozen
program-scope manifest from its event-sourced record after tenant authorization,
without relying on the snapshot projection or mutable source state. The
operation permits eight amendment links at most, bounding one regeneration to
nine snapshot hydrations; both new amendments and regeneration enforce the
limit. The standalone and split API/worker broker flow proves authorized and
denied HTTP/MCP behavior, projection lag recovery, concurrent bounded reads,
and original identity after an amendment. It advanced the snapshot decision
after M0-A01 acceptance, but did not close
[EN-03 #194](https://github.com/bdgrz/compliance/issues/194) before the
workforce consumer, content-identity decision acceptance, and application-level
incomplete-freeze and manifest-recovery evidence exist.

The completed [EN-01a backend #324](https://github.com/bdgrz/compliance/issues/324)
bundle, delivered in [PR #325](https://github.com/bdgrz/compliance/pull/325),
makes the shared Portia composition fail closed, protects OIDC continuation in
the Portia pipeline, and restricts tenant lifecycle reactions to the system
actor. [EN-01 backend #157](https://github.com/bdgrz/compliance/issues/157)
still owns first-consumer field restrictions, firm-staff denial, and
tenant-isolation evidence. The architecture decisions are now accepted;
implementation and proof remain.

Keep shared artifact and import behavior in their enablers. ADR 0006 selects
S3 storage through Portia, with adapter delivery tracked by
[Portia #69](https://github.com/cntryl/portia/issues/69). ADR 0005 defines
the all-or-nothing import acceptance barrier; [EN-05 backend
#195](https://github.com/bdgrz/compliance/issues/195) owns the worker
crash/restart proof when the P1 import capability is scheduled.

## Synthetic documentation PR policy

When a child or discovery issue is already complete, create a small synthetic
documentation PR rather than manufacturing new behavior. Record the exact
merged PR or decision commit, acceptance evidence, known limits, and links to
the closed issue. Synthetic PRs improve discoverability and tracking; they do
not close an open child and do not replace missing backend proof.

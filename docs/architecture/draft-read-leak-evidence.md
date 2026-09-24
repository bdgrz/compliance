# Draft read isolation evidence

Issue #157 tracks tenant-owned read isolation across the backend, HTTP API, and MCP. `DraftReadLeakMatrixE2ETests` supplies a two-populated-tenant broker case in standalone and split API/worker hosts. One signed-in member owns both tenants, and an unrelated signed-in member exercises denial.

Each tenant gets one program, one client service, two controls, two commitment drafts, and two risks. The first record of each kind is revised once, so current drafts reach revision 2 and history has revisions 1 and 2. Setup waits until each revised draft and history list reaches revision 2, and until each current-draft list contains every seeded record, before the test reads anything.

The test covers these 12 reads over both HTTP and MCP:

| Read | HTTP | MCP tool |
| --- | --- | --- |
| Control draft list | `GET …/programs/{program}/controls` | `bdgrz.control.draft.list` |
| Commitment draft list | `GET …/programs/{program}/commitment-drafts` | `bdgrz.commitment.draft.list` |
| Risk draft list | `GET …/programs/{program}/risks` | `bdgrz.risk.draft.list` |
| Control draft history | `GET …/controls/{control}/draft/revisions` | `bdgrz.control.draft.revisions.list` |
| Commitment draft history | `GET …/commitment-drafts/{draft}/revisions` | `bdgrz.commitment.draft.revision.list` |
| Risk draft history | `GET …/risks/{risk}/draft/revisions` | `bdgrz.risk.draft.revisions.list` |
| Current control draft | `GET …/controls/{control}/draft` | `bdgrz.control.draft.get` |
| Current commitment draft | `GET …/commitment-drafts/{draft}` | `bdgrz.commitment.draft.get` |
| Current risk draft | `GET …/risks/{risk}/draft` | `bdgrz.risk.draft.get` |
| Control draft revision | `GET …/controls/{control}/draft/revisions/1` | `bdgrz.control.draft.revision.get` |
| Commitment draft revision | `GET …/commitment-drafts/{draft}/revisions/1` | `bdgrz.commitment.draft.revision.get` |
| Risk draft revision | `GET …/risks/{risk}/draft/revisions/1` | `bdgrz.risk.draft.revision.get` |

Every list is followed with `limit=1` to its empty terminal page on both transports. The test checks tenant ID, program ID, parent record ID, the exact set of record IDs or revisions, and tenant-specific identifier and title or statement text. Exact reads check the same fields and the expected revision.

The member of both tenants runs these denial probes over HTTP and MCP:

- **Cursor transplant.** A cursor from tenant A's list is rejected on tenant B's matching list with `400` or `Validation`.
- **Record swap.** A's record ID under B's tenant and program returns `NotFound` for all 6 exact reads and all 3 history lists.
- **Tenant swap.** B's tenant ID with A's program and record IDs returns `NotFound` for all 12 reads.
- **Program swap.** A's tenant ID with B's program ID and A's record IDs returns `NotFound` for all 12 reads.

The unrelated member gets `NotFound` for all 12 of tenant B's reads over both transports.

This evidence covers only current drafts and draft history. It does not cover approved versions, other draft kinds, a stopped worker, or replay. The broader #157 inventory is in [tenant-read-leak-matrix.md](tenant-read-leak-matrix.md).

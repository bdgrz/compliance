# R1-03 Criteria catalog backend slice

This slice delivers the platform SOC 2 criteria catalog and explicit program
edition selection for [#209](https://github.com/bdgrz/compliance/issues/209),
following [M0-D02](decisions/m0-d02-criteria-content.md) and
[M0-D01](decisions/m0-d01-program-targets.md). Tenant-supplied licensed text
overlays are implemented in [#448](https://github.com/bdgrz/compliance/issues/448).
Supplier and license facts still come from [#351](https://github.com/bdgrz/compliance/issues/351);
they inform overlay content without blocking its data shape.
Catalog selection, mapping, and tenant-overlay entry are usable through product
actions without a licensed catalog import or external integration. An overlay's
supplier must still have the permitted-use authority described in M0-D02.

## Catalog content

- One immutable platform edition, `2017_tsc_2022_pof` (framework `tsc`), with a
  fixed `edition_id`. It holds all 61 numbered 2017 Trust Services Criteria
  (`CC1.1`–`CC9.2`, `A1.1`–`A1.3`, `C1.1`–`C1.2`, `PI1.1`–`PI1.5`,
  `P1.1`–`P8.1`). `identifier` equals `source_identifier` for every criterion.
- Summaries are short original Bdgrz wording. `content_rights` is
  `identifiers_and_original_summaries`; no AICPA text ships. A unit test
  rejects any summary that shares a five-word run with reference AICPA
  phrasing, starts with "The entity", or exceeds 140 characters.
- The AICPA publication does not number points of focus. Points of focus are
  first-class, mappable `point_of_focus` entries with local
  `bdgrz:focus:<criterion>:<slug>` identifiers, a null `source_identifier`,
  and exactly one parent criterion in the same category. Only a few are seeded.
- `is_complete` means every numbered criterion is present. Known limits are
  listed in `support_gaps`: `points_of_focus_partial` for every category, and
  `privacy_lifecycle_unsupported` for `privacy` (#349). Category selection
  itself lives on the boundary (`trust_services_categories`); the catalog does
  not infer or restrict it.
- Catalog construction rejects duplicate editions or identifiers, a criterion
  identifier that does not match its category's pattern (for example `CC` for
  `security`, `PI1.` for `processing_integrity`), orphan points of focus, and
  an edition without content rights or gap metadata. Each support gap needs a
  supported category, a nonblank code and note, and a unique category/code key
  within its edition.
- [Readiness assessments](readiness-catalog-support-gaps-v1.md) disclose the
  selected edition's applicable support gaps for the categories in the
  approved scope at the assessment time. They retain the exact declared note,
  edition provenance and historical result.

## Contract

All routes are tenant scoped and require tenant access. Names are snake_case.

| HTTP | MCP tool | Notes |
| --- | --- | --- |
| `GET /api/v1/tenants/{tenant_id}/criteria-editions` | `bdgrz.criteria.editions.list` | Read only |
| `GET .../criteria-editions/{edition_id}` | `bdgrz.criteria.edition.get` | 404 for unknown edition |
| `GET .../criteria-editions/{edition_id}/entries` | `bdgrz.criteria.entries.list` | `category`, `kind`, `parent_identifier`, `limit` (1–200, default 50), `cursor` |
| `GET .../criteria-editions/{edition_id}/entries/export` | `bdgrz.criteria.entries.export` | Same bounded filters and paging as the display list; export use is selected by the server |
| `GET .../criteria-editions/{edition_id}/entries/{identifier}` | `bdgrz.criteria.entry.get` | 404 for unknown identifier |
| `PUT .../criteria-editions/{edition_id}/entries/{identifier}/overlay` | `bdgrz.criteria.overlay.set` | `expected_revision`, `content`; requires organization-wide `program.manage` |
| `PUT .../programs/{program_id}/criteria-edition` | `bdgrz.program.criteria.select` (idempotent) | Body `expected_revision`, `edition_id`; requires `program.manage` |

Entry reads use the operation to select their purpose; callers do not provide a
purpose discriminator. The entries list and single-entry read are display
operations. The separate entries export operation is server-selected for
export use. Each operation returns overlay text only when its corresponding
usage flag allows it; otherwise it returns the platform-written summary.
Overlay writes are tenant-owned, version-checked and attributed; each accepted
revision is retained as an immutable event. Platform catalog data continues to
contain only identifiers and original summaries.

- Entry cursors are bound to the tenant, edition, filters, and operation. A
  display cursor cannot be used for export, or vice versa; a cursor used outside
  its scope returns 400 / `Validation`.
- Selection appends `ProgramCriteriaEditionSelected` to the program stream and
  advances the program revision. An unknown edition returns 400; an incomplete
  edition returns 409; a stale `expected_revision` returns 409 with the current
  revision. Re-selecting the already selected edition succeeds without an event,
  so a retried request is safe. Changing editions later is an explicit,
  attributed, version-checked remap; editions are never mutated.
- The program directory projection exposes `criteria_edition_id` on the current
  program and on every revision row, so history shows which edition applied at
  each revision. `minimum_revision` distinguishes projection lag as usual.

## Validation coverage

The following test sources describe intended and retained verification coverage.
This documentation update does not execute them; current run results and release
acceptance belong to the linked implementation issues and CI at the reviewed head.

- Unit: `PlatformCriteriaCatalogTests`, `CriteriaCatalogTests`,
  `CriteriaCatalogHandlerTests`, `ProgramCriteriaProjectionTests` (projection
  replay from source events).
- Contract: `ComplianceWebTests.ShouldDescribeCriteriaCatalogAndSelectionGivenOpenApi`,
  `RbacMcpScenarioTests` (tool list and annotations).
- Broker E2E, standalone and split API/worker: `CriteriaCatalogE2ETests`
  (catalog reads, concurrent select/revise, idempotent retry, projection),
  `ProgramAuthorizationMcpE2ETests` (participant reads, denied select),
  `CriteriaReadLeakMatrixE2ETests` (outsider, tenant-swap, program-swap, and
  cursor-transplant probes).

## Limits

- Point-of-focus coverage is partial; the full 2022 hierarchy still needs
  authored summaries and review against the source.
- Control mappings reference identifiers, never text. No other catalog export
  format is provided by this slice.
- Only one platform edition exists, so edition remapping is proven with test
  catalogs rather than a second shipped edition.

## Criterion applicability and coverage reads (#476)

- `POST .../programs/{program_id}/criterion-applicability` proposes that one
  criterion (not a point of focus) of the program's selected edition does not
  apply. `.../{decision_id}/reviews` accepts or rejects it; the proposer may
  review only under a waiver scoped to `criterion_applicability`.
  `.../{decision_id}/withdrawals` restores the criterion to coverage. Writes are
  HTTP-only; get and list are read-only MCP tools.
- Coverage reports `not_applicable` (with `not_applicable_decision_id`) for an
  accepted decision, then `mapped` or `unmapped`. A mapped control whose mapped
  version is no longer its current approved version reports
  `remap_required: true`; mappings are never moved silently.
- Coverage, mapping lists, and applicability lists read the
  `ControlMappingDirectoryV1` and `CriterionApplicabilityDirectoryV1` Fitz
  projections. A read before both reach every tenant source event returns a
  transient `409`.
- Licensed overlay text and usage-flag enforcement are delivered by #448.

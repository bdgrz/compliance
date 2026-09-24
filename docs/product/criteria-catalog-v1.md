# R1-03 Criteria catalog backend slice

This slice delivers the platform SOC 2 criteria catalog and explicit program
edition selection for [#209](https://github.com/bdgrz/compliance/issues/209),
following [M0-D02](decisions/m0-d02-criteria-content.md) and
[M0-D01](decisions/m0-d01-program-targets.md). The licensed text overlay
(`CriteriaTextOverlay`, supplier, license reference, usage flags) is not part
of this slice; it waits on [#351](https://github.com/bdgrz/compliance/issues/351).

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
  an edition without content rights or gap metadata.

## Contract

All routes are tenant scoped and require tenant access. Names are snake_case.

| HTTP | MCP tool | Notes |
| --- | --- | --- |
| `GET /api/v1/tenants/{tenant_id}/criteria-editions` | `bdgrz.criteria.editions.list` | Read only |
| `GET .../criteria-editions/{edition_id}` | `bdgrz.criteria.edition.get` | 404 for unknown edition |
| `GET .../criteria-editions/{edition_id}/entries` | `bdgrz.criteria.entries.list` | `category`, `kind`, `parent_identifier`, `limit` (1–200, default 50), `cursor` |
| `GET .../criteria-editions/{edition_id}/entries/{identifier}` | `bdgrz.criteria.entry.get` | 404 for unknown identifier |
| `PUT .../programs/{program_id}/criteria-edition` | `bdgrz.program.criteria.select` (idempotent) | Body `expected_revision`, `edition_id`; requires `program.manage` |

- Entry cursors are bound to the tenant, edition, and filters; a cursor used
  elsewhere returns 400 / `Validation`.
- Selection appends `ProgramCriteriaEditionSelected` to the program stream and
  advances the program revision. An unknown edition returns 400; an incomplete
  edition returns 409; a stale `expected_revision` returns 409 with the current
  revision. Re-selecting the already selected edition succeeds without an event,
  so a retried request is safe. Changing editions later is an explicit,
  attributed, version-checked remap; editions are never mutated.
- The program directory projection exposes `criteria_edition_id` on the current
  program and on every revision row, so history shows which edition applied at
  each revision. `minimum_revision` distinguishes projection lag as usual.

## Evidence

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
- No control mapping, overlay, export, or usage-flag enforcement yet.
- Only one platform edition exists, so edition remapping is proven with test
  catalogs rather than a second shipped edition.

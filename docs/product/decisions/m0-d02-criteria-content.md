# M0-D02: Criteria content source, edition, and permitted use

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.

| Question | Decision and rationale |
| --- | --- |
| Edition | The first platform edition is the 2017 Trust Services Criteria with the 2022 revised points of focus. |
| Permitted use | The product ships **no licensed AICPA text**. It ships criterion and point-of-focus identifiers (for example `CC6.1`) plus short, original product-written summaries. Full criteria text is available only from a catalog file that a firm or client supplies under its own AICPA license. This avoids depending on redistribution terms in an Apache-2.0 product. |
| Points of focus | Points of focus are modeled as first-class catalog entries and are mappable, not reference-only. |
| Initial catalog | The platform catalog (IDs plus original summaries) is platform-level content. A licensed text overlay is supplied per tenant or firm template; the supplier and license scope are confirmed in [#351](https://github.com/bdgrz/compliance/issues/351). |
| Later editions | A new edition is added side by side as a new immutable catalog edition. Existing programs and engagements keep the edition they selected; migration is an explicit, attributed remapping, never a mutation of an existing edition. |
| Multi-client use | Platform identifiers and original summaries may be used across all client organizations and in exports. Licensed overlay text is used only where the supplier's license permits; any restriction becomes a catalog usage flag. |

## Data-shape rules

- `CriteriaCatalogEdition`: platform-level, immutable once published. Fields:
  `edition_id`, `framework` (`tsc`), `edition_label` (`2017_tsc_2022_pof`),
  `published_at`. It is referenced by exact edition ID from a program.
- `Criterion`: `edition_id`, `identifier` (unique within edition), `category`
  (`security | availability | confidentiality | processing_integrity | privacy`),
  `kind` (`criterion | point_of_focus`), `parent_identifier` (a point of focus
  belongs to exactly one criterion), and `summary` (original product text).
- `CriteriaTextOverlay`: tenant- or template-owned. Fields: `edition_id`,
  `identifier`, `text`, `supplier`, `license_reference`, and `usage_flags`.
  Licensed text never lives in platform-level content or in the repository.
- A control mapping references `(edition_id, identifier)`, never text.

## Repository check

No criteria catalog or fixture is seeded in the repository today, so there is
no shipped text to test. When R1-03 seeds the platform catalog, it must add a
test asserting that the seed contains only identifiers, edition metadata, and
original summaries.

## Follow-up discovery

[#351](https://github.com/bdgrz/compliance/issues/351) confirms the licensed
catalog supplier and the firm's license scope. R1-03 does not wait on it.

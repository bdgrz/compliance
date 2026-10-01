# Manual provider register V1

Implements the independent authored-facts slice [#525](https://github.com/bdgrz/compliance/issues/525) of [#231](https://github.com/bdgrz/compliance/issues/231), using M0-D11, M0-D22, M0-D09 and ADR 0003. A declaration is neither an approved ProviderVersion nor an assurance or downstream scope decision.

## Authored facts

A Provider has a stable opaque identity owned by its tenant. Its governed name is unique among active providers in the tenant. All records in this slice are active; retirement belongs to later lifecycle work. `provider_kind` is bounded authored metadata rather than a new provider taxonomy.

Materiality is `material` or `not_material`, with `materiality_basis` drawn from `customer_data` and `critical_path`. Either exposure makes a provider material. Spend is not a basis. Missing classification, basis or rationale remains explicitly unresolved. A non-material classification cannot declare either exposure.

Subservice treatment defaults to `carve_out`. An explicit `inclusive` declaration requires a rationale. Carved-out subservices retain an unresolved `csocs` fact: this slice does not create or verify CSOC commitments, reviews, reports or coverage. #231 and #489 own the remaining diligence and readiness rules.

An accountable owner is an explicitly selected same-tenant canonical Person, including a Person without membership or login. Source-only owner text stays unresolved. The recording member is retained separately; an owner reference does not imply employment eligibility or grant access.

Dependencies explicitly reference a ClientService with its owning Program, or a SystemInstance with its owning Application. Authoritative records verify those relationships and capture the inspected subject and parent revisions. Legacy application-stream SystemInstance declarations remain valid canonical references. Source-only dependencies retain an unresolved reference. Each dependency has a rationale and a declared half-open interval `[effective_from, effective_until_exclusive)`; a supplied end must follow its start. Later source changes do not rewrite these observations. No atomic external current-at-commit guarantee is made; stronger fences belong to #519.

## Ordinary citation metadata

A typed citation retains `artifact_kind`, `title`, `version_or_date`, `locator` and optional same-tenant governed `artifact_id`. External locators are retained without fetching. A supplied citation must explicitly declare `metadata_classification` as `public` or `internal`; there is no default. This is the author's classification of the citation metadata, not verification of the referenced content, its authenticity, its handling class or its availability. In particular, an artifact reference does not expose artifact content or override its existing access policy.

Confidential, restricted or unclassified citation metadata is rejected by this register. The citation may be omitted and remain unresolved. Sensitive report/contract content, metadata classifications and field grants remain due-diligence work under #231. Do not paste confidential source content into ordinary register facts.

## Authority and retained history

Reads require active current client membership, an active tenant and organization-wide `tenant.access`. Writes require organization-wide `provider_inventory.manage`. Program-only scope does not authorize the tenant-wide register. Operator identity and standing firm membership provide no organization authority.

`ProviderInventoryGrantBackfillV1` replays new and retained tenant registrations and adds only the provider authoring permission to Org Admin and Compliance Lead roles. Existing bootstrap effects and unrelated permissions are preserved.

One event-sourced tenant register owns provider identities, name uniqueness, revisions and immutable logical request decisions. Revision writes require `expected_revision`; stale conflicts state the current revision. A retry with the same logical request and authored input returns its original decision without new events, even after canonical sources or provider facts advance. Server-captured source revisions and original attribution are retained. Request payloads are bounded before append; malformed nested inputs return validation errors.

Fitz current/history projection rows and their checkpoint commit together. Reads check authoritative source progress before returning current facts or an empty list. Minimum-revision reads return a transient conflict while the source or projection is behind. Exact historical reads preserve the requested payload. Provider and revision lists use scoped cursors with limits of 1–200; a foreign tenant, record or query cursor is rejected without exposing foreign identities.

## HTTP and MCP operations

All operations use the same Portia authorizers and handlers, with snake_case fields.

| Operation | HTTP | MCP |
| --- | --- | --- |
| Record | `POST /api/v1/tenants/{tenant_id}/providers` | `bdgrz.provider.record` |
| Revise | `PUT /api/v1/tenants/{tenant_id}/providers/{provider_id}` | `bdgrz.provider.revise` |
| Current | `GET /api/v1/tenants/{tenant_id}/providers/{provider_id}` | `bdgrz.provider.get` |
| List | `GET /api/v1/tenants/{tenant_id}/providers` | `bdgrz.providers.list` |
| Exact revision | `GET /api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions/{revision}` | `bdgrz.provider.revision.get` |
| Revision list | `GET /api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions` | `bdgrz.provider.revisions.list` |

Write bodies carry `content`; revisions also carry `expected_revision`. Current reads accept `minimum_revision`. List requests accept `limit` and `cursor`. MCP exposes authored facts only; later personal reviews, approvals and sign-offs remain HTTP-only workflows.

No existing event or projection schema is rewritten. Provider events, the V1 read projection and the narrow backfill checkpoint are additive. No uploads, import/connectors, scheduling, automatic links, UI or production lifecycle flags are added.

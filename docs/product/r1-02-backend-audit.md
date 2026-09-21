# R1-02 backend acceptance audit

The baseline backend child is
[#162](https://github.com/bdgrz/compliance/issues/162). Downstream impact and
successor completion belong to
[#246](https://github.com/bdgrz/compliance/issues/246). The product parent
[#8](https://github.com/bdgrz/compliance/issues/8) retains UI work. This audit
records partial backend delivery and does not claim these issues are complete.

## Delivered slices

- Boundary drafts, revisions, review and approval decisions, approved versions,
  effective-date history, and impact preview use Portia requests and
  event-sourced `SystemBoundary` state with Fitz projections. Personal review
  and approval are HTTP-only; machine authoring and reads use MCP where suitable.
- Client services are governed within a program. Manual Applications have
  tenant-owned aggregates containing SystemInstance records, with validated
  boundary links.
  Remaining subject types stay explicit unresolved references until their
  owning inventories and source authority exist.
- Applications and SystemInstances expose authorized, paged reverse boundary
  references over snake_case HTTP and read-only MCP. A separate per-tenant Fitz
  projector replays retained boundary events into current-draft and approved
  history rows. Reads return a conflict while that projector trails the source,
  including when an empty page would otherwise appear complete. Earlier
  revised or discarded drafts remain in retained boundary event history.
- Current-boundary reads accept `minimum_revision`. Immutable version, version
  list, and `/effective_version` reads accept `minimum_boundary_revision` over
  snake_case HTTP query strings and read-only MCP tool arguments. Boundary
  `/impact_preview` uses the same route convention. A caller may
  anchor these reads to a boundary stream revision returned by an earlier
  operation. When the source has reached that revision but Fitz has not, the
  query returns a recoverable conflict. When the source itself has not reached
  it, the query returns a source conflict. A missing tenant-scoped boundary or
  version remains not found.

## Remaining backend acceptance

Boundary-consuming inventory, evidence, risk, provider, readiness, and
engagement children can use the #162 baseline. Their completed impact
contributions and historical engagement binding are tracked in #246, which
depends on those later contexts. Until they exist, an incomplete preview blocks
successor approval rather than claiming a complete impact result.

- Implement governing inventories for person, component,
  information, data flow, process, location, provider, and commitment references
  in their owning stories, then validate those references on boundary writes.
- Add real impact contributors for controls, evidence, risks, providers,
  engagements, and readiness. `BoundaryImpactService` intentionally marks these
  contexts pending and blocks successor approval while they are absent.
- Prove complete successor approval and the exact boundary-version binding of
  historical engagement snapshots after those dependent contexts arrive.
- Complete inherited M0 decisions that require engagement facts, organization
  policy, or recovery targets. Do not infer them from the backend defaults.

The revision anchor describes a specific boundary stream, not an atomic
snapshot across downstream inventories. The impact digest and approval guard
remain the change gate; a client must not treat a version read as a readiness or
auditor opinion.

## Verification and tracking

The original history-read slice proved approved-version lag and catch-up with
a real Fitz broker and live OpenAPI checks for snake_case
`/effective_version` and `/impact_preview` routes. The later manual inventory
and boundary-reference slices were reviewed and merged in
[PR #244](https://github.com/bdgrz/compliance/pull/244) and
[PR #245](https://github.com/bdgrz/compliance/pull/245). Their exact-head CI
runs passed Validate, dependency review, and Native AOT containers on amd64
and arm64. PR #245 additionally proved a stopped split worker returns a
projection-lag conflict, then reads both governed references after replay.

The broker integration suite now gives each test class fresh broker history
through class or dedicated collection fixtures within a nonparallel collection.
Each fixture uses a unique Compose project and mapped port. This bounds test-history
contention; it does not establish production startup performance with many
tenants. That investigation remains in
[Portia #58](https://github.com/cntryl/portia/issues/58). Backend children
[#160](https://github.com/bdgrz/compliance/issues/160),
[#162](https://github.com/bdgrz/compliance/issues/162), and
[#246](https://github.com/bdgrz/compliance/issues/246) remain open for the
acceptance gaps above and their inherited decisions.

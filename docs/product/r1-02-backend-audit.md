# R1-02 backend acceptance audit

The backend child is [#162](https://github.com/bdgrz/compliance/issues/162). The
product parent [#8](https://github.com/bdgrz/compliance/issues/8) retains UI work.
This audit records partial backend delivery and does not claim that either issue
is complete.

## Delivered slices

- Boundary drafts, revisions, review and approval decisions, approved versions,
  effective-date history, and impact preview use Portia requests and
  event-sourced `SystemBoundary` state with Fitz projections. Personal review
  and approval are HTTP-only; machine authoring and reads use MCP where suitable.
- Client services are governed within a program. Other scope subjects remain
  explicit unresolved references until their owning inventories exist.
- Current-boundary reads accept `minimum_revision`. Immutable version, version
  list, and effective-version reads accept `minimum_boundary_revision` over
  snake_case HTTP query strings and read-only MCP tool arguments. A caller may
  anchor these reads to a boundary stream revision returned by an earlier
  operation. When the source has reached that revision but Fitz has not, the
  query returns a recoverable conflict. When the source itself has not reached
  it, the query returns a source conflict. A missing tenant-scoped boundary or
  version remains not found.

## Remaining backend acceptance

- Implement governing inventories for person, application, component,
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

## Local verification for the history-read slice

Locked restore, warning-free Release build, `dotnet format`, client checks,
265 nonbroker tests, and 16 broker tests passed. The broker suite uses one fixed
Compose project and port, so it must run without another local broker suite.
Hosted exact-head checks and merge evidence remain pending.

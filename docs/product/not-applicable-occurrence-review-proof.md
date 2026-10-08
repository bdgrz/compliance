# Not-applicable occurrence review acceptance

Refs #278. A `not_applicable` attestation already requires a rationale and enters `submitted`, the same independent review path as other attestation results. It is not automatically a failed control or a finding. This increment changes tests and documentation only.

Three focused composed cases prove this previously uncovered result-specific path:

- A real personal HTTP owner attestation retains `not_applicable`, the owner recorder and the rationale, and appears as one projected occurrence-review item for the assigned reviewer. Giving the performer ordinary program management authority still does not permit self-review; the exact source refusal and unchanged ledger position are asserted.
- An independent personal HTTP decision binds the exact attestation ID and version. Approval retires review work; deferral preserves it. Fresh tenant projections rebuilt from the retained event store return the same item IDs and counts, and another catch-up creates no duplicates.
- A returned decision removes pending review work. A personal correction retains the old attestation and review, creates a superseding version, and returns that occurrence to the reviewer. An attempt to approve the old attestation at the current revision fails with the original latest-version refusal and no append. Approval of the corrected version binds version 2 and retires the work after retained-event replay.

The work item is intentionally occurrence-scoped: correction recreates the same work identity rather than inventing a new identity scheme. Source revision and exact attestation checks remain authoritative when recording a decision.

The tests use production `AddCompliance` request composition and registered tenant projectors through `ProjectorRunner`, backed by an in-memory retained event store and fresh Fitz KV projections. Native operating-plan and control source setup comes from the existing `OperationsFixture`; actor/grant, tenant-activity and membership directory doubles are explicit test scaffolding. This is backend unit acceptance, not broker, deployment, browser, real-client authorization or governed evidence-content proof. Referenced evidence remains unresolved under the existing #196/#272 boundary.

No source policy, automatic finding, HTTP/MCP contract, grant, schema, reminder behavior or migration changes. Remaining #278 evidence resolution, exact evidence versions, and other unproved criteria keep the parent open. No delivery forecast decrement is implied by this acceptance proof.

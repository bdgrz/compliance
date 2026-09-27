# Boundary decision actor attribution

Boundary review and approval events store the typed `actor` snapshot alongside
the existing member ID and display fields. Legacy events derive the typed actor
from those fields. `BoundaryDecisionView` exposes the same snapshot and falls
back to its existing fields when reading older Fitz directory rows. The reader
does not resolve the member's current identity.

`SystemBoundaryTests.ShouldSnapshotActorsGivenReviewAndApprovalDecisions` checks
the aggregate events. `BoundaryActorSnapshotTests` covers event and directory
row compatibility. `BoundarySnapshotReadLeakMatrixE2ETests.ShouldScopeBoundaryAndSnapshotReadsGivenTwoTenants`
checks decision actor snapshots over HTTP and MCP for review and approval
decisions in standalone and split API/worker modes.

This slice covers decision actors. Boundary draft authorship events continue to
carry their existing member ID and display fields. Identity replacement and
deprovisioning policy remains in [#183](https://github.com/bdgrz/compliance/issues/183).

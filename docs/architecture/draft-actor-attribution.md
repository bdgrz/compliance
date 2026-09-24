# Draft actor attribution

Control, Commitment, and Risk draft create and revise events now save a
Compliance `actor` snapshot containing the tenant member ID and the display at
the time of the action. Control discard events save the same snapshot. The
existing member ID and display fields, event versions, and stream addresses
remain unchanged; older events without `actor` derive it from those fields.

Current draft detail and list views expose `last_changed_by`. Immutable
revision list and exact revision views expose `actor`. Both values derive from
the member ID and display already saved in the projections, so old projection
rows remain readable. Rendering the recorded actor does not look up that
member's current identity; normal authorization of the reader still applies.

`DraftActorSnapshotTests` covers changed displays for one member ID, all seven
new event snapshots, and legacy event/projection JSON plus aggregate replay
without the typed fields.
`DraftActorSnapshotE2ETests` checks the three create/revise paths, stored
events, and HTTP/MCP current and immutable history in standalone and split
API/worker hosts. Developer-session relinking supplies distinct test displays;
production identity replacement and deprovisioning policy remain in
[#183](https://github.com/bdgrz/compliance/issues/183).

Control discard removes the draft from its existing public reads, so its actor
is available on the durable event rather than a public revision view. The
existing discard release gate still applies.

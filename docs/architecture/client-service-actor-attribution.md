# Client service actor attribution

Client service create, revise, and retire events carry an `actor` snapshot with
`kind`, stable tenant member `id`, and display at the time of the action. The
existing `actor_member_id` and `actor_display` fields remain for event replay.
Older events without `actor` derive the same typed value from those fields.
Event names, versions, and stream addresses remain unchanged.

The client service projection stores the saved member ID and display for each
immutable revision. Current views expose `last_changed_by`; revision list and
exact revision views expose `actor`. These values come from the event and
projection data. Rendering the recorded actor does not resolve that member's
current identity; normal authorization of the reader still applies. Later
display changes do not rewrite earlier attribution.

`ClientServiceActorE2ETests` exercises create, revise, and retire with three
different developer-session displays for one member ID. It checks stored
events, current and immutable HTTP/MCP views, and source replay in standalone
and split API/worker hosts. `ClientServiceTests` verifies legacy event and
projection JSON without typed actor fields. Developer-session relinking is a
test mechanism; this slice does not define production identity replacement,
revocation, or lost-identity recovery policy under [#183](https://github.com/bdgrz/compliance/issues/183).

# Application actor attribution

Application declaration and revision events, and new system instance
registration events, save an `actor` snapshot with the tenant member ID and the
display at the time of the action. The legacy member ID and display fields,
event versions, and stream addresses remain unchanged. Older events without
`actor`, including legacy application-stream system instance declarations,
derive the typed value from those fields.

Current application views expose `last_changed_by`; application revision views
expose `actor`; system instance views expose `declared_by`. The application
directory projection persists the actor snapshot and falls back to legacy
member fields when reading rows written before the typed fields existed.
Rendering an actor does not resolve the member's current identity, and normal
authorization of the reader still applies.

`ApplicationActorSnapshotTests`, the application aggregate tests, and
`FitzApplicationDirectoryTests` cover snapshot creation, legacy event fallback,
projection replay, and serialization of legacy read rows. This slice does not
define production identity replacement, revocation, or lost-identity recovery
policy under [#183](https://github.com/bdgrz/compliance/issues/183).

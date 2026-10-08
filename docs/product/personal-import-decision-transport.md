# Personal import decision transport

Correlation, acceptance and cancellation are attributed personal import decisions. AcceptApplicationImportHandler, CorrelateApplicationImportRowHandler and CancelApplicationImportHandler require native HttpInvocation plus a canonical non-system Bdgrz actor before reading source state. HTTP-only route/tool exposure remains part of the public contract; handler enforcement also denies direct internal and native MCP dispatch with a valid personal identity.

The existing production authorizers still require current client membership, active tenant and ordinary import management grants. Source identity, plan freezing, batch revision, target claims, OCC, cancellation durability and changed-intent replay remain governed by the existing source ledger. Trusted ExecuteApplicationImport and ApplyApplicationImportEffect commands retain their separate system and persisted-plan authority. Staging, reads and previews retain their existing transport contracts.

Six genuine AddCompliance → IRequestBus RED cases persisted valid Direct/MCP decisions before this correction. GREEN checks retain exact source positions/revisions and states on denial; three native HTTP positives retain correlation, a frozen plan attributed to the canonical member, or durable cancellation. Existing import fixtures use a local explicit HTTP context for their intentional personal decisions; global RequestContext and Scenario defaults remain Direct.

This is a correction funded from Sprint 1 reserve. It does not count the same acceptance criteria twice or supply retirement effect authority. Broader import criteria remain open. No wire, route, event, projection or deployment schema changes are introduced.

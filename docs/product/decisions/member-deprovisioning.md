# Tenant membership deprovisioning

Status: accepted product decision, 2026-10-03. Decision owner: product owner.
Recorded in [#492](https://github.com/bdgrz/compliance/issues/492); implemented
for [#444](https://github.com/bdgrz/compliance/issues/444).

Deprovisioning permanently terminates one membership episode in one tenant. It
is separate from suspension, which remains reversible. A deprovisioned person
cannot be reinstated; a fresh invitation creates a new episode after the prior
episode's tenant authority cleanup completes.

Any tenant RBAC manager may deprovision another member through the HTTP API.
Self-deprovisioning is refused, and the last active tenant administrator cannot
be deprovisioned. The command records the actor, timestamp, and reason.

Deprovisioning removes current team assignments and active member-scoped access
grants prospectively. It retains the platform identity, Person, event history,
business records, and unfinished work. The person loses prospective access to
those records under the same authorization rules as a suspended member.

The event history remains the durable lifecycle record across invitations. A
new invitation does not restore old team assignments or member-scoped grants.
Each direct member grant is bound to the membership episode that was active when
it was issued, so retained or delayed grant history from a prior episode cannot
become effective after rejoining.

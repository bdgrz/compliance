# Provider management independence guard

Eight concrete provider mutations additionally require the shared retained-history
client management guard: provider record/revision, assurance report record/revision,
provider review, coverage gap record/closure and risk acceptance linking. Existing
organization-wide provider authority, resource ownership and source validation
remain mandatory.

`IProviderManagementRequest` also includes `PreviewProviderChange`, so that broader
interface remains unmarked. Reads, histories and change previews preserve their
ordinary authorization and source/projection freshness fences. No change preview
grants mutation or assurance authority.

Production-composed request-bus tests demonstrate the missing historical denials
before the annotations, then verify active and closed Attest history denials over
HTTP/MCP dispatch, retained supplier state unchanged after a denied revision,
real provider and setup projection catchup for successful reads/previews, and
successful retained writes with Advisory or other-client Attest history. Missing
ordinary grants still deny without any history. Membership and grant dependencies
are doubled; aggregate events, projections, handlers and dispatch are real.

Synthetic internal assignment fixtures prove this consumer boundary only. They do
not supply real professional approval, ratified production rules, partner duties,
professional access or cross-stream acceptance/write atomicity. Broader issue #277
acceptance remains open.

# Operational separation-of-duties waiver management

RecordSeparationOfDutiesWaiver and ApproveSeparationOfDutiesWaiver are explicit client management mutations. The existing supplemental authorizer reads canonical actual assignment history and denies these writes for any historical Attest assignee of this client, including a closed engagement. Existing current membership, tenant activity and tenant.rbac.manage authorization remain mandatory.

The independent approver must still differ from the requester and beneficiary. Beneficiary eligibility, source scope, expiry, attributed request and approval history remain governed by the existing waiver domain. GetSeparationOfDutiesWaiver remains an ordinary authorized read. No broad waiver administration interface is marked; native bootstrap and personal operations remain under their existing boundaries.

Fourteen production-composed cases exercise real retained waiver streams. Four behavioral REDs recorded or approved a valid waiver before the two annotations; GREEN denies those writes and retains the prior source position. Ordinary client, Advisory history and another client’s Attest history remain eligible, subject to ordinary grants and independent approval. Synthetic accepted history is test evidence only.

These operational waivers do not override the professional independence wall or provide professional acceptance authority. No route, wire schema, event schema, deployment or raw-byte retention behavior changes. The compound criteria under #277 remain open. This consumer belongs to the existing client administration delivery slice.

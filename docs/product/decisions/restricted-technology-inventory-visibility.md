# Restricted technology inventory visibility

This decision records the product direction accepted in [#492](https://github.com/bdgrz/compliance/issues/492#issuecomment-5979326833) for [#465](https://github.com/bdgrz/compliance/issues/465).

The backend operation contract is in [Restricted technology inventory reads, version 1](../technology-inventory-restricted-reads-v1.md).

## Decision

- The separate `technology_inventory.restricted.read` permission applies only to information assets and data flows whose current classification is exactly `restricted`. `confidential` continues to trigger the M0-D08 encryption requirements and does not restrict reads.
- `technology_inventory.manage` remains the permission for ordinary technology inventory operations. Restricted visibility is checked separately on reads.
- Tenant Administration and Compliance Management receive `technology_inventory.restricted.read` through recorded role-permission assignments. Other callers need an active access grant scoped to the exact tenant-local record, with resource type `information_asset` or `data_flow`; the granting role must hold the restricted-read permission.
- Access to one data flow does not grant access to its information assets. Program, boundary, application, SystemInstance, owner, and related-record relationships do not imply access to either record.
- Direct reads of unauthorized restricted records return NotFound. Lists, history, boundary references, and impact previews filter records before pagination or aggregation. Historical revisions are checked using their own classification.
- Resource-scope validation resolves the record from its tenant source. Missing, foreign-tenant, unsupported, or deleted resources cannot create an effective grant.

## Delivery boundary

This backend slice owns authorization, source and projection consistency, and unit coverage. Client changes remain in their client repositories and should consume the backend contract after this slice is independently complete.

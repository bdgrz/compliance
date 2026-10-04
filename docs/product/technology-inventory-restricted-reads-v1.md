# Restricted technology inventory reads, version 1

This document specifies the backend behavior for [restricted technology inventory visibility](decisions/restricted-technology-inventory-visibility.md), accepted for [issue #465](https://github.com/bdgrz/compliance/issues/465). It complements the inventory data model in [M0-D08](decisions/m0-d08-technology-inventory.md) and tenant authorization in [ADR 0002](../architecture/decisions/0002-authorization-and-tenant-isolation.md).

## Authorization

All technology inventory operations first require active tenant membership and `technology_inventory.manage`. An organization-wide permission permits the ordinary request family. An exact resource grant can supply this permission for reads of that information asset or data flow; it does not authorize mutations, inventory components, or unrelated records. List and history reads enforce the permission on each record before returning it. The restricted-read check is an additional read rule for only these records:

| Record | Restricted when | Resource type for a record grant |
| --- | --- | --- |
| Information asset | `classification` is exactly `restricted` | `information_asset` |
| Data flow | `classification` is exactly `restricted` | `data_flow` |

`confidential` continues to require encryption in transit and at rest under M0-D08. It does not make a record restricted for read authorization.

An organization-wide `technology_inventory.restricted.read` permission permits reading every restricted technology inventory record in the tenant when the caller also has its base inventory authority. Org Admin and Compliance Lead receive both permissions through recorded role assignments; their existing role IDs remain stable. Other callers need an active access grant for the exact tenant-local resource. The granting role must hold `technology_inventory.manage` and, for a restricted record, `technology_inventory.restricted.read`.

The grant is issued through `POST /api/v1/tenants/{tenant_id}/access-grants/{grant_id}` with a `GrantAccess` request. The scope has `kind: "shared_resource"`, the record's `id`, and `resource_type` set to `information_asset` or `data_flow`. The API validates that the source aggregate exists in that tenant before recording the grant. Existing `principal`, `role_id`, `source`, and effective-time requirements continue to apply.

For example, the scope in a grant proposal is:

```json
{
  "kind": "shared_resource",
  "id": "<tenant-local-record-id>",
  "resource_type": "information_asset"
}
```

An information asset grant does not cover a flow that carries the asset. A data-flow grant does not cover its information assets. Program, boundary, application, SystemInstance, owner, and other record relationships do not expand a grant.

## Read operations

The request authorizer first enforces tenant membership and base inventory
authority. A caller with neither standing authority nor an eligible resource
grant is denied before the handler runs. After admission, an unauthorized
record has the following behavior:

| Operation | Behavior for an unauthorized restricted record |
| --- | --- |
| `GET /api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}` | Returns NotFound. |
| `GET /api/v1/tenants/{tenant_id}/information-assets` | Omits the record before filling the requested page. |
| `GET /api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/revisions` | Returns NotFound when the current asset is restricted; otherwise omits each restricted historical revision before filling the page. |
| `GET /api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/boundary-references` | Returns NotFound and does not query the reference index when the asset is restricted. |
| `POST /api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/change-previews` | Returns NotFound when the asset is restricted and omits restricted flows from the preview scan. |
| `POST /api/v1/tenants/{tenant_id}/providers/{provider_id}/change-impact-previews` | Omits restricted flows and linked assets from derived impact sections, checking source classifications before relying on inventory projections. |
| `GET /api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}` | Returns NotFound. |
| `GET /api/v1/tenants/{tenant_id}/data-flows` | Omits the record before filling the requested page. |
| `GET /api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}/revisions` | Returns NotFound when the current flow is restricted; otherwise omits each restricted historical revision before filling the page. |

For a caller admitted through an exact resource grant, these operations also
omit unrelated ordinary records or return NotFound for a direct read. Grant
expiration, revocation, and current membership eligibility are checked through
the same authoritative access-grant readers used by other scoped operations.

List page limits apply to visible rows. The server can scan additional projection pages to fill a page after filtering. A cursor advances past rows consumed during that scan, and does not provide a total count of hidden records. Technology-inventory list and history reads return a transient conflict if their projection changes while the filtered page is being read.

Direct asset and flow reads recheck authoritative source visibility after loading
the projected row. If a concurrent change makes the record restricted to the
caller, the result is NotFound. If its source revision changes while remaining
visible, the result is a transient conflict. Asset boundary-reference reads
apply the same final visibility and revision check before returning the page.
These checks detect changes observed during the read; they do not introduce a
transaction spanning inventory, permission, and boundary streams.

Change previews use the same row visibility rules before counting scanned flows or deriving impact from linked assets. Hidden records do not affect the preview's result or its scan-limit indication.

## Delivery ownership

This is a backend contract. Web, iOS, and Android repositories consume it through their pinned API contract; they do not reproduce these authorization rules. Unit acceptance lives with the backend handlers, grant validation, and access projection.

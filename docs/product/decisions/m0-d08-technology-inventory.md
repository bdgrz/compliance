# M0-D08: Minimum technology, information, and data-flow inventory

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.

| Question | Decision and rationale |
| --- | --- |
| Material component categories | The first boundary is inventoried at account and environment granularity, not per resource: cloud accounts or subscriptions, environments, networks or VPCs, data stores, source repositories, and endpoint classes. Resource-level inventory is heavier and adds no Type I value for a small team. |
| Information classification | Four levels: `public`, `internal`, `confidential`, `restricted`. Each `InformationAsset` carries exactly one level and a retention reference. Evidence retention values follow M0-D16. |
| Data-flow granularity | A data flow runs from a service or system instance to a data store (or to an external party). Each flow records the information assets carried, classification, `encrypted_in_transit`, and `encrypted_at_rest` at the destination. Protection expectations are derived from classification: `confidential` and `restricted` require both encryption flags. |
| Sources | The governed Compliance inventory is authoritative. The cloud console, MDM, and repository host are discovery-only sources used to find gaps. No connector is built now (product direction, 2026-09-22). |

## Data-shape rules

- `InfrastructureComponent` (or the canonical equivalents): `component_category` ∈ `cloud_account | environment | network | data_store | repository | endpoint_class`, a governed name, an owner, an environment reference, a location reference, and a lifecycle. `cloud_account` components correspond one-to-one with `SystemInstance` records of kind `aws_account` and are not duplicated.
- `InformationAsset.classification` ∈ `public | internal | confidential | restricted` (required).
- `DataFlow`: `source` (a service or system instance), `destination` (a data store or external party), `information_asset_ids[]` (1..*), `purpose`, `encrypted_in_transit`, and `encrypted_at_rest`. It is effective-dated and versioned. It is invalid when a carried asset is `confidential` or `restricted` and either encryption flag is false, unless an approved exception is referenced.
- An endpoint class (for example "managed macOS laptops") is a class record with a count and a management source. Individual devices are out of scope for R1.

## Canonical model alignment

This uses `Network`, `InformationAsset`, `DataFlow`, and `Location`. The
component-category vocabulary, the classification vocabulary, and the
encryption fields on `DataFlow` are alignments requested on M0-D28.

## Follow-up discovery

None. Entering the first client's inventory is ordinary R1-12 use, not
discovery.

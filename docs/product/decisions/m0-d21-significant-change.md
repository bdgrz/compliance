# M0-D21: Compliance-facing significant change and incident facts

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D21 #78](https://github.com/bdgrz/compliance/issues/78).

## Minimum facts

Compliance records compliance-facing facts and supporting references without
copying the ITSM or incident tool.
An authorized user may record a change or incident directly when the
organization has no source tool or integration. External `source_system`,
`source_identifier`, and `source_url` are optional provenance in that case;
the product retains its own record identity, recorder, dates, assessment,
and supporting evidence. When an external source exists, its supplied
identifiers and locator remain provenance rather than a required connection.

| Record | Fields |
| --- | --- |
| `SignificantChange` | `source_system`, `source_identifier`, `source_url`, `title`, `occurred_at` (or an effective date range), `significance`, `significance_rationale`, `handling_class`, the affected boundary, commitment, control, or inventory references, and `assessed_by` and `assessed_at` |
| `ComplianceIncident` | `source_system`, `source_identifier`, `source_url`, `title`, `detected_at`, `resolved_at`, `significance`, `significance_rationale`, `handling_class`, `customer_data_affected` (bool), `availability_sla_affected` (bool), the affected references, and `assessed_by` and `assessed_at` |

`ComplianceIncident` is the native compliance-facing record owned by T2-07.
It may link to an earlier R1-07 `IncidentReference`, the externally sourced
risk-context reference defined in [M0-D22](m0-d22-canonical-ownership.md).
That external reference retains its required source identity, occurrence time,
summary, and provenance; linking it does not rewrite earlier risk history.
Optional external provenance on a manually recorded `ComplianceIncident` does
not weaken the source requirements of an `IncidentReference`. These are
conceptual product names; implementation naming and any history-preserving
adaptation remain with their owning delivery slices.

The two classification fields are distinct:

- `significance`: `significant` or `not_significant`, a recorded human
  assessment with a required rationale;
- `handling_class`: the M0-D16 vocabulary, `confidential` by default.

Neither record stores ticket bodies, logs, or personal data beyond the actor
attribution.

## Significance rule

A change is **significant** when it does any of the following:

- changes the system boundary;
- changes a service commitment or system requirement;
- changes a key control;
- changes infrastructure that hosts in-scope data.

An incident is **significant** when it is a security incident that affects
customer data, or one that affects an availability SLA.

## Restricted detail

- **Auditor visibility:** every significant item's reference, dates,
  significance, rationale, and affected-record links are included in the
  authorized engagement access or delivered package, according to M0-D17 and
  M0-D27. This holds regardless of `handling_class`, so period close and the
  examination see the complete set. It does not create direct product access
  for an external audit firm.
- **Source detail:** stays in the source tool when one exists. For a manually
  recorded item, supporting detail is supplied as governed evidence rather
  than a recreated incident-management workflow.
- **`handling_class: restricted`:** limits who may view and edit the item
  internally to the Compliance Lead, the Org Admin, the assessor, and engagement
  auditors. It never removes an item from the auditor-visible significant set.

## Effect on the system description and period close

- **System description:** a `significant` change or incident flags every
  `SystemDescription` section that references an affected record as
  `needs_review` (T1-02). The flag clears only with a new approved section
  version, or a recorded "no change needed" decision.
- **Period close:** T3-01 close validation lists every significant item in
  the period and fails while any flagged section is unresolved.

## Consequences

[T2-07 #37](https://github.com/bdgrz/compliance/issues/37) and
[#308](https://github.com/bdgrz/compliance/issues/308) adopt these fields,
the significance rule, and the close effect.

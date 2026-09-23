# M0-D21: Compliance-facing significant change and incident facts

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D21 #78](https://github.com/bdgrz/compliance/issues/78).

## Minimum facts

Compliance holds references, not a copy of the ITSM or incident tool.

| Record | Fields |
| --- | --- |
| `SignificantChange` | `source_system`, `source_identifier`, `source_url`, `title`, `occurred_at` (or an effective date range), `significance`, `significance_rationale`, `handling_class`, the affected boundary, commitment, control, or inventory references, and `assessed_by` and `assessed_at` |
| `IncidentReference` | `source_system`, `source_identifier`, `source_url`, `title`, `detected_at`, `resolved_at`, `significance`, `significance_rationale`, `handling_class`, `customer_data_affected` (bool), `availability_sla_affected` (bool), the affected references, and `assessed_by` and `assessed_at` |

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

- **Auditor visibility:** engagement auditors see every significant item's
  reference, dates, significance, rationale, and affected-record links. This
  holds regardless of `handling_class`, so period close and the examination
  see the complete set.
- **Source detail:** stays in the source tool, and the auditor requests it
  there.
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

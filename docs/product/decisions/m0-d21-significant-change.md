# M0-D21: Compliance-facing significant change and incident facts

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D21 #78](https://github.com/bdgrz/compliance/issues/78).

## Minimum facts

Compliance holds references, not a copy of the ITSM or incident tool.

| Record | Fields |
| --- | --- |
| `SignificantChange` | `source_system`, `source_identifier`, `source_url`, `title`, `occurred_at` (or an effective date range), `classification`, `significance_rationale`, the affected boundary, commitment, control, or inventory references, and `assessed_by` and `assessed_at` |
| `IncidentReference` | `source_system`, `source_identifier`, `source_url`, `title`, `detected_at`, `resolved_at`, `classification`, `customer_data_affected` (bool), `availability_sla_affected` (bool), the affected references, and `assessed_by` and `assessed_at` |

Neither record stores:

- ticket bodies;
- logs;
- personal data beyond the actor attribution.

## Significance rule

A change is **significant** when it does any of the following:

- changes the system boundary;
- changes a service commitment or system requirement;
- changes a key control;
- changes infrastructure that hosts in-scope data.

An incident is **significant** when it is a security incident that affects
customer data, or one that affects an availability SLA.

Classification is a recorded human assessment. The only values are
`significant` and `not_significant`, and each needs a rationale.

## Restricted detail

- **Record classification:** both records are `confidential` by default
  (M0-D16).
- **Auditor visibility:** auditors see the reference, classification,
  rationale, dates, and affected-record links. Source detail stays in the
  source tool, and the auditor requests it there.
- **Incident access:** an incident marked `restricted` is visible only to the
  Compliance Lead, the Org Admin, and the assessor.

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

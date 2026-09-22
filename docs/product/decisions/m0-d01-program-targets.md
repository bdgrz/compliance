# M0-D01: Program targets and engagement facts

Status: decided, 2026-09-22. Decision owner: Jeff Repanich, product owner.
The 2026-09-19 product defaults below stand. The 2026-09-22 decision selects
the first engagement's Trust Services categories and moves the remaining
organization facts to follow-up discovery issues.

| Question | Product decision and rationale |
| --- | --- |
| Program path | A program retains one identity through readiness, Type I, and Type II. These are planned stages, not claims that an examination or operating period happened. An engagement and its outcome are separate records. |
| Dates | Program dates are optional targets. A target is never promoted to an auditor-confirmed date by passage of time, stage display, or an imported note. Confirmed dates require an attributable engagement-owned decision or auditor communication. Plan revision preserves earlier targets and author attribution. |
| Category selection | A boundary records intended Trust Services categories explicitly. `security` is always present; the other four categories add to it. No optional category is inferred from a program name or target path. Selecting a category does not claim that the product supports every category-specific requirement. |
| First engagement categories | All five categories are in scope: `security`, `availability`, `confidentiality`, `processing_integrity`, and `privacy`. Rationale: the first client's commitments span uptime, confidential customer data, processing, and personal information, and the product must model every category. |
| Privacy | Privacy is selected, so a separate personal-information lifecycle backlog is required before the product claims Privacy support ([#349](https://github.com/bdgrz/compliance/issues/349)). Until then, Privacy criteria may be mapped to ordinary controls, but no readiness, package, or export claims lifecycle support. |
| First service scope | A boundary may carry a typed, owned unresolved service reference before the ClientService register exists. It cannot claim a governed service identity from an arbitrary UUID. The governed service and its exact boundary relationship must later be validated. |
| Milestone dates | No R1, R2, or T1 due date is invented from a program target. Milestones stay undated until an agreed plan exists. |

## Data-shape rules

- `SystemBoundary.trust_services_categories`: a set of 1–5 distinct values from
  `security | availability | confidentiality | processing_integrity | privacy`.
  It must contain `security`. Draft create and revise reject any other shape.
  This invariant is enforced by `SystemBoundary.Validate` and covered by
  `SystemBoundaryTests.ShouldRequireSecurityCategoryGivenOptionalCategoriesOnly`.
- Program target dates are nullable `target` values with author and revision.
  An engagement-confirmed date is a distinct attributed value with an evidence
  reference, never a status flag on a target.
- A missing audit firm, date, or service is represented as unknown, not as a
  placeholder value.

## Follow-up discovery

- [#348](https://github.com/bdgrz/compliance/issues/348) records the first
  client, the Type I as-of target, the Type II observation period, the audit
  firm and its confirmed dates, the first boundary services, and milestone
  due dates. Building the program, boundary, and engagement records does not
  wait on it.
- [#349](https://github.com/bdgrz/compliance/issues/349) defines the Privacy
  personal-information lifecycle backlog. It blocks the Type I package
  ([T1-05 #28](https://github.com/bdgrz/compliance/issues/28)).

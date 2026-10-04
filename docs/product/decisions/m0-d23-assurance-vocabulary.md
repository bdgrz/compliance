# M0-D23 assurance vocabulary and readiness ownership

Status: accepted product default under the delegated M0 decision authority,
2026-09-19. Decision owner: product owner and tech lead. This decision defines
record ownership; it does not assert audit-firm practice or an audit outcome.
The rationale is to retain each source's meaning and history while letting
readiness, remediation, and review share links and calculations without a
generic aggregate that erases their different state transitions.

## Distinct records, explicit links

`Gap` is R1-08's assessment result: an explainable unmet readiness rule for an
exact assessment and as-of time. Its stable identity permits reassessment and
an owned action plan. `ProviderCoverageGap` is R1-14's provider-specific
uncovered assertion, period, service, or carved-out responsibility. It links to
a `Gap` when it affects readiness, but its source and closure remain with the
provider assessment. `ControlEvaluationDeviation` is R2-05's result for a
specific procedure and inspected item; it retains the procedure, exact inputs,
tester, and original result. A material deviation creates or links to an R2-07
`Finding`. `Finding` is the governed deficiency and corrective-work record;
it can arise from a gap, deviation, provider assessment, incident, imported
consultant observation, or examination item. Conversion preserves source text,
provenance, and links. None of these records silently changes into another or
closes merely because a linked record changes state.

Use `Waiver` for an authorized, time-bounded exception to a product expectation
or rule, such as access, separation of duties, or an occurrence. Record its
scope, approver, rationale, expiry, and compensating action. Use
`AuditTestException` for an auditor-identified exception to an examination
test in T3-04. It preserves auditor wording and response history; it is not an
approved waiver. UI text may explain both in familiar terms, but API and
domain types must remain distinct. R2-07 may relate either to a finding.

R1-07 owns the `RiskAcceptance` decision as part of risk treatment. It has an
approver, rationale, affected risk and records, expiry, review trigger, and
history. R2-07 links a finding to that decision and projects its current
effect; it does not create a second risk-acceptance authority. Acceptance does
not close the finding, erase corrective work, or count as remediation.

## Decisions in their owning workflows

EN-04 supplies a reusable immutable decision shape: exact subject and version,
decision kind, actor, role/scope, time, rationale, separation-of-duties result,
and any policy exception reference. Each workflow owns the request, permitted
outcomes, state transition, and decision history. R1-02 owns boundary review
and approval decisions; R1-05 owns control approval; R1-06 owns mapping review;
R1-13 owns commitment review and approval; EN-05 owns batch import acceptance;
R2-02 owns policy review and approval; R2-03 owns evidence review; R2-05 owns control
evaluation reviews. There is no universal `Review` aggregate or approval that
confers validity on unrelated records. Human approvals and personal sign-offs
remain HTTP-only; machine operations may author drafts and read decisions when
authorized. M0-D03 still determines firm-specific roles and approved small-
team separation-of-duties exceptions.

## One readiness rule owner

R1-08 owns versioned, explainable readiness rule definitions, input selection,
and assessment calculations. Each `ReadinessAssessment` records rule version,
source record versions, completeness, as-of time, output, and derived gaps.
R2-09 freezes an immutable `ReadinessSnapshot` of one reproducible R1-08
calculation and adds management's Type I entry decision; it does not redefine
readiness rules. T2-04 defines monitoring measures and forecast views over
current records and the same rule outputs, with separate measure versions and
as-of times; it cannot silently change a readiness verdict. A management
compliance review is a recurring control occurrence
([M0-D19](m0-d19-periodic-reviews.md), which closed T2-09). Its frozen evidence
set binds the exact readiness and monitoring versions management considered.
Its sign-off records management's decision, and the actions it requests are
R2-07 findings. All views retain
links to authorized underlying records, and missing or stale inputs remain
visible rather than becoming a positive score.

The firm's client-service `ServiceEngagement` in F1-07 and the client's
`AuditEngagement` in Type I/II workflows are different records. References
must name the kind and tenant; neither implies the other's authorization.

## Policy inputs and follow-up

M0-D03 defines waiver authority and small-team separation-of-duties rules;
M0-D13 defines evaluation results and minor/material deviations. M0-D10 owns
risk appetite, M0-D26 owns independence rules, and engagement decisions own
auditor-specific commitments. A severity rubric or workflow default not covered
by those records must be named as a delivery limitation with its owning story,
rather than described as an undecided M0 question. Affected stories must carry
these terms, decision links, and any remaining policy inputs before their
backend child is closed.

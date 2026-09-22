# M0-D13: Control evaluation procedures and tester independence

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D13 #70](https://github.com/bdgrz/compliance/issues/70).

## Procedures

A `ControlEvaluationPlan` version is authored in the product for each control
under evaluation. No consultant procedure library is imported (M0-D04); a
procedure is data the evaluator writes, not a product-supplied template. Each
procedure step records:

- `assertion`: one of `design`, `implementation`, or `evidence_sufficiency`;
- `method`: one of `inquiry`, `inspection`, `observation`, or `reperformance`;
- the inspected items, as exact record versions or artifact content references
  (ADR 0006);
- the expected condition.

The plan version is frozen when evaluation starts. A changed plan is a new
version and never rewrites results already recorded.

## Result vocabulary

- **Per step:** `met`, `not_met`, or `not_tested`, with a rationale.
- **Per assertion, and overall for the `ControlEvaluation`:**
  - `effective`;
  - `effective_with_exceptions`;
  - `ineffective`;
  - `not_tested`.

An overall `effective` requires every step for every assertion to be `met`.

## Deviations

A `not_met` step creates a `ControlEvaluationDeviation` (M0-D23). The
evaluator classifies it as `minor` or `material`:

- **`material`:** creates or links a `Finding` with a corrective action
  (R2-07) and requires a retest. The retest is a new evaluation of the same
  plan version or a successor.
- **`minor`:** needs a recorded disposition, `corrected` or `accepted_with_waiver`
  (a `Waiver` per M0-D23). The overall result is at best
  `effective_with_exceptions`.

A deviation never closes because a later evaluation passed. The retest links
to it explicitly.

## Who may evaluate and review

- **Evaluator:** the control owner **may** evaluate their own control's design
  and implementation. This is a small-team default.
- **Reviewer:** every evaluation requires an independent review. The reviewer
  must not be the evaluator, and self-review is blocked. This is the M0-D03
  separation-of-duties default, and the only relief is a recorded, time-bound
  SoD exception approved by an Org Admin, which is flagged in readiness and
  audit exports.
- **Competence:** the reviewer must hold the Compliance Lead role or be an
  Advisor assigned to the engagement. Competence is otherwise a firm
  procedure, not a product check.

## Consequences

[R2-05 #19](https://github.com/bdgrz/compliance/issues/19),
[R2-05a #110](https://github.com/bdgrz/compliance/issues/110), and
[#279](https://github.com/bdgrz/compliance/issues/279) adopt this vocabulary and
evaluator/reviewer rule. The reviewer rule is enforced through the authorization
model accepted in ADR 0002 (M0-A04).

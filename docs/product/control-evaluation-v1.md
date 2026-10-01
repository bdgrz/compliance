# Control evaluation v1: conclusions, independent review, and deviations

Backend contract for [R2-05b #289](https://github.com/bdgrz/compliance/issues/289),
[R2-05c #294](https://github.com/bdgrz/compliance/issues/294), and
[R2-05d #296](https://github.com/bdgrz/compliance/issues/296). It applies M0-D13 and
M0-D03 and reuses the R2-07 finding path. It does not change readiness rules (R1-08).

## Records and streams

| Record | Stream | Concurrency |
| --- | --- | --- |
| `ControlEvaluation` with its frozen procedure, step results, deviations, submissions, and review decisions | `control-evaluations/{program_id}` | Per evaluation (`expected_revision`) |

Reads hydrate the program stream, so they never lag. A deviation's
`routed_to_finding` status is read from the remediation ledger.

## R2-05b conclusions

- An evaluation targets the control version effective today. The control
  version's owner may evaluate their own control; otherwise a program manager
  (`program.manage`) evaluates. The starter is the evaluator of record.
- The procedure is frozen at start. Each step has an `assertion` (`design`,
  `implementation`, or `evidence_sufficiency`), a `method` (`inquiry`,
  `inspection`, `observation`, or `reperformance`), the exact inspected items
  (`record` or `artifact` with a reference and exact version), and an expected
  condition.
- A step result is `met`, `not_met`, or `not_tested`, with a rationale and the
  items actually inspected. `not_met` requires a deviation classified `minor` or
  `material`.
- Submission (HTTP-only, the evaluator's sign-off) concludes each of the three
  assertions exactly once. A conclusion the results do not support is
  rejected: no steps or an untested step cannot be `effective`, a material
  deviation is `ineffective`, and a minor one is at best
  `effective_with_exceptions`. The overall result is derived, never supplied.
  Type II operating effectiveness is not an evaluation assertion.

## R2-05c independent review

- Only a program manager (Compliance Lead or Org Admin) reviews, through HTTP
  only. The evaluator cannot review without a waiver scoped to
  `(control_evaluation, evaluation_id, evaluation_id, round, review)`.
- `accepted` closes the round. `changes_requested` reopens it with the results
  kept. `rejected` reopens it with every step to re-perform. Both start a new
  round. Every submission and review stays in history, and `latest_review`
  plus `accepted_overall` show the current valid outcome.

## R2-05d deviations and retest

- A submitted material deviation becomes an owned finding with a corrective
  action through the `ControlEvaluationDeviationFindingsV1` reactor. The
  finding ID derives from the deviation, so replay is idempotent. A minor
  deviation needs a `corrected` or `accepted_with_waiver` disposition before
  submission. The waiver must be approved, active, and scoped to
  `(control_evaluation, evaluation_id, deviation_id, round, approve)` for the
  evaluator. A deviation's identity is stable per evaluation step, so a
  resubmission after rejection reuses its finding. An untested step stays visible
  as `not_tested`.
- An accepted evaluation with a material deviation has the `retest_status`
  `required`. A retest is a new evaluation that names it, reuses its procedure
  by default, and links its material deviations. The original evaluation, its
  decision, and the finding are never changed. Its `retest_status` follows the
  latest retest: `in_progress`, `passed`, or `failed`.

## Product defaults awaiting confirmation

- Deviation findings use the occurrence-finding defaults: `medium` severity,
  due in 30 days, owned by the control version's owner member (or the
  evaluator).
- Readiness does not consume evaluation results yet. That is a separate R1-08
  change.
- The plan is authored inline when an evaluation starts. Independent
  versioned plan records stay with #279.

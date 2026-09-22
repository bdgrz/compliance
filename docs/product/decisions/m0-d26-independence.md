# M0-D26: Independence between advisory and attest services

Status: accepted, 2026-09-22. Decision owner: Jeff Repanich (product owner).
Closes [M0-D26 #124](https://github.com/bdgrz/compliance/issues/124). The
compartment mechanism is part of the authorization model in ADR 0002 (M0-A04).
The firm's quality-management partner ratifies the initial rule-set content in
[#343](https://github.com/bdgrz/compliance/issues/343).

## Enforced by the platform: a strict wall

1. **Person exclusivity.** No person may hold both an `Advisor` and an `Attest`
   assignment for the same client organization, at the same time or ever.
   The check covers the person's full assignment history for that client.
2. **Compartments.** Attest assignees cannot read advisory working-note
   records (`AdvisorFeedback`, `AdvisorHandoff` content, and advisory notes).
   Advisors cannot read attest-side request discussions marked `attest_internal`.
   Shared management records are visible to both, subject to normal grants.
3. **Look-back guard.** An attest `ServiceEngagement` cannot be accepted for a
   client that received control design, control implementation, or operation
   of controls from the firm within the 12 months before the examination
   period starts. The guard reads the client's `NonattestServiceRecord`
   history.
4. **Readiness-only clients.** A client whose only nonattest service was a
   readiness assessment is `conditionally_compatible`. Acceptance requires a
   recorded partner evaluation.

## Service classification

| Nonattest service | Classification |
| --- | --- |
| Readiness assessment | `conditionally_compatible` |
| Control design | `impairing`, within the 12-month look-back |
| Control implementation | `impairing`, within the 12-month look-back |
| Operating controls on the client's behalf | `impairing`, within the 12-month look-back |
| vCISO | `impairing`, within the 12-month look-back |

The classification lives in a versioned `IndependenceRuleSet`, so rule changes
are new versions and never rewrite past evaluations.

## Engagement acceptance record

`IndependenceEvaluation` records:

- the rule-set version;
- the nonattest services considered;
- the result: `compatible`, `conditionally_compatible`, or `impaired`;
- the partner evaluation, when conditional;
- management's acknowledgement of responsibility for nonattest services;
- the approver and the time.

An `impaired` result blocks acceptance, and there is no override.

## Advisory client later requesting an examination

A new attest `ServiceEngagement` goes through the same evaluation using the
client's full `NonattestServiceRecord` history. The advisory staff who served
the client are excluded from the attest team by rule 1.

## Firm procedure, not platform

The platform does not enforce:

- the firm's system of quality management;
- competence;
- financial-interest independence;
- partner rotation.

## Consequences

[F1-07 #134](https://github.com/bdgrz/compliance/issues/134),
[F1-08 #135](https://github.com/bdgrz/compliance/issues/135),
[#269](https://github.com/bdgrz/compliance/issues/269), and
[#277](https://github.com/bdgrz/compliance/issues/277) adopt these rules.

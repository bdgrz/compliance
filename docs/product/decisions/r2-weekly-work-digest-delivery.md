# R2 decision: weekly work digest delivery policy (#647)

Status: accepted product decision, 2026-10-10. Decision owner: product owner.
Operational owner: IT.

## Decision

Send at most one weekly email digest per tenant/member to an active tenant
member who has at least one assigned work item visible in the current
authorized queue for one or more Programs. The digest combines that member's
overdue items and items due in the next seven days across those Programs. It
is one member-level digest, not a separate email for each Program. Include a
visible assignment even when the source says an explicit separation-of-duties
waiver is required; the digest does not make the item actionable or grant the
waiver. The source queue remains authoritative for membership, assignment,
due state, action eligibility, and restricted-work visibility.

Define a digest week as the member's local calendar week from Monday 00:00 up
to, but not including, the following Monday 00:00. Its `week_of` is that
week's local Monday date. The unique dispatch key is `(tenant_id, member_id,
week_of)`. Schedule it for Monday at 09:00 in the member's validated time
zone, using that zone's calendar rules for the date (including daylight
saving changes). Use UTC when the member has no valid time zone. At the first
durable claim, freeze the week key, resolved time zone, local `week_of`, and
the corresponding scheduled UTC instant. A time-zone update applies only to
the next unclaimed week; it cannot move or create another dispatch for a
claimed week. Replays resume the same dispatch record. Do not backfill a
missed prior week's digest.

For this email digest, evaluate overdue and due-soon dates against the local
calendar date at the frozen Monday 09:00 schedule. Overdue means due before
that date; due soon means due on that date through the seventh calendar day
after it, inclusive. This date basis is an email selection rule, not a source
due-date mutation. The current in-product queue still derives `Today` from
UTC; #292 must make the email date basis explicit when implementing the
member-local policy.

Immediately before composing and sending, recheck active tenant membership,
the currently verified destination for the canonical user identity attached
to that membership, the member's email opt-out, and current queue/source
visibility. Skip email for an empty digest, an unverified or no-longer-current
destination, an opt-out, or inactive tenant membership. Record the skip for
that week so later queue changes do not trigger a second send. Email
preference and delivery outcomes never affect in-app reminders, source due
dates, overdue visibility, escalation, or source completion.

If the member has multiple verified addresses, choose the ordinal-first
currently verified address for the attached canonical user identity, matching
the existing [member presentation rule](../../../src/Compliance.Core/Features/Tenants/MemberPresentationEnrichment.cs).
Recompute and verify this choice before dispatch; do not deliver to a stale
address captured in an earlier schedule scan.

## Recipient scope and links

An eligible Program is one the active member can currently read. Include only
work assigned to that member and visible in the current authorized queue for
that Program. Recheck source eligibility and restricted-system visibility
before assembling the digest. Manager oversight, generic queue assignment, or
an email link must not grant access to an item. Keep tenant and Program scope
on every item and direct link; never combine items across tenants.

Each entry links to the canonical HTTPS application view for that exact
tenant, Program, and work item. The canonical HTTPS application origin must
be supplied by deployment configuration; #121 owns the client-facing route
and #292's dispatcher consumes that route and origin. This decision does not
select an unimplemented client path or claim that a canonical origin is
configured today. The link carries no bearer token or source
credentials, retains all three scope identifiers, and loads the item through
the current backend detail route,
`/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}`.
Do not use the queue's `ActionPath` or the API endpoint as the email
hyperlink. Opening the client link runs current API authorization and
visibility checks again; the email is not an authorization grant. Do not
include a restricted item when the recipient cannot currently see its source
system.

The existing [GetWorkDigestHandler](../../../src/Compliance.Core/Features/Work/GetWorkDigestHandler.cs)
query is Program-scoped and returns overdue and next-seven-day assigned work.
A dispatcher that implements this policy must aggregate that content across
the member's eligible Programs and deduplicate by stable work-item identity
before creating the one tenant/member/week dispatch. The existing
[WorkDigestView](../../../src/Compliance.Common/Features/Work/WorkDigestView.cs)
is likewise a Program query; its `DigestId` is not the cross-Program delivery
identity.

## Delivery, retries, and operator response

The configured STARTTLS SMTP relay remains the email transport. Do not claim
exactly-once physical delivery: SMTP acceptance and the durable application
outcome cannot be committed atomically, and a stable `Message-ID` does not
make the relay idempotent.

Atomically claim a unique tenant/member/week dispatch reservation before
calling SMTP. A repeated claim resumes or inspects that dispatch record; it
does not begin another SMTP call while the earlier outcome is unresolved.
Use a stable message identity for correlation and record attempt timestamps,
delivery status, and a redacted failure category. Keep provider credentials,
full message bodies, and source payloads out of diagnostic logs.

- Retry only a definite non-acceptance. Automatically retry each eligible
  definite transient rejection with configured backoff, within the same
  dispatch record and before the next weekly window. Configure a maximum total
  attempt count (including the initial attempt) and a retry deadline. If either
  bound is exhausted, persist a terminal `retry_exhausted` status visible to
  IT. Record a definite permanent rejection as `permanent_failure` for IT and
  do not automatically retry it.
- If acceptance is ambiguous—for example, the connection is lost after the
  relay may have accepted the message, or the worker stops before persisting a
  successful outcome—persist an `unknown` outcome for IT. Never automatically
  resend it. IT may authorize a same-window retry only after relay evidence
  establishes non-acceptance; otherwise leave the outcome unresolved.
- A confirmed acceptance is recorded as sent. If that outcome write fails,
  recovery treats the claimed attempt as ambiguous and does not send again.

IT owns production SMTP configuration and operational response to digest
delivery failures, as confirmed by the product owner in [the #647 planning
comment](https://github.com/bdgrz/compliance/issues/647#issuecomment-6096865235).
Operators can inspect the member/week dispatch identity, status, attempt time,
stable message identity, and redacted failure category without seeing source
payloads or provider secrets.

## Current implementation boundary

This is a product policy, not a claim that digest dispatch is implemented.
[GetWorkDigestHandler](../../../src/Compliance.Core/Features/Work/GetWorkDigestHandler.cs)
exposes Program-scoped content,
[WorkDigestView.DigestId](../../../src/Compliance.Common/Features/Work/WorkDigestView.cs)
includes the Program, and
[WorkDigestPreference](../../../src/Compliance.Core/Features/Work/WorkDigestPreference.cs)
supplies email opt-out. There is no weekly dispatcher or cross-Program
delivery ledger, and no member time-zone field. A validated member time-zone
source with UTC fallback is a prerequisite for member-local scheduling under
#292.

The existing [email-challenge reactor](../../../src/Compliance.Core/Features/UserIdentities/EmailChallengeDeliveryReactor.cs)
and [tenant-invitation reactor](../../../src/Compliance.Core/Features/Tenants/TenantInvitationDeliveryReactor.cs)
call SMTP before recording the outcome. A non-cancellation transport
exception is recorded as generic `delivery_failed`; it does not distinguish
definite non-acceptance from ambiguous acceptance. If SMTP accepts a message
and the subsequent sent-outcome write fails, the reactor throws and can replay
the source event, sending again. Those reactors and their retry behavior
cannot be reused unchanged for digests. Digest delivery needs a durable
reservation and transport outcomes that distinguish definite non-acceptance
from ambiguity. No separate email provider is justified by the current
evidence; the concrete gap is the missing outcome distinction and dispatch
ledger, not SMTP configuration or relay selection.

## Source decisions and Jev judgment

This record resolves the digest scheduling item tracked in
[#492](https://github.com/bdgrz/compliance/issues/492), and refines
[M0-D15](m0-d15-work-queue.md) for the weekly email schedule and delivery
boundary. It applies to the notification outcome in
[#121](https://github.com/bdgrz/compliance/issues/121) and the backend digest
capability in [#292](https://github.com/bdgrz/compliance/issues/292). M0-D15
remains unchanged: one weekly email per member for overdue and next-seven-day
items, email opt-out only, no per-item email, in-app reminders, and seven-day
escalation visibility without reassignment.

Jev-1.13.0 supplied advisory Choice judgments using the issue text, M0-D15,
current #292 behavior, current SMTP evidence, the confirmed IT owner, and the
selected policy constraints. The product owner selected the policy after
using Jev as a decision aid; its probabilities are not approval or
implementation evidence. These answer objects preserve the selected
structured output and probabilities from the calls.

```json
{
  "model": "jev-1.13.0",
  "answers": {
    "schedule_scope": {
      "type": "choice",
      "choice": "member_weekly_across_eligible_programs",
      "confidence": 1.0,
      "probabilities": {
        "member_weekly_across_eligible_programs": 1.0,
        "per_program_emails": 0.0,
        "single_tenant_digest": 0.0
      }
    },
    "smtp_outcome": {
      "type": "choice",
      "choice": "retry_definite_nonacceptance_only",
      "confidence": 1.0,
      "probabilities": {
        "retry_definite_nonacceptance_only": 1.0,
        "retry_all_transient_errors": 0.0,
        "require_idempotent_relay": 0.0
      }
    }
  }
}
```

```json
{
  "model": "jev-1.13.0",
  "answers": {
    "schedule_timezone": {
      "type": "choice",
      "choice": "validated_member_timezone_utc_default",
      "confidence": 1.0,
      "probabilities": {
        "validated_member_timezone_utc_default": 1.0,
        "monday_0900_utc_only": 0.0,
        "tenant_timezone": 0.0
      }
    }
  }
}
```

```json
{
  "model": "jev-1.13.0",
  "answers": {
    "week_identity": {
      "type": "choice",
      "choice": "member_local_week_key",
      "confidence": 0.88,
      "probabilities": {
        "member_local_week_key": 0.92,
        "utc_week_key": 0.08,
        "first_claim_only_key": 0.0
      }
    },
    "due_date_basis": {
      "type": "choice",
      "choice": "member_local_send_date",
      "confidence": 0.83,
      "probabilities": {
        "member_local_send_date": 0.89,
        "current_utc_date": 0.09,
        "reservation_utc_date": 0.02
      }
    }
  }
}
```

The timezone judgment included the current code constraint that no member
timezone field exists. It supports the selected member-local schedule only
with a timezone source added before dispatcher implementation; it does not
indicate that the source exists today.

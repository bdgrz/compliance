# Domain event universe and backfill list

Baseline: repository source and the accepted backlog reviewed 2026-10-17. This is the event counterpart to the [system command list](command-list.md) and [query/view-model list](query-and-view-model-list.md).

## Existing event contracts

The source inventory contains **236 `DomainEvent` contracts**. Their complete feature-grouped, source-linked list is in the [component inventory](component-inventory.md#existing-events). Those are coded event types; this does not by itself prove the owning command handlers emit them, persistence retains them, or projectors consume them.

## Backfill result

- **128 missing event contracts** support accepted, requirement-backed workflows below.
- **8 additional event contracts** belong to readiness-material import and stay deferred with that feature until its prerequisites and durable import boundary are implemented.
- The named universe is therefore **364 current-plus-required event contracts**, with **8 deferred import events** outside the immediate backfill.

These names were reconciled against current `DomainEvent` declarations, the pruned command inventory, and gap-analysis sections G01–G20. Event names retain business facts and outcomes; they are not a one-for-one mirror of every command or query. Existing events remain the owner when they already represent a required fact. Each backfill event is a typed contract for the listed story outcome. Payloads carry tenant and subject identity, revision, optional member attribution, and occurrence time; handlers still need to populate and emit them, and projectors still need to consume them.

## Required backfill by feature

### Evidence capture and support — EN-06, R2-03/04/05

**Requirement source:** [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence).

The generic evidence-to-record relationship and the durable issuance of authorized artifact access. Capture, inspection, and quarantine-release facts already have events.

Events to backfill:
- `EvidenceSupportLinked`
- `EvidenceArtifactDownloadIssued`

### Evidence handling, disclosure, holds, and disposition — R2-12

**Requirement source:** [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure); [M0-D16](decisions/m0-d16-evidence-handling.md).

Policy assignment, engagement/report hold lifecycle, exact-recipient disclosure, and authorized disposition intent/result. Existing artifact legal-hold and retention-basis events are reused.

Events to backfill:
- `ArtifactHandlingPolicyAssigned`
- `ArtifactEngagementHoldPlaced`
- `EngagementReportIssued`
- `ArtifactEngagementHoldReleased`
- `ArtifactDisclosureApproved`
- `ArtifactDisclosureDelivered`
- `ArtifactDisclosureRevoked`
- `ArtifactDispositionApproved`
- `ArtifactDispositionExecutionRequested`
- `ArtifactDispositionResultRecorded`

### Advisor handoffs and feedback — R2-08

**Requirement source:** [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md).

Scoped handoff selection/access transitions and immutable feedback/correction/response/resolution. The older proposed `AdvisorRequestWithdrawn` name is omitted because the required command plan uses close and revoke intents.

Events to backfill:
- `AdvisorRequestOpened`
- `AdvisorHandoffSelectionRevised`
- `AdvisorHandoffReadiedForReview`
- `AdvisorHandoffClosed`
- `AdvisorHandoffRevoked`
- `AdvisorFeedbackRecorded`
- `AdvisorFeedbackCorrected`
- `AdvisorFeedbackResponded`
- `AdvisorFeedbackResolved`

### Type I engagement and baseline — T1-01

**Requirement source:** [T1-01](backlog.md#t1-01-establish-the-type-i-engagement-and-point-in-time-snapshot).

Engagement lifecycle and frozen/amended exact baseline. T1-03 external auditor access is a P2 hypothesis and does not add an event family.

Events to backfill:
- `ExaminationEngagementOpened`
- `ExaminationEngagementRevised`
- `TypeIBaselineFrozen`
- `TypeIBaselineAmended`

### Management-authored system description — T1-02

**Requirement source:** [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description).

Authored revisions, source bindings, decisions, successor, examination binding, and generated deliverable provenance.

Events to backfill:
- `SystemDescriptionDraftCreated`
- `SystemDescriptionDraftRevised`
- `SystemDescriptionSourcesBound`
- `SystemDescriptionReviewed`
- `SystemDescriptionApproved`
- `SystemDescriptionSuccessorProposed`
- `SystemDescriptionNoChangeRecorded`
- `SystemDescriptionBound`
- `SystemDescriptionExportGenerated`

### Auditor requests, responses, and samples — T1-04, T3-03

**Requirement source:** [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md).

Request and sample history, authored response drafts, submission, disclosure decision, delivery, and final disposition.

Events to backfill:
- `AuditorRequestRecorded`
- `AuditorRequestRevised`
- `AuditorRequestAssigned`
- `AuditorRequestCommentRecorded`
- `AuditorRequestResolved`
- `AuditorRequestReopened`
- `SampleSelectionRecorded`
- `AuditResponseDraftCreated`
- `AuditResponseDraftRevised`
- `AuditorResponseSubmitted`
- `AuditorResponseDisclosureReviewed`
- `AuditorResponseDelivered`
- `AuditorResponseDispositionRecorded`

### Indexed audit packages — T1-05, T3-05

**Requirement source:** [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md).

Build intent/outcome, validation and explicit override, disclosure/delivery, and linked amendments.

Events to backfill:
- `AuditPackageBuildRequested`
- `AuditPackageBuilt`
- `AuditPackageBuildFailed`
- `AuditPackageValidated`
- `AuditPackageValidationOverrideApproved`
- `AuditPackageDisclosureApproved`
- `AuditPackageDelivered`
- `AuditPackageAmended`

### Audit observations, assertions, and outcomes — T1-06/07, T3-04/06

**Requirement source:** [T1-06](backlog.md#t1-06-review-auditor-findings-and-evidence), [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-04](backlog.md#t3-04-review-type-ii-findings-and-evidence), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome).

Auditor observations and findings remain distinct from management-authored assertions, representation letters, and examination outcomes.

Events to backfill:
- `AuditObservationRecorded`
- `AuditObservationFindingLinked`
- `AuditOutcomeRecorded`
- `TypeIIPlanProposed`
- `TypeIIPlanApproved`
- `ManagementAssertionRecorded`
- `RepresentationLetterRecorded`
- `ExaminationSignedOff`

### Observation periods and recurrence — T2-01/02/03

**Requirement source:** [T2-01](backlog.md#t2-01-start-the-type-ii-observation-period-from-the-approved-baseline), [T2-02](backlog.md#t2-02-operate-a-recurring-control-calendar), [T2-03](backlog.md#t2-03-capture-control-performance-and-review).

Period and schedule changes, approved Type II operating plan, durable recurring occurrence generation, and explicit occurrence completeness.

Events to backfill:
- `ObservationPeriodStarted`
- `ObservationPeriodAmended`
- `FutureControlScheduleRevised`
- `TypeIIOperatingPlanProposed`
- `TypeIIOperatingPlanReviewed`
- `TypeIIOperatingPlanApproved`
- `DueControlOccurrencesGenerated`
- `MissingOccurrenceRecorded`
- `ObservationPeriodEnded`

### Recurring governance and management reviews — T2-06/09, M0-D19

**Requirement source:** [T2-06](backlog.md#t2-06-complete-periodic-policy-risk-and-vendor-reviews), [T2-09](backlog.md#t2-09-review-management-oversight-and-monitoring); [M0-D19](decisions/m0-d19-periodic-reviews.md).

Use control occurrences and existing review workflows as M0-D19 directs; retain the management record and its follow-up actions without inventing separate policy/risk/vendor review aggregates.

Events to backfill:
- `GovernanceReviewScheduled`
- `ManagementReviewRecorded`
- `ManagementReviewActionRecorded`
- `ManagementReviewActionCompleted`

### Significant changes and incidents — T2-07

**Requirement source:** [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact); [M0-D21](decisions/m0-d21-significant-change.md).

Native change/incident facts, their assessed compliance impact, resolution, and linked accountable review work.

Events to backfill:
- `SignificantChangeRecorded`
- `SignificantChangeRevised`
- `SignificantChangeImpactAssessed`
- `ComplianceIncidentRecorded`
- `ComplianceIncidentRevised`
- `ComplianceIncidentImpactAssessed`
- `IncidentResolutionRecorded`
- `ImpactReviewWorkLinked`

### Period close and examination populations — T3-01/02

**Requirement source:** [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md).

Close proposal/approval/amendment and immutable population definition, facts, generation, classification, reconciliation, freeze, and export.

Events to backfill:
- `ObservationPeriodCloseProposed`
- `ObservationPeriodCloseFrozen`
- `TypeIIPeriodCloseApproved`
- `TypeIIPeriodCloseAmended`
- `AuditPopulationDefined`
- `AuditPopulationFactRecorded`
- `AuditPopulationGenerated`
- `AuditPopulationRowClassified`
- `AuditPopulationReconciled`
- `AuditPopulationFrozen`
- `AuditPopulationAmended`
- `AuditPopulationExportGenerated`

### Program continuity — T3-07

**Requirement source:** [T3-07](backlog.md#t3-07-roll-the-program-into-continuous-compliance-and-the-next-period).

Approved continuity plan and explicit successor-period/carry-forward links.

Events to backfill:
- `ProgramRolloverProposed`
- `ProgramRolloverApproved`
- `NextObservationPeriodStarted`
- `OpenRemediationCarriedForward`

### Methodology templates — F1-04

**Requirement source:** [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients).

Versioned platform template lifecycle, tenant application/provenance, successor disposition, and explicitly reviewed de-identified contribution.

Events to backfill:
- `MethodologyTemplateCreated`
- `MethodologyTemplateDraftRevised`
- `MethodologyTemplateVersionPublished`
- `MethodologyTemplateRetired`
- `MethodologyTemplateApplied`
- `TemplateSuccessorAdoptionProposed`
- `TemplateSuccessorDispositionRecorded`
- `MethodologyTemplateContributionProposed`
- `MethodologyTemplateContributionReviewed`
- `MethodologyTemplateContributionAccepted`

### Own-attest delivery and offboarding — F1-01/07/08

**Requirement source:** [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D27](decisions/m0-d27-attest-scope.md).

Required record-package/export manifest and attributable external delivery/verification, hold release, and terminal offboarding completion.

Events to backfill:
- `OwnAttestRecordPackagePrepared`
- `OwnAttestRecordDeliveryRecorded`
- `OwnAttestRecordDeliveryVerified`
- `DeliveredEngagementHoldReleased`
- `ClientExportRequested`
- `ClientExportBuilt`
- `ClientExportBuildFailed`
- `ClientExportDelivered`
- `TenantOffboardingCompleted`

### Professional authority and actual assignments — F1-07/08, M0-D26

**Requirement source:** [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md).

Ratified partner authority, personal independence evaluation, and actual accepted-engagement assignment. Reuse existing acceptance and revocation events.

Events to backfill:
- `IndependenceRuleVersionRatified`
- `PartnerIndependenceEvaluationRecorded`
- `ServiceEngagementStaffAssigned`

### Client-specific federation — F1-06, EN-01

**Requirement source:** [F1-06](backlog.md#f1-06-let-client-users-sign-in-through-their-own-identity-providers), [EN-01](backlog.md#en-01-provide-shared-authorization-and-actor-reference-foundations); [M0-A07](../architecture/decisions/0009-tenant-identity-federation-and-context.md).

Tenant-scoped issuer trust lifecycle and retained explicit configuration-test result. Secret material itself must never be embedded in an event.

Events to backfill:
- `TenantIdentityProviderConfigured`
- `TenantIdentityProviderTested`
- `TenantIdentityProviderDisabled`
- `TenantIdentityProviderCredentialsRotated`

### Application and commitment lifecycle — R1-10/13

**Requirement source:** [R1-10](backlog.md#r1-10-establish-the-application-inventory-and-review-scope), [R1-13](backlog.md#r1-13-record-service-commitments-system-requirements-and-user-responsibilities).

Explicit instance revision and historically safe commitment retirement; existing application/system-instance declare/revise/retire and commitment approval events continue to own their current facts.

Events to backfill:
- `SystemInstanceRevised`
- `CommitmentRetired`

## Deferred event family — readiness-material import

R1-09/EN-05 is a committed feature, but the command plan marks the multi-family import slice deferred until prerequisites are met. Do not implement a second import event stream until the handler design resolves how the existing application-import staging, sealing, effect-pending, commit, cancel, and failure facts are reused or generalized. If readiness-material imports retain a distinct family, these are the candidate lifecycle facts:

- `ReadinessMaterialImportStaged`
- `ReadinessMaterialImportRowResolved`
- `ReadinessMaterialImportAccepted`
- `ReadinessMaterialImportCanceled`
- `ReadinessMaterialImportExecutionStarted`
- `ReadinessMaterialImportEffectPending`
- `ReadinessMaterialImportCommitted`
- `ReadinessMaterialImportFailed`

## Reuse, exclusions, and event rules

- Reuse existing evidence facts for artifact registration, inspection, quarantine release, legal holds, retention basis, and redaction. Reuse existing application-import lifecycle facts for application inventory imports.
- Reuse existing service-engagement acceptance/revocation, independence reevaluation, and assignment-revocation facts. The backfill adds only the missing partner ratification/evaluation and actual assignment facts.
- Periodic policy, risk, provider, and management review occurrences reuse M0-D19 control-occurrence and review/finding workflows; do not create parallel policy/risk/vendor-review event families.
- Exclude T1-03 direct external-auditor access events, T2-08 integration/agent events, F1-03 capacity-planning events, BO-01/02/03 handover/billing/add-on events, and Privacy lifecycle events while those remain optional, unvalidated, or policy-gated. T1-04 request/response records and T1-05 packages remain in scope.
- Weekly digest scheduling/delivery outcomes remain decision-gated under G08. The command names in the provisional command inventory do not establish the delivery-consent and retry policy needed for durable event contracts.
- A projector rebuilds from retained facts; it does not decide policy. Events require tenant identity, stable resource/revision identity, actor/time where attributable, and exact source-version references where history or reproducibility depends on them. External effects must distinguish committed intent from observed provider completion.

## Sources

[Backlog](backlog.md), [command list](command-list.md), [command inventory gap analysis](command-inventory-gap-analysis.md), [component/source inventory](component-inventory.md), [domain model](domain-model.md), [event and projection architecture](../architecture/decisions/0007-read-models-projections-and-as-of-calculations.md), [M0-D14 advisor collaboration](decisions/m0-d14-advisor-collaboration.md), [M0-D16 evidence handling](decisions/m0-d16-evidence-handling.md), [M0-D17 audit deliverables](decisions/m0-d17-audit-deliverables.md), [M0-D19 periodic reviews](decisions/m0-d19-periodic-reviews.md), [M0-D21 significant change](decisions/m0-d21-significant-change.md), [M0-D26 independence](decisions/m0-d26-independence.md), [M0-D27 attest scope](decisions/m0-d27-attest-scope.md).

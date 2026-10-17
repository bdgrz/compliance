# Query and view-model inventory

This is the requirement-backed read-layer plan for the committed product scope. It names query operations and their feature-owned response views. A query is a read, list, preview, or derived assessment; a POST transport does not make a read a command. The response views below are proposed contracts to backfill, not evidence that a handler, projection, API route, or authorization policy already exists.

## Existing inventory

Before this backfill, the source inventory recorded **216 query contracts** and **436 models/supporting contracts**. The current code now contains **271 query contracts** (216 existing plus 55 added here) and **490 model/supporting contracts** (436 existing plus 54 response views added here). The full, source-linked query list and feature-owned models are in the [component inventory](component-inventory.md#existing-queries) and [model appendix](component-inventory.md#existing-models-and-supporting-contracts). Those totals are a static source inventory, not proof that every query is authorized, registered, tested, or satisfies its owning story. Reconcile by behavior before adding a new name: a current query can satisfy a requirement even when its vocabulary differs.

## Required read workflows to backfill

The 55 planned query contracts and 54 response-view types listed below now have one source file each under their owning feature. This fills the contract-file gap recorded in the source audit. Query handlers, projector-backed behavior, endpoint registration, and story-level authorization/field validation remain to be implemented and verified. Existing contracts with incomplete consumers are tracked as completion gaps rather than duplicated here.

### Advisor collaboration — R2-08

| Query | Response view | Required read |
| --- | --- | --- |
| [GetAdvisorRequest](../../src/Compliance.Common/Features/AdvisorCollaboration/GetAdvisorRequest.cs#L9) | [AdvisorRequestView](../../src/Compliance.Common/Features/AdvisorCollaboration/AdvisorRequestView.cs#L6) | One scoped advisor handoff, including selected immutable record versions and lifecycle state. |
| [ListAdvisorRequests](../../src/Compliance.Common/Features/AdvisorCollaboration/ListAdvisorRequests.cs#L9) | `Page<[AdvisorRequestSummaryView](../../src/Compliance.Common/Features/AdvisorCollaboration/AdvisorRequestSummaryView.cs#L6)>` | Scoped handoff directory with status and review readiness. |
| [ListAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/ListAdvisorFeedback.cs#L9) | `Page<[AdvisorFeedbackView](../../src/Compliance.Common/Features/AdvisorCollaboration/AdvisorFeedbackView.cs#L6)>` | Feedback and responses for a handoff or its selected records. |

### Type I engagement — T1-01/03

| Query | Response view | Required read |
| --- | --- | --- |
| [GetExaminationEngagement](../../src/Compliance.Common/Features/Examinations/GetExaminationEngagement.cs#L9) | [ExaminationEngagementView](../../src/Compliance.Common/Features/Examinations/ExaminationEngagementView.cs#L6) | Engagement identity, period, lifecycle, and baseline binding. |
| [GetTypeIBaseline](../../src/Compliance.Common/Features/Examinations/GetTypeIBaseline.cs#L9) | [TypeIBaselineView](../../src/Compliance.Common/Features/Examinations/TypeIBaselineView.cs#L6) | Frozen baseline manifest, exact source versions, and verification state. |
| [ListEngagementAccess](../../src/Compliance.Common/Features/Examinations/ListEngagementAccess.cs#L9) | `Page<[EngagementAccessView](../../src/Compliance.Common/Features/Examinations/EngagementAccessView.cs#L6)>` | Auditor grants and revocations scoped to an engagement. |

### System description — T1-02

| Query | Response view | Required read |
| --- | --- | --- |
| [GetSystemDescription](../../src/Compliance.Common/Features/SystemDescription/GetSystemDescription.cs#L9) | [SystemDescriptionView](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionView.cs#L6) | Current or requested version, review/approval state, and engagement binding. |
| [ListSystemDescriptionVersions](../../src/Compliance.Common/Features/SystemDescription/ListSystemDescriptionVersions.cs#L9) | `Page<[SystemDescriptionVersionView](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionVersionView.cs#L6)>` | Version history with authorship and decision state. |
| [PreviewSystemDescriptionImpact](../../src/Compliance.Common/Features/SystemDescription/PreviewSystemDescriptionImpact.cs#L9) | [SystemDescriptionImpactPreview](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionImpactPreview.cs#L6) | Records and baseline references affected by a proposed successor. |

### Auditor requests and samples — T1-04, T3-03

| Query | Response view | Required read |
| --- | --- | --- |
| [GetAuditorRequest](../../src/Compliance.Common/Features/AuditorRequests/GetAuditorRequest.cs#L9) | [AuditorRequestView](../../src/Compliance.Common/Features/AuditorRequests/AuditorRequestView.cs#L6) | Request, assignment, due state, responses, and disclosure state. |
| [ListAuditorRequests](../../src/Compliance.Common/Features/AuditorRequests/ListAuditorRequests.cs#L9) | `Page<[AuditorRequestSummaryView](../../src/Compliance.Common/Features/AuditorRequests/AuditorRequestSummaryView.cs#L6)>` | Engagement-scoped requests and their current work status. |
| [GetSampleSelection](../../src/Compliance.Common/Features/AuditorRequests/GetSampleSelection.cs#L9) | [SampleSelectionView](../../src/Compliance.Common/Features/AuditorRequests/SampleSelectionView.cs#L6) | Frozen selection method, population reference, selected rows, and manifest. |
| [ListRequestResponses](../../src/Compliance.Common/Features/AuditorRequests/ListRequestResponses.cs#L9) | `Page<[AuditorRequestResponseView](../../src/Compliance.Common/Features/AuditorRequests/AuditorRequestResponseView.cs#L6)>` | Submitted responses and governed delivery/disposition receipts. |

### Audit packages — T1-05, T3-05

| Query | Response view | Required read |
| --- | --- | --- |
| [GetAuditPackage](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackage.cs#L9) | [AuditPackageView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageView.cs#L6) | Package lifecycle, build result, validation, disclosure, and delivery state. |
| [ListAuditPackages](../../src/Compliance.Common/Features/AuditPackages/ListAuditPackages.cs#L9) | `Page<[AuditPackageSummaryView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageSummaryView.cs#L6)>` | Packages for the authorized engagement or period. |
| [GetAuditPackageManifest](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackageManifest.cs#L9) | [AuditPackageManifestView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageManifestView.cs#L6) | Exact included records, versions, evidence references, and integrity metadata. |
| [GetAuditPackageValidation](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackageValidation.cs#L9) | [AuditPackageValidationView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageValidationView.cs#L6) | Validation findings and any separately approved override. |
| [GetAuditPackageDelivery](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackageDelivery.cs#L9) | [AuditPackageDeliveryView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageDeliveryView.cs#L6) | Attributed delivery attempt and recipient/provider receipt. |

### Audit observations and outcomes — T1-06/07, T3-04/06

| Query | Response view | Required read |
| --- | --- | --- |
| [ListAuditObservations](../../src/Compliance.Common/Features/Examinations/ListAuditObservations.cs#L9) | `Page<[AuditObservationView](../../src/Compliance.Common/Features/Examinations/AuditObservationView.cs#L6)>` | Engagement/period observations, evidence references, and linked findings. |
| [GetAuditOutcome](../../src/Compliance.Common/Features/Examinations/GetAuditOutcome.cs#L9) | [AuditOutcomeView](../../src/Compliance.Common/Features/Examinations/AuditOutcomeView.cs#L6) | Recorded outcome and its authorized, attributable basis. |
| [GetTypeIIPlan](../../src/Compliance.Common/Features/Examinations/GetTypeIIPlan.cs#L9) | [TypeIIPlanView](../../src/Compliance.Common/Features/Examinations/TypeIIPlanView.cs#L6) | Proposed or approved period plan and decision history. |
| [GetManagementAssertion](../../src/Compliance.Common/Features/Examinations/GetManagementAssertion.cs#L9) | [ManagementAssertionView](../../src/Compliance.Common/Features/Examinations/ManagementAssertionView.cs#L6) | Management-authored assertion and signature/status. |
| [GetExaminationSignOff](../../src/Compliance.Common/Features/Examinations/GetExaminationSignOff.cs#L9) | [ExaminationSignOffView](../../src/Compliance.Common/Features/Examinations/ExaminationSignOffView.cs#L6) | Sign-off record, author, timestamp, and exact package/period references. |

### Observation periods and recurrence — T2-01/02/03

| Query | Response view | Required read |
| --- | --- | --- |
| [GetObservationPeriod](../../src/Compliance.Common/Features/Operations/GetObservationPeriod.cs#L9) | [ObservationPeriodView](../../src/Compliance.Common/Features/Operations/ObservationPeriodView.cs#L6) | Period lifecycle, dates, and governing program. |
| [ListObservationPeriods](../../src/Compliance.Common/Features/Operations/ListObservationPeriods.cs#L9) | `Page<[ObservationPeriodSummaryView](../../src/Compliance.Common/Features/Operations/ObservationPeriodSummaryView.cs#L6)>` | Program period history and current status. |
| [GetControlCalendar](../../src/Compliance.Common/Features/Operations/GetControlCalendar.cs#L9) | [ControlCalendarView](../../src/Compliance.Common/Features/Operations/ControlCalendarView.cs#L6) | Due control occurrences over the requested range and their source schedules. |
| [ListMissingOccurrences](../../src/Compliance.Common/Features/Operations/ListMissingOccurrences.cs#L9) | `Page<[MissingOccurrenceView](../../src/Compliance.Common/Features/Operations/MissingOccurrenceView.cs#L6)>` | Expected control occurrences with no accepted performance record. |

### Recurring governance — T2-06/09

| Query | Response view | Required read |
| --- | --- | --- |
| [ListDueGovernanceReviews](../../src/Compliance.Common/Features/Operations/ListDueGovernanceReviews.cs#L9) | `Page<[GovernanceReviewDueView](../../src/Compliance.Common/Features/Operations/GovernanceReviewDueView.cs#L6)>` | Reviews due in the selected period, with source and owner. |
| [GetManagementReview](../../src/Compliance.Common/Features/Operations/GetManagementReview.cs#L9) | [ManagementReviewView](../../src/Compliance.Common/Features/Operations/ManagementReviewView.cs#L6) | Management review record, inputs, participants, and completion state. |
| [ListManagementReviewActions](../../src/Compliance.Common/Features/Operations/ListManagementReviewActions.cs#L9) | `Page<[ManagementReviewActionView](../../src/Compliance.Common/Features/Operations/ManagementReviewActionView.cs#L6)>` | Actions arising from a review and their ownership/status. |

### Changes and incidents — T2-07

| Query | Response view | Required read |
| --- | --- | --- |
| [GetSignificantChange](../../src/Compliance.Common/Features/Changes/GetSignificantChange.cs#L9) | [SignificantChangeView](../../src/Compliance.Common/Features/Changes/SignificantChangeView.cs#L6) | Change record, impact assessment, and linked review work. |
| [ListSignificantChanges](../../src/Compliance.Common/Features/Changes/ListSignificantChanges.cs#L9) | `Page<[SignificantChangeSummaryView](../../src/Compliance.Common/Features/Changes/SignificantChangeSummaryView.cs#L6)>` | Changes within the authorized program/period scope. |
| [GetComplianceIncident](../../src/Compliance.Common/Features/Incidents/GetComplianceIncident.cs#L9) | [ComplianceIncidentView](../../src/Compliance.Common/Features/Incidents/ComplianceIncidentView.cs#L6) | Incident, assessment, resolution, and evidence references. |
| [ListComplianceIncidents](../../src/Compliance.Common/Features/Incidents/ListComplianceIncidents.cs#L9) | `Page<[ComplianceIncidentSummaryView](../../src/Compliance.Common/Features/Incidents/ComplianceIncidentSummaryView.cs#L6)>` | Incidents within authorized program/period scope. |
| [GetComplianceImpactAssessment](../../src/Compliance.Common/Features/Incidents/GetComplianceImpactAssessment.cs#L9) | [ComplianceImpactAssessmentView](../../src/Compliance.Common/Features/Incidents/ComplianceImpactAssessmentView.cs#L6) | Assessed control, risk, and evidence impacts with disposition. |

### Period close and populations — T3-01/02

| Query | Response view | Required read |
| --- | --- | --- |
| [PreviewObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/PreviewObservationPeriodClose.cs#L9) | [ObservationPeriodClosePreview](../../src/Compliance.Common/Features/AuditPopulations/ObservationPeriodClosePreview.cs#L6) | Completeness and unresolved-item report before freezing close. |
| [GetObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/GetObservationPeriodClose.cs#L9) | [ObservationPeriodCloseView](../../src/Compliance.Common/Features/AuditPopulations/ObservationPeriodCloseView.cs#L6) | Frozen close manifest, verification, and amendment history. |
| [ListAuditPopulationDefinitions](../../src/Compliance.Common/Features/AuditPopulations/ListAuditPopulationDefinitions.cs#L9) | `Page<[AuditPopulationDefinitionView](../../src/Compliance.Common/Features/AuditPopulations/AuditPopulationDefinitionView.cs#L6)>` | Authorized definitions and version state for the period. |
| [GetAuditPopulation](../../src/Compliance.Common/Features/AuditPopulations/GetAuditPopulation.cs#L9) | [AuditPopulationView](../../src/Compliance.Common/Features/AuditPopulations/AuditPopulationView.cs#L6) | Frozen population metadata, source, and reconciliation state. |
| [ListAuditPopulationRows](../../src/Compliance.Common/Features/AuditPopulations/ListAuditPopulationRows.cs#L9) | `Page<[AuditPopulationRowView](../../src/Compliance.Common/Features/AuditPopulations/AuditPopulationRowView.cs#L6)>` | Population members with classification and provenance. |
| [GetPopulationReconciliation](../../src/Compliance.Common/Features/AuditPopulations/GetPopulationReconciliation.cs#L9) | [PopulationReconciliationView](../../src/Compliance.Common/Features/AuditPopulations/PopulationReconciliationView.cs#L6) | Reconciliation totals, exceptions, and resolution state. |

### Continuity — T3-07

| Query | Response view | Required read |
| --- | --- | --- |
| [PreviewProgramRollover](../../src/Compliance.Common/Features/Programs/PreviewProgramRollover.cs#L9) | [ProgramRolloverPreview](../../src/Compliance.Common/Features/Programs/ProgramRolloverPreview.cs#L6) | Carry-forward candidates and unresolved dependencies before proposal. |
| [GetProgramRollover](../../src/Compliance.Common/Features/Programs/GetProgramRollover.cs#L9) | [ProgramRolloverView](../../src/Compliance.Common/Features/Programs/ProgramRolloverView.cs#L6) | Rollover proposal, decision, and successor-period binding. |
| [GetNextPeriodPlan](../../src/Compliance.Common/Features/Programs/GetNextPeriodPlan.cs#L9) | [NextPeriodPlanView](../../src/Compliance.Common/Features/Programs/NextPeriodPlanView.cs#L6) | Proposed successor-period dates and carry-forward plan. |
| [ListCarryForwardReferences](../../src/Compliance.Common/Features/Programs/ListCarryForwardReferences.cs#L9) | `Page<[CarryForwardReferenceView](../../src/Compliance.Common/Features/Programs/CarryForwardReferenceView.cs#L6)>` | Source-to-successor references for carried work and records. |

### Firm portfolio and personal cross-client work — F1-02/05

| Query | Response view | Required read |
| --- | --- | --- |
| [ListAuthorizedClientPortfolio](../../src/Compliance.Common/Features/FirmPortfolio/ListAuthorizedClientPortfolio.cs#L9) | `Page<[AuthorizedClientSummaryView](../../src/Compliance.Common/Features/FirmPortfolio/AuthorizedClientSummaryView.cs#L6)>` | Only client summaries within the actor's explicit portfolio authority. |
| [GetAuthorizedClientSummary](../../src/Compliance.Common/Features/FirmPortfolio/GetAuthorizedClientSummary.cs#L9) | [AuthorizedClientSummaryView](../../src/Compliance.Common/Features/FirmPortfolio/AuthorizedClientSummaryView.cs#L6) | One authorized client's permitted portfolio summary. |
| [ListMyCrossClientWork](../../src/Compliance.Common/Features/FirmPortfolio/ListMyCrossClientWork.cs#L9) | `Page<[CrossClientWorkItemView](../../src/Compliance.Common/Features/FirmPortfolio/CrossClientWorkItemView.cs#L6)>` | The actor's own eligible work across authorized clients, independently filtered per client. |

### Methodology templates — F1-04

| Query | Response view | Required read |
| --- | --- | --- |
| [ListMethodologyTemplates](../../src/Compliance.Common/Features/MethodologyTemplates/ListMethodologyTemplates.cs#L9) | `Page<[MethodologyTemplateSummaryView](../../src/Compliance.Common/Features/MethodologyTemplates/MethodologyTemplateSummaryView.cs#L6)>` | Published templates visible to authorized tenant administrators. |
| [GetMethodologyTemplateVersion](../../src/Compliance.Common/Features/MethodologyTemplates/GetMethodologyTemplateVersion.cs#L9) | [MethodologyTemplateVersionView](../../src/Compliance.Common/Features/MethodologyTemplates/MethodologyTemplateVersionView.cs#L6) | Immutable template version and provenance. |
| [PreviewMethodologyTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/PreviewMethodologyTemplateApplication.cs#L9) | [MethodologyTemplateApplicationPreview](../../src/Compliance.Common/Features/MethodologyTemplates/MethodologyTemplateApplicationPreview.cs#L6) | Proposed additions/changes and conflicts before applying a template. |
| [GetTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/GetTemplateApplication.cs#L9) | [TemplateApplicationView](../../src/Compliance.Common/Features/MethodologyTemplates/TemplateApplicationView.cs#L6) | Application record, source version, and created-record provenance. |

### Own-attest delivery and offboarding — F1-01/07/08

| Query | Response view | Required read |
| --- | --- | --- |
| [GetOwnAttestRecordPackage](../../src/Compliance.Common/Features/AttestRecords/GetOwnAttestRecordPackage.cs#L9) | [OwnAttestRecordPackageView](../../src/Compliance.Common/Features/AttestRecords/OwnAttestRecordPackageView.cs#L6) | Required record package manifest and delivery readiness. |
| [GetOwnAttestRecordDelivery](../../src/Compliance.Common/Features/AttestRecords/GetOwnAttestRecordDelivery.cs#L9) | [OwnAttestRecordDeliveryView](../../src/Compliance.Common/Features/AttestRecords/OwnAttestRecordDeliveryView.cs#L6) | Attributed delivery and verification receipt. |
| [ListOutstandingAttestDeliveryHolds](../../src/Compliance.Common/Features/AttestRecords/ListOutstandingAttestDeliveryHolds.cs#L9) | `Page<[AttestDeliveryHoldView](../../src/Compliance.Common/Features/AttestRecords/AttestDeliveryHoldView.cs#L6)>` | Engagement holds still requiring governed delivery or release. |

## Explicitly excluded from this backfill

- T2-08 integration endpoints, collection runs, and reconciliation queries remain optional collection hypotheses; manual source completeness and reconciliation remain the requirement baseline.
- F1-03 staff capacity and assignment-conflict queries are excluded until that staffing-capacity feature is accepted.
- BO-01/02/03 organization handover, hosted billing, and add-on queries remain excluded while their policy decisions are unsettled.
- Direct external auditor account and identity administration queries are excluded; T1/T3 request and access workflows above do not imply a separate auditor account product.
- Privacy lifecycle query families are not enumerated because the required Privacy scope remains unresolved.

## Read-model rules

Every listed query must enforce tenant, program, permission, sensitive-field, and independence boundaries before returning rows, totals, cursor state, exports, or derived results. Cross-client portfolio and personal-work reads require explicit authority and independent per-client filtering. Preview responses are derived and non-authoritative; they must identify relevant source versions and be recomputed at the owning command boundary. Historical and reproducible results must retain or reference exact input versions. Do not create one generic view that crosses feature ownership or leaks fields unnecessary to its consumer.

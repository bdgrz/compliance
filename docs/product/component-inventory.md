# Required component inventory

Planning baseline: local product contracts and backend source inspected 2026-10-17.

For the reconciled command list, requirement coverage and remaining work, use [System command list](command-list.md), with rationale in the [command gap analysis](command-inventory-gap-analysis.md). Its command conclusions supersede the provisional completion suggestions below, including periodic-review subsystems and external-auditor account proposals.

For the read layer, use the [query and view-model list](query-and-view-model-list.md) for the pruned, story-backed backfill. Proposed query candidates later in this inventory are not all required; optional collection and unsettled commerce/capacity hypotheses are excluded there.

This inventory covers commands, queries, domain events, aggregates, models, HTTP/MCP APIs, reactors, and projectors for the documented Compliance product. It includes the complete local source inventory below and a proposed completion inventory for later workflows. Source presence is not proof of delivery, registration, tests, authorization correctness, or release acceptance. GitHub delivery status was not audited for this document.

## How to read the inventory

- **Commands** express an attributable intent and may change authoritative state.
- **Queries** read or preview state. HTTP POST does not necessarily mean a command.
- **Events** retain accepted domain facts; they do not replace operational logs.
- **Aggregates** enforce consistency and workflow transitions. A model is not automatically an aggregate.
- **Models** include entities, immutable versions, relationships, decisions, snapshots, request results, and read views.
- **APIs** expose authorized operations through HTTP and, where appropriate, MCP. Internal commands and human approvals are not automatically MCP tools.
- **Reactors** consume facts and perform follow-up commands or external effects. They require tenant scope, stable deduplication identity, safe replay, and inspectable failures.
- **Projectors** derive rebuildable read models. They do not own business decisions or execute side effects.

The existing inventory preserves actual source names, including older vocabulary. Proposed names below are design candidates, not approved aggregate boundaries or a declaration that similarly named existing behavior is missing. Compare capabilities and invariants before adding anything.

Sources: [product scope](README.md), [domain ownership](domain-model.md), [canonical models](canonical-entity-model.md), [backlog](backlog.md), [commercial scope](business-operations.md), [projection rules](../architecture/decisions/0007-read-models-projections-and-as-of-calculations.md), [work-source coverage](work-queue-source-coverage.md).

## Required capability checklist

Each capability needs its owning commands and queries, retained facts, domain state, authorized API surface, and read models. A reactor is required only where an asynchronous reaction is actually needed. The source appendix names the components already present for these capabilities.

| Capability | Required behavior and records | Product scope |
| --- | --- | --- |
| Sign-in and recovery | Platform users, federated identities, verified email challenges, identity linking, recovery and browser/API authentication | EN-01, R1-04, F1-06 |
| Organizations and membership | Organization creation/routing, lifecycle, invitations, membership, tenant issuer trust, operator grants | R1-04/15, F1-01/06 |
| Authorization and responsibilities | Roles, permissions, team membership, grants, responsibility intervals, self-review constraints and governed exceptions | EN-01/04, R1-04 |
| Firm engagements and independence | Staff directory, service engagements, acceptance, assignments, nonattest records, reevaluation and walls | F1-03/07/08 |
| Program and scope | Program, client services, versioned boundaries, inclusions/exclusions, approvals and impact previews | R1-01/02 |
| Criteria | Catalog editions, entries, permitted content overlays, selected criteria and applicability decisions | R1-03/06 |
| Workforce | People, work relationships, workforce authority, observations, source reconciliation, service-identity ownership | R1-11 |
| Application inventory | Logical applications, system instances, relationships, lifecycle, restricted visibility and review scope | R1-10 |
| Technology and information | Material components, locations, processes, information assets, data flows, effective history and classifications | R1-12 |
| Commitments | Service commitments, system requirements, CUECs/CSOCs, applicability, exact-version review and approval | R1-13 |
| Controls and mappings | Drafts, immutable approved versions, retirement, criteria mappings, applicability, owner and operating plan | R1-05/06, R2-01 |
| Risk governance | Methods, assessment rounds, treatment, control-treatment links, actions, residual risk, acceptance and reassessment | R1-07 |
| Provider assurance | Provider register, report coverage, materiality, boundary treatment, reviews and coverage gaps | R1-14 |
| Policies and training | Policy lifecycle and applicability, review cycles, frozen distribution audiences, acknowledgements, training and waivers | R2-02/10 |
| Control operation | Calendar, occurrences, performance submissions, corrections, independent reviews, gaps and recurring scheduling | R2-01/04, T2-02/03 |
| Control evaluation | Evaluation plans and rounds, test-step observations, submissions, independent review and deviations | R2-05 |
| Evidence | Requests, capture, immutable content, support links, inspection/quarantine, contextual sufficiency and recovery | EN-06, R2-03 |
| Evidence governance | Access/disclosure, redaction lineage, retention bases, holds, governed disposition and preserved metadata | R2-12 |
| External access | Accounts, groups, roles, entitlements, direct assignments, derived paths, expected access and source completeness | R2-06 |
| Access review | Frozen populations, campaign scope, personal/bulk decisions, missing populations, remediation and verification | R2-06, T2-05 |
| Remediation and waivers | Findings, source wording, corrective actions, closure review, reopening, waiver approval/expiry and risk-acceptance links | R2-07 |
| Readiness | Versioned rules, exact input sets, retained assessments, gaps, action plans, annotations and human entry decisions | R1-08, R2-09, T2-04 |
| Accountable work | Source-owned work, assignment, claim/delegation, eligible actors, blocked/overdue state, search, reminders and escalation | R2-11 |
| Imports and collection | Staging, preview, correlation, reconciliation, batch acceptance, durable execution/recovery and source provenance | EN-05, R1-09, T2-08 |
| Snapshots and history | Frozen exact versions, manifests, verification, amendments, regeneration and replay | EN-02/03 |
| Advisor collaboration | Scoped requests, feedback, response, resolution and independent advisory compartment | R2-08 |
| Type I preparation | Engagement, baseline, management-authored system description, auditor access, requests, samples, packages and outcome | T1-01–07 |
| Type II operations | Observation period, periodic reviews, change/incident assessment, completeness monitoring and management reviews | T2-01–09 |
| Examination and rollover | Period close, populations, samples, exceptions, management assertion/sign-off, indexed package, outcome and next period | T3-01–07 |
| Firm portfolio and methodology | Authorized multi-client summaries, staff capacity, personal cross-client queue, templates and application provenance | F1-02–05 |
| Delegation and commerce | Organization handover, billing administration, subscription, selected add-ons and entitlement/charge history | BO-01–03 |

## Proposed completion inventory

These candidates make later scope concrete. They are not established public contracts. Each row must be reconciled with existing components below and the linked story before implementation. Slash-separated names denote separate operations or records, not one generic workflow.

### Advisor collaboration — R2-08

- Aggregate/model candidates: AdvisorRequest; AdvisorFeedback; AdvisoryReadinessNote; FeedbackResponse.
- Commands: OpenAdvisorRequest; RecordAdvisorFeedback; RespondToAdvisorFeedback; ResolveAdvisorFeedback; WithdrawAdvisorRequest.
- Queries: GetAdvisorRequest; ListAdvisorRequests; ListAdvisorFeedback.
- Events: AdvisorRequestOpened; AdvisorFeedbackRecorded; AdvisorFeedbackResponded; AdvisorFeedbackResolved; AdvisorRequestWithdrawn.
- Reactors: AdvisorRequestNotificationReactor.
- Projectors: AdvisorRequestDirectoryProjector; AdvisorFeedbackWorkItemProjector.
- API family: advisory requests, feedback, responses and resolution.

### Type I engagement — T1-01/03

- Aggregate/model candidates: ExaminationEngagement; TypeIBaseline; EngagementAccessGrant.
- Commands: OpenExaminationEngagement; FreezeTypeIBaseline; AmendTypeIBaseline; GrantAuditorEngagementAccess; RevokeAuditorEngagementAccess.
- Queries: GetExaminationEngagement; GetTypeIBaseline; ListEngagementAccess.
- Events: ExaminationEngagementOpened; TypeIBaselineFrozen; TypeIBaselineAmended; AuditorEngagementAccessGranted; AuditorEngagementAccessRevoked.
- Reactors: TypeIBaselineAssemblyReactor.
- Projectors: ExaminationEngagementDirectoryProjector; TypeIBaselineProjector; EngagementAccessProjector.
- API family: engagements, baselines and engagement access.

### System description — T1-02

- Aggregate/model candidates: SystemDescription; SystemDescriptionVersion; SystemDescriptionApproval.
- Commands: CreateSystemDescriptionDraft; ReviseSystemDescriptionDraft; ReviewSystemDescription; ApproveSystemDescription; ProposeSystemDescriptionSuccessor; BindSystemDescriptionToEngagement.
- Queries: GetSystemDescription; ListSystemDescriptionVersions; PreviewSystemDescriptionImpact.
- Events: SystemDescriptionDraftCreated; SystemDescriptionDraftRevised; SystemDescriptionReviewed; SystemDescriptionApproved; SystemDescriptionSuccessorProposed; SystemDescriptionBound.
- Reactors: SystemDescriptionImpactReactor.
- Projectors: SystemDescriptionDirectoryProjector; SystemDescriptionHistoryProjector; SystemDescriptionDecisionWorkItemProjector.
- API family: descriptions, versions, reviews, approvals and engagement bindings.

### Auditor requests and samples — T1-04, T3-03

- Aggregate/model candidates: AuditorRequest; SampleSelection; RequestResponse; ResponseDisclosureDecision.
- Commands: RecordAuditorRequest; RecordSampleSelection; AssignAuditorRequest; SubmitAuditorResponse; ReviewAuditorResponseDisclosure; DeliverAuditorResponse; RecordAuditorResponseDisposition.
- Queries: GetAuditorRequest; ListAuditorRequests; GetSampleSelection; ListRequestResponses.
- Events: AuditorRequestRecorded; SampleSelectionRecorded; AuditorRequestAssigned; AuditorResponseSubmitted; AuditorResponseDisclosureReviewed; AuditorResponseDelivered; AuditorResponseDispositionRecorded.
- Reactors: AuditorResponseDeliveryReactor; AuditorRequestReminderReactor.
- Projectors: AuditorRequestDirectoryProjector; SampleSelectionDirectoryProjector; AuditorRequestWorkItemProjector.
- API family: requests, frozen sample selections, responses and delivery receipts.

### Audit packages — T1-05, T3-05

- Aggregate/model candidates: AuditPackage; AuditPackageManifest; PackageValidation; PackageDelivery.
- Commands: RequestAuditPackageBuild; ValidateAuditPackage; ApproveAuditPackageDisclosure; DeliverAuditPackage; AmendAuditPackage.
- Queries: GetAuditPackage; ListAuditPackages; GetAuditPackageManifest; GetAuditPackageValidation; GetAuditPackageDelivery.
- Events: AuditPackageBuildRequested; AuditPackageBuilt; AuditPackageBuildFailed; AuditPackageValidated; AuditPackageDisclosureApproved; AuditPackageDelivered; AuditPackageAmended.
- Reactors: AuditPackageBuildReactor; AuditPackageDeliveryReactor.
- Projectors: AuditPackageDirectoryProjector; AuditPackageValidationProjector; PackageDeliveryProjector.
- API family: packages, manifests, validation, disclosure and delivery.

### Audit observations and outcomes — T1-06/07, T3-04/06

- Aggregate/model candidates: AuditObservation; AuditFindingReference; AuditOutcome; TypeIIPlan; ManagementAssertion; RepresentationLetterReference.
- Commands: RecordAuditObservation; LinkAuditObservationFinding; RecordAuditOutcome; ProposeTypeIIPlan; ApproveTypeIIPlan; RecordManagementAssertion; RecordRepresentationLetter; SignOffExamination.
- Queries: ListAuditObservations; GetAuditOutcome; GetTypeIIPlan; GetManagementAssertion; GetExaminationSignOff.
- Events: AuditObservationRecorded; AuditObservationFindingLinked; AuditOutcomeRecorded; TypeIIPlanProposed; TypeIIPlanApproved; ManagementAssertionRecorded; RepresentationLetterRecorded; ExaminationSignedOff.
- Reactors: AuditOutcomeStageTransitionReactor.
- Projectors: AuditObservationDirectoryProjector; AuditOutcomeProjector; TypeIIPlanProjector; ExaminationSignOffProjector.
- API family: observations, outcomes, plans, assertions, letters and sign-offs.

### Observation periods and recurrence — T2-01/02/03

- Aggregate/model candidates: ObservationPeriod; ControlSchedule; OccurrenceGenerationCheckpoint.
- Commands: StartObservationPeriod; ReviseFutureControlSchedule; GenerateDueControlOccurrences; RecordMissingOccurrence; EndObservationPeriod.
- Queries: GetObservationPeriod; ListObservationPeriods; GetControlCalendar; ListMissingOccurrences.
- Events: ObservationPeriodStarted; FutureControlScheduleRevised; DueControlOccurrencesGenerated; MissingOccurrenceRecorded; ObservationPeriodEnded.
- Reactors: ControlOccurrenceSchedulingReactor; MissingOccurrenceDetectionReactor.
- Projectors: ObservationPeriodDirectoryProjector; ControlCalendarProjector; OccurrenceCompletenessProjector.
- API family: periods, control calendar and occurrence completeness.

### Recurring governance — T2-06/09

- Aggregate/model candidates: GovernanceReview (control occurrence where accepted); ManagementReviewRecord; ReviewAction.
- Commands: ScheduleGovernanceReview; RecordManagementReview; RecordManagementReviewAction; CompleteManagementReviewAction.
- Queries: ListDueGovernanceReviews; GetManagementReview; ListManagementReviewActions.
- Events: GovernanceReviewScheduled; ManagementReviewRecorded; ManagementReviewActionRecorded; ManagementReviewActionCompleted.
- Reactors: GovernanceReviewSchedulingReactor.
- Projectors: GovernanceReviewWorkItemProjector; ManagementReviewDirectoryProjector.
- API family: governance review occurrences, management review records and actions.

### Changes and incidents — T2-07

- Aggregate/model candidates: SignificantChange; ComplianceIncident; ComplianceImpactAssessment; ChangeReviewReference.
- Commands: RecordSignificantChange; ReviseSignificantChange; AssessSignificantChangeImpact; RecordComplianceIncident; ReviseComplianceIncident; AssessComplianceIncidentImpact; RecordIncidentResolution; LinkImpactReviewWork.
- Queries: GetSignificantChange; ListSignificantChanges; GetComplianceIncident; ListComplianceIncidents; GetComplianceImpactAssessment.
- Events: SignificantChangeRecorded; SignificantChangeRevised; SignificantChangeImpactAssessed; ComplianceIncidentRecorded; ComplianceIncidentRevised; ComplianceIncidentImpactAssessed; IncidentResolutionRecorded; ImpactReviewWorkLinked.
- Reactors: ComplianceImpactReviewReactor; ChangeRiskReassessmentReactor.
- Projectors: SignificantChangeDirectoryProjector; ComplianceIncidentDirectoryProjector; ComplianceImpactWorkItemProjector.
- API family: native changes, native incidents, impact assessments and linked reviews.

### Optional collection — T2-08

- Aggregate/model candidates: IntegrationEndpoint; CollectionSchedule; CollectionRun; SourceCompletenessStatement.
- Commands: ConfigureIntegrationEndpoint; DisableIntegrationEndpoint; ConfigureCollectionSchedule; StartCollectionRun; CancelCollectionRun; RecordCollectionObservation; AcceptCollectionReconciliation.
- Queries: GetIntegrationEndpoint; ListIntegrationEndpoints; ListCollectionRuns; GetCollectionRun; PreviewCollectionReconciliation.
- Events: IntegrationEndpointConfigured; IntegrationEndpointDisabled; CollectionScheduleConfigured; CollectionRunStarted; CollectionObservationRecorded; CollectionRunCompleted; CollectionRunFailed; CollectionRunCanceled; CollectionReconciliationAccepted.
- Reactors: CollectionScheduleReactor; CollectionExecutionReactor; CollectionReconciliationReactor.
- Projectors: IntegrationEndpointProjector; CollectionRunProjector; SourceCompletenessProjector.
- API family: integration configuration, runs, observations and governed reconciliation.

### Period close and populations — T3-01/02

- Aggregate/model candidates: ObservationPeriodClose; AuditPopulationDefinition; AuditPopulation; AuditPopulationRow; PopulationReconciliation; PopulationAmendment.
- Commands: ProposeObservationPeriodClose; FreezeObservationPeriodClose; DefineAuditPopulation; RecordAuditPopulationFact; ClassifyAuditPopulationRow; ReconcileAuditPopulation; FreezeAuditPopulation; AmendAuditPopulation.
- Queries: PreviewObservationPeriodClose; GetObservationPeriodClose; ListAuditPopulationDefinitions; GetAuditPopulation; ListAuditPopulationRows; GetPopulationReconciliation.
- Events: ObservationPeriodCloseProposed; ObservationPeriodCloseFrozen; AuditPopulationDefined; AuditPopulationFactRecorded; AuditPopulationRowClassified; AuditPopulationReconciled; AuditPopulationFrozen; AuditPopulationAmended.
- Reactors: ObservationPeriodCloseAssemblyReactor; AuditPopulationBuildReactor.
- Projectors: ObservationPeriodCloseProjector; AuditPopulationDirectoryProjector; PopulationCompletenessProjector.
- API family: period closes, population definitions, immutable populations, rows and reconciliation.

### Continuity — T3-07

- Aggregate/model candidates: ProgramRollover; NextPeriodPlan; CarryForwardReference.
- Commands: ProposeProgramRollover; ApproveProgramRollover; StartNextObservationPeriod; CarryForwardOpenRemediation.
- Queries: PreviewProgramRollover; GetProgramRollover; GetNextPeriodPlan; ListCarryForwardReferences.
- Events: ProgramRolloverProposed; ProgramRolloverApproved; NextObservationPeriodStarted; OpenRemediationCarriedForward.
- Reactors: ProgramRolloverReactor.
- Projectors: ProgramRolloverProjector; NextPeriodPlanProjector.
- API family: rollover proposals, approvals, successor periods and carry-forward links.

### Firm portfolio and staffing — F1-02/03/05

- Aggregate/model candidates: AuthorizedClientSummary; StaffCapacityPlan; StaffAssignment; CrossClientWorkPage.
- Commands: DefineStaffCapacity; ProposeStaffAssignment; AcceptStaffAssignment; EndStaffAssignment.
- Queries: ListAuthorizedClientPortfolio; GetAuthorizedClientSummary; ListStaffCapacity; PreviewStaffAssignmentConflicts; ListMyCrossClientWork.
- Events: StaffCapacityDefined; StaffAssignmentProposed; StaffAssignmentAccepted; StaffAssignmentEnded.
- Reactors: StaffAssignmentIndependenceReevaluationReactor.
- Projectors: StaffCapacityProjector; tenant-local PortfolioSummaryProjector; tenant-local PersonalWorkProjector.
- API family: explicitly authorized portfolio, capacity and personal work queries; no shared client-data cache by default.

### Methodology templates — F1-04

- Aggregate/model candidates: MethodologyTemplate; TemplateVersion; TemplateApplication; TemplateRecordProvenance.
- Commands: CreateMethodologyTemplate; ReviseMethodologyTemplateDraft; PublishMethodologyTemplateVersion; RetireMethodologyTemplate; ApplyMethodologyTemplate.
- Queries: ListMethodologyTemplates; GetMethodologyTemplateVersion; PreviewMethodologyTemplateApplication; GetTemplateApplication.
- Events: MethodologyTemplateCreated; MethodologyTemplateDraftRevised; MethodologyTemplateVersionPublished; MethodologyTemplateRetired; MethodologyTemplateApplied.
- Reactors: TemplateApplicationReactor.
- Projectors: MethodologyTemplateDirectoryProjector; TemplateApplicationProjector.
- API family: platform template library, versions, preview and tenant-specific application.

### Own-attest closure and offboarding — F1-01/07/08

- Aggregate/model candidates: OwnAttestRecordPackage; AttestRecordDeliveryReceipt; EngagementRetentionHold.
- Commands: PrepareOwnAttestRecordPackage; RecordOwnAttestRecordDelivery; VerifyOwnAttestRecordDelivery; ReleaseDeliveredEngagementHold.
- Queries: GetOwnAttestRecordPackage; GetOwnAttestRecordDelivery; ListOutstandingAttestDeliveryHolds.
- Events: OwnAttestRecordPackagePrepared; OwnAttestRecordDeliveryRecorded; OwnAttestRecordDeliveryVerified; DeliveredEngagementHoldReleased.
- Reactors: OwnAttestRecordPackageReactor; AttestRecordDeliveryReactor.
- Projectors: OwnAttestDeliveryProjector; EngagementDeliveryHoldProjector.
- API family: required record packages and attributed delivery into attest software; no native workpapers or report drafting.

### Organization handover — BO-01

- Aggregate/model candidates: OrganizationManagementHandover; HandoverGrantPlan; HandoverAcceptance.
- Commands: ProposeOrganizationManagementHandover; AcceptOrganizationManagementHandover; CompleteOrganizationManagementHandover; CancelOrganizationManagementHandover.
- Queries: PreviewOrganizationManagementHandover; GetOrganizationManagementHandover; ListOrganizationAdministrators.
- Events: OrganizationManagementHandoverProposed; OrganizationManagementHandoverAccepted; OrganizationManagementHandoverCompleted; OrganizationManagementHandoverCanceled.
- Reactors: OrganizationHandoverGrantReconciliationReactor.
- Projectors: OrganizationManagementHandoverProjector.
- API family: handover, acceptance and explicit retained/revoked administrative grants.

### Hosted billing — BO-02

- Aggregate/model candidates: OrganizationSubscription; BillingAccount; BillingAdministratorGrant; InvoiceReference; PaymentObservation; BillingPolicyVersion.
- Commands: ConfigureOrganizationBilling; AssignBillingAdministrator; RevokeBillingAdministrator; StartOrganizationSubscription; ChangeOrganizationSubscription; CancelOrganizationSubscription; RecordBillingProviderObservation; ReconcileBillingCharges.
- Queries: GetOrganizationSubscription; GetOrganizationBilling; ListBillingAdministrators; ListOrganizationInvoices; GetBillingChargeHistory.
- Events: OrganizationBillingConfigured; BillingAdministratorAssigned; BillingAdministratorRevoked; OrganizationSubscriptionStarted; OrganizationSubscriptionChanged; OrganizationSubscriptionCanceled; BillingProviderObservationRecorded; BillingChargesReconciled.
- Reactors: SubscriptionProvisioningReactor; BillingProviderWebhookReactor; BillingChargeReconciliationReactor.
- Projectors: OrganizationSubscriptionProjector; BillingHistoryProjector; BillingAdministratorProjector.
- API family: organization-specific subscription, billing administration and charge history; exact lifecycle awaits policy approval.

### Add-ons — BO-03

- Aggregate/model candidates: AddonOffering; OfferingVersion; OrganizationAddonSelection; AddonEntitlement; AddonAcceptance.
- Commands: PublishAddonOffering; SelectOrganizationAddon; AcceptOrganizationAddonTerms; ActivateOrganizationAddon; CancelOrganizationAddon; ReconcileAddonEntitlements.
- Queries: ListAddonOfferings; PreviewOrganizationAddonCharges; ListOrganizationAddons; GetOrganizationEntitlements.
- Events: AddonOfferingPublished; OrganizationAddonSelected; OrganizationAddonTermsAccepted; OrganizationAddonActivated; OrganizationAddonCanceled; AddonEntitlementsReconciled.
- Reactors: AddonProvisioningReactor; AddonEntitlementReconciliationReactor.
- Projectors: AddonOfferingProjector; OrganizationAddonProjector; OrganizationEntitlementProjector.
- API family: offerings, selection, acceptance, billing effects and entitlement lifecycle.

## Cross-cutting requirements and unresolved design

- Reuse the existing version, actor, decision, scope, content-identity and snapshot primitives. Keep review and approval transitions with the workflow; do not introduce a universal Review aggregate.
- Every owning entity needs only the lifecycle operations its contract permits: draft creation/revision, review, approval, successor, retirement and never-used-draft deletion where applicable. Historical use prevents destructive removal.
- Decisions bind exact versions, input identities, actor, time and rationale. Readiness calculations and population completeness remain distinct from auditor conclusions.
- Preserve manual capture for evidence, workforce/access populations, operational facts, professional handoffs and all compliance outcomes. Collection and integrations remain optional.
- The canonical catalog's finer device, resource, network and provider facets are not an instruction to build a full CMDB. Add persistence and APIs when an approved story consumes them.
- Every read must apply tenant, permission, sensitive-field and independence rules before rows, counts, cursors, exports or derived results become visible.
- Use versioned projector identities and atomic rows/checkpoints. Freshness applies per required source/projection; reproducible history requires retained exact inputs or snapshots.
- Schedule time-driven checks through an explicit clock/tick/job contract. Ordinary event consumption alone does not detect elapsed deadlines, expiry or missing occurrences. Reminders, waiver expiry, acceptance expiry, retention eligibility and recurring work each need an owning policy and safe scheduler.
- A reminder reactor must not silently approve work, expire a business decision, release a hold or dispose of content. Those transitions require their owning commands and authorization.
- Reactor follow-up commands and external effects need stable IDs, durable progress, retry/recovery and duplicate suppression. Distinguish committed intent from observed provider completion.
- Commercial command names and events remain provisional until pricing, payer, payment, lifecycle, handover and add-on acceptance policies are settled in business-operations.md.
- Portfolio queries require an explicit cross-client authority contract and independent per-client filtering. Existing tenant-local projections do not authorize shared cross-client storage.
- Offboarding retention duration, exact auditor deliverables, provider adapters and additional Privacy lifecycle coverage remain governed follow-up scope. Do not fill these gaps with invented policy.

## Exact existing backend inventory

The following is a static source inventory, including internal operations. Queries are classified by mapped HTTP GET operations and read/preview name conventions; remaining IRequest contracts are listed as commands. This is a planning classification, not an automated proof of purity. In particular, exports, previews and authentication POST operations require semantic review before exposure to automation. Supporting models include enums and interfaces in Compliance.Common; they are not all persistent business entities.

| Category | Unique feature/type names |
| --- | ---: |
| Commands | 230 |
| Queries | 271 |
| Events | 236 |
| Aggregates | 68 |
| Models and supporting contracts | 490 |
| Reactors | 30 |
| Projectors | 66 |
| Explicit MapPortia HTTP route registrations | 327 |

### Existing commands

#### AccessControl

- [AcknowledgeEngagementManagement](../../src/Compliance.Common/Features/AccessControl/AcknowledgeEngagementManagement.cs#L6) — `bdgrz.service-engagement.management-acknowledge` v1
- [AmendServiceEngagement](../../src/Compliance.Common/Features/AccessControl/AmendServiceEngagement.cs#L6) — `bdgrz.service-engagement.amend` v1
- [ApproveSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/ApproveSeparationOfDutiesWaiver.cs#L6) — `bdgrz.sod-waiver.approve` v1
- [AssignRolePermission](../../src/Compliance.Common/Features/AccessControl/AssignRolePermission.cs#L9) — `bdgrz.rbac.role-permission.assign` v1
- [AssignTeamMember](../../src/Compliance.Common/Features/AccessControl/AssignTeamMember.cs#L7) — `bdgrz.rbac.team-member.assign` v1
- [AssignTeamRole](../../src/Compliance.Common/Features/AccessControl/AssignTeamRole.cs#L7) — `bdgrz.rbac.team-role.assign` v1
- [CloseServiceEngagement](../../src/Compliance.Common/Features/AccessControl/CloseServiceEngagement.cs#L6) — `bdgrz.service-engagement.close` v1
- [CreateServiceEngagement](../../src/Compliance.Common/Features/AccessControl/CreateServiceEngagement.cs#L6) — `bdgrz.service-engagement.create` v1
- [DefineRole](../../src/Compliance.Common/Features/AccessControl/DefineRole.cs#L7) — `bdgrz.rbac.role.define` v1
- [DefineTeam](../../src/Compliance.Common/Features/AccessControl/DefineTeam.cs#L7) — `bdgrz.rbac.team.define` v1
- [DeleteRole](../../src/Compliance.Common/Features/AccessControl/DeleteRole.cs#L7) — `bdgrz.rbac.role.delete` v1
- [DeleteTeam](../../src/Compliance.Common/Features/AccessControl/DeleteTeam.cs#L7) — `bdgrz.rbac.team.delete` v1
- [DeprovisionMember](../../src/Compliance.Common/Features/AccessControl/DeprovisionMember.cs#L6) — `bdgrz.tenant.member.deprovision` v1
- [EvaluateClientIndependence](../../src/Compliance.Common/Features/AccessControl/EvaluateClientIndependence.cs#L7) — `bdgrz.independence.evaluate` v1
- [GrantAccess](../../src/Compliance.Common/Features/AccessControl/GrantAccess.cs#L6) — `bdgrz.access-grant.issue` v1
- [ProposeServiceEngagementStaff](../../src/Compliance.Common/Features/AccessControl/ProposeServiceEngagementStaff.cs#L6) — `bdgrz.service-engagement.staff.propose` v1
- [RecordDirectoryIndependenceReevaluation](../../src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluation.cs#L7) — `bdgrz.independence.record-directory-reevaluation` v1
- [RecordNonattestService](../../src/Compliance.Common/Features/AccessControl/RecordNonattestService.cs#L6) — `bdgrz.independence.service.record` v1
- [RecordSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/RecordSeparationOfDutiesWaiver.cs#L6) — `bdgrz.sod-waiver.record` v1
- [RegisterFirmStaff](../../src/Compliance.Common/Features/AccessControl/RegisterFirmStaff.cs#L6) — `bdgrz.firm-staff.register` v1
- [RegisterMember](../../src/Compliance.Common/Features/AccessControl/RegisterMember.cs#L6) — `bdgrz.rbac.member.register` v1
- [ReinstateMember](../../src/Compliance.Common/Features/AccessControl/ReinstateMember.cs#L6) — `bdgrz.tenant.member.reinstate` v1
- [RemoveRolePermission](../../src/Compliance.Common/Features/AccessControl/RemoveRolePermission.cs#L10) — `bdgrz.rbac.role-permission.remove` v1
- [RemoveTeamMember](../../src/Compliance.Common/Features/AccessControl/RemoveTeamMember.cs#L7) — `bdgrz.rbac.team-member.remove` v1
- [RemoveTeamRole](../../src/Compliance.Common/Features/AccessControl/RemoveTeamRole.cs#L7) — `bdgrz.rbac.team-role.remove` v1
- [RenameRole](../../src/Compliance.Common/Features/AccessControl/RenameRole.cs#L6) — `bdgrz.rbac.role.rename` v1
- [ReviseIndependenceRules](../../src/Compliance.Common/Features/AccessControl/ReviseIndependenceRules.cs#L6) — `bdgrz.independence.rules.revise` v1
- [RevokeAccessGrant](../../src/Compliance.Common/Features/AccessControl/RevokeAccessGrant.cs#L6) — `bdgrz.access-grant.revoke` v1
- [SetFirmStaffStatus](../../src/Compliance.Common/Features/AccessControl/SetFirmStaffStatus.cs#L6) — `bdgrz.firm-staff.status.set` v1
- [SuspendMember](../../src/Compliance.Common/Features/AccessControl/SuspendMember.cs#L6) — `bdgrz.tenant.member.suspend` v1
- [WithdrawServiceEngagementStaffProposal](../../src/Compliance.Common/Features/AccessControl/WithdrawServiceEngagementStaffProposal.cs#L6) — `bdgrz.service-engagement.staff.withdraw` v1

#### AccessReviews

- [AcceptAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/AcceptAccessPopulation.cs#L10) — `bdgrz.access_population.accept` v1
- [ApproveAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/ApproveAccessExpectation.cs#L10) — `bdgrz.access_expectation.approve` v1
- [ClassifyAccessPrincipal](../../src/Compliance.Common/Features/AccessReviews/ClassifyAccessPrincipal.cs#L7) — `bdgrz.access_population.principal.classify` v1
- [CompleteAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/CompleteAccessReviewCampaign.cs#L11) — `bdgrz.access_review.campaign.complete` v1
- [ExemptAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/ExemptAccessExpectation.cs#L10) — `bdgrz.access_expectation.exception.record` v1
- [ExemptAccessRemediation](../../src/Compliance.Common/Features/AccessReviews/ExemptAccessRemediation.cs#L10) — `bdgrz.access_review.remediation.exception.record` v1
- [ExemptMissingAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/ExemptMissingAccessPopulation.cs#L10) — `bdgrz.access_population.exception.record` v1
- [LaunchAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/LaunchAccessReviewCampaign.cs#L10) — `bdgrz.access_review.campaign.launch` v1
- [OpenAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/OpenAccessPopulation.cs#L7) — `bdgrz.access_population.open` v1
- [ProposeAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/ProposeAccessExpectation.cs#L7) — `bdgrz.access_expectation.propose` v1
- [RecordAccessDecision](../../src/Compliance.Common/Features/AccessReviews/RecordAccessDecision.cs#L10) — `bdgrz.access_review.decision.record` v1
- [RecordAccessPopulationFacts](../../src/Compliance.Common/Features/AccessReviews/RecordAccessPopulationFacts.cs#L7) — `bdgrz.access_population.facts.record` v1
- [RecordAccessRemediationChange](../../src/Compliance.Common/Features/AccessReviews/RecordAccessRemediationChange.cs#L7) — `bdgrz.access_review.remediation.change.record` v1
- [RecordBulkAccessDecision](../../src/Compliance.Common/Features/AccessReviews/RecordBulkAccessDecision.cs#L10) — `bdgrz.access_review.decision.bulk_record` v1
- [VerifyAccessRemediation](../../src/Compliance.Common/Features/AccessReviews/VerifyAccessRemediation.cs#L7) — `bdgrz.access_review.remediation.verify` v1

#### Applications

- [AcceptApplicationImport](../../src/Compliance.Common/Features/Applications/AcceptApplicationImport.cs#L6) — `bdgrz.application_import.accept` v1
- [AddApplicationRelationship](../../src/Compliance.Common/Features/Applications/AddApplicationRelationship.cs#L6) — `bdgrz.application.relationship.add` v1
- [ApplyApplicationImportEffect](../../src/Compliance.Common/Features/Applications/ApplyApplicationImportEffect.cs#L7) — `bdgrz.application_import.apply_effect` v1
- [CancelApplicationImport](../../src/Compliance.Common/Features/Applications/CancelApplicationImport.cs#L6) — `bdgrz.application_import.cancel` v1
- [CorrelateApplicationImportRow](../../src/Compliance.Common/Features/Applications/CorrelateApplicationImportRow.cs#L6) — `bdgrz.application_import.correlate_row` v1
- [DecideAccessReviewScope](../../src/Compliance.Common/Features/Applications/DecideAccessReviewScope.cs#L10) — `bdgrz.system_instance.access_review_scope.decide` v1
- [DeclareApplication](../../src/Compliance.Common/Features/Applications/DeclareApplication.cs#L6) — `bdgrz.application.declare` v1
- [DeclareSystemInstance](../../src/Compliance.Common/Features/Applications/DeclareSystemInstance.cs#L6) — `bdgrz.system_instance.declare` v1
- [ExecuteApplicationImport](../../src/Compliance.Common/Features/Applications/ExecuteApplicationImport.cs#L7) — `bdgrz.application_import.execute` v1
- [FreezeApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/FreezeApplicationImportOmissionProposal.cs#L7) — `bdgrz.application_import.omission.freeze` v1
- [RemoveApplicationRelationship](../../src/Compliance.Common/Features/Applications/RemoveApplicationRelationship.cs#L6) — `bdgrz.application.relationship.remove` v1
- [RetireApplication](../../src/Compliance.Common/Features/Applications/RetireApplication.cs#L6) — `bdgrz.application.retire` v1
- [RetireSystemInstance](../../src/Compliance.Common/Features/Applications/RetireSystemInstance.cs#L6) — `bdgrz.system_instance.retire` v1
- [ReviseApplication](../../src/Compliance.Common/Features/Applications/ReviseApplication.cs#L7) — `bdgrz.application.revise` v1
- [ReviseApplicationMcpInput](../../src/Compliance.App/Features/Applications/ReviseApplicationMcpInput.cs#L7) — `bdgrz.application.revise.mcp-input` v1
- [StageApplicationImport](../../src/Compliance.Common/Features/Applications/StageApplicationImport.cs#L6) — `bdgrz.application_import.stage` v1

#### Boundaries

- [ApproveBoundary](../../src/Compliance.Common/Features/Boundaries/ApproveBoundary.cs#L7) — `bdgrz.boundary.approve` v1
- [CreateBoundary](../../src/Compliance.Common/Features/Boundaries/CreateBoundary.cs#L8) — `bdgrz.boundary.create` v1
- [DiscardBoundaryDraft](../../src/Compliance.Common/Features/Boundaries/DiscardBoundaryDraft.cs#L7) — `bdgrz.boundary.draft.discard` v1
- [ProposeBoundarySuccessor](../../src/Compliance.Common/Features/Boundaries/ProposeBoundarySuccessor.cs#L7) — `bdgrz.boundary.successor.propose` v1
- [ReviewBoundary](../../src/Compliance.Common/Features/Boundaries/ReviewBoundary.cs#L7) — `bdgrz.boundary.review` v1
- [ReviseBoundaryDraft](../../src/Compliance.Common/Features/Boundaries/ReviseBoundaryDraft.cs#L7) — `bdgrz.boundary.draft.revise` v1

#### Commitments

- [ApproveCommitmentDraft](../../src/Compliance.Common/Features/Commitments/ApproveCommitmentDraft.cs#L12) — `bdgrz.commitment.draft.approve` v1
- [CreateCommitmentDraft](../../src/Compliance.Common/Features/Commitments/CreateCommitmentDraft.cs#L8) — `bdgrz.commitment.draft.create` v1
- [ReviewCommitmentDraft](../../src/Compliance.Common/Features/Commitments/ReviewCommitmentDraft.cs#L13) — `bdgrz.commitment.draft.review` v1
- [ReviseCommitmentDraft](../../src/Compliance.Common/Features/Commitments/ReviseCommitmentDraft.cs#L8) — `bdgrz.commitment.draft.revise` v1

#### ControlMappings

- [ProposeControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/ProposeControlCriterionMapping.cs#L12) — `bdgrz.control_mapping.propose` v1
- [ProposeCriterionNotApplicable](../../src/Compliance.Common/Features/ControlMappings/ProposeCriterionNotApplicable.cs#L13) — `bdgrz.criterion_applicability.propose_not_applicable` v1
- [RetireControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/RetireControlCriterionMapping.cs#L9) — `bdgrz.control_mapping.retire` v1
- [ReviewControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/ReviewControlCriterionMapping.cs#L9) — `bdgrz.control_mapping.review` v1
- [ReviewCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/ReviewCriterionApplicability.cs#L13) — `bdgrz.criterion_applicability.review` v1
- [WithdrawCriterionNotApplicable](../../src/Compliance.Common/Features/ControlMappings/WithdrawCriterionNotApplicable.cs#L9) — `bdgrz.criterion_applicability.withdraw_not_applicable` v1

#### Controls

- [ApproveControl](../../src/Compliance.Common/Features/Controls/ApproveControl.cs#L8) — `bdgrz.control.approve` v1
- [CreateControlDraft](../../src/Compliance.Common/Features/Controls/CreateControlDraft.cs#L8) — `bdgrz.control.draft.create` v1
- [DesignateControlOwnerPerson](../../src/Compliance.Common/Features/Controls/DesignateControlOwnerPerson.cs#L13) — `bdgrz.control.owner_person.designate` v1
- [DiscardControlDraft](../../src/Compliance.Common/Features/Controls/DiscardControlDraft.cs#L8) — `bdgrz.control.draft.discard` v1
- [ProposeControlRetirement](../../src/Compliance.Common/Features/Controls/ProposeControlRetirement.cs#L8) — `bdgrz.control.retirement.propose` v1
- [ProposeControlSuccessor](../../src/Compliance.Common/Features/Controls/ProposeControlSuccessor.cs#L8) — `bdgrz.control.successor.propose` v1
- [RetireControl](../../src/Compliance.Common/Features/Controls/RetireControl.cs#L8) — `bdgrz.control.retire` v1
- [ReviewControl](../../src/Compliance.Common/Features/Controls/ReviewControl.cs#L8) — `bdgrz.control.review` v1
- [ReviseControlDraft](../../src/Compliance.Common/Features/Controls/ReviseControlDraft.cs#L8) — `bdgrz.control.draft.revise` v1
- [WithdrawControlProposal](../../src/Compliance.Common/Features/Controls/WithdrawControlProposal.cs#L12) — `bdgrz.control.proposal.withdraw` v1

#### Criteria

- [SetCriteriaTextOverlay](../../src/Compliance.Common/Features/Criteria/SetCriteriaTextOverlay.cs#L9) — `bdgrz.criteria.overlay.set` v1

#### Evaluations

- [DefineControlEvaluationPlan](../../src/Compliance.Common/Features/Evaluations/DefineControlEvaluationPlan.cs#L8) — `bdgrz.control.evaluation.plan.define` v1
- [DisposeControlEvaluationDeviation](../../src/Compliance.Common/Features/Evaluations/DisposeControlEvaluationDeviation.cs#L9) — `bdgrz.control.evaluation.deviation.dispose` v1
- [RaiseEvaluationDeviationFinding](../../src/Compliance.Common/Features/Evaluations/RaiseEvaluationDeviationFinding.cs#L11) — `bdgrz.finding.raise_from_evaluation_deviation` v1
- [RecordControlEvaluationStep](../../src/Compliance.Common/Features/Evaluations/RecordControlEvaluationStep.cs#L12) — `bdgrz.control.evaluation.step.record` v1
- [ReviewControlEvaluation](../../src/Compliance.Common/Features/Evaluations/ReviewControlEvaluation.cs#L12) — `bdgrz.control.evaluation.review` v1
- [StartControlEvaluation](../../src/Compliance.Common/Features/Evaluations/StartControlEvaluation.cs#L12) — `bdgrz.control.evaluation.start` v1
- [SubmitControlEvaluation](../../src/Compliance.Common/Features/Evaluations/SubmitControlEvaluation.cs#L12) — `bdgrz.control.evaluation.submit` v1

#### Evidence

- [ApproveEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/ApproveEvidenceRedaction.cs#L7) — `bdgrz.evidence.redaction.approve` v1
- [CancelEvidenceRequest](../../src/Compliance.Common/Features/Evidence/CancelEvidenceRequest.cs#L9) — `bdgrz.evidence.request.cancel` v1
- [FulfilEvidenceRequest](../../src/Compliance.Common/Features/Evidence/FulfilEvidenceRequest.cs#L10) — `bdgrz.evidence.request.fulfil` v1
- [OpenEvidenceRequest](../../src/Compliance.Common/Features/Evidence/OpenEvidenceRequest.cs#L9) — `bdgrz.evidence.request.open` v1
- [PrepareEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/PrepareEvidenceRedaction.cs#L6) — `bdgrz.evidence.redaction.prepare` v1

#### Operations

- [ApproveControlOperatingPlan](../../src/Compliance.Common/Features/Operations/ApproveControlOperatingPlan.cs#L12) — `bdgrz.control.operating_plan.approve` v1
- [AttestControlOccurrence](../../src/Compliance.Common/Features/Operations/AttestControlOccurrence.cs#L11) — `bdgrz.control.occurrence.attest` v1
- [CorrectControlAttestation](../../src/Compliance.Common/Features/Operations/CorrectControlAttestation.cs#L8) — `bdgrz.control.occurrence.correct` v1
- [OpenControlOccurrence](../../src/Compliance.Common/Features/Operations/OpenControlOccurrence.cs#L8) — `bdgrz.control.occurrence.open` v1
- [ProposeControlOperatingPlan](../../src/Compliance.Common/Features/Operations/ProposeControlOperatingPlan.cs#L12) — `bdgrz.control.operating_plan.propose` v1
- [ReviewControlOccurrence](../../src/Compliance.Common/Features/Operations/ReviewControlOccurrence.cs#L11) — `bdgrz.control.occurrence.review` v1

#### Policies

- [ApprovePolicy](../../src/Compliance.Common/Features/Policies/ApprovePolicy.cs#L12) — `bdgrz.policy.approve` v1
- [ApprovePolicyRetirement](../../src/Compliance.Common/Features/Policies/ApprovePolicyRetirement.cs#L9) — `bdgrz.policy.retire` v1
- [ConfirmPolicyReview](../../src/Compliance.Common/Features/Policies/ConfirmPolicyReview.cs#L12) — `bdgrz.policy.periodic_review.confirm` v1
- [CreatePolicyDraft](../../src/Compliance.Common/Features/Policies/CreatePolicyDraft.cs#L8) — `bdgrz.policy.draft.create` v1
- [DiscardPolicyDraft](../../src/Compliance.Common/Features/Policies/DiscardPolicyDraft.cs#L9) — `bdgrz.policy.draft.discard` v1
- [ProposePolicyRetirement](../../src/Compliance.Common/Features/Policies/ProposePolicyRetirement.cs#L8) — `bdgrz.policy.retirement.propose` v1
- [ProposePolicySuccessor](../../src/Compliance.Common/Features/Policies/ProposePolicySuccessor.cs#L9) — `bdgrz.policy.successor.propose` v1
- [ReviewPolicyDraft](../../src/Compliance.Common/Features/Policies/ReviewPolicyDraft.cs#L12) — `bdgrz.policy.review` v1
- [RevisePolicyDraft](../../src/Compliance.Common/Features/Policies/RevisePolicyDraft.cs#L8) — `bdgrz.policy.draft.revise` v1

#### PolicyDistribution

- [AcknowledgePolicy](../../src/Compliance.Common/Features/PolicyDistribution/AcknowledgePolicy.cs#L12) — `bdgrz.policy.acknowledge` v1
- [ApproveCampaignWaiver](../../src/Compliance.Common/Features/PolicyDistribution/ApproveCampaignWaiver.cs#L12) — `bdgrz.campaign.waiver.approve` v1
- [CloseCampaign](../../src/Compliance.Common/Features/PolicyDistribution/CloseCampaign.cs#L9) — `bdgrz.campaign.close` v1
- [DefineTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/DefineTrainingRequirement.cs#L8) — `bdgrz.training_requirement.define` v1
- [LaunchPolicyCampaign](../../src/Compliance.Common/Features/PolicyDistribution/LaunchPolicyCampaign.cs#L12) — `bdgrz.policy_campaign.launch` v1
- [LaunchTrainingCampaign](../../src/Compliance.Common/Features/PolicyDistribution/LaunchTrainingCampaign.cs#L8) — `bdgrz.training_campaign.launch` v1
- [ReconcileCampaignAudience](../../src/Compliance.Common/Features/PolicyDistribution/ReconcileCampaignAudience.cs#L12) — `bdgrz.campaign.audience.reconcile` v1
- [RecordTrainingCompletion](../../src/Compliance.Common/Features/PolicyDistribution/RecordTrainingCompletion.cs#L9) — `bdgrz.training_completion.record` v1
- [ReviseTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/ReviseTrainingRequirement.cs#L9) — `bdgrz.training_requirement.revise` v1

#### Programs

- [CreateClientService](../../src/Compliance.Common/Features/Programs/CreateClientService.cs#L7) — `bdgrz.client-service.create` v2
- [CreateProgram](../../src/Compliance.Common/Features/Programs/CreateProgram.cs#L7) — `bdgrz.program.create` v1
- [RetireClientService](../../src/Compliance.Common/Features/Programs/RetireClientService.cs#L7) — `bdgrz.client-service.retire` v1
- [ReviseClientService](../../src/Compliance.Common/Features/Programs/ReviseClientService.cs#L7) — `bdgrz.client-service.revise` v1
- [ReviseProgram](../../src/Compliance.Common/Features/Programs/ReviseProgram.cs#L7) — `bdgrz.program.revise` v1
- [SelectProgramCriteriaEdition](../../src/Compliance.Common/Features/Programs/SelectProgramCriteriaEdition.cs#L7) — `bdgrz.program.criteria.select` v1

#### Providers

- [CloseProviderCoverageGap](../../src/Compliance.Common/Features/Providers/CloseProviderCoverageGap.cs#L8) — `bdgrz.provider.coverage_gap.close` v1
- [LinkProviderCoverageGapRiskAcceptance](../../src/Compliance.Common/Features/Providers/LinkProviderCoverageGapRiskAcceptance.cs#L9) — `bdgrz.provider.coverage_gap.link_risk_acceptance` v1
- [RecordAssuranceReport](../../src/Compliance.Common/Features/Providers/RecordAssuranceReport.cs#L8) — `bdgrz.provider.assurance_report.record` v1
- [RecordProvider](../../src/Compliance.Common/Features/Providers/RecordProvider.cs#L8) — `bdgrz.provider.record` v1
- [RecordProviderCoverageGap](../../src/Compliance.Common/Features/Providers/RecordProviderCoverageGap.cs#L7) — `bdgrz.provider.coverage_gap.record` v1
- [RecordProviderReview](../../src/Compliance.Common/Features/Providers/RecordProviderReview.cs#L11) — `bdgrz.provider.review.record` v1
- [ReviseAssuranceReport](../../src/Compliance.Common/Features/Providers/ReviseAssuranceReport.cs#L8) — `bdgrz.provider.assurance_report.revise` v1
- [ReviseProvider](../../src/Compliance.Common/Features/Providers/ReviseProvider.cs#L8) — `bdgrz.provider.revise` v1

#### Readiness

- [AnnotateReadinessGap](../../src/Compliance.Common/Features/Readiness/AnnotateReadinessGap.cs#L9) — `bdgrz.readiness.gap.annotate` v1
- [DecideReadiness](../../src/Compliance.Common/Features/Readiness/DecideReadiness.cs#L12) — `bdgrz.readiness.decide` v1
- [DecideTypeIEntry](../../src/Compliance.Common/Features/Readiness/DecideTypeIEntry.cs#L13) — `bdgrz.readiness.type_i_entry.decide` v1
- [PlanReadinessGap](../../src/Compliance.Common/Features/Readiness/PlanReadinessGap.cs#L9) — `bdgrz.readiness.gap.plan` v1
- [RunReadinessAssessment](../../src/Compliance.Common/Features/Readiness/RunReadinessAssessment.cs#L12) — `bdgrz.readiness.assessment.run` v1

#### Remediation

- [AddCorrectiveAction](../../src/Compliance.Common/Features/Remediation/AddCorrectiveAction.cs#L8) — `bdgrz.finding.corrective_action.add` v1
- [CloseFinding](../../src/Compliance.Common/Features/Remediation/CloseFinding.cs#L14) — `bdgrz.finding.close` v1
- [CompleteCorrectiveAction](../../src/Compliance.Common/Features/Remediation/CompleteCorrectiveAction.cs#L9) — `bdgrz.finding.corrective_action.complete` v1
- [LinkFindingAcceptance](../../src/Compliance.Common/Features/Remediation/LinkFindingAcceptance.cs#L12) — `bdgrz.finding.acceptance.link` v1
- [RaiseFinding](../../src/Compliance.Common/Features/Remediation/RaiseFinding.cs#L9) — `bdgrz.finding.raise` v1
- [RaiseOccurrenceFinding](../../src/Compliance.Common/Features/Remediation/RaiseOccurrenceFinding.cs#L11) — `bdgrz.finding.raise_from_occurrence` v1
- [ReopenFinding](../../src/Compliance.Common/Features/Remediation/ReopenFinding.cs#L8) — `bdgrz.finding.reopen` v1
- [ReviseFinding](../../src/Compliance.Common/Features/Remediation/ReviseFinding.cs#L9) — `bdgrz.finding.revise` v1

#### Responsibilities

- [AssignResponsibility](../../src/Compliance.Common/Features/Responsibilities/AssignResponsibility.cs#L8) — `bdgrz.responsibility.assign` v1
- [RevokeResponsibility](../../src/Compliance.Common/Features/Responsibilities/RevokeResponsibility.cs#L8) — `bdgrz.responsibility.revoke` v1

#### Retention

- [PlaceArtifactLegalHold](../../src/Compliance.Common/Features/Retention/PlaceArtifactLegalHold.cs#L7) — `bdgrz.artifact_retention.legal_hold.place` v1
- [RecordArtifactRetentionBasis](../../src/Compliance.Common/Features/Retention/RecordArtifactRetentionBasis.cs#L7) — `bdgrz.artifact_retention.basis.record` v1
- [ReleaseArtifactLegalHold](../../src/Compliance.Common/Features/Retention/ReleaseArtifactLegalHold.cs#L7) — `bdgrz.artifact_retention.legal_hold.release` v1

#### Risks

- [AcceptRisk](../../src/Compliance.Common/Features/Risks/AcceptRisk.cs#L13) — `bdgrz.risk.accept` v1
- [AddRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/AddRiskTreatmentAction.cs#L14) — `bdgrz.risk.treatment_action.add` v1
- [AssignRiskOwner](../../src/Compliance.Common/Features/Risks/AssignRiskOwner.cs#L9) — `bdgrz.risk.owner.assign` v1
- [CancelRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/CancelRiskTreatmentAction.cs#L11) — `bdgrz.risk.treatment_action.cancel` v1
- [ChooseRiskTreatment](../../src/Compliance.Common/Features/Risks/ChooseRiskTreatment.cs#L8) — `bdgrz.risk.treatment.choose` v1
- [CreateRiskDraft](../../src/Compliance.Common/Features/Risks/CreateRiskDraft.cs#L8) — `bdgrz.risk.draft.create` v1
- [ProposeRiskControlTreatment](../../src/Compliance.Common/Features/Risks/ProposeRiskControlTreatment.cs#L12) — `bdgrz.risk.control_treatment.propose` v1
- [PublishRiskMethodVersion](../../src/Compliance.Common/Features/Risks/PublishRiskMethodVersion.cs#L8) — `bdgrz.risk.method.publish` v1
- [RaiseRiskReassessmentTriggers](../../src/Compliance.Common/Features/Risks/RaiseRiskReassessmentTriggers.cs#L10) — `bdgrz.risk.reassessment.raise` v1
- [RecordRiskAssessment](../../src/Compliance.Common/Features/Risks/RecordRiskAssessment.cs#L8) — `bdgrz.risk.assessment.record` v1
- [RetireRiskControlTreatment](../../src/Compliance.Common/Features/Risks/RetireRiskControlTreatment.cs#L9) — `bdgrz.risk.control_treatment.retire` v1
- [ReviewRiskControlTreatment](../../src/Compliance.Common/Features/Risks/ReviewRiskControlTreatment.cs#L13) — `bdgrz.risk.control_treatment.review` v1
- [ReviewRiskTreatmentActionCompletion](../../src/Compliance.Common/Features/Risks/ReviewRiskTreatmentActionCompletion.cs#L13) — `bdgrz.risk.treatment_action.completion.review` v1
- [ReviseRiskDraft](../../src/Compliance.Common/Features/Risks/ReviseRiskDraft.cs#L8) — `bdgrz.risk.draft.revise` v1
- [ReviseRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/ReviseRiskTreatmentAction.cs#L9) — `bdgrz.risk.treatment_action.revise` v1
- [SubmitRiskTreatmentActionCompletion](../../src/Compliance.Common/Features/Risks/SubmitRiskTreatmentActionCompletion.cs#L13) — `bdgrz.risk.treatment_action.completion.submit` v1

#### Snapshots

- [AmendProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/AmendProgramScopeSnapshot.cs#L8) — `bdgrz.snapshot.program_scope.amend` v1
- [FreezeProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/FreezeProgramScopeSnapshot.cs#L8) — `bdgrz.snapshot.program_scope.freeze` v1

#### TechnologyInventory

- [RecordDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/RecordDataFlow.cs#L7) — `bdgrz.inventory.data_flow.record` v1
- [RecordInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/RecordInformationAsset.cs#L7) — `bdgrz.inventory.information_asset.record` v1
- [RecordLocation](../../src/Compliance.Common/Features/TechnologyInventory/RecordLocation.cs#L7) — `bdgrz.inventory.location.record` v1
- [RecordOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/RecordOperationalProcess.cs#L7) — `bdgrz.inventory.operational_process.record` v1
- [RecordTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/RecordTechnologyComponent.cs#L7) — `bdgrz.inventory.component.record` v1
- [ReviseDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/ReviseDataFlow.cs#L7) — `bdgrz.inventory.data_flow.revise` v1
- [ReviseInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/ReviseInformationAsset.cs#L6) — `bdgrz.inventory.information_asset.revise` v1
- [ReviseLocation](../../src/Compliance.Common/Features/TechnologyInventory/ReviseLocation.cs#L7) — `bdgrz.inventory.location.revise` v1
- [ReviseOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/ReviseOperationalProcess.cs#L6) — `bdgrz.inventory.operational_process.revise` v1
- [ReviseTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/ReviseTechnologyComponent.cs#L7) — `bdgrz.inventory.component.revise` v1

#### Tenants

- [AcceptTenantInvitation](../../src/Compliance.Common/Features/Tenants/AcceptTenantInvitation.cs#L6) — `bdgrz.tenant-invitation.accept` v1
- [ActivateTenant](../../src/Compliance.Common/Features/Tenants/ActivateTenant.cs#L6) — `bdgrz.tenant.activate` v1
- [ChangeTenantSlug](../../src/Compliance.Common/Features/Tenants/ChangeTenantSlug.cs#L6) — `bdgrz.tenant.change-slug` v1
- [ConfirmTenantSlug](../../src/Compliance.Common/Features/Tenants/ConfirmTenantSlug.cs#L6) — `bdgrz.tenant-slug.confirm` v1
- [ConfirmTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/ConfirmTenantSlugSurrender.cs#L6) — `bdgrz.tenant-slug-surrender.confirm` v1
- [GrantPlatformOperator](../../src/Compliance.Common/Features/Tenants/GrantPlatformOperator.cs#L6) — `bdgrz.platform.operator.grant` v1
- [InviteOrganizationMember](../../src/Compliance.Common/Features/Tenants/InviteOrganizationMember.cs#L7) — `bdgrz.organization-member.invite` v1
- [InviteTenantMember](../../src/Compliance.Common/Features/Tenants/InviteTenantMember.cs#L6) — `bdgrz.tenant-member.invite` v1
- [OffboardTenant](../../src/Compliance.Common/Features/Tenants/OffboardTenant.cs#L6) — `bdgrz.tenant.offboard` v1
- [ReactivateTenant](../../src/Compliance.Common/Features/Tenants/ReactivateTenant.cs#L6) — `bdgrz.tenant.reactivate` v2
- [RegisterTenant](../../src/Compliance.Common/Features/Tenants/RegisterTenant.cs#L10) — `bdgrz.tenant.register` v1
- [RegisterTenantOwner](../../src/Compliance.Common/Features/Tenants/RegisterTenantOwner.cs#L6) — `bdgrz.tenant-owner.register` v1
- [RegisterTenantSlug](../../src/Compliance.Common/Features/Tenants/RegisterTenantSlug.cs#L6) — `bdgrz.tenant-slug.register` v1
- [RejectTenantSlug](../../src/Compliance.Common/Features/Tenants/RejectTenantSlug.cs#L6) — `bdgrz.tenant-slug.reject` v1
- [RejectTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/RejectTenantSlugSurrender.cs#L6) — `bdgrz.tenant-slug-surrender.reject` v1
- [RequestTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/RequestTenantSlugSurrender.cs#L6) — `bdgrz.tenant-slug-surrender.request` v1
- [RevokePlatformOperator](../../src/Compliance.Common/Features/Tenants/RevokePlatformOperator.cs#L6) — `bdgrz.platform.operator.revoke` v1
- [SeedPlatformOperatorRoster](../../src/Compliance.Common/Features/Tenants/SeedPlatformOperatorRoster.cs#L5)
- [SurrenderTenantSlug](../../src/Compliance.Common/Features/Tenants/SurrenderTenantSlug.cs#L6) — `bdgrz.tenant-slug.surrender` v1
- [SuspendTenant](../../src/Compliance.Common/Features/Tenants/SuspendTenant.cs#L6) — `bdgrz.tenant.suspend` v2

#### UserIdentities

- [CompleteEmailChallenge](../../src/Compliance.Common/Features/UserIdentities/CompleteEmailChallenge.cs#L6) — `bdgrz.email-address.complete-challenge` v1
- [CompleteIdentityRecovery](../../src/Compliance.Common/Features/UserIdentities/CompleteIdentityRecovery.cs#L6) — `bdgrz.user-identity.complete-recovery` v1
- [ContinueWithDeveloperIdentity](../../src/Compliance.Common/Features/UserIdentities/ContinueWithDeveloperIdentity.cs#L7) — `bdgrz.developer-identity.continue` v1
- [ContinueWithOidcProvider](../../src/Compliance.Common/Features/UserIdentities/ContinueWithOidcProvider.cs#L7) — `bdgrz.oidc-provider.continue` v1
- [IssueEmailChallenge](../../src/Compliance.Common/Features/UserIdentities/IssueEmailChallenge.cs#L6) — `bdgrz.email-address.issue-challenge` v1
- [LinkOidcProviderIdentity](../../src/Compliance.Common/Features/UserIdentities/LinkOidcProviderIdentity.cs#L7) — `bdgrz.oidc-provider.link` v1
- [ReserveEmail](../../src/Compliance.Common/Features/UserIdentities/ReserveEmail.cs#L6) — `bdgrz.email-address.reserve` v1
- [StartIdentityRecovery](../../src/Compliance.Common/Features/UserIdentities/StartIdentityRecovery.cs#L6) — `bdgrz.user-identity.start-recovery` v1

#### Work

- [AssignWorkItem](../../src/Compliance.Common/Features/Work/AssignWorkItem.cs#L8) — `bdgrz.work.assign` v1
- [ClaimWorkItem](../../src/Compliance.Common/Features/Work/ClaimWorkItem.cs#L8) — `bdgrz.work.claim` v1
- [DelegateWorkItem](../../src/Compliance.Common/Features/Work/DelegateWorkItem.cs#L8) — `bdgrz.work.delegate` v1
- [EscalateWorkItem](../../src/Compliance.Common/Features/Work/EscalateWorkItem.cs#L8) — `bdgrz.work.escalate` v1
- [SetWorkDigestPreference](../../src/Compliance.Common/Features/Work/SetWorkDigestPreference.cs#L8) — `bdgrz.work.digest_preference.set` v1

#### Workforce

- [AmendWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/AmendWorkforceRosterSnapshot.cs#L12) — `bdgrz.snapshot.workforce_roster.amend` v1
- [CorrelatePersonMembership](../../src/Compliance.Common/Features/Workforce/CorrelatePersonMembership.cs#L13) — `bdgrz.workforce.person.correlate-membership` v1
- [FreezeWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/FreezeWorkforceRosterSnapshot.cs#L9) — `bdgrz.snapshot.workforce_roster.freeze` v1
- [ReconcileWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/ReconcileWorkforceSourceObservation.cs#L12) — `bdgrz.workforce.source.reconcile` v1
- [RecordPerson](../../src/Compliance.Common/Features/Workforce/RecordPerson.cs#L8) — `bdgrz.workforce.person.record` v1
- [RecordServiceIdentity](../../src/Compliance.Common/Features/Workforce/RecordServiceIdentity.cs#L13) — `bdgrz.workforce.service-identity.record` v1
- [RecordWorkRelationship](../../src/Compliance.Common/Features/Workforce/RecordWorkRelationship.cs#L11) — `bdgrz.workforce.work-relationship.record` v1
- [RecordWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/RecordWorkforceSourceObservation.cs#L8) — `bdgrz.workforce.source.record` v1
- [ResolveWorkforceObservation](../../src/Compliance.Common/Features/Workforce/ResolveWorkforceObservation.cs#L14) — `bdgrz.workforce.observation.resolve` v1
- [RevisePerson](../../src/Compliance.Common/Features/Workforce/RevisePerson.cs#L7) — `bdgrz.workforce.person.revise` v1
- [ReviseServiceIdentity](../../src/Compliance.Common/Features/Workforce/ReviseServiceIdentity.cs#L9) — `bdgrz.workforce.service-identity.revise` v1
- [ReviseWorkRelationship](../../src/Compliance.Common/Features/Workforce/ReviseWorkRelationship.cs#L8) — `bdgrz.workforce.work-relationship.revise` v1

### Existing queries

#### AccessControl

- [GetClientIndependenceHistory](../../src/Compliance.Common/Features/AccessControl/GetClientIndependenceHistory.cs#L6) — `bdgrz.independence.history.get` v1
- [GetClientIndependenceRules](../../src/Compliance.Common/Features/AccessControl/GetClientIndependenceRules.cs#L7) — `bdgrz.independence.client-rules.get` v1
- [GetEngagementManagementAcknowledgements](../../src/Compliance.Common/Features/AccessControl/GetEngagementManagementAcknowledgements.cs#L6) — `bdgrz.service-engagement.management-acknowledgements.get` v1
- [GetFirmStaffDirectory](../../src/Compliance.Common/Features/AccessControl/GetFirmStaffDirectory.cs#L6) — `bdgrz.firm-staff.list` v1
- [GetIndependenceRuleVersions](../../src/Compliance.Common/Features/AccessControl/GetIndependenceRuleVersions.cs#L6) — `bdgrz.independence.rules.list` v1
- [GetMemberAccess](../../src/Compliance.Common/Features/AccessControl/GetMemberAccess.cs#L6) — `bdgrz.member.access.get` v1
- [GetRole](../../src/Compliance.Common/Features/AccessControl/GetRole.cs#L7) — `bdgrz.rbac.role.get` v1
- [GetSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/GetSeparationOfDutiesWaiver.cs#L6) — `bdgrz.sod-waiver.get` v1
- [GetServiceEngagement](../../src/Compliance.Common/Features/AccessControl/GetServiceEngagement.cs#L6) — `bdgrz.service-engagement.get` v1
- [GetServiceEngagementAcceptance](../../src/Compliance.Common/Features/AccessControl/GetServiceEngagementAcceptance.cs#L6) — `bdgrz.service-engagement.acceptance.get` v1
- [GetServiceEngagementAcceptanceHistory](../../src/Compliance.Common/Features/AccessControl/GetServiceEngagementAcceptanceHistory.cs#L6) — `bdgrz.service-engagement.acceptance-history.get` v1
- [GetServiceEngagementHistory](../../src/Compliance.Common/Features/AccessControl/GetServiceEngagementHistory.cs#L6) — `bdgrz.service-engagement.history.get` v1
- [GetTeam](../../src/Compliance.Common/Features/AccessControl/GetTeam.cs#L7) — `bdgrz.rbac.team.get` v1
- [GetTenantMember](../../src/Compliance.Common/Features/AccessControl/GetTenantMember.cs#L7) — `bdgrz.tenant.member.get` v1
- [ListAccessGrants](../../src/Compliance.Common/Features/AccessControl/ListAccessGrants.cs#L6) — `bdgrz.access-grant.list` v1
- [ListAssignableFirmStaff](../../src/Compliance.Common/Features/AccessControl/ListAssignableFirmStaff.cs#L6) — `bdgrz.firm-staff.assignable.list` v1
- [ListRolePermissions](../../src/Compliance.Common/Features/AccessControl/ListRolePermissions.cs#L13) — `bdgrz.rbac.role-permission.list` v1
- [ListRoleTeams](../../src/Compliance.Common/Features/AccessControl/ListRoleTeams.cs#L13) — `bdgrz.rbac.role-team.list` v1
- [ListRoles](../../src/Compliance.Common/Features/AccessControl/ListRoles.cs#L13) — `bdgrz.rbac.role.list` v1
- [ListServiceEngagements](../../src/Compliance.Common/Features/AccessControl/ListServiceEngagements.cs#L6) — `bdgrz.service-engagement.list` v1
- [ListTeamMembers](../../src/Compliance.Common/Features/AccessControl/ListTeamMembers.cs#L13) — `bdgrz.rbac.team-member.list` v1
- [ListTeamRoles](../../src/Compliance.Common/Features/AccessControl/ListTeamRoles.cs#L13) — `bdgrz.rbac.team-role.list` v1
- [ListTeams](../../src/Compliance.Common/Features/AccessControl/ListTeams.cs#L13) — `bdgrz.rbac.team.list` v1

#### AccessReviews

- [GetAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/GetAccessPopulation.cs#L7) — `bdgrz.access_population.get` v1
- [GetAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/GetAccessReviewCampaign.cs#L10) — `bdgrz.access_review.campaign.get` v1
- [GetAccessReviewCoverage](../../src/Compliance.Common/Features/AccessReviews/GetAccessReviewCoverage.cs#L7) — `bdgrz.access_review.coverage.get` v1
- [GetAccessVariance](../../src/Compliance.Common/Features/AccessReviews/GetAccessVariance.cs#L7) — `bdgrz.access_population.variance.get` v1
- [ListAccessExpectations](../../src/Compliance.Common/Features/AccessReviews/ListAccessExpectations.cs#L7) — `bdgrz.access_expectation.list` v1
- [ListAccessPopulations](../../src/Compliance.Common/Features/AccessReviews/ListAccessPopulations.cs#L8) — `bdgrz.access_population.list` v1
- [ListAccessPrincipals](../../src/Compliance.Common/Features/AccessReviews/ListAccessPrincipals.cs#L8) — `bdgrz.access_population.principals.list` v1
- [ListAccessReviewCampaigns](../../src/Compliance.Common/Features/AccessReviews/ListAccessReviewCampaigns.cs#L8) — `bdgrz.access_review.campaign.list` v1
- [PreviewAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/PreviewAccessPopulation.cs#L7) — `bdgrz.access_population.preview` v1
- [PreviewBulkAccessDecision](../../src/Compliance.Common/Features/AccessReviews/PreviewBulkAccessDecision.cs#L7) — `bdgrz.access_review.decision.bulk_preview` v1

#### Applications

- [GetAccessReviewScope](../../src/Compliance.Common/Features/Applications/GetAccessReviewScope.cs#L6) — `bdgrz.system_instance.access_review_scope.get` v1
- [GetApplication](../../src/Compliance.Common/Features/Applications/GetApplication.cs#L6) — `bdgrz.application.get` v1
- [GetApplicationImport](../../src/Compliance.Common/Features/Applications/GetApplicationImport.cs#L6) — `bdgrz.application_import.get` v1
- [GetApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/GetApplicationImportOmissionProposal.cs#L7) — `bdgrz.application_import.omission.get` v1
- [GetApplicationImportProgress](../../src/Compliance.Common/Features/Applications/GetApplicationImportProgress.cs#L6) — `bdgrz.application_import.progress` v1
- [GetApplicationImportRejectedReport](../../src/Compliance.Common/Features/Applications/GetApplicationImportRejectedReport.cs#L6) — `bdgrz.application_import.rejected_report` v1
- [GetApplicationRevision](../../src/Compliance.Common/Features/Applications/GetApplicationRevision.cs#L6) — `bdgrz.application.revision.get` v1
- [GetSystemInstance](../../src/Compliance.Common/Features/Applications/GetSystemInstance.cs#L6) — `bdgrz.system_instance.get` v1
- [ListAccessReviewScopes](../../src/Compliance.Common/Features/Applications/ListAccessReviewScopes.cs#L11) — `bdgrz.application.access_review_scopes.list` v1
- [ListApplicationBoundaryReferences](../../src/Compliance.Common/Features/Applications/ListApplicationBoundaryReferences.cs#L8) — `bdgrz.application.boundary_references.list` v1
- [ListApplicationImportRows](../../src/Compliance.Common/Features/Applications/ListApplicationImportRows.cs#L7) — `bdgrz.application_import.rows.list` v1
- [ListApplicationRelationships](../../src/Compliance.Common/Features/Applications/ListApplicationRelationships.cs#L6) — `bdgrz.application.relationship.list` v1
- [ListApplicationRevisions](../../src/Compliance.Common/Features/Applications/ListApplicationRevisions.cs#L7) — `bdgrz.application.revision.list` v1
- [ListApplications](../../src/Compliance.Common/Features/Applications/ListApplications.cs#L7) — `bdgrz.application.list` v1
- [ListSystemInstanceBoundaryReferences](../../src/Compliance.Common/Features/Applications/ListSystemInstanceBoundaryReferences.cs#L8) — `bdgrz.system_instance.boundary_references.list` v1
- [ListSystemInstances](../../src/Compliance.Common/Features/Applications/ListSystemInstances.cs#L7) — `bdgrz.system_instance.list` v1
- [PreviewApplicationChange](../../src/Compliance.Common/Features/Applications/PreviewApplicationChange.cs#L8) — `bdgrz.application.change.preview` v1
- [PreviewApplicationChangeMcpInput](../../src/Compliance.App/Features/Applications/PreviewApplicationChangeMcpInput.cs#L7) — `bdgrz.application.change.preview.mcp-input` v1
- [PreviewApplicationImport](../../src/Compliance.Common/Features/Applications/PreviewApplicationImport.cs#L7) — `bdgrz.application_import.preview` v1
- [PreviewApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/PreviewApplicationImportOmissionProposal.cs#L6) — `bdgrz.application_import.omission.preview` v1
- [PreviewMissingApplicationImportRows](../../src/Compliance.Common/Features/Applications/PreviewMissingApplicationImportRows.cs#L7) — `bdgrz.application_import.missing.preview` v1

#### Boundaries

- [GetBoundary](../../src/Compliance.Common/Features/Boundaries/GetBoundary.cs#L6) — `bdgrz.boundary.get` v1
- [GetBoundaryDecision](../../src/Compliance.Common/Features/Boundaries/GetBoundaryDecision.cs#L6) — `bdgrz.boundary.decision.get` v1
- [GetBoundaryVersion](../../src/Compliance.Common/Features/Boundaries/GetBoundaryVersion.cs#L6) — `bdgrz.boundary.version.get` v1
- [GetEffectiveBoundaryVersion](../../src/Compliance.Common/Features/Boundaries/GetEffectiveBoundaryVersion.cs#L6) — `bdgrz.boundary.version.effective.get` v1
- [ListBoundaryDecisions](../../src/Compliance.Common/Features/Boundaries/ListBoundaryDecisions.cs#L7) — `bdgrz.boundary.decisions.list` v1
- [ListBoundaryVersions](../../src/Compliance.Common/Features/Boundaries/ListBoundaryVersions.cs#L7) — `bdgrz.boundary.versions.list` v1
- [ListProgramBoundaries](../../src/Compliance.Common/Features/Boundaries/ListProgramBoundaries.cs#L9) — `bdgrz.boundary.program.list` v1
- [PreviewBoundaryImpact](../../src/Compliance.Common/Features/Boundaries/PreviewBoundaryImpact.cs#L6) — `bdgrz.boundary.impact.preview` v1

#### Commitments

- [GetCommitmentDraft](../../src/Compliance.Common/Features/Commitments/GetCommitmentDraft.cs#L7) — `bdgrz.commitment.draft.get` v1
- [GetCommitmentDraftRevision](../../src/Compliance.Common/Features/Commitments/GetCommitmentDraftRevision.cs#L7) — `bdgrz.commitment.draft.revision.get` v1
- [GetCommitmentVersion](../../src/Compliance.Common/Features/Commitments/GetCommitmentVersion.cs#L7) — `bdgrz.commitment.version.get` v1
- [GetEffectiveCommitmentVersion](../../src/Compliance.Common/Features/Commitments/GetEffectiveCommitmentVersion.cs#L7) — `bdgrz.commitment.version.effective.get` v1
- [ListCommitmentDecisions](../../src/Compliance.Common/Features/Commitments/ListCommitmentDecisions.cs#L8) — `bdgrz.commitment.decision.list` v1
- [ListCommitmentDraftRevisions](../../src/Compliance.Common/Features/Commitments/ListCommitmentDraftRevisions.cs#L8) — `bdgrz.commitment.draft.revision.list` v1
- [ListCommitmentDrafts](../../src/Compliance.Common/Features/Commitments/ListCommitmentDrafts.cs#L8) — `bdgrz.commitment.draft.list` v1
- [ListCommitmentVersions](../../src/Compliance.Common/Features/Commitments/ListCommitmentVersions.cs#L8) — `bdgrz.commitment.version.list` v1
- [PreviewCommitmentImpact](../../src/Compliance.Common/Features/Commitments/PreviewCommitmentImpact.cs#L7) — `bdgrz.commitment.impact.preview` v1

#### ControlMappings

- [GetControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/GetControlCriterionMapping.cs#L7) — `bdgrz.control_mapping.get` v1
- [GetCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/GetCriterionApplicability.cs#L7) — `bdgrz.criterion_applicability.get` v1
- [ListControlCriterionMappings](../../src/Compliance.Common/Features/ControlMappings/ListControlCriterionMappings.cs#L9) — `bdgrz.control_mappings.list` v1
- [ListCriteriaCoverage](../../src/Compliance.Common/Features/ControlMappings/ListCriteriaCoverage.cs#L12) — `bdgrz.control_mappings.coverage.list` v1
- [ListCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/ListCriterionApplicability.cs#L9) — `bdgrz.criterion_applicability.list` v1

#### Controls

- [GetControlDecision](../../src/Compliance.Common/Features/Controls/GetControlDecision.cs#L7) — `bdgrz.control.decision.get` v1
- [GetControlDraft](../../src/Compliance.Common/Features/Controls/GetControlDraft.cs#L7) — `bdgrz.control.draft.get` v1
- [GetControlDraftOwnerPerson](../../src/Compliance.Common/Features/Controls/GetControlDraftOwnerPerson.cs#L7) — `bdgrz.control.draft.owner_person.get` v1
- [GetControlDraftRevision](../../src/Compliance.Common/Features/Controls/GetControlDraftRevision.cs#L7) — `bdgrz.control.draft.revision.get` v1
- [GetControlVersion](../../src/Compliance.Common/Features/Controls/GetControlVersion.cs#L7) — `bdgrz.control.version.get` v1
- [GetCurrentControlVersion](../../src/Compliance.Common/Features/Controls/GetCurrentControlVersion.cs#L7) — `bdgrz.control.version.current.get` v1
- [GetEffectiveControlVersion](../../src/Compliance.Common/Features/Controls/GetEffectiveControlVersion.cs#L7) — `bdgrz.control.version.effective.get` v1
- [ListControlDecisions](../../src/Compliance.Common/Features/Controls/ListControlDecisions.cs#L8) — `bdgrz.control.decisions.list` v1
- [ListControlDraftRevisions](../../src/Compliance.Common/Features/Controls/ListControlDraftRevisions.cs#L8) — `bdgrz.control.draft.revisions.list` v1
- [ListControlDrafts](../../src/Compliance.Common/Features/Controls/ListControlDrafts.cs#L8) — `bdgrz.control.draft.list` v1
- [ListControlVersions](../../src/Compliance.Common/Features/Controls/ListControlVersions.cs#L8) — `bdgrz.control.versions.list` v1
- [PreviewControlImpact](../../src/Compliance.Common/Features/Controls/PreviewControlImpact.cs#L8) — `bdgrz.control.impact.preview` v1

#### Criteria

- [ExportCriteriaCatalogEntries](../../src/Compliance.Common/Features/Criteria/ExportCriteriaCatalogEntries.cs#L8) — `bdgrz.criteria.entries.export` v1
- [GetCriteriaCatalogEdition](../../src/Compliance.Common/Features/Criteria/GetCriteriaCatalogEdition.cs#L8) — `bdgrz.criteria.edition.get` v1
- [GetCriteriaCatalogEntry](../../src/Compliance.Common/Features/Criteria/GetCriteriaCatalogEntry.cs#L8) — `bdgrz.criteria.entry.get` v1
- [ListCriteriaCatalogEditions](../../src/Compliance.Common/Features/Criteria/ListCriteriaCatalogEditions.cs#L8) — `bdgrz.criteria.editions.list` v1
- [ListCriteriaCatalogEntries](../../src/Compliance.Common/Features/Criteria/ListCriteriaCatalogEntries.cs#L8) — `bdgrz.criteria.entries.list` v1

#### Evaluations

- [GetControlEvaluation](../../src/Compliance.Common/Features/Evaluations/GetControlEvaluation.cs#L7) — `bdgrz.control.evaluation.get` v1
- [GetControlEvaluationPlanVersion](../../src/Compliance.Common/Features/Evaluations/GetControlEvaluationPlanVersion.cs#L7) — `bdgrz.control.evaluation.plan.version.get` v1
- [ListControlEvaluationPlanVersions](../../src/Compliance.Common/Features/Evaluations/ListControlEvaluationPlanVersions.cs#L8) — `bdgrz.control.evaluation.plan.versions.list` v1
- [ListControlEvaluations](../../src/Compliance.Common/Features/Evaluations/ListControlEvaluations.cs#L9) — `bdgrz.control.evaluations.list` v1

#### Evidence

- [GetEvidenceArtifactMetadata](../../src/Compliance.Common/Features/Evidence/GetEvidenceArtifactMetadata.cs#L6) — `bdgrz.evidence.artifact.metadata.get` v1
- [GetEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/GetEvidenceRedaction.cs#L6) — `bdgrz.evidence.redaction.get` v1
- [GetEvidenceRequest](../../src/Compliance.Common/Features/Evidence/GetEvidenceRequest.cs#L7) — `bdgrz.evidence.request.get` v1
- [ListEvidenceRequests](../../src/Compliance.Common/Features/Evidence/ListEvidenceRequests.cs#L9) — `bdgrz.evidence.requests.list` v1

#### Operations

- [GetControlOccurrence](../../src/Compliance.Common/Features/Operations/GetControlOccurrence.cs#L7) — `bdgrz.control.occurrence.get` v1
- [GetControlOperatingPlan](../../src/Compliance.Common/Features/Operations/GetControlOperatingPlan.cs#L7) — `bdgrz.control.operating_plan.get` v1
- [ListControlOccurrences](../../src/Compliance.Common/Features/Operations/ListControlOccurrences.cs#L9) — `bdgrz.control.occurrences.list` v1
- [ListControlOperatingBlockers](../../src/Compliance.Common/Features/Operations/ListControlOperatingBlockers.cs#L9) — `bdgrz.control.operating_blockers.list` v1
- [ListMyControlWork](../../src/Compliance.Common/Features/Operations/ListMyControlWork.cs#L8) — `bdgrz.control.work.mine` v1
- [PreviewControlOperatingPlan](../../src/Compliance.Common/Features/Operations/PreviewControlOperatingPlan.cs#L8) — `bdgrz.control.operating_plan.preview` v1

#### Policies

- [GetEffectivePolicyVersion](../../src/Compliance.Common/Features/Policies/GetEffectivePolicyVersion.cs#L8) — `bdgrz.policy.version.effective.get` v1
- [GetPolicy](../../src/Compliance.Common/Features/Policies/GetPolicy.cs#L7) — `bdgrz.policy.get` v1
- [GetPolicyVersion](../../src/Compliance.Common/Features/Policies/GetPolicyVersion.cs#L7) — `bdgrz.policy.version.get` v1
- [ListPolicies](../../src/Compliance.Common/Features/Policies/ListPolicies.cs#L8) — `bdgrz.policies.list` v1
- [ListPolicyDecisions](../../src/Compliance.Common/Features/Policies/ListPolicyDecisions.cs#L8) — `bdgrz.policy.decisions.list` v1
- [ListPolicyVersions](../../src/Compliance.Common/Features/Policies/ListPolicyVersions.cs#L8) — `bdgrz.policy.versions.list` v1
- [PreviewPolicyImpact](../../src/Compliance.Common/Features/Policies/PreviewPolicyImpact.cs#L7) — `bdgrz.policy.impact.preview` v1

#### PolicyDistribution

- [GetCampaign](../../src/Compliance.Common/Features/PolicyDistribution/GetCampaign.cs#L8) — `bdgrz.campaign.get` v1
- [GetTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/GetTrainingRequirement.cs#L8) — `bdgrz.training_requirement.get` v1
- [ListCampaignAmendments](../../src/Compliance.Common/Features/PolicyDistribution/ListCampaignAmendments.cs#L8) — `bdgrz.campaign.amendments.list` v1
- [ListCampaignParticipants](../../src/Compliance.Common/Features/PolicyDistribution/ListCampaignParticipants.cs#L12) — `bdgrz.campaign.participants.list` v1
- [ListCampaigns](../../src/Compliance.Common/Features/PolicyDistribution/ListCampaigns.cs#L8) — `bdgrz.campaigns.list` v1
- [ListTrainingRequirements](../../src/Compliance.Common/Features/PolicyDistribution/ListTrainingRequirements.cs#L8) — `bdgrz.training_requirements.list` v1

#### Programs

- [GetClientService](../../src/Compliance.Common/Features/Programs/GetClientService.cs#L6) — `bdgrz.client-service.get` v1
- [GetClientServiceRevision](../../src/Compliance.Common/Features/Programs/GetClientServiceRevision.cs#L6) — `bdgrz.client-service.revision.get` v1
- [GetProgram](../../src/Compliance.Common/Features/Programs/GetProgram.cs#L7) — `bdgrz.program.get` v1
- [GetProgramRevision](../../src/Compliance.Common/Features/Programs/GetProgramRevision.cs#L7) — `bdgrz.program.revision.get` v1
- [GetProgramSetupWork](../../src/Compliance.Common/Features/Programs/GetProgramSetupWork.cs#L7) — `bdgrz.program.setup-work.get` v1
- [ListClientServiceRevisions](../../src/Compliance.Common/Features/Programs/ListClientServiceRevisions.cs#L7) — `bdgrz.client-service.revisions.list` v1
- [ListClientServices](../../src/Compliance.Common/Features/Programs/ListClientServices.cs#L7) — `bdgrz.client-service.list` v1
- [ListProgramClientServices](../../src/Compliance.Common/Features/Programs/ListProgramClientServices.cs#L8) — `bdgrz.client-service.program.list` v1
- [ListProgramRevisions](../../src/Compliance.Common/Features/Programs/ListProgramRevisions.cs#L8) — `bdgrz.program.revisions.list` v1
- [ListPrograms](../../src/Compliance.Common/Features/Programs/ListPrograms.cs#L8) — `bdgrz.program.list` v1

#### Providers

- [GetProvider](../../src/Compliance.Common/Features/Providers/GetProvider.cs#L6) — `bdgrz.provider.get` v1
- [GetProviderAssuranceCoverage](../../src/Compliance.Common/Features/Providers/GetProviderAssuranceCoverage.cs#L7) — `bdgrz.provider.assurance_coverage.get` v1
- [GetProviderCoverageGap](../../src/Compliance.Common/Features/Providers/GetProviderCoverageGap.cs#L6) — `bdgrz.provider.coverage_gap.get` v1
- [GetProviderRevision](../../src/Compliance.Common/Features/Providers/GetProviderRevision.cs#L6) — `bdgrz.provider.revision.get` v1
- [ListProviderAssuranceReports](../../src/Compliance.Common/Features/Providers/ListProviderAssuranceReports.cs#L7) — `bdgrz.provider.assurance_reports.list` v1
- [ListProviderCoverageGaps](../../src/Compliance.Common/Features/Providers/ListProviderCoverageGaps.cs#L7) — `bdgrz.provider.coverage_gaps.list` v1
- [ListProviderReviews](../../src/Compliance.Common/Features/Providers/ListProviderReviews.cs#L7) — `bdgrz.provider.reviews.list` v1
- [ListProviderRevisions](../../src/Compliance.Common/Features/Providers/ListProviderRevisions.cs#L7) — `bdgrz.provider.revisions.list` v1
- [ListProviders](../../src/Compliance.Common/Features/Providers/ListProviders.cs#L7) — `bdgrz.providers.list` v1
- [PreviewProviderChange](../../src/Compliance.Common/Features/Providers/PreviewProviderChange.cs#L7) — `bdgrz.provider.change.preview` v1

#### Readiness

- [GetReadinessAssessment](../../src/Compliance.Common/Features/Readiness/GetReadinessAssessment.cs#L7) — `bdgrz.readiness.assessment.get` v1
- [ListReadinessAnnotations](../../src/Compliance.Common/Features/Readiness/ListReadinessAnnotations.cs#L9) — `bdgrz.readiness.annotations.list` v1
- [ListReadinessAssessments](../../src/Compliance.Common/Features/Readiness/ListReadinessAssessments.cs#L8) — `bdgrz.readiness.assessments.list` v1
- [ListReadinessGaps](../../src/Compliance.Common/Features/Readiness/ListReadinessGaps.cs#L12) — `bdgrz.readiness.gaps.list` v1
- [ListTypeIEntryDecisions](../../src/Compliance.Common/Features/Readiness/ListTypeIEntryDecisions.cs#L9) — `bdgrz.readiness.type_i_entry.list` v1

#### Remediation

- [GetFinding](../../src/Compliance.Common/Features/Remediation/GetFinding.cs#L7) — `bdgrz.finding.get` v1
- [ListFindings](../../src/Compliance.Common/Features/Remediation/ListFindings.cs#L9) — `bdgrz.findings.list` v1

#### Responsibilities

- [ListMemberResponsibilities](../../src/Compliance.Common/Features/Responsibilities/ListMemberResponsibilities.cs#L7) — `bdgrz.member.responsibilities.list` v1
- [ListResponsibilities](../../src/Compliance.Common/Features/Responsibilities/ListResponsibilities.cs#L8) — `bdgrz.responsibilities.list` v1
- [PreviewResponsibilityConflicts](../../src/Compliance.Common/Features/Responsibilities/PreviewResponsibilityConflicts.cs#L8) — `bdgrz.responsibilities.conflicts.preview` v1

#### Retention

- [GetApplicationImportRetention](../../src/Compliance.Common/Features/Retention/GetApplicationImportRetention.cs#L7) — `bdgrz.application_import.retention.get` v1
- [GetArtifactRetention](../../src/Compliance.Common/Features/Retention/GetArtifactRetention.cs#L6) — `bdgrz.artifact_retention.get` v1
- [ListArtifactLegalHolds](../../src/Compliance.Common/Features/Retention/ListArtifactLegalHolds.cs#L7) — `bdgrz.artifact_retention.legal_holds.list` v1

#### Risks

- [GetRiskDraft](../../src/Compliance.Common/Features/Risks/GetRiskDraft.cs#L7) — `bdgrz.risk.draft.get` v1
- [GetRiskDraftRevision](../../src/Compliance.Common/Features/Risks/GetRiskDraftRevision.cs#L7) — `bdgrz.risk.draft.revision.get` v1
- [GetRiskEvaluation](../../src/Compliance.Common/Features/Risks/GetRiskEvaluation.cs#L7) — `bdgrz.risk.evaluation.get` v1
- [GetRiskGovernance](../../src/Compliance.Common/Features/Risks/GetRiskGovernance.cs#L7) — `bdgrz.risk.governance.get` v1
- [GetRiskMethod](../../src/Compliance.Common/Features/Risks/GetRiskMethod.cs#L7) — `bdgrz.risk.method.get` v1
- [GetRiskMethodVersion](../../src/Compliance.Common/Features/Risks/GetRiskMethodVersion.cs#L7) — `bdgrz.risk.method.version.get` v1
- [ListRiskDraftRevisions](../../src/Compliance.Common/Features/Risks/ListRiskDraftRevisions.cs#L8) — `bdgrz.risk.draft.revisions.list` v1
- [ListRiskDrafts](../../src/Compliance.Common/Features/Risks/ListRiskDrafts.cs#L8) — `bdgrz.risk.draft.list` v1
- [ListRiskEvaluationHistory](../../src/Compliance.Common/Features/Risks/ListRiskEvaluationHistory.cs#L8) — `bdgrz.risk.evaluation.history.list` v1

#### Snapshots

- [GetSnapshot](../../src/Compliance.Common/Features/Snapshots/GetSnapshot.cs#L6) — `bdgrz.snapshot.get` v1
- [ListProgramSnapshots](../../src/Compliance.Common/Features/Snapshots/ListProgramSnapshots.cs#L9) — `bdgrz.snapshot.program.list` v1
- [RegenerateProgramScopeSnapshotManifest](../../src/Compliance.Common/Features/Snapshots/RegenerateProgramScopeSnapshotManifest.cs#L6) — `bdgrz.snapshot.program_scope.manifest_regenerate` v1
- [VerifyProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/VerifyProgramScopeSnapshot.cs#L6) — `bdgrz.snapshot.program_scope.verify` v1

#### TechnologyInventory

- [GetDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/GetDataFlow.cs#L6) — `bdgrz.inventory.data_flow.get` v1
- [GetInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/GetInformationAsset.cs#L6) — `bdgrz.inventory.information_asset.get` v1
- [GetLocation](../../src/Compliance.Common/Features/TechnologyInventory/GetLocation.cs#L6) — `bdgrz.inventory.location.get` v1
- [GetOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/GetOperationalProcess.cs#L6) — `bdgrz.inventory.operational_process.get` v1
- [GetTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/GetTechnologyComponent.cs#L6) — `bdgrz.inventory.component.get` v1
- [ListDataFlowRevisions](../../src/Compliance.Common/Features/TechnologyInventory/ListDataFlowRevisions.cs#L7) — `bdgrz.inventory.data_flow.revision.list` v1
- [ListDataFlows](../../src/Compliance.Common/Features/TechnologyInventory/ListDataFlows.cs#L7) — `bdgrz.inventory.data_flow.list` v1
- [ListInformationAssetBoundaryReferences](../../src/Compliance.Common/Features/TechnologyInventory/ListInformationAssetBoundaryReferences.cs#L9) — `bdgrz.inventory.asset.boundary_references.list` v1
- [ListInformationAssetRevisions](../../src/Compliance.Common/Features/TechnologyInventory/ListInformationAssetRevisions.cs#L7) — `bdgrz.inventory.information_asset.revision.list` v1
- [ListInformationAssets](../../src/Compliance.Common/Features/TechnologyInventory/ListInformationAssets.cs#L7) — `bdgrz.inventory.information_asset.list` v1
- [ListLocationRevisions](../../src/Compliance.Common/Features/TechnologyInventory/ListLocationRevisions.cs#L7) — `bdgrz.inventory.location.revision.list` v1
- [ListLocations](../../src/Compliance.Common/Features/TechnologyInventory/ListLocations.cs#L7) — `bdgrz.inventory.location.list` v1
- [ListOperationalProcessRevisions](../../src/Compliance.Common/Features/TechnologyInventory/ListOperationalProcessRevisions.cs#L7) — `bdgrz.inventory.operational_process.revision.list` v1
- [ListOperationalProcesses](../../src/Compliance.Common/Features/TechnologyInventory/ListOperationalProcesses.cs#L7) — `bdgrz.inventory.operational_process.list` v1
- [ListTechnologyComponentBoundaryReferences](../../src/Compliance.Common/Features/TechnologyInventory/ListTechnologyComponentBoundaryReferences.cs#L9) — `bdgrz.inventory.component.boundary_references.list` v1
- [ListTechnologyComponentRevisions](../../src/Compliance.Common/Features/TechnologyInventory/ListTechnologyComponentRevisions.cs#L7) — `bdgrz.inventory.component.revision.list` v1
- [ListTechnologyComponents](../../src/Compliance.Common/Features/TechnologyInventory/ListTechnologyComponents.cs#L7) — `bdgrz.inventory.component.list` v1
- [PreviewInformationAssetChange](../../src/Compliance.Common/Features/TechnologyInventory/PreviewInformationAssetChange.cs#L10) — `bdgrz.inventory.information_asset.change.preview` v1

#### Tenants

- [GetTenant](../../src/Compliance.Common/Features/Tenants/GetTenant.cs#L6) — `bdgrz.tenant.get` v1
- [GetTenantProvisioningStatus](../../src/Compliance.Common/Features/Tenants/GetTenantProvisioningStatus.cs#L6) — `bdgrz.tenant.provisioning_status.get` v1
- [ListMyTenants](../../src/Compliance.Common/Features/Tenants/ListMyTenants.cs#L14) — `bdgrz.tenant-membership.list-mine` v1
- [ListPlatformOperators](../../src/Compliance.Common/Features/Tenants/ListPlatformOperators.cs#L6) — `bdgrz.platform.operator.list` v1
- [ListTenantInvitations](../../src/Compliance.Common/Features/Tenants/ListTenantInvitations.cs#L8) — `bdgrz.tenant-invitation.list` v1
- [ListTenantMembers](../../src/Compliance.Common/Features/Tenants/ListTenantMembers.cs#L7) — `bdgrz.tenant-member.list` v1
- [ListTenants](../../src/Compliance.Common/Features/Tenants/ListTenants.cs#L11) — `bdgrz.platform.tenant.list` v1
- [ResolveMyTenantSlug](../../src/Compliance.Common/Features/Tenants/ResolveMyTenantSlug.cs#L6) — `bdgrz.tenant-slug.resolve-mine` v1

#### UserIdentities

- [GetEmailAddress](../../src/Compliance.Common/Features/UserIdentities/GetEmailAddress.cs#L6) — `bdgrz.email-address.get` v1
- [GetEmailChallengeStatus](../../src/Compliance.Common/Features/UserIdentities/GetEmailChallengeStatus.cs#L5)
- [GetIdentityRecoveryOptions](../../src/Compliance.Common/Features/UserIdentities/GetIdentityRecoveryOptions.cs#L7) — `bdgrz.user-identity.recovery-options` v1
- [ListEmailAddresses](../../src/Compliance.Common/Features/UserIdentities/ListEmailAddresses.cs#L7) — `bdgrz.email-address.list` v1

#### Work

- [GetWorkDigest](../../src/Compliance.Common/Features/Work/GetWorkDigest.cs#L8) — `bdgrz.work.digest.get` v1
- [GetWorkDigestPreference](../../src/Compliance.Common/Features/Work/GetWorkDigestPreference.cs#L8) — `bdgrz.work.digest_preference.get` v1
- [GetWorkItem](../../src/Compliance.Common/Features/Work/GetWorkItem.cs#L8) — `bdgrz.work.get` v1
- [ListWork](../../src/Compliance.Common/Features/Work/ListWork.cs#L13) — `bdgrz.work.list` v1
- [ListWorkReminders](../../src/Compliance.Common/Features/Work/ListWorkReminders.cs#L11) — `bdgrz.work.reminders.list` v1

#### Workforce

- [GetPerson](../../src/Compliance.Common/Features/Workforce/GetPerson.cs#L6) — `bdgrz.workforce.person.get` v1
- [GetServiceIdentity](../../src/Compliance.Common/Features/Workforce/GetServiceIdentity.cs#L6) — `bdgrz.workforce.service-identity.get` v1
- [GetWorkRelationship](../../src/Compliance.Common/Features/Workforce/GetWorkRelationship.cs#L6) — `bdgrz.workforce.work-relationship.get` v1
- [GetWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/GetWorkforceRosterSnapshot.cs#L7) — `bdgrz.snapshot.workforce_roster.get` v1
- [GetWorkforceRosterSnapshotAsOf](../../src/Compliance.Common/Features/Workforce/GetWorkforceRosterSnapshotAsOf.cs#L7) — `bdgrz.snapshot.workforce_roster.as_of` v1
- [GetWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/GetWorkforceSourceObservation.cs#L7) — `bdgrz.workforce.source.get` v1
- [ListPeople](../../src/Compliance.Common/Features/Workforce/ListPeople.cs#L7) — `bdgrz.workforce.person.list` v1
- [ListServiceIdentities](../../src/Compliance.Common/Features/Workforce/ListServiceIdentities.cs#L11) — `bdgrz.workforce.service-identity.list` v1
- [ListWorkRelationships](../../src/Compliance.Common/Features/Workforce/ListWorkRelationships.cs#L8) — `bdgrz.workforce.work-relationship.list` v1
- [ListWorkforceObservations](../../src/Compliance.Common/Features/Workforce/ListWorkforceObservations.cs#L11) — `bdgrz.workforce.observation.list` v1
- [ListWorkforceReconciliationObservations](../../src/Compliance.Common/Features/Workforce/ListWorkforceReconciliationObservations.cs#L12) — `bdgrz.workforce.reconciliation.list` v1
- [ListWorkforceRosterSnapshots](../../src/Compliance.Common/Features/Workforce/ListWorkforceRosterSnapshots.cs#L9) — `bdgrz.snapshot.workforce_roster.list` v1
- [ListWorkforceSourceObservations](../../src/Compliance.Common/Features/Workforce/ListWorkforceSourceObservations.cs#L8) — `bdgrz.workforce.source.list` v1
- [PreviewWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/PreviewWorkforceSourceObservation.cs#L7) — `bdgrz.workforce.source.preview` v1
- [RegenerateWorkforceRosterSnapshotManifest](../../src/Compliance.Common/Features/Workforce/RegenerateWorkforceRosterSnapshotManifest.cs#L8) — `bdgrz.snapshot.workforce_roster.manifest_regenerate` v1

#### Requirement-backed read contracts added 2026-10-17

##### AdvisorCollaboration

- [GetAdvisorRequest](../../src/Compliance.Common/Features/AdvisorCollaboration/GetAdvisorRequest.cs#L9) — `bdgrz.advisor-collaboration.get-advisor-request` v1
- [ListAdvisorRequests](../../src/Compliance.Common/Features/AdvisorCollaboration/ListAdvisorRequests.cs#L9) — `bdgrz.advisor-collaboration.list-advisor-requests` v1
- [ListAdvisorFeedback](../../src/Compliance.Common/Features/AdvisorCollaboration/ListAdvisorFeedback.cs#L9) — `bdgrz.advisor-collaboration.list-advisor-feedback` v1

##### Examinations

- [GetExaminationEngagement](../../src/Compliance.Common/Features/Examinations/GetExaminationEngagement.cs#L9) — `bdgrz.examinations.get-examination-engagement` v1
- [GetTypeIBaseline](../../src/Compliance.Common/Features/Examinations/GetTypeIBaseline.cs#L9) — `bdgrz.examinations.get-type-i-baseline` v1
- [ListEngagementAccess](../../src/Compliance.Common/Features/Examinations/ListEngagementAccess.cs#L9) — `bdgrz.examinations.list-engagement-access` v1

##### SystemDescription

- [GetSystemDescription](../../src/Compliance.Common/Features/SystemDescription/GetSystemDescription.cs#L9) — `bdgrz.system-description.get-system-description` v1
- [ListSystemDescriptionVersions](../../src/Compliance.Common/Features/SystemDescription/ListSystemDescriptionVersions.cs#L9) — `bdgrz.system-description.list-system-description-versions` v1
- [PreviewSystemDescriptionImpact](../../src/Compliance.Common/Features/SystemDescription/PreviewSystemDescriptionImpact.cs#L9) — `bdgrz.system-description.preview-system-description-impact` v1

##### AuditorRequests

- [GetAuditorRequest](../../src/Compliance.Common/Features/AuditorRequests/GetAuditorRequest.cs#L9) — `bdgrz.auditor-requests.get-auditor-request` v1
- [ListAuditorRequests](../../src/Compliance.Common/Features/AuditorRequests/ListAuditorRequests.cs#L9) — `bdgrz.auditor-requests.list-auditor-requests` v1
- [GetSampleSelection](../../src/Compliance.Common/Features/AuditorRequests/GetSampleSelection.cs#L9) — `bdgrz.auditor-requests.get-sample-selection` v1
- [ListRequestResponses](../../src/Compliance.Common/Features/AuditorRequests/ListRequestResponses.cs#L9) — `bdgrz.auditor-requests.list-request-responses` v1

##### AuditPackages

- [GetAuditPackage](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackage.cs#L9) — `bdgrz.audit-packages.get-audit-package` v1
- [ListAuditPackages](../../src/Compliance.Common/Features/AuditPackages/ListAuditPackages.cs#L9) — `bdgrz.audit-packages.list-audit-packages` v1
- [GetAuditPackageManifest](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackageManifest.cs#L9) — `bdgrz.audit-packages.get-audit-package-manifest` v1
- [GetAuditPackageValidation](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackageValidation.cs#L9) — `bdgrz.audit-packages.get-audit-package-validation` v1
- [GetAuditPackageDelivery](../../src/Compliance.Common/Features/AuditPackages/GetAuditPackageDelivery.cs#L9) — `bdgrz.audit-packages.get-audit-package-delivery` v1

##### Examinations

- [ListAuditObservations](../../src/Compliance.Common/Features/Examinations/ListAuditObservations.cs#L9) — `bdgrz.examinations.list-audit-observations` v1
- [GetAuditOutcome](../../src/Compliance.Common/Features/Examinations/GetAuditOutcome.cs#L9) — `bdgrz.examinations.get-audit-outcome` v1
- [GetTypeIIPlan](../../src/Compliance.Common/Features/Examinations/GetTypeIIPlan.cs#L9) — `bdgrz.examinations.get-type-i-i-plan` v1
- [GetManagementAssertion](../../src/Compliance.Common/Features/Examinations/GetManagementAssertion.cs#L9) — `bdgrz.examinations.get-management-assertion` v1
- [GetExaminationSignOff](../../src/Compliance.Common/Features/Examinations/GetExaminationSignOff.cs#L9) — `bdgrz.examinations.get-examination-sign-off` v1

##### Operations

- [GetObservationPeriod](../../src/Compliance.Common/Features/Operations/GetObservationPeriod.cs#L9) — `bdgrz.operations.get-observation-period` v1
- [ListObservationPeriods](../../src/Compliance.Common/Features/Operations/ListObservationPeriods.cs#L9) — `bdgrz.operations.list-observation-periods` v1
- [GetControlCalendar](../../src/Compliance.Common/Features/Operations/GetControlCalendar.cs#L9) — `bdgrz.operations.get-control-calendar` v1
- [ListMissingOccurrences](../../src/Compliance.Common/Features/Operations/ListMissingOccurrences.cs#L9) — `bdgrz.operations.list-missing-occurrences` v1
- [ListDueGovernanceReviews](../../src/Compliance.Common/Features/Operations/ListDueGovernanceReviews.cs#L9) — `bdgrz.operations.list-due-governance-reviews` v1
- [GetManagementReview](../../src/Compliance.Common/Features/Operations/GetManagementReview.cs#L9) — `bdgrz.operations.get-management-review` v1
- [ListManagementReviewActions](../../src/Compliance.Common/Features/Operations/ListManagementReviewActions.cs#L9) — `bdgrz.operations.list-management-review-actions` v1

##### Changes

- [GetSignificantChange](../../src/Compliance.Common/Features/Changes/GetSignificantChange.cs#L9) — `bdgrz.changes.get-significant-change` v1
- [ListSignificantChanges](../../src/Compliance.Common/Features/Changes/ListSignificantChanges.cs#L9) — `bdgrz.changes.list-significant-changes` v1

##### Incidents

- [GetComplianceIncident](../../src/Compliance.Common/Features/Incidents/GetComplianceIncident.cs#L9) — `bdgrz.incidents.get-compliance-incident` v1
- [ListComplianceIncidents](../../src/Compliance.Common/Features/Incidents/ListComplianceIncidents.cs#L9) — `bdgrz.incidents.list-compliance-incidents` v1
- [GetComplianceImpactAssessment](../../src/Compliance.Common/Features/Incidents/GetComplianceImpactAssessment.cs#L9) — `bdgrz.incidents.get-compliance-impact-assessment` v1

##### AuditPopulations

- [PreviewObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/PreviewObservationPeriodClose.cs#L9) — `bdgrz.audit-populations.preview-observation-period-close` v1
- [GetObservationPeriodClose](../../src/Compliance.Common/Features/AuditPopulations/GetObservationPeriodClose.cs#L9) — `bdgrz.audit-populations.get-observation-period-close` v1
- [ListAuditPopulationDefinitions](../../src/Compliance.Common/Features/AuditPopulations/ListAuditPopulationDefinitions.cs#L9) — `bdgrz.audit-populations.list-audit-population-definitions` v1
- [GetAuditPopulation](../../src/Compliance.Common/Features/AuditPopulations/GetAuditPopulation.cs#L9) — `bdgrz.audit-populations.get-audit-population` v1
- [ListAuditPopulationRows](../../src/Compliance.Common/Features/AuditPopulations/ListAuditPopulationRows.cs#L9) — `bdgrz.audit-populations.list-audit-population-rows` v1
- [GetPopulationReconciliation](../../src/Compliance.Common/Features/AuditPopulations/GetPopulationReconciliation.cs#L9) — `bdgrz.audit-populations.get-population-reconciliation` v1

##### Programs

- [PreviewProgramRollover](../../src/Compliance.Common/Features/Programs/PreviewProgramRollover.cs#L9) — `bdgrz.programs.preview-program-rollover` v1
- [GetProgramRollover](../../src/Compliance.Common/Features/Programs/GetProgramRollover.cs#L9) — `bdgrz.programs.get-program-rollover` v1
- [GetNextPeriodPlan](../../src/Compliance.Common/Features/Programs/GetNextPeriodPlan.cs#L9) — `bdgrz.programs.get-next-period-plan` v1
- [ListCarryForwardReferences](../../src/Compliance.Common/Features/Programs/ListCarryForwardReferences.cs#L9) — `bdgrz.programs.list-carry-forward-references` v1

##### FirmPortfolio

- [ListAuthorizedClientPortfolio](../../src/Compliance.Common/Features/FirmPortfolio/ListAuthorizedClientPortfolio.cs#L9) — `bdgrz.firm-portfolio.list-authorized-client-portfolio` v1
- [GetAuthorizedClientSummary](../../src/Compliance.Common/Features/FirmPortfolio/GetAuthorizedClientSummary.cs#L9) — `bdgrz.firm-portfolio.get-authorized-client-summary` v1
- [ListMyCrossClientWork](../../src/Compliance.Common/Features/FirmPortfolio/ListMyCrossClientWork.cs#L9) — `bdgrz.firm-portfolio.list-my-cross-client-work` v1

##### MethodologyTemplates

- [ListMethodologyTemplates](../../src/Compliance.Common/Features/MethodologyTemplates/ListMethodologyTemplates.cs#L9) — `bdgrz.methodology-templates.list-methodology-templates` v1
- [GetMethodologyTemplateVersion](../../src/Compliance.Common/Features/MethodologyTemplates/GetMethodologyTemplateVersion.cs#L9) — `bdgrz.methodology-templates.get-methodology-template-version` v1
- [PreviewMethodologyTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/PreviewMethodologyTemplateApplication.cs#L9) — `bdgrz.methodology-templates.preview-methodology-template-application` v1
- [GetTemplateApplication](../../src/Compliance.Common/Features/MethodologyTemplates/GetTemplateApplication.cs#L9) — `bdgrz.methodology-templates.get-template-application` v1

##### AttestRecords

- [GetOwnAttestRecordPackage](../../src/Compliance.Common/Features/AttestRecords/GetOwnAttestRecordPackage.cs#L9) — `bdgrz.attest-records.get-own-attest-record-package` v1
- [GetOwnAttestRecordDelivery](../../src/Compliance.Common/Features/AttestRecords/GetOwnAttestRecordDelivery.cs#L9) — `bdgrz.attest-records.get-own-attest-record-delivery` v1
- [ListOutstandingAttestDeliveryHolds](../../src/Compliance.Common/Features/AttestRecords/ListOutstandingAttestDeliveryHolds.cs#L9) — `bdgrz.attest-records.list-outstanding-attest-delivery-holds` v1

### Existing events

#### AccessControl

- [AccessGrantIssued](../../src/Compliance.Common/Features/AccessControl/AccessGrantIssued.cs#L6) — `bdgrz.access-grant.issued` v1
- [AccessGrantRevoked](../../src/Compliance.Common/Features/AccessControl/AccessGrantRevoked.cs#L6) — `bdgrz.access-grant.revoked` v1
- [AssignmentIndependenceReevaluated](../../src/Compliance.Common/Features/AccessControl/AssignmentIndependenceReevaluated.cs#L6) — `bdgrz.independence.assignment-reevaluated` v1
- [ClientIndependenceEvaluated](../../src/Compliance.Common/Features/AccessControl/ClientIndependenceEvaluated.cs#L6) — `bdgrz.independence.evaluated` v1
- [DirectoryIndependenceReevaluated](../../src/Compliance.Common/Features/AccessControl/DirectoryIndependenceReevaluated.cs#L6) — `bdgrz.independence.directory-reevaluated` v1
- [EngagementManagementAcknowledged](../../src/Compliance.Common/Features/AccessControl/EngagementManagementAcknowledged.cs#L6) — `bdgrz.service-engagement.management-acknowledged` v1
- [FirmStaffChangeRecorded](../../src/Compliance.Common/Features/AccessControl/FirmStaffChangeRecorded.cs#L6) — `bdgrz.firm-staff.changed` v1
- [IndependenceRulesRevised](../../src/Compliance.Common/Features/AccessControl/IndependenceRulesRevised.cs#L6) — `bdgrz.independence.rules.revised` v1
- [MemberDeprovisionCleanupCompleted](../../src/Compliance.Common/Features/AccessControl/MemberDeprovisionCleanupCompleted.cs#L6) — `bdgrz.rbac.member.deprovision_cleanup_completed` v1
- [MemberDeprovisioned](../../src/Compliance.Common/Features/AccessControl/MemberDeprovisioned.cs#L6) — `bdgrz.rbac.member.deprovisioned` v1
- [MemberRegistered](../../src/Compliance.Common/Features/AccessControl/MemberRegistered.cs#L6) — `bdgrz.rbac.member.registered` v1
- [MemberReinstated](../../src/Compliance.Common/Features/AccessControl/MemberReinstated.cs#L6) — `bdgrz.rbac.member.reinstated` v1
- [MemberSuspended](../../src/Compliance.Common/Features/AccessControl/MemberSuspended.cs#L6) — `bdgrz.rbac.member.suspended` v1
- [NonattestServiceRecorded](../../src/Compliance.Common/Features/AccessControl/NonattestServiceRecorded.cs#L6) — `bdgrz.independence.service.recorded` v1
- [RoleDefined](../../src/Compliance.Common/Features/AccessControl/RoleDefined.cs#L6) — `bdgrz.rbac.role.defined` v1
- [RoleDeleted](../../src/Compliance.Common/Features/AccessControl/RoleDeleted.cs#L6) — `bdgrz.rbac.role.deleted` v1
- [RolePermissionAssigned](../../src/Compliance.Common/Features/AccessControl/RolePermissionAssigned.cs#L6) — `bdgrz.rbac.role-permission.assigned` v1
- [RolePermissionRemoved](../../src/Compliance.Common/Features/AccessControl/RolePermissionRemoved.cs#L6) — `bdgrz.rbac.role-permission.removed` v1
- [RoleRenamed](../../src/Compliance.Common/Features/AccessControl/RoleRenamed.cs#L6) — `bdgrz.rbac.role.renamed` v1
- [SeparationOfDutiesWaiverApproved](../../src/Compliance.Common/Features/AccessControl/SeparationOfDutiesWaiverApproved.cs#L7) — `bdgrz.sod-waiver.approved` v1
- [SeparationOfDutiesWaiverRecorded](../../src/Compliance.Common/Features/AccessControl/SeparationOfDutiesWaiverRecorded.cs#L8) — `bdgrz.sod-waiver.recorded` v1
- [ServiceEngagementAcceptanceRecorded](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementAcceptanceRecorded.cs#L6) — `bdgrz.service-engagement.acceptance-recorded` v1
- [ServiceEngagementAssignmentRevoked](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementAssignmentRevoked.cs#L6) — `bdgrz.service-engagement.assignment-revoked` v1
- [ServiceEngagementMutationRecorded](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementMutationRecorded.cs#L6) — `bdgrz.service-engagement.changed` v1
- [ServiceIndependenceReevaluated](../../src/Compliance.Common/Features/AccessControl/ServiceIndependenceReevaluated.cs#L6) — `bdgrz.independence.service-reevaluated` v1
- [TeamDefined](../../src/Compliance.Common/Features/AccessControl/TeamDefined.cs#L6) — `bdgrz.rbac.team.defined` v1
- [TeamDeleted](../../src/Compliance.Common/Features/AccessControl/TeamDeleted.cs#L6) — `bdgrz.rbac.team.deleted` v1
- [TeamMemberAssigned](../../src/Compliance.Common/Features/AccessControl/TeamMemberAssigned.cs#L6) — `bdgrz.rbac.team-member.assigned` v1
- [TeamMemberRemoved](../../src/Compliance.Common/Features/AccessControl/TeamMemberRemoved.cs#L6) — `bdgrz.rbac.team-member.removed` v1
- [TeamRoleAssigned](../../src/Compliance.Common/Features/AccessControl/TeamRoleAssigned.cs#L6) — `bdgrz.rbac.team-role.assigned` v1
- [TeamRoleRemoved](../../src/Compliance.Common/Features/AccessControl/TeamRoleRemoved.cs#L6) — `bdgrz.rbac.team-role.removed` v1
- [TenantManagerRestored](../../src/Compliance.Common/Features/AccessControl/TenantManagerRestored.cs#L7) — `bdgrz.rbac.manager_guard.restored` v1
- [TenantManagerWithdrawn](../../src/Compliance.Common/Features/AccessControl/TenantManagerWithdrawn.cs#L10) — `bdgrz.rbac.manager_guard.withdrawn` v1

#### AccessReviews

- [AccessDecisionsRecorded](../../src/Compliance.Common/Features/AccessReviews/AccessDecisionsRecorded.cs#L8) — `bdgrz.access_review.decisions.recorded` v1
- [AccessExpectationApproved](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationApproved.cs#L8) — `bdgrz.access_expectation.approved` v1
- [AccessExpectationExceptionRecorded](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationExceptionRecorded.cs#L8) — `bdgrz.access_expectation.exception_recorded` v1
- [AccessExpectationProposed](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationProposed.cs#L8) — `bdgrz.access_expectation.proposed` v1
- [AccessPopulationAccepted](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationAccepted.cs#L8) — `bdgrz.access_population.accepted` v1
- [AccessPopulationExceptionRecorded](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationExceptionRecorded.cs#L8) — `bdgrz.access_population.exception_recorded` v1
- [AccessPopulationFactsRecorded](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationFactsRecorded.cs#L8) — `bdgrz.access_population.facts_recorded` v1
- [AccessPopulationOpened](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationOpened.cs#L8) — `bdgrz.access_population.opened` v1
- [AccessPrincipalClassified](../../src/Compliance.Common/Features/AccessReviews/AccessPrincipalClassified.cs#L8) — `bdgrz.access_population.principal_classified` v1
- [AccessRemediationChangeRecorded](../../src/Compliance.Common/Features/AccessReviews/AccessRemediationChangeRecorded.cs#L8) — `bdgrz.access_review.remediation.change_recorded` v1
- [AccessRemediationExceptionRecorded](../../src/Compliance.Common/Features/AccessReviews/AccessRemediationExceptionRecorded.cs#L8) — `bdgrz.access_review.remediation.exception_recorded` v1
- [AccessRemediationVerified](../../src/Compliance.Common/Features/AccessReviews/AccessRemediationVerified.cs#L8) — `bdgrz.access_review.remediation.verified` v1
- [AccessReviewCampaignCompleted](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignCompleted.cs#L8) — `bdgrz.access_review.campaign.completed` v1
- [AccessReviewCampaignLaunched](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignLaunched.cs#L8) — `bdgrz.access_review.campaign.launched` v1

#### Applications

- [AccessReviewScopeDecided](../../src/Compliance.Common/Features/Applications/AccessReviewScopeDecided.cs#L9) — `bdgrz.system_instance.access_review_scope.decided` v1
- [ApplicationDeclared](../../src/Compliance.Common/Features/Applications/ApplicationDeclared.cs#L8) — `bdgrz.application.declared` v1
- [ApplicationImportCanceled](../../src/Compliance.Common/Features/Applications/ApplicationImportCanceled.cs#L6) — `bdgrz.application_import.canceled` v1
- [ApplicationImportCommitted](../../src/Compliance.Common/Features/Applications/ApplicationImportCommitted.cs#L6) — `bdgrz.application_import.committed` v1
- [ApplicationImportEffectPending](../../src/Compliance.Common/Features/Applications/ApplicationImportEffectPending.cs#L8) — `bdgrz.application_import.effect_pending` v1
- [ApplicationImportFailed](../../src/Compliance.Common/Features/Applications/ApplicationImportFailed.cs#L6) — `bdgrz.application_import.failed` v1
- [ApplicationImportPlanRowFrozen](../../src/Compliance.Common/Features/Applications/ApplicationImportPlanRowFrozen.cs#L6) — `bdgrz.application_import.plan_row_frozen` v1
- [ApplicationImportPlanSealed](../../src/Compliance.Common/Features/Applications/ApplicationImportPlanSealed.cs#L6) — `bdgrz.application_import.plan_sealed` v1
- [ApplicationImportPlanStarted](../../src/Compliance.Common/Features/Applications/ApplicationImportPlanStarted.cs#L6) — `bdgrz.application_import.plan_started` v1
- [ApplicationImportRetirementProposalSealed](../../src/Compliance.Common/Features/Applications/ApplicationImportRetirementProposalSealed.cs#L6) — `bdgrz.application_import.retirement_proposal_sealed` v1
- [ApplicationImportRetirementProposalStarted](../../src/Compliance.Common/Features/Applications/ApplicationImportRetirementProposalStarted.cs#L6) — `bdgrz.application_import.retirement_proposal_started` v1
- [ApplicationImportRetirementRowFrozen](../../src/Compliance.Common/Features/Applications/ApplicationImportRetirementRowFrozen.cs#L6) — `bdgrz.application_import.retirement_row_frozen` v1
- [ApplicationImportRowCorrelated](../../src/Compliance.Common/Features/Applications/ApplicationImportRowCorrelated.cs#L6) — `bdgrz.application_import.row_correlated` v1
- [ApplicationImportStaged](../../src/Compliance.Common/Features/Applications/ApplicationImportStaged.cs#L6) — `bdgrz.application_import.staged` v1
- [ApplicationRelationshipAdded](../../src/Compliance.Common/Features/Applications/ApplicationRelationshipAdded.cs#L8) — `bdgrz.application.relationship.added` v1
- [ApplicationRelationshipRemoved](../../src/Compliance.Common/Features/Applications/ApplicationRelationshipRemoved.cs#L8) — `bdgrz.application.relationship.removed` v1
- [ApplicationRetired](../../src/Compliance.Common/Features/Applications/ApplicationRetired.cs#L9) — `bdgrz.application.retired` v1
- [ApplicationRevised](../../src/Compliance.Common/Features/Applications/ApplicationRevised.cs#L8) — `bdgrz.application.revised` v1
- [SystemInstanceDeclared](../../src/Compliance.Common/Features/Applications/SystemInstanceDeclared.cs#L8) — `bdgrz.system_instance.declared` v1
- [SystemInstanceRegistered](../../src/Compliance.Common/Features/Applications/SystemInstanceRegistered.cs#L8) — `bdgrz.system_instance.registered` v1
- [SystemInstanceRetired](../../src/Compliance.Common/Features/Applications/SystemInstanceRetired.cs#L9) — `bdgrz.system_instance.retired` v1

#### Boundaries

- [BoundaryApproved](../../src/Compliance.Common/Features/Boundaries/BoundaryApproved.cs#L8) — `bdgrz.boundary.approved` v1
- [BoundaryDraftCreated](../../src/Compliance.Common/Features/Boundaries/BoundaryDraftCreated.cs#L6) — `bdgrz.boundary.draft.created` v1
- [BoundaryDraftDiscarded](../../src/Compliance.Common/Features/Boundaries/BoundaryDraftDiscarded.cs#L8) — `bdgrz.boundary.draft.discarded` v1
- [BoundaryDraftRevised](../../src/Compliance.Common/Features/Boundaries/BoundaryDraftRevised.cs#L6) — `bdgrz.boundary.draft.revised` v1
- [BoundaryReviewed](../../src/Compliance.Common/Features/Boundaries/BoundaryReviewed.cs#L8) — `bdgrz.boundary.reviewed` v1
- [BoundarySuccessorProposed](../../src/Compliance.Common/Features/Boundaries/BoundarySuccessorProposed.cs#L6) — `bdgrz.boundary.successor.proposed` v1

#### Commitments

- [CommitmentApproved](../../src/Compliance.Common/Features/Commitments/CommitmentApproved.cs#L9) — `bdgrz.commitment.approved` v1
- [CommitmentDraftCreated](../../src/Compliance.Common/Features/Commitments/CommitmentDraftCreated.cs#L8) — `bdgrz.commitment.draft.created` v1
- [CommitmentDraftRevised](../../src/Compliance.Common/Features/Commitments/CommitmentDraftRevised.cs#L8) — `bdgrz.commitment.draft.revised` v1
- [CommitmentReviewed](../../src/Compliance.Common/Features/Commitments/CommitmentReviewed.cs#L12) — `bdgrz.commitment.reviewed` v1

#### ControlMappings

- [ControlCriterionMappingProposed](../../src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingProposed.cs#L8) — `bdgrz.control_mapping.proposed` v1
- [ControlCriterionMappingRetired](../../src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingRetired.cs#L8) — `bdgrz.control_mapping.retired` v1
- [ControlCriterionMappingReviewed](../../src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingReviewed.cs#L8) — `bdgrz.control_mapping.reviewed` v1
- [CriterionApplicabilityReviewed](../../src/Compliance.Common/Features/ControlMappings/CriterionApplicabilityReviewed.cs#L7) — `bdgrz.criterion_applicability.reviewed` v1
- [CriterionNotApplicableProposed](../../src/Compliance.Common/Features/ControlMappings/CriterionNotApplicableProposed.cs#L7) — `bdgrz.criterion_applicability.not_applicable_proposed` v1
- [CriterionNotApplicableWithdrawn](../../src/Compliance.Common/Features/ControlMappings/CriterionNotApplicableWithdrawn.cs#L7) — `bdgrz.criterion_applicability.not_applicable_withdrawn` v1

#### Controls

- [ControlApproved](../../src/Compliance.Common/Features/Controls/ControlApproved.cs#L9) — `bdgrz.control.approved` v1
- [ControlDraftCreated](../../src/Compliance.Common/Features/Controls/ControlDraftCreated.cs#L8) — `bdgrz.control.draft.created` v1
- [ControlDraftDiscarded](../../src/Compliance.Common/Features/Controls/ControlDraftDiscarded.cs#L8) — `bdgrz.control.draft.discarded` v1
- [ControlDraftRevised](../../src/Compliance.Common/Features/Controls/ControlDraftRevised.cs#L8) — `bdgrz.control.draft.revised` v1
- [ControlOwnerPersonDesignated](../../src/Compliance.Common/Features/Controls/ControlOwnerPersonDesignated.cs#L13) — `bdgrz.control.owner_person.designated` v1
- [ControlProposalWithdrawn](../../src/Compliance.Common/Features/Controls/ControlProposalWithdrawn.cs#L15) — `bdgrz.control.proposal.withdrawn` v1
- [ControlRetired](../../src/Compliance.Common/Features/Controls/ControlRetired.cs#L12) — `bdgrz.control.retired` v1
- [ControlRetirementProposed](../../src/Compliance.Common/Features/Controls/ControlRetirementProposed.cs#L9) — `bdgrz.control.retirement.proposed` v1
- [ControlReviewed](../../src/Compliance.Common/Features/Controls/ControlReviewed.cs#L8) — `bdgrz.control.reviewed` v1
- [ControlSuccessorProposed](../../src/Compliance.Common/Features/Controls/ControlSuccessorProposed.cs#L12) — `bdgrz.control.successor.proposed` v1

#### Criteria

- [CriteriaTextOverlayEntryRevised](../../src/Compliance.Common/Features/Criteria/CriteriaTextOverlayEntryRevised.cs#L7) — `bdgrz.criteria-overlay.entry-revised` v1

#### Evaluations

- [ControlEvaluationDeviationDisposed](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationDeviationDisposed.cs#L6) — `bdgrz.control.evaluation.deviation_disposed` v1
- [ControlEvaluationPlanVersionDefined](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationPlanVersionDefined.cs#L7) — `bdgrz.control.evaluation.plan.version-defined` v1
- [ControlEvaluationReviewed](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationReviewed.cs#L6) — `bdgrz.control.evaluation.reviewed` v1
- [ControlEvaluationStarted](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationStarted.cs#L7) — `bdgrz.control.evaluation.started` v1
- [ControlEvaluationStepRecorded](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationStepRecorded.cs#L6) — `bdgrz.control.evaluation.step_recorded` v1
- [ControlEvaluationSubmitted](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationSubmitted.cs#L6) — `bdgrz.control.evaluation.submitted` v1

#### Evidence

- [EvidenceArtifactInspected](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactInspected.cs#L7) — `bdgrz.evidence.artifact.inspected` v1
- [EvidenceArtifactQuarantineReleased](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactQuarantineReleased.cs#L8) — `bdgrz.evidence.artifact.quarantine_released` v1
- [EvidenceArtifactRegistered](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactRegistered.cs#L8) — `bdgrz.evidence.artifact.registered` v1
- [EvidenceRedactionApproved](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionApproved.cs#L6) — `bdgrz.evidence.redaction.approved` v1
- [EvidenceRedactionPrepared](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionPrepared.cs#L6) — `bdgrz.evidence.redaction.prepared` v1
- [EvidenceRequestCancelled](../../src/Compliance.Common/Features/Evidence/EvidenceRequestCancelled.cs#L8) — `bdgrz.evidence.request.cancelled` v1
- [EvidenceRequestFulfilled](../../src/Compliance.Common/Features/Evidence/EvidenceRequestFulfilled.cs#L8) — `bdgrz.evidence.request.fulfilled` v1
- [EvidenceRequestOpened](../../src/Compliance.Common/Features/Evidence/EvidenceRequestOpened.cs#L8) — `bdgrz.evidence.request.opened` v1

#### Operations

- [ControlOccurrenceAttested](../../src/Compliance.Common/Features/Operations/ControlOccurrenceAttested.cs#L6) — `bdgrz.control.occurrence.attested` v1
- [ControlOccurrenceOpened](../../src/Compliance.Common/Features/Operations/ControlOccurrenceOpened.cs#L7) — `bdgrz.control.occurrence.opened` v1
- [ControlOccurrenceReviewed](../../src/Compliance.Common/Features/Operations/ControlOccurrenceReviewed.cs#L6) — `bdgrz.control.occurrence.reviewed` v1
- [ControlOperatingPlanApproved](../../src/Compliance.Common/Features/Operations/ControlOperatingPlanApproved.cs#L7) — `bdgrz.control.operating_plan.approved` v1
- [ControlOperatingPlanProposed](../../src/Compliance.Common/Features/Operations/ControlOperatingPlanProposed.cs#L6) — `bdgrz.control.operating_plan.proposed` v1

#### Policies

- [PolicyApproved](../../src/Compliance.Common/Features/Policies/PolicyApproved.cs#L11) — `bdgrz.policy.approved` v1
- [PolicyDraftCreated](../../src/Compliance.Common/Features/Policies/PolicyDraftCreated.cs#L7) — `bdgrz.policy.draft.created` v1
- [PolicyDraftDiscarded](../../src/Compliance.Common/Features/Policies/PolicyDraftDiscarded.cs#L7) — `bdgrz.policy.draft.discarded` v1
- [PolicyDraftRevised](../../src/Compliance.Common/Features/Policies/PolicyDraftRevised.cs#L11) — `bdgrz.policy.draft.revised` v1
- [PolicyPeriodicReviewConfirmed](../../src/Compliance.Common/Features/Policies/PolicyPeriodicReviewConfirmed.cs#L8) — `bdgrz.policy.periodic_review.confirmed` v1
- [PolicyRetired](../../src/Compliance.Common/Features/Policies/PolicyRetired.cs#L7) — `bdgrz.policy.retired` v1
- [PolicyRetirementProposed](../../src/Compliance.Common/Features/Policies/PolicyRetirementProposed.cs#L7) — `bdgrz.policy.retirement.proposed` v1
- [PolicyReviewed](../../src/Compliance.Common/Features/Policies/PolicyReviewed.cs#L7) — `bdgrz.policy.reviewed` v1

#### PolicyDistribution

- [CampaignWaiverApproved](../../src/Compliance.Common/Features/PolicyDistribution/CampaignWaiverApproved.cs#L8) — `bdgrz.policy_campaign.waiver.approved` v1
- [PolicyAcknowledgementRecorded](../../src/Compliance.Common/Features/PolicyDistribution/PolicyAcknowledgementRecorded.cs#L12) — `bdgrz.policy.acknowledgement.recorded` v1
- [PolicyCampaignAudienceAmended](../../src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignAudienceAmended.cs#L11) — `bdgrz.policy_campaign.audience.amended` v1
- [PolicyCampaignAudienceFrozen](../../src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignAudienceFrozen.cs#L6) — `bdgrz.policy_campaign.audience.frozen` v1
- [PolicyCampaignClosed](../../src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignClosed.cs#L7) — `bdgrz.policy_campaign.closed` v1
- [PolicyCampaignLaunched](../../src/Compliance.Common/Features/PolicyDistribution/PolicyCampaignLaunched.cs#L11) — `bdgrz.policy_campaign.launched` v1
- [TrainingCompletionRecorded](../../src/Compliance.Common/Features/PolicyDistribution/TrainingCompletionRecorded.cs#L11) — `bdgrz.training_completion.recorded` v1
- [TrainingRequirementDefined](../../src/Compliance.Common/Features/PolicyDistribution/TrainingRequirementDefined.cs#L8) — `bdgrz.training_requirement.defined` v1

#### Programs

- [ClientServiceCreated](../../src/Compliance.Common/Features/Programs/ClientServiceCreated.cs#L8) — `bdgrz.client-service.created` v1
- [ClientServiceRetired](../../src/Compliance.Common/Features/Programs/ClientServiceRetired.cs#L8) — `bdgrz.client-service.retired` v1
- [ClientServiceRevised](../../src/Compliance.Common/Features/Programs/ClientServiceRevised.cs#L8) — `bdgrz.client-service.revised` v1
- [ProgramCreated](../../src/Compliance.Common/Features/Programs/ProgramCreated.cs#L8) — `bdgrz.program.created` v1
- [ProgramCriteriaEditionSelected](../../src/Compliance.Common/Features/Programs/ProgramCriteriaEditionSelected.cs#L8) — `bdgrz.program.criteria.selected` v1
- [ProgramRevised](../../src/Compliance.Common/Features/Programs/ProgramRevised.cs#L8) — `bdgrz.program.revised` v1

#### Providers

- [AssuranceReportRecorded](../../src/Compliance.Common/Features/Providers/AssuranceReportRecorded.cs#L7) — `bdgrz.provider.assurance_report.recorded` v1
- [AssuranceReportRevised](../../src/Compliance.Common/Features/Providers/AssuranceReportRevised.cs#L7) — `bdgrz.provider.assurance_report.revised` v1
- [ProviderCoverageGapClosed](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapClosed.cs#L7) — `bdgrz.provider.coverage_gap.closed` v1
- [ProviderCoverageGapRecorded](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapRecorded.cs#L7) — `bdgrz.provider.coverage_gap.recorded` v1
- [ProviderCoverageGapRiskAcceptanceLinked](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapRiskAcceptanceLinked.cs#L6) — `bdgrz.provider.coverage_gap.risk_acceptance_linked` v1
- [ProviderRecorded](../../src/Compliance.Common/Features/Providers/ProviderRecorded.cs#L7) — `bdgrz.provider.recorded` v1
- [ProviderReviewRecorded](../../src/Compliance.Common/Features/Providers/ProviderReviewRecorded.cs#L7) — `bdgrz.provider.review.recorded` v1
- [ProviderRevised](../../src/Compliance.Common/Features/Providers/ProviderRevised.cs#L7) — `bdgrz.provider.revised` v1

#### Readiness

- [ReadinessAssessmentRecorded](../../src/Compliance.Common/Features/Readiness/ReadinessAssessmentRecorded.cs#L7) — `bdgrz.readiness.assessment.recorded` v1
- [ReadinessDecisionRecorded](../../src/Compliance.Common/Features/Readiness/ReadinessDecisionRecorded.cs#L6) — `bdgrz.readiness.decision.recorded` v1
- [ReadinessGapAnnotated](../../src/Compliance.Common/Features/Readiness/ReadinessGapAnnotated.cs#L6) — `bdgrz.readiness.gap.annotated` v1
- [ReadinessGapPlanned](../../src/Compliance.Common/Features/Readiness/ReadinessGapPlanned.cs#L6) — `bdgrz.readiness.gap.planned` v1
- [TypeIEntryDecisionRecorded](../../src/Compliance.Common/Features/Readiness/TypeIEntryDecisionRecorded.cs#L6) — `bdgrz.readiness.type_i_entry.decided` v1

#### Remediation

- [CorrectiveActionAdded](../../src/Compliance.Common/Features/Remediation/CorrectiveActionAdded.cs#L6) — `bdgrz.finding.corrective_action.added` v1
- [CorrectiveActionCompleted](../../src/Compliance.Common/Features/Remediation/CorrectiveActionCompleted.cs#L8) — `bdgrz.finding.corrective_action.completed` v1
- [FindingAcceptanceLinked](../../src/Compliance.Common/Features/Remediation/FindingAcceptanceLinked.cs#L6) — `bdgrz.finding.acceptance.linked` v1
- [FindingClosed](../../src/Compliance.Common/Features/Remediation/FindingClosed.cs#L6) — `bdgrz.finding.closed` v1
- [FindingRaised](../../src/Compliance.Common/Features/Remediation/FindingRaised.cs#L7) — `bdgrz.finding.raised` v1
- [FindingReopened](../../src/Compliance.Common/Features/Remediation/FindingReopened.cs#L7) — `bdgrz.finding.reopened` v1
- [FindingRevised](../../src/Compliance.Common/Features/Remediation/FindingRevised.cs#L7) — `bdgrz.finding.revised` v1

#### Responsibilities

- [ResponsibilityAssigned](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityAssigned.cs#L8) — `bdgrz.responsibility.assigned` v1
- [ResponsibilityRevoked](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityRevoked.cs#L8) — `bdgrz.responsibility.revoked` v1

#### Retention

- [ArtifactLegalHoldPlaced](../../src/Compliance.Common/Features/Retention/ArtifactLegalHoldPlaced.cs#L6) — `bdgrz.artifact_retention.legal_hold_placed` v1
- [ArtifactLegalHoldReleased](../../src/Compliance.Common/Features/Retention/ArtifactLegalHoldReleased.cs#L6) — `bdgrz.artifact_retention.legal_hold_released` v1
- [ArtifactRetentionBasisRecorded](../../src/Compliance.Common/Features/Retention/ArtifactRetentionBasisRecorded.cs#L6) — `bdgrz.artifact_retention.basis_recorded` v1
- [ArtifactRetentionBound](../../src/Compliance.Common/Features/Retention/ArtifactRetentionBound.cs#L6) — `bdgrz.artifact_retention.bound` v1

#### Risks

- [RiskAccepted](../../src/Compliance.Common/Features/Risks/RiskAccepted.cs#L6) — `bdgrz.risk.accepted` v1
- [RiskAssessmentRecorded](../../src/Compliance.Common/Features/Risks/RiskAssessmentRecorded.cs#L6) — `bdgrz.risk.assessment.recorded` v1
- [RiskControlTreatmentProposed](../../src/Compliance.Common/Features/Risks/RiskControlTreatmentProposed.cs#L6) — `bdgrz.risk.control_treatment.proposed` v1
- [RiskControlTreatmentRetired](../../src/Compliance.Common/Features/Risks/RiskControlTreatmentRetired.cs#L7) — `bdgrz.risk.control_treatment.retired` v1
- [RiskControlTreatmentReviewed](../../src/Compliance.Common/Features/Risks/RiskControlTreatmentReviewed.cs#L7) — `bdgrz.risk.control_treatment.reviewed` v1
- [RiskDraftCreated](../../src/Compliance.Common/Features/Risks/RiskDraftCreated.cs#L8) — `bdgrz.risk.draft.created` v1
- [RiskDraftRevised](../../src/Compliance.Common/Features/Risks/RiskDraftRevised.cs#L8) — `bdgrz.risk.draft.revised` v1
- [RiskMethodVersionPublished](../../src/Compliance.Common/Features/Risks/RiskMethodVersionPublished.cs#L6) — `bdgrz.risk.method.version_published` v1
- [RiskOwnerAssigned](../../src/Compliance.Common/Features/Risks/RiskOwnerAssigned.cs#L6) — `bdgrz.risk.owner.assigned` v1
- [RiskReassessmentTriggered](../../src/Compliance.Common/Features/Risks/RiskReassessmentTriggered.cs#L6) — `bdgrz.risk.reassessment.triggered` v1
- [RiskTreatmentActionAdded](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionAdded.cs#L6) — `bdgrz.risk.treatment_action.added` v1
- [RiskTreatmentActionCancelled](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionCancelled.cs#L7) — `bdgrz.risk.treatment_action.cancelled` v1
- [RiskTreatmentActionCompletionReviewed](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionCompletionReviewed.cs#L7) — `bdgrz.risk.treatment_action.completion_reviewed` v1
- [RiskTreatmentActionCompletionSubmitted](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionCompletionSubmitted.cs#L7) — `bdgrz.risk.treatment_action.completion_submitted` v1
- [RiskTreatmentActionRevised](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionRevised.cs#L7) — `bdgrz.risk.treatment_action.revised` v1
- [RiskTreatmentChosen](../../src/Compliance.Common/Features/Risks/RiskTreatmentChosen.cs#L6) — `bdgrz.risk.treatment.chosen` v1

#### Snapshots

- [PopulationSnapshotChunkStored](../../src/Compliance.Common/Features/Snapshots/PopulationSnapshotChunkStored.cs#L11) — `bdgrz.snapshot.population.chunk_stored` v1
- [PopulationSnapshotFrozen](../../src/Compliance.Common/Features/Snapshots/PopulationSnapshotFrozen.cs#L14) — `bdgrz.snapshot.population.frozen` v1
- [SnapshotFrozen](../../src/Compliance.Common/Features/Snapshots/SnapshotFrozen.cs#L6) — `bdgrz.snapshot.frozen` v1

#### TechnologyInventory

- [DataFlowRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/DataFlowRevisionRecorded.cs#L8) — `bdgrz.inventory.data_flow.revision_recorded` v1
- [InformationAssetRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/InformationAssetRevisionRecorded.cs#L8) — `bdgrz.inventory.information_asset.revision_recorded` v1
- [LocationRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/LocationRevisionRecorded.cs#L8) — `bdgrz.inventory.location.revision_recorded` v1
- [OperationalProcessRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/OperationalProcessRevisionRecorded.cs#L8) — `bdgrz.inventory.operational_process.revision_recorded` v1
- [TechnologyComponentRevisionRecorded](../../src/Compliance.Common/Features/TechnologyInventory/TechnologyComponentRevisionRecorded.cs#L8) — `bdgrz.inventory.component.revision_recorded` v1

#### Tenants

- [PlatformOperatorGranted](../../src/Compliance.Common/Features/Tenants/PlatformOperatorGranted.cs#L6) — `bdgrz.platform.operator.granted` v1
- [PlatformOperatorRevoked](../../src/Compliance.Common/Features/Tenants/PlatformOperatorRevoked.cs#L6) — `bdgrz.platform.operator.revoked` v1
- [PlatformOperatorRosterSeeded](../../src/Compliance.Common/Features/Tenants/PlatformOperatorRosterSeeded.cs#L6) — `bdgrz.platform.operator-roster.seeded` v1
- [TenantActivated](../../src/Compliance.Common/Features/Tenants/TenantActivated.cs#L6) — `bdgrz.tenant.activated` v1
- [TenantInvitationAccepted](../../src/Compliance.Common/Features/Tenants/TenantInvitationAccepted.cs#L6) — `bdgrz.tenant-invitation.accepted` v1
- [TenantInvitationDeliveryFailed](../../src/Compliance.Common/Features/Tenants/TenantInvitationDeliveryFailed.cs#L6) — `bdgrz.tenant-invitation.delivery.failed` v1
- [TenantInvitationDeliverySent](../../src/Compliance.Common/Features/Tenants/TenantInvitationDeliverySent.cs#L6) — `bdgrz.tenant-invitation.delivery.sent` v1
- [TenantMemberInvited](../../src/Compliance.Common/Features/Tenants/TenantMemberInvited.cs#L6) — `bdgrz.tenant-member.invited` v1
- [TenantOffboardingStarted](../../src/Compliance.Common/Features/Tenants/TenantOffboardingStarted.cs#L6) — `bdgrz.tenant.offboarding.started` v1
- [TenantOwnerRegistered](../../src/Compliance.Common/Features/Tenants/TenantOwnerRegistered.cs#L6) — `bdgrz.tenant-owner.registered` v1
- [TenantReactivated](../../src/Compliance.Common/Features/Tenants/TenantReactivated.cs#L6) — `bdgrz.tenant.reactivated` v1
- [TenantRegistered](../../src/Compliance.Common/Features/Tenants/TenantRegistered.cs#L6) — `bdgrz.tenant.registered` v1
- [TenantSlugChangeRejected](../../src/Compliance.Common/Features/Tenants/TenantSlugChangeRejected.cs#L6) — `bdgrz.tenant-slug.change-rejected` v1
- [TenantSlugChangeRequested](../../src/Compliance.Common/Features/Tenants/TenantSlugChangeRequested.cs#L7) — `bdgrz.tenant-slug.change-requested` v1
- [TenantSlugChanged](../../src/Compliance.Common/Features/Tenants/TenantSlugChanged.cs#L7) — `bdgrz.tenant-slug.changed` v1
- [TenantSlugConfirmed](../../src/Compliance.Common/Features/Tenants/TenantSlugConfirmed.cs#L6) — `bdgrz.tenant-slug.confirmed` v1
- [TenantSlugRegistered](../../src/Compliance.Common/Features/Tenants/TenantSlugRegistered.cs#L6) — `bdgrz.tenant-slug.registered` v1
- [TenantSlugRegistrationRejected](../../src/Compliance.Common/Features/Tenants/TenantSlugRegistrationRejected.cs#L6) — `bdgrz.tenant-slug.registration-rejected` v1
- [TenantSlugRejected](../../src/Compliance.Common/Features/Tenants/TenantSlugRejected.cs#L6) — `bdgrz.tenant-slug.rejected` v1
- [TenantSlugSurrenderConfirmed](../../src/Compliance.Common/Features/Tenants/TenantSlugSurrenderConfirmed.cs#L6) — `bdgrz.tenant-slug-surrender.confirmed` v1
- [TenantSlugSurrenderFailed](../../src/Compliance.Common/Features/Tenants/TenantSlugSurrenderFailed.cs#L6) — `bdgrz.tenant-slug-surrender.failed` v1
- [TenantSlugSurrenderRejected](../../src/Compliance.Common/Features/Tenants/TenantSlugSurrenderRejected.cs#L6) — `bdgrz.tenant-slug.surrender-rejected` v1
- [TenantSlugSurrenderRequested](../../src/Compliance.Common/Features/Tenants/TenantSlugSurrenderRequested.cs#L6) — `bdgrz.tenant-slug.surrender-requested` v1
- [TenantSlugSurrendered](../../src/Compliance.Common/Features/Tenants/TenantSlugSurrendered.cs#L6) — `bdgrz.tenant-slug.surrendered` v1
- [TenantSuspended](../../src/Compliance.Common/Features/Tenants/TenantSuspended.cs#L6) — `bdgrz.tenant.suspended` v1

#### UserIdentities

- [EmailAddressReserved](../../src/Compliance.Common/Features/UserIdentities/EmailAddressReserved.cs#L6) — `bdgrz.email-address.reserved` v1
- [EmailAddressVerified](../../src/Compliance.Common/Features/UserIdentities/EmailAddressVerified.cs#L6) — `bdgrz.email-address.verified` v1
- [EmailChallengeDeliveryFailed](../../src/Compliance.Common/Features/UserIdentities/EmailChallengeDeliveryFailed.cs#L6) — `bdgrz.email-address.challenge-delivery-failed` v1
- [EmailChallengeDeliverySent](../../src/Compliance.Common/Features/UserIdentities/EmailChallengeDeliverySent.cs#L6) — `bdgrz.email-address.challenge-delivery-sent` v1
- [EmailChallengeIssued](../../src/Compliance.Common/Features/UserIdentities/EmailChallengeIssued.cs#L6) — `bdgrz.email-address.challenge-issued` v1
- [IdentityRecoveryChallengeClaimed](../../src/Compliance.Common/Features/UserIdentities/IdentityRecoveryChallengeClaimed.cs#L7) — `bdgrz.identity-recovery.challenge-claimed` v1
- [IdentityRecoveryChallengeCompleted](../../src/Compliance.Common/Features/UserIdentities/IdentityRecoveryChallengeCompleted.cs#L7) — `bdgrz.identity-recovery.challenge-completed` v1
- [IdentityRecoveryChallengeIssued](../../src/Compliance.Common/Features/UserIdentities/IdentityRecoveryChallengeIssued.cs#L7) — `bdgrz.identity-recovery.challenge-issued` v1
- [UserIdentityAuthenticated](../../src/Compliance.Common/Features/UserIdentities/UserIdentityAuthenticated.cs#L7) — `bdgrz.user-identity.authenticated` v1
- [UserIdentityProfileObserved](../../src/Compliance.Common/Features/UserIdentities/UserIdentityProfileObserved.cs#L7) — `bdgrz.user-identity.profile-observed` v1
- [UserIdentityRegistered](../../src/Compliance.Common/Features/UserIdentities/UserIdentityRegistered.cs#L7) — `bdgrz.user-identity.registered` v1
- [UserIdentityRevoked](../../src/Compliance.Common/Features/UserIdentities/UserIdentityRevoked.cs#L7) — `bdgrz.user-identity.revoked` v1

#### Work

- [WorkDigestPreferenceChanged](../../src/Compliance.Common/Features/Work/WorkDigestPreferenceChanged.cs#L7) — `bdgrz.work.digest_preference.changed` v1
- [WorkItemAssigned](../../src/Compliance.Common/Features/Work/WorkItemAssigned.cs#L8) — `bdgrz.work.assigned` v1
- [WorkItemEscalated](../../src/Compliance.Common/Features/Work/WorkItemEscalated.cs#L8) — `bdgrz.work.escalated` v1

#### Workforce

- [PersonMembershipCorrelated](../../src/Compliance.Common/Features/Workforce/PersonMembershipCorrelated.cs#L11) — `bdgrz.workforce.person.membership-correlated` v1
- [PersonRecorded](../../src/Compliance.Common/Features/Workforce/PersonRecorded.cs#L7) — `bdgrz.workforce.person.recorded` v1
- [PersonRevised](../../src/Compliance.Common/Features/Workforce/PersonRevised.cs#L7) — `bdgrz.workforce.person.revised` v1
- [ServiceIdentityRecorded](../../src/Compliance.Common/Features/Workforce/ServiceIdentityRecorded.cs#L7) — `bdgrz.workforce.service-identity.recorded` v1
- [ServiceIdentityRevised](../../src/Compliance.Common/Features/Workforce/ServiceIdentityRevised.cs#L7) — `bdgrz.workforce.service-identity.revised` v1
- [WorkRelationshipRecorded](../../src/Compliance.Common/Features/Workforce/WorkRelationshipRecorded.cs#L7) — `bdgrz.workforce.work-relationship.recorded` v1
- [WorkRelationshipRevised](../../src/Compliance.Common/Features/Workforce/WorkRelationshipRevised.cs#L7) — `bdgrz.workforce.work-relationship.revised` v1
- [WorkforceObservationResolved](../../src/Compliance.Common/Features/Workforce/WorkforceObservationResolved.cs#L7) — `bdgrz.workforce.observation.resolved` v1
- [WorkforceSourceObserved](../../src/Compliance.Common/Features/Workforce/WorkforceSourceObserved.cs#L7) — `bdgrz.workforce.source.observed` v1
- [WorkforceSourceReconciled](../../src/Compliance.Common/Features/Workforce/WorkforceSourceReconciled.cs#L6) — `bdgrz.workforce.source.reconciled` v1

### Existing aggregates

#### AccessControl

- [AccessGrant](../../src/Compliance.Core/Features/AccessControl/AccessGrant.cs#L5)
- [FirmStaffDirectory](../../src/Compliance.Core/Features/AccessControl/FirmStaffDirectory.cs#L8)
- [IndependenceLedger](../../src/Compliance.Core/Features/AccessControl/IndependenceLedger.cs#L7)
- [IndependenceRuleCatalog](../../src/Compliance.Core/Features/AccessControl/IndependenceRuleCatalog.cs#L8)
- [Member](../../src/Compliance.Core/Features/AccessControl/Member.cs#L5)
- [Role](../../src/Compliance.Core/Features/AccessControl/Role.cs#L5)
- [RolePermission](../../src/Compliance.Core/Features/AccessControl/RolePermission.cs#L8)
- [SeparationOfDutiesWaiver](../../src/Compliance.Core/Features/AccessControl/SeparationOfDutiesWaiver.cs#L7)
- [Team](../../src/Compliance.Core/Features/AccessControl/Team.cs#L5)
- [TeamMember](../../src/Compliance.Core/Features/AccessControl/TeamMember.cs#L5)
- [TeamRole](../../src/Compliance.Core/Features/AccessControl/TeamRole.cs#L5)
- [TenantManagerGuard](../../src/Compliance.Core/Features/AccessControl/TenantManagerGuard.cs#L11)

#### AccessReviews

- [AccessPopulation](../../src/Compliance.Core/Features/AccessReviews/AccessPopulation.cs#L11)
- [AccessReviewCampaign](../../src/Compliance.Core/Features/AccessReviews/AccessReviewCampaign.cs#L11)
- [AccessReviewSystemLedger](../../src/Compliance.Core/Features/AccessReviews/AccessReviewSystemLedger.cs#L10)

#### Applications

- [ApplicationImportLedger](../../src/Compliance.Core/Features/Applications/ApplicationImportLedger.cs#L8)
- [DeclaredApplication](../../src/Compliance.Core/Features/Applications/DeclaredApplication.cs#L8)
- [DeclaredSystemInstance](../../src/Compliance.Core/Features/Applications/DeclaredSystemInstance.cs#L8)
- [ImportBatch](../../src/Compliance.Core/Features/Applications/ImportBatch.cs#L11)
- [SystemInstanceAccessReviewScope](../../src/Compliance.Core/Features/Applications/SystemInstanceAccessReviewScope.cs#L10)

#### Boundaries

- [SystemBoundary](../../src/Compliance.Core/Features/Boundaries/SystemBoundary.cs#L7)

#### Commitments

- [CommitmentDraft](../../src/Compliance.Core/Features/Commitments/CommitmentDraft.cs#L9)

#### ControlMappings

- [ControlCriterionMappingLedger](../../src/Compliance.Core/Features/ControlMappings/ControlCriterionMappingLedger.cs#L13)
- [CriterionApplicabilityLedger](../../src/Compliance.Core/Features/ControlMappings/CriterionApplicabilityLedger.cs#L13)

#### Controls

- [ControlDraft](../../src/Compliance.Core/Features/Controls/ControlDraft.cs#L13)

#### Criteria

- [CriteriaTextOverlayLedger](../../src/Compliance.Core/Features/Criteria/CriteriaTextOverlayLedger.cs#L7)

#### Evaluations

- [ControlEvaluationLedger](../../src/Compliance.Core/Features/Evaluations/ControlEvaluationLedger.cs#L14)
- [ControlEvaluationPlan](../../src/Compliance.Core/Features/Evaluations/ControlEvaluationPlan.cs#L9)

#### Evidence

- [EvidenceArtifact](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs#L12)
- [EvidenceRedaction](../../src/Compliance.Core/Features/Evidence/EvidenceRedaction.cs#L9)
- [EvidenceRequestLedger](../../src/Compliance.Core/Features/Evidence/EvidenceRequestLedger.cs#L11)

#### Operations

- [ControlOperationsLedger](../../src/Compliance.Core/Features/Operations/ControlOperationsLedger.cs#L13)

#### Policies

- [Policy](../../src/Compliance.Core/Features/Policies/Policy.cs#L13)

#### PolicyDistribution

- [PolicyDistributionCampaign](../../src/Compliance.Core/Features/PolicyDistribution/PolicyDistributionCampaign.cs#L13)
- [TrainingCatalog](../../src/Compliance.Core/Features/PolicyDistribution/TrainingCatalog.cs#L13)

#### Programs

- [ClientService](../../src/Compliance.Core/Features/Programs/ClientService.cs#L7)
- [ComplianceProgram](../../src/Compliance.Core/Features/Programs/ComplianceProgram.cs#L7)

#### Providers

- [ProviderAssuranceRegister](../../src/Compliance.Core/Features/Providers/ProviderAssuranceRegister.cs#L13)
- [ProviderRegister](../../src/Compliance.Core/Features/Providers/ProviderRegister.cs#L9)

#### Readiness

- [ReadinessLedger](../../src/Compliance.Core/Features/Readiness/ReadinessLedger.cs#L12)

#### Remediation

- [RemediationLedger](../../src/Compliance.Core/Features/Remediation/RemediationLedger.cs#L13)

#### Retention

- [ArtifactRetention](../../src/Compliance.Core/Features/Retention/ArtifactRetention.cs#L7)

#### Risks

- [RiskDraft](../../src/Compliance.Core/Features/Risks/RiskDraft.cs#L8)
- [RiskEvaluation](../../src/Compliance.Core/Features/Risks/RiskEvaluation.cs#L11)
- [RiskGovernanceLedger](../../src/Compliance.Core/Features/Risks/RiskGovernanceLedger.cs#L13)
- [RiskMethod](../../src/Compliance.Core/Features/Risks/RiskMethod.cs#L8)

#### Snapshots

- [ImmutableSnapshot](../../src/Compliance.Core/Features/Snapshots/ImmutableSnapshot.cs#L5)
- [PopulationSnapshot](../../src/Compliance.Core/Features/Snapshots/PopulationSnapshot.cs#L14)
- [PopulationSnapshotChunk](../../src/Compliance.Core/Features/Snapshots/PopulationSnapshotChunk.cs#L10)

#### TechnologyInventory

- [DataFlow](../../src/Compliance.Core/Features/TechnologyInventory/DataFlow.cs#L8)
- [InformationAsset](../../src/Compliance.Core/Features/TechnologyInventory/InformationAsset.cs#L8)
- [LocationRegister](../../src/Compliance.Core/Features/TechnologyInventory/LocationRegister.cs#L11)
- [OperationalProcessRegister](../../src/Compliance.Core/Features/TechnologyInventory/OperationalProcessRegister.cs#L11)
- [TechnologyComponent](../../src/Compliance.Core/Features/TechnologyInventory/TechnologyComponent.cs#L8)

#### Tenants

- [PlatformOperatorRoster](../../src/Compliance.Core/Features/Tenants/PlatformOperatorRoster.cs#L9)
- [Tenant](../../src/Compliance.Core/Features/Tenants/Tenant.cs#L6)
- [TenantInvitation](../../src/Compliance.Core/Features/Tenants/TenantInvitation.cs#L9)
- [TenantOwner](../../src/Compliance.Core/Features/Tenants/TenantOwner.cs#L6)
- [TenantSlug](../../src/Compliance.Core/Features/Tenants/TenantSlug.cs#L6)

#### UserIdentities

- [EmailAddress](../../src/Compliance.Core/Features/UserIdentities/EmailAddress.cs#L9)
- [UserIdentity](../../src/Compliance.Core/Features/UserIdentities/UserIdentity.cs#L7)

#### Work

- [WorkAssignmentLedger](../../src/Compliance.Core/Features/Work/WorkAssignmentLedger.cs#L12)
- [WorkDigestPreference](../../src/Compliance.Core/Features/Work/WorkDigestPreference.cs#L9)

#### Workforce

- [Person](../../src/Compliance.Core/Features/Workforce/Person.cs#L8)
- [ServiceIdentity](../../src/Compliance.Core/Features/Workforce/ServiceIdentity.cs#L12)
- [WorkRelationship](../../src/Compliance.Core/Features/Workforce/WorkRelationship.cs#L11)
- [WorkforceObservationResolution](../../src/Compliance.Core/Features/Workforce/WorkforceObservationResolution.cs#L11)
- [WorkforceSourceObservation](../../src/Compliance.Core/Features/Workforce/WorkforceSourceObservation.cs#L8)

### Existing models and supporting contracts

#### AccessControl

- [AccessGrantPrincipal](../../src/Compliance.Common/Features/AccessControl/AccessGrantPrincipal.cs#L5)
- [AccessGrantPrincipalKind](../../src/Compliance.Common/Features/AccessControl/AccessGrantPrincipalKind.cs#L6)
- [AccessGrantProposal](../../src/Compliance.Common/Features/AccessControl/AccessGrantProposal.cs#L5)
- [AccessGrantScope](../../src/Compliance.Common/Features/AccessControl/AccessGrantScope.cs#L5)
- [AccessGrantScopeKind](../../src/Compliance.Common/Features/AccessControl/AccessGrantScopeKind.cs#L6)
- [AccessGrantSetView](../../src/Compliance.Common/Features/AccessControl/AccessGrantSetView.cs#L5)
- [AccessGrantSource](../../src/Compliance.Common/Features/AccessControl/AccessGrantSource.cs#L3)
- [AccessGrantTerms](../../src/Compliance.Common/Features/AccessControl/AccessGrantTerms.cs#L5)
- [AccessGrantView](../../src/Compliance.Common/Features/AccessControl/AccessGrantView.cs#L5)
- [ActorReference](../../src/Compliance.Common/Features/AccessControl/ActorReference.cs#L6)
- [AssignmentIndependenceReevaluationView](../../src/Compliance.Common/Features/AccessControl/AssignmentIndependenceReevaluationView.cs#L6)
- [DirectoryIndependenceReevaluationView](../../src/Compliance.Common/Features/AccessControl/DirectoryIndependenceReevaluationView.cs#L6)
- [DirectoryStatusSourceView](../../src/Compliance.Common/Features/AccessControl/DirectoryStatusSourceView.cs#L6)
- [EngagementAcceptanceSourceTimes](../../src/Compliance.Common/Features/AccessControl/EngagementAcceptanceSourceTimes.cs#L3)
- [EngagementActualAssignmentView](../../src/Compliance.Common/Features/AccessControl/EngagementActualAssignmentView.cs#L5)
- [EngagementManagementAcknowledgementView](../../src/Compliance.Common/Features/AccessControl/EngagementManagementAcknowledgementView.cs#L5)
- [FirmStaffDirectoryView](../../src/Compliance.Common/Features/AccessControl/FirmStaffDirectoryView.cs#L5)
- [FirmStaffMemberView](../../src/Compliance.Common/Features/AccessControl/FirmStaffMemberView.cs#L5)
- [HistoricalEngagementAssignmentView](../../src/Compliance.Common/Features/AccessControl/HistoricalEngagementAssignmentView.cs#L5)
- [IClientManagementMutationRequest](../../src/Compliance.Common/Features/AccessControl/IClientManagementMutationRequest.cs#L6)
- [IClientRbacMutationRequest](../../src/Compliance.Common/Features/AccessControl/IClientRbacMutationRequest.cs#L4)
- [IFirmStaffAdministrationRequest](../../src/Compliance.Common/Features/AccessControl/IFirmStaffAdministrationRequest.cs#L5)
- [IIndependenceAdministrationRequest](../../src/Compliance.Common/Features/AccessControl/IIndependenceAdministrationRequest.cs#L6)
- [IIndependenceRuleAdministrationRequest](../../src/Compliance.Common/Features/AccessControl/IIndependenceRuleAdministrationRequest.cs#L6)
- [IRbacManagementRequest](../../src/Compliance.Common/Features/AccessControl/IRbacManagementRequest.cs#L6)
- [ISeparationOfDutiesWaiverAdminRequest](../../src/Compliance.Common/Features/AccessControl/ISeparationOfDutiesWaiverAdminRequest.cs#L6)
- [ITenantAccessRequest](../../src/Compliance.Common/Features/AccessControl/ITenantAccessRequest.cs#L6)
- [IndependenceEvaluationView](../../src/Compliance.Common/Features/AccessControl/IndependenceEvaluationView.cs#L6)
- [IndependenceHistoryView](../../src/Compliance.Common/Features/AccessControl/IndependenceHistoryView.cs#L5)
- [IndependenceRuleContent](../../src/Compliance.Common/Features/AccessControl/IndependenceRuleContent.cs#L3)
- [IndependenceRuleVersionView](../../src/Compliance.Common/Features/AccessControl/IndependenceRuleVersionView.cs#L6)
- [IndependenceServiceRuleContent](../../src/Compliance.Common/Features/AccessControl/IndependenceServiceRuleContent.cs#L3)
- [MemberAccessGrantPath](../../src/Compliance.Common/Features/AccessControl/MemberAccessGrantPath.cs#L3)
- [MemberAccessPath](../../src/Compliance.Common/Features/AccessControl/MemberAccessPath.cs#L5)
- [MemberAccessView](../../src/Compliance.Common/Features/AccessControl/MemberAccessView.cs#L5)
- [NonattestServiceContent](../../src/Compliance.Common/Features/AccessControl/NonattestServiceContent.cs#L5)
- [NonattestServiceView](../../src/Compliance.Common/Features/AccessControl/NonattestServiceView.cs#L5)
- [RolePermissionView](../../src/Compliance.Common/Features/AccessControl/RolePermissionView.cs#L5)
- [RoleTeamView](../../src/Compliance.Common/Features/AccessControl/RoleTeamView.cs#L5)
- [RoleView](../../src/Compliance.Common/Features/AccessControl/RoleView.cs#L5)
- [SeparationOfDutiesActions](../../src/Compliance.Common/Features/AccessControl/SeparationOfDutiesWaiverScope.cs#L72)
- [SeparationOfDutiesRecordTypes](../../src/Compliance.Common/Features/AccessControl/SeparationOfDutiesWaiverScope.cs#L13)
- [SeparationOfDutiesWaiverScope](../../src/Compliance.Common/Features/AccessControl/SeparationOfDutiesWaiverScope.cs#L6)
- [SeparationOfDutiesWaiverView](../../src/Compliance.Common/Features/AccessControl/SeparationOfDutiesWaiverView.cs#L7)
- [ServiceEngagementAcceptanceView](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementAcceptanceView.cs#L5)
- [ServiceEngagementBoundaryReference](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementBoundaryReference.cs#L5)
- [ServiceEngagementDraftContent](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementDraftContent.cs#L5)
- [ServiceEngagementStaffProposalView](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementStaffProposalView.cs#L5)
- [ServiceEngagementView](../../src/Compliance.Common/Features/AccessControl/ServiceEngagementView.cs#L5)
- [ServiceIndependenceReevaluationView](../../src/Compliance.Common/Features/AccessControl/ServiceIndependenceReevaluationView.cs#L6)
- [TeamMemberView](../../src/Compliance.Common/Features/AccessControl/TeamMemberView.cs#L5)
- [TeamRoleView](../../src/Compliance.Common/Features/AccessControl/TeamRoleView.cs#L5)
- [TeamView](../../src/Compliance.Common/Features/AccessControl/TeamView.cs#L5)

#### AccessReviews

- [AccessAssignmentFact](../../src/Compliance.Common/Features/AccessReviews/AccessAssignmentFact.cs#L7)
- [AccessDecisionView](../../src/Compliance.Common/Features/AccessReviews/AccessDecisionView.cs#L10)
- [AccessEntitlementFact](../../src/Compliance.Common/Features/AccessReviews/AccessEntitlementFact.cs#L8)
- [AccessExpectationExceptionView](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationExceptionView.cs#L7)
- [AccessExpectationParameters](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationParameters.cs#L9)
- [AccessExpectationView](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationView.cs#L10)
- [AccessExpectationsView](../../src/Compliance.Common/Features/AccessReviews/AccessExpectationsView.cs#L6)
- [AccessGroupMemberFact](../../src/Compliance.Common/Features/AccessReviews/AccessGroupMemberFact.cs#L7)
- [AccessPopulationAcceptance](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationAcceptance.cs#L6)
- [AccessPopulationExceptionView](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationExceptionView.cs#L10)
- [AccessPopulationFacts](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationFacts.cs#L4)
- [AccessPopulationIssue](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationIssue.cs#L7)
- [AccessPopulationPreview](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationPreview.cs#L9)
- [AccessPopulationRegistration](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationRegistration.cs#L5)
- [AccessPopulationSummaryView](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationSummaryView.cs#L6)
- [AccessPopulationView](../../src/Compliance.Common/Features/AccessReviews/AccessPopulationView.cs#L11)
- [AccessPrincipalClassificationView](../../src/Compliance.Common/Features/AccessReviews/AccessPrincipalClassificationView.cs#L11)
- [AccessPrincipalFact](../../src/Compliance.Common/Features/AccessReviews/AccessPrincipalFact.cs#L8)
- [AccessPrincipalProposalView](../../src/Compliance.Common/Features/AccessReviews/AccessPrincipalProposalView.cs#L6)
- [AccessPrincipalView](../../src/Compliance.Common/Features/AccessReviews/AccessPrincipalView.cs#L9)
- [AccessRemediationChangeView](../../src/Compliance.Common/Features/AccessReviews/AccessRemediationChangeView.cs#L7)
- [AccessRemediationExceptionView](../../src/Compliance.Common/Features/AccessReviews/AccessRemediationExceptionView.cs#L7)
- [AccessRemediationVerificationView](../../src/Compliance.Common/Features/AccessReviews/AccessRemediationVerificationView.cs#L10)
- [AccessReviewAssignment](../../src/Compliance.Common/Features/AccessReviews/AccessReviewAssignment.cs#L9)
- [AccessReviewCampaignCompletionView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignCompletionView.cs#L7)
- [AccessReviewCampaignRegistration](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignRegistration.cs#L5)
- [AccessReviewCampaignSummaryView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignSummaryView.cs#L6)
- [AccessReviewCampaignView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCampaignView.cs#L10)
- [AccessReviewCoverageView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewCoverageView.cs#L6)
- [AccessReviewInstanceCoverageView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewInstanceCoverageView.cs#L10)
- [AccessReviewItemStateView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewItemStateView.cs#L10)
- [AccessReviewItemView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewItemView.cs#L9)
- [AccessReviewerView](../../src/Compliance.Common/Features/AccessReviews/AccessReviewerView.cs#L6)
- [AccessVarianceFinding](../../src/Compliance.Common/Features/AccessReviews/AccessVarianceFinding.cs#L6)
- [AccessVarianceItemView](../../src/Compliance.Common/Features/AccessReviews/AccessVarianceItemView.cs#L10)
- [AccessVarianceView](../../src/Compliance.Common/Features/AccessReviews/AccessVarianceView.cs#L6)
- [BulkAccessDecisionPreview](../../src/Compliance.Common/Features/AccessReviews/BulkAccessDecisionPreview.cs#L9)
- [BulkAccessDecisionRejection](../../src/Compliance.Common/Features/AccessReviews/BulkAccessDecisionRejection.cs#L9)
- [BulkAccessDecisionResult](../../src/Compliance.Common/Features/AccessReviews/BulkAccessDecisionResult.cs#L4)
- [EffectiveAccessHop](../../src/Compliance.Common/Features/AccessReviews/EffectiveAccessHop.cs#L7)
- [EffectiveAccessPathView](../../src/Compliance.Common/Features/AccessReviews/EffectiveAccessPathView.cs#L4)
- [EffectiveAccessView](../../src/Compliance.Common/Features/AccessReviews/EffectiveAccessView.cs#L8)
- [IAccessReviewMutationRequest](../../src/Compliance.Common/Features/AccessReviews/IAccessReviewMutationRequest.cs#L6)
- [IAccessReviewParticipantRequest](../../src/Compliance.Common/Features/AccessReviews/IAccessReviewParticipantRequest.cs#L9)
- [IAccessReviewRequest](../../src/Compliance.Common/Features/AccessReviews/IAccessReviewRequest.cs#L6)

#### Applications

- [AccessReviewScopeDecisionView](../../src/Compliance.Common/Features/Applications/AccessReviewScopeDecisionView.cs#L6)
- [AccessReviewScopeStatusView](../../src/Compliance.Common/Features/Applications/AccessReviewScopeStatusView.cs#L11)
- [AccessReviewScopeView](../../src/Compliance.Common/Features/Applications/AccessReviewScopeView.cs#L9)
- [ApplicationBoundaryReferenceView](../../src/Compliance.Common/Features/Applications/ApplicationBoundaryReferenceView.cs#L9)
- [ApplicationChangePreview](../../src/Compliance.Common/Features/Applications/ApplicationChangePreview.cs#L6)
- [ApplicationControlDraftReferenceView](../../src/Compliance.Common/Features/Applications/ApplicationControlDraftReferenceView.cs#L9)
- [ApplicationFieldChange](../../src/Compliance.Common/Features/Applications/ApplicationFieldChange.cs#L3)
- [ApplicationImportCorrelationView](../../src/Compliance.Common/Features/Applications/ApplicationImportCorrelationView.cs#L5)
- [ApplicationImportEffectProof](../../src/Compliance.Common/Features/Applications/ApplicationImportEffectProof.cs#L5)
- [ApplicationImportInputRow](../../src/Compliance.Common/Features/Applications/ApplicationImportInputRow.cs#L4)
- [ApplicationImportMissingRow](../../src/Compliance.Common/Features/Applications/ApplicationImportMissingRow.cs#L6)
- [ApplicationImportOmissionPreview](../../src/Compliance.Common/Features/Applications/ApplicationImportOmissionPreview.cs#L5)
- [ApplicationImportOmissionProposalView](../../src/Compliance.Common/Features/Applications/ApplicationImportOmissionProposalView.cs#L5)
- [ApplicationImportPlannedRow](../../src/Compliance.Common/Features/Applications/ApplicationImportPlannedRow.cs#L5)
- [ApplicationImportPreviewRow](../../src/Compliance.Common/Features/Applications/ApplicationImportPreviewRow.cs#L6)
- [ApplicationImportProgress](../../src/Compliance.Common/Features/Applications/ApplicationImportProgress.cs#L5)
- [ApplicationImportRegistration](../../src/Compliance.Common/Features/Applications/ApplicationImportRegistration.cs#L5)
- [ApplicationImportRejectedReport](../../src/Compliance.Common/Features/Applications/ApplicationImportRejectedReport.cs#L6)
- [ApplicationImportRejectedRow](../../src/Compliance.Common/Features/Applications/ApplicationImportRejectedRow.cs#L5)
- [ApplicationImportRetirementRow](../../src/Compliance.Common/Features/Applications/ApplicationImportRetirementRow.cs#L5)
- [ApplicationImportRowView](../../src/Compliance.Common/Features/Applications/ApplicationImportRowView.cs#L5)
- [ApplicationImportStagedRow](../../src/Compliance.Common/Features/Applications/ApplicationImportStagedRow.cs#L5)
- [ApplicationImportView](../../src/Compliance.Common/Features/Applications/ApplicationImportView.cs#L5)
- [ApplicationRegistration](../../src/Compliance.Common/Features/Applications/ApplicationRegistration.cs#L5)
- [ApplicationRelationshipChange](../../src/Compliance.Common/Features/Applications/ApplicationRelationshipChange.cs#L5)
- [ApplicationRelationshipPage](../../src/Compliance.Common/Features/Applications/ApplicationRelationshipPage.cs#L5)
- [ApplicationRelationshipView](../../src/Compliance.Common/Features/Applications/ApplicationRelationshipView.cs#L5)
- [ApplicationRevisionField](../../src/Compliance.Common/Features/Applications/ApplicationRevisionField.cs#L4)
- [ApplicationRevisionView](../../src/Compliance.Common/Features/Applications/ApplicationRevisionView.cs#L8)
- [ApplicationView](../../src/Compliance.Common/Features/Applications/ApplicationView.cs#L8)
- [IApplicationInventoryRequest](../../src/Compliance.Common/Features/Applications/IApplicationInventoryRequest.cs#L6)
- [IApplicationInventoryWriteRequest](../../src/Compliance.Common/Features/Applications/IApplicationInventoryRequest.cs#L19)
- [IRestrictedApplicationResourceReadRequest](../../src/Compliance.Common/Features/Applications/IApplicationInventoryRequest.cs#L15)
- [RetirementView](../../src/Compliance.Common/Features/Applications/RetirementView.cs#L6)
- [SystemInstanceRegistration](../../src/Compliance.Common/Features/Applications/SystemInstanceRegistration.cs#L5)
- [SystemInstanceView](../../src/Compliance.Common/Features/Applications/SystemInstanceView.cs#L8)

#### Boundaries

- [BoundaryAffectedRecord](../../src/Compliance.Common/Features/Boundaries/BoundaryAffectedRecord.cs#L5)
- [BoundaryChange](../../src/Compliance.Common/Features/Boundaries/BoundaryChange.cs#L5)
- [BoundaryContent](../../src/Compliance.Common/Features/Boundaries/BoundaryContent.cs#L3)
- [BoundaryDecisionView](../../src/Compliance.Common/Features/Boundaries/BoundaryDecisionView.cs#L7)
- [BoundaryImpactContribution](../../src/Compliance.Common/Features/Boundaries/BoundaryImpactContribution.cs#L3)
- [BoundaryImpactPreview](../../src/Compliance.Common/Features/Boundaries/BoundaryImpactPreview.cs#L5)
- [BoundaryRegistration](../../src/Compliance.Common/Features/Boundaries/BoundaryRegistration.cs#L5)
- [BoundaryScopeEntry](../../src/Compliance.Common/Features/Boundaries/BoundaryScopeEntry.cs#L5)
- [BoundaryVersionView](../../src/Compliance.Common/Features/Boundaries/BoundaryVersionView.cs#L5)
- [BoundaryView](../../src/Compliance.Common/Features/Boundaries/BoundaryView.cs#L5)
- [IBoundaryAuthoringRequest](../../src/Compliance.Common/Features/Boundaries/IBoundaryAuthoringRequest.cs#L5)
- [IBoundaryProgramReadRequest](../../src/Compliance.Common/Features/Boundaries/IBoundaryProgramResourceRequest.cs#L12)
- [IBoundaryProgramResourceRequest](../../src/Compliance.Common/Features/Boundaries/IBoundaryProgramResourceRequest.cs#L5)

#### Commitments

- [CommitmentChange](../../src/Compliance.Common/Features/Commitments/CommitmentChange.cs#L3)
- [CommitmentDecisionView](../../src/Compliance.Common/Features/Commitments/CommitmentDecisionView.cs#L7)
- [CommitmentDependent](../../src/Compliance.Common/Features/Commitments/CommitmentDependent.cs#L5)
- [CommitmentDraftRegistration](../../src/Compliance.Common/Features/Commitments/CommitmentDraftRegistration.cs#L5)
- [CommitmentDraftRevisionView](../../src/Compliance.Common/Features/Commitments/CommitmentDraftRevisionView.cs#L7)
- [CommitmentDraftView](../../src/Compliance.Common/Features/Commitments/CommitmentDraftView.cs#L7)
- [CommitmentImpactPreview](../../src/Compliance.Common/Features/Commitments/CommitmentImpactPreview.cs#L10)
- [CommitmentUnlinkedContext](../../src/Compliance.Common/Features/Commitments/CommitmentUnlinkedContext.cs#L4)
- [CommitmentVersionView](../../src/Compliance.Common/Features/Commitments/CommitmentVersionView.cs#L11)

#### Compliance.Common

- [CommonAssembly](../../src/Compliance.Common/CommonAssembly.cs#L6)

#### ControlMappings

- [ControlCriterionMappingRegistration](../../src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingRegistration.cs#L5)
- [ControlCriterionMappingVersionView](../../src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingVersionView.cs#L10)
- [ControlCriterionMappingView](../../src/Compliance.Common/Features/ControlMappings/ControlCriterionMappingView.cs#L10)
- [CriterionApplicabilityRegistration](../../src/Compliance.Common/Features/ControlMappings/CriterionApplicabilityRegistration.cs#L5)
- [CriterionApplicabilityVersionView](../../src/Compliance.Common/Features/ControlMappings/CriterionApplicabilityVersionView.cs#L10)
- [CriterionApplicabilityView](../../src/Compliance.Common/Features/ControlMappings/CriterionApplicabilityView.cs#L11)
- [CriterionCoverageView](../../src/Compliance.Common/Features/ControlMappings/CriterionCoverageView.cs#L12)
- [MappedControlReference](../../src/Compliance.Common/Features/ControlMappings/MappedControlReference.cs#L10)

#### Controls

- [ControlAffectedRecord](../../src/Compliance.Common/Features/Controls/ControlAffectedRecord.cs#L5)
- [ControlApplicabilityReference](../../src/Compliance.Common/Features/Controls/ControlApplicabilityReference.cs#L5)
- [ControlChange](../../src/Compliance.Common/Features/Controls/ControlChange.cs#L5)
- [ControlContentProvenance](../../src/Compliance.Common/Features/Controls/ControlContentProvenance.cs#L10)
- [ControlDecisionView](../../src/Compliance.Common/Features/Controls/ControlDecisionView.cs#L8)
- [ControlDraftContent](../../src/Compliance.Common/Features/Controls/ControlDraftContent.cs#L3)
- [ControlDraftOwnerPersonView](../../src/Compliance.Common/Features/Controls/ControlDraftOwnerPersonView.cs#L6)
- [ControlDraftRevisionView](../../src/Compliance.Common/Features/Controls/ControlDraftRevisionView.cs#L7)
- [ControlDraftView](../../src/Compliance.Common/Features/Controls/ControlDraftView.cs#L7)
- [ControlImpactContribution](../../src/Compliance.Common/Features/Controls/ControlImpactContribution.cs#L8)
- [ControlImpactPreview](../../src/Compliance.Common/Features/Controls/ControlImpactPreview.cs#L12)
- [ControlPersonOwnerDesignationView](../../src/Compliance.Common/Features/Controls/ControlPersonOwnerDesignationView.cs#L7)
- [ControlRegistration](../../src/Compliance.Common/Features/Controls/ControlRegistration.cs#L5)
- [ControlRetirementRegistration](../../src/Compliance.Common/Features/Controls/ControlRetirementRegistration.cs#L5)
- [ControlSuccessorRegistration](../../src/Compliance.Common/Features/Controls/ControlSuccessorRegistration.cs#L5)
- [ControlVersionIds](../../src/Compliance.Common/Features/Controls/ControlVersionIds.cs#L7)
- [ControlVersionView](../../src/Compliance.Common/Features/Controls/ControlVersionView.cs#L10)

#### Criteria

- [CriteriaCatalogEdition](../../src/Compliance.Common/Features/Criteria/CriteriaCatalogEdition.cs#L11)
- [CriteriaOverlayUsageFlags](../../src/Compliance.Common/Features/Criteria/CriteriaOverlayUsageFlags.cs#L4)
- [CriteriaSupportGap](../../src/Compliance.Common/Features/Criteria/CriteriaSupportGap.cs#L4)
- [CriteriaTextOverlayContent](../../src/Compliance.Common/Features/Criteria/CriteriaTextOverlayContent.cs#L4)
- [CriteriaTextOverlayMetadata](../../src/Compliance.Common/Features/Criteria/CriteriaTextOverlayMetadata.cs#L5)
- [CriteriaTextOverlayRegistration](../../src/Compliance.Common/Features/Criteria/CriteriaTextOverlayRegistration.cs#L5)
- [CriteriaTextOverlayRevision](../../src/Compliance.Common/Features/Criteria/CriteriaTextOverlayRevision.cs#L7)
- [Criterion](../../src/Compliance.Common/Features/Criteria/Criterion.cs#L11)

#### Evaluations

- [ControlEvaluationPlanVersionView](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationPlanVersionView.cs#L6)
- [ControlEvaluationReviewView](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationReviewView.cs#L7)
- [ControlEvaluationStepView](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationStepView.cs#L6)
- [ControlEvaluationView](../../src/Compliance.Common/Features/Evaluations/ControlEvaluationView.cs#L11)
- [EvaluationAssertionConclusion](../../src/Compliance.Common/Features/Evaluations/EvaluationAssertionConclusion.cs#L8)
- [EvaluationDeviationView](../../src/Compliance.Common/Features/Evaluations/EvaluationDeviationView.cs#L11)
- [EvaluationInspectedItem](../../src/Compliance.Common/Features/Evaluations/EvaluationInspectedItem.cs#L8)
- [EvaluationProcedureStep](../../src/Compliance.Common/Features/Evaluations/EvaluationProcedureStep.cs#L7)
- [EvaluationStepResultView](../../src/Compliance.Common/Features/Evaluations/EvaluationStepResultView.cs#L6)
- [EvaluationSubmissionView](../../src/Compliance.Common/Features/Evaluations/EvaluationSubmissionView.cs#L9)

#### Evidence

- [EvidenceArtifactContent](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactContent.cs#L7)
- [EvidenceArtifactMetadataView](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactMetadataView.cs#L7)
- [EvidenceArtifactRegistration](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactRegistration.cs#L5)
- [EvidenceArtifactResourceTypes](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactResourceTypes.cs#L3)
- [EvidenceArtifactStates](../../src/Compliance.Common/Features/Evidence/EvidenceArtifactStates.cs#L4)
- [EvidenceCapture](../../src/Compliance.Common/Features/Evidence/EvidenceCapture.cs#L10)
- [EvidenceInspectionOutcome](../../src/Compliance.Common/Features/Evidence/EvidenceInspectionOutcome.cs#L4)
- [EvidenceRedactionApprovalFact](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionApprovalFact.cs#L6)
- [EvidenceRedactionApprovalView](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionApprovalView.cs#L6)
- [EvidenceRedactionArtifactView](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionArtifactView.cs#L6)
- [EvidenceRedactionPreparationFact](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionPreparationFact.cs#L6)
- [EvidenceRedactionPreparationView](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionPreparationView.cs#L6)
- [EvidenceRedactionSourceCapsule](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionSourceCapsule.cs#L6)
- [EvidenceRedactionView](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionView.cs#L6)
- [EvidenceRedactionWaiverSnapshot](../../src/Compliance.Common/Features/Evidence/EvidenceRedactionWaiverSnapshot.cs#L7)
- [EvidenceRequestView](../../src/Compliance.Common/Features/Evidence/EvidenceRequestView.cs#L10)
- [IEvidenceRedactionMutationRequest](../../src/Compliance.Common/Features/Evidence/IEvidenceRedactionMutationRequest.cs#L6)

#### Operations

- [ControlAttestationView](../../src/Compliance.Common/Features/Operations/ControlAttestationView.cs#L11)
- [ControlCadence](../../src/Compliance.Common/Features/Operations/ControlCadence.cs#L8)
- [ControlOccurrenceReviewView](../../src/Compliance.Common/Features/Operations/ControlOccurrenceReviewView.cs#L10)
- [ControlOccurrenceView](../../src/Compliance.Common/Features/Operations/ControlOccurrenceView.cs#L10)
- [ControlOperatingBlockerView](../../src/Compliance.Common/Features/Operations/ControlOperatingBlockerView.cs#L10)
- [ControlOperatingPlanPreview](../../src/Compliance.Common/Features/Operations/ControlOperatingPlanPreview.cs#L7)
- [ControlOperatingPlanSetView](../../src/Compliance.Common/Features/Operations/ControlOperatingPlanSetView.cs#L6)
- [ControlOperatingPlanView](../../src/Compliance.Common/Features/Operations/ControlOperatingPlanView.cs#L11)
- [ControlResponsibilityView](../../src/Compliance.Common/Features/Operations/ControlResponsibilityView.cs#L6)
- [EvidenceReference](../../src/Compliance.Common/Features/Operations/EvidenceReference.cs#L8)
- [IControlOperationRequest](../../src/Compliance.Common/Features/Operations/IControlOperationRequest.cs#L9)
- [IOperationsReactionRequest](../../src/Compliance.Common/Features/Operations/IOperationsReactionRequest.cs#L6)
- [MyControlWorkView](../../src/Compliance.Common/Features/Operations/MyControlWorkView.cs#L4)
- [OccurrenceReassignmentView](../../src/Compliance.Common/Features/Operations/OccurrenceReassignmentView.cs#L6)
- [OperatingHolder](../../src/Compliance.Common/Features/Operations/OperatingHolder.cs#L9)
- [WorkItemView](../../src/Compliance.Common/Features/Operations/WorkItemView.cs#L9)

#### Policies

- [PolicyApplicabilityReference](../../src/Compliance.Common/Features/Policies/PolicyApplicabilityReference.cs#L11)
- [PolicyContent](../../src/Compliance.Common/Features/Policies/PolicyContent.cs#L9)
- [PolicyDecisionView](../../src/Compliance.Common/Features/Policies/PolicyDecisionView.cs#L10)
- [PolicyImpactPreview](../../src/Compliance.Common/Features/Policies/PolicyImpactPreview.cs#L11)
- [PolicyRegistration](../../src/Compliance.Common/Features/Policies/PolicyRegistration.cs#L5)
- [PolicyRetirementProposalView](../../src/Compliance.Common/Features/Policies/PolicyRetirementProposalView.cs#L5)
- [PolicySummaryView](../../src/Compliance.Common/Features/Policies/PolicySummaryView.cs#L6)
- [PolicyVersionView](../../src/Compliance.Common/Features/Policies/PolicyVersionView.cs#L11)
- [PolicyView](../../src/Compliance.Common/Features/Policies/PolicyView.cs#L12)

#### PolicyDistribution

- [CampaignAcknowledgementView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignAcknowledgementView.cs#L6)
- [CampaignAmendmentView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignAmendmentView.cs#L6)
- [CampaignAudienceAmendment](../../src/Compliance.Common/Features/PolicyDistribution/CampaignAudienceAmendment.cs#L9)
- [CampaignAudienceEntry](../../src/Compliance.Common/Features/PolicyDistribution/CampaignAudienceEntry.cs#L6)
- [CampaignCompletionView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignCompletionView.cs#L6)
- [CampaignParticipantView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignParticipantView.cs#L11)
- [CampaignReconciliationView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignReconciliationView.cs#L5)
- [CampaignRegistration](../../src/Compliance.Common/Features/PolicyDistribution/CampaignRegistration.cs#L5)
- [CampaignSubject](../../src/Compliance.Common/Features/PolicyDistribution/CampaignSubject.cs#L9)
- [CampaignSummaryView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignSummaryView.cs#L6)
- [CampaignTotalsView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignTotalsView.cs#L8)
- [CampaignView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignView.cs#L7)
- [CampaignWaiverView](../../src/Compliance.Common/Features/PolicyDistribution/CampaignWaiverView.cs#L6)
- [TrainingRequirementContent](../../src/Compliance.Common/Features/PolicyDistribution/TrainingRequirementContent.cs#L8)
- [TrainingRequirementRegistration](../../src/Compliance.Common/Features/PolicyDistribution/TrainingRequirementRegistration.cs#L5)
- [TrainingRequirementView](../../src/Compliance.Common/Features/PolicyDistribution/TrainingRequirementView.cs#L10)

#### Programs

- [ClientServiceRegistration](../../src/Compliance.Common/Features/Programs/ClientServiceRegistration.cs#L5)
- [ClientServiceRevisionView](../../src/Compliance.Common/Features/Programs/ClientServiceRevisionView.cs#L6)
- [ClientServiceView](../../src/Compliance.Common/Features/Programs/ClientServiceView.cs#L6)
- [IClientServiceProgramReadRequest](../../src/Compliance.Common/Features/Programs/IClientServiceProgramResourceRequest.cs#L10)
- [IClientServiceProgramResourceRequest](../../src/Compliance.Common/Features/Programs/IClientServiceProgramResourceRequest.cs#L3)
- [IProgramManagementRequest](../../src/Compliance.Common/Features/Programs/IProgramManagementRequest.cs#L5)
- [IProgramReadRequest](../../src/Compliance.Common/Features/Programs/IProgramScopedRequest.cs#L16)
- [IProgramReadResourceRequest](../../src/Compliance.Common/Features/Programs/IProgramResourceRequest.cs#L15)
- [IProgramResourceRequest](../../src/Compliance.Common/Features/Programs/IProgramResourceRequest.cs#L6)
- [IProgramScopedRequest](../../src/Compliance.Common/Features/Programs/IProgramScopedRequest.cs#L6)
- [ProgramPlan](../../src/Compliance.Common/Features/Programs/ProgramPlan.cs#L3)
- [ProgramRegistration](../../src/Compliance.Common/Features/Programs/ProgramRegistration.cs#L5)
- [ProgramResourceKind](../../src/Compliance.Common/Features/Programs/ProgramResourceKind.cs#L3)
- [ProgramRevisionView](../../src/Compliance.Common/Features/Programs/ProgramRevisionView.cs#L6)
- [ProgramSetupWorkItem](../../src/Compliance.Common/Features/Programs/ProgramSetupWorkItem.cs#L5)
- [ProgramSetupWorkView](../../src/Compliance.Common/Features/Programs/ProgramSetupWorkView.cs#L5)
- [ProgramStageView](../../src/Compliance.Common/Features/Programs/ProgramStageView.cs#L3)
- [ProgramView](../../src/Compliance.Common/Features/Programs/ProgramView.cs#L6)

#### Providers

- [AssuranceBridgeLetter](../../src/Compliance.Common/Features/Providers/AssuranceBridgeLetter.cs#L7)
- [AssuranceExceptionNote](../../src/Compliance.Common/Features/Providers/AssuranceExceptionNote.cs#L4)
- [AssuranceReportContent](../../src/Compliance.Common/Features/Providers/AssuranceReportContent.cs#L9)
- [AssuranceReportCoverageView](../../src/Compliance.Common/Features/Providers/AssuranceReportCoverageView.cs#L10)
- [AssuranceReportRegistration](../../src/Compliance.Common/Features/Providers/AssuranceReportRegistration.cs#L5)
- [AssuranceReportView](../../src/Compliance.Common/Features/Providers/AssuranceReportView.cs#L11)
- [IProviderManagementRequest](../../src/Compliance.Common/Features/Providers/IProviderManagementRequest.cs#L5)
- [IProviderRequest](../../src/Compliance.Common/Features/Providers/IProviderRequest.cs#L5)
- [ProviderAssuranceCoverageView](../../src/Compliance.Common/Features/Providers/ProviderAssuranceCoverageView.cs#L15)
- [ProviderChangeAffectedRecord](../../src/Compliance.Common/Features/Providers/ProviderChangeImpactPreview.cs#L17)
- [ProviderChangeImpactPreview](../../src/Compliance.Common/Features/Providers/ProviderChangeImpactPreview.cs#L9)
- [ProviderChangeImpactSection](../../src/Compliance.Common/Features/Providers/ProviderChangeImpactPreview.cs#L14)
- [ProviderContent](../../src/Compliance.Common/Features/Providers/ProviderContent.cs#L6)
- [ProviderCoverageGapClosureContent](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapView.cs#L16)
- [ProviderCoverageGapClosureView](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapView.cs#L19)
- [ProviderCoverageGapContent](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapContent.cs#L6)
- [ProviderCoverageGapRegistration](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapView.cs#L36)
- [ProviderCoverageGapRiskAcceptanceContent](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapView.cs#L33)
- [ProviderCoverageGapRiskAcceptanceView](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapView.cs#L26)
- [ProviderCoverageGapView](../../src/Compliance.Common/Features/Providers/ProviderCoverageGapView.cs#L7)
- [ProviderDependency](../../src/Compliance.Common/Features/Providers/ProviderDependency.cs#L6)
- [ProviderRegistration](../../src/Compliance.Common/Features/Providers/ProviderRegistration.cs#L5)
- [ProviderReviewContent](../../src/Compliance.Common/Features/Providers/ProviderReviewContent.cs#L10)
- [ProviderReviewRegistration](../../src/Compliance.Common/Features/Providers/ProviderReviewRegistration.cs#L5)
- [ProviderReviewView](../../src/Compliance.Common/Features/Providers/ProviderReviewView.cs#L11)
- [ProviderSourceCitation](../../src/Compliance.Common/Features/Providers/ProviderSourceCitation.cs#L6)
- [ProviderView](../../src/Compliance.Common/Features/Providers/ProviderView.cs#L7)

#### Readiness

- [ReadinessAnnotationView](../../src/Compliance.Common/Features/Readiness/ReadinessAnnotationView.cs#L10)
- [ReadinessAssessmentRegistration](../../src/Compliance.Common/Features/Readiness/ReadinessAssessmentRegistration.cs#L5)
- [ReadinessAssessmentSummaryView](../../src/Compliance.Common/Features/Readiness/ReadinessAssessmentSummaryView.cs#L6)
- [ReadinessAssessmentView](../../src/Compliance.Common/Features/Readiness/ReadinessAssessmentView.cs#L11)
- [ReadinessDecisionView](../../src/Compliance.Common/Features/Readiness/ReadinessDecisionView.cs#L7)
- [ReadinessFindingView](../../src/Compliance.Common/Features/Readiness/ReadinessFindingView.cs#L9)
- [ReadinessGapPlanView](../../src/Compliance.Common/Features/Readiness/ReadinessGapPlanView.cs#L7)
- [ReadinessGapView](../../src/Compliance.Common/Features/Readiness/ReadinessGapView.cs#L6)
- [ReadinessInputView](../../src/Compliance.Common/Features/Readiness/ReadinessInputView.cs#L8)
- [ReadinessSourceReference](../../src/Compliance.Common/Features/Readiness/ReadinessSourceReference.cs#L6)
- [TypeIEntryDecisionView](../../src/Compliance.Common/Features/Readiness/TypeIEntryDecisionView.cs#L12)
- [TypeIEntryUnresolvedItemView](../../src/Compliance.Common/Features/Readiness/TypeIEntryUnresolvedItemView.cs#L6)

#### Remediation

- [CorrectiveActionView](../../src/Compliance.Common/Features/Remediation/CorrectiveActionView.cs#L8)
- [FindingAcceptanceView](../../src/Compliance.Common/Features/Remediation/FindingAcceptanceView.cs#L11)
- [FindingClosureView](../../src/Compliance.Common/Features/Remediation/FindingClosureView.cs#L8)
- [FindingHistoryEntryView](../../src/Compliance.Common/Features/Remediation/FindingHistoryEntryView.cs#L6)
- [FindingLink](../../src/Compliance.Common/Features/Remediation/FindingLink.cs#L7)
- [FindingRegistration](../../src/Compliance.Common/Features/Remediation/FindingRegistration.cs#L5)
- [FindingSource](../../src/Compliance.Common/Features/Remediation/FindingSource.cs#L10)
- [FindingView](../../src/Compliance.Common/Features/Remediation/FindingView.cs#L10)

#### Responsibilities

- [ResponsibilityAssignmentView](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityAssignmentView.cs#L5)
- [ResponsibilityConflictKind](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityConflictKind.cs#L6)
- [ResponsibilityConflictPreview](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityConflictPreview.cs#L3)
- [ResponsibilityConflictView](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityConflictView.cs#L5)
- [ResponsibilityScope](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityScope.cs#L5)
- [ResponsibilitySetView](../../src/Compliance.Common/Features/Responsibilities/ResponsibilitySetView.cs#L5)
- [ResponsibilityType](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityType.cs#L6)
- [ResponsibilityTypeWireName](../../src/Compliance.Common/Features/Responsibilities/ResponsibilityTypeWireName.cs#L3)

#### Retention

- [ArtifactLegalHoldView](../../src/Compliance.Common/Features/Retention/ArtifactLegalHoldView.cs#L5)
- [ArtifactRetentionSource](../../src/Compliance.Common/Features/Retention/ArtifactRetentionSource.cs#L6)
- [ArtifactRetentionView](../../src/Compliance.Common/Features/Retention/ArtifactRetentionView.cs#L5)
- [IArtifactRetentionAdminRequest](../../src/Compliance.Common/Features/Retention/IArtifactRetentionAdminRequest.cs#L5)

#### Risks

- [IRiskReassessmentReactionRequest](../../src/Compliance.Common/Features/Risks/IRiskReassessmentReactionRequest.cs#L6)
- [RiskAcceptanceView](../../src/Compliance.Common/Features/Risks/RiskAcceptanceView.cs#L7)
- [RiskAssessmentView](../../src/Compliance.Common/Features/Risks/RiskAssessmentView.cs#L7)
- [RiskControlTreatmentRegistration](../../src/Compliance.Common/Features/Risks/RiskControlTreatmentRegistration.cs#L5)
- [RiskControlTreatmentView](../../src/Compliance.Common/Features/Risks/RiskControlTreatmentView.cs#L12)
- [RiskDraftContent](../../src/Compliance.Common/Features/Risks/RiskDraftContent.cs#L3)
- [RiskDraftRevisionView](../../src/Compliance.Common/Features/Risks/RiskDraftRevisionView.cs#L7)
- [RiskDraftView](../../src/Compliance.Common/Features/Risks/RiskDraftView.cs#L7)
- [RiskEvaluationHistoryEntryView](../../src/Compliance.Common/Features/Risks/RiskEvaluationHistoryEntryView.cs#L7)
- [RiskEvaluationView](../../src/Compliance.Common/Features/Risks/RiskEvaluationView.cs#L6)
- [RiskGovernanceView](../../src/Compliance.Common/Features/Risks/RiskGovernanceView.cs#L12)
- [RiskMethodVersionView](../../src/Compliance.Common/Features/Risks/RiskMethodVersionView.cs#L7)
- [RiskOwnerView](../../src/Compliance.Common/Features/Risks/RiskOwnerView.cs#L11)
- [RiskReassessmentTriggerView](../../src/Compliance.Common/Features/Risks/RiskReassessmentTriggerView.cs#L11)
- [RiskRegistration](../../src/Compliance.Common/Features/Risks/RiskRegistration.cs#L5)
- [RiskTreatmentActionCompletionRegistration](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionCompletionRegistration.cs#L5)
- [RiskTreatmentActionCompletionView](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionCompletionView.cs#L11)
- [RiskTreatmentActionRegistration](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionRegistration.cs#L5)
- [RiskTreatmentActionView](../../src/Compliance.Common/Features/Risks/RiskTreatmentActionView.cs#L13)
- [RiskTreatmentView](../../src/Compliance.Common/Features/Risks/RiskTreatmentView.cs#L5)

#### Snapshots

- [ISnapshotProgramReadRequest](../../src/Compliance.Common/Features/Snapshots/ISnapshotProgramResourceRequest.cs#L12)
- [ISnapshotProgramResourceRequest](../../src/Compliance.Common/Features/Snapshots/ISnapshotProgramResourceRequest.cs#L5)
- [PopulationRow](../../src/Compliance.Common/Features/Snapshots/PopulationRow.cs#L5)
- [PopulationSnapshotManifestRegeneration](../../src/Compliance.Common/Features/Snapshots/PopulationSnapshotManifestRegeneration.cs#L9)
- [PopulationSnapshotSummary](../../src/Compliance.Common/Features/Snapshots/PopulationSnapshotSummary.cs#L7)
- [ProgramScopeManifest](../../src/Compliance.Common/Features/Snapshots/ProgramScopeManifest.cs#L5)
- [ProgramScopeSnapshotManifestRegeneration](../../src/Compliance.Common/Features/Snapshots/ProgramScopeSnapshotManifestRegeneration.cs#L5)
- [ProgramScopeSnapshotVerification](../../src/Compliance.Common/Features/Snapshots/ProgramScopeSnapshotVerification.cs#L5)
- [SnapshotRegistration](../../src/Compliance.Common/Features/Snapshots/SnapshotRegistration.cs#L5)
- [SnapshotSourceCheck](../../src/Compliance.Common/Features/Snapshots/SnapshotSourceCheck.cs#L3)
- [SnapshotView](../../src/Compliance.Common/Features/Snapshots/SnapshotView.cs#L5)

#### TechnologyInventory

- [DataFlowContent](../../src/Compliance.Common/Features/TechnologyInventory/DataFlowContent.cs#L10)
- [DataFlowImpactView](../../src/Compliance.Common/Features/TechnologyInventory/DataFlowImpactView.cs#L10)
- [DataFlowRegistration](../../src/Compliance.Common/Features/TechnologyInventory/DataFlowRegistration.cs#L5)
- [DataFlowView](../../src/Compliance.Common/Features/TechnologyInventory/DataFlowView.cs#L7)
- [ITechnologyInventoryReadRequest](../../src/Compliance.Common/Features/TechnologyInventory/ITechnologyInventoryReadRequest.cs#L4)
- [ITechnologyInventoryRequest](../../src/Compliance.Common/Features/TechnologyInventory/ITechnologyInventoryRequest.cs#L5)
- [ITechnologyInventoryWriteRequest](../../src/Compliance.Common/Features/TechnologyInventory/ITechnologyInventoryWriteRequest.cs#L6)
- [InformationAssetChangePreview](../../src/Compliance.Common/Features/TechnologyInventory/InformationAssetChangePreview.cs#L10)
- [InformationAssetContent](../../src/Compliance.Common/Features/TechnologyInventory/InformationAssetContent.cs#L6)
- [InformationAssetRegistration](../../src/Compliance.Common/Features/TechnologyInventory/InformationAssetRegistration.cs#L5)
- [InformationAssetView](../../src/Compliance.Common/Features/TechnologyInventory/InformationAssetView.cs#L7)
- [LocationContent](../../src/Compliance.Common/Features/TechnologyInventory/LocationContent.cs#L9)
- [LocationRegistration](../../src/Compliance.Common/Features/TechnologyInventory/LocationRegistration.cs#L5)
- [LocationView](../../src/Compliance.Common/Features/TechnologyInventory/LocationView.cs#L7)
- [OperationalProcessContent](../../src/Compliance.Common/Features/TechnologyInventory/OperationalProcessContent.cs#L9)
- [OperationalProcessRegistration](../../src/Compliance.Common/Features/TechnologyInventory/OperationalProcessRegistration.cs#L5)
- [OperationalProcessView](../../src/Compliance.Common/Features/TechnologyInventory/OperationalProcessView.cs#L7)
- [TechnologyComponentContent](../../src/Compliance.Common/Features/TechnologyInventory/TechnologyComponentContent.cs#L9)
- [TechnologyComponentRegistration](../../src/Compliance.Common/Features/TechnologyInventory/TechnologyComponentRegistration.cs#L5)
- [TechnologyComponentView](../../src/Compliance.Common/Features/TechnologyInventory/TechnologyComponentView.cs#L7)
- [TechnologyInventoryResourceTypes](../../src/Compliance.Common/Features/TechnologyInventory/TechnologyInventoryResourceTypes.cs#L4)

#### Tenants

- [IPlatformOperatorOrTenantAdminRequest](../../src/Compliance.Common/Features/Tenants/IPlatformOperatorOrTenantAdminRequest.cs#L6)
- [IPlatformOperatorRequest](../../src/Compliance.Common/Features/Tenants/IPlatformOperatorRequest.cs#L5)
- [ITenantLifecycleReactionRequest](../../src/Compliance.Common/Features/Tenants/ITenantLifecycleReactionRequest.cs#L6)
- [PlatformOperatorRosterView](../../src/Compliance.Common/Features/Tenants/PlatformOperatorRosterView.cs#L5)
- [TenantInvitationView](../../src/Compliance.Common/Features/Tenants/TenantInvitationView.cs#L6)
- [TenantMembershipSummary](../../src/Compliance.Common/Features/Tenants/TenantMembershipSummary.cs#L5)
- [TenantMembershipView](../../src/Compliance.Common/Features/Tenants/TenantMembershipView.cs#L9)
- [TenantProvisioningStatusView](../../src/Compliance.Common/Features/Tenants/TenantProvisioningStatusView.cs#L5)
- [TenantRegistration](../../src/Compliance.Common/Features/Tenants/TenantRegistration.cs#L5)
- [TenantSlugResolution](../../src/Compliance.Common/Features/Tenants/TenantSlugResolution.cs#L5)
- [TenantView](../../src/Compliance.Common/Features/Tenants/TenantView.cs#L5)

#### UserIdentities

- [AuthenticatedUserIdentity](../../src/Compliance.Common/Features/UserIdentities/AuthenticatedUserIdentity.cs#L6)
- [EmailAddressView](../../src/Compliance.Common/Features/UserIdentities/EmailAddressView.cs#L5)
- [EmailChallengeStatusView](../../src/Compliance.Common/Features/UserIdentities/EmailChallengeStatusView.cs#L3)
- [IEmailOwnershipRequest](../../src/Compliance.Common/Features/UserIdentities/IEmailOwnershipRequest.cs#L5)
- [IdentityRecoveryOption](../../src/Compliance.Common/Features/UserIdentities/GetIdentityRecoveryOptions.cs#L19)
- [IdentityRecoveryOptions](../../src/Compliance.Common/Features/UserIdentities/GetIdentityRecoveryOptions.cs#L15)

#### Work

- [WorkAssignmentEntryView](../../src/Compliance.Common/Features/Work/WorkAssignmentEntryView.cs#L7)
- [WorkCountsView](../../src/Compliance.Common/Features/Work/WorkCountsView.cs#L4)
- [WorkDigestPreferenceView](../../src/Compliance.Common/Features/Work/WorkDigestPreferenceView.cs#L4)
- [WorkDigestView](../../src/Compliance.Common/Features/Work/WorkDigestView.cs#L9)
- [WorkItemDetailView](../../src/Compliance.Common/Features/Work/WorkItemDetailView.cs#L4)
- [WorkQueueItemView](../../src/Compliance.Common/Features/Work/WorkQueueItemView.cs#L15)
- [WorkQueueView](../../src/Compliance.Common/Features/Work/WorkQueueView.cs#L4)
- [WorkReminderView](../../src/Compliance.Common/Features/Work/WorkReminderView.cs#L9)

#### Workforce

- [FrozenPerson](../../src/Compliance.Common/Features/Workforce/FrozenPerson.cs#L6)
- [FrozenWorkRelationship](../../src/Compliance.Common/Features/Workforce/FrozenWorkRelationship.cs#L9)
- [IWorkforceRequest](../../src/Compliance.Common/Features/Workforce/IWorkforceRequest.cs#L5)
- [PersonRegistration](../../src/Compliance.Common/Features/Workforce/PersonRegistration.cs#L5)
- [PersonView](../../src/Compliance.Common/Features/Workforce/PersonView.cs#L11)
- [PersonalContactDetails](../../src/Compliance.Common/Features/Workforce/PersonalContactDetails.cs#L4)
- [ServiceIdentityRegistration](../../src/Compliance.Common/Features/Workforce/ServiceIdentityRegistration.cs#L5)
- [ServiceIdentityTerms](../../src/Compliance.Common/Features/Workforce/ServiceIdentityTerms.cs#L10)
- [ServiceIdentityView](../../src/Compliance.Common/Features/Workforce/ServiceIdentityView.cs#L13)
- [WorkRelationshipRegistration](../../src/Compliance.Common/Features/Workforce/WorkRelationshipRegistration.cs#L5)
- [WorkRelationshipTerms](../../src/Compliance.Common/Features/Workforce/WorkRelationshipTerms.cs#L10)
- [WorkRelationshipView](../../src/Compliance.Common/Features/Workforce/WorkRelationshipView.cs#L10)
- [WorkforceObservationResolutionView](../../src/Compliance.Common/Features/Workforce/WorkforceObservationResolutionView.cs#L10)
- [WorkforceObservationView](../../src/Compliance.Common/Features/Workforce/WorkforceObservationView.cs#L13)
- [WorkforcePersonSourceFacts](../../src/Compliance.Common/Features/Workforce/WorkforcePersonSourceFacts.cs#L4)
- [WorkforceReconciliationObservationView](../../src/Compliance.Common/Features/Workforce/WorkforceReconciliationObservationView.cs#L12)
- [WorkforceRosterSnapshotView](../../src/Compliance.Common/Features/Workforce/WorkforceRosterSnapshotView.cs#L10)
- [WorkforceServiceSourceFacts](../../src/Compliance.Common/Features/Workforce/WorkforceServiceSourceFacts.cs#L4)
- [WorkforceSourceDecision](../../src/Compliance.Common/Features/Workforce/WorkforceSourceDecision.cs#L6)
- [WorkforceSourceFacts](../../src/Compliance.Common/Features/Workforce/WorkforceSourceFacts.cs#L4)
- [WorkforceSourceIdentity](../../src/Compliance.Common/Features/Workforce/WorkforceSourceIdentity.cs#L4)
- [WorkforceSourcePreview](../../src/Compliance.Common/Features/Workforce/WorkforceSourcePreview.cs#L6)
- [WorkforceSourceRegistration](../../src/Compliance.Common/Features/Workforce/WorkforceSourceRegistration.cs#L5)
- [WorkforceSourceView](../../src/Compliance.Common/Features/Workforce/WorkforceSourceView.cs#L10)

### Existing reactors

#### AccessControl

- [AcceptedStaffDirectoryReconciliationReactor](../../src/Compliance.Core/Features/AccessControl/AcceptedStaffDirectoryReconciliationReactor.cs#L5)
- [AccessReviewGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/AccessReviewGrantBackfillReactor.cs#L8)
- [ApplicationImportGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ApplicationImportGrantBackfillReactor.cs#L6)
- [ApplicationInventoryGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ApplicationInventoryGrantBackfillReactor.cs#L8)
- [BuiltInRoleCatalogMigrationReactor](../../src/Compliance.Core/Features/AccessControl/BuiltInRoleCatalogMigrationReactor.cs#L6)
- [EvidenceArtifactReadPermissionReactor](../../src/Compliance.Core/Features/AccessControl/EvidenceArtifactReadPermissionReactor.cs#L6)
- [EvidenceRedactionPermissionsReactor](../../src/Compliance.Core/Features/AccessControl/EvidenceRedactionPermissionsReactor.cs#L6)
- [FirmStaffStatusReevaluationReactor](../../src/Compliance.Core/Features/AccessControl/FirmStaffStatusReevaluationReactor.cs#L5)
- [ProviderInventoryGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/ProviderInventoryGrantBackfillReactor.cs#L8)
- [RoleCleanupReactor](../../src/Compliance.Core/Features/AccessControl/RoleCleanupReactor.cs#L10)
- [TeamCleanupReactor](../../src/Compliance.Core/Features/AccessControl/TeamCleanupReactor.cs#L11)
- [TechnologyInventoryGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/TechnologyInventoryGrantBackfillReactor.cs#L6)
- [TenantRbacBootstrapReactor](../../src/Compliance.Core/Features/AccessControl/TenantRbacBootstrapReactor.cs#L5)
- [WorkforceGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/WorkforceGrantBackfillReactor.cs#L8)
- [WorkforcePersonalDetailsGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/WorkforcePersonalDetailsGrantBackfillReactor.cs#L10)
- [WorkforceRestrictedFieldGrantBackfillReactor](../../src/Compliance.Core/Features/AccessControl/WorkforceRestrictedFieldGrantBackfillReactor.cs#L9)

#### Applications

- [ApplicationImportExecutionReactor](../../src/Compliance.Core/Features/Applications/ApplicationImportExecutionReactor.cs#L6)

#### Evaluations

- [ControlEvaluationDeviationFindingReactor](../../src/Compliance.Core/Features/Evaluations/ControlEvaluationDeviationFindingReactor.cs#L9)

#### Evidence

- [EvidenceArtifactInspectionEffectsReactor](../../src/Compliance.Core/Features/Evidence/EvidenceArtifactInspectionEffectsReactor.cs#L6)

#### Remediation

- [ControlOccurrenceFindingReactor](../../src/Compliance.Core/Features/Remediation/ControlOccurrenceFindingReactor.cs#L11)

#### Risks

- [BoundaryRiskReassessmentReactor](../../src/Compliance.Core/Features/Risks/BoundaryRiskReassessmentReactor.cs#L10)
- [RiskMethodReassessmentReactor](../../src/Compliance.Core/Features/Risks/RiskMethodReassessmentReactor.cs#L9)

#### Tenants

- [TenantInvitationDeliveryReactor](../../src/Compliance.Core/Features/Tenants/TenantInvitationDeliveryReactor.cs#L8)
- [TenantInvitationReactor](../../src/Compliance.Core/Features/Tenants/TenantInvitationReactor.cs#L6)
- [TenantRegistrationReactor](../../src/Compliance.Core/Features/Tenants/TenantRegistrationReactor.cs#L5)
- [TenantSelfServiceActivationReactor](../../src/Compliance.Core/Features/Tenants/TenantSelfServiceActivationReactor.cs#L6)
- [TenantSlugReactor](../../src/Compliance.Core/Features/Tenants/TenantSlugReactor.cs#L5)

#### UserIdentities

- [EmailChallengeDeliveryReactor](../../src/Compliance.Core/Features/UserIdentities/EmailChallengeDeliveryReactor.cs#L8)
- [EmailReservationReactor](../../src/Compliance.Core/Features/UserIdentities/EmailReservationReactor.cs#L5)
- [IdentityRecoveryNoticeReactor](../../src/Compliance.Core/Features/UserIdentities/IdentityRecoveryNoticeReactor.cs#L6)

### Existing projectors

#### AccessControl

- [AccessGrantProjector](../../src/Compliance.Core/Features/AccessControl/AccessGrantProjector.cs#L5)
- [ActualStaffEngagementLocatorProjector](../../src/Compliance.Core/Features/AccessControl/ActualStaffEngagementLocatorProjector.cs#L5)
- [PermissionProjector](../../src/Compliance.Core/Features/AccessControl/PermissionProjector.cs#L5)
- [RoleDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/RoleDirectoryProjector.cs#L5)
- [RolePermissionDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/RolePermissionDirectoryProjector.cs#L5)
- [RoleTeamDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/RoleTeamDirectoryProjector.cs#L5)
- [TeamDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/TeamDirectoryProjector.cs#L5)
- [TeamMemberDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/TeamMemberDirectoryProjector.cs#L5)
- [TeamRoleDirectoryProjector](../../src/Compliance.Core/Features/AccessControl/TeamRoleDirectoryProjector.cs#L5)

#### AccessReviews

- [AccessPopulationDirectoryProjector](../../src/Compliance.Core/Features/AccessReviews/AccessPopulationDirectoryProjector.cs#L5)
- [AccessReviewCampaignDirectoryProjector](../../src/Compliance.Core/Features/AccessReviews/AccessReviewCampaignDirectoryProjector.cs#L5)

#### Applications

- [AccessReviewScopeDirectoryProjector](../../src/Compliance.Core/Features/Applications/AccessReviewScopeDirectoryProjector.cs#L5)
- [ApplicationBoundaryReferenceProjector](../../src/Compliance.Core/Features/Applications/ApplicationBoundaryReferenceProjector.cs#L6)
- [ApplicationControlDraftReferenceProjector](../../src/Compliance.Core/Features/Applications/ApplicationControlDraftReferenceProjector.cs#L7)
- [ApplicationDirectoryProjector](../../src/Compliance.Core/Features/Applications/ApplicationDirectoryProjector.cs#L5)
- [ApplicationImportProjector](../../src/Compliance.Core/Features/Applications/ApplicationImportProjector.cs#L5)

#### Boundaries

- [BoundaryDirectoryProjector](../../src/Compliance.Core/Features/Boundaries/BoundaryDirectoryProjector.cs#L5)

#### Commitments

- [CommitmentDraftDirectoryProjector](../../src/Compliance.Core/Features/Commitments/CommitmentDraftDirectoryProjector.cs#L5)
- [CommitmentDraftHistoryDirectoryProjectorV1](../../src/Compliance.Core/Features/Commitments/CommitmentDraftHistoryDirectoryProjectorV1.cs#L5)

#### ControlMappings

- [ControlMappingDirectoryV1Projector](../../src/Compliance.Core/Features/ControlMappings/ControlMappingDirectoryV1Projector.cs#L5)
- [CriterionApplicabilityDirectoryV1Projector](../../src/Compliance.Core/Features/ControlMappings/CriterionApplicabilityDirectoryV1Projector.cs#L5)

#### Controls

- [ControlDraftDirectoryV2Projector](../../src/Compliance.Core/Features/Controls/ControlDraftDirectoryV2Projector.cs#L5)
- [ControlDraftHistoryDirectoryV1Projector](../../src/Compliance.Core/Features/Controls/ControlDraftHistoryDirectoryV1Projector.cs#L5)

#### Criteria

- [CriteriaTextOverlayDirectoryProjector](../../src/Compliance.Core/Features/Criteria/CriteriaTextOverlayDirectoryProjector.cs#L5)

#### Policies

- [PolicyDirectoryProjector](../../src/Compliance.Core/Features/Policies/PolicyDirectoryProjector.cs#L5)

#### PolicyDistribution

- [CampaignDirectoryProjector](../../src/Compliance.Core/Features/PolicyDistribution/CampaignDirectoryProjector.cs#L5)

#### Programs

- [ClientServiceDirectoryProjector](../../src/Compliance.Core/Features/Programs/ClientServiceDirectoryProjector.cs#L5)
- [ProgramDirectoryProjector](../../src/Compliance.Core/Features/Programs/ProgramDirectoryProjector.cs#L6)

#### Providers

- [AssuranceProjector](../../src/Compliance.Core/Features/Providers/AssuranceProjector.cs#L5)
- [ProviderProjector](../../src/Compliance.Core/Features/Providers/ProviderProjector.cs#L5)

#### Readiness

- [ReadinessDirectoryProjector](../../src/Compliance.Core/Features/Readiness/ReadinessDirectoryProjector.cs#L5)

#### Responsibilities

- [MemberResponsibilityProjector](../../src/Compliance.Core/Features/Responsibilities/MemberResponsibilityProjector.cs#L5)
- [ResponsibilitySetProjector](../../src/Compliance.Core/Features/Responsibilities/ResponsibilitySetProjector.cs#L5)

#### Risks

- [RiskDraftDirectoryProjector](../../src/Compliance.Core/Features/Risks/RiskDraftDirectoryProjector.cs#L5)
- [RiskDraftHistoryProjectorV1](../../src/Compliance.Core/Features/Risks/RiskDraftHistoryProjectorV1.cs#L5)
- [RiskEvaluationDirectoryProjectorV1](../../src/Compliance.Core/Features/Risks/RiskEvaluationDirectoryProjectorV1.cs#L5)

#### Snapshots

- [PopulationSnapshotDirectoryProjector](../../src/Compliance.Core/Features/Snapshots/PopulationSnapshotDirectoryProjector.cs#L5)
- [SnapshotDirectoryProjector](../../src/Compliance.Core/Features/Snapshots/SnapshotDirectoryProjector.cs#L5)

#### TechnologyInventory

- [InventoryRegisterProjector](../../src/Compliance.Core/Features/TechnologyInventory/InventoryRegisterProjector.cs#L5)
- [TechnologyInventoryDirectoryProjector](../../src/Compliance.Core/Features/TechnologyInventory/TechnologyInventoryDirectoryProjector.cs#L5)

#### Tenants

- [TenantDirectoryProjector](../../src/Compliance.Core/Features/Tenants/TenantDirectoryProjector.cs#L5)
- [TenantInvitationDirectoryProjector](../../src/Compliance.Core/Features/Tenants/TenantInvitationDirectoryProjector.cs#L5)
- [TenantMembershipProjector](../../src/Compliance.Core/Features/Tenants/TenantMembershipProjector.cs#L5)

#### UserIdentities

- [EmailAddressDirectoryProjector](../../src/Compliance.Core/Features/UserIdentities/EmailAddressDirectoryProjector.cs#L5)
- [IdentityDirectoryProjector](../../src/Compliance.Core/Features/UserIdentities/IdentityDirectoryProjector.cs#L5)
- [PlatformUserDirectoryProjector](../../src/Compliance.Core/Features/UserIdentities/PlatformUserDirectoryProjector.cs#L5)

#### Work

- [BoundaryDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/BoundaryDecisionWorkItemProjector.cs#L7)
- [CommitmentDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/CommitmentDecisionWorkItemProjector.cs#L7)
- [ControlCriterionMappingWorkItemProjector](../../src/Compliance.Core/Features/Work/ControlCriterionMappingWorkItemProjector.cs#L6)
- [ControlDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/ControlDecisionWorkItemProjector.cs#L7)
- [ControlEvaluationWorkItemProjector](../../src/Compliance.Core/Features/Work/ControlEvaluationWorkItemProjector.cs#L6)
- [ControlOccurrenceWorkItemProjector](../../src/Compliance.Core/Features/Work/ControlOccurrenceWorkItemProjector.cs#L6)
- [ControlOperatingPlanWorkItemProjector](../../src/Compliance.Core/Features/Work/ControlOperatingPlanWorkItemProjector.cs#L6)
- [CorrectiveActionWorkItemProjector](../../src/Compliance.Core/Features/Work/CorrectiveActionWorkItemProjector.cs#L6)
- [CriterionApplicabilityWorkItemProjector](../../src/Compliance.Core/Features/Work/CriterionApplicabilityWorkItemProjector.cs#L6)
- [EvidenceWorkItemProjector](../../src/Compliance.Core/Features/Work/EvidenceWorkItemProjector.cs#L6)
- [FindingClosureWorkItemProjector](../../src/Compliance.Core/Features/Work/FindingClosureWorkItemProjector.cs#L6)
- [PolicyCampaignWorkItemProjector](../../src/Compliance.Core/Features/Work/PolicyCampaignWorkItemProjector.cs#L6)
- [PolicyDecisionWorkItemProjector](../../src/Compliance.Core/Features/Work/PolicyDecisionWorkItemProjector.cs#L6)
- [RiskGovernanceWorkItemProjector](../../src/Compliance.Core/Features/Work/RiskGovernanceWorkItemProjector.cs#L6)

#### Workforce

- [PersonDirectoryProjector](../../src/Compliance.Core/Features/Workforce/PersonDirectoryProjector.cs#L5)
- [ServiceIdentityDirectoryProjector](../../src/Compliance.Core/Features/Workforce/ServiceIdentityDirectoryProjector.cs#L5)
- [WorkRelationshipDirectoryProjector](../../src/Compliance.Core/Features/Workforce/WorkRelationshipDirectoryProjector.cs#L5)
- [WorkforceObservationProjector](../../src/Compliance.Core/Features/Workforce/WorkforceObservationProjector.cs#L5)
- [WorkforceObservationResolutionProjector](../../src/Compliance.Core/Features/Workforce/WorkforceObservationResolutionProjector.cs#L5)
- [WorkforceSourceProjector](../../src/Compliance.Core/Features/Workforce/WorkforceSourceProjector.cs#L5)

#### Requirement-backed response views added 2026-10-17

##### AdvisorCollaboration

- [AdvisorRequestView](../../src/Compliance.Common/Features/AdvisorCollaboration/AdvisorRequestView.cs#L6)
- [AdvisorRequestSummaryView](../../src/Compliance.Common/Features/AdvisorCollaboration/AdvisorRequestSummaryView.cs#L6)
- [AdvisorFeedbackView](../../src/Compliance.Common/Features/AdvisorCollaboration/AdvisorFeedbackView.cs#L6)

##### Examinations

- [ExaminationEngagementView](../../src/Compliance.Common/Features/Examinations/ExaminationEngagementView.cs#L6)
- [TypeIBaselineView](../../src/Compliance.Common/Features/Examinations/TypeIBaselineView.cs#L6)
- [EngagementAccessView](../../src/Compliance.Common/Features/Examinations/EngagementAccessView.cs#L6)

##### SystemDescription

- [SystemDescriptionView](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionView.cs#L6)
- [SystemDescriptionVersionView](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionVersionView.cs#L6)
- [SystemDescriptionImpactPreview](../../src/Compliance.Common/Features/SystemDescription/SystemDescriptionImpactPreview.cs#L6)

##### AuditorRequests

- [AuditorRequestView](../../src/Compliance.Common/Features/AuditorRequests/AuditorRequestView.cs#L6)
- [AuditorRequestSummaryView](../../src/Compliance.Common/Features/AuditorRequests/AuditorRequestSummaryView.cs#L6)
- [SampleSelectionView](../../src/Compliance.Common/Features/AuditorRequests/SampleSelectionView.cs#L6)
- [AuditorRequestResponseView](../../src/Compliance.Common/Features/AuditorRequests/AuditorRequestResponseView.cs#L6)

##### AuditPackages

- [AuditPackageView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageView.cs#L6)
- [AuditPackageSummaryView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageSummaryView.cs#L6)
- [AuditPackageManifestView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageManifestView.cs#L6)
- [AuditPackageValidationView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageValidationView.cs#L6)
- [AuditPackageDeliveryView](../../src/Compliance.Common/Features/AuditPackages/AuditPackageDeliveryView.cs#L6)

##### Examinations

- [AuditObservationView](../../src/Compliance.Common/Features/Examinations/AuditObservationView.cs#L6)
- [AuditOutcomeView](../../src/Compliance.Common/Features/Examinations/AuditOutcomeView.cs#L6)
- [TypeIIPlanView](../../src/Compliance.Common/Features/Examinations/TypeIIPlanView.cs#L6)
- [ManagementAssertionView](../../src/Compliance.Common/Features/Examinations/ManagementAssertionView.cs#L6)
- [ExaminationSignOffView](../../src/Compliance.Common/Features/Examinations/ExaminationSignOffView.cs#L6)

##### Operations

- [ObservationPeriodView](../../src/Compliance.Common/Features/Operations/ObservationPeriodView.cs#L6)
- [ObservationPeriodSummaryView](../../src/Compliance.Common/Features/Operations/ObservationPeriodSummaryView.cs#L6)
- [ControlCalendarView](../../src/Compliance.Common/Features/Operations/ControlCalendarView.cs#L6)
- [MissingOccurrenceView](../../src/Compliance.Common/Features/Operations/MissingOccurrenceView.cs#L6)
- [GovernanceReviewDueView](../../src/Compliance.Common/Features/Operations/GovernanceReviewDueView.cs#L6)
- [ManagementReviewView](../../src/Compliance.Common/Features/Operations/ManagementReviewView.cs#L6)
- [ManagementReviewActionView](../../src/Compliance.Common/Features/Operations/ManagementReviewActionView.cs#L6)

##### Changes

- [SignificantChangeView](../../src/Compliance.Common/Features/Changes/SignificantChangeView.cs#L6)
- [SignificantChangeSummaryView](../../src/Compliance.Common/Features/Changes/SignificantChangeSummaryView.cs#L6)

##### Incidents

- [ComplianceIncidentView](../../src/Compliance.Common/Features/Incidents/ComplianceIncidentView.cs#L6)
- [ComplianceIncidentSummaryView](../../src/Compliance.Common/Features/Incidents/ComplianceIncidentSummaryView.cs#L6)
- [ComplianceImpactAssessmentView](../../src/Compliance.Common/Features/Incidents/ComplianceImpactAssessmentView.cs#L6)

##### AuditPopulations

- [ObservationPeriodClosePreview](../../src/Compliance.Common/Features/AuditPopulations/ObservationPeriodClosePreview.cs#L6)
- [ObservationPeriodCloseView](../../src/Compliance.Common/Features/AuditPopulations/ObservationPeriodCloseView.cs#L6)
- [AuditPopulationDefinitionView](../../src/Compliance.Common/Features/AuditPopulations/AuditPopulationDefinitionView.cs#L6)
- [AuditPopulationView](../../src/Compliance.Common/Features/AuditPopulations/AuditPopulationView.cs#L6)
- [AuditPopulationRowView](../../src/Compliance.Common/Features/AuditPopulations/AuditPopulationRowView.cs#L6)
- [PopulationReconciliationView](../../src/Compliance.Common/Features/AuditPopulations/PopulationReconciliationView.cs#L6)

##### Programs

- [ProgramRolloverPreview](../../src/Compliance.Common/Features/Programs/ProgramRolloverPreview.cs#L6)
- [ProgramRolloverView](../../src/Compliance.Common/Features/Programs/ProgramRolloverView.cs#L6)
- [NextPeriodPlanView](../../src/Compliance.Common/Features/Programs/NextPeriodPlanView.cs#L6)
- [CarryForwardReferenceView](../../src/Compliance.Common/Features/Programs/CarryForwardReferenceView.cs#L6)

##### FirmPortfolio

- [AuthorizedClientSummaryView](../../src/Compliance.Common/Features/FirmPortfolio/AuthorizedClientSummaryView.cs#L6)
- [CrossClientWorkItemView](../../src/Compliance.Common/Features/FirmPortfolio/CrossClientWorkItemView.cs#L6)

##### MethodologyTemplates

- [MethodologyTemplateSummaryView](../../src/Compliance.Common/Features/MethodologyTemplates/MethodologyTemplateSummaryView.cs#L6)
- [MethodologyTemplateVersionView](../../src/Compliance.Common/Features/MethodologyTemplates/MethodologyTemplateVersionView.cs#L6)
- [MethodologyTemplateApplicationPreview](../../src/Compliance.Common/Features/MethodologyTemplates/MethodologyTemplateApplicationPreview.cs#L6)
- [TemplateApplicationView](../../src/Compliance.Common/Features/MethodologyTemplates/TemplateApplicationView.cs#L6)

##### AttestRecords

- [OwnAttestRecordPackageView](../../src/Compliance.Common/Features/AttestRecords/OwnAttestRecordPackageView.cs#L6)
- [OwnAttestRecordDeliveryView](../../src/Compliance.Common/Features/AttestRecords/OwnAttestRecordDeliveryView.cs#L6)
- [AttestDeliveryHoldView](../../src/Compliance.Common/Features/AttestRecords/AttestDeliveryHoldView.cs#L6)

### Existing HTTP APIs

These are literal MapPortia registrations in the host. Host-mode/authentication conditionals can limit availability. Middleware, health checks, raw authentication routes and MCP mappings are listed separately below. This is not a generated OpenAPI snapshot.

| Method | Route | Request | Source |
| --- | --- | --- | --- |
| POST | `/api/v1/developer-user-sessions` | `ContinueWithDeveloperIdentity` | [registration](../../src/Compliance.App/Program.cs#L454) |
| POST | `/api/v1/oidc-user-sessions` | `ContinueWithOidcProvider` | [registration](../../src/Compliance.App/Program.cs#L462) |
| POST | `/api/v1/my/oidc-identity-links` | `LinkOidcProviderIdentity` | [registration](../../src/Compliance.App/Program.cs#L467) |
| POST | `/api/v1/identity-recovery/challenges` | `StartIdentityRecovery` | [registration](../../src/Compliance.App/Program.cs#L471) |
| POST | `/api/v1/identity-recovery/options` | `GetIdentityRecoveryOptions` | [registration](../../src/Compliance.App/Program.cs#L474) |
| POST | `/api/v1/identity-recovery/completions` | `CompleteIdentityRecovery` | [registration](../../src/Compliance.App/Program.cs#L478) |
| POST | `/api/v1/tenants` | `RegisterTenant` | [registration](../../src/Compliance.App/Program.cs#L482) |
| POST | `/api/v1/tenants/{tenant_id}/suspensions` | `SuspendTenant` | [registration](../../src/Compliance.App/Program.cs#L485) |
| DELETE | `/api/v1/tenants/{tenant_id}/suspensions` | `ReactivateTenant` | [registration](../../src/Compliance.App/Program.cs#L488) |
| POST | `/api/v1/tenants/{tenant_id}/offboarding` | `OffboardTenant` | [registration](../../src/Compliance.App/Program.cs#L491) |
| POST | `/api/v1/platform/operator-grants` | `GrantPlatformOperator` | [registration](../../src/Compliance.App/Program.cs#L494) |
| POST | `/api/v1/platform/operator-revocations` | `RevokePlatformOperator` | [registration](../../src/Compliance.App/Program.cs#L497) |
| GET | `/api/v1/platform/operators` | `ListPlatformOperators` | [registration](../../src/Compliance.App/Program.cs#L500) |
| POST | `/api/v1/tenants/{tenant_id}/invitations` | `InviteTenantMember` | [registration](../../src/Compliance.App/Program.cs#L503) |
| POST | `/api/v1/tenants/{tenant_id}/member-invitations` | `InviteOrganizationMember` | [registration](../../src/Compliance.App/Program.cs#L506) |
| GET | `/api/v1/tenants/{tenant_id}/members/{user_id}/access` | `GetMemberAccess` | [registration](../../src/Compliance.App/Program.cs#L513) |
| POST | `/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions` | `SuspendMember` | [registration](../../src/Compliance.App/Program.cs#L517) |
| DELETE | `/api/v1/tenants/{tenant_id}/members/{user_id}/suspensions` | `ReinstateMember` | [registration](../../src/Compliance.App/Program.cs#L521) |
| POST | `/api/v1/tenants/{tenant_id}/members/{user_id}/deprovisioning` | `DeprovisionMember` | [registration](../../src/Compliance.App/Program.cs#L525) |
| GET | `/api/v1/tenants/{tenant_id}/members/{user_id}` | `GetTenantMember` | [registration](../../src/Compliance.App/Program.cs#L529) |
| POST | `/api/v1/tenants/{tenant_id}/invitations/acceptance` | `AcceptTenantInvitation` | [registration](../../src/Compliance.App/Program.cs#L537) |
| GET | `/api/v1/tenants/{tenant_id}` | `GetTenant` | [registration](../../src/Compliance.App/Program.cs#L540) |
| GET | `/api/v1/tenants/{tenant_id}/provisioning-status` | `GetTenantProvisioningStatus` | [registration](../../src/Compliance.App/Program.cs#L543) |
| POST | `/api/v1/tenants/{tenant_id}/slug-changes` | `ChangeTenantSlug` | [registration](../../src/Compliance.App/Program.cs#L553) |
| GET | `/api/v1/tenant-slugs/{slug}/mine` | `ResolveMyTenantSlug` | [registration](../../src/Compliance.App/Program.cs#L556) |
| POST | `/api/v1/tenants/{tenant_id}/programs` | `CreateProgram` | [registration](../../src/Compliance.App/Program.cs#L559) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}` | `GetCriteriaCatalogEdition` | [registration](../../src/Compliance.App/Program.cs#L566) |
| GET | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}` | `GetCriteriaCatalogEntry` | [registration](../../src/Compliance.App/Program.cs#L578) |
| PUT | `/api/v1/platform/firm-staff/{staff_member_id}` | `RegisterFirmStaff` | [registration](../../src/Compliance.App/Program.cs#L582) |
| PUT | `/api/v1/platform/firm-staff/{staff_member_id}/status` | `SetFirmStaffStatus` | [registration](../../src/Compliance.App/Program.cs#L586) |
| GET | `/api/v1/platform/firm-staff` | `GetFirmStaffDirectory` | [registration](../../src/Compliance.App/Program.cs#L590) |
| GET | `/api/v1/tenants/{tenant_id}/firm-staff` | `ListAssignableFirmStaff` | [registration](../../src/Compliance.App/Program.cs#L594) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}` | `CreateServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L598) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/draft` | `AmendServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L602) |
| PUT | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}` | `ProposeServiceEngagementStaff` | [registration](../../src/Compliance.App/Program.cs#L606) |
| POST | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/staff-proposals/{staff_member_id}/withdrawals` | `WithdrawServiceEngagementStaffProposal` | [registration](../../src/Compliance.App/Program.cs#L610) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/acceptance` | `GetServiceEngagementAcceptance` | [registration](../../src/Compliance.App/Program.cs#L614) |
| POST | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/management-acknowledgements/{acknowledgement_id}` | `AcknowledgeEngagementManagement` | [registration](../../src/Compliance.App/Program.cs#L622) |
| POST | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}/closure` | `CloseServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L630) |
| GET | `/api/v1/tenants/{tenant_id}/service-engagements/{engagement_id}` | `GetServiceEngagement` | [registration](../../src/Compliance.App/Program.cs#L634) |
| PUT | `/api/v1/platform/independence/rules` | `ReviseIndependenceRules` | [registration](../../src/Compliance.App/Program.cs#L654) |
| PUT | `/api/v1/tenants/{tenant_id}/independence/services/{service_record_id}` | `RecordNonattestService` | [registration](../../src/Compliance.App/Program.cs#L658) |
| PUT | `/api/v1/tenants/{tenant_id}/independence/evaluations/{evaluation_id}` | `EvaluateClientIndependence` | [registration](../../src/Compliance.App/Program.cs#L662) |
| GET | `/api/v1/tenants/{tenant_id}/independence/history` | `GetClientIndependenceHistory` | [registration](../../src/Compliance.App/Program.cs#L666) |
| PUT | `/api/v1/tenants/{tenant_id}/criteria-editions/{edition_id}/entries/{identifier}/overlay` | `SetCriteriaTextOverlay` | [registration](../../src/Compliance.App/Program.cs#L670) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criteria-edition` | `SelectProgramCriteriaEdition` | [registration](../../src/Compliance.App/Program.cs#L674) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}` | `ReviseProgram` | [registration](../../src/Compliance.App/Program.cs#L678) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}` | `GetProgram` | [registration](../../src/Compliance.App/Program.cs#L681) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/revisions/{revision}` | `GetProgramRevision` | [registration](../../src/Compliance.App/Program.cs#L691) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/setup-work` | `GetProgramSetupWork` | [registration](../../src/Compliance.App/Program.cs#L695) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls` | `CreateControlDraft` | [registration](../../src/Compliance.App/Program.cs#L699) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft` | `ReviseControlDraft` | [registration](../../src/Compliance.App/Program.cs#L703) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/discards` | `DiscardControlDraft` | [registration](../../src/Compliance.App/Program.cs#L707) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft` | `GetControlDraft` | [registration](../../src/Compliance.App/Program.cs#L711) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/revisions/{revision}` | `GetControlDraftRevision` | [registration](../../src/Compliance.App/Program.cs#L723) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/reviews` | `ReviewControl` | [registration](../../src/Compliance.App/Program.cs#L728) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/approvals` | `ApproveControl` | [registration](../../src/Compliance.App/Program.cs#L732) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/current-version` | `GetCurrentControlVersion` | [registration](../../src/Compliance.App/Program.cs#L736) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/effective-version` | `GetEffectiveControlVersion` | [registration](../../src/Compliance.App/Program.cs#L740) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/versions/{version_id}` | `GetControlVersion` | [registration](../../src/Compliance.App/Program.cs#L748) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/decisions/{decision_id}` | `GetControlDecision` | [registration](../../src/Compliance.App/Program.cs#L756) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings` | `ProposeControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L761) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/reviews` | `ReviewControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L765) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}/retirements` | `RetireControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L769) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/control-mappings/{mapping_id}` | `GetControlCriterionMapping` | [registration](../../src/Compliance.App/Program.cs#L773) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/successors` | `ProposeControlSuccessor` | [registration](../../src/Compliance.App/Program.cs#L785) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirement-proposals` | `ProposeControlRetirement` | [registration](../../src/Compliance.App/Program.cs#L789) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/impact-preview` | `PreviewControlImpact` | [registration](../../src/Compliance.App/Program.cs#L793) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/retirements` | `RetireControl` | [registration](../../src/Compliance.App/Program.cs#L798) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/proposal-withdrawals` | `WithdrawControlProposal` | [registration](../../src/Compliance.App/Program.cs#L803) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person` | `DesignateControlOwnerPerson` | [registration](../../src/Compliance.App/Program.cs#L807) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/draft/owner-person` | `GetControlDraftOwnerPerson` | [registration](../../src/Compliance.App/Program.cs#L811) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability` | `ProposeCriterionNotApplicable` | [registration](../../src/Compliance.App/Program.cs#L816) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/reviews` | `ReviewCriterionApplicability` | [registration](../../src/Compliance.App/Program.cs#L820) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}/withdrawals` | `WithdrawCriterionNotApplicable` | [registration](../../src/Compliance.App/Program.cs#L824) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/criterion-applicability/{decision_id}` | `GetCriterionApplicability` | [registration](../../src/Compliance.App/Program.cs#L828) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts` | `CreateCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L836) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}` | `ReviseCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L840) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}` | `GetCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L844) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/revisions/{revision}` | `GetCommitmentDraftRevision` | [registration](../../src/Compliance.App/Program.cs#L856) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/impact-preview` | `PreviewCommitmentImpact` | [registration](../../src/Compliance.App/Program.cs#L860) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/reviews` | `ReviewCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L864) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/approvals` | `ApproveCommitmentDraft` | [registration](../../src/Compliance.App/Program.cs#L869) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/versions/{version}` | `GetCommitmentVersion` | [registration](../../src/Compliance.App/Program.cs#L881) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/commitment-drafts/{draft_id}/effective-version` | `GetEffectiveCommitmentVersion` | [registration](../../src/Compliance.App/Program.cs#L885) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies` | `CreatePolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L889) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}` | `GetPolicy` | [registration](../../src/Compliance.App/Program.cs#L897) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft` | `RevisePolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L901) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/draft/discards` | `DiscardPolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L905) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/successors` | `ProposePolicySuccessor` | [registration](../../src/Compliance.App/Program.cs#L909) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/impact-preview` | `PreviewPolicyImpact` | [registration](../../src/Compliance.App/Program.cs#L913) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/reviews` | `ReviewPolicyDraft` | [registration](../../src/Compliance.App/Program.cs#L918) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/approvals` | `ApprovePolicy` | [registration](../../src/Compliance.App/Program.cs#L922) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/periodic-reviews` | `ConfirmPolicyReview` | [registration](../../src/Compliance.App/Program.cs#L926) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirement-proposals` | `ProposePolicyRetirement` | [registration](../../src/Compliance.App/Program.cs#L930) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/retirements` | `ApprovePolicyRetirement` | [registration](../../src/Compliance.App/Program.cs#L934) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/versions/{version}` | `GetPolicyVersion` | [registration](../../src/Compliance.App/Program.cs#L942) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policies/{policy_id}/effective-version` | `GetEffectivePolicyVersion` | [registration](../../src/Compliance.App/Program.cs#L946) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements` | `DefineTrainingRequirement` | [registration](../../src/Compliance.App/Program.cs#L954) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}` | `GetTrainingRequirement` | [registration](../../src/Compliance.App/Program.cs#L962) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-requirements/{requirement_id}` | `ReviseTrainingRequirement` | [registration](../../src/Compliance.App/Program.cs#L966) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/policy-campaigns` | `LaunchPolicyCampaign` | [registration](../../src/Compliance.App/Program.cs#L970) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/training-campaigns` | `LaunchTrainingCampaign` | [registration](../../src/Compliance.App/Program.cs#L974) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}` | `GetCampaign` | [registration](../../src/Compliance.App/Program.cs#L982) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/reconciliations` | `ReconcileCampaignAudience` | [registration](../../src/Compliance.App/Program.cs#L994) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/completions` | `RecordTrainingCompletion` | [registration](../../src/Compliance.App/Program.cs#L998) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/closure` | `CloseCampaign` | [registration](../../src/Compliance.App/Program.cs#L1002) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/acknowledgements` | `AcknowledgePolicy` | [registration](../../src/Compliance.App/Program.cs#L1007) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/campaigns/{campaign_id}/waivers` | `ApproveCampaignWaiver` | [registration](../../src/Compliance.App/Program.cs#L1011) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks` | `CreateRiskDraft` | [registration](../../src/Compliance.App/Program.cs#L1015) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft` | `ReviseRiskDraft` | [registration](../../src/Compliance.App/Program.cs#L1019) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft` | `GetRiskDraft` | [registration](../../src/Compliance.App/Program.cs#L1023) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/draft/revisions/{revision}` | `GetRiskDraftRevision` | [registration](../../src/Compliance.App/Program.cs#L1035) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions` | `PublishRiskMethodVersion` | [registration](../../src/Compliance.App/Program.cs#L1039) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method` | `GetRiskMethod` | [registration](../../src/Compliance.App/Program.cs#L1043) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risk-method/versions/{version}` | `GetRiskMethodVersion` | [registration](../../src/Compliance.App/Program.cs#L1047) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/assessments` | `RecordRiskAssessment` | [registration](../../src/Compliance.App/Program.cs#L1051) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment` | `ChooseRiskTreatment` | [registration](../../src/Compliance.App/Program.cs#L1055) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/acceptances` | `AcceptRisk` | [registration](../../src/Compliance.App/Program.cs#L1059) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/evaluation` | `GetRiskEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1063) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/owner` | `AssignRiskOwner` | [registration](../../src/Compliance.App/Program.cs#L1072) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments` | `ProposeRiskControlTreatment` | [registration](../../src/Compliance.App/Program.cs#L1076) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/reviews` | `ReviewRiskControlTreatment` | [registration](../../src/Compliance.App/Program.cs#L1080) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/control-treatments/{treatment_id}/retirements` | `RetireRiskControlTreatment` | [registration](../../src/Compliance.App/Program.cs#L1084) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions` | `AddRiskTreatmentAction` | [registration](../../src/Compliance.App/Program.cs#L1088) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}` | `ReviseRiskTreatmentAction` | [registration](../../src/Compliance.App/Program.cs#L1092) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/cancellations` | `CancelRiskTreatmentAction` | [registration](../../src/Compliance.App/Program.cs#L1096) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completions` | `SubmitRiskTreatmentActionCompletion` | [registration](../../src/Compliance.App/Program.cs#L1100) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/treatment-actions/{action_id}/completion-reviews` | `ReviewRiskTreatmentActionCompletion` | [registration](../../src/Compliance.App/Program.cs#L1105) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/risks/{risk_id}/governance` | `GetRiskGovernance` | [registration](../../src/Compliance.App/Program.cs#L1109) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments` | `RunReadinessAssessment` | [registration](../../src/Compliance.App/Program.cs#L1113) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}` | `GetReadinessAssessment` | [registration](../../src/Compliance.App/Program.cs#L1121) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/decision` | `DecideReadiness` | [registration](../../src/Compliance.App/Program.cs#L1129) |
| PUT | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/gaps/{gap_id}/plan` | `PlanReadinessGap` | [registration](../../src/Compliance.App/Program.cs#L1133) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/type-i-entry-decision` | `DecideTypeIEntry` | [registration](../../src/Compliance.App/Program.cs#L1139) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/readiness/assessments/{assessment_id}/gaps/{gap_id}/annotations` | `AnnotateReadinessGap` | [registration](../../src/Compliance.App/Program.cs#L1147) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals` | `ProposeControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1155) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/preview` | `PreviewControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1159) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan/proposals/{plan_version_id}/approvals` | `ApproveControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1163) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/operating-plan` | `GetControlOperatingPlan` | [registration](../../src/Compliance.App/Program.cs#L1167) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/my-work` | `ListMyControlWork` | [registration](../../src/Compliance.App/Program.cs#L1175) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences` | `OpenControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1179) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}` | `GetControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1187) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/attestations` | `AttestControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1191) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/corrections` | `CorrectControlAttestation` | [registration](../../src/Compliance.App/Program.cs#L1195) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/occurrences/{occurrence_id}/reviews` | `ReviewControlOccurrence` | [registration](../../src/Compliance.App/Program.cs#L1199) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations` | `StartControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1203) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions` | `DefineControlEvaluationPlan` | [registration](../../src/Compliance.App/Program.cs#L1207) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluation-plan/versions/{plan_version_id}` | `GetControlEvaluationPlanVersion` | [registration](../../src/Compliance.App/Program.cs#L1216) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}` | `GetControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1224) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/steps/{step_id}/results` | `RecordControlEvaluationStep` | [registration](../../src/Compliance.App/Program.cs#L1228) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/deviations/{deviation_id}/dispositions` | `DisposeControlEvaluationDeviation` | [registration](../../src/Compliance.App/Program.cs#L1232) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/submissions` | `SubmitControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1236) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/controls/{control_id}/evaluations/{evaluation_id}/reviews` | `ReviewControlEvaluation` | [registration](../../src/Compliance.App/Program.cs#L1240) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests` | `OpenEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1244) |
| POST | `/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}/preparations` | `PrepareEvidenceRedaction` | [registration](../../src/Compliance.App/Program.cs#L1252) |
| POST | `/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}/approvals` | `ApproveEvidenceRedaction` | [registration](../../src/Compliance.App/Program.cs#L1256) |
| GET | `/api/v1/tenants/{tenant_id}/evidence-redactions/{redaction_id}` | `GetEvidenceRedaction` | [registration](../../src/Compliance.App/Program.cs#L1260) |
| GET | `/api/v1/tenants/{tenant_id}/evidence-artifacts/{artifact_id}/metadata` | `GetEvidenceArtifactMetadata` | [registration](../../src/Compliance.App/Program.cs#L1264) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}` | `GetEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1268) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/fulfilments` | `FulfilEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1272) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/evidence-requests/{evidence_request_id}/cancellations` | `CancelEvidenceRequest` | [registration](../../src/Compliance.App/Program.cs#L1276) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings` | `RaiseFinding` | [registration](../../src/Compliance.App/Program.cs#L1280) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}` | `GetFinding` | [registration](../../src/Compliance.App/Program.cs#L1288) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/revisions` | `ReviseFinding` | [registration](../../src/Compliance.App/Program.cs#L1292) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions` | `AddCorrectiveAction` | [registration](../../src/Compliance.App/Program.cs#L1296) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/corrective-actions/{action_id}/completions` | `CompleteCorrectiveAction` | [registration](../../src/Compliance.App/Program.cs#L1300) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/acceptances` | `LinkFindingAcceptance` | [registration](../../src/Compliance.App/Program.cs#L1304) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/closures` | `CloseFinding` | [registration](../../src/Compliance.App/Program.cs#L1308) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/findings/{finding_id}/reopenings` | `ReopenFinding` | [registration](../../src/Compliance.App/Program.cs#L1312) |
| POST | `/api/v1/tenants/{tenant_id}/access-populations` | `OpenAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1316) |
| PUT | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/facts` | `RecordAccessPopulationFacts` | [registration](../../src/Compliance.App/Program.cs#L1320) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/preview` | `PreviewAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1324) |
| POST | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/acceptance` | `AcceptAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1328) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}` | `GetAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1332) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-population-exceptions` | `ExemptMissingAccessPopulation` | [registration](../../src/Compliance.App/Program.cs#L1340) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/access-review-coverage` | `GetAccessReviewCoverage` | [registration](../../src/Compliance.App/Program.cs#L1344) |
| POST | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/classifications` | `ClassifyAccessPrincipal` | [registration](../../src/Compliance.App/Program.cs#L1348) |
| GET | `/api/v1/tenants/{tenant_id}/access-populations/{population_id}/variance` | `GetAccessVariance` | [registration](../../src/Compliance.App/Program.cs#L1356) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-expectations` | `ProposeAccessExpectation` | [registration](../../src/Compliance.App/Program.cs#L1360) |
| POST | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/approval` | `ApproveAccessExpectation` | [registration](../../src/Compliance.App/Program.cs#L1364) |
| POST | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations/{expectation_id}/exceptions` | `ExemptAccessExpectation` | [registration](../../src/Compliance.App/Program.cs#L1368) |
| GET | `/api/v1/tenants/{tenant_id}/system-instances/{system_instance_id}/access-expectations` | `ListAccessExpectations` | [registration](../../src/Compliance.App/Program.cs#L1372) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns` | `LaunchAccessReviewCampaign` | [registration](../../src/Compliance.App/Program.cs#L1376) |
| GET | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}` | `GetAccessReviewCampaign` | [registration](../../src/Compliance.App/Program.cs#L1384) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/decisions` | `RecordAccessDecision` | [registration](../../src/Compliance.App/Program.cs#L1388) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decision-previews` | `PreviewBulkAccessDecision` | [registration](../../src/Compliance.App/Program.cs#L1392) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/bulk-decisions` | `RecordBulkAccessDecision` | [registration](../../src/Compliance.App/Program.cs#L1396) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-changes` | `RecordAccessRemediationChange` | [registration](../../src/Compliance.App/Program.cs#L1400) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-verifications` | `VerifyAccessRemediation` | [registration](../../src/Compliance.App/Program.cs#L1404) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/items/{item_id}/remediation-exceptions` | `ExemptAccessRemediation` | [registration](../../src/Compliance.App/Program.cs#L1408) |
| POST | `/api/v1/tenants/{tenant_id}/access-review-campaigns/{campaign_id}/completion` | `CompleteAccessReviewCampaign` | [registration](../../src/Compliance.App/Program.cs#L1412) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work` | `ListWork` | [registration](../../src/Compliance.App/Program.cs#L1416) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/digest` | `GetWorkDigest` | [registration](../../src/Compliance.App/Program.cs#L1424) |
| GET | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}` | `GetWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1428) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/claims` | `ClaimWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1432) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/assignments` | `AssignWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1436) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/delegations` | `DelegateWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1440) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/work/{work_item_id}/escalations` | `EscalateWorkItem` | [registration](../../src/Compliance.App/Program.cs#L1444) |
| GET | `/api/v1/tenants/{tenant_id}/work-digest-preference` | `GetWorkDigestPreference` | [registration](../../src/Compliance.App/Program.cs#L1448) |
| PUT | `/api/v1/tenants/{tenant_id}/work-digest-preference` | `SetWorkDigestPreference` | [registration](../../src/Compliance.App/Program.cs#L1452) |
| POST | `/api/v1/tenants/{tenant_id}/people` | `RecordPerson` | [registration](../../src/Compliance.App/Program.cs#L1456) |
| PUT | `/api/v1/tenants/{tenant_id}/people/{person_id}` | `RevisePerson` | [registration](../../src/Compliance.App/Program.cs#L1460) |
| PUT | `/api/v1/tenants/{tenant_id}/people/{person_id}/membership-correlation` | `CorrelatePersonMembership` | [registration](../../src/Compliance.App/Program.cs#L1464) |
| GET | `/api/v1/tenants/{tenant_id}/people/{person_id}` | `GetPerson` | [registration](../../src/Compliance.App/Program.cs#L1468) |
| POST | `/api/v1/tenants/{tenant_id}/work-relationships` | `RecordWorkRelationship` | [registration](../../src/Compliance.App/Program.cs#L1476) |
| PUT | `/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}` | `ReviseWorkRelationship` | [registration](../../src/Compliance.App/Program.cs#L1480) |
| GET | `/api/v1/tenants/{tenant_id}/work-relationships/{relationship_id}` | `GetWorkRelationship` | [registration](../../src/Compliance.App/Program.cs#L1484) |
| PUT | `/api/v1/tenants/{tenant_id}/workforce-observations/{observation_id}/resolution` | `ResolveWorkforceObservation` | [registration](../../src/Compliance.App/Program.cs#L1496) |
| POST | `/api/v1/tenants/{tenant_id}/workforce-source-observations` | `RecordWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1500) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}` | `GetWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1504) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/preview` | `PreviewWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1512) |
| PUT | `/api/v1/tenants/{tenant_id}/workforce-source-observations/{observation_id}/decision` | `ReconcileWorkforceSourceObservation` | [registration](../../src/Compliance.App/Program.cs#L1516) |
| POST | `/api/v1/tenants/{tenant_id}/service-identities` | `RecordServiceIdentity` | [registration](../../src/Compliance.App/Program.cs#L1525) |
| PUT | `/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}` | `ReviseServiceIdentity` | [registration](../../src/Compliance.App/Program.cs#L1529) |
| GET | `/api/v1/tenants/{tenant_id}/service-identities/{service_identity_id}` | `GetServiceIdentity` | [registration](../../src/Compliance.App/Program.cs#L1533) |
| POST | `/api/v1/tenants/{tenant_id}/providers` | `RecordProvider` | [registration](../../src/Compliance.App/Program.cs#L1541) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}` | `ReviseProvider` | [registration](../../src/Compliance.App/Program.cs#L1544) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}` | `GetProvider` | [registration](../../src/Compliance.App/Program.cs#L1547) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/revisions/{revision}` | `GetProviderRevision` | [registration](../../src/Compliance.App/Program.cs#L1553) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports` | `RecordAssuranceReport` | [registration](../../src/Compliance.App/Program.cs#L1559) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-reports/{report_id}` | `ReviseAssuranceReport` | [registration](../../src/Compliance.App/Program.cs#L1563) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/reviews` | `RecordProviderReview` | [registration](../../src/Compliance.App/Program.cs#L1572) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps` | `RecordProviderCoverageGap` | [registration](../../src/Compliance.App/Program.cs#L1580) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}` | `CloseProviderCoverageGap` | [registration](../../src/Compliance.App/Program.cs#L1585) |
| PUT | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}/risk-acceptances` | `LinkProviderCoverageGapRiskAcceptance` | [registration](../../src/Compliance.App/Program.cs#L1590) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/coverage-gaps/{gap_id}` | `GetProviderCoverageGap` | [registration](../../src/Compliance.App/Program.cs#L1594) |
| GET | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/assurance-coverage` | `GetProviderAssuranceCoverage` | [registration](../../src/Compliance.App/Program.cs#L1602) |
| POST | `/api/v1/tenants/{tenant_id}/providers/{provider_id}/change-impact-previews` | `PreviewProviderChange` | [registration](../../src/Compliance.App/Program.cs#L1606) |
| POST | `/api/v1/tenants/{tenant_id}/technology-components` | `RecordTechnologyComponent` | [registration](../../src/Compliance.App/Program.cs#L1611) |
| PUT | `/api/v1/tenants/{tenant_id}/technology-components/{component_id}` | `ReviseTechnologyComponent` | [registration](../../src/Compliance.App/Program.cs#L1615) |
| GET | `/api/v1/tenants/{tenant_id}/technology-components/{component_id}` | `GetTechnologyComponent` | [registration](../../src/Compliance.App/Program.cs#L1631) |
| POST | `/api/v1/tenants/{tenant_id}/information-assets` | `RecordInformationAsset` | [registration](../../src/Compliance.App/Program.cs#L1643) |
| PUT | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}` | `ReviseInformationAsset` | [registration](../../src/Compliance.App/Program.cs#L1647) |
| GET | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}` | `GetInformationAsset` | [registration](../../src/Compliance.App/Program.cs#L1651) |
| POST | `/api/v1/tenants/{tenant_id}/information-assets/{information_asset_id}/change-previews` | `PreviewInformationAssetChange` | [registration](../../src/Compliance.App/Program.cs#L1663) |
| POST | `/api/v1/tenants/{tenant_id}/locations` | `RecordLocation` | [registration](../../src/Compliance.App/Program.cs#L1668) |
| PUT | `/api/v1/tenants/{tenant_id}/locations/{location_id}` | `ReviseLocation` | [registration](../../src/Compliance.App/Program.cs#L1672) |
| GET | `/api/v1/tenants/{tenant_id}/locations/{location_id}` | `GetLocation` | [registration](../../src/Compliance.App/Program.cs#L1676) |
| POST | `/api/v1/tenants/{tenant_id}/operational-processes` | `RecordOperationalProcess` | [registration](../../src/Compliance.App/Program.cs#L1688) |
| PUT | `/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}` | `ReviseOperationalProcess` | [registration](../../src/Compliance.App/Program.cs#L1692) |
| GET | `/api/v1/tenants/{tenant_id}/operational-processes/{operational_process_id}` | `GetOperationalProcess` | [registration](../../src/Compliance.App/Program.cs#L1696) |
| POST | `/api/v1/tenants/{tenant_id}/data-flows` | `RecordDataFlow` | [registration](../../src/Compliance.App/Program.cs#L1708) |
| PUT | `/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}` | `ReviseDataFlow` | [registration](../../src/Compliance.App/Program.cs#L1712) |
| GET | `/api/v1/tenants/{tenant_id}/data-flows/{data_flow_id}` | `GetDataFlow` | [registration](../../src/Compliance.App/Program.cs#L1716) |
| POST | `/api/v1/tenants/{tenant_id}/applications` | `DeclareApplication` | [registration](../../src/Compliance.App/Program.cs#L1728) |
| PUT | `/api/v1/tenants/{tenant_id}/applications/{application_id}` | `ReviseApplication` | [registration](../../src/Compliance.App/Program.cs#L1732) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}` | `GetApplication` | [registration](../../src/Compliance.App/Program.cs#L1741) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}` | `AddApplicationRelationship` | [registration](../../src/Compliance.App/Program.cs#L1749) |
| DELETE | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships/{relationship_id}` | `RemoveApplicationRelationship` | [registration](../../src/Compliance.App/Program.cs#L1757) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/relationships` | `ListApplicationRelationships` | [registration](../../src/Compliance.App/Program.cs#L1765) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/revisions/{revision}` | `GetApplicationRevision` | [registration](../../src/Compliance.App/Program.cs#L1780) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances` | `DeclareSystemInstance` | [registration](../../src/Compliance.App/Program.cs#L1788) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}` | `GetSystemInstance` | [registration](../../src/Compliance.App/Program.cs#L1792) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/change-previews` | `PreviewApplicationChange` | [registration](../../src/Compliance.App/Program.cs#L1812) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/retirements` | `RetireApplication` | [registration](../../src/Compliance.App/Program.cs#L1822) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/retirements` | `RetireSystemInstance` | [registration](../../src/Compliance.App/Program.cs#L1827) |
| POST | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope-decisions` | `DecideAccessReviewScope` | [registration](../../src/Compliance.App/Program.cs#L1832) |
| GET | `/api/v1/tenants/{tenant_id}/applications/{application_id}/system-instances/{system_instance_id}/access-review-scope` | `GetAccessReviewScope` | [registration](../../src/Compliance.App/Program.cs#L1837) |
| GET | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}` | `GetArtifactRetention` | [registration](../../src/Compliance.App/Program.cs#L1847) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/retention` | `GetApplicationImportRetention` | [registration](../../src/Compliance.App/Program.cs#L1855) |
| POST | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/basis` | `RecordArtifactRetentionBasis` | [registration](../../src/Compliance.App/Program.cs#L1859) |
| POST | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds` | `PlaceArtifactLegalHold` | [registration](../../src/Compliance.App/Program.cs#L1863) |
| POST | `/api/v1/tenants/{tenant_id}/artifact-retention/{source_kind}/{source_id}/legal-holds/{hold_id}/releases` | `ReleaseArtifactLegalHold` | [registration](../../src/Compliance.App/Program.cs#L1867) |
| POST | `/api/v1/tenants/{tenant_id}/application-imports` | `StageApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L1871) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/progress` | `GetApplicationImportProgress` | [registration](../../src/Compliance.App/Program.cs#L1875) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rejected-report` | `GetApplicationImportRejectedReport` | [registration](../../src/Compliance.App/Program.cs#L1880) |
| POST | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/acceptances` | `AcceptApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L1885) |
| POST | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/cancellations` | `CancelApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L1890) |
| POST | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/rows/{row_id}/correlations` | `CorrelateApplicationImportRow` | [registration](../../src/Compliance.App/Program.cs#L1894) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}` | `GetApplicationImport` | [registration](../../src/Compliance.App/Program.cs#L1899) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal/preview` | `PreviewApplicationImportOmissionProposal` | [registration](../../src/Compliance.App/Program.cs#L1916) |
| POST | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal` | `FreezeApplicationImportOmissionProposal` | [registration](../../src/Compliance.App/Program.cs#L1920) |
| GET | `/api/v1/tenants/{tenant_id}/application-imports/{batch_id}/omission-proposal` | `GetApplicationImportOmissionProposal` | [registration](../../src/Compliance.App/Program.cs#L1925) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/client-services` | `CreateClientService` | [registration](../../src/Compliance.App/Program.cs#L1929) |
| PUT | `/api/v1/tenants/{tenant_id}/client-services/{service_id}` | `ReviseClientService` | [registration](../../src/Compliance.App/Program.cs#L1937) |
| POST | `/api/v1/tenants/{tenant_id}/client-services/{service_id}/retirements` | `RetireClientService` | [registration](../../src/Compliance.App/Program.cs#L1941) |
| GET | `/api/v1/tenants/{tenant_id}/client-services/{service_id}` | `GetClientService` | [registration](../../src/Compliance.App/Program.cs#L1945) |
| GET | `/api/v1/tenants/{tenant_id}/client-services/{service_id}/revisions/{revision}` | `GetClientServiceRevision` | [registration](../../src/Compliance.App/Program.cs#L1957) |
| POST | `/api/v1/tenants/{tenant_id}/programs/{program_id}/boundaries` | `CreateBoundary` | [registration](../../src/Compliance.App/Program.cs#L1961) |
| PUT | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}` | `ReviseBoundaryDraft` | [registration](../../src/Compliance.App/Program.cs#L1969) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/discards` | `DiscardBoundaryDraft` | [registration](../../src/Compliance.App/Program.cs#L1973) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}` | `GetBoundary` | [registration](../../src/Compliance.App/Program.cs#L1977) |
| POST | `/api/v1/tenants/{tenant_id}/responsibilities` | `AssignResponsibility` | [registration](../../src/Compliance.App/Program.cs#L1981) |
| POST | `/api/v1/tenants/{tenant_id}/responsibilities/{assignment_id}/revocation` | `RevokeResponsibility` | [registration](../../src/Compliance.App/Program.cs#L1985) |
| GET | `/api/v1/tenants/{tenant_id}/responsibilities` | `ListResponsibilities` | [registration](../../src/Compliance.App/Program.cs#L1989) |
| GET | `/api/v1/tenants/{tenant_id}/responsibilities/conflict-preview` | `PreviewResponsibilityConflicts` | [registration](../../src/Compliance.App/Program.cs#L1993) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/versions/{version_id}` | `GetBoundaryVersion` | [registration](../../src/Compliance.App/Program.cs#L1998) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/effective-version` | `GetEffectiveBoundaryVersion` | [registration](../../src/Compliance.App/Program.cs#L2006) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/decisions/{decision_id}` | `GetBoundaryDecision` | [registration](../../src/Compliance.App/Program.cs#L2010) |
| GET | `/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}` | `GetSeparationOfDutiesWaiver` | [registration](../../src/Compliance.App/Program.cs#L2018) |
| POST | `/api/v1/tenants/{tenant_id}/scope-snapshots` | `FreezeProgramScopeSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2022) |
| POST | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/amendments` | `AmendProgramScopeSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2026) |
| GET | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}` | `GetSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2030) |
| GET | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/verification` | `VerifyProgramScopeSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2034) |
| GET | `/api/v1/tenants/{tenant_id}/scope-snapshots/{snapshot_id}/manifest-regeneration` | `RegenerateProgramScopeSnapshotManifest` | [registration](../../src/Compliance.App/Program.cs#L2038) |
| POST | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots` | `FreezeWorkforceRosterSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2047) |
| POST | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/amendments` | `AmendWorkforceRosterSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2051) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}` | `GetWorkforceRosterSnapshot` | [registration](../../src/Compliance.App/Program.cs#L2055) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshots/{snapshot_id}/manifest-regeneration` | `RegenerateWorkforceRosterSnapshotManifest` | [registration](../../src/Compliance.App/Program.cs#L2063) |
| GET | `/api/v1/tenants/{tenant_id}/workforce-roster-snapshot-as-of` | `GetWorkforceRosterSnapshotAsOf` | [registration](../../src/Compliance.App/Program.cs#L2067) |
| GET | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/impact-preview` | `PreviewBoundaryImpact` | [registration](../../src/Compliance.App/Program.cs#L2071) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/reviews` | `ReviewBoundary` | [registration](../../src/Compliance.App/Program.cs#L2075) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/drafts/{draft_version_id}/approvals` | `ApproveBoundary` | [registration](../../src/Compliance.App/Program.cs#L2079) |
| POST | `/api/v1/tenants/{tenant_id}/separation-of-duties-waivers` | `RecordSeparationOfDutiesWaiver` | [registration](../../src/Compliance.App/Program.cs#L2083) |
| POST | `/api/v1/tenants/{tenant_id}/separation-of-duties-waivers/{waiver_id}/approvals` | `ApproveSeparationOfDutiesWaiver` | [registration](../../src/Compliance.App/Program.cs#L2087) |
| POST | `/api/v1/tenants/{tenant_id}/boundaries/{boundary_id}/successors` | `ProposeBoundarySuccessor` | [registration](../../src/Compliance.App/Program.cs#L2091) |
| POST | `/api/v1/users/{user_id}/email-addresses/{email_address}` | `ReserveEmail` | [registration](../../src/Compliance.App/Program.cs#L2095) |
| POST | `/api/v1/users/{user_id}/email-addresses/{email_address}/challenges` | `IssueEmailChallenge` | [registration](../../src/Compliance.App/Program.cs#L2098) |
| GET | `/api/v1/users/{user_id}/email-addresses/{email_address}/challenges/status` | `GetEmailChallengeStatus` | [registration](../../src/Compliance.App/Program.cs#L2101) |
| POST | `/api/v1/users/{user_id}/email-addresses/{email_address}/verifications` | `CompleteEmailChallenge` | [registration](../../src/Compliance.App/Program.cs#L2105) |
| GET | `/api/v1/users/{user_id}/email-addresses/{email_address}` | `GetEmailAddress` | [registration](../../src/Compliance.App/Program.cs#L2108) |
| POST | `/api/v1/tenants/{tenant_id}/teams/{team_id}` | `DefineTeam` | [registration](../../src/Compliance.App/Program.cs#L2117) |
| DELETE | `/api/v1/tenants/{tenant_id}/teams/{team_id}` | `DeleteTeam` | [registration](../../src/Compliance.App/Program.cs#L2120) |
| GET | `/api/v1/tenants/{tenant_id}/teams/{team_id}` | `GetTeam` | [registration](../../src/Compliance.App/Program.cs#L2123) |
| POST | `/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}` | `AssignTeamMember` | [registration](../../src/Compliance.App/Program.cs#L2129) |
| DELETE | `/api/v1/tenants/{tenant_id}/teams/{team_id}/members/{member_id}` | `RemoveTeamMember` | [registration](../../src/Compliance.App/Program.cs#L2132) |
| POST | `/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}` | `AssignTeamRole` | [registration](../../src/Compliance.App/Program.cs#L2138) |
| DELETE | `/api/v1/tenants/{tenant_id}/teams/{team_id}/roles/{role_id}` | `RemoveTeamRole` | [registration](../../src/Compliance.App/Program.cs#L2141) |
| POST | `/api/v1/tenants/{tenant_id}/roles/{role_id}` | `DefineRole` | [registration](../../src/Compliance.App/Program.cs#L2144) |
| DELETE | `/api/v1/tenants/{tenant_id}/roles/{role_id}` | `DeleteRole` | [registration](../../src/Compliance.App/Program.cs#L2147) |
| GET | `/api/v1/tenants/{tenant_id}/roles/{role_id}` | `GetRole` | [registration](../../src/Compliance.App/Program.cs#L2150) |
| POST | `/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}` | `AssignRolePermission` | [registration](../../src/Compliance.App/Program.cs#L2156) |
| DELETE | `/api/v1/tenants/{tenant_id}/roles/{role_id}/permissions/{permission}` | `RemoveRolePermission` | [registration](../../src/Compliance.App/Program.cs#L2159) |
| POST | `/api/v1/tenants/{tenant_id}/access-grants/{grant_id}` | `GrantAccess` | [registration](../../src/Compliance.App/Program.cs#L2172) |
| DELETE | `/api/v1/tenants/{tenant_id}/access-grants/{grant_id}` | `RevokeAccessGrant` | [registration](../../src/Compliance.App/Program.cs#L2175) |
| GET | `/api/v1/tenants/{tenant_id}/access-grants` | `ListAccessGrants` | [registration](../../src/Compliance.App/Program.cs#L2178) |

### Additional transport and MCP registrations

These source lines identify raw routes, health routes and MCP configuration. Follow their surrounding code for prefixes, handlers, allowlists and authentication. ICallable markers and discriminators in the contract inventory alone do not prove a tool is exposed.

- [Program.cs:45](../../src/Compliance.App/Program.cs#L45): `.AddMcpTool<DefineTeam>(tool => tool.Idempotent())`
- [Program.cs:46](../../src/Compliance.App/Program.cs#L46): `.AddMcpTool<DeleteTeam>(tool => tool.Destructive())`
- [Program.cs:47](../../src/Compliance.App/Program.cs#L47): `.AddMcpTool<GetTeam>(tool => tool.ReadOnly())`
- [Program.cs:48](../../src/Compliance.App/Program.cs#L48): `.AddMcpTool<ListTeams>(tool => tool.ReadOnly())`
- [Program.cs:49](../../src/Compliance.App/Program.cs#L49): `.AddMcpTool<AssignTeamMember>(tool => tool.Idempotent())`
- [Program.cs:50](../../src/Compliance.App/Program.cs#L50): `.AddMcpTool<RemoveTeamMember>(tool => tool.Destructive())`
- [Program.cs:51](../../src/Compliance.App/Program.cs#L51): `.AddMcpTool<SuspendMember>(tool => tool.Destructive())`
- [Program.cs:52](../../src/Compliance.App/Program.cs#L52): `.AddMcpTool<ReinstateMember>(tool => tool.Idempotent())`
- [Program.cs:53](../../src/Compliance.App/Program.cs#L53): `.AddMcpTool<GetTenantMember>(tool => tool.ReadOnly())`
- [Program.cs:54](../../src/Compliance.App/Program.cs#L54): `.AddMcpTool<ListMemberResponsibilities>(tool => tool.ReadOnly())`
- [Program.cs:55](../../src/Compliance.App/Program.cs#L55): `.AddMcpTool<ListTeamMembers>(tool => tool.ReadOnly())`
- [Program.cs:56](../../src/Compliance.App/Program.cs#L56): `.AddMcpTool<DefineRole>(tool => tool.Idempotent())`
- [Program.cs:57](../../src/Compliance.App/Program.cs#L57): `.AddMcpTool<DeleteRole>(tool => tool.Destructive())`
- [Program.cs:58](../../src/Compliance.App/Program.cs#L58): `.AddMcpTool<GetRole>(tool => tool.ReadOnly())`
- [Program.cs:59](../../src/Compliance.App/Program.cs#L59): `.AddMcpTool<ListRoles>(tool => tool.ReadOnly())`
- [Program.cs:60](../../src/Compliance.App/Program.cs#L60): `.AddMcpTool<AssignRolePermission>(tool => tool.Idempotent())`
- [Program.cs:61](../../src/Compliance.App/Program.cs#L61): `.AddMcpTool<RemoveRolePermission>(tool => tool.Destructive())`
- [Program.cs:62](../../src/Compliance.App/Program.cs#L62): `.AddMcpTool<ListRolePermissions>(tool => tool.ReadOnly())`
- [Program.cs:63](../../src/Compliance.App/Program.cs#L63): `.AddMcpTool<AssignTeamRole>(tool => tool.Idempotent())`
- [Program.cs:64](../../src/Compliance.App/Program.cs#L64): `.AddMcpTool<RemoveTeamRole>(tool => tool.Destructive())`
- [Program.cs:65](../../src/Compliance.App/Program.cs#L65): `.AddMcpTool<ListRoleTeams>(tool => tool.ReadOnly())`
- [Program.cs:66](../../src/Compliance.App/Program.cs#L66): `.AddMcpTool<ListTeamRoles>(tool => tool.ReadOnly())`
- [Program.cs:67](../../src/Compliance.App/Program.cs#L67): `.AddMcpTool<GrantAccess>(tool => tool.Idempotent())`
- [Program.cs:68](../../src/Compliance.App/Program.cs#L68): `.AddMcpTool<RevokeAccessGrant>(tool => tool.Destructive())`
- [Program.cs:69](../../src/Compliance.App/Program.cs#L69): `.AddMcpTool<ListAccessGrants>(tool => tool.ReadOnly())`
- [Program.cs:70](../../src/Compliance.App/Program.cs#L70): `.AddMcpTool<ListMyTenants>(tool => tool.ReadOnly())`
- [Program.cs:71](../../src/Compliance.App/Program.cs#L71): `.AddMcpTool<RegisterTenant>()`
- [Program.cs:72](../../src/Compliance.App/Program.cs#L72): `.AddMcpTool<SuspendTenant>(tool => tool.Destructive())`
- [Program.cs:73](../../src/Compliance.App/Program.cs#L73): `.AddMcpTool<ReactivateTenant>(tool => tool.Idempotent())`
- [Program.cs:74](../../src/Compliance.App/Program.cs#L74): `.AddMcpTool<OffboardTenant>(tool => tool.Destructive())`
- [Program.cs:75](../../src/Compliance.App/Program.cs#L75): `.AddMcpTool<GrantPlatformOperator>()`
- [Program.cs:76](../../src/Compliance.App/Program.cs#L76): `.AddMcpTool<RevokePlatformOperator>(tool => tool.Destructive())`
- [Program.cs:77](../../src/Compliance.App/Program.cs#L77): `.AddMcpTool<ListPlatformOperators>(tool => tool.ReadOnly())`
- [Program.cs:78](../../src/Compliance.App/Program.cs#L78): `.AddMcpTool<InviteTenantMember>()`
- [Program.cs:79](../../src/Compliance.App/Program.cs#L79): `.AddMcpTool<InviteOrganizationMember>()`
- [Program.cs:80](../../src/Compliance.App/Program.cs#L80): `.AddMcpTool<ListTenantInvitations>(tool => tool.ReadOnly())`
- [Program.cs:81](../../src/Compliance.App/Program.cs#L81): `.AddMcpTool<GetMemberAccess>(tool => tool.ReadOnly())`
- [Program.cs:82](../../src/Compliance.App/Program.cs#L82): `.AddMcpTool<GetTenant>(tool => tool.ReadOnly())`
- [Program.cs:83](../../src/Compliance.App/Program.cs#L83): `.AddMcpTool<GetTenantProvisioningStatus>(tool => tool.ReadOnly())`
- [Program.cs:84](../../src/Compliance.App/Program.cs#L84): `.AddMcpTool<ListTenants>(tool => tool.ReadOnly())`
- [Program.cs:85](../../src/Compliance.App/Program.cs#L85): `.AddMcpTool<ListTenantMembers>(tool => tool.ReadOnly())`
- [Program.cs:86](../../src/Compliance.App/Program.cs#L86): `.AddMcpTool<ChangeTenantSlug>()`
- [Program.cs:87](../../src/Compliance.App/Program.cs#L87): `.AddMcpTool<ResolveMyTenantSlug>(tool => tool.ReadOnly())`
- [Program.cs:88](../../src/Compliance.App/Program.cs#L88): `.AddMcpTool<CreateProgram>()`
- [Program.cs:89](../../src/Compliance.App/Program.cs#L89): `.AddMcpTool<ListCriteriaCatalogEditions>(tool => tool.ReadOnly())`
- [Program.cs:90](../../src/Compliance.App/Program.cs#L90): `.AddMcpTool<GetCriteriaCatalogEdition>(tool => tool.ReadOnly())`
- [Program.cs:91](../../src/Compliance.App/Program.cs#L91): `.AddMcpTool<ListCriteriaCatalogEntries>(tool => tool.ReadOnly())`
- [Program.cs:92](../../src/Compliance.App/Program.cs#L92): `.AddMcpTool<GetCriteriaCatalogEntry>(tool => tool.ReadOnly())`
- [Program.cs:93](../../src/Compliance.App/Program.cs#L93): `.AddMcpTool<ExportCriteriaCatalogEntries>(tool => tool.ReadOnly())`
- [Program.cs:94](../../src/Compliance.App/Program.cs#L94): `.AddMcpTool<RegisterFirmStaff>(tool => tool.Idempotent())`
- [Program.cs:95](../../src/Compliance.App/Program.cs#L95): `.AddMcpTool<SetFirmStaffStatus>(tool => tool.Idempotent())`
- [Program.cs:96](../../src/Compliance.App/Program.cs#L96): `.AddMcpTool<GetFirmStaffDirectory>(tool => tool.ReadOnly())`
- [Program.cs:97](../../src/Compliance.App/Program.cs#L97): `.AddMcpTool<ListAssignableFirmStaff>(tool => tool.ReadOnly())`
- [Program.cs:98](../../src/Compliance.App/Program.cs#L98): `.AddMcpTool<GetServiceEngagementAcceptance>(tool => tool.ReadOnly())`
- [Program.cs:99](../../src/Compliance.App/Program.cs#L99): `.AddMcpTool<GetServiceEngagementAcceptanceHistory>(tool => tool.ReadOnly())`
- [Program.cs:100](../../src/Compliance.App/Program.cs#L100): `.AddMcpTool<GetEngagementManagementAcknowledgements>(tool => tool.ReadOnly())`
- [Program.cs:101](../../src/Compliance.App/Program.cs#L101): `.AddMcpTool<CreateServiceEngagement>(tool => tool.Idempotent())`
- [Program.cs:102](../../src/Compliance.App/Program.cs#L102): `.AddMcpTool<AmendServiceEngagement>(tool => tool.Idempotent())`
- [Program.cs:103](../../src/Compliance.App/Program.cs#L103): `.AddMcpTool<ProposeServiceEngagementStaff>(tool => tool.Idempotent())`
- [Program.cs:104](../../src/Compliance.App/Program.cs#L104): `.AddMcpTool<WithdrawServiceEngagementStaffProposal>(tool => tool.Idempotent())`
- [Program.cs:105](../../src/Compliance.App/Program.cs#L105): `.AddMcpTool<CloseServiceEngagement>(tool => tool.Idempotent())`
- [Program.cs:106](../../src/Compliance.App/Program.cs#L106): `.AddMcpTool<GetServiceEngagement>(tool => tool.ReadOnly())`
- [Program.cs:107](../../src/Compliance.App/Program.cs#L107): `.AddMcpTool<ListServiceEngagements>(tool => tool.ReadOnly())`
- [Program.cs:108](../../src/Compliance.App/Program.cs#L108): `.AddMcpTool<GetServiceEngagementHistory>(tool => tool.ReadOnly())`
- [Program.cs:109](../../src/Compliance.App/Program.cs#L109): `.AddMcpTool<GetClientIndependenceRules>(tool => tool.ReadOnly())`
- [Program.cs:110](../../src/Compliance.App/Program.cs#L110): `.AddMcpTool<GetIndependenceRuleVersions>(tool => tool.ReadOnly())`
- [Program.cs:111](../../src/Compliance.App/Program.cs#L111): `.AddMcpTool<ReviseIndependenceRules>(tool => tool.Idempotent())`
- [Program.cs:112](../../src/Compliance.App/Program.cs#L112): `.AddMcpTool<RecordNonattestService>(tool => tool.Idempotent())`
- [Program.cs:113](../../src/Compliance.App/Program.cs#L113): `.AddMcpTool<EvaluateClientIndependence>(tool => tool.Idempotent())`
- [Program.cs:114](../../src/Compliance.App/Program.cs#L114): `.AddMcpTool<GetClientIndependenceHistory>(tool => tool.ReadOnly())`
- [Program.cs:115](../../src/Compliance.App/Program.cs#L115): `.AddMcpTool<SetCriteriaTextOverlay>(tool => tool.Idempotent())`
- [Program.cs:116](../../src/Compliance.App/Program.cs#L116): `.AddMcpTool<SelectProgramCriteriaEdition>(tool => tool.Idempotent())`
- [Program.cs:117](../../src/Compliance.App/Program.cs#L117): `.AddMcpTool<CreateControlDraft>()`
- [Program.cs:118](../../src/Compliance.App/Program.cs#L118): `.AddMcpTool<ReviseControlDraft>(tool => tool.Idempotent())`
- [Program.cs:119](../../src/Compliance.App/Program.cs#L119): `.AddMcpTool<DiscardControlDraft>(tool => tool.Destructive())`
- [Program.cs:120](../../src/Compliance.App/Program.cs#L120): `.AddMcpTool<GetControlDraft>(tool => tool.ReadOnly())`
- [Program.cs:121](../../src/Compliance.App/Program.cs#L121): `.AddMcpTool<ListControlDrafts>(tool => tool.ReadOnly())`
- [Program.cs:122](../../src/Compliance.App/Program.cs#L122): `.AddMcpTool<ListControlDraftRevisions>(tool => tool.ReadOnly())`
- [Program.cs:123](../../src/Compliance.App/Program.cs#L123): `.AddMcpTool<GetControlDraftRevision>(tool => tool.ReadOnly())`
- [Program.cs:124](../../src/Compliance.App/Program.cs#L124): `.AddMcpTool<GetControlVersion>(tool => tool.ReadOnly())`
- [Program.cs:125](../../src/Compliance.App/Program.cs#L125): `.AddMcpTool<GetCurrentControlVersion>(tool => tool.ReadOnly())`
- [Program.cs:126](../../src/Compliance.App/Program.cs#L126): `.AddMcpTool<GetEffectiveControlVersion>(tool => tool.ReadOnly())`
- [Program.cs:127](../../src/Compliance.App/Program.cs#L127): `.AddMcpTool<ListControlVersions>(tool => tool.ReadOnly())`
- [Program.cs:128](../../src/Compliance.App/Program.cs#L128): `.AddMcpTool<GetControlDecision>(tool => tool.ReadOnly())`
- [Program.cs:129](../../src/Compliance.App/Program.cs#L129): `.AddMcpTool<ListControlDecisions>(tool => tool.ReadOnly())`
- [Program.cs:130](../../src/Compliance.App/Program.cs#L130): `.AddMcpTool<GetControlCriterionMapping>(tool => tool.ReadOnly())`
- [Program.cs:131](../../src/Compliance.App/Program.cs#L131): `.AddMcpTool<ListControlCriterionMappings>(tool => tool.ReadOnly())`
- [Program.cs:132](../../src/Compliance.App/Program.cs#L132): `.AddMcpTool<ListCriteriaCoverage>(tool => tool.ReadOnly())`
- [Program.cs:133](../../src/Compliance.App/Program.cs#L133): `.AddMcpTool<ProposeControlSuccessor>()`
- [Program.cs:134](../../src/Compliance.App/Program.cs#L134): `.AddMcpTool<ProposeControlRetirement>()`
- [Program.cs:135](../../src/Compliance.App/Program.cs#L135): `.AddMcpTool<PreviewControlImpact>(tool => tool.ReadOnly())`
- [Program.cs:136](../../src/Compliance.App/Program.cs#L136): `.AddMcpTool<GetCriterionApplicability>(tool => tool.ReadOnly())`
- [Program.cs:137](../../src/Compliance.App/Program.cs#L137): `.AddMcpTool<ListCriterionApplicability>(tool => tool.ReadOnly())`
- [Program.cs:138](../../src/Compliance.App/Program.cs#L138): `.AddMcpTool<CreateCommitmentDraft>()`
- [Program.cs:139](../../src/Compliance.App/Program.cs#L139): `.AddMcpTool<ReviseCommitmentDraft>()`
- [Program.cs:140](../../src/Compliance.App/Program.cs#L140): `.AddMcpTool<GetCommitmentDraft>(tool => tool.ReadOnly())`
- [Program.cs:141](../../src/Compliance.App/Program.cs#L141): `.AddMcpTool<ListCommitmentDrafts>(tool => tool.ReadOnly())`
- [Program.cs:142](../../src/Compliance.App/Program.cs#L142): `.AddMcpTool<ListCommitmentDraftRevisions>(tool => tool.ReadOnly())`
- [Program.cs:143](../../src/Compliance.App/Program.cs#L143): `.AddMcpTool<GetCommitmentDraftRevision>(tool => tool.ReadOnly())`
- [Program.cs:144](../../src/Compliance.App/Program.cs#L144): `.AddMcpTool<PreviewCommitmentImpact>(tool => tool.ReadOnly())`
- [Program.cs:145](../../src/Compliance.App/Program.cs#L145): `.AddMcpTool<GetCommitmentVersion>(tool => tool.ReadOnly())`
- [Program.cs:146](../../src/Compliance.App/Program.cs#L146): `.AddMcpTool<GetEffectiveCommitmentVersion>(tool => tool.ReadOnly())`
- [Program.cs:147](../../src/Compliance.App/Program.cs#L147): `.AddMcpTool<ListCommitmentVersions>(tool => tool.ReadOnly())`
- [Program.cs:148](../../src/Compliance.App/Program.cs#L148): `.AddMcpTool<ListCommitmentDecisions>(tool => tool.ReadOnly())`
- [Program.cs:149](../../src/Compliance.App/Program.cs#L149): `.AddMcpTool<CreatePolicyDraft>()`
- [Program.cs:150](../../src/Compliance.App/Program.cs#L150): `.AddMcpTool<RevisePolicyDraft>()`
- [Program.cs:151](../../src/Compliance.App/Program.cs#L151): `.AddMcpTool<ProposePolicySuccessor>()`
- [Program.cs:152](../../src/Compliance.App/Program.cs#L152): `.AddMcpTool<DiscardPolicyDraft>(tool => tool.Destructive())`
- [Program.cs:153](../../src/Compliance.App/Program.cs#L153): `.AddMcpTool<ProposePolicyRetirement>()`
- [Program.cs:154](../../src/Compliance.App/Program.cs#L154): `.AddMcpTool<GetPolicy>(tool => tool.ReadOnly())`
- [Program.cs:155](../../src/Compliance.App/Program.cs#L155): `.AddMcpTool<ListPolicies>(tool => tool.ReadOnly())`
- [Program.cs:156](../../src/Compliance.App/Program.cs#L156): `.AddMcpTool<GetPolicyVersion>(tool => tool.ReadOnly())`
- [Program.cs:157](../../src/Compliance.App/Program.cs#L157): `.AddMcpTool<GetEffectivePolicyVersion>(tool => tool.ReadOnly())`
- [Program.cs:158](../../src/Compliance.App/Program.cs#L158): `.AddMcpTool<ListPolicyVersions>(tool => tool.ReadOnly())`
- [Program.cs:159](../../src/Compliance.App/Program.cs#L159): `.AddMcpTool<ListPolicyDecisions>(tool => tool.ReadOnly())`
- [Program.cs:160](../../src/Compliance.App/Program.cs#L160): `.AddMcpTool<PreviewPolicyImpact>(tool => tool.ReadOnly())`
- [Program.cs:161](../../src/Compliance.App/Program.cs#L161): `.AddMcpTool<DefineTrainingRequirement>()`
- [Program.cs:162](../../src/Compliance.App/Program.cs#L162): `.AddMcpTool<ReviseTrainingRequirement>()`
- [Program.cs:163](../../src/Compliance.App/Program.cs#L163): `.AddMcpTool<GetTrainingRequirement>(tool => tool.ReadOnly())`
- [Program.cs:164](../../src/Compliance.App/Program.cs#L164): `.AddMcpTool<ListTrainingRequirements>(tool => tool.ReadOnly())`
- [Program.cs:165](../../src/Compliance.App/Program.cs#L165): `.AddMcpTool<LaunchPolicyCampaign>()`
- [Program.cs:166](../../src/Compliance.App/Program.cs#L166): `.AddMcpTool<LaunchTrainingCampaign>()`
- [Program.cs:167](../../src/Compliance.App/Program.cs#L167): `.AddMcpTool<ReconcileCampaignAudience>()`
- [Program.cs:168](../../src/Compliance.App/Program.cs#L168): `.AddMcpTool<RecordTrainingCompletion>()`
- [Program.cs:169](../../src/Compliance.App/Program.cs#L169): `.AddMcpTool<CloseCampaign>()`
- [Program.cs:170](../../src/Compliance.App/Program.cs#L170): `.AddMcpTool<GetCampaign>(tool => tool.ReadOnly())`
- [Program.cs:171](../../src/Compliance.App/Program.cs#L171): `.AddMcpTool<ListCampaigns>(tool => tool.ReadOnly())`
- [Program.cs:172](../../src/Compliance.App/Program.cs#L172): `.AddMcpTool<ListCampaignParticipants>(tool => tool.ReadOnly())`
- [Program.cs:173](../../src/Compliance.App/Program.cs#L173): `.AddMcpTool<ListCampaignAmendments>(tool => tool.ReadOnly())`
- [Program.cs:174](../../src/Compliance.App/Program.cs#L174): `.AddMcpTool<CreateRiskDraft>()`
- [Program.cs:175](../../src/Compliance.App/Program.cs#L175): `.AddMcpTool<ReviseRiskDraft>(tool => tool.Idempotent())`
- [Program.cs:176](../../src/Compliance.App/Program.cs#L176): `.AddMcpTool<GetRiskDraft>(tool => tool.ReadOnly())`
- [Program.cs:177](../../src/Compliance.App/Program.cs#L177): `.AddMcpTool<ListRiskDrafts>(tool => tool.ReadOnly())`
- [Program.cs:178](../../src/Compliance.App/Program.cs#L178): `.AddMcpTool<ListRiskDraftRevisions>(tool => tool.ReadOnly())`
- [Program.cs:179](../../src/Compliance.App/Program.cs#L179): `.AddMcpTool<GetRiskDraftRevision>(tool => tool.ReadOnly())`
- [Program.cs:180](../../src/Compliance.App/Program.cs#L180): `.AddMcpTool<GetRiskMethod>(tool => tool.ReadOnly())`
- [Program.cs:181](../../src/Compliance.App/Program.cs#L181): `.AddMcpTool<GetRiskMethodVersion>(tool => tool.ReadOnly())`
- [Program.cs:182](../../src/Compliance.App/Program.cs#L182): `.AddMcpTool<GetRiskEvaluation>(tool => tool.ReadOnly())`
- [Program.cs:183](../../src/Compliance.App/Program.cs#L183): `.AddMcpTool<ListRiskEvaluationHistory>(tool => tool.ReadOnly())`
- [Program.cs:184](../../src/Compliance.App/Program.cs#L184): `.AddMcpTool<GetRiskGovernance>(tool => tool.ReadOnly())`
- [Program.cs:185](../../src/Compliance.App/Program.cs#L185): `.AddMcpTool<ReviseProgram>(tool => tool.Idempotent())`
- [Program.cs:186](../../src/Compliance.App/Program.cs#L186): `.AddMcpTool<GetProgram>(tool => tool.ReadOnly())`
- [Program.cs:187](../../src/Compliance.App/Program.cs#L187): `.AddMcpTool<ListPrograms>(tool => tool.ReadOnly())`
- [Program.cs:188](../../src/Compliance.App/Program.cs#L188): `.AddMcpTool<ListProgramRevisions>(tool => tool.ReadOnly())`
- [Program.cs:189](../../src/Compliance.App/Program.cs#L189): `.AddMcpTool<GetProgramRevision>(tool => tool.ReadOnly())`
- [Program.cs:190](../../src/Compliance.App/Program.cs#L190): `.AddMcpTool<GetProgramSetupWork>(tool => tool.ReadOnly())`
- [Program.cs:191](../../src/Compliance.App/Program.cs#L191): `.AddMcpTool<DeclareApplication>()`
- [Program.cs:192](../../src/Compliance.App/Program.cs#L192): `.AddMcpTool<ReviseApplicationMcpInput>(tool => tool.Named("bdgrz.application.revise")`
- [Program.cs:194](../../src/Compliance.App/Program.cs#L194): `.AddMcpTool<DeclareSystemInstance>()`
- [Program.cs:195](../../src/Compliance.App/Program.cs#L195): `.AddMcpTool<GetApplication>(tool => tool.ReadOnly())`
- [Program.cs:196](../../src/Compliance.App/Program.cs#L196): `.AddMcpTool<ListApplications>(tool => tool.ReadOnly())`
- [Program.cs:197](../../src/Compliance.App/Program.cs#L197): `.AddMcpTool<GetApplicationRevision>(tool => tool.ReadOnly())`
- [Program.cs:198](../../src/Compliance.App/Program.cs#L198): `.AddMcpTool<ListApplicationRevisions>(tool => tool.ReadOnly())`
- [Program.cs:199](../../src/Compliance.App/Program.cs#L199): `.AddMcpTool<GetSystemInstance>(tool => tool.ReadOnly())`
- [Program.cs:200](../../src/Compliance.App/Program.cs#L200): `.AddMcpTool<ListSystemInstances>(tool => tool.ReadOnly())`
- [Program.cs:201](../../src/Compliance.App/Program.cs#L201): `.AddMcpTool<ListApplicationBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:202](../../src/Compliance.App/Program.cs#L202): `.AddMcpTool<ListSystemInstanceBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:203](../../src/Compliance.App/Program.cs#L203): `.AddMcpTool<PreviewApplicationChangeMcpInput>(tool => tool.Named("bdgrz.application.change.preview")`
- [Program.cs:205](../../src/Compliance.App/Program.cs#L205): `.AddMcpTool<RetireApplication>(tool => tool.Idempotent())`
- [Program.cs:206](../../src/Compliance.App/Program.cs#L206): `.AddMcpTool<RetireSystemInstance>(tool => tool.Idempotent())`
- [Program.cs:207](../../src/Compliance.App/Program.cs#L207): `.AddMcpTool<GetAccessReviewScope>(tool => tool.ReadOnly())`
- [Program.cs:208](../../src/Compliance.App/Program.cs#L208): `.AddMcpTool<ListAccessReviewScopes>(tool => tool.ReadOnly())`
- [Program.cs:209](../../src/Compliance.App/Program.cs#L209): `.AddMcpTool<StageApplicationImport>(tool => tool.Idempotent())`
- [Program.cs:210](../../src/Compliance.App/Program.cs#L210): `.AddMcpTool<GetApplicationImport>(tool => tool.ReadOnly())`
- [Program.cs:211](../../src/Compliance.App/Program.cs#L211): `.AddMcpTool<ListApplicationImportRows>(tool => tool.ReadOnly())`
- [Program.cs:212](../../src/Compliance.App/Program.cs#L212): `.AddMcpTool<PreviewApplicationImport>(tool => tool.ReadOnly())`
- [Program.cs:213](../../src/Compliance.App/Program.cs#L213): `.AddMcpTool<PreviewMissingApplicationImportRows>(tool => tool.ReadOnly())`
- [Program.cs:214](../../src/Compliance.App/Program.cs#L214): `.AddMcpTool<PreviewApplicationImportOmissionProposal>(tool => tool.ReadOnly())`
- [Program.cs:215](../../src/Compliance.App/Program.cs#L215): `.AddMcpTool<GetApplicationImportOmissionProposal>(tool => tool.ReadOnly())`
- [Program.cs:216](../../src/Compliance.App/Program.cs#L216): `.AddMcpTool<GetArtifactRetention>(tool => tool.ReadOnly())`
- [Program.cs:217](../../src/Compliance.App/Program.cs#L217): `.AddMcpTool<ListArtifactLegalHolds>(tool => tool.ReadOnly())`
- [Program.cs:218](../../src/Compliance.App/Program.cs#L218): `.AddMcpTool<GetApplicationImportRetention>(tool => tool.ReadOnly())`
- [Program.cs:219](../../src/Compliance.App/Program.cs#L219): `.AddMcpTool<GetApplicationImportProgress>(tool => tool.ReadOnly())`
- [Program.cs:220](../../src/Compliance.App/Program.cs#L220): `.AddMcpTool<GetApplicationImportRejectedReport>(tool => tool.ReadOnly())`
- [Program.cs:221](../../src/Compliance.App/Program.cs#L221): `.AddMcpTool<RecordPerson>()`
- [Program.cs:222](../../src/Compliance.App/Program.cs#L222): `.AddMcpTool<RevisePerson>(tool => tool.Idempotent())`
- [Program.cs:223](../../src/Compliance.App/Program.cs#L223): `.AddMcpTool<CorrelatePersonMembership>(tool => tool.Idempotent())`
- [Program.cs:224](../../src/Compliance.App/Program.cs#L224): `.AddMcpTool<GetPerson>(tool => tool.ReadOnly())`
- [Program.cs:225](../../src/Compliance.App/Program.cs#L225): `.AddMcpTool<ListPeople>(tool => tool.ReadOnly())`
- [Program.cs:226](../../src/Compliance.App/Program.cs#L226): `.AddMcpTool<RecordWorkRelationship>()`
- [Program.cs:227](../../src/Compliance.App/Program.cs#L227): `.AddMcpTool<ReviseWorkRelationship>(tool => tool.Idempotent())`
- [Program.cs:228](../../src/Compliance.App/Program.cs#L228): `.AddMcpTool<GetWorkRelationship>(tool => tool.ReadOnly())`
- [Program.cs:229](../../src/Compliance.App/Program.cs#L229): `.AddMcpTool<ListWorkRelationships>(tool => tool.ReadOnly())`
- [Program.cs:230](../../src/Compliance.App/Program.cs#L230): `.AddMcpTool<ListWorkforceObservations>(tool => tool.ReadOnly())`
- [Program.cs:231](../../src/Compliance.App/Program.cs#L231): `.AddMcpTool<ListWorkforceReconciliationObservations>(tool => tool.ReadOnly())`
- [Program.cs:232](../../src/Compliance.App/Program.cs#L232): `.AddMcpTool<RecordWorkforceSourceObservation>(tool => tool.Idempotent())`
- [Program.cs:233](../../src/Compliance.App/Program.cs#L233): `.AddMcpTool<GetWorkforceSourceObservation>(tool => tool.ReadOnly())`
- [Program.cs:234](../../src/Compliance.App/Program.cs#L234): `.AddMcpTool<ListWorkforceSourceObservations>(tool => tool.ReadOnly())`
- [Program.cs:235](../../src/Compliance.App/Program.cs#L235): `.AddMcpTool<PreviewWorkforceSourceObservation>(tool => tool.ReadOnly())`
- [Program.cs:236](../../src/Compliance.App/Program.cs#L236): `.AddMcpTool<RecordServiceIdentity>()`
- [Program.cs:237](../../src/Compliance.App/Program.cs#L237): `.AddMcpTool<ReviseServiceIdentity>(tool => tool.Idempotent())`
- [Program.cs:238](../../src/Compliance.App/Program.cs#L238): `.AddMcpTool<GetServiceIdentity>(tool => tool.ReadOnly())`
- [Program.cs:239](../../src/Compliance.App/Program.cs#L239): `.AddMcpTool<ListServiceIdentities>(tool => tool.ReadOnly())`
- [Program.cs:240](../../src/Compliance.App/Program.cs#L240): `.AddMcpTool<RecordProvider>()`
- [Program.cs:241](../../src/Compliance.App/Program.cs#L241): `.AddMcpTool<ReviseProvider>(tool => tool.Idempotent())`
- [Program.cs:242](../../src/Compliance.App/Program.cs#L242): `.AddMcpTool<GetProvider>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:243](../../src/Compliance.App/Program.cs#L243): `.AddMcpTool<ListProviders>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:244](../../src/Compliance.App/Program.cs#L244): `.AddMcpTool<GetProviderRevision>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:245](../../src/Compliance.App/Program.cs#L245): `.AddMcpTool<ListProviderRevisions>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:246](../../src/Compliance.App/Program.cs#L246): `.AddMcpTool<RecordAssuranceReport>()`
- [Program.cs:247](../../src/Compliance.App/Program.cs#L247): `.AddMcpTool<ReviseAssuranceReport>(tool => tool.Idempotent())`
- [Program.cs:248](../../src/Compliance.App/Program.cs#L248): `.AddMcpTool<ListProviderAssuranceReports>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:249](../../src/Compliance.App/Program.cs#L249): `.AddMcpTool<ListProviderReviews>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:250](../../src/Compliance.App/Program.cs#L250): `.AddMcpTool<RecordProviderCoverageGap>()`
- [Program.cs:251](../../src/Compliance.App/Program.cs#L251): `.AddMcpTool<GetProviderCoverageGap>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:252](../../src/Compliance.App/Program.cs#L252): `.AddMcpTool<ListProviderCoverageGaps>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:253](../../src/Compliance.App/Program.cs#L253): `.AddMcpTool<GetProviderAssuranceCoverage>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:254](../../src/Compliance.App/Program.cs#L254): `.AddMcpTool<PreviewProviderChange>(tool => tool.ReadOnly().Idempotent())`
- [Program.cs:255](../../src/Compliance.App/Program.cs#L255): `.AddMcpTool<RecordTechnologyComponent>()`
- [Program.cs:256](../../src/Compliance.App/Program.cs#L256): `.AddMcpTool<ReviseTechnologyComponent>(tool => tool.Idempotent())`
- [Program.cs:257](../../src/Compliance.App/Program.cs#L257): `.AddMcpTool<GetTechnologyComponent>(tool => tool.ReadOnly())`
- [Program.cs:258](../../src/Compliance.App/Program.cs#L258): `.AddMcpTool<ListTechnologyComponentBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:259](../../src/Compliance.App/Program.cs#L259): `.AddMcpTool<ListInformationAssetBoundaryReferences>(tool => tool.ReadOnly())`
- [Program.cs:260](../../src/Compliance.App/Program.cs#L260): `.AddMcpTool<ListTechnologyComponents>(tool => tool.ReadOnly())`
- [Program.cs:261](../../src/Compliance.App/Program.cs#L261): `.AddMcpTool<ListTechnologyComponentRevisions>(tool => tool.ReadOnly())`
- [Program.cs:262](../../src/Compliance.App/Program.cs#L262): `.AddMcpTool<RecordInformationAsset>()`
- [Program.cs:263](../../src/Compliance.App/Program.cs#L263): `.AddMcpTool<ReviseInformationAsset>(tool => tool.Idempotent())`
- [Program.cs:264](../../src/Compliance.App/Program.cs#L264): `.AddMcpTool<GetInformationAsset>(tool => tool.ReadOnly())`
- [Program.cs:265](../../src/Compliance.App/Program.cs#L265): `.AddMcpTool<ListInformationAssets>(tool => tool.ReadOnly())`
- [Program.cs:266](../../src/Compliance.App/Program.cs#L266): `.AddMcpTool<ListInformationAssetRevisions>(tool => tool.ReadOnly())`
- [Program.cs:267](../../src/Compliance.App/Program.cs#L267): `.AddMcpTool<PreviewInformationAssetChange>(tool => tool.ReadOnly())`
- [Program.cs:268](../../src/Compliance.App/Program.cs#L268): `.AddMcpTool<RecordLocation>()`
- [Program.cs:269](../../src/Compliance.App/Program.cs#L269): `.AddMcpTool<ReviseLocation>(tool => tool.Idempotent())`
- [Program.cs:270](../../src/Compliance.App/Program.cs#L270): `.AddMcpTool<GetLocation>(tool => tool.ReadOnly())`
- [Program.cs:271](../../src/Compliance.App/Program.cs#L271): `.AddMcpTool<ListLocations>(tool => tool.ReadOnly())`
- [Program.cs:272](../../src/Compliance.App/Program.cs#L272): `.AddMcpTool<ListLocationRevisions>(tool => tool.ReadOnly())`
- [Program.cs:273](../../src/Compliance.App/Program.cs#L273): `.AddMcpTool<RecordOperationalProcess>()`
- [Program.cs:274](../../src/Compliance.App/Program.cs#L274): `.AddMcpTool<ReviseOperationalProcess>(tool => tool.Idempotent())`
- [Program.cs:275](../../src/Compliance.App/Program.cs#L275): `.AddMcpTool<GetOperationalProcess>(tool => tool.ReadOnly())`
- [Program.cs:276](../../src/Compliance.App/Program.cs#L276): `.AddMcpTool<ListOperationalProcesses>(tool => tool.ReadOnly())`
- [Program.cs:277](../../src/Compliance.App/Program.cs#L277): `.AddMcpTool<ListOperationalProcessRevisions>(tool => tool.ReadOnly())`
- [Program.cs:278](../../src/Compliance.App/Program.cs#L278): `.AddMcpTool<RecordDataFlow>()`
- [Program.cs:279](../../src/Compliance.App/Program.cs#L279): `.AddMcpTool<ReviseDataFlow>(tool => tool.Idempotent())`
- [Program.cs:280](../../src/Compliance.App/Program.cs#L280): `.AddMcpTool<GetDataFlow>(tool => tool.ReadOnly())`
- [Program.cs:281](../../src/Compliance.App/Program.cs#L281): `.AddMcpTool<ListDataFlows>(tool => tool.ReadOnly())`
- [Program.cs:282](../../src/Compliance.App/Program.cs#L282): `.AddMcpTool<ListDataFlowRevisions>(tool => tool.ReadOnly())`
- [Program.cs:283](../../src/Compliance.App/Program.cs#L283): `.AddMcpTool<CreateClientService>()`
- [Program.cs:284](../../src/Compliance.App/Program.cs#L284): `.AddMcpTool<ReviseClientService>(tool => tool.Idempotent())`
- [Program.cs:285](../../src/Compliance.App/Program.cs#L285): `.AddMcpTool<RetireClientService>(tool => tool.Destructive())`
- [Program.cs:286](../../src/Compliance.App/Program.cs#L286): `.AddMcpTool<GetClientService>(tool => tool.ReadOnly())`
- [Program.cs:287](../../src/Compliance.App/Program.cs#L287): `.AddMcpTool<ListClientServices>(tool => tool.ReadOnly())`
- [Program.cs:288](../../src/Compliance.App/Program.cs#L288): `.AddMcpTool<ListProgramClientServices>(tool => tool.ReadOnly())`
- [Program.cs:289](../../src/Compliance.App/Program.cs#L289): `.AddMcpTool<ListClientServiceRevisions>(tool => tool.ReadOnly())`
- [Program.cs:290](../../src/Compliance.App/Program.cs#L290): `.AddMcpTool<GetClientServiceRevision>(tool => tool.ReadOnly())`
- [Program.cs:291](../../src/Compliance.App/Program.cs#L291): `.AddMcpTool<CreateBoundary>()`
- [Program.cs:292](../../src/Compliance.App/Program.cs#L292): `.AddMcpTool<ReviseBoundaryDraft>(tool => tool.Idempotent())`
- [Program.cs:293](../../src/Compliance.App/Program.cs#L293): `.AddMcpTool<DiscardBoundaryDraft>(tool => tool.Destructive())`
- [Program.cs:294](../../src/Compliance.App/Program.cs#L294): `.AddMcpTool<GetBoundary>(tool => tool.ReadOnly())`
- [Program.cs:295](../../src/Compliance.App/Program.cs#L295): `.AddMcpTool<ListResponsibilities>(tool => tool.ReadOnly())`
- [Program.cs:296](../../src/Compliance.App/Program.cs#L296): `.AddMcpTool<PreviewResponsibilityConflicts>(tool => tool.ReadOnly())`
- [Program.cs:297](../../src/Compliance.App/Program.cs#L297): `.AddMcpTool<ListProgramBoundaries>(tool => tool.ReadOnly())`
- [Program.cs:298](../../src/Compliance.App/Program.cs#L298): `.AddMcpTool<GetBoundaryVersion>(tool => tool.ReadOnly())`
- [Program.cs:299](../../src/Compliance.App/Program.cs#L299): `.AddMcpTool<ListBoundaryVersions>(tool => tool.ReadOnly())`
- [Program.cs:300](../../src/Compliance.App/Program.cs#L300): `.AddMcpTool<GetEffectiveBoundaryVersion>(tool => tool.ReadOnly())`
- [Program.cs:301](../../src/Compliance.App/Program.cs#L301): `.AddMcpTool<GetBoundaryDecision>(tool => tool.ReadOnly())`
- [Program.cs:302](../../src/Compliance.App/Program.cs#L302): `.AddMcpTool<ListBoundaryDecisions>(tool => tool.ReadOnly())`
- [Program.cs:303](../../src/Compliance.App/Program.cs#L303): `.AddMcpTool<PreviewBoundaryImpact>(tool => tool.ReadOnly())`
- [Program.cs:304](../../src/Compliance.App/Program.cs#L304): `.AddMcpTool<ProposeBoundarySuccessor>()`
- [Program.cs:305](../../src/Compliance.App/Program.cs#L305): `.AddMcpTool<FreezeProgramScopeSnapshot>()`
- [Program.cs:306](../../src/Compliance.App/Program.cs#L306): `.AddMcpTool<AmendProgramScopeSnapshot>()`
- [Program.cs:307](../../src/Compliance.App/Program.cs#L307): `.AddMcpTool<GetSnapshot>(tool => tool.ReadOnly())`
- [Program.cs:308](../../src/Compliance.App/Program.cs#L308): `.AddMcpTool<VerifyProgramScopeSnapshot>(tool => tool.ReadOnly())`
- [Program.cs:309](../../src/Compliance.App/Program.cs#L309): `.AddMcpTool<RegenerateProgramScopeSnapshotManifest>(tool => tool.ReadOnly())`
- [Program.cs:310](../../src/Compliance.App/Program.cs#L310): `.AddMcpTool<ListProgramSnapshots>(tool => tool.ReadOnly())`
- [Program.cs:311](../../src/Compliance.App/Program.cs#L311): `.AddMcpTool<FreezeWorkforceRosterSnapshot>()`
- [Program.cs:312](../../src/Compliance.App/Program.cs#L312): `.AddMcpTool<AmendWorkforceRosterSnapshot>()`
- [Program.cs:313](../../src/Compliance.App/Program.cs#L313): `.AddMcpTool<GetWorkforceRosterSnapshot>(tool => tool.ReadOnly())`
- [Program.cs:314](../../src/Compliance.App/Program.cs#L314): `.AddMcpTool<GetWorkforceRosterSnapshotAsOf>(tool => tool.ReadOnly())`
- [Program.cs:315](../../src/Compliance.App/Program.cs#L315): `.AddMcpTool<ListWorkforceRosterSnapshots>(tool => tool.ReadOnly())`
- [Program.cs:316](../../src/Compliance.App/Program.cs#L316): `.AddMcpTool<RegenerateWorkforceRosterSnapshotManifest>(tool => tool.ReadOnly())`
- [Program.cs:317](../../src/Compliance.App/Program.cs#L317): `.AddMcpTool<GetReadinessAssessment>(tool => tool.ReadOnly())`
- [Program.cs:318](../../src/Compliance.App/Program.cs#L318): `.AddMcpTool<ListReadinessAssessments>(tool => tool.ReadOnly())`
- [Program.cs:319](../../src/Compliance.App/Program.cs#L319): `.AddMcpTool<ListReadinessGaps>(tool => tool.ReadOnly())`
- [Program.cs:320](../../src/Compliance.App/Program.cs#L320): `.AddMcpTool<ListTypeIEntryDecisions>(tool => tool.ReadOnly())`
- [Program.cs:321](../../src/Compliance.App/Program.cs#L321): `.AddMcpTool<ListReadinessAnnotations>(tool => tool.ReadOnly())`
- [Program.cs:322](../../src/Compliance.App/Program.cs#L322): `.AddMcpTool<ProposeControlOperatingPlan>()`
- [Program.cs:323](../../src/Compliance.App/Program.cs#L323): `.AddMcpTool<PreviewControlOperatingPlan>(tool => tool.ReadOnly())`
- [Program.cs:324](../../src/Compliance.App/Program.cs#L324): `.AddMcpTool<GetControlOperatingPlan>(tool => tool.ReadOnly())`
- [Program.cs:325](../../src/Compliance.App/Program.cs#L325): `.AddMcpTool<ListControlOperatingBlockers>(tool => tool.ReadOnly())`
- [Program.cs:326](../../src/Compliance.App/Program.cs#L326): `.AddMcpTool<ListMyControlWork>(tool => tool.ReadOnly())`
- [Program.cs:327](../../src/Compliance.App/Program.cs#L327): `.AddMcpTool<GetControlOccurrence>(tool => tool.ReadOnly())`
- [Program.cs:328](../../src/Compliance.App/Program.cs#L328): `.AddMcpTool<ListControlOccurrences>(tool => tool.ReadOnly())`
- [Program.cs:329](../../src/Compliance.App/Program.cs#L329): `.AddMcpTool<DefineControlEvaluationPlan>()`
- [Program.cs:330](../../src/Compliance.App/Program.cs#L330): `.AddMcpTool<GetControlEvaluationPlanVersion>(tool => tool.ReadOnly())`
- [Program.cs:331](../../src/Compliance.App/Program.cs#L331): `.AddMcpTool<ListControlEvaluationPlanVersions>(tool => tool.ReadOnly())`
- [Program.cs:332](../../src/Compliance.App/Program.cs#L332): `.AddMcpTool<StartControlEvaluation>()`
- [Program.cs:333](../../src/Compliance.App/Program.cs#L333): `.AddMcpTool<RecordControlEvaluationStep>()`
- [Program.cs:334](../../src/Compliance.App/Program.cs#L334): `.AddMcpTool<DisposeControlEvaluationDeviation>()`
- [Program.cs:335](../../src/Compliance.App/Program.cs#L335): `.AddMcpTool<GetControlEvaluation>(tool => tool.ReadOnly())`
- [Program.cs:336](../../src/Compliance.App/Program.cs#L336): `.AddMcpTool<ListControlEvaluations>(tool => tool.ReadOnly())`
- [Program.cs:337](../../src/Compliance.App/Program.cs#L337): `.AddMcpTool<RaiseFinding>()`
- [Program.cs:338](../../src/Compliance.App/Program.cs#L338): `.AddMcpTool<GetFinding>(tool => tool.ReadOnly())`
- [Program.cs:339](../../src/Compliance.App/Program.cs#L339): `.AddMcpTool<ListFindings>(tool => tool.ReadOnly())`
- [Program.cs:340](../../src/Compliance.App/Program.cs#L340): `.AddMcpTool<OpenEvidenceRequest>()`
- [Program.cs:341](../../src/Compliance.App/Program.cs#L341): `.AddMcpTool<CancelEvidenceRequest>()`
- [Program.cs:342](../../src/Compliance.App/Program.cs#L342): `.AddMcpTool<PrepareEvidenceRedaction>()`
- [Program.cs:343](../../src/Compliance.App/Program.cs#L343): `.AddMcpTool<GetEvidenceRedaction>(tool => tool.ReadOnly())`
- [Program.cs:344](../../src/Compliance.App/Program.cs#L344): `.AddMcpTool<GetEvidenceArtifactMetadata>(tool => tool.ReadOnly())`
- [Program.cs:345](../../src/Compliance.App/Program.cs#L345): `.AddMcpTool<GetEvidenceRequest>(tool => tool.ReadOnly())`
- [Program.cs:346](../../src/Compliance.App/Program.cs#L346): `.AddMcpTool<ListEvidenceRequests>(tool => tool.ReadOnly())`
- [Program.cs:347](../../src/Compliance.App/Program.cs#L347): `.AddMcpTool<OpenAccessPopulation>()`
- [Program.cs:348](../../src/Compliance.App/Program.cs#L348): `.AddMcpTool<RecordAccessPopulationFacts>(tool => tool.Idempotent())`
- [Program.cs:349](../../src/Compliance.App/Program.cs#L349): `.AddMcpTool<PreviewAccessPopulation>(tool => tool.ReadOnly())`
- [Program.cs:350](../../src/Compliance.App/Program.cs#L350): `.AddMcpTool<GetAccessPopulation>(tool => tool.ReadOnly())`
- [Program.cs:351](../../src/Compliance.App/Program.cs#L351): `.AddMcpTool<ListAccessPopulations>(tool => tool.ReadOnly())`
- [Program.cs:352](../../src/Compliance.App/Program.cs#L352): `.AddMcpTool<GetAccessReviewCoverage>(tool => tool.ReadOnly())`
- [Program.cs:353](../../src/Compliance.App/Program.cs#L353): `.AddMcpTool<ClassifyAccessPrincipal>()`
- [Program.cs:354](../../src/Compliance.App/Program.cs#L354): `.AddMcpTool<ListAccessPrincipals>(tool => tool.ReadOnly())`
- [Program.cs:355](../../src/Compliance.App/Program.cs#L355): `.AddMcpTool<GetAccessVariance>(tool => tool.ReadOnly())`
- [Program.cs:356](../../src/Compliance.App/Program.cs#L356): `.AddMcpTool<ProposeAccessExpectation>()`
- [Program.cs:357](../../src/Compliance.App/Program.cs#L357): `.AddMcpTool<ListAccessExpectations>(tool => tool.ReadOnly())`
- [Program.cs:358](../../src/Compliance.App/Program.cs#L358): `.AddMcpTool<LaunchAccessReviewCampaign>()`
- [Program.cs:359](../../src/Compliance.App/Program.cs#L359): `.AddMcpTool<GetAccessReviewCampaign>(tool => tool.ReadOnly())`
- [Program.cs:360](../../src/Compliance.App/Program.cs#L360): `.AddMcpTool<ListAccessReviewCampaigns>(tool => tool.ReadOnly())`
- [Program.cs:361](../../src/Compliance.App/Program.cs#L361): `.AddMcpTool<PreviewBulkAccessDecision>(tool => tool.ReadOnly())`
- [Program.cs:362](../../src/Compliance.App/Program.cs#L362): `.AddMcpTool<RecordAccessRemediationChange>()`
- [Program.cs:363](../../src/Compliance.App/Program.cs#L363): `.AddMcpTool<VerifyAccessRemediation>()`
- [Program.cs:364](../../src/Compliance.App/Program.cs#L364): `.AddMcpTool<ListWork>(tool => tool.ReadOnly())`
- [Program.cs:365](../../src/Compliance.App/Program.cs#L365): `.AddMcpTool<GetWorkItem>(tool => tool.ReadOnly())`
- [Program.cs:366](../../src/Compliance.App/Program.cs#L366): `.AddMcpTool<ClaimWorkItem>()`
- [Program.cs:367](../../src/Compliance.App/Program.cs#L367): `.AddMcpTool<AssignWorkItem>()`
- [Program.cs:368](../../src/Compliance.App/Program.cs#L368): `.AddMcpTool<DelegateWorkItem>()`
- [Program.cs:369](../../src/Compliance.App/Program.cs#L369): `.AddMcpTool<EscalateWorkItem>()`
- [Program.cs:370](../../src/Compliance.App/Program.cs#L370): `.AddMcpTool<ListWorkReminders>(tool => tool.ReadOnly())`
- [Program.cs:371](../../src/Compliance.App/Program.cs#L371): `.AddMcpTool<GetWorkDigest>(tool => tool.ReadOnly())`
- [Program.cs:372](../../src/Compliance.App/Program.cs#L372): `.AddMcpTool<GetWorkDigestPreference>(tool => tool.ReadOnly())`
- [Program.cs:373](../../src/Compliance.App/Program.cs#L373): `.AddMcpTool<SetWorkDigestPreference>()`
- [Program.cs:415](../../src/Compliance.App/Program.cs#L415): `app.MapPortiaMcp(AuthorizationDenialLog.McpPath).RequireAuthorization(ComplianceAuthorizationPolicies.ApiUser);`
- [Program.cs:418](../../src/Compliance.App/Program.cs#L418): `app.MapGet(`
- [Program.cs:424](../../src/Compliance.App/Program.cs#L424): `app.MapGet(`
- [Program.cs:441](../../src/Compliance.App/Program.cs#L441): `app.MapPost(`
- [ComplianceHealthExtensions.cs:18](../../src/Compliance.App/Hosting/Health/ComplianceHealthExtensions.cs#L18): `endpoints.MapHealthChecks(`
- [ComplianceHealthExtensions.cs:22](../../src/Compliance.App/Hosting/Health/ComplianceHealthExtensions.cs#L22): `endpoints.MapHealthChecks(`
- [ComplianceHealthExtensions.cs:26](../../src/Compliance.App/Hosting/Health/ComplianceHealthExtensions.cs#L26): `endpoints.MapHealthChecks(`

### Canonical model checklist

These approved entity names are a semantic checklist. They do not imply one aggregate, table, endpoint or projector per entity.

`Organization`, `OrganizationalUnit`, `Person`, `WorkRelationship`, `JobProfile`, `Responsibility`, `FederatedIdentity`, `PlatformUser`, `Membership`, `Team`, `AccessRole`, `Permission`, `RoleAssignment`, `SeparationOfDutyConstraint`, `SeparationOfDutyException`, `TrustedIssuer`, `PlatformOperatorGrant`, `FirmStaffMember`, `ServiceEngagement`, `EngagementAssignment`, `Application`, `ClientService`, `SystemInstance`, `Resource`, `IntegrationEndpoint`, `Device`, `DeviceComponent`, `ComputeInstance`, `NetworkInterface`, `Network`, `NetworkConnection`, `SoftwareInstallation`, `Location`, `EndpointClass`, `OperationalProcess`, `InformationAsset`, `Provider`, `DataFlow`, `Account`, `ServiceIdentity`, `Group`, `GroupMember`, `Role`, `Entitlement`, `RoleEntitlement`, `AccessAssignment`, `EffectiveAccess`, `SourceSystem`, `ExternalIdentifier`, `Observation`, `Correlation`, `Snapshot`, `IncidentReference`, `ControlRiskTreatment`, `SignificantChange`, `ComplianceIncident`.

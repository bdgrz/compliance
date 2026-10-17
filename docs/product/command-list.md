# System command list

The source-backed command plan for the product features we have committed to support. Existing commands are listed separately from required additions. Each addition is grounded in the linked P0/P1 feature requirements or an accepted M0 decision. Names describe distinct durable intents; projections, previews, reads, and derived calculations are not commands. Worker and scheduler commands are labeled as internal. P2 hypotheses and scope without accepted requirements are excluded from the required list.

## Existing commands

### AccessControl

- **AcknowledgeEngagementManagement** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/AcknowledgeEngagementManagement.cs#L6))
- **AmendServiceEngagement** — extend ([contract](../../src/Compliance.Common/Features/AccessControl/AmendServiceEngagement.cs#L6))
- **ApproveSeparationOfDutiesWaiver** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/ApproveSeparationOfDutiesWaiver.cs#L6))
- **AssignRolePermission** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/AssignRolePermission.cs#L9))
- **AssignTeamMember** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/AssignTeamMember.cs#L7))
- **AssignTeamRole** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/AssignTeamRole.cs#L7))
- **CloseServiceEngagement** — extend ([contract](../../src/Compliance.Common/Features/AccessControl/CloseServiceEngagement.cs#L6))
- **CreateServiceEngagement** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/CreateServiceEngagement.cs#L6))
- **DefineRole** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/DefineRole.cs#L7))
- **DefineTeam** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/DefineTeam.cs#L7))
- **DeleteRole** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/DeleteRole.cs#L7))
- **DeleteTeam** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/DeleteTeam.cs#L7))
- **DeprovisionMember** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/DeprovisionMember.cs#L6))
- **EvaluateClientIndependence** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/EvaluateClientIndependence.cs#L7))
- **GrantAccess** — extend ([contract](../../src/Compliance.Common/Features/AccessControl/GrantAccess.cs#L6))
- **ProposeServiceEngagementStaff** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/ProposeServiceEngagementStaff.cs#L6))
- **RecordDirectoryIndependenceReevaluation** — exists ([contract](../../src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluation.cs#L7))
- **RecordNonattestService** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RecordNonattestService.cs#L6))
- **RecordSeparationOfDutiesWaiver** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RecordSeparationOfDutiesWaiver.cs#L6))
- **RegisterFirmStaff** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RegisterFirmStaff.cs#L6))
- **RegisterMember** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RegisterMember.cs#L6))
- **ReinstateMember** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/ReinstateMember.cs#L6))
- **RemoveRolePermission** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RemoveRolePermission.cs#L10))
- **RemoveTeamMember** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RemoveTeamMember.cs#L7))
- **RemoveTeamRole** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RemoveTeamRole.cs#L7))
- **RenameRole** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/RenameRole.cs#L6))
- **ReviseIndependenceRules** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/ReviseIndependenceRules.cs#L6))
- **RevokeAccessGrant** — extend ([contract](../../src/Compliance.Common/Features/AccessControl/RevokeAccessGrant.cs#L6))
- **SetFirmStaffStatus** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/SetFirmStaffStatus.cs#L6))
- **SuspendMember** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/SuspendMember.cs#L6))
- **WithdrawServiceEngagementStaffProposal** — exists ([contract](../../src/Compliance.Common/Features/AccessControl/WithdrawServiceEngagementStaffProposal.cs#L6))

### AccessReviews

- **AcceptAccessPopulation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/AcceptAccessPopulation.cs#L10))
- **ApproveAccessExpectation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/ApproveAccessExpectation.cs#L10))
- **ClassifyAccessPrincipal** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/ClassifyAccessPrincipal.cs#L7))
- **CompleteAccessReviewCampaign** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/CompleteAccessReviewCampaign.cs#L11))
- **ExemptAccessExpectation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/ExemptAccessExpectation.cs#L10))
- **ExemptAccessRemediation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/ExemptAccessRemediation.cs#L10))
- **ExemptMissingAccessPopulation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/ExemptMissingAccessPopulation.cs#L10))
- **LaunchAccessReviewCampaign** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/LaunchAccessReviewCampaign.cs#L10))
- **OpenAccessPopulation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/OpenAccessPopulation.cs#L7))
- **ProposeAccessExpectation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/ProposeAccessExpectation.cs#L7))
- **RecordAccessDecision** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/RecordAccessDecision.cs#L10))
- **RecordAccessPopulationFacts** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/RecordAccessPopulationFacts.cs#L7))
- **RecordAccessRemediationChange** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/RecordAccessRemediationChange.cs#L7))
- **RecordBulkAccessDecision** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/RecordBulkAccessDecision.cs#L10))
- **VerifyAccessRemediation** — exists ([contract](../../src/Compliance.Common/Features/AccessReviews/VerifyAccessRemediation.cs#L7))

### Applications

- **AcceptApplicationImport** — exists ([contract](../../src/Compliance.Common/Features/Applications/AcceptApplicationImport.cs#L6))
- **AddApplicationRelationship** — exists ([contract](../../src/Compliance.Common/Features/Applications/AddApplicationRelationship.cs#L6))
- **ApplyApplicationImportEffect** — exists ([contract](../../src/Compliance.Common/Features/Applications/ApplyApplicationImportEffect.cs#L7))
- **CancelApplicationImport** — exists ([contract](../../src/Compliance.Common/Features/Applications/CancelApplicationImport.cs#L6))
- **CorrelateApplicationImportRow** — exists ([contract](../../src/Compliance.Common/Features/Applications/CorrelateApplicationImportRow.cs#L6))
- **DecideAccessReviewScope** — exists ([contract](../../src/Compliance.Common/Features/Applications/DecideAccessReviewScope.cs#L10))
- **DeclareApplication** — exists ([contract](../../src/Compliance.Common/Features/Applications/DeclareApplication.cs#L6))
- **DeclareSystemInstance** — exists ([contract](../../src/Compliance.Common/Features/Applications/DeclareSystemInstance.cs#L6))
- **ExecuteApplicationImport** — exists ([contract](../../src/Compliance.Common/Features/Applications/ExecuteApplicationImport.cs#L7))
- **FreezeApplicationImportOmissionProposal** — extend ([contract](../../src/Compliance.Common/Features/Applications/FreezeApplicationImportOmissionProposal.cs#L7))
- **RemoveApplicationRelationship** — exists ([contract](../../src/Compliance.Common/Features/Applications/RemoveApplicationRelationship.cs#L6))
- **RetireApplication** — extend ([contract](../../src/Compliance.Common/Features/Applications/RetireApplication.cs#L6))
- **RetireSystemInstance** — extend ([contract](../../src/Compliance.Common/Features/Applications/RetireSystemInstance.cs#L6))
- **ReviseApplication** — extend ([contract](../../src/Compliance.Common/Features/Applications/ReviseApplication.cs#L7))
- **ReviseApplicationMcpInput** — exists ([contract](../../src/Compliance.App/Features/Applications/ReviseApplicationMcpInput.cs#L7))
- **StageApplicationImport** — exists ([contract](../../src/Compliance.Common/Features/Applications/StageApplicationImport.cs#L6))

### Boundaries

- **ApproveBoundary** — extend ([contract](../../src/Compliance.Common/Features/Boundaries/ApproveBoundary.cs#L7))
- **CreateBoundary** — exists ([contract](../../src/Compliance.Common/Features/Boundaries/CreateBoundary.cs#L8))
- **DiscardBoundaryDraft** — exists ([contract](../../src/Compliance.Common/Features/Boundaries/DiscardBoundaryDraft.cs#L7))
- **ProposeBoundarySuccessor** — exists ([contract](../../src/Compliance.Common/Features/Boundaries/ProposeBoundarySuccessor.cs#L7))
- **ReviewBoundary** — exists ([contract](../../src/Compliance.Common/Features/Boundaries/ReviewBoundary.cs#L7))
- **ReviseBoundaryDraft** — exists ([contract](../../src/Compliance.Common/Features/Boundaries/ReviseBoundaryDraft.cs#L7))

### Commitments

- **ApproveCommitmentDraft** — extend ([contract](../../src/Compliance.Common/Features/Commitments/ApproveCommitmentDraft.cs#L12))
- **CreateCommitmentDraft** — extend ([contract](../../src/Compliance.Common/Features/Commitments/CreateCommitmentDraft.cs#L8))
- **ReviewCommitmentDraft** — extend ([contract](../../src/Compliance.Common/Features/Commitments/ReviewCommitmentDraft.cs#L13))
- **ReviseCommitmentDraft** — extend ([contract](../../src/Compliance.Common/Features/Commitments/ReviseCommitmentDraft.cs#L8))

### ControlMappings

- **ProposeControlCriterionMapping** — exists ([contract](../../src/Compliance.Common/Features/ControlMappings/ProposeControlCriterionMapping.cs#L12))
- **ProposeCriterionNotApplicable** — exists ([contract](../../src/Compliance.Common/Features/ControlMappings/ProposeCriterionNotApplicable.cs#L13))
- **RetireControlCriterionMapping** — exists ([contract](../../src/Compliance.Common/Features/ControlMappings/RetireControlCriterionMapping.cs#L9))
- **ReviewControlCriterionMapping** — exists ([contract](../../src/Compliance.Common/Features/ControlMappings/ReviewControlCriterionMapping.cs#L9))
- **ReviewCriterionApplicability** — exists ([contract](../../src/Compliance.Common/Features/ControlMappings/ReviewCriterionApplicability.cs#L13))
- **WithdrawCriterionNotApplicable** — exists ([contract](../../src/Compliance.Common/Features/ControlMappings/WithdrawCriterionNotApplicable.cs#L9))

### Controls

- **ApproveControl** — extend ([contract](../../src/Compliance.Common/Features/Controls/ApproveControl.cs#L8))
- **CreateControlDraft** — exists ([contract](../../src/Compliance.Common/Features/Controls/CreateControlDraft.cs#L8))
- **DesignateControlOwnerPerson** — exists ([contract](../../src/Compliance.Common/Features/Controls/DesignateControlOwnerPerson.cs#L13))
- **DiscardControlDraft** — exists ([contract](../../src/Compliance.Common/Features/Controls/DiscardControlDraft.cs#L8))
- **ProposeControlRetirement** — exists ([contract](../../src/Compliance.Common/Features/Controls/ProposeControlRetirement.cs#L8))
- **ProposeControlSuccessor** — extend ([contract](../../src/Compliance.Common/Features/Controls/ProposeControlSuccessor.cs#L8))
- **RetireControl** — extend ([contract](../../src/Compliance.Common/Features/Controls/RetireControl.cs#L8))
- **ReviewControl** — exists ([contract](../../src/Compliance.Common/Features/Controls/ReviewControl.cs#L8))
- **ReviseControlDraft** — exists ([contract](../../src/Compliance.Common/Features/Controls/ReviseControlDraft.cs#L8))
- **WithdrawControlProposal** — exists ([contract](../../src/Compliance.Common/Features/Controls/WithdrawControlProposal.cs#L12))

### Criteria

- **SetCriteriaTextOverlay** — exists ([contract](../../src/Compliance.Common/Features/Criteria/SetCriteriaTextOverlay.cs#L9))

### Evaluations

- **DefineControlEvaluationPlan** — exists ([contract](../../src/Compliance.Common/Features/Evaluations/DefineControlEvaluationPlan.cs#L8))
- **DisposeControlEvaluationDeviation** — exists ([contract](../../src/Compliance.Common/Features/Evaluations/DisposeControlEvaluationDeviation.cs#L9))
- **RaiseEvaluationDeviationFinding** — extend ([contract](../../src/Compliance.Common/Features/Evaluations/RaiseEvaluationDeviationFinding.cs#L11))
- **RecordControlEvaluationStep** — extend ([contract](../../src/Compliance.Common/Features/Evaluations/RecordControlEvaluationStep.cs#L12))
- **ReviewControlEvaluation** — extend ([contract](../../src/Compliance.Common/Features/Evaluations/ReviewControlEvaluation.cs#L12))
- **StartControlEvaluation** — exists ([contract](../../src/Compliance.Common/Features/Evaluations/StartControlEvaluation.cs#L12))
- **SubmitControlEvaluation** — exists ([contract](../../src/Compliance.Common/Features/Evaluations/SubmitControlEvaluation.cs#L12))

### Evidence

- **ApproveEvidenceRedaction** — extend ([contract](../../src/Compliance.Common/Features/Evidence/ApproveEvidenceRedaction.cs#L7))
- **CancelEvidenceRequest** — exists ([contract](../../src/Compliance.Common/Features/Evidence/CancelEvidenceRequest.cs#L9))
- **FulfilEvidenceRequest** — extend ([contract](../../src/Compliance.Common/Features/Evidence/FulfilEvidenceRequest.cs#L10))
- **OpenEvidenceRequest** — exists ([contract](../../src/Compliance.Common/Features/Evidence/OpenEvidenceRequest.cs#L9))
- **PrepareEvidenceRedaction** — extend ([contract](../../src/Compliance.Common/Features/Evidence/PrepareEvidenceRedaction.cs#L6))

### Operations

- **ApproveControlOperatingPlan** — extend ([contract](../../src/Compliance.Common/Features/Operations/ApproveControlOperatingPlan.cs#L12))
- **AttestControlOccurrence** — extend ([contract](../../src/Compliance.Common/Features/Operations/AttestControlOccurrence.cs#L11))
- **CorrectControlAttestation** — extend ([contract](../../src/Compliance.Common/Features/Operations/CorrectControlAttestation.cs#L8))
- **OpenControlOccurrence** — exists ([contract](../../src/Compliance.Common/Features/Operations/OpenControlOccurrence.cs#L8))
- **ProposeControlOperatingPlan** — extend ([contract](../../src/Compliance.Common/Features/Operations/ProposeControlOperatingPlan.cs#L12))
- **ReviewControlOccurrence** — extend ([contract](../../src/Compliance.Common/Features/Operations/ReviewControlOccurrence.cs#L11))

### Policies

- **ApprovePolicy** — extend ([contract](../../src/Compliance.Common/Features/Policies/ApprovePolicy.cs#L12))
- **ApprovePolicyRetirement** — extend ([contract](../../src/Compliance.Common/Features/Policies/ApprovePolicyRetirement.cs#L9))
- **ConfirmPolicyReview** — exists ([contract](../../src/Compliance.Common/Features/Policies/ConfirmPolicyReview.cs#L12))
- **CreatePolicyDraft** — exists ([contract](../../src/Compliance.Common/Features/Policies/CreatePolicyDraft.cs#L8))
- **DiscardPolicyDraft** — exists ([contract](../../src/Compliance.Common/Features/Policies/DiscardPolicyDraft.cs#L9))
- **ProposePolicyRetirement** — exists ([contract](../../src/Compliance.Common/Features/Policies/ProposePolicyRetirement.cs#L8))
- **ProposePolicySuccessor** — exists ([contract](../../src/Compliance.Common/Features/Policies/ProposePolicySuccessor.cs#L9))
- **ReviewPolicyDraft** — exists ([contract](../../src/Compliance.Common/Features/Policies/ReviewPolicyDraft.cs#L12))
- **RevisePolicyDraft** — exists ([contract](../../src/Compliance.Common/Features/Policies/RevisePolicyDraft.cs#L8))

### PolicyDistribution

- **AcknowledgePolicy** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/AcknowledgePolicy.cs#L12))
- **ApproveCampaignWaiver** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/ApproveCampaignWaiver.cs#L12))
- **CloseCampaign** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/CloseCampaign.cs#L9))
- **DefineTrainingRequirement** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/DefineTrainingRequirement.cs#L8))
- **LaunchPolicyCampaign** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/LaunchPolicyCampaign.cs#L12))
- **LaunchTrainingCampaign** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/LaunchTrainingCampaign.cs#L8))
- **ReconcileCampaignAudience** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/ReconcileCampaignAudience.cs#L12))
- **RecordTrainingCompletion** — extend ([contract](../../src/Compliance.Common/Features/PolicyDistribution/RecordTrainingCompletion.cs#L9))
- **ReviseTrainingRequirement** — exists ([contract](../../src/Compliance.Common/Features/PolicyDistribution/ReviseTrainingRequirement.cs#L9))

### Programs

- **CreateClientService** — exists ([contract](../../src/Compliance.Common/Features/Programs/CreateClientService.cs#L7))
- **CreateProgram** — exists ([contract](../../src/Compliance.Common/Features/Programs/CreateProgram.cs#L7))
- **RetireClientService** — exists ([contract](../../src/Compliance.Common/Features/Programs/RetireClientService.cs#L7))
- **ReviseClientService** — exists ([contract](../../src/Compliance.Common/Features/Programs/ReviseClientService.cs#L7))
- **ReviseProgram** — exists ([contract](../../src/Compliance.Common/Features/Programs/ReviseProgram.cs#L7))
- **SelectProgramCriteriaEdition** — exists ([contract](../../src/Compliance.Common/Features/Programs/SelectProgramCriteriaEdition.cs#L7))

### Providers

- **CloseProviderCoverageGap** — exists ([contract](../../src/Compliance.Common/Features/Providers/CloseProviderCoverageGap.cs#L8))
- **LinkProviderCoverageGapRiskAcceptance** — exists ([contract](../../src/Compliance.Common/Features/Providers/LinkProviderCoverageGapRiskAcceptance.cs#L9))
- **RecordAssuranceReport** — extend ([contract](../../src/Compliance.Common/Features/Providers/RecordAssuranceReport.cs#L8))
- **RecordProvider** — exists ([contract](../../src/Compliance.Common/Features/Providers/RecordProvider.cs#L8))
- **RecordProviderCoverageGap** — exists ([contract](../../src/Compliance.Common/Features/Providers/RecordProviderCoverageGap.cs#L7))
- **RecordProviderReview** — extend ([contract](../../src/Compliance.Common/Features/Providers/RecordProviderReview.cs#L11))
- **ReviseAssuranceReport** — exists ([contract](../../src/Compliance.Common/Features/Providers/ReviseAssuranceReport.cs#L8))
- **ReviseProvider** — exists ([contract](../../src/Compliance.Common/Features/Providers/ReviseProvider.cs#L8))

### Readiness

- **AnnotateReadinessGap** — exists ([contract](../../src/Compliance.Common/Features/Readiness/AnnotateReadinessGap.cs#L9))
- **DecideReadiness** — extend ([contract](../../src/Compliance.Common/Features/Readiness/DecideReadiness.cs#L12))
- **DecideTypeIEntry** — extend ([contract](../../src/Compliance.Common/Features/Readiness/DecideTypeIEntry.cs#L13))
- **PlanReadinessGap** — extend ([contract](../../src/Compliance.Common/Features/Readiness/PlanReadinessGap.cs#L9))
- **RunReadinessAssessment** — extend ([contract](../../src/Compliance.Common/Features/Readiness/RunReadinessAssessment.cs#L12))

### Remediation

- **AddCorrectiveAction** — exists ([contract](../../src/Compliance.Common/Features/Remediation/AddCorrectiveAction.cs#L8))
- **CloseFinding** — extend ([contract](../../src/Compliance.Common/Features/Remediation/CloseFinding.cs#L14))
- **CompleteCorrectiveAction** — extend ([contract](../../src/Compliance.Common/Features/Remediation/CompleteCorrectiveAction.cs#L9))
- **LinkFindingAcceptance** — exists ([contract](../../src/Compliance.Common/Features/Remediation/LinkFindingAcceptance.cs#L12))
- **RaiseFinding** — exists ([contract](../../src/Compliance.Common/Features/Remediation/RaiseFinding.cs#L9))
- **RaiseOccurrenceFinding** — extend ([contract](../../src/Compliance.Common/Features/Remediation/RaiseOccurrenceFinding.cs#L11))
- **ReopenFinding** — exists ([contract](../../src/Compliance.Common/Features/Remediation/ReopenFinding.cs#L8))
- **ReviseFinding** — exists ([contract](../../src/Compliance.Common/Features/Remediation/ReviseFinding.cs#L9))

### Responsibilities

- **AssignResponsibility** — extend ([contract](../../src/Compliance.Common/Features/Responsibilities/AssignResponsibility.cs#L8))
- **RevokeResponsibility** — extend ([contract](../../src/Compliance.Common/Features/Responsibilities/RevokeResponsibility.cs#L8))

### Retention

- **PlaceArtifactLegalHold** — extend ([contract](../../src/Compliance.Common/Features/Retention/PlaceArtifactLegalHold.cs#L7))
- **RecordArtifactRetentionBasis** — extend ([contract](../../src/Compliance.Common/Features/Retention/RecordArtifactRetentionBasis.cs#L7))
- **ReleaseArtifactLegalHold** — extend ([contract](../../src/Compliance.Common/Features/Retention/ReleaseArtifactLegalHold.cs#L7))

### Risks

- **AcceptRisk** — exists ([contract](../../src/Compliance.Common/Features/Risks/AcceptRisk.cs#L13))
- **AddRiskTreatmentAction** — exists ([contract](../../src/Compliance.Common/Features/Risks/AddRiskTreatmentAction.cs#L14))
- **AssignRiskOwner** — exists ([contract](../../src/Compliance.Common/Features/Risks/AssignRiskOwner.cs#L9))
- **CancelRiskTreatmentAction** — exists ([contract](../../src/Compliance.Common/Features/Risks/CancelRiskTreatmentAction.cs#L11))
- **ChooseRiskTreatment** — exists ([contract](../../src/Compliance.Common/Features/Risks/ChooseRiskTreatment.cs#L8))
- **CreateRiskDraft** — exists ([contract](../../src/Compliance.Common/Features/Risks/CreateRiskDraft.cs#L8))
- **ProposeRiskControlTreatment** — exists ([contract](../../src/Compliance.Common/Features/Risks/ProposeRiskControlTreatment.cs#L12))
- **PublishRiskMethodVersion** — exists ([contract](../../src/Compliance.Common/Features/Risks/PublishRiskMethodVersion.cs#L8))
- **RaiseRiskReassessmentTriggers** — extend ([contract](../../src/Compliance.Common/Features/Risks/RaiseRiskReassessmentTriggers.cs#L10))
- **RecordRiskAssessment** — exists ([contract](../../src/Compliance.Common/Features/Risks/RecordRiskAssessment.cs#L8))
- **RetireRiskControlTreatment** — exists ([contract](../../src/Compliance.Common/Features/Risks/RetireRiskControlTreatment.cs#L9))
- **ReviewRiskControlTreatment** — exists ([contract](../../src/Compliance.Common/Features/Risks/ReviewRiskControlTreatment.cs#L13))
- **ReviewRiskTreatmentActionCompletion** — exists ([contract](../../src/Compliance.Common/Features/Risks/ReviewRiskTreatmentActionCompletion.cs#L13))
- **ReviseRiskDraft** — exists ([contract](../../src/Compliance.Common/Features/Risks/ReviseRiskDraft.cs#L8))
- **ReviseRiskTreatmentAction** — exists ([contract](../../src/Compliance.Common/Features/Risks/ReviseRiskTreatmentAction.cs#L9))
- **SubmitRiskTreatmentActionCompletion** — exists ([contract](../../src/Compliance.Common/Features/Risks/SubmitRiskTreatmentActionCompletion.cs#L13))

### Snapshots

- **AmendProgramScopeSnapshot** — exists ([contract](../../src/Compliance.Common/Features/Snapshots/AmendProgramScopeSnapshot.cs#L8))
- **FreezeProgramScopeSnapshot** — exists ([contract](../../src/Compliance.Common/Features/Snapshots/FreezeProgramScopeSnapshot.cs#L8))

### TechnologyInventory

- **RecordDataFlow** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/RecordDataFlow.cs#L7))
- **RecordInformationAsset** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/RecordInformationAsset.cs#L7))
- **RecordLocation** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/RecordLocation.cs#L7))
- **RecordOperationalProcess** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/RecordOperationalProcess.cs#L7))
- **RecordTechnologyComponent** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/RecordTechnologyComponent.cs#L7))
- **ReviseDataFlow** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/ReviseDataFlow.cs#L7))
- **ReviseInformationAsset** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/ReviseInformationAsset.cs#L6))
- **ReviseLocation** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/ReviseLocation.cs#L7))
- **ReviseOperationalProcess** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/ReviseOperationalProcess.cs#L6))
- **ReviseTechnologyComponent** — exists ([contract](../../src/Compliance.Common/Features/TechnologyInventory/ReviseTechnologyComponent.cs#L7))

### Tenants

- **AcceptTenantInvitation** — exists ([contract](../../src/Compliance.Common/Features/Tenants/AcceptTenantInvitation.cs#L6))
- **ActivateTenant** — exists ([contract](../../src/Compliance.Common/Features/Tenants/ActivateTenant.cs#L6))
- **ChangeTenantSlug** — exists ([contract](../../src/Compliance.Common/Features/Tenants/ChangeTenantSlug.cs#L6))
- **ConfirmTenantSlug** — exists ([contract](../../src/Compliance.Common/Features/Tenants/ConfirmTenantSlug.cs#L6))
- **ConfirmTenantSlugSurrender** — exists ([contract](../../src/Compliance.Common/Features/Tenants/ConfirmTenantSlugSurrender.cs#L6))
- **GrantPlatformOperator** — exists ([contract](../../src/Compliance.Common/Features/Tenants/GrantPlatformOperator.cs#L6))
- **InviteOrganizationMember** — extend ([contract](../../src/Compliance.Common/Features/Tenants/InviteOrganizationMember.cs#L7))
- **InviteTenantMember** — exists ([contract](../../src/Compliance.Common/Features/Tenants/InviteTenantMember.cs#L6))
- **OffboardTenant** — extend ([contract](../../src/Compliance.Common/Features/Tenants/OffboardTenant.cs#L6))
- **ReactivateTenant** — exists ([contract](../../src/Compliance.Common/Features/Tenants/ReactivateTenant.cs#L6))
- **RegisterTenant** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RegisterTenant.cs#L10))
- **RegisterTenantOwner** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RegisterTenantOwner.cs#L6))
- **RegisterTenantSlug** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RegisterTenantSlug.cs#L6))
- **RejectTenantSlug** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RejectTenantSlug.cs#L6))
- **RejectTenantSlugSurrender** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RejectTenantSlugSurrender.cs#L6))
- **RequestTenantSlugSurrender** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RequestTenantSlugSurrender.cs#L6))
- **RevokePlatformOperator** — exists ([contract](../../src/Compliance.Common/Features/Tenants/RevokePlatformOperator.cs#L6))
- **SeedPlatformOperatorRoster** — exists ([contract](../../src/Compliance.Common/Features/Tenants/SeedPlatformOperatorRoster.cs#L5))
- **SurrenderTenantSlug** — exists ([contract](../../src/Compliance.Common/Features/Tenants/SurrenderTenantSlug.cs#L6))
- **SuspendTenant** — exists ([contract](../../src/Compliance.Common/Features/Tenants/SuspendTenant.cs#L6))

### UserIdentities

- **CompleteEmailChallenge** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/CompleteEmailChallenge.cs#L6))
- **CompleteIdentityRecovery** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/CompleteIdentityRecovery.cs#L6))
- **ContinueWithDeveloperIdentity** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/ContinueWithDeveloperIdentity.cs#L7))
- **ContinueWithOidcProvider** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/ContinueWithOidcProvider.cs#L7))
- **IssueEmailChallenge** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/IssueEmailChallenge.cs#L6))
- **LinkOidcProviderIdentity** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/LinkOidcProviderIdentity.cs#L7))
- **ReserveEmail** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/ReserveEmail.cs#L6))
- **StartIdentityRecovery** — exists ([contract](../../src/Compliance.Common/Features/UserIdentities/StartIdentityRecovery.cs#L6))

### Work

- **AssignWorkItem** — extend ([contract](../../src/Compliance.Common/Features/Work/AssignWorkItem.cs#L8))
- **ClaimWorkItem** — extend ([contract](../../src/Compliance.Common/Features/Work/ClaimWorkItem.cs#L8))
- **DelegateWorkItem** — extend ([contract](../../src/Compliance.Common/Features/Work/DelegateWorkItem.cs#L8))
- **EscalateWorkItem** — extend ([contract](../../src/Compliance.Common/Features/Work/EscalateWorkItem.cs#L8))
- **SetWorkDigestPreference** — exists ([contract](../../src/Compliance.Common/Features/Work/SetWorkDigestPreference.cs#L8))

### Workforce

- **AmendWorkforceRosterSnapshot** — exists ([contract](../../src/Compliance.Common/Features/Workforce/AmendWorkforceRosterSnapshot.cs#L12))
- **CorrelatePersonMembership** — exists ([contract](../../src/Compliance.Common/Features/Workforce/CorrelatePersonMembership.cs#L13))
- **FreezeWorkforceRosterSnapshot** — exists ([contract](../../src/Compliance.Common/Features/Workforce/FreezeWorkforceRosterSnapshot.cs#L9))
- **ReconcileWorkforceSourceObservation** — exists ([contract](../../src/Compliance.Common/Features/Workforce/ReconcileWorkforceSourceObservation.cs#L12))
- **RecordPerson** — exists ([contract](../../src/Compliance.Common/Features/Workforce/RecordPerson.cs#L8))
- **RecordServiceIdentity** — exists ([contract](../../src/Compliance.Common/Features/Workforce/RecordServiceIdentity.cs#L13))
- **RecordWorkRelationship** — exists ([contract](../../src/Compliance.Common/Features/Workforce/RecordWorkRelationship.cs#L11))
- **RecordWorkforceSourceObservation** — exists ([contract](../../src/Compliance.Common/Features/Workforce/RecordWorkforceSourceObservation.cs#L8))
- **ResolveWorkforceObservation** — exists ([contract](../../src/Compliance.Common/Features/Workforce/ResolveWorkforceObservation.cs#L14))
- **RevisePerson** — exists ([contract](../../src/Compliance.Common/Features/Workforce/RevisePerson.cs#L7))
- **ReviseServiceIdentity** — exists ([contract](../../src/Compliance.Common/Features/Workforce/ReviseServiceIdentity.cs#L9))
- **ReviseWorkRelationship** — exists ([contract](../../src/Compliance.Common/Features/Workforce/ReviseWorkRelationship.cs#L8))

### Internal mutations and transport action

- **EvidenceIntake.CaptureAsync** — internal, existing ([source](../../src/Compliance.Core/Features/Evidence/EvidenceIntake.cs))
- **EvidenceArtifact.Register** — internal, existing ([source](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs))
- **EvidenceArtifact.RecordInspection** — internal, existing ([source](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs))
- **EvidenceArtifact.ReleaseQuarantine** — internal, existing ([source](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs))
- **IndependenceLedger.AcceptEngagement** — internal, existing ([source](../../src/Compliance.Core/Features/AccessControl/IndependenceLedger.Acceptance.cs))
- **IndependenceLedger.RemoveActualStaff** — internal, existing ([source](../../src/Compliance.Core/Features/AccessControl/IndependenceLedger.Revocation.cs))
- **Browser session logout** — existing `POST /auth/logout` action.

## Required command additions

The list below maps directly to accepted feature requirements. P0/P1 manual outcomes are included even when a later slice has prerequisite work; P2 hypotheses and proposed BO scope are not treated as committed commands. A worker result is listed only when a feature requires a durable outcome or replayable external effect.

This pruned plan has 127 required additions, including five evidence command contracts already present in source; 122 required command contracts remain to be created. The 230 existing command contracts are listed above. **Existing + required additions** is the complete current command plan.

### Governed evidence capture and support — [EN-06](backlog.md#en-06-store-governed-artifacts-with-immutable-content-identity), [R2-03](backlog.md#r2-03-request-and-capture-trustworthy-evidence), [R2-04](backlog.md#r2-04-perform-a-control-and-attest-to-the-result), [R2-05](backlog.md#r2-05-review-control-design-implementation-and-evidence), [R1-14](backlog.md#r1-14-evaluate-vendors-and-subservice-organizations)

- **CaptureEvidenceArtifact** — contract created ([draft contract](../../src/Compliance.Common/Features/Evidence/CaptureEvidenceArtifact.cs)); handler, API exposure, and upload storage boundary remain to be wired
- **RecordEvidenceArtifactInspection** — contract created ([internal contract](../../src/Compliance.Common/Features/Evidence/RecordEvidenceArtifactInspection.cs)); trusted inspection handler and durable worker remain to be wired
- **IssueArtifactDownload** — contract created ([draft contract](../../src/Compliance.Common/Features/Evidence/IssueArtifactDownload.cs)); handler must enforce available-state and exact artifact authorization
- **LinkEvidenceSupport** — contract created ([draft contract](../../src/Compliance.Common/Features/Evidence/LinkEvidenceSupport.cs)); aggregate, handler, and exact support type validation remain to be wired
- **ReleaseEvidenceArtifactQuarantine** — contract created ([draft contract](../../src/Compliance.Common/Features/Evidence/ReleaseEvidenceArtifactQuarantine.cs)); handler must require a clean rescan and admin authority

### Evidence handling, disclosure, holds and disposition — [R2-12](backlog.md#r2-12-govern-evidence-access-retention-and-disclosure), EN-06; [M0-D16](decisions/m0-d16-evidence-handling.md)

- **AssignArtifactHandlingPolicy** — coded ([contract](../../src/Compliance.Common/Features/Retention/AssignArtifactHandlingPolicy.cs#L7))
- **PlaceArtifactEngagementHold** — coded ([contract](../../src/Compliance.Common/Features/Retention/PlaceArtifactEngagementHold.cs#L7))
- **RecordEngagementReportIssued** — coded ([contract](../../src/Compliance.Common/Features/Retention/RecordEngagementReportIssued.cs#L7))
- **ReleaseArtifactEngagementHold** — coded ([contract](../../src/Compliance.Common/Features/Retention/ReleaseArtifactEngagementHold.cs#L7))
- **ApproveArtifactDisclosure** — coded ([contract](../../src/Compliance.Common/Features/Retention/ApproveArtifactDisclosure.cs#L7))
- **RecordArtifactDisclosureDelivery** — coded ([contract](../../src/Compliance.Common/Features/Retention/RecordArtifactDisclosureDelivery.cs#L7))
- **RevokeArtifactDisclosure** — coded ([contract](../../src/Compliance.Common/Features/Retention/RevokeArtifactDisclosure.cs#L7))
- **ApproveArtifactDisposition** — coded ([contract](../../src/Compliance.Common/Features/Retention/ApproveArtifactDisposition.cs#L7))
- **ExecuteArtifactDisposition** — coded ([contract](../../src/Compliance.Common/Features/Retention/ExecuteArtifactDisposition.cs#L7))
- **RecordArtifactDispositionResult** — coded ([contract](../../src/Compliance.Common/Features/Retention/RecordArtifactDispositionResult.cs#L7))

### Advisor handoffs and feedback — [R2-08](backlog.md#r2-08-collaborate-with-a-readiness-advisor-on-work-in-progress); [M0-D14](decisions/m0-d14-advisor-collaboration.md)

- **CreateAdvisorHandoff** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/CreateAdvisorHandoff.cs#L7))
- **ReviseAdvisorHandoffSelection** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/ReviseAdvisorHandoffSelection.cs#L7))
- **MarkAdvisorHandoffReadyForReview** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/MarkAdvisorHandoffReadyForReview.cs#L7))
- **CloseAdvisorHandoff** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/CloseAdvisorHandoff.cs#L7))
- **RevokeAdvisorHandoff** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/RevokeAdvisorHandoff.cs#L7))
- **RecordAdvisorFeedback** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/RecordAdvisorFeedback.cs#L7))
- **CorrectAdvisorFeedback** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/CorrectAdvisorFeedback.cs#L7))
- **RespondToAdvisorFeedback** — coded ([contract](../../src/Compliance.Common/Features/AdvisorCollaboration/RespondToAdvisorFeedback.cs#L7))


### Control operation and recurrence — [R2-01](backlog.md#r2-01-assign-control-ownership-and-operating-cadence), [T2-02](backlog.md#t2-02-operate-a-recurring-control-calendar), R2-04, T2-03; [M0-D19](decisions/m0-d19-periodic-reviews.md)

- **GenerateDueControlOccurrences** — coded ([contract](../../src/Compliance.Common/Features/Operations/GenerateDueControlOccurrences.cs#L7))

### Inventory and commitment lifecycle — [R1-10](backlog.md#r1-10-establish-the-application-inventory-and-review-scope), [R1-13](backlog.md#r1-13-record-service-commitments-system-requirements-and-user-responsibilities)

- **ReviseSystemInstance** — required by R1-10 application/system inventory change workflow
- **RetireCommitment** — coded ([contract](../../src/Compliance.Common/Features/Commitments/RetireCommitment.cs#L7)); required by R1-13 changed, superseded, withdrawn, and historically applicable commitment lifecycle

### Readiness-material import — [R1-09](backlog.md#r1-09-bring-existing-readiness-work-into-the-program), [EN-05](backlog.md#en-05-import-with-preview-reconciliation-and-safe-replay) (deferred until prerequisites are met)

- **StageReadinessMaterialImport** — coded ([contract](../../src/Compliance.Common/Features/Applications/StageReadinessMaterialImport.cs#L7))
- **ResolveReadinessMaterialImportRow** — coded ([contract](../../src/Compliance.Common/Features/Applications/ResolveReadinessMaterialImportRow.cs#L7))
- **AcceptReadinessMaterialImport** — coded ([contract](../../src/Compliance.Common/Features/Applications/AcceptReadinessMaterialImport.cs#L7))
- **CancelReadinessMaterialImport** — coded ([contract](../../src/Compliance.Common/Features/Applications/CancelReadinessMaterialImport.cs#L7))
- **ExecuteReadinessMaterialImport** — coded ([contract](../../src/Compliance.Common/Features/Applications/ExecuteReadinessMaterialImport.cs#L7))



### Work queue and digest delivery — [R2-11](backlog.md#r2-11-manage-accountable-compliance-work); [M0-D15](decisions/m0-d15-work-queue.md)

- **ScheduleWeeklyWorkDigest** — required internal scheduler command for the opt-out weekly digest
- **DeliverWeeklyWorkDigest** — required internal delivery command; delivery failure never gates the queue
- **RecordWorkDigestDelivery** — required internal command to retain delivery outcome and retry identity

### Type I engagement and baseline — [T1-01](backlog.md#t1-01-establish-the-type-i-engagement-and-point-in-time-snapshot)

- **CreateExaminationEngagement** — coded ([contract](../../src/Compliance.Common/Features/Examinations/CreateExaminationEngagement.cs#L7))
- **ReviseExaminationEngagement** — coded ([contract](../../src/Compliance.Common/Features/Examinations/ReviseExaminationEngagement.cs#L7))
- **FreezeTypeIBaseline** — coded ([contract](../../src/Compliance.Common/Features/Examinations/FreezeTypeIBaseline.cs#L7))
- **AmendTypeIBaseline** — coded ([contract](../../src/Compliance.Common/Features/Examinations/AmendTypeIBaseline.cs#L7))

### Management-authored system description — [T1-02](backlog.md#t1-02-author-approve-and-maintain-the-system-description), T2-07, T3-01/05/06; [M0-D21](decisions/m0-d21-significant-change.md)

- **CreateSystemDescriptionDraft** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/CreateSystemDescriptionDraft.cs#L7))
- **ReviseSystemDescriptionDraft** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/ReviseSystemDescriptionDraft.cs#L7))
- **BindSystemDescriptionSources** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/BindSystemDescriptionSources.cs#L7))
- **ReviewSystemDescription** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/ReviewSystemDescription.cs#L7))
- **ApproveSystemDescription** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/ApproveSystemDescription.cs#L7))
- **ProposeSystemDescriptionSuccessor** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/ProposeSystemDescriptionSuccessor.cs#L7))
- **RecordSystemDescriptionNoChangeDecision** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/RecordSystemDescriptionNoChangeDecision.cs#L7))
- **BindSystemDescriptionToExamination** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/BindSystemDescriptionToExamination.cs#L7))
- **GenerateSystemDescriptionExport** — coded ([contract](../../src/Compliance.Common/Features/SystemDescription/GenerateSystemDescriptionExport.cs#L7))

### Auditor requests, responses and samples — [T1-04](backlog.md#t1-04-respond-to-auditor-requests-and-samples), [T3-03](backlog.md#t3-03-fulfill-type-ii-sample-and-evidence-requests); [M0-D17](decisions/m0-d17-audit-deliverables.md), [M0-D27](decisions/m0-d27-attest-scope.md)

- **RecordAuditorRequest** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/RecordAuditorRequest.cs#L7))
- **ReviseAuditorRequest** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/ReviseAuditorRequest.cs#L7))
- **AssignAuditorRequest** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/AssignAuditorRequest.cs#L7))
- **RecordAuditorRequestComment** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/RecordAuditorRequestComment.cs#L7))
- **CreateAuditResponseDraft** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/CreateAuditResponseDraft.cs#L7))
- **ReviseAuditResponseDraft** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/ReviseAuditResponseDraft.cs#L7))
- **SubmitAuditResponse** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/SubmitAuditResponse.cs#L7))
- **ReviewAuditResponse** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/ReviewAuditResponse.cs#L7))
- **DeliverAuditResponse** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/DeliverAuditResponse.cs#L7))
- **RecordAuditorResponseDisposition** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/RecordAuditorResponseDisposition.cs#L7))
- **ResolveAuditorRequest** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/ResolveAuditorRequest.cs#L7))
- **ReopenAuditorRequest** — coded ([contract](../../src/Compliance.Common/Features/AuditorRequests/ReopenAuditorRequest.cs#L7))
- **RecordSampleSelection** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/RecordSampleSelection.cs#L7))
- **AmendSampleSelection** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/AmendSampleSelection.cs#L7))

### Indexed audit packages — [T1-05](backlog.md#t1-05-produce-and-validate-the-type-i-package), [T3-05](backlog.md#t3-05-produce-and-validate-the-type-ii-package); [M0-D17](decisions/m0-d17-audit-deliverables.md)

- **RequestAuditPackageBuild** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/RequestAuditPackageBuild.cs#L7))
- **RecordAuditPackageBuildResult** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/RecordAuditPackageBuildResult.cs#L7))
- **ValidateAuditPackage** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/ValidateAuditPackage.cs#L7))
- **ApproveAuditPackageValidationOverride** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/ApproveAuditPackageValidationOverride.cs#L7))
- **ApproveAuditPackageDisclosure** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/ApproveAuditPackageDisclosure.cs#L7))
- **RecordAuditPackageDelivery** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/RecordAuditPackageDelivery.cs#L7))
- **AmendAuditPackage** — coded ([contract](../../src/Compliance.Common/Features/AuditPackages/AmendAuditPackage.cs#L7))

### Assertions, representation letters, outcomes and Type II plan — [T1-07](backlog.md#t1-07-record-the-type-i-outcome-and-approve-the-type-ii-plan), [T3-06](backlog.md#t3-06-record-management-sign-off-and-the-type-ii-outcome); [M0-D27](decisions/m0-d27-attest-scope.md)

- **RecordManagementAssertion** — coded ([contract](../../src/Compliance.Common/Features/Examinations/RecordManagementAssertion.cs#L7))
- **ApproveManagementAssertion** — coded ([contract](../../src/Compliance.Common/Features/Examinations/ApproveManagementAssertion.cs#L7))
- **RecordManagementRepresentationLetter** — coded ([contract](../../src/Compliance.Common/Features/Examinations/RecordManagementRepresentationLetter.cs#L7))
- **SignManagementRepresentationLetter** — coded ([contract](../../src/Compliance.Common/Features/Examinations/SignManagementRepresentationLetter.cs#L7))
- **RecordExaminationOutcome** — coded ([contract](../../src/Compliance.Common/Features/Examinations/RecordExaminationOutcome.cs#L7))
- **RecordManagementExaminationReview** — coded ([contract](../../src/Compliance.Common/Features/Examinations/RecordManagementExaminationReview.cs#L7))
- **ProposeTypeIIOperatingPlan** — coded ([contract](../../src/Compliance.Common/Features/Operations/ProposeTypeIIOperatingPlan.cs#L7))
- **ReviewTypeIIOperatingPlan** — coded ([contract](../../src/Compliance.Common/Features/Operations/ReviewTypeIIOperatingPlan.cs#L7))
- **ApproveTypeIIOperatingPlan** — coded ([contract](../../src/Compliance.Common/Features/Operations/ApproveTypeIIOperatingPlan.cs#L7))

### Observation periods and continuous operation — [T2-01](backlog.md#t2-01-start-the-type-ii-observation-period-from-the-approved-baseline), [T3-07](backlog.md#t3-07-roll-the-program-into-continuous-compliance-and-the-next-period)

- **StartTypeIIObservationPeriod** — coded ([contract](../../src/Compliance.Common/Features/Operations/StartTypeIIObservationPeriod.cs#L7))
- **AmendTypeIIObservationPeriod** — coded ([contract](../../src/Compliance.Common/Features/Operations/AmendTypeIIObservationPeriod.cs#L7))
- **ProposeProgramRollover** — coded ([contract](../../src/Compliance.Common/Features/Programs/ProposeProgramRollover.cs#L7))
- **ApproveProgramRollover** — coded ([contract](../../src/Compliance.Common/Features/Programs/ApproveProgramRollover.cs#L7))

### Native significant changes and incidents — [T2-07](backlog.md#t2-07-assess-system-changes-and-incidents-for-compliance-impact), [M0-D21](decisions/m0-d21-significant-change.md)

- **RecordSignificantChange** — coded ([contract](../../src/Compliance.Common/Features/Changes/RecordSignificantChange.cs#L7))
- **ReviseSignificantChange** — coded ([contract](../../src/Compliance.Common/Features/Changes/ReviseSignificantChange.cs#L7))
- **AssessSignificantChange** — coded ([contract](../../src/Compliance.Common/Features/Changes/AssessSignificantChange.cs#L7))
- **RecordComplianceIncident** — coded ([contract](../../src/Compliance.Common/Features/Incidents/RecordComplianceIncident.cs#L7))
- **ReviseComplianceIncident** — coded ([contract](../../src/Compliance.Common/Features/Incidents/ReviseComplianceIncident.cs#L7))
- **AssessComplianceIncident** — coded ([contract](../../src/Compliance.Common/Features/Incidents/AssessComplianceIncident.cs#L7))
- **RecordComplianceIncidentResolution** — coded ([contract](../../src/Compliance.Common/Features/Incidents/RecordComplianceIncidentResolution.cs#L7))

### Period close and examination populations — [T3-01](backlog.md#t3-01-close-the-observation-period-and-freeze-the-examination-snapshot), [T3-02](backlog.md#t3-02-demonstrate-complete-audit-populations); [M0-D17](decisions/m0-d17-audit-deliverables.md)

- **ApproveTypeIIPeriodClose** — coded ([contract](../../src/Compliance.Common/Features/Examinations/ApproveTypeIIPeriodClose.cs#L7))
- **FreezeTypeIIPeriodClose** — coded ([contract](../../src/Compliance.Common/Features/Examinations/FreezeTypeIIPeriodClose.cs#L7))
- **AmendTypeIIPeriodClose** — coded ([contract](../../src/Compliance.Common/Features/Examinations/AmendTypeIIPeriodClose.cs#L7))
- **DefineAuditPopulation** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/DefineAuditPopulation.cs#L7))
- **ReviseAuditPopulationDefinition** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/ReviseAuditPopulationDefinition.cs#L7))
- **RecordAuditPopulationFacts** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/RecordAuditPopulationFacts.cs#L7))
- **ClassifyAuditPopulationRow** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/ClassifyAuditPopulationRow.cs#L7))
- **ReconcileAuditPopulation** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/ReconcileAuditPopulation.cs#L7))
- **GenerateAuditPopulation** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/GenerateAuditPopulation.cs#L7))
- **FreezeAuditPopulation** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/FreezeAuditPopulation.cs#L7))
- **AmendAuditPopulation** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/AmendAuditPopulation.cs#L7))
- **GenerateAuditPopulationExport** — coded ([contract](../../src/Compliance.Common/Features/AuditPopulations/GenerateAuditPopulationExport.cs#L7))

### Professional authority and actual assignments — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md)

- **AcceptServiceEngagement** — extend existing internal behavior
- **AssignServiceEngagementStaff** — coded ([contract](../../src/Compliance.Common/Features/AccessControl/AssignServiceEngagementStaff.cs#L7))
- **RevokeServiceEngagementStaff** — extend existing internal behavior

### Independence authority — [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks), [F1-08](backlog.md#f1-08-enforce-independence-walls-between-advisory-and-attest-work); [M0-D26](decisions/m0-d26-independence.md)

- **RatifyIndependenceRuleVersion** — required partner ratification before production acceptance can use a rule version
- **RecordPartnerIndependenceEvaluation** — required personal evaluation when the accepted rules classify readiness-only service as conditionally compatible

### Own-attest delivery and organization offboarding — [F1-01](backlog.md#f1-01-onboard-suspend-and-offboard-client-organizations), [F1-07](backlog.md#f1-07-record-client-engagements-and-accept-them-after-independence-checks); [M0-D27](decisions/m0-d27-attest-scope.md)

- **RequestClientExport** — coded ([contract](../../src/Compliance.Common/Features/Exports/RequestClientExport.cs#L7))
- **RecordClientExportBuildResult** — coded ([contract](../../src/Compliance.Common/Features/Exports/RecordClientExportBuildResult.cs#L7))
- **RecordClientExportDelivery** — coded ([contract](../../src/Compliance.Common/Features/Exports/RecordClientExportDelivery.cs#L7))
- **RequestOwnAttestRecordPackage** — coded ([contract](../../src/Compliance.Common/Features/AttestRecords/RequestOwnAttestRecordPackage.cs#L7))
- **RecordOwnAttestRecordPackageResult** — coded ([contract](../../src/Compliance.Common/Features/AttestRecords/RecordOwnAttestRecordPackageResult.cs#L7))
- **RecordOwnAttestRecordDelivery** — coded ([contract](../../src/Compliance.Common/Features/AttestRecords/RecordOwnAttestRecordDelivery.cs#L7))
- **CompleteTenantOffboarding** — coded ([contract](../../src/Compliance.Common/Features/Tenants/CompleteTenantOffboarding.cs#L7))

### Client-specific federation — [F1-06](backlog.md#f1-06-let-client-users-sign-in-through-their-own-identity-providers), [EN-01](backlog.md#en-01-provide-shared-authorization-and-actor-reference-foundations), [M0-A07](../architecture/decisions/0009-tenant-identity-federation-and-context.md)

- **ConfigureTenantIdentityProvider** — coded ([contract](../../src/Compliance.Common/Features/UserIdentities/ConfigureTenantIdentityProvider.cs#L7))
- **TestTenantIdentityProvider** — coded ([contract](../../src/Compliance.Common/Features/UserIdentities/TestTenantIdentityProvider.cs#L7))
- **DisableTenantIdentityProvider** — coded ([contract](../../src/Compliance.Common/Features/UserIdentities/DisableTenantIdentityProvider.cs#L7))
- **RotateTenantIdentityProviderCredentials** — coded ([contract](../../src/Compliance.Common/Features/UserIdentities/RotateTenantIdentityProviderCredentials.cs#L7))

### Methodology templates — [F1-04](backlog.md#f1-04-maintain-a-reusable-template-library-and-apply-it-to-clients)

- **CreateMethodologyTemplate** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/CreateMethodologyTemplate.cs#L7))
- **ReviseMethodologyTemplateDraft** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ReviseMethodologyTemplateDraft.cs#L7))
- **ReviewMethodologyTemplate** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ReviewMethodologyTemplate.cs#L7))
- **ApproveMethodologyTemplateVersion** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ApproveMethodologyTemplateVersion.cs#L7))
- **RetireMethodologyTemplate** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/RetireMethodologyTemplate.cs#L7))
- **ApplyMethodologyTemplate** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ApplyMethodologyTemplate.cs#L7))
- **ProposeTemplateSuccessorAdoption** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ProposeTemplateSuccessorAdoption.cs#L7))
- **RecordTemplateSuccessorDisposition** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/RecordTemplateSuccessorDisposition.cs#L7))
- **ProposeDeidentifiedTemplateContribution** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ProposeDeidentifiedTemplateContribution.cs#L7))
- **ReviewTemplateContribution** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/ReviewTemplateContribution.cs#L7))
- **AcceptTemplateContribution** — coded ([contract](../../src/Compliance.Common/Features/MethodologyTemplates/AcceptTemplateContribution.cs#L7))

### Excluded from the required command list

These are not current command requirements: direct external-auditor accounts (T1-03, P2 hypothesis); connector/customer-hosted collection commands (T2-08, P2 hypotheses under M0-D20); firm-capacity planning commands (F1-03, P2 pending firm validation); BO-01/02/03 organization handover, billing, and add-on operations (proposed scope with unsatisfied policy/readiness gates); and Privacy lifecycle operations (requirements not yet validated under #349). Query-only portfolio and cross-client queue features do not create commands. Periodic policy, risk, provider, and management reviews reuse control occurrences and existing review/finding workflows under M0-D19.

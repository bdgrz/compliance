# Command inventory and gap analysis

Baseline: local backend commit `1f9bb3c9b9ae3459f3ca563a4355c5b6a6187689`, inspected 2026-10-17. This is a command-only design and static implementation comparison; it does not report GitHub issue completion or production release status.

## Result

- **229 existing domain/infrastructure request commands**, each with a handler and production handler registration. Their complete names, contract links, handlers, HTTP exposure and test-reference evidence are listed below.
- **1 transport adapter**, listed separately to avoid double-counting its underlying business command.
- **6 additional internal mutation entry points**, plus browser logout, are listed separately. They are not six additional complete public workflows.
- **25 gap areas**, covering accepted manual workflows, incomplete existing commands, unresolved policies and optional collection. Every EN/R1/R2/T1/T2/T3/F1/BO story has a reconciliation row.
- The earlier broad candidate inventory is not the implementation plan. It mixed required P0/P1 workflow actions with P2 hypotheses, policy-gated ideas, and internal operations. Use the pruned, requirement-linked [system command list](command-list.md) as the required command plan; the remaining names in this gap analysis are not automatically required. See [system layer coverage](system-layer-coverage.md) for source inventory counts.

## Scope and evidence

The baseline is the local accepted backlog/decisions and current source. Where an older feature note differs from current code, current code governs existence: for example readiness-rules/12 already assesses effective commitments, while workforce completeness and evidence remain explicit unassessed families. No claim here relies on historical issue status. Exact command names and transaction boundaries for missing workflows will be settled in their consuming slices. Privacy lifecycle scope (#349) remains an explicit unresolved requirement, not an invented list.

## Status and counting rules

The named candidates without a current request contract comprise **116 Missing**, **5 Partial** (internal behavior exists), **42 Decision**, and **15 Hypothesis** entries. These 178 names are distinct; the additional unnamed Privacy lifecycle scope remains G25. Existing requests with incomplete consumers are separately marked Partial in the source list and are not counted again as absent names.

| Status | Meaning |
| --- | --- |
| Exists | Contract, handler and production registration exist for the named intent; this does not certify the entire parent workflow or correctness. |
| Partial | Some behavior/domain mutation already exists, but a documented or source-backed requirement/consumer/exposure is missing. |
| Missing | No corresponding complete command workflow was found for an accepted required write outcome. Candidate names remain provisional. |
| Decision | A required policy/authority or contract-shape decision is unresolved; do not invent it or label an arbitrary command name mandatory. |
| Hypothesis | Optional P2 scope, outside the accepted manual baseline until validated. |
| Reuse | Existing commands own the write; the story needs composition, reads or consumers rather than a duplicate command. |

Commands here are business/system intents, not shell commands, queries, events or aggregate type names. A POST can be a read: GetIdentityRecoveryOptions is excluded. VerifyProgramScopeSnapshot and the manifest-regeneration GET operations are read/reconstruction behavior and excluded. EvaluateClientIndependence and RunReadinessAssessment are included because they retain results. Aggregate methods invoked by an existing request are implementation details, not separately counted business commands; unexposed evidence and professional mutations are shown because they reveal partial workflows.

Test links below identify source references, not tests run in this audit or proof of every invariant. “No direct request reference” means the request type name was absent from test source; aggregate-level coverage may still exist. HTTP exposure is separate from command existence: internal reactor/bootstrap commands do not need public endpoints. MCP exposure requires its own reviewed human-versus-machine boundary and is not inferred from ICallable.

## Most consequential gaps

1. **Governed evidence is partial**: internal capture exists, but public upload/download, qualified scanner composition, contextual support and full disclosure/disposition remain unfinished (G01/G02).
2. **Advisor collaboration is missing**: existing memberships/annotations do not provide exact-version handoff and feedback lifecycles (G03).
3. **Readiness and operation have incomplete consumers**: existing command names are reusable, but source coverage, real evidence and recurring materialization remain gaps (G04/G05/G07/G08).
4. **The examination command chain is missing**: examination engagement → complete baseline → system description → requests/samples/packages → assertion/letter/outcome → operating period → close/populations → rollover (G09–G16).
5. **Professional access is partial**: internal acceptance/removal exists, but real partner/rule authority and public acceptance/actual-assignment workflows remain unfinished (G17/G18).
6. **Imports, federation and templates need bounded additions** (G06/G19/G20). **Commerce needs policy decisions first** (G21–G23); optional connectors/agents remain hypotheses (G24). Selected Privacy lifecycle commands await the validated story set (G25).

## Full existing request-command list

Every row below has a concrete contract and handler. “HTTP” identifies an explicit host registration; “internal” means no direct HTTP command route. Existing names are preserved even when canonical vocabulary differs. Gap links describe incomplete behavior, not a reason to introduce a duplicate command.

### AccessControl — R1-04, EN-01/04, F1-03/07/08

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AcknowledgeEngagementManagement](../../src/Compliance.Common/Features/AccessControl/AcknowledgeEngagementManagement.cs#L6) | [AcknowledgeEngagementManagementHandler](../../src/Compliance.Core/Features/AccessControl/AcknowledgeEngagementManagementHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L622) | Exists | [AssignmentIndependenceReevaluationTests](../../test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs) (+7) |
| [AmendServiceEngagement](../../src/Compliance.Common/Features/AccessControl/AmendServiceEngagement.cs#L6) | [AmendServiceEngagementHandler](../../src/Compliance.Core/Features/AccessControl/AmendServiceEngagementHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L602) | Partial / [G17](#g17) | [FirmOperationsCompositionTests](../../test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs) |
| [ApproveSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/ApproveSeparationOfDutiesWaiver.cs#L6) | [ApproveSeparationOfDutiesWaiverHandler](../../src/Compliance.Core/Features/AccessControl/ApproveSeparationOfDutiesWaiverHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2087) | Exists | [EvidenceRedactionCompositionTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRedactionCompositionTests.cs) (+3) |
| [AssignRolePermission](../../src/Compliance.Common/Features/AccessControl/AssignRolePermission.cs#L9) | [AssignRolePermissionHandler](../../src/Compliance.Core/Features/AccessControl/AssignRolePermissionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2156) | Exists | [ApplicationInventoryGrantBackfillReactorTests](../../test/Compliance.Tests/Features/AccessControl/ApplicationInventoryGrantBackfillReactorTests.cs) (+9) |
| [AssignTeamMember](../../src/Compliance.Common/Features/AccessControl/AssignTeamMember.cs#L7) | [AssignTeamMemberHandler](../../src/Compliance.Core/Features/AccessControl/AssignTeamMemberHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2129) | Exists | [TenantManagerInvariantTests](../../test/Compliance.Tests/Features/AccessControl/TenantManagerInvariantTests.cs) (+3) |
| [AssignTeamRole](../../src/Compliance.Common/Features/AccessControl/AssignTeamRole.cs#L7) | [AssignTeamRoleHandler](../../src/Compliance.Core/Features/AccessControl/AssignTeamRoleHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2138) | Exists | [BuiltInRoleCatalogMigrationReactorTests](../../test/Compliance.Tests/Features/AccessControl/BuiltInRoleCatalogMigrationReactorTests.cs) |
| [CloseServiceEngagement](../../src/Compliance.Common/Features/AccessControl/CloseServiceEngagement.cs#L6) | [CloseServiceEngagementHandler](../../src/Compliance.Core/Features/AccessControl/CloseServiceEngagementHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L630) | Partial / [G17](#g17), [G18](#g18) | [AssignmentIndependenceReevaluationTests](../../test/Compliance.Tests/Features/AccessControl/AssignmentIndependenceReevaluationTests.cs) (+1) |
| [CreateServiceEngagement](../../src/Compliance.Common/Features/AccessControl/CreateServiceEngagement.cs#L6) | [CreateServiceEngagementHandler](../../src/Compliance.Core/Features/AccessControl/CreateServiceEngagementHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L598) | Exists | [ServiceEngagementHandlerTests](../../test/Compliance.Tests/Features/AccessControl/ServiceEngagementHandlerTests.cs) (+1) |
| [DefineRole](../../src/Compliance.Common/Features/AccessControl/DefineRole.cs#L7) | [DefineRoleHandler](../../src/Compliance.Core/Features/AccessControl/DefineRoleHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2144) | Exists | [BuiltInRoleCatalogMigrationReactorTests](../../test/Compliance.Tests/Features/AccessControl/BuiltInRoleCatalogMigrationReactorTests.cs) (+3) |
| [DefineTeam](../../src/Compliance.Common/Features/AccessControl/DefineTeam.cs#L7) | [DefineTeamHandler](../../src/Compliance.Core/Features/AccessControl/DefineTeamHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2117) | Exists | [ActorReferenceTests](../../test/Compliance.Tests/Features/AccessControl/ActorReferenceTests.cs) (+3) |
| [DeleteRole](../../src/Compliance.Common/Features/AccessControl/DeleteRole.cs#L7) | [DeleteRoleHandler](../../src/Compliance.Core/Features/AccessControl/DeleteRoleHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L2147) | Exists | [DeleteRoleRequestScenarioTests](../../test/Compliance.Tests/Features/AccessControl/DeleteRoleRequestScenarioTests.cs) |
| [DeleteTeam](../../src/Compliance.Common/Features/AccessControl/DeleteTeam.cs#L7) | [DeleteTeamHandler](../../src/Compliance.Core/Features/AccessControl/DeleteTeamHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L2120) | Exists | [RequestScenarioJsonContext](../../test/Compliance.Tests/RequestScenarioJsonContext.cs) (+3) |
| [DeprovisionMember](../../src/Compliance.Common/Features/AccessControl/DeprovisionMember.cs#L6) | [DeprovisionMemberHandler](../../src/Compliance.Core/Features/AccessControl/DeprovisionMemberHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L525) | Exists | [RbacManagementRequestScenarioTests](../../test/Compliance.Tests/Features/AccessControl/RbacManagementRequestScenarioTests.cs) (+1) |
| [EvaluateClientIndependence](../../src/Compliance.Common/Features/AccessControl/EvaluateClientIndependence.cs#L7) | [EvaluateClientIndependenceHandler](../../src/Compliance.Core/Features/AccessControl/EvaluateClientIndependenceHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L662) | Exists | [IndependenceOperationTests](../../test/Compliance.Tests/Features/AccessControl/IndependenceOperationTests.cs) (+1) |
| [GrantAccess](../../src/Compliance.Common/Features/AccessControl/GrantAccess.cs#L6) | [GrantAccessHandler](../../src/Compliance.Core/Features/AccessControl/GrantAccessHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2172) | Partial / [G03](#g03), [G21](#g21) | [ArtifactMetadataAccessTests](../../test/Compliance.Tests/Features/Evidence/ArtifactMetadataAccessTests.cs) (+2) |
| [ProposeServiceEngagementStaff](../../src/Compliance.Common/Features/AccessControl/ProposeServiceEngagementStaff.cs#L6) | [ProposeServiceEngagementStaffHandler](../../src/Compliance.Core/Features/AccessControl/ProposeServiceEngagementStaffHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L606) | Exists | [FirmOperationsCompositionTests](../../test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs) |
| [RecordDirectoryIndependenceReevaluation](../../src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluation.cs#L7) | [RecordDirectoryIndependenceReevaluationHandler](../../src/Compliance.Core/Features/AccessControl/RecordDirectoryIndependenceReevaluationHandler.cs) | internal | Exists | [DirectoryIndependenceReevaluationTests](../../test/Compliance.Tests/Features/AccessControl/DirectoryIndependenceReevaluationTests.cs) |
| [RecordNonattestService](../../src/Compliance.Common/Features/AccessControl/RecordNonattestService.cs#L6) | [RecordNonattestServiceHandler](../../src/Compliance.Core/Features/AccessControl/RecordNonattestServiceHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L658) | Exists | [SourceIndependenceReevaluationTests](../../test/Compliance.Tests/Features/AccessControl/SourceIndependenceReevaluationTests.cs) (+1) |
| [RecordSeparationOfDutiesWaiver](../../src/Compliance.Common/Features/AccessControl/RecordSeparationOfDutiesWaiver.cs#L6) | [RecordSeparationOfDutiesWaiverHandler](../../src/Compliance.Core/Features/AccessControl/RecordSeparationOfDutiesWaiverHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2083) | Exists | [EvidenceRedactionCompositionTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRedactionCompositionTests.cs) (+2) |
| [RegisterFirmStaff](../../src/Compliance.Common/Features/AccessControl/RegisterFirmStaff.cs#L6) | [RegisterFirmStaffHandler](../../src/Compliance.Core/Features/AccessControl/RegisterFirmStaffHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L582) | Exists | [ServiceEngagementHandlerTests](../../test/Compliance.Tests/Features/AccessControl/ServiceEngagementHandlerTests.cs) (+1) |
| [RegisterMember](../../src/Compliance.Common/Features/AccessControl/RegisterMember.cs#L6) | [RegisterMemberHandler](../../src/Compliance.Core/Features/AccessControl/RegisterMemberHandler.cs) | internal | Exists | [RequestScenarioJsonContext](../../test/Compliance.Tests/RequestScenarioJsonContext.cs) (+7) |
| [ReinstateMember](../../src/Compliance.Common/Features/AccessControl/ReinstateMember.cs#L6) | [ReinstateMemberHandler](../../src/Compliance.Core/Features/AccessControl/ReinstateMemberHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L521) | Exists | [TenantManagerInvariantTests](../../test/Compliance.Tests/Features/AccessControl/TenantManagerInvariantTests.cs) |
| [RemoveRolePermission](../../src/Compliance.Common/Features/AccessControl/RemoveRolePermission.cs#L10) | [RemoveRolePermissionHandler](../../src/Compliance.Core/Features/AccessControl/RemoveRolePermissionHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L2159) | Exists | [RoleCleanupReactorTests](../../test/Compliance.Tests/Features/AccessControl/RoleCleanupReactorTests.cs) (+2) |
| [RemoveTeamMember](../../src/Compliance.Common/Features/AccessControl/RemoveTeamMember.cs#L7) | [RemoveTeamMemberHandler](../../src/Compliance.Core/Features/AccessControl/RemoveTeamMemberHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L2132) | Exists | [TeamCleanupReactorTests](../../test/Compliance.Tests/Features/AccessControl/TeamCleanupReactorTests.cs) (+2) |
| [RemoveTeamRole](../../src/Compliance.Common/Features/AccessControl/RemoveTeamRole.cs#L7) | [RemoveTeamRoleHandler](../../src/Compliance.Core/Features/AccessControl/RemoveTeamRoleHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L2141) | Exists | [RoleCleanupReactorTests](../../test/Compliance.Tests/Features/AccessControl/RoleCleanupReactorTests.cs) (+2) |
| [RenameRole](../../src/Compliance.Common/Features/AccessControl/RenameRole.cs#L6) | [RenameRoleHandler](../../src/Compliance.Core/Features/AccessControl/RenameRoleHandler.cs) | internal | Exists | [BuiltInRoleCatalogMigrationReactorTests](../../test/Compliance.Tests/Features/AccessControl/BuiltInRoleCatalogMigrationReactorTests.cs) |
| [ReviseIndependenceRules](../../src/Compliance.Common/Features/AccessControl/ReviseIndependenceRules.cs#L6) | [ReviseIndependenceRulesHandler](../../src/Compliance.Core/Features/AccessControl/ReviseIndependenceRulesHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L654) | Exists | [IndependenceOperationTests](../../test/Compliance.Tests/Features/AccessControl/IndependenceOperationTests.cs) (+1) |
| [RevokeAccessGrant](../../src/Compliance.Common/Features/AccessControl/RevokeAccessGrant.cs#L6) | [RevokeAccessGrantHandler](../../src/Compliance.Core/Features/AccessControl/RevokeAccessGrantHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L2175) | Partial / [G03](#g03), [G21](#g21) | [EvidenceRedactionCompositionTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRedactionCompositionTests.cs) (+3) |
| [SetFirmStaffStatus](../../src/Compliance.Common/Features/AccessControl/SetFirmStaffStatus.cs#L6) | [SetFirmStaffStatusHandler](../../src/Compliance.Core/Features/AccessControl/SetFirmStaffStatusHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L586) | Exists | [FirmOperationsCompositionTests](../../test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs) |
| [SuspendMember](../../src/Compliance.Common/Features/AccessControl/SuspendMember.cs#L6) | [SuspendMemberHandler](../../src/Compliance.Core/Features/AccessControl/SuspendMemberHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L517) | Exists | [TenantManagerInvariantTests](../../test/Compliance.Tests/Features/AccessControl/TenantManagerInvariantTests.cs) (+1) |
| [WithdrawServiceEngagementStaffProposal](../../src/Compliance.Common/Features/AccessControl/WithdrawServiceEngagementStaffProposal.cs#L6) | [WithdrawServiceEngagementStaffProposalHandler](../../src/Compliance.Core/Features/AccessControl/WithdrawServiceEngagementStaffProposalHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L610) | Exists | [FirmOperationsCompositionTests](../../test/Compliance.Tests/Features/AccessControl/FirmOperationsCompositionTests.cs) |

### AccessReviews — R2-06, T2-05

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AcceptAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/AcceptAccessPopulation.cs#L10) | [AcceptAccessPopulationHandler](../../src/Compliance.Core/Features/AccessReviews/AcceptAccessPopulationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1328) | Exists | [RestrictedAccessPopulationReadTests](../../test/Compliance.Tests/Features/AccessReviews/RestrictedAccessPopulationReadTests.cs) (+3) |
| [ApproveAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/ApproveAccessExpectation.cs#L10) | [ApproveAccessExpectationHandler](../../src/Compliance.Core/Features/AccessReviews/ApproveAccessExpectationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1364) | Exists | [PersonalAccessReviewTransportTests](../../test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs) (+2) |
| [ClassifyAccessPrincipal](../../src/Compliance.Common/Features/AccessReviews/ClassifyAccessPrincipal.cs#L7) | [ClassifyAccessPrincipalHandler](../../src/Compliance.Core/Features/AccessReviews/ClassifyAccessPrincipalHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1348) | Exists | [AccessReviewFixture](../../test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs) (+1) |
| [CompleteAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/CompleteAccessReviewCampaign.cs#L11) | [CompleteAccessReviewCampaignHandler](../../src/Compliance.Core/Features/AccessReviews/CompleteAccessReviewCampaignHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1412) | Exists | [AccessRemediationTests](../../test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs) (+2) |
| [ExemptAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/ExemptAccessExpectation.cs#L10) | [ExemptAccessExpectationHandler](../../src/Compliance.Core/Features/AccessReviews/ExemptAccessExpectationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1368) | Exists | [PersonalAccessReviewTransportTests](../../test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs) (+1) |
| [ExemptAccessRemediation](../../src/Compliance.Common/Features/AccessReviews/ExemptAccessRemediation.cs#L10) | [ExemptAccessRemediationHandler](../../src/Compliance.Core/Features/AccessReviews/ExemptAccessRemediationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1408) | Exists | [AccessRemediationTests](../../test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs) (+1) |
| [ExemptMissingAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/ExemptMissingAccessPopulation.cs#L10) | [ExemptMissingAccessPopulationHandler](../../src/Compliance.Core/Features/AccessReviews/ExemptMissingAccessPopulationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1340) | Exists | [PersonalAccessReviewTransportTests](../../test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs) |
| [LaunchAccessReviewCampaign](../../src/Compliance.Common/Features/AccessReviews/LaunchAccessReviewCampaign.cs#L10) | [LaunchAccessReviewCampaignHandler](../../src/Compliance.Core/Features/AccessReviews/LaunchAccessReviewCampaignHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1376) | Exists | [AccessReviewFixture](../../test/Compliance.Tests/Features/AccessReviews/AccessReviewFixture.cs) (+2) |
| [OpenAccessPopulation](../../src/Compliance.Common/Features/AccessReviews/OpenAccessPopulation.cs#L7) | [OpenAccessPopulationHandler](../../src/Compliance.Core/Features/AccessReviews/OpenAccessPopulationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1316) | Exists | [PersonalAccessReviewTransportTests](../../test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs) (+4) |
| [ProposeAccessExpectation](../../src/Compliance.Common/Features/AccessReviews/ProposeAccessExpectation.cs#L7) | [ProposeAccessExpectationHandler](../../src/Compliance.Core/Features/AccessReviews/ProposeAccessExpectationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1360) | Exists | [PersonalAccessReviewTransportTests](../../test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs) (+2) |
| [RecordAccessDecision](../../src/Compliance.Common/Features/AccessReviews/RecordAccessDecision.cs#L10) | [RecordAccessDecisionHandler](../../src/Compliance.Core/Features/AccessReviews/RecordAccessDecisionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1388) | Exists | [AccessRemediationTests](../../test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs) (+4) |
| [RecordAccessPopulationFacts](../../src/Compliance.Common/Features/AccessReviews/RecordAccessPopulationFacts.cs#L7) | [RecordAccessPopulationFactsHandler](../../src/Compliance.Core/Features/AccessReviews/RecordAccessPopulationFactsHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1320) | Exists | [RestrictedAccessPopulationReadTests](../../test/Compliance.Tests/Features/AccessReviews/RestrictedAccessPopulationReadTests.cs) (+3) |
| [RecordAccessRemediationChange](../../src/Compliance.Common/Features/AccessReviews/RecordAccessRemediationChange.cs#L7) | [RecordAccessRemediationChangeHandler](../../src/Compliance.Core/Features/AccessReviews/RecordAccessRemediationChangeHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1400) | Exists | [AccessRemediationTests](../../test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs) |
| [RecordBulkAccessDecision](../../src/Compliance.Common/Features/AccessReviews/RecordBulkAccessDecision.cs#L10) | [RecordBulkAccessDecisionHandler](../../src/Compliance.Core/Features/AccessReviews/RecordBulkAccessDecisionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1396) | Exists | [PersonalAccessReviewTransportTests](../../test/Compliance.Tests/Features/AccessReviews/PersonalAccessReviewTransportTests.cs) (+2) |
| [VerifyAccessRemediation](../../src/Compliance.Common/Features/AccessReviews/VerifyAccessRemediation.cs#L7) | [VerifyAccessRemediationHandler](../../src/Compliance.Core/Features/AccessReviews/VerifyAccessRemediationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1404) | Exists | [AccessRemediationTests](../../test/Compliance.Tests/Features/AccessReviews/AccessRemediationTests.cs) |

### Applications — R1-10, EN-05

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AcceptApplicationImport](../../src/Compliance.Common/Features/Applications/AcceptApplicationImport.cs#L6) | [AcceptApplicationImportHandler](../../src/Compliance.Core/Features/Applications/AcceptApplicationImportHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1885) | Exists | [ApplicationImportCompositionTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs) (+4) |
| [AddApplicationRelationship](../../src/Compliance.Common/Features/Applications/AddApplicationRelationship.cs#L6) | [AddApplicationRelationshipHandler](../../src/Compliance.Core/Features/Applications/AddApplicationRelationshipHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1749) | Exists | [ApplicationChangePreviewTests](../../test/Compliance.Tests/Features/Applications/ApplicationChangePreviewTests.cs) (+2) |
| [ApplyApplicationImportEffect](../../src/Compliance.Common/Features/Applications/ApplyApplicationImportEffect.cs#L7) | [ApplyApplicationImportEffectHandler](../../src/Compliance.Core/Features/Applications/ApplyApplicationImportEffectHandler.cs) | internal | Exists | [ApplicationImportWriteGuardTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs) (+4) |
| [CancelApplicationImport](../../src/Compliance.Common/Features/Applications/CancelApplicationImport.cs#L6) | [CancelApplicationImportHandler](../../src/Compliance.Core/Features/Applications/CancelApplicationImportHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1890) | Exists | [ApplicationImportPersonalTransportTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportPersonalTransportTests.cs) (+3) |
| [CorrelateApplicationImportRow](../../src/Compliance.Common/Features/Applications/CorrelateApplicationImportRow.cs#L6) | [CorrelateApplicationImportRowHandler](../../src/Compliance.Core/Features/Applications/CorrelateApplicationImportRowHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1894) | Exists | [ApplicationImportCommitTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportCommitTests.cs) (+18) |
| [DecideAccessReviewScope](../../src/Compliance.Common/Features/Applications/DecideAccessReviewScope.cs#L10) | [DecideAccessReviewScopeHandler](../../src/Compliance.Core/Features/Applications/DecideAccessReviewScopeHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1832) | Exists | [PersonalAccessReviewScopeTransportTests](../../test/Compliance.Tests/Features/Applications/PersonalAccessReviewScopeTransportTests.cs) (+2) |
| [DeclareApplication](../../src/Compliance.Common/Features/Applications/DeclareApplication.cs#L6) | [DeclareApplicationHandler](../../src/Compliance.Core/Features/Applications/DeclareApplicationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1728) | Exists | [InventoryManagementIndependenceTests](../../test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs) (+3) |
| [DeclareSystemInstance](../../src/Compliance.Common/Features/Applications/DeclareSystemInstance.cs#L6) | [DeclareSystemInstanceHandler](../../src/Compliance.Core/Features/Applications/DeclareSystemInstanceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1788) | Exists | [SystemInstanceBoundedReadTests](../../test/Compliance.Tests/Features/Applications/SystemInstanceBoundedReadTests.cs) (+3) |
| [ExecuteApplicationImport](../../src/Compliance.Common/Features/Applications/ExecuteApplicationImport.cs#L7) | [ExecuteApplicationImportHandler](../../src/Compliance.Core/Features/Applications/ExecuteApplicationImportHandler.cs) | internal | Exists | [ApplicationImportCompositionTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportCompositionTests.cs) (+1) |
| [FreezeApplicationImportOmissionProposal](../../src/Compliance.Common/Features/Applications/FreezeApplicationImportOmissionProposal.cs#L7) | [FreezeApplicationImportOmissionProposalHandler](../../src/Compliance.Core/Features/Applications/FreezeApplicationImportOmissionProposalHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1920) | Partial / [G06](#g06) | [ApplicationImportOmissionConsumerTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportOmissionConsumerTests.cs) |
| [RemoveApplicationRelationship](../../src/Compliance.Common/Features/Applications/RemoveApplicationRelationship.cs#L6) | [RemoveApplicationRelationshipHandler](../../src/Compliance.Core/Features/Applications/RemoveApplicationRelationshipHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L1757) | Exists | [ApplicationChangePreviewTests](../../test/Compliance.Tests/Features/Applications/ApplicationChangePreviewTests.cs) (+2) |
| [RetireApplication](../../src/Compliance.Common/Features/Applications/RetireApplication.cs#L6) | [RetireApplicationHandler](../../src/Compliance.Core/Features/Applications/RetireApplicationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1822) | Partial / [G07](#g07) | [ApplicationImportWriteGuardTests](../../test/Compliance.Tests/Features/Applications/ApplicationImportWriteGuardTests.cs) |
| [RetireSystemInstance](../../src/Compliance.Common/Features/Applications/RetireSystemInstance.cs#L6) | [RetireSystemInstanceHandler](../../src/Compliance.Core/Features/Applications/RetireSystemInstanceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1827) | Partial / [G07](#g07) | No direct request reference |
| [ReviseApplication](../../src/Compliance.Common/Features/Applications/ReviseApplication.cs#L7) | [ReviseApplicationHandler](../../src/Compliance.Core/Features/Applications/ReviseApplicationHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1732) | Partial / [G07](#g07) | [InventoryManagementIndependenceTests](../../test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs) (+6) |
| [StageApplicationImport](../../src/Compliance.Common/Features/Applications/StageApplicationImport.cs#L6) | [StageApplicationImportHandler](../../src/Compliance.Core/Features/Applications/StageApplicationImportHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1871) | Exists | [ArtifactRetentionHandlerTests](../../test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs) (+22) |

### Boundaries — R1-02, EN-02/04

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ApproveBoundary](../../src/Compliance.Common/Features/Boundaries/ApproveBoundary.cs#L7) | [ApproveBoundaryHandler](../../src/Compliance.Core/Features/Boundaries/ApproveBoundaryHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2079) | Partial / [G07](#g07) | [PersonalBoundaryDecisionTransportTests](../../test/Compliance.Tests/Features/Boundaries/PersonalBoundaryDecisionTransportTests.cs) (+1) |
| [CreateBoundary](../../src/Compliance.Common/Features/Boundaries/CreateBoundary.cs#L8) | [CreateBoundaryHandler](../../src/Compliance.Core/Features/Boundaries/CreateBoundaryHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1961) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) |
| [DiscardBoundaryDraft](../../src/Compliance.Common/Features/Boundaries/DiscardBoundaryDraft.cs#L7) | [DiscardBoundaryDraftHandler](../../src/Compliance.Core/Features/Boundaries/DiscardBoundaryDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1973) | Exists | No direct request reference |
| [ProposeBoundarySuccessor](../../src/Compliance.Common/Features/Boundaries/ProposeBoundarySuccessor.cs#L7) | [ProposeBoundarySuccessorHandler](../../src/Compliance.Core/Features/Boundaries/ProposeBoundarySuccessorHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2091) | Exists | No direct request reference |
| [ReviewBoundary](../../src/Compliance.Common/Features/Boundaries/ReviewBoundary.cs#L7) | [ReviewBoundaryHandler](../../src/Compliance.Core/Features/Boundaries/ReviewBoundaryHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2075) | Exists | [PersonalBoundaryDecisionTransportTests](../../test/Compliance.Tests/Features/Boundaries/PersonalBoundaryDecisionTransportTests.cs) (+1) |
| [ReviseBoundaryDraft](../../src/Compliance.Common/Features/Boundaries/ReviseBoundaryDraft.cs#L7) | [ReviseBoundaryDraftHandler](../../src/Compliance.Core/Features/Boundaries/ReviseBoundaryDraftHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1969) | Exists | No direct request reference |

### Commitments — R1-13

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ApproveCommitmentDraft](../../src/Compliance.Common/Features/Commitments/ApproveCommitmentDraft.cs#L12) | [ApproveCommitmentDraftHandler](../../src/Compliance.Core/Features/Commitments/ApproveCommitmentDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L869) | Partial / [G07](#g07) | [PersonalCommitmentDecisionTransportTests](../../test/Compliance.Tests/Features/Commitments/PersonalCommitmentDecisionTransportTests.cs) (+1) |
| [CreateCommitmentDraft](../../src/Compliance.Common/Features/Commitments/CreateCommitmentDraft.cs#L8) | [CreateCommitmentDraftHandler](../../src/Compliance.Core/Features/Commitments/CreateCommitmentDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L836) | Partial / [G07](#g07) | [CommitmentDraftReplayTests](../../test/Compliance.Tests/Features/Commitments/CommitmentDraftReplayTests.cs) (+1) |
| [ReviewCommitmentDraft](../../src/Compliance.Common/Features/Commitments/ReviewCommitmentDraft.cs#L13) | [ReviewCommitmentDraftHandler](../../src/Compliance.Core/Features/Commitments/ReviewCommitmentDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L864) | Partial / [G07](#g07) | [PersonalCommitmentDecisionTransportTests](../../test/Compliance.Tests/Features/Commitments/PersonalCommitmentDecisionTransportTests.cs) (+2) |
| [ReviseCommitmentDraft](../../src/Compliance.Common/Features/Commitments/ReviseCommitmentDraft.cs#L8) | [ReviseCommitmentDraftHandler](../../src/Compliance.Core/Features/Commitments/ReviseCommitmentDraftHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L840) | Partial / [G07](#g07) | No direct request reference |

### ControlMappings — R1-06

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ProposeControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/ProposeControlCriterionMapping.cs#L12) | [ProposeControlCriterionMappingHandler](../../src/Compliance.Core/Features/ControlMappings/ProposeControlCriterionMappingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L761) | Exists | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) (+1) |
| [ProposeCriterionNotApplicable](../../src/Compliance.Common/Features/ControlMappings/ProposeCriterionNotApplicable.cs#L13) | [ProposeCriterionNotApplicableHandler](../../src/Compliance.Core/Features/ControlMappings/ProposeCriterionNotApplicableHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L816) | Exists | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) (+2) |
| [RetireControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/RetireControlCriterionMapping.cs#L9) | [RetireControlCriterionMappingHandler](../../src/Compliance.Core/Features/ControlMappings/RetireControlCriterionMappingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L769) | Exists | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) |
| [ReviewControlCriterionMapping](../../src/Compliance.Common/Features/ControlMappings/ReviewControlCriterionMapping.cs#L9) | [ReviewControlCriterionMappingHandler](../../src/Compliance.Core/Features/ControlMappings/ReviewControlCriterionMappingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L765) | Exists | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) (+2) |
| [ReviewCriterionApplicability](../../src/Compliance.Common/Features/ControlMappings/ReviewCriterionApplicability.cs#L13) | [ReviewCriterionApplicabilityHandler](../../src/Compliance.Core/Features/ControlMappings/ReviewCriterionApplicabilityHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L820) | Exists | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) (+2) |
| [WithdrawCriterionNotApplicable](../../src/Compliance.Common/Features/ControlMappings/WithdrawCriterionNotApplicable.cs#L9) | [WithdrawCriterionNotApplicableHandler](../../src/Compliance.Core/Features/ControlMappings/WithdrawCriterionNotApplicableHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L824) | Exists | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) |

### Controls — R1-05

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ApproveControl](../../src/Compliance.Common/Features/Controls/ApproveControl.cs#L8) | [ApproveControlHandler](../../src/Compliance.Core/Features/Controls/ApproveControlHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L732) | Partial / [G07](#g07) | [ControlLifecycleHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlLifecycleHandlerTests.cs) (+2) |
| [CreateControlDraft](../../src/Compliance.Common/Features/Controls/CreateControlDraft.cs#L8) | [CreateControlDraftHandler](../../src/Compliance.Core/Features/Controls/CreateControlDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L699) | Exists | [AttestManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs) (+1) |
| [DesignateControlOwnerPerson](../../src/Compliance.Common/Features/Controls/DesignateControlOwnerPerson.cs#L13) | [DesignateControlOwnerPersonHandler](../../src/Compliance.Core/Features/Controls/DesignateControlOwnerPersonHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L807) | Exists | [ControlOwnerPersonReadTests](../../test/Compliance.Tests/Features/Controls/ControlOwnerPersonReadTests.cs) (+2) |
| [DiscardControlDraft](../../src/Compliance.Common/Features/Controls/DiscardControlDraft.cs#L8) | [DiscardControlDraftHandler](../../src/Compliance.Core/Features/Controls/DiscardControlDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L707) | Exists | [ControlDraftHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlDraftHandlerTests.cs) |
| [ProposeControlRetirement](../../src/Compliance.Common/Features/Controls/ProposeControlRetirement.cs#L8) | [ProposeControlRetirementHandler](../../src/Compliance.Core/Features/Controls/ProposeControlRetirementHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L789) | Exists | [ControlLifecycleHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlLifecycleHandlerTests.cs) (+1) |
| [ProposeControlSuccessor](../../src/Compliance.Common/Features/Controls/ProposeControlSuccessor.cs#L8) | [ProposeControlSuccessorHandler](../../src/Compliance.Core/Features/Controls/ProposeControlSuccessorHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L785) | Partial / [G07](#g07) | [ControlLifecycleHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlLifecycleHandlerTests.cs) |
| [RetireControl](../../src/Compliance.Common/Features/Controls/RetireControl.cs#L8) | [RetireControlHandler](../../src/Compliance.Core/Features/Controls/RetireControlHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L798) | Partial / [G07](#g07) | [ControlLifecycleHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlLifecycleHandlerTests.cs) (+1) |
| [ReviewControl](../../src/Compliance.Common/Features/Controls/ReviewControl.cs#L8) | [ReviewControlHandler](../../src/Compliance.Core/Features/Controls/ReviewControlHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L728) | Exists | [ControlLifecycleHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlLifecycleHandlerTests.cs) (+3) |
| [ReviseControlDraft](../../src/Compliance.Common/Features/Controls/ReviseControlDraft.cs#L8) | [ReviseControlDraftHandler](../../src/Compliance.Core/Features/Controls/ReviseControlDraftHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L703) | Exists | No direct request reference |
| [WithdrawControlProposal](../../src/Compliance.Common/Features/Controls/WithdrawControlProposal.cs#L12) | [WithdrawControlProposalHandler](../../src/Compliance.Core/Features/Controls/WithdrawControlProposalHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L803) | Exists | [ControlLifecycleHandlerTests](../../test/Compliance.Tests/Features/Controls/ControlLifecycleHandlerTests.cs) |

### Criteria — R1-03

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [SetCriteriaTextOverlay](../../src/Compliance.Common/Features/Criteria/SetCriteriaTextOverlay.cs#L9) | [SetCriteriaTextOverlayHandler](../../src/Compliance.Core/Features/Criteria/SetCriteriaTextOverlayHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L670) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) (+2) |

### Evaluations — R2-05

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [DefineControlEvaluationPlan](../../src/Compliance.Common/Features/Evaluations/DefineControlEvaluationPlan.cs#L8) | [DefineControlEvaluationPlanHandler](../../src/Compliance.Core/Features/Evaluations/DefineControlEvaluationPlanHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1207) | Exists | [ControlEvaluationTests](../../test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs) (+3) |
| [DisposeControlEvaluationDeviation](../../src/Compliance.Common/Features/Evaluations/DisposeControlEvaluationDeviation.cs#L9) | [DisposeControlEvaluationDeviationHandler](../../src/Compliance.Core/Features/Evaluations/DisposeControlEvaluationDeviationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1232) | Exists | [ControlEvaluationTests](../../test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs) |
| [RaiseEvaluationDeviationFinding](../../src/Compliance.Common/Features/Evaluations/RaiseEvaluationDeviationFinding.cs#L11) | [RaiseEvaluationDeviationFindingHandler](../../src/Compliance.Core/Features/Evaluations/RaiseEvaluationDeviationFindingHandler.cs) | internal | Partial / [G05](#g05) | [EvaluationDeviationFindingTests](../../test/Compliance.Tests/Features/Evaluations/EvaluationDeviationFindingTests.cs) |
| [RecordControlEvaluationStep](../../src/Compliance.Common/Features/Evaluations/RecordControlEvaluationStep.cs#L12) | [RecordControlEvaluationStepHandler](../../src/Compliance.Core/Features/Evaluations/RecordControlEvaluationStepHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1228) | Partial / [G01](#g01) | [ControlEvaluationTests](../../test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs) (+3) |
| [ReviewControlEvaluation](../../src/Compliance.Common/Features/Evaluations/ReviewControlEvaluation.cs#L12) | [ReviewControlEvaluationHandler](../../src/Compliance.Core/Features/Evaluations/ReviewControlEvaluationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1240) | Partial / [G01](#g01) | [ControlEvaluationTests](../../test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs) (+3) |
| [StartControlEvaluation](../../src/Compliance.Common/Features/Evaluations/StartControlEvaluation.cs#L12) | [StartControlEvaluationHandler](../../src/Compliance.Core/Features/Evaluations/StartControlEvaluationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1203) | Exists | [ControlEvaluationTests](../../test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs) (+5) |
| [SubmitControlEvaluation](../../src/Compliance.Common/Features/Evaluations/SubmitControlEvaluation.cs#L12) | [SubmitControlEvaluationHandler](../../src/Compliance.Core/Features/Evaluations/SubmitControlEvaluationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1236) | Exists | [ControlEvaluationTests](../../test/Compliance.Tests/Features/Evaluations/ControlEvaluationTests.cs) (+4) |

### Evidence — R2-03/12

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ApproveEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/ApproveEvidenceRedaction.cs#L7) | [ApproveEvidenceRedactionHandler](../../src/Compliance.Core/Features/Evidence/ApproveEvidenceRedactionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1256) | Partial / [G02](#g02) | [EvidenceRedactionCompositionTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRedactionCompositionTests.cs) |
| [CancelEvidenceRequest](../../src/Compliance.Common/Features/Evidence/CancelEvidenceRequest.cs#L9) | [CancelEvidenceRequestHandler](../../src/Compliance.Core/Features/Evidence/CancelEvidenceRequestHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1276) | Exists | [EvidenceRequestHandlerTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs) (+3) |
| [FulfilEvidenceRequest](../../src/Compliance.Common/Features/Evidence/FulfilEvidenceRequest.cs#L10) | [FulfilEvidenceRequestHandler](../../src/Compliance.Core/Features/Evidence/FulfilEvidenceRequestHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1272) | Partial / [G01](#g01) | [EvidenceRequestHandlerTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs) |
| [OpenEvidenceRequest](../../src/Compliance.Common/Features/Evidence/OpenEvidenceRequest.cs#L9) | [OpenEvidenceRequestHandler](../../src/Compliance.Core/Features/Evidence/OpenEvidenceRequestHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1244) | Exists | [EvidenceRequestHandlerTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRequestHandlerTests.cs) (+4) |
| [PrepareEvidenceRedaction](../../src/Compliance.Common/Features/Evidence/PrepareEvidenceRedaction.cs#L6) | [PrepareEvidenceRedactionHandler](../../src/Compliance.Core/Features/Evidence/PrepareEvidenceRedactionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1252) | Partial / [G02](#g02) | [EvidenceRedactionCompositionTests](../../test/Compliance.Tests/Features/Evidence/EvidenceRedactionCompositionTests.cs) |

### Operations — R2-01/04, T2-02/03/06/09

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ApproveControlOperatingPlan](../../src/Compliance.Common/Features/Operations/ApproveControlOperatingPlan.cs#L12) | [ApproveControlOperatingPlanHandler](../../src/Compliance.Core/Features/Operations/ApproveControlOperatingPlanHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1163) | Partial / [G05](#g05) | [ControlOperatingPlanTests](../../test/Compliance.Tests/Features/Operations/ControlOperatingPlanTests.cs) (+6) |
| [AttestControlOccurrence](../../src/Compliance.Common/Features/Operations/AttestControlOccurrence.cs#L11) | [AttestControlOccurrenceHandler](../../src/Compliance.Core/Features/Operations/AttestControlOccurrenceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1191) | Partial / [G01](#g01), [G05](#g05) | [AttestManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs) (+2) |
| [CorrectControlAttestation](../../src/Compliance.Common/Features/Operations/CorrectControlAttestation.cs#L8) | [CorrectControlAttestationHandler](../../src/Compliance.Core/Features/Operations/CorrectControlAttestationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1195) | Partial / [G01](#g01) | [PersonalOccurrenceProofTransportTests](../../test/Compliance.Tests/Features/Operations/PersonalOccurrenceProofTransportTests.cs) (+4) |
| [OpenControlOccurrence](../../src/Compliance.Common/Features/Operations/OpenControlOccurrence.cs#L8) | [OpenControlOccurrenceHandler](../../src/Compliance.Core/Features/Operations/OpenControlOccurrenceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1179) | Exists | [ControlOperatingPlanTests](../../test/Compliance.Tests/Features/Operations/ControlOperatingPlanTests.cs) (+3) |
| [ProposeControlOperatingPlan](../../src/Compliance.Common/Features/Operations/ProposeControlOperatingPlan.cs#L12) | [ProposeControlOperatingPlanHandler](../../src/Compliance.Core/Features/Operations/ProposeControlOperatingPlanHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1155) | Partial / [G05](#g05) | [OperationsFixture](../../test/Compliance.Tests/Features/Operations/OperationsFixture.cs) |
| [ReviewControlOccurrence](../../src/Compliance.Common/Features/Operations/ReviewControlOccurrence.cs#L11) | [ReviewControlOccurrenceHandler](../../src/Compliance.Core/Features/Operations/ReviewControlOccurrenceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1199) | Partial / [G05](#g05) | [AttestManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs) (+1) |

### Policies — R2-02

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [ApprovePolicy](../../src/Compliance.Common/Features/Policies/ApprovePolicy.cs#L12) | [ApprovePolicyHandler](../../src/Compliance.Core/Features/Policies/ApprovePolicyHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L922) | Partial / [G07](#g07) | [PersonalPolicyDecisionTransportTests](../../test/Compliance.Tests/Features/Policies/PersonalPolicyDecisionTransportTests.cs) (+1) |
| [ApprovePolicyRetirement](../../src/Compliance.Common/Features/Policies/ApprovePolicyRetirement.cs#L9) | [ApprovePolicyRetirementHandler](../../src/Compliance.Core/Features/Policies/ApprovePolicyRetirementHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L934) | Partial / [G07](#g07) | [PersonalPolicyDecisionTransportTests](../../test/Compliance.Tests/Features/Policies/PersonalPolicyDecisionTransportTests.cs) |
| [ConfirmPolicyReview](../../src/Compliance.Common/Features/Policies/ConfirmPolicyReview.cs#L12) | [ConfirmPolicyReviewHandler](../../src/Compliance.Core/Features/Policies/ConfirmPolicyReviewHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L926) | Exists | [PersonalPolicyDecisionTransportTests](../../test/Compliance.Tests/Features/Policies/PersonalPolicyDecisionTransportTests.cs) |
| [CreatePolicyDraft](../../src/Compliance.Common/Features/Policies/CreatePolicyDraft.cs#L8) | [CreatePolicyDraftHandler](../../src/Compliance.Core/Features/Policies/CreatePolicyDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L889) | Exists | [AttestManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs) (+1) |
| [DiscardPolicyDraft](../../src/Compliance.Common/Features/Policies/DiscardPolicyDraft.cs#L9) | [DiscardPolicyDraftHandler](../../src/Compliance.Core/Features/Policies/DiscardPolicyDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L905) | Exists | No direct request reference |
| [ProposePolicyRetirement](../../src/Compliance.Common/Features/Policies/ProposePolicyRetirement.cs#L8) | [ProposePolicyRetirementHandler](../../src/Compliance.Core/Features/Policies/ProposePolicyRetirementHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L930) | Exists | No direct request reference |
| [ProposePolicySuccessor](../../src/Compliance.Common/Features/Policies/ProposePolicySuccessor.cs#L9) | [ProposePolicySuccessorHandler](../../src/Compliance.Core/Features/Policies/ProposePolicySuccessorHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L909) | Exists | [PolicyCampaignHandlerTests](../../test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs) |
| [ReviewPolicyDraft](../../src/Compliance.Common/Features/Policies/ReviewPolicyDraft.cs#L12) | [ReviewPolicyDraftHandler](../../src/Compliance.Core/Features/Policies/ReviewPolicyDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L918) | Exists | [PersonalPolicyDecisionTransportTests](../../test/Compliance.Tests/Features/Policies/PersonalPolicyDecisionTransportTests.cs) (+2) |
| [RevisePolicyDraft](../../src/Compliance.Common/Features/Policies/RevisePolicyDraft.cs#L8) | [RevisePolicyDraftHandler](../../src/Compliance.Core/Features/Policies/RevisePolicyDraftHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L901) | Exists | No direct request reference |

### PolicyDistribution — R2-10

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AcknowledgePolicy](../../src/Compliance.Common/Features/PolicyDistribution/AcknowledgePolicy.cs#L12) | [AcknowledgePolicyHandler](../../src/Compliance.Core/Features/PolicyDistribution/AcknowledgePolicyHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1007) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) (+3) |
| [ApproveCampaignWaiver](../../src/Compliance.Common/Features/PolicyDistribution/ApproveCampaignWaiver.cs#L12) | [ApproveCampaignWaiverHandler](../../src/Compliance.Core/Features/PolicyDistribution/ApproveCampaignWaiverHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1011) | Exists | [PolicyCampaignHandlerTests](../../test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs) (+1) |
| [CloseCampaign](../../src/Compliance.Common/Features/PolicyDistribution/CloseCampaign.cs#L9) | [CloseCampaignHandler](../../src/Compliance.Core/Features/PolicyDistribution/CloseCampaignHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1002) | Exists | No direct request reference |
| [DefineTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/DefineTrainingRequirement.cs#L8) | [DefineTrainingRequirementHandler](../../src/Compliance.Core/Features/PolicyDistribution/DefineTrainingRequirementHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L954) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) |
| [LaunchPolicyCampaign](../../src/Compliance.Common/Features/PolicyDistribution/LaunchPolicyCampaign.cs#L12) | [LaunchPolicyCampaignHandler](../../src/Compliance.Core/Features/PolicyDistribution/LaunchPolicyCampaignHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L970) | Exists | [PolicyCampaignHandlerTests](../../test/Compliance.Tests/Features/PolicyDistribution/PolicyCampaignHandlerTests.cs) (+1) |
| [LaunchTrainingCampaign](../../src/Compliance.Common/Features/PolicyDistribution/LaunchTrainingCampaign.cs#L8) | [LaunchTrainingCampaignHandler](../../src/Compliance.Core/Features/PolicyDistribution/LaunchTrainingCampaignHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L974) | Exists | No direct request reference |
| [ReconcileCampaignAudience](../../src/Compliance.Common/Features/PolicyDistribution/ReconcileCampaignAudience.cs#L12) | [ReconcileCampaignAudienceHandler](../../src/Compliance.Core/Features/PolicyDistribution/ReconcileCampaignAudienceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L994) | Exists | No direct request reference |
| [RecordTrainingCompletion](../../src/Compliance.Common/Features/PolicyDistribution/RecordTrainingCompletion.cs#L9) | [RecordTrainingCompletionHandler](../../src/Compliance.Core/Features/PolicyDistribution/RecordTrainingCompletionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L998) | Partial / [G01](#g01) | [TrainingWorkAuthorityTests](../../test/Compliance.Tests/Features/Work/TrainingWorkAuthorityTests.cs) |
| [ReviseTrainingRequirement](../../src/Compliance.Common/Features/PolicyDistribution/ReviseTrainingRequirement.cs#L9) | [ReviseTrainingRequirementHandler](../../src/Compliance.Core/Features/PolicyDistribution/ReviseTrainingRequirementHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L966) | Exists | No direct request reference |

### Programs — R1-01/02/03

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [CreateClientService](../../src/Compliance.Common/Features/Programs/CreateClientService.cs#L7) | [CreateClientServiceHandler](../../src/Compliance.Core/Features/Programs/CreateClientServiceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1929) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) |
| [CreateProgram](../../src/Compliance.Common/Features/Programs/CreateProgram.cs#L7) | [CreateProgramHandler](../../src/Compliance.Core/Features/Programs/CreateProgramHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L559) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) (+5) |
| [RetireClientService](../../src/Compliance.Common/Features/Programs/RetireClientService.cs#L7) | [RetireClientServiceHandler](../../src/Compliance.Core/Features/Programs/RetireClientServiceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1941) | Exists | No direct request reference |
| [ReviseClientService](../../src/Compliance.Common/Features/Programs/ReviseClientService.cs#L7) | [ReviseClientServiceHandler](../../src/Compliance.Core/Features/Programs/ReviseClientServiceHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1937) | Exists | No direct request reference |
| [ReviseProgram](../../src/Compliance.Common/Features/Programs/ReviseProgram.cs#L7) | [ReviseProgramHandler](../../src/Compliance.Core/Features/Programs/ReviseProgramHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L678) | Exists | [ProgramCriteriaProjectionTests](../../test/Compliance.Tests/Features/Criteria/ProgramCriteriaProjectionTests.cs) |
| [SelectProgramCriteriaEdition](../../src/Compliance.Common/Features/Programs/SelectProgramCriteriaEdition.cs#L7) | [SelectProgramCriteriaEditionHandler](../../src/Compliance.Core/Features/Programs/SelectProgramCriteriaEditionHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L674) | Exists | [ProgramCriteriaProjectionTests](../../test/Compliance.Tests/Features/Criteria/ProgramCriteriaProjectionTests.cs) (+1) |

### Providers — R1-14

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [CloseProviderCoverageGap](../../src/Compliance.Common/Features/Providers/CloseProviderCoverageGap.cs#L8) | [CloseProviderCoverageGapHandler](../../src/Compliance.Core/Features/Providers/CloseProviderCoverageGapHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1585) | Exists | No direct request reference |
| [LinkProviderCoverageGapRiskAcceptance](../../src/Compliance.Common/Features/Providers/LinkProviderCoverageGapRiskAcceptance.cs#L9) | [LinkProviderCoverageGapRiskAcceptanceHandler](../../src/Compliance.Core/Features/Providers/LinkProviderCoverageGapRiskAcceptanceHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1590) | Exists | No direct request reference |
| [RecordAssuranceReport](../../src/Compliance.Common/Features/Providers/RecordAssuranceReport.cs#L8) | [RecordAssuranceReportHandler](../../src/Compliance.Core/Features/Providers/RecordAssuranceReportHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1559) | Partial / [G01](#g01) | [PersonalProviderReviewTransportTests](../../test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs) (+1) |
| [RecordProvider](../../src/Compliance.Common/Features/Providers/RecordProvider.cs#L8) | [RecordProviderHandler](../../src/Compliance.Core/Features/Providers/RecordProviderHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1541) | Exists | [ProviderReferenceTests](../../test/Compliance.Tests/Features/Providers/ProviderReferenceTests.cs) (+4) |
| [RecordProviderCoverageGap](../../src/Compliance.Common/Features/Providers/RecordProviderCoverageGap.cs#L7) | [RecordProviderCoverageGapHandler](../../src/Compliance.Core/Features/Providers/RecordProviderCoverageGapHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1580) | Exists | No direct request reference |
| [RecordProviderReview](../../src/Compliance.Common/Features/Providers/RecordProviderReview.cs#L11) | [RecordProviderReviewHandler](../../src/Compliance.Core/Features/Providers/RecordProviderReviewHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1572) | Partial / [G01](#g01) | [PersonalProviderReviewTransportTests](../../test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs) (+1) |
| [ReviseAssuranceReport](../../src/Compliance.Common/Features/Providers/ReviseAssuranceReport.cs#L8) | [ReviseAssuranceReportHandler](../../src/Compliance.Core/Features/Providers/ReviseAssuranceReportHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1563) | Exists | [PersonalProviderReviewTransportTests](../../test/Compliance.Tests/Features/Providers/PersonalProviderReviewTransportTests.cs) (+1) |
| [ReviseProvider](../../src/Compliance.Common/Features/Providers/ReviseProvider.cs#L8) | [ReviseProviderHandler](../../src/Compliance.Core/Features/Providers/ReviseProviderHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1544) | Exists | [ProviderReferenceTests](../../test/Compliance.Tests/Features/Providers/ProviderReferenceTests.cs) (+2) |

### Readiness — R1-08, R2-09, T2-04

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AnnotateReadinessGap](../../src/Compliance.Common/Features/Readiness/AnnotateReadinessGap.cs#L9) | [AnnotateReadinessGapHandler](../../src/Compliance.Core/Features/Readiness/AnnotateReadinessGapHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1147) | Exists | [PersonalReadinessAnnotationTransportTests](../../test/Compliance.Tests/Features/Readiness/PersonalReadinessAnnotationTransportTests.cs) (+1) |
| [DecideReadiness](../../src/Compliance.Common/Features/Readiness/DecideReadiness.cs#L12) | [DecideReadinessHandler](../../src/Compliance.Core/Features/Readiness/DecideReadinessHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1129) | Partial / [G04](#g04) | [PersonalReadinessClosureTransportTests](../../test/Compliance.Tests/Features/Readiness/PersonalReadinessClosureTransportTests.cs) (+1) |
| [DecideTypeIEntry](../../src/Compliance.Common/Features/Readiness/DecideTypeIEntry.cs#L13) | [DecideTypeIEntryHandler](../../src/Compliance.Core/Features/Readiness/DecideTypeIEntryHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1139) | Partial / [G04](#g04) | [ProgramStageProjectionTests](../../test/Compliance.Tests/Features/Programs/ProgramStageProjectionTests.cs) (+2) |
| [PlanReadinessGap](../../src/Compliance.Common/Features/Readiness/PlanReadinessGap.cs#L9) | [PlanReadinessGapHandler](../../src/Compliance.Core/Features/Readiness/PlanReadinessGapHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1133) | Partial / [G04](#g04) | [ReadinessAssessmentHandlerTests](../../test/Compliance.Tests/Features/Readiness/ReadinessAssessmentHandlerTests.cs) |
| [RunReadinessAssessment](../../src/Compliance.Common/Features/Readiness/RunReadinessAssessment.cs#L12) | [RunReadinessAssessmentHandler](../../src/Compliance.Core/Features/Readiness/RunReadinessAssessmentHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1113) | Partial / [G04](#g04) | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) (+1) |

### Remediation — R2-07, T1-06, T3-04

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AddCorrectiveAction](../../src/Compliance.Common/Features/Remediation/AddCorrectiveAction.cs#L8) | [AddCorrectiveActionHandler](../../src/Compliance.Core/Features/Remediation/AddCorrectiveActionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1296) | Exists | [WorkTestData](../../test/Compliance.Tests/Features/Work/WorkTestData.cs) (+1) |
| [CloseFinding](../../src/Compliance.Common/Features/Remediation/CloseFinding.cs#L14) | [CloseFindingHandler](../../src/Compliance.Core/Features/Remediation/CloseFindingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1308) | Partial / [G01](#g01) | [WorkSourceCompositionTests](../../test/Compliance.Tests/Features/Work/WorkSourceCompositionTests.cs) (+4) |
| [CompleteCorrectiveAction](../../src/Compliance.Common/Features/Remediation/CompleteCorrectiveAction.cs#L9) | [CompleteCorrectiveActionHandler](../../src/Compliance.Core/Features/Remediation/CompleteCorrectiveActionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1300) | Partial / [G01](#g01) | [AttestManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestManagementWriteWallTests.cs) (+6) |
| [LinkFindingAcceptance](../../src/Compliance.Common/Features/Remediation/LinkFindingAcceptance.cs#L12) | [LinkFindingAcceptanceHandler](../../src/Compliance.Core/Features/Remediation/LinkFindingAcceptanceHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1304) | Exists | [FindingTests](../../test/Compliance.Tests/Features/Remediation/FindingTests.cs) |
| [RaiseFinding](../../src/Compliance.Common/Features/Remediation/RaiseFinding.cs#L9) | [RaiseFindingHandler](../../src/Compliance.Core/Features/Remediation/RaiseFindingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1280) | Exists | [AttestProgramManagementWriteWallTests](../../test/Compliance.Tests/Features/AccessControl/AttestProgramManagementWriteWallTests.cs) (+1) |
| [RaiseOccurrenceFinding](../../src/Compliance.Common/Features/Remediation/RaiseOccurrenceFinding.cs#L11) | [RaiseOccurrenceFindingHandler](../../src/Compliance.Core/Features/Remediation/RaiseOccurrenceFindingHandler.cs) | internal | Partial / [G05](#g05) | [OccurrenceFindingTests](../../test/Compliance.Tests/Features/Remediation/OccurrenceFindingTests.cs) |
| [ReopenFinding](../../src/Compliance.Common/Features/Remediation/ReopenFinding.cs#L8) | [ReopenFindingHandler](../../src/Compliance.Core/Features/Remediation/ReopenFindingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1312) | Exists | [FindingTests](../../test/Compliance.Tests/Features/Remediation/FindingTests.cs) |
| [ReviseFinding](../../src/Compliance.Common/Features/Remediation/ReviseFinding.cs#L9) | [ReviseFindingHandler](../../src/Compliance.Core/Features/Remediation/ReviseFindingHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1292) | Partial / [G14](#g14) | [FindingClosureWorkTests](../../test/Compliance.Tests/Features/Work/FindingClosureWorkTests.cs) (+2) |

### Responsibilities — R1-04, EN-04

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AssignResponsibility](../../src/Compliance.Common/Features/Responsibilities/AssignResponsibility.cs#L8) | [AssignResponsibilityHandler](../../src/Compliance.Core/Features/Responsibilities/AssignResponsibilityHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1981) | Partial / [G21](#g21) | [ControlCriterionMappingHandlerTests](../../test/Compliance.Tests/Features/ControlMappings/ControlCriterionMappingHandlerTests.cs) (+24) |
| [RevokeResponsibility](../../src/Compliance.Common/Features/Responsibilities/RevokeResponsibility.cs#L8) | [RevokeResponsibilityHandler](../../src/Compliance.Core/Features/Responsibilities/RevokeResponsibilityHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1985) | Partial / [G21](#g21) | [ScopeResponsibilityIndependenceTests](../../test/Compliance.Tests/Features/AccessControl/ScopeResponsibilityIndependenceTests.cs) (+4) |

### Retention — R2-12

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [PlaceArtifactLegalHold](../../src/Compliance.Common/Features/Retention/PlaceArtifactLegalHold.cs#L7) | [PlaceArtifactLegalHoldHandler](../../src/Compliance.Core/Features/Retention/PlaceArtifactLegalHoldHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1863) | Partial / [G02](#g02) | [ArtifactRetentionHandlerTests](../../test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs) (+1) |
| [RecordArtifactRetentionBasis](../../src/Compliance.Common/Features/Retention/RecordArtifactRetentionBasis.cs#L7) | [RecordArtifactRetentionBasisHandler](../../src/Compliance.Core/Features/Retention/RecordArtifactRetentionBasisHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1859) | Partial / [G02](#g02) | [ArtifactRetentionHandlerTests](../../test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs) (+1) |
| [ReleaseArtifactLegalHold](../../src/Compliance.Common/Features/Retention/ReleaseArtifactLegalHold.cs#L7) | [ReleaseArtifactLegalHoldHandler](../../src/Compliance.Core/Features/Retention/ReleaseArtifactLegalHoldHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1867) | Partial / [G02](#g02) | [ArtifactRetentionHandlerTests](../../test/Compliance.Tests/Features/Retention/ArtifactRetentionHandlerTests.cs) (+1) |

### Risks — R1-07

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AcceptRisk](../../src/Compliance.Common/Features/Risks/AcceptRisk.cs#L13) | [AcceptRiskHandler](../../src/Compliance.Core/Features/Risks/AcceptRiskHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1059) | Exists | [RiskAcceptanceWorkAuthorityTests](../../test/Compliance.Tests/Features/Work/RiskAcceptanceWorkAuthorityTests.cs) (+2) |
| [AddRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/AddRiskTreatmentAction.cs#L14) | [AddRiskTreatmentActionHandler](../../src/Compliance.Core/Features/Risks/AddRiskTreatmentActionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1088) | Exists | [RiskGovernanceHandlerTests](../../test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs) |
| [AssignRiskOwner](../../src/Compliance.Common/Features/Risks/AssignRiskOwner.cs#L9) | [AssignRiskOwnerHandler](../../src/Compliance.Core/Features/Risks/AssignRiskOwnerHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1072) | Exists | [RiskGovernanceHandlerTests](../../test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs) |
| [CancelRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/CancelRiskTreatmentAction.cs#L11) | [CancelRiskTreatmentActionHandler](../../src/Compliance.Core/Features/Risks/CancelRiskTreatmentActionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1096) | Exists | No direct request reference |
| [ChooseRiskTreatment](../../src/Compliance.Common/Features/Risks/ChooseRiskTreatment.cs#L8) | [ChooseRiskTreatmentHandler](../../src/Compliance.Core/Features/Risks/ChooseRiskTreatmentHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1055) | Exists | No direct request reference |
| [CreateRiskDraft](../../src/Compliance.Common/Features/Risks/CreateRiskDraft.cs#L8) | [CreateRiskDraftHandler](../../src/Compliance.Core/Features/Risks/CreateRiskDraftHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1015) | Exists | No direct request reference |
| [ProposeRiskControlTreatment](../../src/Compliance.Common/Features/Risks/ProposeRiskControlTreatment.cs#L12) | [ProposeRiskControlTreatmentHandler](../../src/Compliance.Core/Features/Risks/ProposeRiskControlTreatmentHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1076) | Exists | [AttestRiskManagementWriteWallTests](../../test/Compliance.Tests/Features/Risks/AttestRiskManagementWriteWallTests.cs) (+2) |
| [PublishRiskMethodVersion](../../src/Compliance.Common/Features/Risks/PublishRiskMethodVersion.cs#L8) | [PublishRiskMethodVersionHandler](../../src/Compliance.Core/Features/Risks/PublishRiskMethodVersionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1039) | Exists | No direct request reference |
| [RaiseRiskReassessmentTriggers](../../src/Compliance.Common/Features/Risks/RaiseRiskReassessmentTriggers.cs#L10) | [RaiseRiskReassessmentTriggersHandler](../../src/Compliance.Core/Features/Risks/RaiseRiskReassessmentTriggersHandler.cs) | internal | Partial / [G07](#g07) | [RiskGovernanceHandlerTests](../../test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs) |
| [RecordRiskAssessment](../../src/Compliance.Common/Features/Risks/RecordRiskAssessment.cs#L8) | [RecordRiskAssessmentHandler](../../src/Compliance.Core/Features/Risks/RecordRiskAssessmentHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1051) | Exists | [RiskGovernanceHandlerTests](../../test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs) |
| [RetireRiskControlTreatment](../../src/Compliance.Common/Features/Risks/RetireRiskControlTreatment.cs#L9) | [RetireRiskControlTreatmentHandler](../../src/Compliance.Core/Features/Risks/RetireRiskControlTreatmentHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1084) | Exists | No direct request reference |
| [ReviewRiskControlTreatment](../../src/Compliance.Common/Features/Risks/ReviewRiskControlTreatment.cs#L13) | [ReviewRiskControlTreatmentHandler](../../src/Compliance.Core/Features/Risks/ReviewRiskControlTreatmentHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1080) | Exists | [ReviewerWorkAuthorityTests](../../test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs) (+1) |
| [ReviewRiskTreatmentActionCompletion](../../src/Compliance.Common/Features/Risks/ReviewRiskTreatmentActionCompletion.cs#L13) | [ReviewRiskTreatmentActionCompletionHandler](../../src/Compliance.Core/Features/Risks/ReviewRiskTreatmentActionCompletionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1105) | Exists | [ReviewerWorkAuthorityTests](../../test/Compliance.Tests/Features/Work/ReviewerWorkAuthorityTests.cs) (+1) |
| [ReviseRiskDraft](../../src/Compliance.Common/Features/Risks/ReviseRiskDraft.cs#L8) | [ReviseRiskDraftHandler](../../src/Compliance.Core/Features/Risks/ReviseRiskDraftHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1019) | Exists | No direct request reference |
| [ReviseRiskTreatmentAction](../../src/Compliance.Common/Features/Risks/ReviseRiskTreatmentAction.cs#L9) | [ReviseRiskTreatmentActionHandler](../../src/Compliance.Core/Features/Risks/ReviseRiskTreatmentActionHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1092) | Exists | [RiskGovernanceHandlerTests](../../test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs) |
| [SubmitRiskTreatmentActionCompletion](../../src/Compliance.Common/Features/Risks/SubmitRiskTreatmentActionCompletion.cs#L13) | [SubmitRiskTreatmentActionCompletionHandler](../../src/Compliance.Core/Features/Risks/SubmitRiskTreatmentActionCompletionHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1100) | Exists | [RiskGovernanceHandlerTests](../../test/Compliance.Tests/Features/Risks/RiskGovernanceHandlerTests.cs) |

### Snapshots — EN-03, R1-02

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AmendProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/AmendProgramScopeSnapshot.cs#L8) | [AmendProgramScopeSnapshotHandler](../../src/Compliance.Core/Features/Snapshots/AmendProgramScopeSnapshotHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2026) | Exists | [ScopeSnapshotTests](../../test/Compliance.Tests/Features/Snapshots/ScopeSnapshotTests.cs) (+1) |
| [FreezeProgramScopeSnapshot](../../src/Compliance.Common/Features/Snapshots/FreezeProgramScopeSnapshot.cs#L8) | [FreezeProgramScopeSnapshotHandler](../../src/Compliance.Core/Features/Snapshots/FreezeProgramScopeSnapshotHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2022) | Exists | [ScopeSnapshotTests](../../test/Compliance.Tests/Features/Snapshots/ScopeSnapshotTests.cs) (+1) |

### TechnologyInventory — R1-12

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [RecordDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/RecordDataFlow.cs#L7) | [RecordDataFlowHandler](../../src/Compliance.Core/Features/TechnologyInventory/RecordDataFlowHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1708) | Exists | No direct request reference |
| [RecordInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/RecordInformationAsset.cs#L7) | [RecordInformationAssetHandler](../../src/Compliance.Core/Features/TechnologyInventory/RecordInformationAssetHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1643) | Exists | [TechnologyInventoryScopedGrantTests](../../test/Compliance.Tests/Features/TechnologyInventory/TechnologyInventoryScopedGrantTests.cs) |
| [RecordLocation](../../src/Compliance.Common/Features/TechnologyInventory/RecordLocation.cs#L7) | [RecordLocationHandler](../../src/Compliance.Core/Features/TechnologyInventory/RecordLocationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1668) | Exists | No direct request reference |
| [RecordOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/RecordOperationalProcess.cs#L7) | [RecordOperationalProcessHandler](../../src/Compliance.Core/Features/TechnologyInventory/RecordOperationalProcessHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1688) | Exists | No direct request reference |
| [RecordTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/RecordTechnologyComponent.cs#L7) | [RecordTechnologyComponentHandler](../../src/Compliance.Core/Features/TechnologyInventory/RecordTechnologyComponentHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1611) | Exists | [InventoryManagementIndependenceTests](../../test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs) |
| [ReviseDataFlow](../../src/Compliance.Common/Features/TechnologyInventory/ReviseDataFlow.cs#L7) | [ReviseDataFlowHandler](../../src/Compliance.Core/Features/TechnologyInventory/ReviseDataFlowHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1712) | Exists | No direct request reference |
| [ReviseInformationAsset](../../src/Compliance.Common/Features/TechnologyInventory/ReviseInformationAsset.cs#L6) | [ReviseInformationAssetHandler](../../src/Compliance.Core/Features/TechnologyInventory/ReviseInformationAssetHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1647) | Exists | No direct request reference |
| [ReviseLocation](../../src/Compliance.Common/Features/TechnologyInventory/ReviseLocation.cs#L7) | [ReviseLocationHandler](../../src/Compliance.Core/Features/TechnologyInventory/ReviseLocationHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1672) | Exists | No direct request reference |
| [ReviseOperationalProcess](../../src/Compliance.Common/Features/TechnologyInventory/ReviseOperationalProcess.cs#L6) | [ReviseOperationalProcessHandler](../../src/Compliance.Core/Features/TechnologyInventory/ReviseOperationalProcessHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1692) | Exists | No direct request reference |
| [ReviseTechnologyComponent](../../src/Compliance.Common/Features/TechnologyInventory/ReviseTechnologyComponent.cs#L7) | [ReviseTechnologyComponentHandler](../../src/Compliance.Core/Features/TechnologyInventory/ReviseTechnologyComponentHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1615) | Exists | [InventoryManagementIndependenceTests](../../test/Compliance.Tests/Features/AccessControl/InventoryManagementIndependenceTests.cs) |

### Tenants — R1-04/15, F1-01

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AcceptTenantInvitation](../../src/Compliance.Common/Features/Tenants/AcceptTenantInvitation.cs#L6) | [AcceptTenantInvitationHandler](../../src/Compliance.Core/Features/Tenants/AcceptTenantInvitationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L537) | Exists | [TenantInvitationConcurrencyTests](../../test/Compliance.Tests/Features/Tenants/TenantInvitationConcurrencyTests.cs) |
| [ActivateTenant](../../src/Compliance.Common/Features/Tenants/ActivateTenant.cs#L6) | [ActivateTenantHandler](../../src/Compliance.Core/Features/Tenants/ActivateTenantHandler.cs) | internal | Exists | [TenantRbacBootstrapReactorTests](../../test/Compliance.Tests/Features/AccessControl/TenantRbacBootstrapReactorTests.cs) (+4) |
| [ChangeTenantSlug](../../src/Compliance.Common/Features/Tenants/ChangeTenantSlug.cs#L6) | [ChangeTenantSlugHandler](../../src/Compliance.Core/Features/Tenants/ChangeTenantSlugHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L553) | Exists | [PlatformOperatorOrTenantAdminAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/PlatformOperatorOrTenantAdminAuthorizerTests.cs) (+1) |
| [ConfirmTenantSlug](../../src/Compliance.Common/Features/Tenants/ConfirmTenantSlug.cs#L6) | [ConfirmTenantSlugHandler](../../src/Compliance.Core/Features/Tenants/ConfirmTenantSlugHandler.cs) | internal | Exists | [TenantLifecycleReactionAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionAuthorizerTests.cs) |
| [ConfirmTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/ConfirmTenantSlugSurrender.cs#L6) | [ConfirmTenantSlugSurrenderHandler](../../src/Compliance.Core/Features/Tenants/ConfirmTenantSlugSurrenderHandler.cs) | internal | Exists | [TenantLifecycleReactionAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionAuthorizerTests.cs) |
| [GrantPlatformOperator](../../src/Compliance.Common/Features/Tenants/GrantPlatformOperator.cs#L6) | [GrantPlatformOperatorHandler](../../src/Compliance.Core/Features/Tenants/GrantPlatformOperatorHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L494) | Exists | No direct request reference |
| [InviteOrganizationMember](../../src/Compliance.Common/Features/Tenants/InviteOrganizationMember.cs#L7) | [InviteOrganizationMemberHandler](../../src/Compliance.Core/Features/Tenants/InviteOrganizationMemberHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L506) | Partial / [G03](#g03), [G21](#g21) | [TenantInvitationConcurrencyTests](../../test/Compliance.Tests/Features/Tenants/TenantInvitationConcurrencyTests.cs) |
| [InviteTenantMember](../../src/Compliance.Common/Features/Tenants/InviteTenantMember.cs#L6) | [InviteTenantMemberHandler](../../src/Compliance.Core/Features/Tenants/InviteTenantMemberHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L503) | Exists | No direct request reference |
| [OffboardTenant](../../src/Compliance.Common/Features/Tenants/OffboardTenant.cs#L6) | [OffboardTenantHandler](../../src/Compliance.Core/Features/Tenants/OffboardTenantHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L491) | Partial / [G18](#g18) | [TenantTests](../../test/Compliance.Tests/Features/Tenants/TenantTests.cs) |
| [ReactivateTenant](../../src/Compliance.Common/Features/Tenants/ReactivateTenant.cs#L6) | [ReactivateTenantHandler](../../src/Compliance.Core/Features/Tenants/ReactivateTenantHandler.cs) | [DELETE](../../src/Compliance.App/Program.cs#L488) | Exists | No direct request reference |
| [RegisterTenant](../../src/Compliance.Common/Features/Tenants/RegisterTenant.cs#L10) | [RegisterTenantHandler](../../src/Compliance.Core/Features/Tenants/RegisterTenantHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L482) | Exists | [RequestScenarioJsonContext](../../test/Compliance.Tests/RequestScenarioJsonContext.cs) (+5) |
| [RegisterTenantOwner](../../src/Compliance.Common/Features/Tenants/RegisterTenantOwner.cs#L6) | [RegisterTenantOwnerHandler](../../src/Compliance.Core/Features/Tenants/RegisterTenantOwnerHandler.cs) | internal | Exists | [TenantLifecycleReactionAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionAuthorizerTests.cs) |
| [RegisterTenantSlug](../../src/Compliance.Common/Features/Tenants/RegisterTenantSlug.cs#L6) | [RegisterTenantSlugHandler](../../src/Compliance.Core/Features/Tenants/RegisterTenantSlugHandler.cs) | internal | Exists | [TenantLifecycleReactionRequestScenarioTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionRequestScenarioTests.cs) (+1) |
| [RejectTenantSlug](../../src/Compliance.Common/Features/Tenants/RejectTenantSlug.cs#L6) | [RejectTenantSlugHandler](../../src/Compliance.Core/Features/Tenants/RejectTenantSlugHandler.cs) | internal | Exists | [TenantLifecycleReactionAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionAuthorizerTests.cs) |
| [RejectTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/RejectTenantSlugSurrender.cs#L6) | [RejectTenantSlugSurrenderHandler](../../src/Compliance.Core/Features/Tenants/RejectTenantSlugSurrenderHandler.cs) | internal | Exists | [TenantLifecycleReactionAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionAuthorizerTests.cs) |
| [RequestTenantSlugSurrender](../../src/Compliance.Common/Features/Tenants/RequestTenantSlugSurrender.cs#L6) | [RequestTenantSlugSurrenderHandler](../../src/Compliance.Core/Features/Tenants/RequestTenantSlugSurrenderHandler.cs) | internal | Exists | No direct request reference |
| [RevokePlatformOperator](../../src/Compliance.Common/Features/Tenants/RevokePlatformOperator.cs#L6) | [RevokePlatformOperatorHandler](../../src/Compliance.Core/Features/Tenants/RevokePlatformOperatorHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L497) | Exists | No direct request reference |
| [SeedPlatformOperatorRoster](../../src/Compliance.Common/Features/Tenants/SeedPlatformOperatorRoster.cs#L5) | [SeedPlatformOperatorRosterHandler](../../src/Compliance.Core/Features/Tenants/SeedPlatformOperatorRosterHandler.cs) | internal | Exists | No direct request reference |
| [SurrenderTenantSlug](../../src/Compliance.Common/Features/Tenants/SurrenderTenantSlug.cs#L6) | [SurrenderTenantSlugHandler](../../src/Compliance.Core/Features/Tenants/SurrenderTenantSlugHandler.cs) | internal | Exists | [TenantLifecycleReactionAuthorizerTests](../../test/Compliance.Tests/Features/Tenants/TenantLifecycleReactionAuthorizerTests.cs) |
| [SuspendTenant](../../src/Compliance.Common/Features/Tenants/SuspendTenant.cs#L6) | [SuspendTenantHandler](../../src/Compliance.Core/Features/Tenants/SuspendTenantHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L485) | Exists | No direct request reference |

### UserIdentities — EN-01, R1-04

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [CompleteEmailChallenge](../../src/Compliance.Common/Features/UserIdentities/CompleteEmailChallenge.cs#L6) | [CompleteEmailChallengeHandler](../../src/Compliance.Core/Features/UserIdentities/CompleteEmailChallengeHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2105) | Exists | [CompleteEmailChallengeConcurrencyTests](../../test/Compliance.Tests/Features/UserIdentities/CompleteEmailChallengeConcurrencyTests.cs) |
| [CompleteIdentityRecovery](../../src/Compliance.Common/Features/UserIdentities/CompleteIdentityRecovery.cs#L6) | [CompleteIdentityRecoveryHandler](../../src/Compliance.Core/Features/UserIdentities/CompleteIdentityRecoveryHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L478) | Exists | [IdentityRecoveryHandlerTests](../../test/Compliance.Tests/Features/UserIdentities/IdentityRecoveryHandlerTests.cs) |
| [ContinueWithDeveloperIdentity](../../src/Compliance.Common/Features/UserIdentities/ContinueWithDeveloperIdentity.cs#L7) | [ContinueWithDeveloperIdentityHandler](../../src/Compliance.Core/Features/UserIdentities/ContinueWithDeveloperIdentityHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L454) | Exists | [ContinueWithDeveloperIdentityRequestScenarioTests](../../test/Compliance.Tests/Features/UserIdentities/ContinueWithDeveloperIdentityRequestScenarioTests.cs) (+2) |
| [ContinueWithOidcProvider](../../src/Compliance.Common/Features/UserIdentities/ContinueWithOidcProvider.cs#L7) | [ContinueWithOidcProviderHandler](../../src/Compliance.Core/Features/UserIdentities/ContinueWithOidcProviderHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L462) | Exists | [UserIdentityContinuationTests](../../test/Compliance.Tests/Features/UserIdentities/UserIdentityContinuationTests.cs) (+3) |
| [IssueEmailChallenge](../../src/Compliance.Common/Features/UserIdentities/IssueEmailChallenge.cs#L6) | [IssueEmailChallengeHandler](../../src/Compliance.Core/Features/UserIdentities/IssueEmailChallengeHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2098) | Exists | No direct request reference |
| [LinkOidcProviderIdentity](../../src/Compliance.Common/Features/UserIdentities/LinkOidcProviderIdentity.cs#L7) | [LinkOidcProviderIdentityHandler](../../src/Compliance.Core/Features/UserIdentities/LinkOidcProviderIdentityHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L467) | Exists | [UserIdentityContinuationTests](../../test/Compliance.Tests/Features/UserIdentities/UserIdentityContinuationTests.cs) (+1) |
| [ReserveEmail](../../src/Compliance.Common/Features/UserIdentities/ReserveEmail.cs#L6) | [ReserveEmailHandler](../../src/Compliance.Core/Features/UserIdentities/ReserveEmailHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2095) | Exists | [EmailReservationReactorTests](../../test/Compliance.Tests/Features/UserIdentities/EmailReservationReactorTests.cs) |
| [StartIdentityRecovery](../../src/Compliance.Common/Features/UserIdentities/StartIdentityRecovery.cs#L6) | [StartIdentityRecoveryHandler](../../src/Compliance.Core/Features/UserIdentities/StartIdentityRecoveryHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L471) | Exists | [IdentityRecoveryHandlerTests](../../test/Compliance.Tests/Features/UserIdentities/IdentityRecoveryHandlerTests.cs) |

### Work — R2-11

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AssignWorkItem](../../src/Compliance.Common/Features/Work/AssignWorkItem.cs#L8) | [AssignWorkItemHandler](../../src/Compliance.Core/Features/Work/AssignWorkItemHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1436) | Partial / [G03](#g03), [G08](#g08) | [PolicyAcknowledgementIndependenceTests](../../test/Compliance.Tests/Features/PolicyDistribution/PolicyAcknowledgementIndependenceTests.cs) (+8) |
| [ClaimWorkItem](../../src/Compliance.Common/Features/Work/ClaimWorkItem.cs#L8) | [ClaimWorkItemHandler](../../src/Compliance.Core/Features/Work/ClaimWorkItemHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1432) | Partial / [G08](#g08) | [PolicyAcknowledgementIndependenceTests](../../test/Compliance.Tests/Features/PolicyDistribution/PolicyAcknowledgementIndependenceTests.cs) (+3) |
| [DelegateWorkItem](../../src/Compliance.Common/Features/Work/DelegateWorkItem.cs#L8) | [DelegateWorkItemHandler](../../src/Compliance.Core/Features/Work/DelegateWorkItemHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1440) | Partial / [G08](#g08) | [PolicyAcknowledgementIndependenceTests](../../test/Compliance.Tests/Features/PolicyDistribution/PolicyAcknowledgementIndependenceTests.cs) (+3) |
| [EscalateWorkItem](../../src/Compliance.Common/Features/Work/EscalateWorkItem.cs#L8) | [EscalateWorkItemHandler](../../src/Compliance.Core/Features/Work/EscalateWorkItemHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1444) | Partial / [G08](#g08) | [WorkAssignmentTests](../../test/Compliance.Tests/Features/Work/WorkAssignmentTests.cs) (+1) |
| [SetWorkDigestPreference](../../src/Compliance.Common/Features/Work/SetWorkDigestPreference.cs#L8) | [SetWorkDigestPreferenceHandler](../../src/Compliance.Core/Features/Work/SetWorkDigestPreferenceHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1452) | Exists | [WorkReminderTests](../../test/Compliance.Tests/Features/Work/WorkReminderTests.cs) |

### Workforce — R1-11, EN-03

| Command | Handler | Exposure | Status / gap | Test-reference example |
| --- | --- | --- | --- | --- |
| [AmendWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/AmendWorkforceRosterSnapshot.cs#L12) | [AmendWorkforceRosterSnapshotHandler](../../src/Compliance.Core/Features/Workforce/AmendWorkforceRosterSnapshotHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2051) | Exists | [WorkforceRosterSnapshotTests](../../test/Compliance.Tests/Features/Workforce/WorkforceRosterSnapshotTests.cs) (+1) |
| [CorrelatePersonMembership](../../src/Compliance.Common/Features/Workforce/CorrelatePersonMembership.cs#L13) | [CorrelatePersonMembershipHandler](../../src/Compliance.Core/Features/Workforce/CorrelatePersonMembershipHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1464) | Exists | [AttestWorkforceManagementWallTests](../../test/Compliance.Tests/Features/Workforce/AttestWorkforceManagementWallTests.cs) |
| [FreezeWorkforceRosterSnapshot](../../src/Compliance.Common/Features/Workforce/FreezeWorkforceRosterSnapshot.cs#L9) | [FreezeWorkforceRosterSnapshotHandler](../../src/Compliance.Core/Features/Workforce/FreezeWorkforceRosterSnapshotHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L2047) | Exists | [PopulationSnapshotChunkingTests](../../test/Compliance.Tests/Features/Snapshots/PopulationSnapshotChunkingTests.cs) (+2) |
| [ReconcileWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/ReconcileWorkforceSourceObservation.cs#L12) | [ReconcileWorkforceSourceObservationHandler](../../src/Compliance.Core/Features/Workforce/ReconcileWorkforceSourceObservationHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1516) | Exists | [PersonalWorkforceObservationTransportTests](../../test/Compliance.Tests/Features/Workforce/PersonalWorkforceObservationTransportTests.cs) (+2) |
| [RecordPerson](../../src/Compliance.Common/Features/Workforce/RecordPerson.cs#L8) | [RecordPersonHandler](../../src/Compliance.Core/Features/Workforce/RecordPersonHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1456) | Exists | [WorkforceAuthorizerTests](../../test/Compliance.Tests/Features/Workforce/WorkforceAuthorizerTests.cs) (+1) |
| [RecordServiceIdentity](../../src/Compliance.Common/Features/Workforce/RecordServiceIdentity.cs#L13) | [RecordServiceIdentityHandler](../../src/Compliance.Core/Features/Workforce/RecordServiceIdentityHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1525) | Exists | [AttestWorkforceManagementWallTests](../../test/Compliance.Tests/Features/Workforce/AttestWorkforceManagementWallTests.cs) |
| [RecordWorkRelationship](../../src/Compliance.Common/Features/Workforce/RecordWorkRelationship.cs#L11) | [RecordWorkRelationshipHandler](../../src/Compliance.Core/Features/Workforce/RecordWorkRelationshipHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1476) | Exists | [RecordWorkRelationshipHandlerTests](../../test/Compliance.Tests/Features/Workforce/RecordWorkRelationshipHandlerTests.cs) (+1) |
| [RecordWorkforceSourceObservation](../../src/Compliance.Common/Features/Workforce/RecordWorkforceSourceObservation.cs#L8) | [RecordWorkforceSourceObservationHandler](../../src/Compliance.Core/Features/Workforce/RecordWorkforceSourceObservationHandler.cs) | [POST](../../src/Compliance.App/Program.cs#L1500) | Exists | [AttestWorkforceManagementWallTests](../../test/Compliance.Tests/Features/Workforce/AttestWorkforceManagementWallTests.cs) (+1) |
| [ResolveWorkforceObservation](../../src/Compliance.Common/Features/Workforce/ResolveWorkforceObservation.cs#L14) | [ResolveWorkforceObservationHandler](../../src/Compliance.Core/Features/Workforce/ResolveWorkforceObservationHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1496) | Exists | [PersonalWorkforceObservationTransportTests](../../test/Compliance.Tests/Features/Workforce/PersonalWorkforceObservationTransportTests.cs) (+1) |
| [RevisePerson](../../src/Compliance.Common/Features/Workforce/RevisePerson.cs#L7) | [RevisePersonHandler](../../src/Compliance.Core/Features/Workforce/RevisePersonHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1460) | Exists | [AttestWorkforceManagementWallTests](../../test/Compliance.Tests/Features/Workforce/AttestWorkforceManagementWallTests.cs) |
| [ReviseServiceIdentity](../../src/Compliance.Common/Features/Workforce/ReviseServiceIdentity.cs#L9) | [ReviseServiceIdentityHandler](../../src/Compliance.Core/Features/Workforce/ReviseServiceIdentityHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1529) | Exists | [AttestWorkforceManagementWallTests](../../test/Compliance.Tests/Features/Workforce/AttestWorkforceManagementWallTests.cs) |
| [ReviseWorkRelationship](../../src/Compliance.Common/Features/Workforce/ReviseWorkRelationship.cs#L8) | [ReviseWorkRelationshipHandler](../../src/Compliance.Core/Features/Workforce/ReviseWorkRelationshipHandler.cs) | [PUT](../../src/Compliance.App/Program.cs#L1480) | Exists | [AttestWorkforceManagementWallTests](../../test/Compliance.Tests/Features/Workforce/AttestWorkforceManagementWallTests.cs) |

### Transport adapter

- [ReviseApplicationMcpInput](../../src/Compliance.App/Features/Applications/ReviseApplicationMcpInput.cs) adapts MCP arguments to ReviseApplication. Count the underlying application revision intent once.

### Internal mutations outside request contracts

| Entry point | What exists / remaining gap |
| --- | --- |
| [EvidenceIntake.CaptureAsync](../../src/Compliance.Core/Features/Evidence/EvidenceIntake.cs) | Internal capture orchestration; registered bytes and resumable inspection/storage effects. Public capture is G01. |
| [EvidenceArtifact.Register](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs) | Internal aggregate registration used by capture; not a second business command in addition to CaptureEvidenceArtifact. |
| [EvidenceArtifact.RecordInspection](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs) | Internal retained verdict, used by capture. Qualified production scan composition is G01. |
| [EvidenceArtifact.ReleaseQuarantine](../../src/Compliance.Core/Features/Evidence/EvidenceArtifact.cs) | Domain transition exists. Authorized clean-rescan release workflow is G01. |
| [IndependenceLedger.AcceptEngagement](../../src/Compliance.Core/Features/AccessControl/IndependenceLedger.Acceptance.cs) | Verified-proof domain acceptance exists; personal production acceptance orchestration is G17. |
| [IndependenceLedger.RemoveActualStaff](../../src/Compliance.Core/Features/AccessControl/IndependenceLedger.Revocation.cs) | Actual-assignment removal exists internally; public professional revocation is G17. |
| [POST /auth/logout](../../src/Compliance.App/Program.cs#L441) | Browser-session cookie clearing exists as a raw HTTP action; no domain IRequest needed. |

## Required command gaps and reuse decisions

For Missing rows, the absence claim is about the required owning workflow, not absence of every possible string or low-level helper. Each group records its requirement source and the nearest existing behavior. New identifiers are candidate names; accepted intent and invariants are the contract.

<a id="g01"></a>

### G01 — Governed evidence capture and support

Scope: **EN-06, R2-03/04/05, R1-14**. Requirement source: [evidence-intake-recovery-v1](evidence-intake-recovery-v1.md).

EvidenceIntake.CaptureAsync registers and inspects bytes internally. EvidenceIntake is not composed as a public upload handler; Program.cs has no upload route. The default UninspectedArtifactInspector never returns clean. Existing request fulfilment requires an available canonical artifact.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Partial | `CaptureEvidenceArtifact` | Expose authorized manual upload with original metadata, hash identity, size/type checks, durable inspection progress and attributable collector; adapt existing EvidenceIntake rather than create another artifact aggregate. |
| Partial | `RecordEvidenceArtifactInspection` | Compose a qualified scan engine and durable pending-inspection processing around the existing RecordInspection mutation. Internal system operation; no user may assert an unverified clean result. |
| Missing | `IssueArtifactDownload` | Authorize exact artifact/version independently, record delivery issuance and return usable bytes only for an available artifact; a metadata read is insufficient. |
| Missing | `LinkEvidenceSupport` | Record an exact artifact-to-control/version/occurrence/evaluation/period relationship with purpose and contextual sufficiency. Existing fulfilment only supplies request-specific support. |
| Partial | `FulfilEvidenceRequest`; `AttestControlOccurrence`; `CorrectControlAttestation`; `RecordControlEvaluationStep`; `ReviewControlEvaluation`; `RecordAssuranceReport`; `RecordProviderReview`; `CloseFinding`; `CompleteCorrectiveAction`; `RecordTrainingCompletion` | Connect existing evidence-reference writes to governed content and appropriate exact support/period checks; do not add duplicate submission commands. |
| Partial | `ReleaseEvidenceArtifactQuarantine` | Wrap existing ReleaseQuarantine with personal Org Admin authority, a retained clean rescan and rationale. Secrets/invalid uploads are never releasable. |

<a id="g02"></a>

### G02 — Evidence handling, disclosure, holds and disposition

Scope: **R2-12, EN-06**. Requirement source: [artifact-retention-contract](artifact-retention-contract.md).

Retention supports evidence artifacts and application import sources, one retention basis and legal-hold metadata. Every assessment returns disposition_allowed:false and reports storage_hold_enforcement_pending or inline_disposition_unsupported. Redaction lineage is metadata only (evidence-redaction-lineage.md).

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `AssignArtifactHandlingPolicy` | Record governed handling/access assignment and applicable policy version across evidence, provider reports, snapshots, imports, responses and packages. |
| Partial | `RecordArtifactRetentionBasis` | Extend supported source families and any approved period-amendment path; preserve immutable source identity. The current fixed seven-year basis is already implemented. |
| Partial | `PlaceArtifactLegalHold`; `ReleaseArtifactLegalHold` | Enforce canonical holds at the byte-store boundary and preserve them through retries/recovery; current commands retain decisions but do not call storage hold hooks. |
| Missing | `PlaceArtifactEngagementHold`; `RecordEngagementReportIssued`; `ReleaseArtifactEngagementHold` | Bind hold scope to the engagement/report and release authority, including report issuance plus one year and outstanding own-attest delivery requirements. |
| Partial | `PrepareEvidenceRedaction`; `ApproveEvidenceRedaction` | Retain existing lineage/approval commands; add an authorized byte-derivation workflow and govern actual delivery of the approved derivative. |
| Missing | `ApproveArtifactDisclosure`; `RecordArtifactDisclosureDelivery`; `RevokeArtifactDisclosure` | Bind approval and actual external delivery to exact content, recipient, purpose, actor and time. Revoke further access where supported without claiming downloaded bytes can be recalled. |
| Missing | `ApproveArtifactDisposition`; `ExecuteArtifactDisposition`; `RecordArtifactDispositionResult` | Require explicit Org Admin approval after retention, holds and historical reliance checks. Execute safely, retain tombstones and partial/failure state; no automatic expiry deletion. |

<a id="g03"></a>

### G03 — Advisor handoffs and feedback

Scope: **R2-08**. Requirement source: [decisions/m0-d14-advisor-collaboration](decisions/m0-d14-advisor-collaboration.md).

There are no AdvisorHandoff/AdvisorFeedback request contracts, handlers or aggregates. AnnotateReadinessGap is a narrower readiness annotation; membership and grants are reusable.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `CreateAdvisorHandoff`; `ReviseAdvisorHandoffSelection`; `MarkAdvisorHandoffReadyForReview` | Select exact draft versions and questions and explicitly open bounded in-product review access; no R2 export package or secure-link subsystem. |
| Missing | `CloseAdvisorHandoff`; `RevokeAdvisorHandoff` | End access to selected drafts while preserving handoff history. |
| Missing | `RecordAdvisorFeedback`; `CorrectAdvisorFeedback`; `RespondToAdvisorFeedback` | Retain immutable comment/change_request/validated feedback, actual author and exact target version; correction is a linked new entry. Validation never approves a record. |
| Partial | `InviteOrganizationMember`; `GrantAccess`; `RevokeAccessGrant`; `AssignWorkItem` | Reuse scoped expiring guest membership, grants and source-owned accountability. Add handoff/feedback consumers and authority checks; do not create a second consultant identity or generic task workflow. |

<a id="g04"></a>

### G04 — Readiness and source integration

Scope: **R1-08, R2-09, T2-04**. Requirement source: [readiness-catalog-support-gaps-v1](readiness-catalog-support-gaps-v1.md).

RunReadinessAssessment persists versioned inputs/results; ReadinessRules explicitly records unassessed source families. Readiness catalog limitations and Privacy gaps are not resolved by management annotations.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Partial | `RunReadinessAssessment` | Complete workforce completeness and governed-evidence assessment, broader required source/rule coverage and exact input retention. Current readiness-rules/12 already assesses effective commitments and provider CSOCs; do not treat that existing coverage as missing. Preserve selected-category limitations. |
| Partial | `PlanReadinessGap` | Reconcile severity, target stage, control links and remaining plan policy under the owning readiness contract; keep stable gap identities. |
| Partial | `DecideReadiness`; `DecideTypeIEntry` | Retain exact-version personal decisions; complete their source/snapshot prerequisites without treating entry approval as an examination baseline or auditor opinion. |
| Decision | `PublishReadinessRuleVersion`; `RecordReadinessMonitoringResult` | Determine whether rule versions remain code-owned or need governed authoring, and which T2 monitoring results must be retained for management-review evidence. Do not add a generic calculation-state command prematurely. |

<a id="g05"></a>

### G05 — Control operation, recurrence and management review

Scope: **R2-01/04, T2-02/03/06/09**. Requirement source: [control-operation-v1](control-operation-v1.md).

Operating plans calculate expected cadence and OpenControlOccurrence retains deterministic occurrences. The repository contains no registered recurring-occurrence scheduling command. ReviewControlOccurrence already supports approved/action_requested/deferred; the finding reactor routes requested actions.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `GenerateDueControlOccurrences` | Materialize approved recurring occurrences with deterministic identities and durable generation provenance across retries/restarts. Reuse OpenControlOccurrence; event-driven/ad hoc opening already exists. |
| Partial | `ProposeControlOperatingPlan`; `ApproveControlOperatingPlan` | Extend period-aware future schedule changes and reconcile added/retained/removed work when TypeIIPeriod exists; retain recorded reassignments and historical work. |
| Partial | `AttestControlOccurrence`; `ReviewControlOccurrence`; `RaiseOccurrenceFinding` | Bind management-review submissions to exact readiness/monitoring versions and agenda/minutes artifacts; route requested actions through existing findings. No separate ManagementReviewSnapshot or governance-review approval command. |
| Decision | `RaiseOccurrenceFinding`; `RaiseEvaluationDeviationFinding` | Ratify or replace the existing automatic severity/deadline/fallback-owner defaults (#274/#279); these names already exist, so this is a policy gap, not missing command code. |

<a id="g06"></a>

### G06 — Imports beyond applications

Scope: **R1-09, EN-05**. Requirement source: [backlog](backlog.md).

Application import has stage/correlate/accept/cancel/execution contracts and a durable visibility barrier. Workforce observations are manually recorded facts, not a CSV importer. No readiness-material multi-family import contract exists.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `StageReadinessMaterialImport`; `ResolveReadinessMaterialImportRow`; `AcceptReadinessMaterialImport`; `CancelReadinessMaterialImport`; `ExecuteReadinessMaterialImport` | Deliver staged, explicitly accepted imports for controls, mappings, owners, policies/files, evidence/files, boundaries, workforce, technology/information, commitments, risks, providers and findings. Keep record-family slices, provenance, atomic visible effects, rejected reports, replay and conflict reconciliation. |
| Decision | `StageWorkforceImport`; `StageTechnologyInventoryImport`; `StageAccessPopulationImport`; `StageCriteriaOverlayImport` | Choose typed adapters versus reuse of a bounded import command after exact source/rights/schema discovery. Manual records and RecordAccessPopulationFacts remain the base path. |
| Partial | `FreezeApplicationImportOmissionProposal` | Retain the sealed proposal; reconcile its downstream explicit retirement decision/consumer with application lifecycle. Missing source rows never silently retire entities. |

<a id="g07"></a>

### G07 — Existing authoring and cross-record lifecycle

Scope: **R1-02/05/07/10/12/13/14, EN-02**. Requirement source: [control-draft-v1](control-draft-v1.md).

Bounded feature contracts retain unresolved contexts: control risk/process applicability, commitment evidence/risk/engagement links, application lifecycle impact and provider evidence integration. Existing authoring commands should be extended where their intent is unchanged.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Partial | `ApproveControl`; `ProposeControlSuccessor`; `RetireControl`; `ApproveBoundary`; `ApproveCommitmentDraft`; `ApprovePolicy`; `ApprovePolicyRetirement` | Reconcile exact source versions and complete downstream impact checks, including future/frozen engagement dependencies; preserve explicit pending contexts until delivered. |
| Partial | `CreateCommitmentDraft`; `ReviseCommitmentDraft`; `ReviewCommitmentDraft`; `ApproveCommitmentDraft` | Complete evidence, risk and engagement consumers; current ReadinessRules already evaluates effective commitments. Existing revision/review/approval creates effective versions, so do not add a duplicate ActivateCommitment. |
| Partial | `RaiseRiskReassessmentTriggers` | Extend the existing internal command to all approved change sources (commitments, assets, providers, controls and native incidents); boundary/method reactions are already present. |
| Partial | `ReviseApplication`; `RetireApplication`; `RetireSystemInstance` | Complete bounded application/system-instance and incoming-relationship impact before governed lifecycle decisions; current application change preview explicitly returns complete:false. |
| Decision | `ReviseSystemInstance` | Decide the exact permitted correction/successor operation for a concrete instance after declaration. Current requests declare and retire instances; ReviseApplication only changes the logical application. |
| Decision | `RetireCommitment` | Confirm whether retirement/end-of-effect uses a dedicated command or the existing approved-version workflow; do not infer permission to delete historically used commitments. |

<a id="g08"></a>

### G08 — Attention and weekly delivery

Scope: **R2-11, F1-05**. Requirement source: [work-queue-source-coverage](work-queue-source-coverage.md).

SetWorkDigestPreference persists preference; GetWorkDigest/ListWorkReminders are queries. Weekly email delivery/scheduling remains #647; tenant-owned campaign/waiver source placement remains #646.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Decision | `ScheduleWeeklyWorkDigest`; `DeliverWeeklyWorkDigest`; `RecordWorkDigestDelivery` | Set delivery cadence, recipient/consent policy, deduplication identity and failure/retry behavior before adding the delivery commands; queries and preferences already exist. |
| Partial | `AssignWorkItem`; `ClaimWorkItem`; `DelegateWorkItem`; `EscalateWorkItem` | Extend only after the campaign/expectation/waiver owning-queue decision. Preserve source authority and eligibility; cross-client presentation does not introduce another task completion command. |

<a id="g09"></a>

### G09 — Type I engagement and baseline

Scope: **T1-01**. Requirement source: [backlog](backlog.md).

ServiceEngagement is the firm-services engagement, not the client examination. FreezeProgramScopeSnapshot only freezes its bounded scope inputs and cannot serve as the full Type I baseline.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `CreateExaminationEngagement`; `ReviseExaminationEngagement` | Record the client examination, auditor, dates, selected categories and baseline scope separately from professional service acceptance. |
| Missing | `FreezeTypeIBaseline`; `AmendTypeIBaseline` | Freeze all required exact scope/control/policy/workforce/inventory/commitment/risk/provider/evaluation/evidence/decision inputs; preserve amendment ancestry and content identities. |

<a id="g10"></a>

### G10 — Management-authored system description

Scope: **T1-02, T2-07, T3-01/05/06**. Requirement source: [backlog](backlog.md).

No SystemDescription command contracts/handlers/aggregate exist. Boundaries and control narratives are inputs, not the management-authored description.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `CreateSystemDescriptionDraft`; `ReviseSystemDescriptionDraft`; `BindSystemDescriptionSources` | Author structured sections and reconcile exact source versions; distinguish complete/incomplete/not-applicable sections. |
| Missing | `ReviewSystemDescription`; `ApproveSystemDescription`; `ProposeSystemDescriptionSuccessor` | Retain exact-version review/approval, effective history and Type I/II scope. |
| Missing | `RecordSystemDescriptionNoChangeDecision`; `BindSystemDescriptionToExamination` | Resolve significant-change review flags by a retained no-change decision or approved successor and bind the exact approved description to baseline/period/close. |
| Missing | `GenerateSystemDescriptionExport` | Create an attributable immutable report-ready artifact and section manifest from an exact approved version; plain retrieval of existing output remains a query. |

<a id="g11"></a>

### G11 — Auditor requests, responses and samples

Scope: **T1-04, T3-03**. Requirement source: [backlog](backlog.md).

OpenEvidenceRequest handles internal evidence work; it does not retain the external request lifecycle, response review, discussion compartment or delivered response versions. External firms need no product account.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `RecordAuditorRequest`; `ReviseAuditorRequest`; `AssignAuditorRequest`; `RecordAuditorRequestComment` | Capture external source wording or assigned Attest input, scope, owner, priority, due date and compartment-aware discussion. |
| Missing | `CreateAuditResponseDraft`; `ReviseAuditResponseDraft`; `SubmitAuditResponse`; `ReviewAuditResponse` | Assemble exact existing records and artifacts, retain versions and internal review before authorized disclosure. |
| Missing | `DeliverAuditResponse`; `RecordAuditorResponseDisposition`; `ResolveAuditorRequest`; `ReopenAuditorRequest` | Record intentional delivery, external receipt/disposition and retained reopen/resolution history; do not infer receipt from a successful send. |
| Missing | `RecordSampleSelection`; `AmendSampleSelection` | Retain actual selector, exact frozen population hash and row IDs. An amended population never moves an earlier selection; any new selection is linked. |

<a id="g12"></a>

### G12 — Indexed audit packages

Scope: **T1-05, T3-05**. Requirement source: [decisions/m0-d17-audit-deliverables](decisions/m0-d17-audit-deliverables.md).

No AuditPackage request/build/validation/delivery contracts exist. Snapshot manifests are a reusable primitive, not an examination package. #339 must validate actual auditor expectations; Privacy support remains #349.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `RequestAuditPackageBuild`; `RecordAuditPackageBuildResult` | Generate deterministic exact-version package/index/control-matrix/evidence-index outputs with durable progress and terminal failure; internal completion records remain distinct from human approval. |
| Missing | `ValidateAuditPackage`; `ApproveAuditPackageValidationOverride` | Retain validation against exact manifest/version and explicit authorized rationale for permitted overrides. Missing or unauthorized artifacts cannot be hidden by a clean result. |
| Missing | `ApproveAuditPackageDisclosure`; `RecordAuditPackageDelivery`; `AmendAuditPackage` | Bind approval/recipient/actual delivery to immutable package bytes and retain amendments. Reuse general artifact disclosure where semantics match rather than create two approval authorities. |

<a id="g13"></a>

### G13 — Assertions, representation letters, outcomes and Type II plan

Scope: **T1-07, T3-06**. Requirement source: [decisions/m0-d17-audit-deliverables](decisions/m0-d17-audit-deliverables.md).

DecideTypeIEntry is a readiness entry decision, not an assertion, representation letter, issued report or outcome. No examination sign-off/outcome/operating-plan commands exist.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `RecordManagementAssertion`; `ApproveManagementAssertion` | Retain exact assertion wording/artifact and personal CEO/CISO sign-off against the validated exact package, description, criteria and known exceptions. |
| Missing | `RecordManagementRepresentationLetter`; `SignManagementRepresentationLetter` | Preserve auditor-supplied source/form, signed artifact, exact package, actual signers and time; retain the required sequence after assertion approval. |
| Missing | `RecordExaminationOutcome`; `RecordManagementExaminationReview` | Capture externally issued report/status/opinion provenance and management review after the required sign-offs; never derive an opinion from readiness. |
| Missing | `ProposeTypeIIOperatingPlan`; `ReviewTypeIIOperatingPlan`; `ApproveTypeIIOperatingPlan` | Bind the proposed observation period to exact control/policy/risk/provider versions, ownership, cadence, changes and open remediation, with explicit approval. |

<a id="g14"></a>

### G14 — Observation periods and continuous operation

Scope: **T2-01, T3-07**. Requirement source: [backlog](backlog.md).

ProgramPlan holds target dates; it does not retain an approved TypeIIPeriod opening snapshot, overlap constraints, amendments or rollover approvals.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `StartTypeIIObservationPeriod`; `AmendTypeIIObservationPeriod` | Start from approved plan/baseline, freeze opening inputs and govern date/scope amendments without rewriting earlier operation. |
| Missing | `ProposeProgramRollover`; `ApproveProgramRollover` | Review changed/carried-forward/retired/unresolved records and retain approval before opening the next period. |
| Reuse | `ReviseFinding` | Reuse the new period-start operation for later periods and existing finding identity/history for carry-forward. Add explicit carry-forward source references if needed, not a duplicate finding or schedule. |

<a id="g15"></a>

### G15 — Native significant changes and incidents

Scope: **T2-07, M0-D21**. Requirement source: [decisions/m0-d21-significant-change](decisions/m0-d21-significant-change.md).

No SignificantChange or ComplianceIncident write contracts/handlers exist. IncidentReference is externally sourced risk context; it is not the native manually recordable compliance incident.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `RecordSignificantChange`; `ReviseSignificantChange`; `AssessSignificantChange` | Capture native identity, dates/interval, optional source locator, handling class, exact affected records and attributable significance/rationale. |
| Missing | `RecordComplianceIncident`; `ReviseComplianceIncident`; `AssessComplianceIncident`; `RecordComplianceIncidentResolution` | Capture customer-data/SLA impact, significance, assessor, chronology and optional external provenance; preserve earlier IncidentReference history. |
| Missing | `RequireSystemDescriptionSectionReview` | Internal reaction marks affected description sections for review on significant change/incident. Existing risk trigger and finding commands handle resulting reassessment/remediation where applicable. |

<a id="g16"></a>

### G16 — Period close and examination populations

Scope: **T3-01/02**. Requirement source: [backlog](backlog.md).

Access populations and workforce snapshots are bounded reusable records; no TypeIIPeriodCloseSnapshot or general AuditPopulation write workflow exists.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `ApproveTypeIIPeriodClose`; `FreezeTypeIIPeriodClose`; `AmendTypeIIPeriodClose` | Bind management approval to completeness/blockers/unresolved acknowledgements and freeze the exact complete examination boundary; amendments preserve original content. |
| Missing | `DefineAuditPopulation`; `ReviseAuditPopulationDefinition` | Version source universe, covered period, inclusion/exclusion, completeness evidence, permitted fields and control relationships. |
| Missing | `RecordAuditPopulationFacts`; `ClassifyAuditPopulationRow`; `ReconcileAuditPopulation` | Allow manual source facts and classify included/excluded/duplicate/missing/rejected/unresolved rows; preserve source identity and explain exact totals. |
| Missing | `GenerateAuditPopulation`; `FreezeAuditPopulation`; `AmendAuditPopulation`; `GenerateAuditPopulationExport` | Generate from exact close/source snapshots, retain completeness and immutable hash/row IDs, freeze before sampling and create linked amendments/exports. No connector dependency. |

<a id="g17"></a>

### G17 — Professional authority, ratification and actual assignments

Scope: **F1-03/07/08**. Requirement source: [service-engagement-backend](service-engagement-backend.md).

ReviseIndependenceRules authors unratified rules. AcceptEngagement and RemoveActualStaff exist as internal ledger methods with unit fixtures. Public professional acceptance, verified partner authority and production ratification are unfinished; provisional draft staffing grants no professional access.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Decision | `DesignateEngagementPartnerDuty`; `RevokeEngagementPartnerDuty`; `RatifyIndependenceRuleVersion`; `RecordPartnerIndependenceEvaluation` | Finalize and implement real named-partner authority and ratification evidence under #269/#343; directory practice/operator status is insufficient. |
| Partial | `AcceptServiceEngagement` | Compose the existing internal acceptance boundary with verified current partner/directory/rule/acknowledgement/boundary sources and a personal decision. No public IRequest/handler/route exists. |
| Missing | `AssignServiceEngagementStaff` | Authorize actual additions/changes after acceptance with current eligibility and lifetime wall checks; ProposeServiceEngagementStaff only records a tentative draft proposal. |
| Partial | `RevokeServiceEngagementStaff` | Expose authorized actual-assignment revocation using RemoveActualStaff; withdrawing a tentative proposal is a different intent. |
| Partial | `AmendServiceEngagement`; `CloseServiceEngagement` | Complete accepted-engagement amendment and attest package/delivery/hold prerequisites. Existing close can revoke accepted assignments but does not establish required attest delivery proof. |

<a id="g18"></a>

### G18 — Own-attest delivery and organization offboarding

Scope: **F1-01/07, M0-D27**. Requirement source: [decisions/m0-d27-attest-scope](decisions/m0-d27-attest-scope.md).

OffboardTenant is a tenant lifecycle write. No ClientExport or own-attest collaboration-package/delivery workflow exists; record preservation/disposition cannot be inferred from tenant status.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `RequestClientExport`; `RecordClientExportBuildResult`; `RecordClientExportDelivery` | Export management-owned records/evidence/history/packages with withheld-item reasons and attributed actual delivery. |
| Missing | `RequestOwnAttestRecordPackage`; `RecordOwnAttestRecordPackageResult`; `RecordOwnAttestRecordDelivery` | Package every request/response/population/sample/delivered-evidence version with M0-D17 manifest and record delivery into dedicated attest software; no native workpapers. |
| Missing | `CompleteTenantOffboarding` | Retain a terminal attributed completion after export, current access revocation, approved hold/retention evaluation and required own-attest delivery; OffboardTenant currently only records TenantOffboardingStarted. |
| Partial | `OffboardTenant`; `CloseServiceEngagement` | Bind completion to final export, access revocation, retention/hold plan and required own-attest delivery; interrupted packaging must preserve holds. |
| Decision | `ApproveOrganizationDisposition`; `ExecuteOrganizationDisposition`; `RecordOrganizationDispositionResult` | Settle #346 retention/ownership facts and approved store/index/projection/backup disposition plan; execute only after required retained duties and holds are satisfied. |

<a id="g19"></a>

### G19 — Client-specific federation

Scope: **F1-06, EN-01**. Requirement source: [tenant federation ADR](../architecture/decisions/0009-tenant-identity-federation-and-context.md).

Deployment OIDC/session/link/recovery operations exist. There is no TrustedIssuer/tenant identity-provider lifecycle write contract. Configured resource-server issuers alone are not a tenant-admin connection workflow.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `ConfigureTenantIdentityProvider`; `TestTenantIdentityProvider`; `DisableTenantIdentityProvider`; `RotateTenantIdentityProviderCredentials` | Persist per-tenant issuer trust and safe lifecycle, test configuration without granting membership, immediately block new sign-ins when disabled, preserve subject/history and secret custody. Test command is a retained explicit configuration test, not a normal read. |
| Decision | `ConfigureTenantIdentityGroupMapping`; `RevokeTenantIdentityGroupMapping` | Use only if the approved IdP group-mapping slice is selected; preserve explicit organization grants and never grant membership from email/domain alone. |

<a id="g20"></a>

### G20 — Methodology templates

Scope: **F1-04**. Requirement source: [backlog](backlog.md).

No platform template lifecycle/application/contribution command contracts or handlers exist. Control content may carry provenance but does not provide a governed template library.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Missing | `CreateMethodologyTemplate`; `ReviseMethodologyTemplateDraft`; `ReviewMethodologyTemplate`; `ApproveMethodologyTemplateVersion`; `RetireMethodologyTemplate` | Maintain platform template versions, authorship, approval, change notes and licensing constraints without client content leakage. |
| Missing | `ApplyMethodologyTemplate`; `ProposeTemplateSuccessorAdoption`; `RecordTemplateSuccessorDisposition` | Create tenant-owned drafts with exact template provenance; let each client adopt/adapt/decline a successor without changing existing records automatically. |
| Missing | `ProposeDeidentifiedTemplateContribution`; `ReviewTemplateContribution`; `AcceptTemplateContribution` | Explicitly review de-identification and rights before client-authored material may enter the platform library. |

<a id="g21"></a>

### G21 — Organization management delegation

Scope: **BO-01**. Requirement source: [business-operations](business-operations.md).

Invitations, scoped GrantAccess/RevokeAccessGrant and responsibilities already implement the core administrative mechanisms. There is no agreed composite handover/consent policy or durable handover record.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Partial | `InviteOrganizationMember`; `GrantAccess`; `RevokeAccessGrant`; `AssignResponsibility`; `RevokeResponsibility` | Use explicit organization grants and retain provider duties/history. Do not imply legal ownership transfer, payer change or Advisor/Attest assignment. |
| Decision | `ProposeOrganizationManagementHandover`; `AcceptOrganizationManagementHandover`; `CompleteOrganizationManagementHandover`; `CancelOrganizationManagementHandover` | Add only the composite handover decisions required by the approved consent/authority policy; existing grant operations may satisfy a simpler policy without four new commands. |

<a id="g22"></a>

### G22 — Hosted billing

Scope: **BO-02**. Requirement source: [business-operations](business-operations.md).

No organization subscription/billing command contracts or handlers exist. Self-hosted manual operation remains free; payer/pricing/payment/financial lifecycle authority is unresolved.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Decision | `ConfigureOrganizationBilling`; `ChangeOrganizationBilling`; `AssignBillingAdministrator`; `RevokeBillingAdministrator` | Finalize organization-specific payer/administration/terms authority, then implement attributed billing configuration independent of compliance-data access. |
| Decision | `StartOrganizationSubscription`; `ChangeOrganizationSubscription`; `CancelOrganizationSubscription` | Implement only the approved base-offering/lifecycle/cadence/failure policy; multiple organizations remain separate billing units. |
| Decision | `RecordBillingProviderObservation`; `ReconcileOrganizationBilling`; `RecordBillingAdjustment` | Select provider and financial policy; retain external observation identity, reconciliation and authorized corrections. Avoid invented refund, tax or delinquency policy. |

<a id="g23"></a>

### G23 — Organization add-ons

Scope: **BO-03**. Requirement source: [business-operations](business-operations.md).

No add-on offering, selection, commercial acceptance, entitlement or activation commands exist. Catalog/prices/eligibility/activation/termination policy is unresolved.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Decision | `PublishAddonOfferingVersion`; `RetireAddonOffering`; `SelectOrganizationAddon`; `AcceptOrganizationAddonTerms`; `EndOrganizationAddon` | Record approved versioned catalog and organization-specific selection/terms/ending; keep sale distinct from functional activation and professional service acceptance. |
| Decision | `ActivateOrganizationAddon`; `DisableOrganizationAddon`; `ReconcileOrganizationAddonEntitlements` | Implement only approved capability/price/lifecycle policy; preserve governed records and complete manual operation after disabling an enhancement. |

<a id="g24"></a>

### G24 — Optional collection and customer-hosted agent

Scope: **T2-08/08a/08b, M0-D20**. Requirement source: [decisions/m0-d20-integration-sources](decisions/m0-d20-integration-sources.md).

No collection endpoint/run/agent command contracts or handlers exist. Selected sources and customer-hosted agent scope remain P2 hypotheses; manual entry and reviewed imports are distinct.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Hypothesis | `ConfigureIntegrationEndpoint`; `RotateIntegrationCredentials`; `DisableIntegrationEndpoint`; `ConfigureCollectionSchedule` | Require exact source/rights/authority/completeness/security/custody review before building optional collection. |
| Hypothesis | `StartCollectionRun`; `CancelCollectionRun`; `RecordCollectionObservations`; `CompleteCollectionRun`; `FailCollectionRun`; `AcceptCollectionReconciliation` | Retain immutable observed data, progress/completeness and explicit owning-workflow reconciliation; collection never directly overwrites governed facts. |
| Hypothesis | `EnrollCollectionAgent`; `RotateCollectionAgentCredentials`; `RevokeCollectionAgent`; `RecordCollectionAgentHealth`; `AssignAgentCollectionRun` | Validate private-source need, enrollment/update/revocation and least-privilege boundaries before adding a customer-hosted agent surface. |

<a id="g25"></a>

### G25 — Selected Privacy lifecycle coverage

Scope: **M0-D01, R1-08, T1-05, T3-05**. Requirement source: [Privacy scope decision and #349](decisions/m0-d01-program-targets.md).

The current catalog and readiness contract explicitly declare unsupported Privacy lifecycle coverage. The additional validated stories are not defined in this local command inventory. A selected Privacy category cannot be marked supported by catalog mappings alone.

| Status | Command intent / candidate names | Remaining behavior |
| --- | --- | --- |
| Decision | `Privacy lifecycle write intents (names not yet defined)` | Obtain the validated #349 story set and map its actual manual workflows to command owners before naming contracts. Keep support gaps visible and block complete Privacy package claims; do not fabricate collection/consent/rights/retention policies or mark this scope covered. |

## Complete product-story reconciliation

This maps write outcomes only. “Exists” does not declare a milestone accepted; permissions, source correctness, unit evidence and client delivery still belong to the owning story. Later stories intentionally reuse earlier commands. Read-only portfolio/queue capabilities are not missing command families.

| Story | Existing owning commands / nearest behavior | Command coverage | Gap / reuse conclusion |
| --- | --- | --- | --- |
| [**R1-01**](backlog.md#L1522) Start a SOC 2 program and see the path to Type II | CreateProgram; ReviseProgram | Exists | Examination/period stage transitions remain G09/G13/G14. |
| [**R1-02**](backlog.md#L1567) Define the system boundary and intended audit scope | CreateBoundary; ReviseBoundaryDraft; ReviewBoundary; ApproveBoundary; ProposeBoundarySuccessor; CreateClientService | Partial | G07/G09: full downstream impact and examination binding. |
| [**R1-03**](backlog.md#L1627) Select a traceable SOC 2 criteria catalog | SelectProgramCriteriaEdition; SetCriteriaTextOverlay | Exists | Catalog licensing/support gaps are explicit; no editable criterion CRUD requirement. |
| [**R1-04**](backlog.md#L1687) Invite the compliance team and assign responsibilities | Invitations; RegisterMember; GrantAccess; RevokeAccessGrant; team/role commands; AssignResponsibility; SoD waiver commands; SuspendMember; DeprovisionMember | Exists | G03/G17/G21 are consuming workflow/authority gaps, not replacement RBAC. |
| [**R1-05**](backlog.md#L1832) Build the control inventory and implementation narratives | CreateControlDraft; ReviseControlDraft; ReviewControl; ApproveControl; ProposeControlSuccessor; ProposeControlRetirement; RetireControl; DiscardControlDraft | Partial | G07: incomplete cross-context applicability/impact, future engagement consumers. |
| [**R1-06**](backlog.md#L1894) Map controls to criteria and explain applicability | ProposeControlCriterionMapping; ReviewControlCriterionMapping; RetireControlCriterionMapping; ProposeCriterionNotApplicable; ReviewCriterionApplicability; WithdrawCriterionNotApplicable | Exists | Accepted mapping review already records its governed outcome; no duplicate generic approval command. |
| [**R1-07**](backlog.md#L1938) Assess scoped risks and choose treatment | Risk draft/method/assessment/treatment/action/acceptance commands | Partial | G07: complete approved reassessment-source consumers. Existing expired decisions can be evaluated at read time; no forced ExpireRisk command. |
| [**R1-08**](backlog.md#L2012) Complete the readiness assessment and own the gap plan | RunReadinessAssessment; PlanReadinessGap; AnnotateReadinessGap; DecideReadiness | Partial | G04: remaining source/rule/plan semantics. |
| [**R1-09**](backlog.md#L2067) Bring existing readiness work into the program | Application import is reusable but only one record family | Missing | G06: readiness-material family adapters, acceptance and replay. |
| [**R1-10**](backlog.md#L2196) Establish the application inventory and review scope | DeclareApplication; ReviseApplication; DeclareSystemInstance; RetireApplication; RetireSystemInstance; relationship/scope/import commands | Partial | G06/G07: omission consumer, bounded change impact and instance correction design. |
| [**R1-11**](backlog.md#L2334) Establish the authoritative workforce and identity-owner roster | Person/work-relationship/service-identity/source-observation/reconciliation/roster-snapshot commands | Exists | Source adapters await facts; canonical manual entry is present. No HRIS subsystem required. |
| [**R1-12**](backlog.md#L2470) Inventory scoped technology and information assets | Record/ReviseTechnologyComponent, InformationAsset, DataFlow, Location, OperationalProcess | Exists | Revision carries governed lifecycle where supported; no automatic requirement for one endpoint per canonical facet. |
| [**R1-13**](backlog.md#L2542) Record service commitments, system requirements, and user responsibilities | CreateCommitmentDraft; ReviseCommitmentDraft; ReviewCommitmentDraft; ApproveCommitmentDraft | Partial | G07: existing approved versions need remaining consumers/retirement semantics. |
| [**R1-14**](backlog.md#L2607) Evaluate vendors and subservice organizations | Record/ReviseProvider; Record/ReviseAssuranceReport; RecordProviderReview; provider coverage-gap commands | Partial | G01/G07: governed report bytes, source coverage and downstream links. |
| [**R1-15**](backlog.md#L2677) Provision a client organization and its first administrators | RegisterTenant; InviteOrganizationMember; lifecycle/slug/operator internal commands | Exists | Core self-service creation is present; commercial/complete offboarding scope is separate. |
| [**EN-01**](backlog.md#L2757) Provide shared authorization and actor-reference foundations | Identity, membership, role, grant, operator and scoped mutation commands | Exists | G17/G19: remaining professional authority and optional per-client federation consumers. |
| [**EN-02**](backlog.md#L2807) Provide versioned records, effective history, and change-impact preview | Owning draft/review/approval/successor/retirement commands | Partial | G07 and future domains; no universal version-change command. |
| [**EN-03**](backlog.md#L2852) Freeze immutable snapshots with content identity and amendments | Freeze/AmendProgramScopeSnapshot; Freeze/AmendWorkforceRosterSnapshot | Partial | G09/G16: full examination baseline and period-close contents are missing. |
| [**EN-04**](backlog.md#L2894) Record attributable review and approval decisions with separation of duties | Owning reviews/approvals; Record/ApproveSeparationOfDutiesWaiver | Exists | Shared immutable decision primitive does not imply a universal Review command. |
| [**EN-05**](backlog.md#L2940) Import with preview, reconciliation, and safe replay | Stage/Correlate/Accept/CancelApplicationImport; internal execution/effects | Partial | G06: additional record families and accepted consumer coverage. |
| [**EN-06**](backlog.md#L2993) Store governed artifacts with immutable content identity | Internal EvidenceIntake/artifact mutations; Prepare/ApproveEvidenceRedaction | Partial | G01/G02: capture/download/scan composition, storage and disclosure/disposition integration. |
| [**R2-01**](backlog.md#L3050) Assign control ownership and operating cadence | DesignateControlOwnerPerson; Propose/ApproveControlOperatingPlan; responsibilities | Partial | G05: complete period-aware recurrence and future schedule reconciliation. |
| [**R2-02**](backlog.md#L3100) Maintain approved policies and review cycles | Policy draft/review/approval/successor/retirement/discard; ConfirmPolicyReview | Partial | G01/G07: governed policy bytes and complete downstream impact. |
| [**R2-03**](backlog.md#L3161) Request and capture trustworthy evidence | Open/Fulfil/CancelEvidenceRequest; internal capture | Partial | G01: public content capture/download and contextual support. |
| [**R2-04**](backlog.md#L3206) Perform a control and attest to the result | OpenControlOccurrence; AttestControlOccurrence; CorrectControlAttestation; ReviewControlOccurrence | Partial | G01/G05: resolved governed evidence and exact frozen governance-review support. |
| [**R2-05**](backlog.md#L3254) Review control design, implementation, and evidence | Define/Start/RecordStep/Submit/ReviewControlEvaluation; deviation disposition/finding | Partial | G01/G05: governed evidence integration and defaults ratification. |
| [**R2-06**](backlog.md#L3382) Reconcile application access and complete the initial review | Population, principal classification, expectation, campaign, decision, remediation, verification and exemption commands | Exists | T3 populations/samples are different workflows. Optional imports are G06. |
| [**R2-07**](backlog.md#L3588) Resolve findings, exceptions, and corrective actions | Raise/Revise/Reopen/CloseFinding; Add/CompleteCorrectiveAction; LinkFindingAcceptance | Partial | G01/G05: governed resolution evidence and automatic-default ratification; use domain-specific waivers. |
| [**R2-08**](backlog.md#L3642) Collaborate with a readiness advisor on work in progress | Scoped membership/grants and readiness annotation are prerequisites | Missing | G03: handoff/feedback/response/close/revoke lifecycle. |
| [**R2-09**](backlog.md#L3706) Make and record the Type I entry decision | DecideTypeIEntry | Partial | G04: complete required input/source acceptance. This is not Type I sign-off. |
| [**R2-10**](backlog.md#L3760) Publish policies and verify acknowledgement and training | LaunchPolicyCampaign; AcknowledgePolicy; training requirement/completion/campaign/waiver commands | Exists | G01/G08: governed support and source queue placement remain consuming integration work. |
| [**R2-11**](backlog.md#L3835) Manage accountable compliance work | Assign/Claim/Delegate/EscalateWorkItem; SetWorkDigestPreference | Partial | G08: email policy/delivery and source placement. Source commands own completion. |
| [**R2-12**](backlog.md#L3950) Govern evidence access, retention, and disclosure | Retention basis/legal holds; redaction lineage and approval | Partial | G02: full handling/storage hold/disclosure/engagement hold/disposition. |
| [**T1-01**](backlog.md#L4030) Establish the Type I engagement and point-in-time snapshot | Program/workforce snapshots are only prerequisites | Missing | G09: examination engagement and complete point-in-time baseline. |
| [**T1-02**](backlog.md#L4080) Author, approve, and maintain the system description | Boundary/control/policy versions are only inputs | Missing | G10: management-authored system description lifecycle/export. |
| [**T1-03**](backlog.md#L4139) Give the auditor least-privilege engagement access | Existing scoped membership is reusable | Hypothesis | External auditor accounts are outside accepted baseline under M0-D17; no required new commands. |
| [**T1-04**](backlog.md#L4193) Respond to auditor requests and samples | Evidence requests are narrower prerequisites | Missing | G11: auditor request/response/discussion/review/delivery/reopen/sample selection. |
| [**T1-05**](backlog.md#L4246) Produce and validate the Type I package | Snapshot manifests are narrower primitives | Missing | G12: package build/validation/disclosure/delivery/amendment; #339/#349 gates. |
| [**T1-06**](backlog.md#L4315) Track Type I observations and audit findings | RaiseFinding; ReviseFinding; AddCorrectiveAction; CompleteCorrectiveAction; CloseFinding; ReopenFinding | Partial | Reuse existing findings. Add exact examination/baseline/delivery/rollover references, not audit-only finding commands. |
| [**T1-07**](backlog.md#L4359) Record the Type I outcome and approve the Type II plan | DecideTypeIEntry is a different decision | Missing | G13: assertions/letters/outcome/approved Type II operating plan. |
| [**T2-01**](backlog.md#L4421) Start the Type II observation period from the approved baseline | ProgramPlan contains target dates only | Missing | G14: approved operating period and opening snapshot/amendment. |
| [**T2-02**](backlog.md#L4471) Operate a recurring control calendar | Operating plan and OpenControlOccurrence exist | Partial | G05: recurrence materialization/provenance, schedule changes and frozen review support. |
| [**T2-03**](backlog.md#L4532) Collect and review evidence for every required occurrence | Evidence requests and occurrence attest/review already exist | Partial | G01/G05: complete governed per-occurrence evidence and scheduling. |
| [**T2-04**](backlog.md#L4577) Monitor readiness and intervene before gaps age | Readiness assessment and source-owned accountability writes | Partial | G04: source/monitoring semantics; forecast/drill-down are queries, not new commands. |
| [**T2-05**](backlog.md#L4623) Run recurring application access reviews | LaunchAccessReviewCampaign; decisions/remediation/verification/completion | Exists | Reuse recurring campaigns; no new TypeIIAccessDecision command. |
| [**T2-06**](backlog.md#L4682) Complete periodic policy, risk, and vendor reviews | Operating-plan/occurrence/review and finding commands | Reuse | M0-D19 supersedes a dedicated periodic-review subsystem; remaining recurrence is G05. |
| [**T2-07**](backlog.md#L4737) Assess system changes and incidents for compliance impact | Risk IncidentReference is not a native incident | Missing | G15/G10: native change/incident significance and description-review consequences. |
| [**T2-08**](backlog.md#L4802) Automate selected inventory, access, and evidence collection | Manual source observations/imports are different capabilities | Hypothesis | G24: validate sources/cloud/private agent before connector commands. |
| [**T2-09**](backlog.md#L4942) Conduct periodic management compliance reviews | Occurrence attestation/review; RaiseOccurrenceFinding; corrective actions | Reuse | M0-D19: no separate ManagementReviewSnapshot/approval lifecycle. Remaining frozen support is G05. |
| [**T3-01**](backlog.md#L5007) Close the observation period and freeze the examination snapshot | Program/workforce snapshots are narrower primitives | Missing | G16: authorized full period close, freeze and amendments. |
| [**T3-02**](backlog.md#L5057) Demonstrate complete audit populations | Workforce/access populations are narrower domains | Missing | G16: general audit-population definition/facts/classification/reconciliation/freeze/export/amendment. |
| [**T3-03**](backlog.md#L5128) Fulfill Type II sample and evidence requests | Evidence request writes are narrower | Missing | G11: reuse auditor request/response and exact frozen sample-selection workflows. |
| [**T3-04**](backlog.md#L5181) Resolve examination exceptions and remediation commitments | Existing finding/corrective-action/closure/reopen writes | Partial | Reuse findings with exact examination/source/commitment references; G01/G13/G14 supply dependencies. |
| [**T3-05**](backlog.md#L5227) Produce and validate the Type II package | No package workflow | Missing | G12 reused with exact Type II close and full period material. |
| [**T3-06**](backlog.md#L5289) Record management sign-off and the Type II outcome | No examination sign-off/outcome workflow | Missing | G13 reused with exact final description/population/exception/close/package identities. |
| [**T3-07**](backlog.md#L5346) Roll the program into continuous compliance and the next period | Current record revisions and finding history are reusable | Missing | G14: explicit rollover approval and successor period; avoid cloning open findings. |
| [**F1-01**](backlog.md#L5410) Onboard, suspend, and offboard client organizations | Register/Suspend/Reactivate/OffboardTenant | Partial | G18/G20: client export, attest delivery, templates and approved complete disposition. |
| [**F1-02**](backlog.md#L5470) See the client portfolio across organizations | No new mutation inherently required | Reuse | Authorized portfolio is query/projection work; no EditPortfolioStatus command. |
| [**F1-03**](backlog.md#L5511) Plan firm staff assignments and capacity across clients | Tentative staff proposal/withdrawal; internal acceptance/removal | Partial | G17: actual approved assignments and authority. Requirements exclude standalone capacity/timekeeping system. |
| [**F1-04**](backlog.md#L5551) Maintain a reusable template library and apply it to clients | No template workflow | Missing | G20: version/approval/apply/successor/contribution lifecycle. |
| [**F1-05**](backlog.md#L5594) Work one queue across all my client organizations | Existing source work assignments and digest preference | Reuse | Cross-client queue is authorized read composition. G08 only for delivery policy, not duplicate task writes. |
| [**F1-06**](backlog.md#L5635) Let client users sign in through their own identity providers | ContinueWithOidcProvider; LinkOidcProviderIdentity; recovery | Missing | G19: tenant connection trust/test/disable/credential lifecycle. |
| [**F1-07**](backlog.md#L5681) Record client engagements and accept them after independence checks | Create/Amend/CloseServiceEngagement; tentative staff proposals; AcknowledgeEngagementManagement; internal acceptance | Partial | G17/G18: real acceptance authority/public consumer and attest delivery gates. |
| [**F1-08**](backlog.md#L5738) Enforce independence walls between advisory and attest work | Draft rules; service facts; retained evaluation; internal actual-history reevaluation | Partial | G17 and owning compartment consumers; policy previews do not ratify rules or accept service. |
| [**BO-01**](backlog.md#L5812) Delegate organization management to client personnel | Invitations/grants/responsibilities | Decision | G21: approve remaining handover/consent/duties policy before adding composite commands. |
| [**BO-02**](backlog.md#L5873) Set up and administer an organization's hosted subscription and billing | No billing/subscription writes | Decision | G22: unresolved commercial authority/provider/lifecycle policy. |
| [**BO-03**](backlog.md#L5935) Select and manage organization add-ons | No offering/selection/entitlement writes | Decision | G23: unresolved catalog/price/eligibility/activation/termination policy. |

## Deliberately excluded new commands

- No universal Review/Approve/TaskComplete command: workflows own their decisions and completion.
- No separate policy/risk/provider/management periodic-review aggregate or command subsystem: M0-D19 uses recurring control occurrences, evidence, review decisions and findings.
- No required GrantExternalAuditorAccess/InviteExternalAuditor command: M0-D17 sends external firms packages; direct accounts remain T1-03 hypothesis. Our firm's Attest access belongs to accepted professional engagements.
- No audit-only finding/TypeIIAccessDecision/copy-forward-finding commands: reuse remediation and access-review owners with exact examination references.
- No EditPortfolioStatus, SetReadinessPercentage or CompleteWorkItem: these would create unexplained competing source state.
- No mandatory staff-capacity/timekeeping subsystem: F1-03 requires explainable assignments/workload and explicitly excludes resource management/timekeeping.
- No automatic evidence-disposal command triggered solely by time: eligibility is a read; actual disposition requires approved policy, no blocking hold/reliance, and explicit approval.
- No universal entity CRUD generated from the canonical catalog: each command requires an actual owning workflow and invariant.

## Suggested delivery order

1. Complete manual governed evidence and contextual support (G01), then handling/hold/disclosure/disposition increments (G02) as required by consumers.
2. Complete advisor handoffs and remaining readiness/control/work consumers (G03–G05/G07/G08). Resolve finding defaults and queue/email policies in their owning slices.
3. Deliver examination engagement/baseline and system description, then request/sample/package/sign-off/outcome workflows (G09–G13), retaining reuse of existing findings.
4. Deliver period opening, recurrence integration, native changes/incidents, close/populations and rollover (G05/G14–G16).
5. Complete real professional authority/acceptance and required own-attest export/delivery/offboarding (G17/G18) before selling or relying on those services.
6. Schedule additional imports, per-client federation and templates when their owning consumer is ready (G06/G19/G20). Resolve BO policy before commerce (G21–G23). Validate collection demand before G24.

This is dependency guidance, not a change to the GitHub delivery queue. No runtime implementation, issue state, PR or deployment changed in this analysis.

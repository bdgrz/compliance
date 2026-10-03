using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bdgrz.Compliance;

/// <summary>
/// Composes the shared Compliance application for every deployment role.
/// </summary>
public static class ComplianceServiceCollectionExtensions
{
    /// <summary>
    /// Registers Compliance components and their Fitz-backed Portia infrastructure.
    /// </summary>
    public static PortiaBuilder AddCompliance(
        this IServiceCollection services,
        IConfiguration configuration,
        bool developerAuthentication = false,
        bool requireRealEmailDelivery = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new DeveloperUserRegistration(developerAuthentication));
        services.AddSingleton(PlatformOperatorAuthority.FromConfiguration(configuration, developerAuthentication));
        services.AddScoped<IPlatformOperatorAccess, EventSourcedPlatformOperatorAccess>();
        services.AddSingleton(ControlDraftDiscardReleaseGate.FromConfiguration(configuration));
        services.AddSingleton(ControlActivationReleaseGate.FromConfiguration(configuration));
        services.AddSingleton(ControlLifecycleReleaseGate.FromConfiguration(configuration));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICriteriaCatalog>(CriteriaCatalog.Platform);
        services.AddScoped<CriteriaTextOverlayReader>();
        services.AddSingleton<IReactorPrincipalProvider, ComplianceReactorPrincipalProvider>();
        services.AddSingleton(ArtifactContentStoreOptions.FromConfiguration(configuration));
        services.AddSingleton<IArtifactContentStore, LocalArtifactContentStore>();
        services.AddSingleton<IArtifactInspector, UninspectedArtifactInspector>();
        var emailDeliverySettings = EmailChallengeDeliverySettings.FromConfiguration(configuration,
            requireRealEmailDelivery);
        services.AddSingleton(emailDeliverySettings);
        services.AddSingleton(EmailChallengeTokenKeys.FromConfiguration(configuration,
            emailDeliverySettings.Mode == "mock"));
        services.AddSingleton<MockEmailChallengeDelivery>();
        services.AddSingleton<IEmailChallengeDelivery>(provider =>
            emailDeliverySettings.Mode == "smtp"
                ? new SmtpEmailChallengeDelivery(emailDeliverySettings)
                : provider.GetRequiredService<MockEmailChallengeDelivery>());
        services.AddSingleton<MockTenantInvitationDelivery>();
        services.AddSingleton<ITenantInvitationDelivery>(provider =>
            emailDeliverySettings.Mode == "smtp"
                ? new SmtpTenantInvitationDelivery(emailDeliverySettings)
                : provider.GetRequiredService<MockTenantInvitationDelivery>());
        services.AddScoped<FitzEmailAddressDirectory>();
        services.AddScoped<IEmailAddressDirectoryProjection>(
            provider => provider.GetRequiredService<FitzEmailAddressDirectory>());
        services.AddScoped<IEmailAddressDirectoryReader>(
            provider => provider.GetRequiredService<FitzEmailAddressDirectory>());
        services.AddScoped<FitzUserIdentityDirectory>();
        services.AddScoped<IUserIdentityDirectoryProjection>(
            provider => provider.GetRequiredService<FitzUserIdentityDirectory>());
        services.AddScoped<IUserIdentityDirectoryReader>(
            provider => provider.GetRequiredService<FitzUserIdentityDirectory>());
        services.AddScoped<IUserIdentityDirectoryReadConsistency, IdentityDirectoryReadConsistency>();
        services.AddScoped<FitzPlatformUserDirectory>();
        services.AddScoped<IPlatformUserDirectoryProjection>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<IPlatformUserDirectoryReader>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<IUserDisplayNameReader>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<UserIdentityContinuation>();
        services.AddScoped<TenantInvitationIssuer>();
        services.AddScoped<FitzPermissionAuthorizer>();
        services.AddScoped<IPermissionProjection>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IPermissionAuthorizer>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IMemberAccessReader>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IMemberAccessEligibility, EventSourcedMemberAccessEligibility>();
        services.AddScoped<FitzTeamDirectoryReader>();
        services.AddScoped<ITeamDirectoryProjection>(provider => provider.GetRequiredService<FitzTeamDirectoryReader>());
        services.AddScoped<ITeamDirectoryReader>(provider => provider.GetRequiredService<FitzTeamDirectoryReader>());
        services.AddScoped<FitzTeamMemberDirectoryReader>();
        services.AddScoped<ITeamMemberDirectoryProjection>(
            provider => provider.GetRequiredService<FitzTeamMemberDirectoryReader>());
        services.AddScoped<ITeamMemberDirectoryReader>(
            provider => provider.GetRequiredService<FitzTeamMemberDirectoryReader>());
        services.AddScoped<FitzTenantDirectoryReader>();
        services.AddScoped<ITenantDirectoryProjection>(provider => provider.GetRequiredService<FitzTenantDirectoryReader>());
        services.AddScoped<ITenantDirectoryReader>(provider => provider.GetRequiredService<FitzTenantDirectoryReader>());
        services.AddScoped<ITenantActivity, EventSourcedTenantActivity>();
        services.AddScoped<FitzTenantMembershipDirectoryReader>();
        services.AddScoped<ITenantMembershipDirectoryProjection>(
            provider => provider.GetRequiredService<FitzTenantMembershipDirectoryReader>());
        services.AddScoped<ITenantMembershipDirectoryReader>(
            provider => provider.GetRequiredService<FitzTenantMembershipDirectoryReader>());
        services.AddScoped<FitzTenantInvitationDirectory>();
        services.AddScoped<ITenantInvitationDirectoryProjection>(
            provider => provider.GetRequiredService<FitzTenantInvitationDirectory>());
        services.AddScoped<ITenantInvitationDirectoryReader>(
            provider => provider.GetRequiredService<FitzTenantInvitationDirectory>());
        services.AddScoped<FitzProgramDirectory>();
        services.AddScoped<FitzApplicationDirectory>();
        services.AddScoped<IApplicationDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzApplicationDirectory>());
        services.AddScoped<IApplicationDirectoryReader>(provider =>
            provider.GetRequiredService<FitzApplicationDirectory>());
        services.AddScoped<FitzApplicationImportDirectory>();
        services.AddScoped<IApplicationImportDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzApplicationImportDirectory>());
        services.AddScoped<IApplicationImportDirectoryReader>(provider =>
            provider.GetRequiredService<FitzApplicationImportDirectory>());
        services.AddScoped<ApplicationImportReadConsistency>();
        services.AddScoped<FitzAccessReviewScopeDirectory>();
        services.AddScoped<IAccessReviewScopeDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzAccessReviewScopeDirectory>());
        services.AddScoped<IAccessReviewScopeDirectoryReader>(provider =>
            provider.GetRequiredService<FitzAccessReviewScopeDirectory>());
        services.AddScoped<IApplicationInventoryActivity,
            EventSourcedApplicationInventoryActivity>();
        services.AddScoped<IControlApplicabilityReferenceValidator,
            GovernedControlApplicabilityReferenceValidator>();
        services.AddScoped<ApplicationHistoryReadConsistency>();
        services.AddScoped<SystemInstanceReadConsistency>();
        services.AddScoped<LegacySystemInstanceSource>();
        services.AddScoped<FitzApplicationBoundaryReferenceDirectory>();
        services.AddScoped<IApplicationBoundaryReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationBoundaryReferenceDirectory>());
        services.AddScoped<IApplicationBoundaryReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationBoundaryReferenceDirectory>());
        services.AddScoped<ApplicationBoundaryReferenceReadConsistency>();
        services.AddScoped<FitzApplicationControlDraftReferenceDirectory>();
        services.AddScoped<IApplicationControlDraftReferenceProjection>(provider =>
            provider.GetRequiredService<FitzApplicationControlDraftReferenceDirectory>());
        services.AddScoped<IApplicationControlDraftReferenceDirectory>(provider =>
            provider.GetRequiredService<FitzApplicationControlDraftReferenceDirectory>());
        services.AddScoped<ApplicationControlDraftReferenceReadConsistency>();
        services.AddScoped<IProgramDirectoryProjection>(
            provider => provider.GetRequiredService<FitzProgramDirectory>());
        services.AddScoped<IProgramDirectoryReader>(
            provider => provider.GetRequiredService<FitzProgramDirectory>());
        services.AddScoped<ProgramHistoryReadConsistency>();
        services.AddScoped<ProgramSetupWorkReadConsistency>();
        services.AddScoped<FitzControlDraftDirectoryV2>();
        services.AddScoped<IControlDraftDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzControlDraftDirectoryV2>());
        services.AddScoped<IControlDraftDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlDraftDirectoryV2>());
        services.AddScoped<ControlDraftReadConsistency>();
        services.AddScoped<ControlDraftListReadConsistency>();
        services.AddScoped<FitzControlDraftHistoryDirectoryV1>();
        services.AddScoped<IControlDraftHistoryDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzControlDraftHistoryDirectoryV1>());
        services.AddScoped<IControlDraftHistoryDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlDraftHistoryDirectoryV1>());
        services.AddScoped<ControlDraftHistoryReadConsistency>();
        services.AddScoped<FitzCommitmentDraftDirectory>();
        services.AddScoped<ICommitmentDraftDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftDirectory>());
        services.AddScoped<ICommitmentDraftDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftDirectory>());
        services.AddScoped<CommitmentDraftReadConsistency>();
        services.AddScoped<CommitmentDraftListReadConsistency>();
        services.AddScoped<FitzCommitmentDraftHistoryDirectoryV1>();
        services.AddScoped<ICommitmentDraftHistoryDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftHistoryDirectoryV1>());
        services.AddScoped<ICommitmentDraftHistoryDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCommitmentDraftHistoryDirectoryV1>());
        services.AddScoped<CommitmentDraftHistoryReadConsistency>();
        services.AddScoped<CommitmentVersionReadConsistency>();
        services.AddScoped<CommitmentImpactService>();
        services.AddScoped<ICommitmentReferenceReader, EventSourcedCommitmentReferenceReader>();
        services.AddScoped<FitzPolicyDirectory>();
        services.AddScoped<IPolicyDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzPolicyDirectory>());
        services.AddScoped<IPolicyDirectoryReader>(provider =>
            provider.GetRequiredService<FitzPolicyDirectory>());
        services.AddScoped<PolicyImpactService>();
        services.AddScoped<FitzCampaignDirectory>();
        services.AddScoped<ICampaignDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCampaignDirectory>());
        services.AddScoped<ICampaignDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCampaignDirectory>());
        services.AddScoped<FitzPersonDirectory>();
        services.AddScoped<IPersonMemberDisplayReader>(
            provider => provider.GetRequiredService<FitzPersonDirectory>());
        services.AddScoped<IPersonDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzPersonDirectory>());
        services.AddScoped<IPersonDirectoryReader>(provider =>
            provider.GetRequiredService<FitzPersonDirectory>());
        services.AddScoped<FitzWorkRelationshipDirectory>();
        services.AddScoped<IWorkRelationshipDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzWorkRelationshipDirectory>());
        services.AddScoped<IWorkRelationshipDirectoryReader>(provider =>
            provider.GetRequiredService<FitzWorkRelationshipDirectory>());
        services.AddScoped<FitzTechnologyInventoryDirectory>();
        services.AddScoped<ITechnologyInventoryProjection>(provider =>
            provider.GetRequiredService<FitzTechnologyInventoryDirectory>());
        services.AddScoped<ITechnologyInventoryReader>(provider =>
            provider.GetRequiredService<FitzTechnologyInventoryDirectory>());
        services.AddScoped<ProviderReferences>();
        services.AddScoped<FitzProviderDirectory>();
        services.AddScoped<IProviderProjection>(provider => provider.GetRequiredService<FitzProviderDirectory>());
        services.AddScoped<IProviderReader>(provider => provider.GetRequiredService<FitzProviderDirectory>());
        services.AddScoped<ProviderReadConsistency>();
        services.AddScoped<ProviderChangeImpactService>();
        services.AddScoped<AssuranceReferences>();
        services.AddScoped<AssuranceDisclosure>();
        services.AddScoped<FitzAssuranceDirectory>();
        services.AddScoped<IAssuranceProjection>(provider => provider.GetRequiredService<FitzAssuranceDirectory>());
        services.AddScoped<IAssuranceReader>(provider => provider.GetRequiredService<FitzAssuranceDirectory>());
        services.AddScoped<AssuranceReadConsistency>();
        services.AddScoped<TechnologyInventoryReadConsistency>();
        services.AddScoped<FitzInventoryRegisterDirectory>();
        services.AddScoped<IInventoryRegisterProjection>(provider =>
            provider.GetRequiredService<FitzInventoryRegisterDirectory>());
        services.AddScoped<IInventoryRegisterReader>(provider =>
            provider.GetRequiredService<FitzInventoryRegisterDirectory>());
        services.AddScoped<InventoryRegisterReadConsistency>();
        services.AddScoped<TechnologyInventoryReferences>();
        services.AddScoped<ITechnologyInventoryActivity>(provider =>
            provider.GetRequiredService<TechnologyInventoryReferences>());
        services.AddScoped<PersonReadConsistency>();
        services.AddScoped<WorkRelationshipReadConsistency>();
        services.AddScoped<FitzWorkforceObservationDirectory>();
        services.AddScoped<IWorkforceObservationProjection>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationDirectory>());
        services.AddScoped<IWorkforceObservationDirectoryReader>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationDirectory>());
        services.AddScoped<WorkforceObservationReadConsistency>();
        services.AddScoped<FitzWorkforceObservationResolutionDirectory>();
        services.AddScoped<IWorkforceObservationResolutionProjection>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationResolutionDirectory>());
        services.AddScoped<IWorkforceObservationResolutionReader>(provider =>
            provider.GetRequiredService<FitzWorkforceObservationResolutionDirectory>());
        services.AddScoped<WorkforceObservationResolutions>();
        services.AddScoped<FitzWorkforceSourceDirectory>();
        services.AddScoped<IWorkforceSourceDirectoryReader>(provider =>
            provider.GetRequiredService<FitzWorkforceSourceDirectory>());
        services.AddScoped<IWorkforceSourceProjection>(provider =>
            provider.GetRequiredService<FitzWorkforceSourceDirectory>());
        services.AddScoped<WorkforceSourceReadConsistency>();
        services.AddScoped<WorkforceSourceTargets>();
        services.AddScoped<WorkforceRosterReconciler>();
        services.AddScoped<FitzServiceIdentityDirectory>();
        services.AddScoped<IServiceIdentityDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzServiceIdentityDirectory>());
        services.AddScoped<IServiceIdentityDirectoryReader>(provider =>
            provider.GetRequiredService<FitzServiceIdentityDirectory>());
        services.AddScoped<ServiceIdentityReadConsistency>();
        services.AddScoped<ServiceIdentityOwnership>();
        services.AddScoped<FitzRiskDraftDirectory>();
        services.AddScoped<IRiskDraftDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzRiskDraftDirectory>());
        services.AddScoped<IRiskDraftDirectoryReader>(provider =>
            provider.GetRequiredService<FitzRiskDraftDirectory>());
        services.AddScoped<RiskDraftReadConsistency>();
        services.AddScoped<RiskDraftListReadConsistency>();
        services.AddScoped<FitzRiskDraftHistoryDirectoryV1>();
        services.AddScoped<IRiskDraftHistoryDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzRiskDraftHistoryDirectoryV1>());
        services.AddScoped<IRiskDraftHistoryDirectoryReader>(provider =>
            provider.GetRequiredService<FitzRiskDraftHistoryDirectoryV1>());
        services.AddScoped<RiskDraftHistoryReadConsistency>();
        services.AddScoped<FitzRiskEvaluationDirectory>();
        services.AddScoped<IRiskEvaluationDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzRiskEvaluationDirectory>());
        services.AddScoped<IRiskEvaluationDirectoryReader>(provider =>
            provider.GetRequiredService<FitzRiskEvaluationDirectory>());
        services.AddScoped<RiskEvaluationReadConsistency>();
        services.AddScoped<FitzControlMappingDirectoryV1>();
        services.AddScoped<IControlMappingDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzControlMappingDirectoryV1>());
        services.AddScoped<IControlMappingDirectoryReader>(provider =>
            provider.GetRequiredService<FitzControlMappingDirectoryV1>());
        services.AddScoped<FitzCriterionApplicabilityDirectoryV1>();
        services.AddScoped<ICriterionApplicabilityDirectoryProjection>(provider =>
            provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>());
        services.AddScoped<ICriterionApplicabilityDirectoryReader>(provider =>
            provider.GetRequiredService<FitzCriterionApplicabilityDirectoryV1>());
        services.AddScoped<CriteriaCoverageReadConsistency>();
        services.AddScoped<FitzClientServiceDirectory>();
        services.AddScoped<IClientServiceDirectoryProjection>(
            provider => provider.GetRequiredService<FitzClientServiceDirectory>());
        services.AddScoped<IClientServiceDirectoryReader>(
            provider => provider.GetRequiredService<FitzClientServiceDirectory>());
        services.AddScoped<ClientServiceHistoryReadConsistency>();
        services.AddScoped<IClientServiceActivity, EventSourcedClientServiceActivity>();
        services.AddScoped<FitzBoundaryDirectory>();
        services.AddScoped<IBoundaryDirectoryProjection>(
            provider => provider.GetRequiredService<FitzBoundaryDirectory>());
        services.AddScoped<IBoundaryDirectoryReader>(
            provider => provider.GetRequiredService<FitzBoundaryDirectory>());
        services.AddScoped<FitzSnapshotDirectory>();
        services.AddScoped<ISnapshotDirectoryProjection>(
            provider => provider.GetRequiredService<FitzSnapshotDirectory>());
        services.AddScoped<ISnapshotDirectoryReader>(
            provider => provider.GetRequiredService<FitzSnapshotDirectory>());
        services.AddScoped<ScopeSnapshotFreezer>();
        services.AddScoped<FitzAccessPopulationDirectory>();
        services.AddScoped<IAccessPopulationDirectoryProjection>(
            provider => provider.GetRequiredService<FitzAccessPopulationDirectory>());
        services.AddScoped<IAccessPopulationDirectoryReader>(
            provider => provider.GetRequiredService<FitzAccessPopulationDirectory>());
        services.AddScoped<FitzAccessReviewCampaignDirectory>();
        services.AddScoped<IAccessReviewCampaignDirectoryProjection>(
            provider => provider.GetRequiredService<FitzAccessReviewCampaignDirectory>());
        services.AddScoped<IAccessReviewCampaignDirectoryReader>(
            provider => provider.GetRequiredService<FitzAccessReviewCampaignDirectory>());
        services.AddScoped<IAccessReviewSources, GovernedAccessReviewSources>();
        services.AddScoped<FitzPopulationSnapshotDirectory>();
        services.AddScoped<IPopulationSnapshotDirectoryProjection>(
            provider => provider.GetRequiredService<FitzPopulationSnapshotDirectory>());
        services.AddScoped<IPopulationSnapshotDirectoryReader>(
            provider => provider.GetRequiredService<FitzPopulationSnapshotDirectory>());
        services.AddScoped<PopulationSnapshotFreezer>();
        services.TryAddSingleton(TenantActivationPolicy.Default);
        services.AddScoped<TenantManagerInvariant>();
        services.AddScoped<WorkforceRosterSnapshotter>();
        services.AddScoped<BoundaryHistoryReadConsistency>();
        services.AddScoped<IBoundaryImpactContributor, ProgramBoundaryImpactContributor>();
        services.AddScoped<IBoundaryImpactContributor, ControlBoundaryImpactContributor>();
        services.AddScoped<FitzResponsibilitySetDirectory>();
        services.AddScoped<FitzMemberResponsibilityIndex>();
        services.AddScoped<IMemberResponsibilityIndex>(provider =>
            provider.GetRequiredService<FitzMemberResponsibilityIndex>());
        services.AddScoped<IResponsibilitySetDirectory>(provider =>
            provider.GetRequiredService<FitzResponsibilitySetDirectory>());
        services.AddScoped<IResponsibilitySetProjection>(provider =>
            provider.GetRequiredService<FitzResponsibilitySetDirectory>());
        services.AddScoped<FitzAccessGrantDirectory>();
        services.AddScoped<IAccessGrantProposalValidator, AccessGrantProposalValidator>();
        services.AddScoped<IAccessGrantDirectory>(provider =>
            provider.GetRequiredService<FitzAccessGrantDirectory>());
        services.AddScoped<IAccessGrantProjection>(provider =>
            provider.GetRequiredService<FitzAccessGrantDirectory>());
        services.AddScoped<IAccessGrantPermissionAuthorizer, AccessGrantPermissionAuthorizer>();
        services.AddScoped<OperatingAuthority>();
        services.AddScoped<WorkQueueReader>();
        services.AddScoped<IProgramResourceScopeResolver, ProgramResourceScopeResolver>();
        services.AddScoped<BoundaryResponsibilityScopeValidator>();
        services.AddScoped<ControlResponsibilityScopeValidator>();
        services.AddScoped<CommitmentResponsibilityScopeValidator>();
        services.AddScoped<ControlActivationSource>();
        services.AddScoped<ControlImpactService>();
        services.AddScoped<IControlImpactContributor, ApplicabilityControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, ResponsibilityControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, MappingControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, RiskTreatmentControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, ReadinessControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, EvidenceControlImpactContributor>();
        services.AddScoped<IControlImpactContributor, WorkControlImpactContributor>();
        services.AddScoped<ReadinessProjectionReadConsistency>();
        services.AddScoped<IReadinessSourceReader, DirectoryReadinessSourceReader>();
        services.AddScoped<IResponsibilityScopeValidator, SourceRecordResponsibilityScopeValidator>();
        services.AddScoped<BoundaryImpactService>();
        services.AddScoped<IBoundaryReferenceValidator,
            GovernedBoundaryReferenceValidator>();
        services.AddScoped<FitzRoleDirectoryReader>();
        services.AddScoped<IRoleDirectoryProjection>(provider => provider.GetRequiredService<FitzRoleDirectoryReader>());
        services.AddScoped<IRoleDirectoryReader>(provider => provider.GetRequiredService<FitzRoleDirectoryReader>());
        services.AddScoped<FitzRolePermissionDirectoryReader>();
        services.AddScoped<IRolePermissionDirectoryProjection>(
            provider => provider.GetRequiredService<FitzRolePermissionDirectoryReader>());
        services.AddScoped<IRolePermissionDirectoryReader>(
            provider => provider.GetRequiredService<FitzRolePermissionDirectoryReader>());
        services.AddScoped<FitzRoleTeamDirectoryReader>();
        services.AddScoped<IRoleTeamDirectoryProjection>(provider => provider.GetRequiredService<FitzRoleTeamDirectoryReader>());
        services.AddScoped<IRoleTeamDirectoryReader>(provider => provider.GetRequiredService<FitzRoleTeamDirectoryReader>());
        services.AddScoped<FitzTeamRoleDirectoryReader>();
        services.AddScoped<ITeamRoleDirectoryProjection>(provider => provider.GetRequiredService<FitzTeamRoleDirectoryReader>());
        services.AddScoped<ITeamRoleDirectoryReader>(provider => provider.GetRequiredService<FitzTeamRoleDirectoryReader>());
        services.AddSingleton<ITenantDirectory>(provider =>
            new EventSourcedTenantDirectory<TenantRegistered, TenantRegistered>(
                provider.GetRequiredService<IDomainEventReader>(),
                EventStreamPattern.ForPattern("bdgrz", "tenants"),
                domainEvent => new TenantId(((TenantRegistered)domainEvent).TenantId.ToString())));

        var portia = services
            .AddPortia()
            .AddRequestHandler<ContinueWithDeveloperIdentityHandler>()
            .AddRequestAuthorizer<ContinueWithDeveloperIdentityAuthorizer>()
            .AddRequestHandler<ContinueWithOidcProviderHandler>()
            .AddRequestAuthorizer<ContinueWithOidcProviderAuthorizer>()
            .AddRequestHandler<LinkOidcProviderIdentityHandler>()
            .AddRequestAuthorizer<LinkOidcProviderIdentityAuthorizer>()
            .AddRequestHandler<StartIdentityRecoveryHandler>()
            .AddRequestHandler<CompleteIdentityRecoveryHandler>()
            .AddRequestAuthorizer<IdentityRecoveryAuthorizer>()
            .AddRequestHandler<GetIdentityRecoveryOptionsHandler>()
            .AddRequestAuthorizer<IdentityRecoveryOptionsAuthorizer>()
            .AddRequestHandler<ReserveEmailHandler>()
            .AddRequestHandler<IssueEmailChallengeHandler>()
            .AddRequestHandler<CompleteEmailChallengeHandler>()
            .AddRequestHandler<GetEmailChallengeStatusHandler>()
            .AddRequestHandler<GetEmailAddressHandler>()
            .AddRequestHandler<ListEmailAddressesHandler>()
            .AddRequestAuthorizer<EmailOwnershipAuthorizer>()
            .AddRequestHandler<RegisterMemberHandler>()
            .AddRequestHandler<SuspendMemberHandler>()
            .AddRequestHandler<ReinstateMemberHandler>()
            .AddRequestHandler<GetTenantMemberHandler>()
            .AddRequestHandler<DefineTeamHandler>()
            .AddRequestHandler<DeleteTeamHandler>()
            .AddRequestHandler<DefineRoleHandler>()
            .AddRequestHandler<DeleteRoleHandler>()
            .AddRequestHandler<AssignTeamMemberHandler>()
            .AddRequestHandler<RemoveTeamMemberHandler>()
            .AddRequestHandler<AssignTeamRoleHandler>()
            .AddRequestHandler<RemoveTeamRoleHandler>()
            .AddRequestHandler<AssignRolePermissionHandler>()
            .AddRequestHandler<RemoveRolePermissionHandler>()
            .AddRequestHandler<GrantAccessHandler>()
            .AddRequestHandler<RevokeAccessGrantHandler>()
            .AddRequestHandler<ListAccessGrantsHandler>()
            .AddRequestAuthorizer<RbacManagementAuthorizer>()
            .AddRequestHandler<GetTeamHandler>()
            .AddRequestHandler<ListTeamsHandler>()
            .AddRequestHandler<ListTeamMembersHandler>()
            .AddRequestHandler<GetRoleHandler>()
            .AddRequestHandler<ListRolesHandler>()
            .AddRequestHandler<ListRolePermissionsHandler>()
            .AddRequestHandler<ListRoleTeamsHandler>()
            .AddRequestHandler<ListTeamRolesHandler>()
            .AddRequestAuthorizer<TenantAccessAuthorizer>()
            .AddRequestHandler<DeclareApplicationHandler>()
            .AddRequestHandler<ReviseApplicationHandler>()
            .AddRequestHandler<DeclareSystemInstanceHandler>()
            .AddRequestHandler<GetApplicationHandler>()
            .AddRequestHandler<ListApplicationsHandler>()
            .AddRequestHandler<GetApplicationRevisionHandler>()
            .AddRequestHandler<ListApplicationRevisionsHandler>()
            .AddRequestHandler<GetSystemInstanceHandler>()
            .AddRequestHandler<ListSystemInstancesHandler>()
            .AddRequestHandler<ListApplicationBoundaryReferencesHandler>()
            .AddRequestHandler<ListTechnologyComponentBoundaryReferencesHandler>()
            .AddRequestHandler<ListInformationAssetBoundaryReferencesHandler>()
            .AddRequestHandler<ListSystemInstanceBoundaryReferencesHandler>()
            .AddRequestHandler<PreviewApplicationChangeHandler>()
            .AddRequestHandler<RetireApplicationHandler>()
            .AddRequestHandler<RetireSystemInstanceHandler>()
            .AddRequestHandler<DecideAccessReviewScopeHandler>()
            .AddRequestHandler<GetAccessReviewScopeHandler>()
            .AddRequestHandler<ListAccessReviewScopesHandler>()
            .AddRequestHandler<PreviewInformationAssetChangeHandler>()
            .AddRequestHandler<StageApplicationImportHandler>()
            .AddRequestHandler<CancelApplicationImportHandler>()
            .AddRequestHandler<GetApplicationImportHandler>()
            .AddRequestHandler<ListApplicationImportRowsHandler>()
            .AddRequestHandler<PreviewApplicationImportHandler>()
            .AddRequestAuthorizer<ApplicationInventoryAuthorizer>()
            .AddRequestHandler<RecordProviderHandler>()
            .AddRequestHandler<ReviseProviderHandler>()
            .AddRequestHandler<GetProviderHandler>()
            .AddRequestHandler<ListProvidersHandler>()
            .AddRequestHandler<GetProviderRevisionHandler>()
            .AddRequestHandler<ListProviderRevisionsHandler>()
            .AddRequestHandler<RecordAssuranceReportHandler>()
            .AddRequestHandler<ReviseAssuranceReportHandler>()
            .AddRequestHandler<RecordProviderReviewHandler>()
            .AddRequestHandler<ListProviderAssuranceReportsHandler>()
            .AddRequestHandler<ListProviderReviewsHandler>()
            .AddRequestHandler<GetProviderAssuranceCoverageHandler>()
            .AddRequestHandler<PreviewProviderChangeHandler>()
            .AddRequestAuthorizer<ProviderAuthorizer>()
            .AddRequestHandler<RecordPersonHandler>()
            .AddRequestHandler<RevisePersonHandler>()
            .AddRequestHandler<CorrelatePersonMembershipHandler>()
            .AddRequestHandler<GetPersonHandler>()
            .AddRequestHandler<ListPeopleHandler>()
            .AddRequestHandler<RecordWorkRelationshipHandler>()
            .AddRequestHandler<ReviseWorkRelationshipHandler>()
            .AddRequestHandler<GetWorkRelationshipHandler>()
            .AddRequestHandler<ListWorkRelationshipsHandler>()
            .AddRequestHandler<ListWorkforceObservationsHandler>()
            .AddRequestHandler<ListWorkforceReconciliationObservationsHandler>()
            .AddRequestHandler<ResolveWorkforceObservationHandler>()
            .AddRequestHandler<RecordWorkforceSourceObservationHandler>()
            .AddRequestHandler<GetWorkforceSourceObservationHandler>()
            .AddRequestHandler<ListWorkforceSourceObservationsHandler>()
            .AddRequestHandler<PreviewWorkforceSourceObservationHandler>()
            .AddRequestHandler<ReconcileWorkforceSourceObservationHandler>()
            .AddRequestHandler<RecordServiceIdentityHandler>()
            .AddRequestHandler<ReviseServiceIdentityHandler>()
            .AddRequestHandler<GetServiceIdentityHandler>()
            .AddRequestHandler<ListServiceIdentitiesHandler>()
            .AddRequestAuthorizer<WorkforceAuthorizer>()
            .AddRequestHandler<RecordTechnologyComponentHandler>()
            .AddRequestHandler<ReviseTechnologyComponentHandler>()
            .AddRequestHandler<GetTechnologyComponentHandler>()
            .AddRequestHandler<ListTechnologyComponentsHandler>()
            .AddRequestHandler<ListTechnologyComponentRevisionsHandler>()
            .AddRequestHandler<RecordInformationAssetHandler>()
            .AddRequestHandler<ReviseInformationAssetHandler>()
            .AddRequestHandler<GetInformationAssetHandler>()
            .AddRequestHandler<ListInformationAssetsHandler>()
            .AddRequestHandler<ListInformationAssetRevisionsHandler>()
            .AddRequestHandler<RecordDataFlowHandler>()
            .AddRequestHandler<ReviseDataFlowHandler>()
            .AddRequestHandler<GetDataFlowHandler>()
            .AddRequestHandler<ListDataFlowsHandler>()
            .AddRequestHandler<ListDataFlowRevisionsHandler>()
            .AddRequestHandler<RecordLocationHandler>()
            .AddRequestHandler<ReviseLocationHandler>()
            .AddRequestHandler<GetLocationHandler>()
            .AddRequestHandler<ListLocationsHandler>()
            .AddRequestHandler<ListLocationRevisionsHandler>()
            .AddRequestHandler<RecordOperationalProcessHandler>()
            .AddRequestHandler<ReviseOperationalProcessHandler>()
            .AddRequestHandler<GetOperationalProcessHandler>()
            .AddRequestHandler<ListOperationalProcessesHandler>()
            .AddRequestHandler<ListOperationalProcessRevisionsHandler>()
            .AddRequestAuthorizer<TechnologyInventoryAuthorizer>()
            .AddRequestHandler<CreateProgramHandler>()
            .AddRequestHandler<ListCriteriaCatalogEditionsHandler>()
            .AddRequestHandler<GetCriteriaCatalogEditionHandler>()
            .AddRequestHandler<ListCriteriaCatalogEntriesHandler>()
            .AddRequestHandler<ExportCriteriaCatalogEntriesHandler>()
            .AddRequestHandler<GetCriteriaCatalogEntryHandler>()
            .AddRequestHandler<SetCriteriaTextOverlayHandler>()
            .AddRequestHandler<CreateControlDraftHandler>()
            .AddRequestHandler<ReviseControlDraftHandler>()
            .AddRequestHandler<DiscardControlDraftHandler>()
            .AddRequestHandler<GetControlDraftHandler>()
            .AddRequestHandler<ListControlDraftsHandler>()
            .AddRequestHandler<ListControlDraftRevisionsHandler>()
            .AddRequestHandler<GetControlDraftRevisionHandler>()
            .AddRequestHandler<ReviewControlHandler>()
            .AddRequestHandler<ApproveControlHandler>()
            .AddRequestHandler<GetControlVersionHandler>()
            .AddRequestHandler<GetCurrentControlVersionHandler>()
            .AddRequestHandler<GetEffectiveControlVersionHandler>()
            .AddRequestHandler<ListControlVersionsHandler>()
            .AddRequestHandler<GetControlDecisionHandler>()
            .AddRequestHandler<ListControlDecisionsHandler>()
            .AddRequestHandler<ProposeControlCriterionMappingHandler>()
            .AddRequestHandler<ReviewControlCriterionMappingHandler>()
            .AddRequestHandler<RetireControlCriterionMappingHandler>()
            .AddRequestHandler<GetControlCriterionMappingHandler>()
            .AddRequestHandler<ListControlCriterionMappingsHandler>()
            .AddRequestHandler<ListCriteriaCoverageHandler>()
            .AddRequestHandler<ProposeCriterionNotApplicableHandler>()
            .AddRequestHandler<ReviewCriterionApplicabilityHandler>()
            .AddRequestHandler<WithdrawCriterionNotApplicableHandler>()
            .AddRequestHandler<GetCriterionApplicabilityHandler>()
            .AddRequestHandler<ListCriterionApplicabilityHandler>()
            .AddRequestHandler<RunReadinessAssessmentHandler>()
            .AddRequestHandler<GetReadinessAssessmentHandler>()
            .AddRequestHandler<ListReadinessAssessmentsHandler>()
            .AddRequestHandler<ListReadinessGapsHandler>()
            .AddRequestHandler<PlanReadinessGapHandler>()
            .AddRequestHandler<DecideReadinessHandler>()
            .AddRequestHandler<DecideTypeIEntryHandler>()
            .AddRequestHandler<ListTypeIEntryDecisionsHandler>()
            .AddRequestHandler<AnnotateReadinessGapHandler>()
            .AddRequestHandler<ListReadinessAnnotationsHandler>()
            .AddRequestHandler<ProposeControlOperatingPlanHandler>()
            .AddRequestHandler<PreviewControlOperatingPlanHandler>()
            .AddRequestHandler<ApproveControlOperatingPlanHandler>()
            .AddRequestHandler<GetControlOperatingPlanHandler>()
            .AddRequestHandler<ListControlOperatingBlockersHandler>()
            .AddRequestHandler<ListMyControlWorkHandler>()
            .AddRequestHandler<OpenControlOccurrenceHandler>()
            .AddRequestHandler<AttestControlOccurrenceHandler>()
            .AddRequestHandler<CorrectControlAttestationHandler>()
            .AddRequestHandler<ReviewControlOccurrenceHandler>()
            .AddRequestHandler<GetControlOccurrenceHandler>()
            .AddRequestHandler<ListControlOccurrencesHandler>()
            .AddRequestHandler<RaiseFindingHandler>()
            .AddRequestHandler<RaiseOccurrenceFindingHandler>()
            .AddRequestHandler<ReviseFindingHandler>()
            .AddRequestHandler<AddCorrectiveActionHandler>()
            .AddRequestHandler<CompleteCorrectiveActionHandler>()
            .AddRequestHandler<LinkFindingAcceptanceHandler>()
            .AddRequestHandler<CloseFindingHandler>()
            .AddRequestHandler<ReopenFindingHandler>()
            .AddRequestHandler<GetFindingHandler>()
            .AddRequestHandler<ListFindingsHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.OpenEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.FulfilEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.CancelEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.GetEvidenceRequestHandler>()
            .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.ListEvidenceRequestsHandler>()
            .AddRequestHandler<ListWorkHandler>()
            .AddRequestHandler<GetWorkItemHandler>()
            .AddRequestHandler<ClaimWorkItemHandler>()
            .AddRequestHandler<AssignWorkItemHandler>()
            .AddRequestHandler<DelegateWorkItemHandler>()
            .AddRequestHandler<EscalateWorkItemHandler>()
            .AddRequestHandler<ListWorkRemindersHandler>()
            .AddRequestHandler<GetWorkDigestHandler>()
            .AddRequestHandler<SetWorkDigestPreferenceHandler>()
            .AddRequestHandler<GetWorkDigestPreferenceHandler>()
            .AddRequestAuthorizer<OperationsReactionAuthorizer>()
            .AddRequestHandler<AcceptAccessPopulationHandler>()
            .AddRequestHandler<ApproveAccessExpectationHandler>()
            .AddRequestHandler<ClassifyAccessPrincipalHandler>()
            .AddRequestHandler<CompleteAccessReviewCampaignHandler>()
            .AddRequestHandler<GetAccessPopulationHandler>()
            .AddRequestHandler<GetAccessReviewCampaignHandler>()
            .AddRequestHandler<GetAccessReviewCoverageHandler>()
            .AddRequestHandler<GetAccessVarianceHandler>()
            .AddRequestHandler<LaunchAccessReviewCampaignHandler>()
            .AddRequestHandler<ListAccessExpectationsHandler>()
            .AddRequestHandler<ListAccessPopulationsHandler>()
            .AddRequestHandler<ListAccessPrincipalsHandler>()
            .AddRequestHandler<ListAccessReviewCampaignsHandler>()
            .AddRequestHandler<OpenAccessPopulationHandler>()
            .AddRequestHandler<PreviewAccessPopulationHandler>()
            .AddRequestHandler<PreviewBulkAccessDecisionHandler>()
            .AddRequestHandler<ProposeAccessExpectationHandler>()
            .AddRequestHandler<RecordAccessDecisionHandler>()
            .AddRequestHandler<ExemptAccessExpectationHandler>()
            .AddRequestHandler<ExemptMissingAccessPopulationHandler>()
            .AddRequestHandler<RecordAccessPopulationFactsHandler>()
            .AddRequestHandler<RecordAccessRemediationChangeHandler>()
            .AddRequestHandler<ExemptAccessRemediationHandler>()
            .AddRequestHandler<RecordBulkAccessDecisionHandler>()
            .AddRequestHandler<VerifyAccessRemediationHandler>()
            .AddRequestAuthorizer<AccessReviewAuthorizer>()
            .AddRequestHandler<StartControlEvaluationHandler>()
            .AddRequestHandler<RecordControlEvaluationStepHandler>()
            .AddRequestHandler<DisposeControlEvaluationDeviationHandler>()
            .AddRequestHandler<SubmitControlEvaluationHandler>()
            .AddRequestHandler<ReviewControlEvaluationHandler>()
            .AddRequestHandler<GetControlEvaluationHandler>()
            .AddRequestHandler<ListControlEvaluationsHandler>()
            .AddRequestHandler<RaiseEvaluationDeviationFindingHandler>()
            .AddRequestHandler<ProposeControlSuccessorHandler>()
            .AddRequestHandler<ProposeControlRetirementHandler>()
            .AddRequestHandler<PreviewControlImpactHandler>()
            .AddRequestHandler<RetireControlHandler>()
            .AddRequestHandler<WithdrawControlProposalHandler>()
            .AddRequestHandler<DesignateControlOwnerPersonHandler>()
            .AddRequestHandler<CreateCommitmentDraftHandler>()
            .AddRequestHandler<ReviseCommitmentDraftHandler>()
            .AddRequestHandler<GetCommitmentDraftHandler>()
            .AddRequestHandler<ListCommitmentDraftsHandler>()
            .AddRequestHandler<ListCommitmentDraftRevisionsHandler>()
            .AddRequestHandler<GetCommitmentDraftRevisionHandler>()
            .AddRequestHandler<ReviewCommitmentDraftHandler>()
            .AddRequestHandler<ApproveCommitmentDraftHandler>()
            .AddRequestHandler<PreviewCommitmentImpactHandler>()
            .AddRequestHandler<GetCommitmentVersionHandler>()
            .AddRequestHandler<GetEffectiveCommitmentVersionHandler>()
            .AddRequestHandler<ListCommitmentVersionsHandler>()
            .AddRequestHandler<ListCommitmentDecisionsHandler>()
            .AddRequestHandler<CreatePolicyDraftHandler>()
            .AddRequestHandler<RevisePolicyDraftHandler>()
            .AddRequestHandler<ProposePolicySuccessorHandler>()
            .AddRequestHandler<DiscardPolicyDraftHandler>()
            .AddRequestHandler<ReviewPolicyDraftHandler>()
            .AddRequestHandler<ApprovePolicyHandler>()
            .AddRequestHandler<ConfirmPolicyReviewHandler>()
            .AddRequestHandler<ProposePolicyRetirementHandler>()
            .AddRequestHandler<ApprovePolicyRetirementHandler>()
            .AddRequestHandler<GetPolicyHandler>()
            .AddRequestHandler<ListPoliciesHandler>()
            .AddRequestHandler<GetPolicyVersionHandler>()
            .AddRequestHandler<GetEffectivePolicyVersionHandler>()
            .AddRequestHandler<ListPolicyVersionsHandler>()
            .AddRequestHandler<ListPolicyDecisionsHandler>()
            .AddRequestHandler<PreviewPolicyImpactHandler>()
            .AddRequestHandler<DefineTrainingRequirementHandler>()
            .AddRequestHandler<ReviseTrainingRequirementHandler>()
            .AddRequestHandler<GetTrainingRequirementHandler>()
            .AddRequestHandler<ListTrainingRequirementsHandler>()
            .AddRequestHandler<LaunchPolicyCampaignHandler>()
            .AddRequestHandler<LaunchTrainingCampaignHandler>()
            .AddRequestHandler<ReconcileCampaignAudienceHandler>()
            .AddRequestHandler<AcknowledgePolicyHandler>()
            .AddRequestHandler<RecordTrainingCompletionHandler>()
            .AddRequestHandler<ApproveCampaignWaiverHandler>()
            .AddRequestHandler<CloseCampaignHandler>()
            .AddRequestHandler<GetCampaignHandler>()
            .AddRequestHandler<ListCampaignsHandler>()
            .AddRequestHandler<ListCampaignParticipantsHandler>()
            .AddRequestHandler<ListCampaignAmendmentsHandler>()
            .AddRequestHandler<CreateRiskDraftHandler>()
            .AddRequestHandler<ReviseRiskDraftHandler>()
            .AddRequestHandler<GetRiskDraftHandler>()
            .AddRequestHandler<ListRiskDraftsHandler>()
            .AddRequestHandler<ListRiskDraftRevisionsHandler>()
            .AddRequestHandler<GetRiskDraftRevisionHandler>()
            .AddRequestHandler<PublishRiskMethodVersionHandler>()
            .AddRequestHandler<GetRiskMethodHandler>()
            .AddRequestHandler<GetRiskMethodVersionHandler>()
            .AddRequestHandler<RecordRiskAssessmentHandler>()
            .AddRequestHandler<ChooseRiskTreatmentHandler>()
            .AddRequestHandler<AcceptRiskHandler>()
            .AddRequestHandler<GetRiskEvaluationHandler>()
            .AddRequestHandler<ListRiskEvaluationHistoryHandler>()
            .AddRequestHandler<AssignRiskOwnerHandler>()
            .AddRequestHandler<ProposeRiskControlTreatmentHandler>()
            .AddRequestHandler<ReviewRiskControlTreatmentHandler>()
            .AddRequestHandler<RetireRiskControlTreatmentHandler>()
            .AddRequestHandler<GetRiskGovernanceHandler>()
            .AddRequestHandler<AddRiskTreatmentActionHandler>()
            .AddRequestHandler<ReviseRiskTreatmentActionHandler>()
            .AddRequestHandler<CancelRiskTreatmentActionHandler>()
            .AddRequestHandler<SubmitRiskTreatmentActionCompletionHandler>()
            .AddRequestHandler<ReviewRiskTreatmentActionCompletionHandler>()
            .AddRequestHandler<RaiseRiskReassessmentTriggersHandler>()
            .AddRequestAuthorizer<RiskReassessmentReactionAuthorizer>()
            .AddRequestHandler<ReviseProgramHandler>()
            .AddRequestHandler<SelectProgramCriteriaEditionHandler>()
            .AddRequestHandler<GetProgramHandler>()
            .AddRequestHandler<ListProgramsHandler>()
            .AddRequestHandler<ListProgramRevisionsHandler>()
            .AddRequestHandler<GetProgramRevisionHandler>()
            .AddRequestHandler<GetProgramSetupWorkHandler>()
            .AddRequestHandler<CreateClientServiceHandler>()
            .AddRequestHandler<ReviseClientServiceHandler>()
            .AddRequestHandler<RetireClientServiceHandler>()
            .AddRequestHandler<GetClientServiceHandler>()
            .AddRequestHandler<ListClientServicesHandler>()
            .AddRequestHandler<ListProgramClientServicesHandler>()
            .AddRequestHandler<ListClientServiceRevisionsHandler>()
            .AddRequestHandler<GetClientServiceRevisionHandler>()
            .AddRequestHandler<CreateBoundaryHandler>()
            .AddRequestHandler<ReviseBoundaryDraftHandler>()
            .AddRequestHandler<DiscardBoundaryDraftHandler>()
            .AddRequestHandler<GetBoundaryHandler>()
            .AddRequestHandler<ListProgramBoundariesHandler>()
            .AddRequestHandler<GetBoundaryVersionHandler>()
            .AddRequestHandler<ListBoundaryVersionsHandler>()
            .AddRequestHandler<GetEffectiveBoundaryVersionHandler>()
            .AddRequestHandler<GetBoundaryDecisionHandler>()
            .AddRequestHandler<ListBoundaryDecisionsHandler>()
            .AddRequestHandler<PreviewBoundaryImpactHandler>()
            .AddRequestHandler<ReviewBoundaryHandler>()
            .AddRequestHandler<ApproveBoundaryHandler>()
            .AddRequestHandler<RecordSeparationOfDutiesWaiverHandler>()
            .AddRequestHandler<ApproveSeparationOfDutiesWaiverHandler>()
            .AddRequestHandler<GetSeparationOfDutiesWaiverHandler>()
            .AddRequestHandler<AssignResponsibilityHandler>()
            .AddRequestHandler<RevokeResponsibilityHandler>()
            .AddRequestHandler<ListResponsibilitiesHandler>()
            .AddRequestHandler<ListMemberResponsibilitiesHandler>()
            .AddRequestHandler<PreviewResponsibilityConflictsHandler>()
            .AddRequestHandler<ProposeBoundarySuccessorHandler>()
            .AddRequestHandler<FreezeProgramScopeSnapshotHandler>()
            .AddRequestHandler<AmendProgramScopeSnapshotHandler>()
            .AddRequestHandler<GetSnapshotHandler>()
            .AddRequestHandler<VerifyProgramScopeSnapshotHandler>()
            .AddRequestHandler<RegenerateProgramScopeSnapshotManifestHandler>()
            .AddRequestHandler<ListProgramSnapshotsHandler>()
            .AddRequestHandler<FreezeWorkforceRosterSnapshotHandler>()
            .AddRequestHandler<AmendWorkforceRosterSnapshotHandler>()
            .AddRequestHandler<GetWorkforceRosterSnapshotHandler>()
            .AddRequestHandler<GetWorkforceRosterSnapshotAsOfHandler>()
            .AddRequestHandler<ListWorkforceRosterSnapshotsHandler>()
            .AddRequestHandler<RegenerateWorkforceRosterSnapshotManifestHandler>()
            .AddRequestAuthorizer<ProgramManagementAuthorizer>()
            .AddRequestAuthorizer<SeparationOfDutiesWaiverAuthorizer>()
            .AddRequestHandler<RegisterTenantHandler>()
            .AddRequestAuthorizer<RegisterTenantAuthorizer>()
            .AddRequestHandler<SeedPlatformOperatorRosterHandler>()
            .AddRequestAuthorizer<SeedPlatformOperatorRosterAuthorizer>()
            .AddRequestHandler<GrantPlatformOperatorHandler>()
            .AddRequestHandler<RevokePlatformOperatorHandler>()
            .AddRequestHandler<ListPlatformOperatorsHandler>()
            .AddRequestHandler<SuspendTenantHandler>()
            .AddRequestHandler<ReactivateTenantHandler>()
            .AddRequestHandler<InviteTenantMemberHandler>()
            .AddRequestHandler<InviteOrganizationMemberHandler>()
            .AddRequestHandler<ListTenantInvitationsHandler>()
            .AddRequestHandler<GetMemberAccessHandler>()
            .AddRequestHandler<AcceptTenantInvitationHandler>()
            .AddRequestHandler<GetTenantHandler>()
            .AddRequestAuthorizer<GetTenantAuthorizer>()
            .AddRequestHandler<ListTenantsHandler>()
            .AddRequestHandler<ListTenantMembersHandler>()
            .AddRequestHandler<ChangeTenantSlugHandler>()
            .AddRequestAuthorizer<PlatformOperatorOrTenantAdminAuthorizer>()
            .AddRequestHandler<ResolveMyTenantSlugHandler>()
            .AddRequestAuthorizer<ResolveMyTenantSlugAuthorizer>()
            .AddRequestAuthorizer<AcceptTenantInvitationAuthorizer>()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>()
            .AddRequestAuthorizer<ListTenantMembersAuthorizer>()
            .AddRequestHandler<ListMyTenantsHandler>()
            .AddRequestAuthorizer<ListMyTenantsAuthorizer>()
            .AddRequestGuard<RegisterTenantSlugAvailabilityGuard>()
            .AddRequestHandler<RegisterTenantSlugHandler>()
            .AddRequestHandler<RegisterTenantOwnerHandler>()
            .AddRequestHandler<ConfirmTenantSlugHandler>()
            .AddRequestHandler<RejectTenantSlugHandler>()
            .AddRequestHandler<RequestTenantSlugSurrenderHandler>()
            .AddRequestHandler<SurrenderTenantSlugHandler>()
            .AddRequestHandler<ConfirmTenantSlugSurrenderHandler>()
            .AddRequestHandler<RejectTenantSlugSurrenderHandler>()
            .AddRequestAuthorizer<TenantLifecycleReactionAuthorizer>()
            .AddReactor<TenantRegistrationReactor>("TenantRegistration", WorkloadScope.Global)
            .AddReactor<TenantInvitationReactor>("TenantInvitation", WorkloadScope.PerTenant, options =>
            {
                // Activation can wait for membership and permission projectors on another host.
                // Keep replay gaps short after the local transient retry window expires. A long
                // outage for one tenant must never fault the worker host (#430); permanent
                // failures are bounded by TenantActivationPolicy instead.
                options.MaximumFailureDelay = TimeSpan.FromSeconds(2);
                options.FailureAttemptLimit = TenantActivationPolicy.WorkloadFailureAttemptLimit;
            })
            .AddReactor<TenantInvitationDeliveryReactor>("TenantInvitationDeliveryV1",
                WorkloadScope.PerTenant)
            .AddReactor<EmailReservationReactor>("EmailReservation", WorkloadScope.Global)
            .AddReactor<EmailChallengeDeliveryReactor>("EmailChallengeDeliveryV1", WorkloadScope.Global)
            .AddReactor<IdentityRecoveryNoticeReactor>("IdentityRecoveryNoticeV1", WorkloadScope.Global)
            .AddProjector<PlatformUserDirectoryProjector>("PlatformUserDirectoryV2", WorkloadScope.Global)
            .AddProjector<IdentityDirectoryProjector>("UserIdentityDirectory", WorkloadScope.Global)
            .AddProjector<EmailAddressDirectoryProjector>("EmailAddressDirectory", WorkloadScope.Global)
            .AddReactor<TenantRbacBootstrapReactor>("TenantRbacBootstrap", WorkloadScope.Global)
            // Creator activation follows its durable team assignment on a tenant workload,
            // so a stalled activation cannot hold the global RBAC bootstrap cursor.
            .AddReactor<TenantSelfServiceActivationReactor>("TenantSelfServiceActivationV1",
                WorkloadScope.PerTenant, options =>
                {
                    options.MaximumFailureDelay = TimeSpan.FromSeconds(2);
                    options.FailureAttemptLimit = TenantActivationPolicy.WorkloadFailureAttemptLimit;
                })
            // This narrow backfill has its own checkpoint so it can safely replay historical
            // registrations without restoring intentionally removed memberships or grants.
            .AddReactor<ApplicationInventoryGrantBackfillReactor>(
                "ApplicationInventoryGrantBackfillV1", WorkloadScope.Global)
            .AddReactor<ProviderInventoryGrantBackfillReactor>(
                "ProviderInventoryGrantBackfillV1", WorkloadScope.Global)
            .AddReactor<WorkforceGrantBackfillReactor>(
                "WorkforceGrantBackfillV1", WorkloadScope.Global)
            .AddReactor<AccessReviewGrantBackfillReactor>(
                "AccessReviewGrantBackfillV1", WorkloadScope.Global)
            .AddReactor<WorkforceRestrictedFieldGrantBackfillReactor>(
                "WorkforceRestrictedFieldGrantBackfillV1", WorkloadScope.Global)
            .AddReactor<WorkforcePersonalDetailsGrantBackfillReactor>(
                WorkforcePersonalDetailsGrantBackfillReactor.WorkloadName, WorkloadScope.Global)
            .AddReactor<TenantSlugReactor>("TenantSlug", WorkloadScope.Global)
            .AddReactor<RiskMethodReassessmentReactor>("RiskMethodReassessmentV1",
                WorkloadScope.PerTenant)
            .AddReactor<BoundaryRiskReassessmentReactor>("BoundaryRiskReassessmentV1",
                WorkloadScope.PerTenant)
            .AddReactor<TeamCleanupReactor>("TeamCleanup", WorkloadScope.PerTenant)
            .AddReactor<RoleCleanupReactor>("RoleCleanup", WorkloadScope.PerTenant)
            .AddReactor<ControlOccurrenceFindingReactor>(
                ControlOccurrenceFindingReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddReactor<ControlEvaluationDeviationFindingReactor>(
                ControlEvaluationDeviationFindingReactor.WorkloadName, WorkloadScope.PerTenant)
            .AddProjector<PermissionProjector>("PermissionProjection", WorkloadScope.PerTenant)
            .AddProjector<TeamDirectoryProjector>("TeamDirectory", WorkloadScope.PerTenant)
            .AddProjector<TeamMemberDirectoryProjector>("TeamMemberDirectory", WorkloadScope.PerTenant)
            .AddProjector<RoleDirectoryProjector>("RoleDirectory", WorkloadScope.PerTenant)
            .AddProjector<RolePermissionDirectoryProjector>("RolePermissionDirectory", WorkloadScope.PerTenant)
            .AddProjector<RoleTeamDirectoryProjector>("RoleTeamDirectory", WorkloadScope.PerTenant)
            .AddProjector<TeamRoleDirectoryProjector>("TeamRoleDirectory", WorkloadScope.PerTenant)
            .AddProjector<TenantDirectoryProjector>("TenantDirectory", WorkloadScope.Global)
            .AddProjector<TenantMembershipProjector>("TenantMembership", WorkloadScope.PerTenant)
            .AddProjector<TenantInvitationDirectoryProjector>("TenantInvitationDirectory",
                WorkloadScope.PerTenant)
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant)
            .AddProjector<ControlDraftDirectoryV2Projector>("ControlDraftDirectoryV2",
                WorkloadScope.PerTenant)
            .AddProjector<ControlDraftHistoryDirectoryV1Projector>(
                "ControlDraftHistoryDirectoryV1", WorkloadScope.PerTenant)
            .AddProjector<CommitmentDraftDirectoryProjector>("CommitmentDraftDirectory",
                WorkloadScope.PerTenant)
            .AddProjector<PolicyDirectoryProjector>(FitzPolicyDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<CampaignDirectoryProjector>(FitzCampaignDirectory.ProjectorName,
                WorkloadScope.PerTenant)
            .AddProjector<CommitmentDraftHistoryDirectoryProjectorV1>(
                "CommitmentDraftHistoryDirectoryV1", WorkloadScope.PerTenant)
            .AddProjector<RiskDraftDirectoryProjector>("RiskDraftDirectory", WorkloadScope.PerTenant)
            .AddProjector<PersonDirectoryProjector>("PersonDirectoryV2", WorkloadScope.PerTenant)
            .AddProjector<TechnologyInventoryDirectoryProjector>(
                FitzTechnologyInventoryDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<InventoryRegisterProjector>(
                FitzInventoryRegisterDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<WorkRelationshipDirectoryProjector>("WorkRelationshipDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<WorkforceObservationProjector>("WorkforceObservationsV1",
                WorkloadScope.PerTenant)
            .AddProjector<WorkforceObservationResolutionProjector>(
                "WorkforceObservationResolutionsV1", WorkloadScope.PerTenant)
            .AddProjector<WorkforceSourceProjector>("WorkforceSourcesV1", WorkloadScope.PerTenant)
            .AddProjector<ServiceIdentityDirectoryProjector>("ServiceIdentityDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<RiskDraftHistoryProjectorV1>("RiskDraftHistoryDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<RiskEvaluationDirectoryProjectorV1>("RiskEvaluationDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<ControlMappingDirectoryV1Projector>(
                FitzControlMappingDirectoryV1.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<CriterionApplicabilityDirectoryV1Projector>(
                FitzCriterionApplicabilityDirectoryV1.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ApplicationDirectoryProjector>("ApplicationDirectoryV2", WorkloadScope.PerTenant)
            .AddProjector<ApplicationImportProjector>("ApplicationImportDirectoryV1", WorkloadScope.PerTenant)
            .AddProjector<AccessPopulationDirectoryProjector>(
                AccessReviewDirectorySchema.PopulationProjector, WorkloadScope.PerTenant)
            .AddProjector<AccessReviewCampaignDirectoryProjector>(
                AccessReviewDirectorySchema.CampaignProjector, WorkloadScope.PerTenant)
            .AddProjector<AccessReviewScopeDirectoryProjector>(
                AccessReviewScopeStreams.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ApplicationBoundaryReferenceProjector>(
                "ApplicationBoundaryReferencesV2", WorkloadScope.PerTenant)
            .AddProjector<ApplicationControlDraftReferenceProjector>(
                "ApplicationControlDraftReferencesV1", WorkloadScope.PerTenant)
            .AddProjector<ProviderProjector>(FitzProviderDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<AssuranceProjector>(FitzAssuranceDirectory.ProjectorName, WorkloadScope.PerTenant)
            .AddProjector<ClientServiceDirectoryProjector>("ClientServiceDirectory", WorkloadScope.PerTenant)
            .AddProjector<BoundaryDirectoryProjector>("BoundaryDirectoryV2", WorkloadScope.PerTenant)
            .AddProjector<ResponsibilitySetProjector>("ResponsibilitySetsV1", WorkloadScope.PerTenant)
            .AddProjector<MemberResponsibilityProjector>("MemberResponsibilitiesV1", WorkloadScope.PerTenant)
            .AddProjector<AccessGrantProjector>("AccessGrantsV1", WorkloadScope.PerTenant)
            .AddProjector<SnapshotDirectoryProjector>("SnapshotDirectory", WorkloadScope.PerTenant)
            .AddProjector<PopulationSnapshotDirectoryProjector>("PopulationSnapshotDirectoryV2",
                WorkloadScope.PerTenant)
            .AddFitz(
                configuration.GetSection("Fitz"),
                fitz => fitz.UseKvCheckpoints("kv://bdgrz/reactors/checkpoints"))
            .RequireAuthorization(options => options.AllowAnonymous<StartIdentityRecovery>());

        services.AddHostedService<PlatformOperatorRosterBootstrap>();
        return portia;
    }
}

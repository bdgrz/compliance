using Bdgrz.Compliance.Features.Criteria;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICriteriaCatalog>(CriteriaCatalog.Foundation);
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
        services.AddScoped<FitzPlatformUserDirectory>();
        services.AddScoped<IPlatformUserDirectoryProjection>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<IPlatformUserDirectoryReader>(
            provider => provider.GetRequiredService<FitzPlatformUserDirectory>());
        services.AddScoped<UserIdentityContinuation>();
        services.AddScoped<TenantInvitationIssuer>();
        services.AddScoped<FitzPermissionAuthorizer>();
        services.AddScoped<IPermissionProjection>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IPermissionAuthorizer>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IMemberAccessReader>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
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
        services.AddScoped<IApplicationInventoryActivity,
            EventSourcedApplicationInventoryActivity>();
        services.AddScoped<IControlApplicabilityReferenceValidator,
            GovernedControlApplicabilityReferenceValidator>();
        services.AddScoped<ApplicationHistoryReadConsistency>();
        services.AddScoped<SystemInstanceReadConsistency>();
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
        services.AddScoped<BoundaryHistoryReadConsistency>();
        services.AddScoped<IBoundaryImpactContributor, ProgramBoundaryImpactContributor>();
        services.AddScoped<IBoundaryImpactContributor, ControlBoundaryImpactContributor>();
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
            .AddRequestHandler<ReserveEmailHandler>()
            .AddRequestHandler<IssueEmailChallengeHandler>()
            .AddRequestHandler<CompleteEmailChallengeHandler>()
            .AddRequestHandler<GetEmailChallengeStatusHandler>()
            .AddRequestHandler<GetEmailAddressHandler>()
            .AddRequestHandler<ListEmailAddressesHandler>()
            .AddRequestAuthorizer<EmailOwnershipAuthorizer>()
            .AddRequestHandler<RegisterMemberHandler>()
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
            .AddRequestAuthorizer<RbacManagementAuthorizer>()
            .AddRequestHandler<GetTeamHandler>()
            .AddRequestHandler<ListTeamsHandler>()
            .AddRequestHandler<ListTeamMembersHandler>()
            .AddRequestHandler<GetRoleHandler>()
            .AddRequestHandler<ListRolesHandler>()
            .AddRequestHandler<ListRolePermissionsHandler>()
            .AddRequestHandler<ListRoleTeamsHandler>()
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
            .AddRequestHandler<ListSystemInstanceBoundaryReferencesHandler>()
            .AddRequestHandler<PreviewApplicationChangeHandler>()
            .AddRequestHandler<StageApplicationImportHandler>()
            .AddRequestHandler<CancelApplicationImportHandler>()
            .AddRequestHandler<GetApplicationImportHandler>()
            .AddRequestHandler<ListApplicationImportRowsHandler>()
            .AddRequestHandler<PreviewApplicationImportHandler>()
            .AddRequestAuthorizer<ApplicationInventoryAuthorizer>()
            .AddRequestHandler<CreateProgramHandler>()
            .AddRequestHandler<ListCriteriaCatalogEditionsHandler>()
            .AddRequestHandler<GetCriteriaCatalogEditionHandler>()
            .AddRequestHandler<ListCriteriaCatalogEntriesHandler>()
            .AddRequestHandler<GetCriteriaCatalogEntryHandler>()
            .AddRequestHandler<CreateControlDraftHandler>()
            .AddRequestHandler<ReviseControlDraftHandler>()
            .AddRequestHandler<DiscardControlDraftHandler>()
            .AddRequestHandler<GetControlDraftHandler>()
            .AddRequestHandler<ListControlDraftsHandler>()
            .AddRequestHandler<ListControlDraftRevisionsHandler>()
            .AddRequestHandler<GetControlDraftRevisionHandler>()
            .AddRequestHandler<CreateCommitmentDraftHandler>()
            .AddRequestHandler<ReviseCommitmentDraftHandler>()
            .AddRequestHandler<GetCommitmentDraftHandler>()
            .AddRequestHandler<ListCommitmentDraftsHandler>()
            .AddRequestHandler<ListCommitmentDraftRevisionsHandler>()
            .AddRequestHandler<GetCommitmentDraftRevisionHandler>()
            .AddRequestHandler<CreateRiskDraftHandler>()
            .AddRequestHandler<ReviseRiskDraftHandler>()
            .AddRequestHandler<GetRiskDraftHandler>()
            .AddRequestHandler<ListRiskDraftsHandler>()
            .AddRequestHandler<ListRiskDraftRevisionsHandler>()
            .AddRequestHandler<GetRiskDraftRevisionHandler>()
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
            .AddRequestHandler<ProposeBoundarySuccessorHandler>()
            .AddRequestHandler<FreezeProgramScopeSnapshotHandler>()
            .AddRequestHandler<AmendProgramScopeSnapshotHandler>()
            .AddRequestHandler<GetSnapshotHandler>()
            .AddRequestHandler<VerifyProgramScopeSnapshotHandler>()
            .AddRequestHandler<RegenerateProgramScopeSnapshotManifestHandler>()
            .AddRequestHandler<ListProgramSnapshotsHandler>()
            .AddRequestAuthorizer<ProgramManagementAuthorizer>()
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
            .AddRequestHandler<ResolveMyTenantSlugHandler>()
            .AddRequestAuthorizer<ResolveMyTenantSlugAuthorizer>()
            .AddRequestAuthorizer<AcceptTenantInvitationAuthorizer>()
            .AddRequestHandler<ActivateTenantHandler>()
            .AddRequestAuthorizer<ActivateTenantAuthorizer>()
            .AddRequestAuthorizer<PlatformOperatorAuthorizer>()
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
            .AddReactor<TenantInvitationReactor>("TenantInvitation", WorkloadScope.PerTenant)
            .AddReactor<TenantInvitationDeliveryReactor>("TenantInvitationDeliveryV1",
                WorkloadScope.PerTenant)
            .AddReactor<EmailReservationReactor>("EmailReservation", WorkloadScope.Global)
            .AddReactor<EmailChallengeDeliveryReactor>("EmailChallengeDeliveryV1", WorkloadScope.Global)
            .AddProjector<PlatformUserDirectoryProjector>("PlatformUserDirectory", WorkloadScope.Global)
            .AddProjector<EmailAddressDirectoryProjector>("EmailAddressDirectory", WorkloadScope.Global)
            .AddReactor<TenantRbacBootstrapReactor>("TenantRbacBootstrap", WorkloadScope.Global)
            // This narrow backfill has its own checkpoint so it can safely replay historical
            // registrations without restoring intentionally removed memberships or grants.
            .AddReactor<ApplicationInventoryGrantBackfillReactor>(
                "ApplicationInventoryGrantBackfillV1", WorkloadScope.Global)
            .AddReactor<TenantSlugReactor>("TenantSlug", WorkloadScope.Global)
            .AddReactor<TeamCleanupReactor>("TeamCleanup", WorkloadScope.PerTenant)
            .AddReactor<RoleCleanupReactor>("RoleCleanup", WorkloadScope.PerTenant)
            .AddProjector<PermissionProjector>("PermissionProjection", WorkloadScope.PerTenant)
            .AddProjector<TeamDirectoryProjector>("TeamDirectory", WorkloadScope.PerTenant)
            .AddProjector<TeamMemberDirectoryProjector>("TeamMemberDirectory", WorkloadScope.PerTenant)
            .AddProjector<RoleDirectoryProjector>("RoleDirectory", WorkloadScope.PerTenant)
            .AddProjector<RolePermissionDirectoryProjector>("RolePermissionDirectory", WorkloadScope.PerTenant)
            .AddProjector<RoleTeamDirectoryProjector>("RoleTeamDirectory", WorkloadScope.PerTenant)
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
            .AddProjector<CommitmentDraftHistoryDirectoryProjectorV1>(
                "CommitmentDraftHistoryDirectoryV1", WorkloadScope.PerTenant)
            .AddProjector<RiskDraftDirectoryProjector>("RiskDraftDirectory", WorkloadScope.PerTenant)
            .AddProjector<RiskDraftHistoryProjectorV1>("RiskDraftHistoryDirectoryV1",
                WorkloadScope.PerTenant)
            .AddProjector<ApplicationDirectoryProjector>("ApplicationDirectory", WorkloadScope.PerTenant)
            .AddProjector<ApplicationImportProjector>("ApplicationImportDirectoryV1", WorkloadScope.PerTenant)
            .AddProjector<ApplicationBoundaryReferenceProjector>(
                "ApplicationBoundaryReferencesV1", WorkloadScope.PerTenant)
            .AddProjector<ApplicationControlDraftReferenceProjector>(
                "ApplicationControlDraftReferencesV1", WorkloadScope.PerTenant)
            .AddProjector<ClientServiceDirectoryProjector>("ClientServiceDirectory", WorkloadScope.PerTenant)
            .AddProjector<BoundaryDirectoryProjector>("BoundaryDirectoryV2", WorkloadScope.PerTenant)
            .AddProjector<SnapshotDirectoryProjector>("SnapshotDirectory", WorkloadScope.PerTenant)
            .AddFitz(
                configuration.GetSection("Fitz"),
                fitz => fitz.UseKvCheckpoints("kv://bdgrz/reactors/checkpoints"))
            .RequireAuthorization();

        services.AddHostedService<PlatformOperatorRosterBootstrap>();
        return portia;
    }
}

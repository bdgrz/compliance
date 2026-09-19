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
        bool developerAuthentication = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton(new DeveloperUserRegistration(developerAuthentication));
        services.AddSingleton(PlatformOperatorAuthority.FromConfiguration(configuration, developerAuthentication));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MockEmailChallengeDelivery>();
        services.AddSingleton<IEmailChallengeDelivery>(provider => provider.GetRequiredService<MockEmailChallengeDelivery>());
        services.AddSingleton<MockTenantInvitationDelivery>();
        services.AddSingleton<ITenantInvitationDelivery>(provider =>
            provider.GetRequiredService<MockTenantInvitationDelivery>());
        services.AddScoped<FitzEmailAddressDirectory>();
        services.AddScoped<IEmailAddressDirectoryProjection>(
            provider => provider.GetRequiredService<FitzEmailAddressDirectory>());
        services.AddScoped<IEmailAddressDirectoryReader>(
            provider => provider.GetRequiredService<FitzEmailAddressDirectory>());
        services.AddScoped<UserIdentityContinuation>();
        services.AddScoped<FitzPermissionAuthorizer>();
        services.AddScoped<IPermissionProjection>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
        services.AddScoped<IPermissionAuthorizer>(provider => provider.GetRequiredService<FitzPermissionAuthorizer>());
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
        services.AddScoped<FitzProgramDirectory>();
        services.AddScoped<IProgramDirectoryProjection>(
            provider => provider.GetRequiredService<FitzProgramDirectory>());
        services.AddScoped<IProgramDirectoryReader>(
            provider => provider.GetRequiredService<FitzProgramDirectory>());
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

        return services
            .AddPortia()
            .AddRequestHandler<ContinueWithDeveloperIdentityHandler>()
            .AddRequestHandler<ContinueWithOidcProviderHandler>()
            .AddRequestHandler<ReserveEmailHandler>()
            .AddRequestHandler<IssueEmailChallengeHandler>()
            .AddRequestHandler<CompleteEmailChallengeHandler>()
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
            .AddRequestHandler<CreateProgramHandler>()
            .AddRequestHandler<ReviseProgramHandler>()
            .AddRequestHandler<GetProgramHandler>()
            .AddRequestHandler<ListProgramsHandler>()
            .AddRequestAuthorizer<ProgramManagementAuthorizer>()
            .AddRequestHandler<RegisterTenantHandler>()
            .AddRequestHandler<SuspendTenantHandler>()
            .AddRequestHandler<ReactivateTenantHandler>()
            .AddRequestHandler<InviteTenantMemberHandler>()
            .AddRequestHandler<AcceptTenantInvitationHandler>()
            .AddRequestHandler<GetTenantHandler>()
            .AddRequestAuthorizer<GetTenantAuthorizer>()
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
            .AddReactor<TenantRegistrationReactor>("TenantRegistration", WorkloadScope.Global)
            .AddReactor<TenantInvitationReactor>("TenantInvitation", WorkloadScope.PerTenant)
            .AddReactor<EmailReservationReactor>("EmailReservation", WorkloadScope.Global)
            .AddProjector<EmailAddressDirectoryProjector>("EmailAddressDirectory", WorkloadScope.Global)
            .AddReactor<TenantRbacBootstrapReactor>("TenantRbacBootstrap", WorkloadScope.Global)
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
            .AddProjector<ProgramDirectoryProjector>("ProgramDirectory", WorkloadScope.PerTenant)
            .AddFitz(
                configuration.GetSection("Fitz"),
                fitz => fitz.UseKvCheckpoints("kv://bdgrz/reactors/checkpoints"));
    }
}

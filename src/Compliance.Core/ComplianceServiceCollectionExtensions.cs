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
        services.AddScoped<UserIdentityContinuation>();
        services.AddScoped<IPermissionProjection, FitzPermissionProjection>();
        services.AddSingleton<IPermissionAuthorizer, FitzPermissionAuthorizer>();
        services.AddScoped<ITeamDirectoryProjection, FitzTeamDirectoryProjection>();
        services.AddSingleton<ITeamDirectoryReader, FitzTeamDirectoryReader>();
        services.AddScoped<ITeamMemberDirectoryProjection, FitzTeamMemberDirectoryProjection>();
        services.AddSingleton<ITeamMemberDirectoryReader, FitzTeamMemberDirectoryReader>();
        services.AddSingleton<ITenantDirectory>(provider =>
            new EventSourcedTenantDirectory<TenantRegistered, TenantRegistered>(
                provider.GetRequiredService<IDomainEventReader>(),
                EventStreamPattern.ForPattern("bdgrz", "tenants"),
                domainEvent => new TenantId(((TenantRegistered)domainEvent).TenantId.ToString())));

        return services
            .AddPortia()
            .AddRequestHandler<ContinueWithDeveloperIdentityHandler>()
            .AddRequestHandler<ContinueWithOidcProviderHandler>()
            .AddRequestHandler<RegisterMemberHandler>()
            .AddRequestHandler<DefineTeamHandler>()
            .AddRequestHandler<DeleteTeamHandler>()
            .AddRequestHandler<DefineRoleHandler>()
            .AddRequestHandler<AssignTeamMemberHandler>()
            .AddRequestHandler<RemoveTeamMemberHandler>()
            .AddRequestHandler<AssignTeamRoleHandler>()
            .AddRequestHandler<AssignRolePermissionHandler>()
            .AddRequestAuthorizer<RbacManagementAuthorizer>()
            .AddRequestHandler<GetTeamHandler>()
            .AddRequestHandler<ListTeamsHandler>()
            .AddRequestHandler<ListTeamMembersHandler>()
            .AddRequestAuthorizer<TenantAccessAuthorizer>()
            .AddRequestHandler<RegisterTenantHandler>()
            .AddRequestAuthorizer<RegisterTenantAuthorizer>()
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
            .AddReactor<TenantRbacBootstrapReactor>("TenantRbacBootstrap", WorkloadScope.Global)
            .AddReactor<TenantSlugReactor>("TenantSlug", WorkloadScope.Global)
            .AddReactor<TeamCleanupReactor>("TeamCleanup", WorkloadScope.PerTenant)
            .AddProjector<PermissionProjector>("PermissionProjection", WorkloadScope.PerTenant)
            .AddProjector<TeamDirectoryProjector>("TeamDirectory", WorkloadScope.PerTenant)
            .AddProjector<TeamMemberDirectoryProjector>("TeamMemberDirectory", WorkloadScope.PerTenant)
            .AddFitz(
                configuration.GetSection("Fitz"),
                fitz => fitz.UseKvCheckpoints("kv://bdgrz/reactors/checkpoints"));
    }
}

using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Tenants;

public sealed class GetTenantProvisioningStatusRequestScenarioTests
{
    [Fact]
    public async Task ShouldReturnRejectedStatusGivenCreatorRegistrationLosesSlugClaim()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        await using var provider = BuildProvider();
        var tenant = new Tenant(tenantId);
        Assert.True(tenant.Register(creatorId, "Acme", "acme", creatorIsAdministrator: true).IsSuccess);
        Assert.True(tenant.RejectSlug("acme").IsSuccess);
        Assert.False(tenant.IsRegistered);
        await using (var scope = provider.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
                .SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));

        // Act
        var scenario = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(creatorId))
            .When(new GetTenantProvisioningStatus(tenantId))
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectSuccess();

        // Assert
        Assert.Equal(new TenantProvisioningStatusView(tenantId, "rejected", "slug_unavailable"), scenario.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnProvisioningStatusGivenCreatorBeforeMembershipAndGrantProjection(bool confirmed)
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var tenant = RegisteredTenant(creatorId);
        if (confirmed)
            Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        await using var provider = BuildProvider();
        await SaveAsync(provider, tenant);

        // Act
        var result = await ReadAsync(provider, tenant.Id, creatorId);

        // Assert
        Assert.Equal(new TenantProvisioningStatusView(tenant.Id, "provisioning", null), result.Value);
    }

    [Fact]
    public async Task ShouldReturnCompletedStatusGivenCreatorActivationCompleted()
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var tenant = CompletedTenant(creatorId);
        await using var provider = BuildProvider(projectedWorkspaceAccess: false);
        await SaveAsync(provider, tenant);

        // Act
        var result = await ReadAsync(provider, tenant.Id, creatorId);
        var privateRead = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(creatorId))
            .When(new GetTenant(tenant.Id))
            .ExpectDenied(RequestErrorKind.NotFound)
            .ExpectNotHandled();

        // Assert
        Assert.Equal(new TenantProvisioningStatusView(tenant.Id, "completed", null), result.Value);
        Assert.False(privateRead.IsSuccess);
    }

    [Fact]
    public async Task ShouldReturnCompletedStatusGivenHistoricalSelfServiceRegistrationWithoutActivationFence()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var creatorId = Uuid.CreateVersion4();
        await using var provider = BuildProvider();
        await SeedHistoricalAsync(provider, tenantId, creatorId);

        // Act
        var result = await ReadAsync(provider, tenantId, creatorId);

        // Assert
        Assert.Equal(new TenantProvisioningStatusView(tenantId, "completed", null), result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainCompletedStatusGivenTenantSuspendedAfterInitialActivation(bool historical)
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        await using var provider = BuildProvider();
        var tenant = await SeedCompletedAsync(provider, tenantId, creatorId, historical);
        Assert.True(tenant.Suspend(Uuid.CreateVersion4(), "Temporarily suspended", DateTimeOffset.UtcNow).IsSuccess);
        await SaveAsync(provider, tenant);

        // Act
        var result = await ReadAsync(provider, tenantId, creatorId);

        // Assert
        Assert.False(tenant.IsActive);
        Assert.Equal(new TenantProvisioningStatusView(tenantId, "completed", null), result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainCompletedStatusGivenSlugSurrenderAfterInitialProvisioning(bool historical)
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var tenantId = Uuid.CreateVersion4();
        await using var provider = BuildProvider();
        var tenant = await SeedCompletedAsync(provider, tenantId, creatorId, historical);
        Assert.True(tenant.RequestSlugSurrender("acme").IsSuccess);
        Assert.True(tenant.ConfirmSlugSurrender("acme").IsSuccess);
        await SaveAsync(provider, tenant);

        // Act
        var result = await ReadAsync(provider, tenantId, creatorId);

        // Assert
        Assert.False(tenant.IsActive);
        Assert.False(tenant.IsRegistered);
        Assert.Equal(new TenantProvisioningStatusView(tenantId, "completed", null), result.Value);
    }

    [Fact]
    public async Task ShouldHideProvisioningStatusGivenDifferentCanonicalUserOrMissingTenant()
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var foreignId = Uuid.CreateVersion4();
        var tenant = RegisteredTenant(creatorId);
        await using var provider = BuildProvider(projectedWorkspaceAccess: true, operatorAccess: true);
        await SaveAsync(provider, tenant);
        var actor = BdgrzActor(foreignId);
        ((ClaimsIdentity)actor.Identity!).AddClaim(new Claim("role", "administrator"));

        // Act
        var foreign = await RequestScenario.For(provider)
            .GivenActor(actor)
            .When(new GetTenantProvisioningStatus(tenant.Id))
            .ExpectDenied(RequestErrorKind.NotFound)
            .ExpectNotHandled();
        var missing = await RequestScenario.For(provider)
            .GivenActor(actor)
            .When(new GetTenantProvisioningStatus(Uuid.CreateVersion4()))
            .ExpectDenied(RequestErrorKind.NotFound)
            .ExpectNotHandled();

        // Assert
        Assert.NotNull(missing.Error);
        Assert.NotNull(foreign.Error);
        Assert.Equal((missing.Error.Kind, missing.Error.Message, missing.Error.IsTransient),
            (foreign.Error.Kind, foreign.Error.Message, foreign.Error.IsTransient));
        Assert.Equal("The tenant was not found.", foreign.Error.Message);
    }

    [Fact]
    public async Task ShouldHideProvisioningStatusGivenLegacyOperatorCreatedRegistration()
    {
        // Arrange
        var operatorId = Uuid.CreateVersion4();
        var tenant = new Tenant(Uuid.CreateVersion4());
        Assert.True(tenant.Register(operatorId, "Acme", "acme").IsSuccess);
        await using var provider = BuildProvider(projectedWorkspaceAccess: true, operatorAccess: true);
        await SaveAsync(provider, tenant);

        // Act
        var legacy = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(operatorId))
            .When(new GetTenantProvisioningStatus(tenant.Id))
            .ExpectDenied(RequestErrorKind.NotFound)
            .ExpectNotHandled();
        var missing = await RequestScenario.For(provider)
            .GivenActor(BdgrzActor(operatorId))
            .When(new GetTenantProvisioningStatus(Uuid.CreateVersion4()))
            .ExpectDenied(RequestErrorKind.NotFound)
            .ExpectNotHandled();

        // Assert
        Assert.NotNull(missing.Error);
        Assert.NotNull(legacy.Error);
        Assert.Equal((missing.Error.Kind, missing.Error.Message, missing.Error.IsTransient),
            (legacy.Error.Kind, legacy.Error.Message, legacy.Error.IsTransient));
        Assert.Equal("The tenant was not found.", legacy.Error.Message);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("external")]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("unauthenticated")]
    [InlineData("system")]
    [InlineData("canonical_system")]
    public async Task ShouldRejectProvisioningStatusGivenActorWithoutCanonicalUser(string actorKind)
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var tenant = RegisteredTenant(creatorId);
        await using var provider = BuildProvider();
        await SaveAsync(provider, tenant);
        var actor = actorKind switch
        {
            "anonymous" => RequestActor.Anonymous,
            "system" => RequestActor.System,
            "canonical_system" => RequestActor.CreateSystem(creatorId.ToString(), "bdgrz"),
            "external" => new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "external"), new Claim("sub", creatorId.ToString())], "External")),
            "unauthenticated" => new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", creatorId.ToString())])),
            _ => new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", "bdgrz"), new Claim("sub", actorKind switch
                {
                    "malformed" => "invalid-user-id",
                    "zero" => Uuid.Empty.ToString(),
                    _ => string.Empty,
                })], "BdgrzSession")),
        };

        // Act
        var result = await RequestScenario.For(provider)
            .GivenActor(actor)
            .When(new GetTenantProvisioningStatus(tenant.Id))
            .ExpectDenied(RequestErrorKind.Unauthorized)
            .ExpectNotHandled();

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveEventHistoryGivenRepeatedProvisioningStatusReads(bool rejected)
    {
        // Arrange
        var creatorId = Uuid.CreateVersion4();
        var tenant = rejected ? RegisteredTenant(creatorId) : CompletedTenant(creatorId);
        if (rejected)
            Assert.True(tenant.RejectSlug("acme").IsSuccess);
        var commands = new ReactorScenario();
        await using var provider = BuildProvider(commands: commands.Requests);
        await SaveAsync(provider, tenant);
        var before = await HistoryAsync(provider, tenant);

        // Act
        var first = await ReadAsync(provider, tenant.Id, creatorId);
        var second = await ReadAsync(provider, tenant.Id, creatorId);
        var after = await HistoryAsync(provider, tenant);

        // Assert
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(before, after);
        Assert.Empty(commands.SentRequests);
    }

    static ServiceProvider BuildProvider(bool? projectedWorkspaceAccess = null, bool operatorAccess = false,
        IRequestBus? commands = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia()
            .AddRequestHandler<GetTenantProvisioningStatusHandler>()
            .AddRequestAuthorizer<GetTenantProvisioningStatusAuthorizer>();
        if (commands is not null)
            services.AddSingleton(commands);
        if (projectedWorkspaceAccess is { } allowed)
        {
            var access = new ProjectedAccess(allowed);
            services.AddSingleton<IPlatformOperatorAccess>(new FixedOperatorAccess { AllowAll = operatorAccess });
            services.AddSingleton<ITenantMembershipDirectoryReader>(access);
            services.AddSingleton<ITenantActivity>(access);
            services.AddSingleton<IPermissionAuthorizer>(access);
            services.AddSingleton<ITenantDirectoryReader>(access);
            services.AddPortia()
                .AddRequestHandler<GetTenantHandler>()
                .AddRequestAuthorizer<GetTenantAuthorizer>();
        }
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    static async Task<Result<TenantProvisioningStatusView>> ReadAsync(ServiceProvider provider,
        Uuid tenantId, Uuid creatorId) => await RequestScenario.For(provider)
        .GivenActor(BdgrzActor(creatorId))
        .When(new GetTenantProvisioningStatus(tenantId))
        .ExpectAuthorized()
        .ExpectHandled()
        .ExpectSuccess();

    static Tenant RegisteredTenant(Uuid creatorId, Uuid? tenantId = null)
    {
        var tenant = new Tenant(tenantId ?? Uuid.CreateVersion4());
        Assert.True(tenant.Register(creatorId, "Acme", "acme", creatorIsAdministrator: true).IsSuccess);
        return tenant;
    }

    static Tenant CompletedTenant(Uuid creatorId, Uuid? tenantId = null)
    {
        var tenant = RegisteredTenant(creatorId, tenantId);
        Assert.True(tenant.ConfirmSlug("acme").IsSuccess);
        Assert.True(tenant.Activate(creatorId, null).IsSuccess);
        return tenant;
    }

    static async Task SaveAsync(ServiceProvider provider, Tenant tenant)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAggregateWriter>()
            .SaveAsync(tenant, new RequestDispatchContext(RequestActor.System));
    }

    static async Task SeedHistoricalAsync(ServiceProvider provider, Uuid tenantId, Uuid creatorId)
    {
        var json = $$"""{"tenant_id":"{{tenantId}}","owner_user_id":"{{creatorId}}","name":"Acme","slug":"acme","creator_is_administrator":true}""";
        var registered = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.TenantRegistered);
        Assert.NotNull(registered);
        Assert.False(registered.ActivationRequired);
        await provider.GetRequiredService<IEventStore>().AppendAsync(new Tenant(tenantId).Stream, 0,
            [DomainEventSeed.Attach(registered, tenantId, 1),
                DomainEventSeed.Attach(new TenantSlugConfirmed(tenantId, "acme"), tenantId, 2)]);
    }

    static async Task<Tenant> SeedCompletedAsync(ServiceProvider provider, Uuid tenantId,
        Uuid creatorId, bool historical)
    {
        if (historical)
            await SeedHistoricalAsync(provider, tenantId, creatorId);
        else
            await SaveAsync(provider, CompletedTenant(creatorId, tenantId));
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(new Tenant(tenantId));
    }

    static async Task<DomainEvent[]> HistoryAsync(ServiceProvider provider, Tenant tenant)
    {
        var history = new List<DomainEvent>();
        await foreach (var record in provider.GetRequiredService<IEventStore>().ReadAsync(tenant.Stream, 0))
            history.Add(record.Event);
        return history.ToArray();
    }

    static ClaimsPrincipal BdgrzActor(Uuid userId) => new(new ClaimsIdentity(
        [new Claim("iss", "bdgrz"), new Claim("sub", userId.ToString())], "BdgrzSession"));

    sealed class ProjectedAccess(bool allowed) : ITenantMembershipDirectoryReader, ITenantActivity,
        IPermissionAuthorizer, ITenantDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult<TenantMembershipView?>(allowed
            ? new TenantMembershipView(userId, Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture))
            : null);

        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));

        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(allowed);

        public ValueTask<TenantView?> GetAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantView?>(allowed ? new TenantView(tenantId, "Acme", "acme") : null);

        public ValueTask<Page<TenantView>> ListAsync(int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantView>([], null));
    }
}

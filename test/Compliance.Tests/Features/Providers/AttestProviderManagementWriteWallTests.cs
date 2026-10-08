using Bdgrz.Compliance.Features.Providers;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class AttestProviderManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldDenyProviderAuthoringGivenActualAttestHistoryAndOrdinaryOrganizationGrant(bool revoked, bool mcp)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: revoked);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordProvider(Tenant, new ProviderContent("Supplier", "Supplier")),
            Context(mcp), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ProviderRegister(Tenant));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(retained.Get(result.Value?.ProviderId ?? Uuid.Empty));
    }

    [Fact]
    public async Task ShouldPreserveProviderReadsAndPreviewButDenyRevisionGivenHistoricalAttestAssignment()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var recorded = await bus.DispatchAsync(new RecordProvider(Tenant, new ProviderContent("Supplier", "Supplier")),
            Context(), CancellationToken.None);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        await ProjectProvidersAsync(scope.ServiceProvider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);

        // Act
        var revision = await bus.DispatchAsync(new ReviseProvider(Tenant, recorded.Value!.ProviderId, 1,
            new ProviderContent("Changed", "Supplier")), Context(), CancellationToken.None);
        var read = await bus.DispatchAsync(new GetProvider(Tenant, recorded.Value.ProviderId), Context(), CancellationToken.None);
        var preview = await bus.DispatchAsync(new PreviewProviderChange(Tenant, recorded.Value.ProviderId, 1,
            "renewal", new DateOnly(2027, 1, 1), "Review renewal"), Context(), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ProviderRegister(Tenant));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, revision.Error?.Kind);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.Equal(1, retained.Get(recorded.Value.ProviderId)!.Revision);
        Assert.Equal("Supplier", retained.Get(recorded.Value.ProviderId)!.Content.Name);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPermitProviderAuthoringGivenNoSameClientActualAttestHistory(string practice, bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(), User,
            practice: practice);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordProvider(Tenant, new ProviderContent("Supplier", "Supplier")), Context(), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new ProviderRegister(Tenant));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Supplier", retained.Get(result.Value!.ProviderId)!.Content.Name);
    }

    static RequestDispatchContext Context(bool mcp = false) => new(ProgramManagementServices.Actor(User),
        mcp ? new McpInvocation("synthetic.provider") : new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"));

    static async Task ProjectProvidersAsync(IServiceProvider services)
    {
        var events = services.GetRequiredService<IDomainEventReader>();
        var projection = services.GetRequiredService<IProviderProjection>();
        var identity = new CheckpointIdentity("ProviderRegisterV1", EventStreamPattern.ForPattern(Tenant.ToString(), ProviderRegister.Area));
        await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
        var cursor = ProjectionCheckpoint.Start.Cursor;
        await foreach (var record in events.ReadAsync(identity.Pattern, cursor, CancellationToken.None))
        {
            await projection.ApplyAsync(record.Event, CancellationToken.None);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }

    static ServiceProvider Compose()
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(true)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(User, Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

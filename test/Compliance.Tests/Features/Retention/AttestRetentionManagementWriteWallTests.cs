using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Retention;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class AttestRetentionManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public async Task ShouldDenyRetentionManagementButPreserveReadsGivenActualHistoricalAttestAssignment(string operation)
    {
        // Arrange
        await using var provider = Compose();
        var id = await SeedEvidenceAsync(provider);
        var hold = Uuid.CreateVersion4();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        if (operation == "release")
        {
            var initial = await bus.DispatchAsync(new PlaceArtifactLegalHold(Tenant, "evidence_artifact", id,
                Sha, hold, 0, "Preserve original source"), Http(), CancellationToken.None);
            Assert.True(initial.IsSuccess, initial.Error?.Message);
        }
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetention(Tenant, "evidence_artifact", id));
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);

        // Act
        var result = operation switch
        {
            "basis" => await bus.DispatchAsync(new RecordArtifactRetentionBasis(Tenant, "evidence_artifact",
                id, Sha, 0, "Native evidence period"), Http(), CancellationToken.None),
            "place" => await bus.DispatchAsync(new PlaceArtifactLegalHold(Tenant, "evidence_artifact", id,
                Sha, hold, 0, "Preserve original source"), Http(), CancellationToken.None),
            _ => await bus.DispatchAsync(new ReleaseArtifactLegalHold(Tenant, "evidence_artifact", id,
                Sha, hold, before.Revision, "Client hold release"), Http(), CancellationToken.None)
        };
        var read = await bus.DispatchAsync(new GetArtifactRetention(Tenant, "evidence_artifact", id),
            new RequestDispatchContext(ProgramManagementServices.Actor(User), new McpInvocation("synthetic.retention.read")),
            CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetention(Tenant, "evidence_artifact", id));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(before.Revision, retained.Revision);
        Assert.False(read.Value!.DispositionAllowed);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldRetainNativeRetentionBasisGivenHistoryOutsideTheClientAttestWall(string practice, bool sameClient)
    {
        // Arrange
        await using var provider = Compose();
        var id = await SeedEvidenceAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameClient ? Tenant : Uuid.CreateVersion4(), User,
            practice: practice);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordArtifactRetentionBasis(Tenant, "evidence_artifact", id, Sha, 0, "Native evidence period"),
            Http(), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetention(Tenant, "evidence_artifact", id));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, retained.Revision);
    }

    [Theory]
    [InlineData("grant")]
    [InlineData("direct")]
    [InlineData("mcp")]
    public async Task ShouldPreserveOrdinaryAuthorityAndPersonalTransportGivenNoAttestHistory(string refusal)
    {
        // Arrange
        await using var provider = Compose(allowed: refusal != "grant");
        var id = await SeedEvidenceAsync(provider);
        await using var scope = provider.CreateAsyncScope();
        var context = refusal == "grant" ? Http() : new RequestDispatchContext(ProgramManagementServices.Actor(User),
            refusal == "mcp" ? new McpInvocation("synthetic.retention.decision") : new DirectInvocation());

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new RecordArtifactRetentionBasis(Tenant, "evidence_artifact", id, Sha, 0, "Native period"),
            context, CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider,
            new ArtifactRetention(Tenant, "evidence_artifact", id));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(0, retained.Revision);
    }

    static RequestDispatchContext Http() => new(ProgramManagementServices.Actor(User),
        new HttpInvocation("POST", "/synthetic/retention", "/synthetic/retention", "synthetic"));

    static async Task<Uuid> SeedEvidenceAsync(IServiceProvider provider)
    {
        var id = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new EvidenceArtifact(Tenant, id), artifact =>
        {
            var registered = artifact.Register(new("Payroll access", null, "export", "manual", DateTimeOffset.UtcNow,
                new(2019, 1, 1), new(2019, 12, 31), "confidential"), Sha, 100,
                ActorReference.ForMember(RbacIds.Member(Tenant, User), "Collector"), DateTimeOffset.UtcNow);
            Assert.True(registered.IsSuccess);
            return registered;
        });
        return id;
    }

    static ServiceProvider Compose(bool allowed = true)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build(), developerAuthentication: true);
        var events = new InMemoryEventStore();
        services.AddSingleton<IEventStore>(events);
        services.AddSingleton<IDomainEventReader>(events);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(allowed));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenant == Tenant.ToString() && userId == User
                ? new TenantMembershipView(User, Tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

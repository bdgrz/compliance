using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AttestAccessReviewWriteWallTests
{
    [Theory]
    [InlineData("client_personnel", false)]
    [InlineData("client_personnel", true)]
    [InlineData("guest", false)]
    [InlineData("guest", true)]
    public async Task ShouldDenyPopulationManagementGivenActualAttestHistoryAndRelinkedCurrentMembership(string affiliation, bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(affiliation);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.ManagerUserId);

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open(), mcp);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
        Assert.Contains("Attest", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldDenyPopulationManagementGivenRevokedActualAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.ManagerUserId, revoked: true);

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task ShouldRetainPopulationAndAllowReadGivenOrdinaryClientManagementAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());
        var read = await fixture.SendAsync(fixture.Source.ManagerUserId,
            new GetAccessPopulation(fixture.Source.TenantId, result.Value!.PopulationId));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal(result.Value.PopulationId, read.Value!.PopulationId);
    }

    [Fact]
    public async Task ShouldPermitClientManagementGivenSameCanonicalUserAttestHistoryInDifferentTenant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, Uuid.CreateVersion4(), fixture.Source.ManagerUserId);

        // Act
        var result = await fixture.SendAsync(fixture.Source.ManagerUserId, fixture.Open());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldDenyAssignedAccessDecisionGivenActualAttestHistoryDespitePersonalHttpAndReviewAssignment(bool bulk)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var (populationId, _) = await fixture.Source.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.Source.ClassifyStandardAsync(populationId);
        var launched = await fixture.Source.LaunchAsync(populationId);
        var campaign = await fixture.Source.CampaignAsync(launched.CampaignId);
        var itemId = AccessReviewCampaignTests.ItemId(campaign, "ada", "deploy");
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Source.TenantId, fixture.Source.ReviewerUserId);
        var before = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new GetAccessReviewCampaign(fixture.Source.TenantId, launched.CampaignId));
        var preview = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new PreviewBulkAccessDecision(fixture.Source.TenantId, launched.CampaignId, [itemId], "keep"));

        // Act
        var error = bulk
            ? (await fixture.SendAsync(fixture.Source.ReviewerUserId, new RecordBulkAccessDecision(fixture.Source.TenantId,
                launched.CampaignId, [itemId], "keep", "Reviewed", preview.Value!.PreviewToken))).Error
            : (await fixture.SendAsync(fixture.Source.ReviewerUserId, new RecordAccessDecision(fixture.Source.TenantId,
                launched.CampaignId, itemId, 1, "keep", "Reviewed"))).Error;
        var after = await fixture.SendAsync(fixture.Source.ReviewerUserId,
            new GetAccessReviewCampaign(fixture.Source.TenantId, launched.CampaignId));

        // Assert
        Assert.True(before.IsSuccess);
        Assert.True(preview.IsSuccess);
        Assert.NotNull(error);
        Assert.Equal(RequestErrorKind.Forbidden, error.Kind);
        Assert.True(after.IsSuccess);
        Assert.Equal(before.Value!.Revision, after.Value!.Revision);
        Assert.All(after.Value.Items, item => Assert.Equal("unresolved", item.Status));
    }

    sealed class Fixture(AccessReviewFixture source, ServiceProvider provider) : IAsyncDisposable
    {
        public AccessReviewFixture Source { get; } = source;
        public ServiceProvider Provider { get; } = provider;

        public static async Task<Fixture> CreateAsync(string affiliation = "client_personnel")
        {
            var source = await AccessReviewFixture.CreateAsync();
            var events = source.Provider.GetRequiredService<IEventStore>();
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build();
            services.AddCompliance(configuration, developerAuthentication: true);
            services.AddSingleton<IEventStore>(events);
            services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(source.Permissions);
            services.AddSingleton<IAccessReviewSources>(source.Sources);
            services.AddSingleton<IApplicationDirectoryReader>(source.Applications);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            services.AddSingleton<ITenantMembershipDirectoryReader>(new MembershipDirectory(source.TenantId, affiliation));
            return new Fixture(source, services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }));
        }

        public OpenAccessPopulation Open() => new(Source.TenantId, Source.ApplicationId, Source.InstanceId, 1,
            AccessReviewFixture.Observed, "Synthetic observed export");

        public async Task<Result<T>> SendAsync<T>(Uuid userId, IRequest<T> request, bool mcp = false)
        {
            await using var scope = Provider.CreateAsyncScope();
            var context = new RequestDispatchContext(ProgramManagementServices.Actor(userId),
                mcp ? new McpInvocation("synthetic.access-review") : new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"));
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, context, CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await Source.DisposeAsync();
        }
    }

    sealed class MembershipDirectory(Uuid tenantId, string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenant == tenantId.ToString() ? new TenantMembershipView(userId, tenantId, affiliation) : null);
        public ValueTask<bool> IsMemberAsync(string tenant, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenant == tenantId.ToString());
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenant, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

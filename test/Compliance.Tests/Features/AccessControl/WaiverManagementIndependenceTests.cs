using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class WaiverManagementIndependenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainNoWaiverGivenHistoricalAttestRequester(bool closed)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.Requester, closed);

        // Act
        var result = await fixture.RecordAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(0, await fixture.WaiverEventsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreservePendingWaiverAndReadGivenHistoricalAttestIndependentApprover(bool closed)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.Approver, closed);

        // Act
        var result = await fixture.SendAsync(new ApproveSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId), fixture.Approver);
        var read = await fixture.SendAsync(new GetSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId), fixture.Approver, mcp: true);
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId));

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("pending", read.Value.Status);
        Assert.Null(source.ApprovedAt);
        Assert.Equal(1UL, source.CommittedStreamPosition);
        Assert.Equal(1, await fixture.WaiverEventsAsync());
    }

    [Theory]
    [InlineData("none", false)]
    [InlineData("advisory", false)]
    [InlineData("advisory", true)]
    [InlineData("other_client", false)]
    [InlineData("other_client", true)]
    public async Task ShouldRetainAttributableIndependentWaiverApprovalGivenOrdinaryEligibleClientActors(string history, bool approverHistory)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        if (history != "none")
            await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider,
                history == "other_client" ? Uuid.CreateVersion4() : fixture.Tenant,
                approverHistory ? fixture.Approver : fixture.Requester, true,
                history == "advisory" ? "advisory" : "attest");

        // Act
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var approved = await fixture.SendAsync(new ApproveSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId), fixture.Approver);
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId));

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(approved.Value.Active);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Requester), source.RequesterMemberId);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver), source.ApproverMemberId);
        Assert.Equal(fixture.Scope, source.Scope);
        Assert.Equal(2UL, source.CommittedStreamPosition);
    }

    [Theory]
    [InlineData("requester")]
    [InlineData("beneficiary")]
    public async Task ShouldPreserveIndependentApprovalRequirementGivenOrdinaryManagementGrant(string actor)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);

        // Act
        var result = await fixture.SendAsync(new ApproveSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId),
            actor == "requester" ? fixture.Requester : fixture.Beneficiary);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("different Org Admin", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(1, await fixture.WaiverEventsAsync());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("suspended")]
    public async Task ShouldPreserveCurrentBeneficiaryRequirementGivenEligibleRequester(string status)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Memberships.BeneficiaryStatus = status;

        // Act
        var result = await fixture.RecordAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, await fixture.WaiverEventsAsync());
    }

    [Fact]
    public async Task ShouldPreserveOrdinaryGrantRequirementGivenIndependentActorsWithoutAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        fixture.Permissions.Allowed = false;

        // Act
        var record = await fixture.RecordAsync();
        var approve = await fixture.SendAsync(new ApproveSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId), fixture.Approver);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, record.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, approve.Error?.Kind);
        Assert.Equal(1, await fixture.WaiverEventsAsync());
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid Requester { get; } = Uuid.CreateVersion4();
        public Uuid Approver { get; } = Uuid.CreateVersion4();
        public Uuid Beneficiary { get; } = Uuid.CreateVersion4();
        Uuid Boundary { get; } = Uuid.CreateVersion4();
        Uuid Version { get; } = Uuid.CreateVersion4();
        public SeparationOfDutiesWaiverScope Scope => new("boundary", Boundary, Version, 1, "review");
        public ServiceProvider Provider { get; private set; } = null!;
        public Permissions Permissions { get; } = new();
        public Memberships Memberships { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
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
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            fixture.Memberships = new Memberships(fixture.Tenant, fixture.Beneficiary,
                new HashSet<Uuid> { fixture.Requester, fixture.Approver, fixture.Beneficiary });
            services.AddSingleton<ITenantMembershipDirectoryReader>(fixture.Memberships);
            services.AddSingleton<IPermissionAuthorizer>(fixture.Permissions);
            fixture.Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            await ProgramManagementServices.SeedAsync(fixture.Provider, new SystemBoundary(fixture.Tenant, fixture.Boundary), boundary =>
                boundary.Create(Uuid.CreateVersion4(), fixture.Version,
                    new BoundaryContent("Scoped management record", "readiness", ["security"], []),
                    RbacIds.Member(fixture.Tenant, fixture.Beneficiary), "Client author", DateTimeOffset.UtcNow));
            return fixture;
        }

        public Task<Result<SeparationOfDutiesWaiverView>> RecordAsync() => SendAsync(new RecordSeparationOfDutiesWaiver(
            Tenant, Scope, Beneficiary, "Small operational team exception", DateTimeOffset.UtcNow.AddDays(7)), Requester);

        public async Task<Result<T>> SendAsync<T>(IRequest<T> request, Uuid user, bool mcp = false)
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
                new RequestDispatchContext(ProgramManagementServices.Actor(user), mcp ? new McpInvocation("synthetic.waiver.read")
                    : new HttpInvocation("POST", "/synthetic/waiver", "/synthetic/waiver", "synthetic")), CancellationToken.None);
        }

        public async Task<int> WaiverEventsAsync()
        {
            var count = 0;
            await foreach (var record in Provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern(Tenant.ToString(), "separation-of-duties-waivers"), EventCursor.Start, CancellationToken.None))
                count++;
            return count;
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class Memberships(Uuid tenant, Uuid beneficiary, IReadOnlySet<Uuid> users) : ITenantMembershipDirectoryReader
    {
        public string BeneficiaryStatus { get; set; } = "active";
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && users.Contains(userId) &&
                (userId != beneficiary || BeneficiaryStatus != "missing")
                ? new TenantMembershipView(userId, tenant, "client_personnel", IsSuspended: userId == beneficiary && BeneficiaryStatus == "suspended") : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) => await GetAsync(tenantId, userId, ct) is not null;
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

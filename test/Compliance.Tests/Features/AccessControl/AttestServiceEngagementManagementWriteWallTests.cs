using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AttestServiceEngagementManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid OtherTenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("create", "http")]
    [InlineData("create", "mcp")]
    [InlineData("amend", "http")]
    [InlineData("amend", "mcp")]
    [InlineData("close", "http")]
    [InlineData("close", "mcp")]
    [InlineData("evaluate", "http")]
    [InlineData("evaluate", "mcp")]
    [InlineData("propose", "http")]
    [InlineData("propose", "mcp")]
    [InlineData("withdraw", "http")]
    [InlineData("withdraw", "mcp")]
    [InlineData("record_service", "http")]
    [InlineData("record_service", "mcp")]
    [InlineData("revoke", "http")]
    [InlineData("revoke", "mcp")]
    [InlineData("acknowledge", "http")]
    [InlineData("acknowledge", "mcp")]
    public async Task ShouldDenyClientIndependenceWriteGivenRetainedSameTenantAttestHistoryWithoutAppending(
        string operation, string transport)
    {
        // Arrange
        await using var provider = Compose();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked: true);
        var before = await TenantEventCountAsync(provider, Tenant);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var error = await DispatchAsync(scope.ServiceProvider, operation, transport);
        var after = await TenantEventCountAsync(provider, Tenant);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains("Attest", error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldAllowClientIndependenceWriteGivenOnlyAdvisoryOrOtherTenantAttestHistory(
        string practice, bool historyBelongsToTenant)
    {
        // Arrange
        await using var provider = Compose();
        var historyTenant = historyBelongsToTenant ? Tenant : OtherTenant;
        var prior = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(provider, historyTenant, User,
            revoked: true, practice);
        await ProgramManagementServices.SeedAsync(provider, new FirmStaffDirectory(), directory =>
        {
            var staff = directory.Register(Uuid.CreateVersion4(), prior.StaffMemberId, User, prior.Practice,
                "Synthetic staff directory source", 0,
                ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic directory operator"), Now);
            Assert.True(staff.IsSuccess, staff.Error?.Message);
            return Result.Success;
        });
        var ledger = await ProgramManagementServices.HydrateAsync(provider, new IndependenceLedger(Tenant));
        await using var scope = provider.CreateAsyncScope();
        var engagementId = Uuid.CreateVersion4();
        var request = new CreateServiceEngagement(Tenant, engagementId, ledger.Sequence,
            new ServiceEngagementDraftContent(prior.Practice, "Synthetic service scope", new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), prior.StaffMemberId));

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request,
            Context("create", "http"), CancellationToken.None);
        var retained = await ProgramManagementServices.HydrateAsync(provider, new IndependenceLedger(Tenant));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ledger.Sequence + 1, retained.Sequence);
        Assert.Equal("Synthetic service scope", Assert.Single(retained.Engagements,
            engagement => engagement.EngagementId == engagementId).Content.Scope);
    }

    [Fact]
    public async Task ShouldPreserveClientManagementGrantRequirementGivenNoAttestHistory()
    {
        // Arrange
        await using var provider = Compose(permitted: false);
        await using var scope = provider.CreateAsyncScope();
        var before = await TenantEventCountAsync(provider, Tenant);

        // Act
        var error = await DispatchAsync(scope.ServiceProvider, "create", "http");
        var after = await TenantEventCountAsync(provider, Tenant);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.DoesNotContain("Attest", error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task ShouldPreservePersonalHttpGuardGivenMcpManagementAcknowledgementWithoutAttestHistory()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var before = await TenantEventCountAsync(provider, Tenant);

        // Act
        var error = await DispatchAsync(scope.ServiceProvider, "acknowledge", "mcp");
        var after = await TenantEventCountAsync(provider, Tenant);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains("personal HTTP acknowledgement", error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    [Fact]
    public void ShouldMarkOnlyClientOwnedIndependenceMutationsForAttestManagementWallGivenRequestTypes()
    {
        // Arrange
        var requests = new[]
        {
            typeof(CreateServiceEngagement),
            typeof(AmendServiceEngagement),
            typeof(CloseServiceEngagement),
            typeof(EvaluateClientIndependence),
            typeof(ProposeServiceEngagementStaff),
            typeof(WithdrawServiceEngagementStaffProposal),
            typeof(RecordNonattestService),
            typeof(RevokeServiceEngagementActualStaff),
            typeof(AcknowledgeEngagementManagement),
        };

        // Act
        var marked = requests.Where(type => typeof(IClientManagementMutationRequest).IsAssignableFrom(type)).ToArray();

        // Assert
        Assert.Equal(requests.OrderBy(type => type.FullName), marked.OrderBy(type => type.FullName));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(AcceptServiceEngagement)));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(ReviseIndependenceRules)));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(GetServiceEngagement)));
    }

    static async Task<RequestError?> DispatchAsync(IServiceProvider services, string operation, string transport)
    {
        var bus = services.GetRequiredService<IRequestBus>();
        var context = Context(operation, transport);
        return operation switch
        {
            "create" => (await bus.DispatchAsync(new CreateServiceEngagement(Tenant, Uuid.CreateVersion4(), 0,
                Content), context, CancellationToken.None)).Error,
            "amend" => (await bus.DispatchAsync(new AmendServiceEngagement(Tenant, Uuid.CreateVersion4(), 0,
                Content), context, CancellationToken.None)).Error,
            "close" => (await bus.DispatchAsync(new CloseServiceEngagement(Tenant, Uuid.CreateVersion4(), 0,
                "Synthetic close reason"), context, CancellationToken.None)).Error,
            "evaluate" => (await bus.DispatchAsync(new EvaluateClientIndependence(Tenant, Uuid.CreateVersion4(), 0,
                1, new DateOnly(2026, 1, 1)), context, CancellationToken.None)).Error,
            "propose" => (await bus.DispatchAsync(new ProposeServiceEngagementStaff(Tenant, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 0), context, CancellationToken.None)).Error,
            "withdraw" => (await bus.DispatchAsync(new WithdrawServiceEngagementStaffProposal(Tenant,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, "Synthetic withdrawal reason"), context,
                CancellationToken.None)).Error,
            "record_service" => (await bus.DispatchAsync(new RecordNonattestService(Tenant, Uuid.CreateVersion4(), 0,
                new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2026, 1, 1), null,
                    [], false, "Synthetic service source")), context, CancellationToken.None)).Error,
            "revoke" => (await bus.DispatchAsync(new RevokeServiceEngagementActualStaff(Tenant, Uuid.CreateVersion4(),
                Uuid.CreateVersion4(), 0, "Synthetic revocation reason"), context, CancellationToken.None)).Error,
            "acknowledge" => (await bus.DispatchAsync(new AcknowledgeEngagementManagement(Tenant,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, 1, [], "I retain management responsibility"), context,
                CancellationToken.None)).Error,
            _ => throw new InvalidOperationException(operation),
        };
    }

    static ServiceEngagementDraftContent Content => new("advisory", "Synthetic scope", new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31), Uuid.CreateVersion4());

    static RequestDispatchContext Context(string operation, string transport) => new(
        ProgramManagementServices.Actor(User), transport == "http"
            ? new HttpInvocation("POST", $"/synthetic/{operation}", $"/synthetic/{operation}", operation)
            : new McpInvocation($"bdgrz.service_engagement.{operation}"));

    static async Task<long> TenantEventCountAsync(IServiceProvider provider, Uuid tenant)
    {
        long count = 0;
        var events = provider.GetRequiredService<IDomainEventReader>();
        await foreach (var _ in events.ReadAsync(EventStreamPattern.ForPattern(tenant.ToString()), EventCursor.Start,
            CancellationToken.None))
            count++;
        return count;
    }

    static ServiceProvider Compose(bool permitted = true)
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
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(permitted));
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(permitted)));
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

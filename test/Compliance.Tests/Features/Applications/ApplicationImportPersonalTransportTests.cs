using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportPersonalTransportTests
{
    [Theory]
    [InlineData("correlate", false)]
    [InlineData("correlate", true)]
    [InlineData("accept", false)]
    [InlineData("accept", true)]
    [InlineData("cancel", false)]
    [InlineData("cancel", true)]
    public async Task ShouldRetainExactImportSourceGivenNonHttpPersonalDecision(string operation, bool mcp)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batch = await fixture.StageAsync();
        if (operation == "accept")
            Assert.True((await fixture.SendAsync(fixture.Correlate(batch), Fixture.Http)).IsSuccess);
        var before = await fixture.LedgerAsync();
        var request = fixture.Decision(operation, batch);

        // Act
        var result = mcp
            ? await fixture.SendAsync(request, new McpInvocation("synthetic.import.decision"))
            : await fixture.Bus.SendAsync(request, fixture.Actor);
        var retained = await fixture.LedgerAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("HTTP", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(before.GetRevision(batch), retained.GetRevision(batch));
        Assert.Equal(before.GetState(batch), retained.GetState(batch));
        Assert.Null(retained.GetFrozenPlan(batch.Id));
        Assert.Null(retained.GetCanceledRevision(batch.Id));
    }

    [Theory]
    [InlineData("correlate")]
    [InlineData("accept")]
    [InlineData("cancel")]
    public async Task ShouldRetainAttributedImportDecisionGivenNativePersonalHttp(string operation)
    {
        // Arrange
        await using var fixture = new Fixture();
        var batch = await fixture.StageAsync();
        if (operation == "accept")
            Assert.True((await fixture.SendAsync(fixture.Correlate(batch), Fixture.Http)).IsSuccess);
        var before = await fixture.LedgerAsync();

        // Act
        var result = await fixture.SendAsync(fixture.Decision(operation, batch), Fixture.Http);
        var retained = await fixture.LedgerAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retained.CommittedStreamPosition > before.CommittedStreamPosition);
        Assert.Equal(operation == "accept" ? "accepting" : operation == "cancel" ? "canceled" : "preview_ready", retained.GetState(batch));
        if (operation == "accept")
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User), retained.GetFrozenPlan(batch.Id)!.Start.ApproverMemberId);
        if (operation == "cancel")
            Assert.True(retained.IsCancellationDurable(batch.Id));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public static HttpInvocation Http => new("POST", "/synthetic/import", "/synthetic/import", "synthetic");
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public System.Security.Claims.ClaimsPrincipal Actor => ProgramManagementServices.Actor(User);
        public IRequestBus Bus { get; }
        readonly IAggregateReader _reader;

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<ITenantActivity>(new Activity());
            services.AddSingleton<IPermissionAuthorizer>(new Permissions());
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(Tenant, User));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            _reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public async Task<ImportBatch> StageAsync()
        {
            var staged = await Bus.SendAsync(new StageApplicationImport(Tenant, Uuid.CreateVersion4(),
                "manual", "transport", "partial", [new("app", "Application", "Purpose", null)]), Actor);
            Assert.True(staged.IsSuccess, staged.Error?.Message);
            return await _reader.HydrateAsync(new ImportBatch(Tenant, staged.Value!.BatchId));
        }

        public CorrelateApplicationImportRow Correlate(ImportBatch batch) => new(Tenant, batch.Id,
            Assert.Single(batch.GetRows()).RowId, 1, "create_new", null, null, "Reviewed source identity");
        public IRequest Decision(string operation, ImportBatch batch) => operation switch
        {
            "correlate" => Correlate(batch),
            "accept" => new AcceptApplicationImport(Tenant, batch.Id, 2),
            _ => new CancelApplicationImport(Tenant, batch.Id, 1, "Personal cancellation")
        };
        public ValueTask<Result> SendAsync(IRequest request, RequestInvocation invocation) =>
            Bus.DispatchAsync(request, new RequestDispatchContext(Actor, invocation), CancellationToken.None);
        public ValueTask<ApplicationImportLedger> LedgerAsync() =>
            _reader.HydrateAsync(new ApplicationImportLedger(Tenant, "manual", "transport"));
        public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
    }

    sealed class Activity : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(permission == RbacPermissions.ApplicationInventoryManage);
    }

    sealed class Memberships(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && userId == user
                ? new TenantMembershipView(user, tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(tenantId == tenant.ToString() && userId == user);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

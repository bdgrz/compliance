using System.Security.Claims;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreviewRetainedSourceOmissionGivenProductionCompositionAndCurrentImportAuthority(bool stageOnly)
    {
        // Arrange
        await using var fixture = new Fixture();
        var (batchId, originalBatchId, applicationId) = await fixture.SeedAsync();
        fixture.Permissions.StageOnly = stageOnly;

        // Act
        var result = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(fixture.Tenant, batchId), fixture.Actor);
        var target = await fixture.Reader.HydrateApplicationAsync(fixture.Tenant, applicationId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var missing = Assert.Single(result.Value!.Items);
        Assert.Equal("omitted", missing.SourceRecordId);
        Assert.Equal(originalBatchId, missing.LastObservedBatchId);
        Assert.Equal(applicationId, missing.ApplicationId);
        Assert.Equal("missing_from_source", missing.MatchState);
        Assert.Contains("missing_source_retirement_unavailable", missing.AcceptanceBlockers);
        Assert.Contains("retirement_impact_unavailable", missing.AcceptanceBlockers);
        Assert.True(target.IsCreated);
        Assert.False(target.IsRetired);
        Assert.Equal(1, target.Revision);
    }

    [Theory]
    [InlineData("nonmember", RequestErrorKind.NotFound)]
    [InlineData("foreign_tenant", RequestErrorKind.NotFound)]
    [InlineData("suspended", RequestErrorKind.NotFound)]
    [InlineData("deprovisioned", RequestErrorKind.NotFound)]
    [InlineData("firm_staff", RequestErrorKind.Forbidden)]
    [InlineData("missing_grant", RequestErrorKind.Forbidden)]
    public async Task ShouldDenyMissingPreviewGivenCurrentOwningImportAuthorityLost(string denial, RequestErrorKind expected)
    {
        // Arrange
        await using var fixture = new Fixture();
        var (batchId, _, _) = await fixture.SeedAsync();
        fixture.Memberships.State = denial;
        fixture.Permissions.Allowed = denial != "missing_grant";
        var tenant = denial == "foreign_tenant" ? Uuid.CreateVersion4() : fixture.Tenant;

        // Act
        var result = await fixture.Bus.SendAsync(new PreviewMissingApplicationImportRows(tenant, batchId), fixture.Actor);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error?.Kind);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public ClaimsPrincipal Actor => new(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));
        public Permissions Permissions { get; } = new();
        public MembershipDirectory Memberships { get; }
        public IRequestBus Bus { get; }
        public IAggregateReader Reader { get; }

        public Fixture()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build();
            services.AddCompliance(configuration, developerAuthentication: true);
            services.AddSingleton<IEventStore>(new InMemoryEventStore());
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(Permissions);
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            Memberships = new MembershipDirectory(Tenant, User);
            services.AddSingleton<ITenantMembershipDirectoryReader>(Memberships);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
            Reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public async Task<(Uuid BatchId, Uuid OriginalBatchId, Uuid ApplicationId)> SeedAsync()
        {
            var original = await Bus.SendAsync(new StageApplicationImport(Tenant, Uuid.CreateVersion4(),
                "manual", "applications", "partial", [new("omitted", "Observed application", "Purpose", null)]), Actor);
            Assert.True(original.IsSuccess, original.Error?.Message);
            var originalBatch = await Reader.HydrateAsync(new ImportBatch(Tenant, original.Value!.BatchId));
            var row = Assert.Single(originalBatch.GetRows());
            Assert.True((await Bus.SendAsync(new CorrelateApplicationImportRow(Tenant, originalBatch.Id,
                row.RowId, 1, "create_new", null, null, "New source identity"), Actor)).IsSuccess);
            Assert.True((await Bus.SendAsync(new AcceptApplicationImport(Tenant, originalBatch.Id, 2), Actor)).IsSuccess);
            Assert.True((await Bus.SendAsync(new ExecuteApplicationImport(Tenant, originalBatch.Id), RequestActor.System)).IsSuccess);
            var ledger = await Reader.HydrateAsync(new ApplicationImportLedger(Tenant, "manual", "applications"));
            Assert.Equal("committed", ledger.GetState(originalBatch));
            var applicationId = Assert.Single(ledger.GetFrozenPlan(originalBatch.Id)!.Rows).ApplicationId;
            var staged = await Bus.SendAsync(new StageApplicationImport(Tenant, Uuid.CreateVersion4(),
                "manual", "applications", "declared_complete", [new("present", "Present application", "Purpose", null)]), Actor);
            Assert.True(staged.IsSuccess, staged.Error?.Message);
            var batch = await Reader.HydrateAsync(new ImportBatch(Tenant, staged.Value!.BatchId));
            var directory = _scope.ServiceProvider.GetRequiredService<IApplicationImportDirectoryProjection>();
            var identity = new CheckpointIdentity("ApplicationImportDirectoryV1", EventStreamPattern.ForPattern(Tenant.ToString(), "application_imports"));
            await using var projection = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
            await directory.ApplyAsync(Assert.IsType<ApplicationImportStaged>(batch.SourceObservation));
            await projection.CommitAsync(ProjectionCheckpoint.Start);
            return (batch.Id, originalBatch.Id, applicationId);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public bool StageOnly { get; set; }
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(Allowed &&
            (permission == RbacPermissions.ApplicationImportStage ||
             !StageOnly && permission == RbacPermissions.ApplicationInventoryManage));
    }

    sealed class MembershipDirectory(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public string State { get; set; } = "active";
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(State != "nonmember" && tenantId == tenant.ToString() && userId == user
                ? new TenantMembershipView(userId, tenant, State == "firm_staff" ? "firm_staff" : "client_personnel",
                    IsSuspended: State == "suspended", IsDeprovisioned: State == "deprovisioned") : null);
        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            await GetAsync(tenantId, userId, ct) is { IsSuspended: false, IsDeprovisioned: false };
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }

    sealed class ActiveTenant : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
}

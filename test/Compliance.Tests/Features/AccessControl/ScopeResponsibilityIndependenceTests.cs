using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Snapshots;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ScopeResponsibilityIndependenceTests
{
    [Theory]
    [InlineData("assign", false, "client_personnel", "http")]
    [InlineData("revoke", false, "client_personnel", "http")]
    [InlineData("freeze", false, "client_personnel", "http")]
    [InlineData("amend", false, "client_personnel", "http")]
    [InlineData("assign", true, "guest", "mcp")]
    [InlineData("amend", true, "guest", "mcp")]
    public async Task ShouldPreserveRetainedSourcesGivenDeniedHistoricalAttestManagementWrite(string operation,
        bool closed, string affiliation, string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(affiliation);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, closed);
        var before = await fixture.ManagementEventsAsync();

        // Act
        var result = await fixture.ExecuteAsync(operation, transport);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Contains("Attest", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.ManagementEventsAsync());
        var boundary = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SystemBoundary(fixture.Tenant, fixture.DraftBoundary));
        Assert.Null(Assert.Single(boundary.GetResponsibilitySet(fixture.Scope).ReadAssignments()).RevokedAt);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    [InlineData("freeze")]
    [InlineData("amend")]
    public async Task ShouldRetainManagementWriteGivenActualAdvisoryHistoryAndOrdinaryGrants(string operation)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, true, "advisory");
        var before = await fixture.ManagementEventsAsync();

        // Act
        var result = await fixture.ExecuteAsync(operation);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(before + 1, await fixture.ManagementEventsAsync());
    }

    [Fact]
    public async Task ShouldRetainManagementWritesGivenOnlyAnotherClientsActualAttestHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, Uuid.CreateVersion4(), fixture.User, true);

        // Act
        var assignment = await fixture.ExecuteAsync("assign");
        var snapshot = await fixture.ExecuteAsync("freeze");

        // Assert
        Assert.True(assignment.IsSuccess, assignment.Error?.Message);
        Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
    }

    [Fact]
    public async Task ShouldPreserveSnapshotReadVerificationAndManifestGivenHistoricalAttestIdentity()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, true);
        var before = await fixture.ManagementEventsAsync();

        // Act
        var read = await fixture.SendAsync(new GetSnapshot(fixture.Tenant, fixture.Snapshot));
        var verify = await fixture.SendAsync(new VerifyProgramScopeSnapshot(fixture.Tenant, fixture.Snapshot));
        var manifest = await fixture.SendAsync(new RegenerateProgramScopeSnapshotManifest(fixture.Tenant, fixture.Snapshot));
        var responsibilities = await fixture.SendAsync(new ListResponsibilities(fixture.Tenant,
            "boundary", fixture.DraftBoundary, fixture.Scope.VersionId, fixture.Scope.Revision, 1));

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.True(verify.IsSuccess, verify.Error?.Message);
        Assert.True(verify.Value.Verified);
        Assert.True(manifest.IsSuccess, manifest.Error?.Message);
        Assert.True(responsibilities.IsSuccess, responsibilities.Error?.Message);
        Assert.Null(Assert.Single(responsibilities.Value.Assignments).RevokedAt);
        Assert.Equal(before, await fixture.ManagementEventsAsync());
    }

    [Fact]
    public async Task ShouldPreserveOrdinaryGrantDenialGivenNoHistoricalAttestAssignment()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Permissions.Allowed = false;
        var before = await fixture.ManagementEventsAsync();

        // Act
        var assignment = await fixture.ExecuteAsync("assign");
        var snapshot = await fixture.ExecuteAsync("freeze");

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, assignment.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, snapshot.Error?.Kind);
        Assert.Equal(before, await fixture.ManagementEventsAsync());
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        Uuid Assignee { get; } = Uuid.CreateVersion4();
        Uuid Replacement { get; } = Uuid.CreateVersion4();
        Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid DraftBoundary { get; } = Uuid.CreateVersion4();
        Uuid DraftVersion { get; } = Uuid.CreateVersion4();
        Uuid ApprovedBoundary { get; } = Uuid.CreateVersion4();
        Uuid ApprovedVersion { get; } = Uuid.CreateVersion4();
        Uuid Assignment { get; set; }
        public Uuid Snapshot { get; private set; }
        public ServiceProvider Provider { get; private set; } = null!;
        public Permissions Permissions { get; } = new();
        public ResponsibilityScope Scope => new("boundary", DraftBoundary, DraftVersion, 1);

        public static async Task<Fixture> CreateAsync(string affiliation = "client_personnel")
        {
            var fixture = new Fixture();
            var services = new ServiceCollection();
            services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance"
            }).Build(), developerAuthentication: true);
            var store = new InMemoryEventStore();
            services.AddSingleton<IEventStore>(store);
            services.AddSingleton<IDomainEventReader>(store);
            services.AddSingleton<IKvClient>(new InMemoryKvClient());
            services.AddSingleton<IPermissionAuthorizer>(fixture.Permissions);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(fixture.Permissions));
            services.AddSingleton<ITenantActivity, ActiveTenant>();
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(fixture.Tenant, affiliation));
            fixture.Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var now = DateTimeOffset.UtcNow.AddDays(-1);
            var author = RbacIds.Member(fixture.Tenant, Uuid.CreateVersion4());
            var reviewer = RbacIds.Member(fixture.Tenant, Uuid.CreateVersion4());
            await ProgramManagementServices.SeedAsync(fixture.Provider, new ComplianceProgram(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), author, "Client author", now));
                return Result.Success;
            });
            await ProgramManagementServices.SeedAsync(fixture.Provider, new SystemBoundary(fixture.Tenant, fixture.DraftBoundary), boundary =>
                boundary.Create(fixture.Program, fixture.DraftVersion, new BoundaryContent("Draft source", "readiness", ["security"], []), author, "Client author", now));
            await ProgramManagementServices.SeedAsync(fixture.Provider, new SystemBoundary(fixture.Tenant, fixture.ApprovedBoundary), boundary =>
            {
                Assert.True(boundary.Create(fixture.Program, fixture.ApprovedVersion,
                    new BoundaryContent("Approved source", "readiness", ["security"], []), author, "Client author", now).IsSuccess);
                var decision = Uuid.CreateVersion4();
                Assert.Null(boundary.Review(fixture.ApprovedVersion, 1, decision, "accept", "Reviewed", reviewer, "Client reviewer", now));
                Assert.Null(boundary.Approve(fixture.ApprovedVersion, 1, Uuid.CreateVersion4(), decision,
                    new DateOnly(2026, 10, 1), "Approved", "synthetic-impact", reviewer, "Client reviewer", now));
                return Result.Success;
            });
            foreach (var user in new[] { fixture.Assignee, fixture.Replacement })
                await ProgramManagementServices.SeedAsync(fixture.Provider, new Member(fixture.Tenant, user), member => member.Register());
            await fixture.CatchUpAsync();
            var assigned = await fixture.AssignAsync(fixture.Assignee);
            Assert.True(assigned.IsSuccess, assigned.Error?.Message);
            var source = await ProgramManagementServices.HydrateAsync(fixture.Provider, new SystemBoundary(fixture.Tenant, fixture.DraftBoundary));
            fixture.Assignment = Assert.Single(source.GetResponsibilitySet(fixture.Scope).ReadAssignments()).AssignmentId;
            var frozen = await fixture.SendAsync(new FreezeProgramScopeSnapshot(fixture.Tenant, fixture.Program, 1,
                fixture.ApprovedBoundary, fixture.ApprovedVersion));
            Assert.True(frozen.IsSuccess, frozen.Error?.Message);
            fixture.Snapshot = frozen.Value.SnapshotId;
            await fixture.CatchUpAsync();
            return fixture;
        }

        RequestDispatchContext Context(string transport = "http") => new(ProgramManagementServices.Actor(User),
            transport == "mcp" ? new McpInvocation("synthetic.management")
                : new HttpInvocation("POST", "/synthetic/management", "/synthetic/management", "synthetic"));

        public async Task<Result<T>> SendAsync<T>(IRequest<T> request, string transport = "http")
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, Context(transport), CancellationToken.None);
        }

        async Task<Result> SendAsync(IRequest request, string transport = "http")
        {
            await using var scope = Provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, Context(transport), CancellationToken.None);
        }

        async Task<Result> OutcomeAsync<T>(IRequest<T> request, string transport)
        {
            var result = await SendAsync(request, transport);
            return result.IsSuccess ? Result.Success : Result.Failure(result.Error!);
        }

        Task<Result> AssignAsync(Uuid assignee, string transport = "http") => SendAsync(new AssignResponsibility(Tenant, assignee,
            ResponsibilityType.ControlOwner, "boundary", DraftBoundary, DraftVersion, 1, DateTimeOffset.UtcNow, null, []), transport);

        public Task<Result> ExecuteAsync(string operation, string transport = "http") => operation switch
        {
            "assign" => AssignAsync(Replacement, transport),
            "revoke" => SendAsync(new RevokeResponsibility(Tenant, "boundary", DraftBoundary, DraftVersion, 1, Assignment, "Reassigned responsibility"), transport),
            "freeze" => OutcomeAsync(new FreezeProgramScopeSnapshot(Tenant, Program, 1, ApprovedBoundary, ApprovedVersion), transport),
            "amend" => OutcomeAsync(new AmendProgramScopeSnapshot(Tenant, Snapshot, Program, 1, ApprovedBoundary, ApprovedVersion, "Reviewed amendment"), transport),
            _ => throw new InvalidOperationException(operation)
        };

        public async Task<int> ManagementEventsAsync()
        {
            var count = 0;
            await foreach (var record in Provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern(Tenant.ToString()), EventCursor.Start, CancellationToken.None))
                if (record.Event is ResponsibilityAssigned or ResponsibilityRevoked or SnapshotFrozen)
                    count++;
            return count;
        }

        async Task CatchUpAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var programs = services.GetRequiredService<IProgramDirectoryProjection>();
            var boundaries = services.GetRequiredService<IBoundaryDirectoryProjection>();
            var snapshots = services.GetRequiredService<ISnapshotDirectoryProjection>();
            var responsibilities = services.GetRequiredService<IResponsibilitySetProjection>();
            await ProjectAsync("ProgramDirectory", null, programs, programs.ApplyAsync,
                await services.GetRequiredService<IProgramDirectoryReader>().LoadCheckpointAsync(Tenant));
            await ProjectAsync("BoundaryDirectoryV2", null, boundaries, boundaries.ApplyAsync,
                await services.GetRequiredService<IBoundaryDirectoryReader>().LoadCheckpointAsync(Tenant));
            await ProjectAsync("SnapshotDirectory", "snapshots", snapshots, snapshots.ApplyAsync,
                await services.GetRequiredService<ISnapshotDirectoryReader>().LoadCheckpointAsync(Tenant));
            await ProjectAsync("ResponsibilitySetsV1", null, responsibilities, responsibilities.ApplyAsync,
                await responsibilities.LoadCheckpointAsync(new CheckpointIdentity("ResponsibilitySetsV1",
                    EventStreamPattern.ForPattern(Tenant.ToString()))));
            async Task ProjectAsync(string name, string? area, IProjectionStore store,
                Func<DomainEvent, CancellationToken, ValueTask> apply, ProjectionCheckpoint checkpoint)
            {
                var pattern = area is null ? EventStreamPattern.ForPattern(Tenant.ToString())
                    : EventStreamPattern.ForPattern(Tenant.ToString(), area);
                await using var batch = await store.BeginAsync(new ProjectionBatchContext(new CheckpointIdentity(name, pattern), checkpoint));
                var cursor = checkpoint.Cursor;
                await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(pattern, cursor, CancellationToken.None))
                {
                    await apply(record.Event, CancellationToken.None);
                    cursor = record.NextCursor;
                }
                await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            }
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission, CancellationToken ct = default) => ValueTask.FromResult(Allowed);
    }

    sealed class Memberships(Uuid tenant, string affiliation) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() ? new TenantMembershipView(userId, tenant, affiliation) : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(tenantId == tenant.ToString());
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

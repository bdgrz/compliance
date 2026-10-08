using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Readiness;

public sealed class AdvisoryReadinessNoteIndependenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldDenyAdvisoryNotesButPreserveSharedAssessmentGivenActualAttestHistory(bool closed, bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var baseline = await fixture.NotesAsync(mcp);
        Assert.True(baseline.IsSuccess, baseline.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(baseline.Value!.Items).Body);
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, closed);

        // Act
        var notes = await fixture.NotesAsync(mcp);
        var assessment = await fixture.SendAsync(new GetReadinessAssessment(fixture.Tenant, fixture.Program,
            fixture.Assessment), mcp);
        var gaps = await fixture.SendAsync(new ListReadinessGaps(fixture.Tenant, fixture.Program,
            fixture.Assessment), mcp);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, notes.Error?.Kind);
        Assert.True(assessment.IsSuccess, assessment.Error?.Message);
        Assert.True(gaps.IsSuccess, gaps.Error?.Message);
        Assert.Single(gaps.Value!.Items);
    }

    [Theory]
    [InlineData("advisory", true)]
    [InlineData("attest", false)]
    public async Task ShouldPreserveOrdinaryNoteReadGivenHistoryOutsideTheClientsAttestWall(string practice, bool sameClient)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider,
            sameClient ? fixture.Tenant : Uuid.CreateVersion4(), fixture.User, true, practice);

        // Act
        var notes = await fixture.NotesAsync(true);

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldDenyNoteReadGivenNoOrdinaryProgramReadGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, notes.Error?.Kind);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("suspended")]
    [InlineData("deprovisioned")]
    [InlineData("wrong_user")]
    [InlineData("wrong_tenant")]
    public async Task ShouldPreserveMembershipPrivacyGivenHistoricalAttestUserWithoutExactCurrentMembership(string state)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await AttestAssignmentHistoryFixture.SeedAsync(fixture.Provider, fixture.Tenant, fixture.User, true);
        fixture.Memberships.State = state;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
        Assert.DoesNotContain("Attest", notes.Error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("user")]
    [InlineData("compartment")]
    public async Task ShouldFailClosedGivenInvalidCompartmentEvaluationInputs(string invalid)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();

        // Act
        var allowed = await scope.ServiceProvider.GetRequiredService<ClientCompartmentIndependenceGuard>().CanReadAsync(
            invalid == "tenant" ? Uuid.Empty : fixture.Tenant,
            invalid == "user" ? Uuid.Empty : fixture.User,
            invalid == "compartment" ? (RecordCompartment)999 : RecordCompartment.AdvisoryWorkingNotes);

        // Assert
        Assert.False(allowed);
    }

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Assessment { get; } = Uuid.CreateVersion4();
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
            services.AddSingleton<ITenantActivity>(new ActiveTenant());
            fixture.Memberships = new Memberships(fixture.Tenant, fixture.User);
            services.AddSingleton<ITenantMembershipDirectoryReader>(fixture.Memberships);
            services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(fixture.Permissions));
            fixture.Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var now = DateTimeOffset.UtcNow;
            var member = RbacIds.Member(fixture.Tenant, fixture.User);
            await ProgramManagementServices.SeedAsync(fixture.Provider, new ComplianceProgram(fixture.Tenant, fixture.Program), program =>
            {
                Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null), member, "Client administrator", now));
                return Result.Success;
            });
            var gap = new ReadinessGapView(Uuid.CreateVersion4(), "missing_input", "risk", "risk_reviewed", "Shared management gap", []);
            await ProgramManagementServices.SeedAsync(fixture.Provider, new ReadinessLedger(fixture.Tenant, fixture.Program), ledger =>
            {
                Assert.Null(ledger.Record(0, fixture.Assessment, now, null,
                    new ReadinessEvaluation([], [], [gap], new string('a', 64)), member, "Client runner", now));
                Assert.Null(ledger.Annotate(fixture.Assessment, gap.GapId, 1, Uuid.CreateVersion4(),
                    "Advisor working feedback", member, "Attributed feedback author", now));
                return Result.Success;
            });
            await fixture.ProjectAsync();
            return fixture;
        }

        public Task<Result<Page<ReadinessAnnotationView>>> NotesAsync(bool mcp = false) =>
            SendAsync(new ListReadinessAnnotations(Tenant, Program, Assessment), mcp);

        public async Task<Result<T>> SendAsync<T>(IRequest<T> request, bool mcp = false)
        {
            await using var scope = Provider.CreateAsyncScope();
            var context = new RequestDispatchContext(ProgramManagementServices.Actor(User),
                mcp ? new McpInvocation("synthetic.readiness.read") : new HttpInvocation("GET", "/synthetic/readiness", "/synthetic/readiness", "synthetic"));
            return await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(request, context, CancellationToken.None);
        }

        async Task ProjectAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var projection = services.GetRequiredService<IReadinessDirectoryProjection>();
            var identity = new CheckpointIdentity(FitzReadinessDirectory.ProjectorName,
                EventStreamPattern.ForPattern(Tenant.ToString(), ReadinessLedger.Area));
            await using var batch = await projection.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start));
            var cursor = ProjectionCheckpoint.Start.Cursor;
            await foreach (var record in services.GetRequiredService<IDomainEventReader>().ReadAsync(identity.Pattern, cursor, CancellationToken.None))
            {
                await projection.ApplyAsync(record.Event, CancellationToken.None);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class Permissions : IPermissionAuthorizer
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<bool> IsAllowedAsync(Uuid tenant, Uuid user, Uuid member, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(Allowed && permission == IProgramReadRequest.ReadPermission);
    }

    sealed class Memberships(Uuid tenant, Uuid user) : ITenantMembershipDirectoryReader
    {
        public string State { get; set; } = "active";
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && userId == user && State != "absent"
                ? new TenantMembershipView(State == "wrong_user" ? Uuid.CreateVersion4() : user,
                    State == "wrong_tenant" ? Uuid.CreateVersion4() : tenant, "client_personnel",
                    IsSuspended: State == "suspended", IsDeprovisioned: State == "deprovisioned") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() && userId == user);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

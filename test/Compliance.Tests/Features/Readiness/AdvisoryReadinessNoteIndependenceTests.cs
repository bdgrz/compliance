using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Readiness;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Tenants;
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldAllowAssignedAdvisorToReadAdvisoryNotesGivenNoClientMembershipOrProgramGrant(bool mcp)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync(mcp);

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenAcceptanceBoundToAnotherProgram()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(Uuid.CreateVersion4());
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: binding.Version, boundaryApproval: binding.Approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldAllowAssignedAdvisorReadGivenExactApprovedBoundaryInRequestedProgram()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(fixture.Program);
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: binding.Version, boundaryApproval: binding.Approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync(true);

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unapproved")]
    [InlineData("foreign_tenant")]
    [InlineData("version")]
    [InlineData("revision")]
    [InlineData("approval")]
    public async Task ShouldDenyAssignedAdvisorReadGivenMissingOrMismatchedApprovedBoundarySource(string mismatch)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(fixture.Program,
            seedApproval: mismatch != "unapproved",
            sourceTenantId: mismatch == "foreign_tenant" ? Uuid.CreateVersion4() : null);
        var version = binding.Version;
        var approval = binding.Approval;
        if (mismatch == "missing")
        {
            var missingId = Uuid.CreateVersion4();
            version = version with { BoundaryId = missingId };
            approval = approval with { BoundaryId = missingId };
        }
        else if (mismatch == "version")
        {
            var otherVersion = Uuid.CreateVersion4();
            version = version with { VersionId = otherVersion };
            approval = approval with { VersionId = otherVersion };
        }
        else if (mismatch == "revision")
        {
            version = version with { Revision = 2 };
            approval = approval with { Revision = 2 };
        }
        else if (mismatch == "approval")
            approval = approval with { DecisionId = Uuid.CreateVersion4() };
        // Synthetic acceptance capsules exercise current authoritative source verification.
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: version, boundaryApproval: approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldPreserveAssignedAdvisorReadGivenSuccessorToAcceptedBoundaryVersion()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var binding = await fixture.SeedApprovedBoundaryAsync(fixture.Program);
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory", boundary: binding.Version, boundaryApproval: binding.Approval);
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        await fixture.SeedBoundarySuccessorAsync(binding.Version);

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldPreserveAcceptedAdvisorAccessGivenLaterUnratifiedRuleDraft()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        await fixture.SeedLaterUnratifiedRuleDraftAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.True(notes.IsSuccess, notes.Error?.Message);
        Assert.Equal("Advisor working feedback", Assert.Single(notes.Value!.Items).Body);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenRetainedAttestHistoryForTheSameClient()
    {
        // Arrange
        await using var advisorFixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(advisorFixture.Provider,
            advisorFixture.Tenant, advisorFixture.User, practice: "advisory");
        await advisorFixture.SeedStaffDirectoryAsync(staff);
        advisorFixture.Memberships.State = "absent";
        advisorFixture.Permissions.Allowed = false;
        await using var attestFixture = await Fixture.CreateAsync(advisorFixture.Tenant, advisorFixture.User);
        await AttestAssignmentHistoryFixture.SeedAsync(attestFixture.Provider, advisorFixture.Tenant,
            advisorFixture.User);
        await using var advisorScope = advisorFixture.Provider.CreateAsyncScope();
        await using var attestScope = attestFixture.Provider.CreateAsyncScope();
        var professionalAccess = advisorScope.ServiceProvider.GetRequiredService<ProfessionalAdvisoryReadAccess>();
        var authorizer = new ReadinessAnnotationCompartmentAuthorizer(
            advisorScope.ServiceProvider.GetRequiredService<ITenantMembershipDirectoryReader>(),
            attestScope.ServiceProvider.GetRequiredService<ClientCompartmentIndependenceGuard>(),
            professionalAccess);
        var request = new ListReadinessAnnotations(advisorFixture.Tenant, advisorFixture.Program,
            advisorFixture.Assessment);

        // Act
        var isAssignedAndCurrent = await professionalAccess.CanReadAsync(advisorFixture.Tenant,
            advisorFixture.Program, advisorFixture.User);
        var result = await authorizer.AuthorizeAsync(new RequestContext<ListReadinessAnnotations>(request,
            ProgramManagementServices.Actor(advisorFixture.User)), CancellationToken.None);

        // Assert
        Assert.True(isAssignedAndCurrent);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("mismatched")]
    public async Task ShouldDenyAssignedAdvisorReadGivenInactiveOrMismatchedCurrentDirectoryIdentity(string state)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff, active: state != "inactive",
            userId: state == "mismatched" ? Uuid.CreateVersion4() : staff.UserId);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenInactiveTenant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        fixture.Tenants.IsActive = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenClosedAcceptedEngagement()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, revoked: true, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
    }

    [Fact]
    public async Task ShouldDenyAssignedAdvisorReadGivenExpiredEngagementPeriod()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = await AttestAssignmentHistoryFixture.SeedAndReturnAsync(fixture.Provider, fixture.Tenant,
            fixture.User, practice: "advisory");
        await fixture.SeedStaffDirectoryAsync(staff);
        fixture.Memberships.State = "absent";
        fixture.Permissions.Allowed = false;
        fixture.Clock.UtcNow = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var notes = await fixture.NotesAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, notes.Error?.Kind);
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
        Fixture(Uuid? tenant = null, Uuid? user = null)
        {
            Tenant = tenant ?? Uuid.CreateVersion4();
            User = user ?? Uuid.CreateVersion4();
        }

        public Uuid Tenant { get; }
        public Uuid User { get; }
        public Uuid Program { get; } = Uuid.CreateVersion4();
        public Uuid Assessment { get; } = Uuid.CreateVersion4();
        public ServiceProvider Provider { get; private set; } = null!;
        public Permissions Permissions { get; } = new();
        public Memberships Memberships { get; private set; } = null!;
        public TenantActivity Tenants { get; } = new();
        public MutableTimeProvider Clock { get; } = new();

        public static async Task<Fixture> CreateAsync(Uuid? tenant = null, Uuid? user = null)
        {
            var fixture = new Fixture(tenant, user);
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
            services.AddSingleton<ITenantActivity>(fixture.Tenants);
            services.AddSingleton<TimeProvider>(fixture.Clock);
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

        public async Task<(BoundaryVersionView Version, BoundaryDecisionView Approval)> SeedApprovedBoundaryAsync(
            Uuid programId, bool seedApproval = true, Uuid? sourceTenantId = null)
        {
            var at = new DateTimeOffset(2026, 10, 7, 11, 0, 0, TimeSpan.Zero);
            var author = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            var boundaryId = Uuid.CreateVersion4();
            var versionId = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            var approvalId = Uuid.CreateVersion4();
            var sourceTenant = sourceTenantId ?? Tenant;
            var content = new BoundaryContent("Synthetic scoped advisory work", "readiness", ["security"], []);
            if (programId != Program)
                await ProgramManagementServices.SeedAsync(Provider, new ComplianceProgram(sourceTenant, programId), program =>
                {
                    Assert.Null(program.Create("Other program", new ProgramPlan(null, null, null, null, null, null),
                        author, "Synthetic author", at));
                    return Result.Success;
                });
            await ProgramManagementServices.SeedAsync(Provider, new SystemBoundary(sourceTenant, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(programId, versionId, content, author, "Synthetic author", at).IsSuccess);
                Assert.Null(boundary.Review(versionId, 1, reviewId, "accept", "Synthetic review", reviewer,
                    "Synthetic reviewer", at));
                if (seedApproval)
                    Assert.Null(boundary.Approve(versionId, 1, approvalId, reviewId, new DateOnly(2026, 1, 1),
                        "Synthetic approval", "synthetic-digest", reviewer, "Synthetic reviewer", at));
                return Result.Success;
            });
            return (new BoundaryVersionView(Tenant, boundaryId, programId, versionId, 1, content, "approved",
                new DateOnly(2026, 1, 1), author, "Synthetic author", at),
                new BoundaryDecisionView(Tenant, boundaryId, approvalId, versionId, 1, "approve", reviewer,
                    "Synthetic reviewer", "Synthetic approval", at, null, null, "synthetic-digest"));
        }

        public async Task SeedBoundarySuccessorAsync(BoundaryVersionView original)
        {
            var at = original.ChangedAt.AddDays(1);
            var successor = Uuid.CreateVersion4();
            var review = Uuid.CreateVersion4();
            var reviewer = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(Provider, new SystemBoundary(Tenant, original.BoundaryId), boundary =>
            {
                Assert.Null(boundary.ProposeSuccessor(original.VersionId, successor,
                    original.Content with { Statement = "Synthetic successor scope" }, original.AuthorMemberId,
                    original.AuthorDisplay, at));
                Assert.Null(boundary.Review(successor, 1, review, "accept", "Synthetic successor review",
                    reviewer, "Synthetic reviewer", at));
                Assert.Null(boundary.Approve(successor, 1, Uuid.CreateVersion4(), review, new DateOnly(2026, 1, 2),
                    "Synthetic successor approval", "synthetic-successor-digest", reviewer, "Synthetic reviewer", at));
                Assert.Equal(successor, boundary.LatestApprovedVersionId);
                return Result.Success;
            });
        }

        public async Task SeedStaffDirectoryAsync(FirmStaffMemberView staff, bool active = true,
            Uuid? userId = null)
        {
            await ProgramManagementServices.SeedAsync(Provider, new FirmStaffDirectory(), directory =>
            {
                var registered = directory.Register(Uuid.CreateVersion4(), staff.StaffMemberId,
                    userId ?? staff.UserId, staff.Practice, "Synthetic current directory identity",
                    directory.Sequence, staff.Actor, staff.RecordedAt);
                Assert.True(registered.IsSuccess, registered.Error?.Message);
                if (!active)
                {
                    var disabled = directory.SetStatus(Uuid.CreateVersion4(), staff.StaffMemberId, false,
                        "Synthetic inactive directory state", directory.Sequence, staff.Actor,
                        staff.RecordedAt.AddSeconds(1));
                    Assert.True(disabled.IsSuccess, disabled.Error?.Message);
                }
                return Result.Success;
            });
        }

        public async Task SeedLaterUnratifiedRuleDraftAsync(FirmStaffMemberView staff)
        {
            await ProgramManagementServices.SeedAsync(Provider, new IndependenceRuleCatalog(), catalog =>
            {
                var content = new IndependenceRuleContent(12,
                    [new("design", "impairing", "impairing"),
                     new("readiness", "conditionally_compatible", "impairing")],
                    "Synthetic unratified rule draft");
                var first = catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence, content,
                    staff.Actor, staff.RecordedAt.AddMinutes(1));
                Assert.True(first.IsSuccess, first.Error?.Message);
                var second = catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence,
                    content with { SourceReference = "Synthetic later unratified rule draft" },
                    staff.Actor, staff.RecordedAt.AddMinutes(2));
                Assert.True(second.IsSuccess, second.Error?.Message);
                Assert.False(second.Value!.IsRatified);
                return Result.Success;
            });
        }

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

    sealed class TenantActivity : ITenantActivity
    {
        public bool IsActive { get; set; } = true;

        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) =>
            ValueTask.FromResult(IsActive);
    }

    sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

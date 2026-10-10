using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AssignmentIndependenceReevaluationTests
{
    [Fact]
    public async Task ShouldRetainCausalClosureReceiptGivenProductionCloseAndUnaffectedOverlappingEngagement()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];
        var overlapping = before.Engagements[1];
        var previous = before.Acceptance(target.EngagementId)!;
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.User),
            new McpInvocation("bdgrz.service-engagement.close"));
        var request = new CloseServiceEngagement(fixture.Tenant, target.EngagementId, before.Sequence, "Attributed closure");

        // Act
        var result = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var retained = await fixture.ReadAsync();
        var history = await fixture.Bus.SendAsync(new GetClientIndependenceHistory(fixture.Tenant), ProgramManagementServices.Actor(fixture.User));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(history.IsSuccess, history.Error?.Message);
        var receipt = Assert.Single(history.Value!.AssignmentReevaluations);
        Assert.Equal(context.Metadata.RequestId, receipt.CausalRequestId);
        Assert.Equal(before.Sequence + 1, receipt.SourceSequence);
        Assert.Equal("engagement_closed", receipt.CauseCode);
        Assert.Equal("review_required", receipt.State);
        Assert.True(receipt.ProductionAcceptanceBlocked);
        Assert.Equal(JsonSerializer.Serialize(previous, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView),
            JsonSerializer.Serialize(receipt.PreviousAcceptance, ComplianceCoreJsonContext.Default.ServiceEngagementAcceptanceView));
        Assert.Equal("closed", receipt.ResultingAcceptance.Status);
        Assert.Equal(previous.Revision + 1, receipt.ResultingAcceptance.Revision);
        Assert.Equal(before.ActualAssignmentHistory.Count, receipt.CanonicalAssignmentHistory.Count);
        Assert.Equal(before.Sequence + 2, retained.Sequence);
        Assert.False(Fixture.Eligible(retained, target.EngagementId, fixture.Lead));
        Assert.True(Fixture.Eligible(retained, overlapping.EngagementId, fixture.Lead));
        Assert.Equal(before.Acceptance(overlapping.EngagementId)!.Revision, retained.Acceptance(overlapping.EngagementId)!.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRetainInternalRemovalReceiptGivenExactAssignmentLifecycle(bool removeLead)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];
        var removed = removeLead ? fixture.Lead : fixture.Other;

        // Act
        var result = await fixture.RemoveAsync(target.EngagementId, removed.StaffMemberId, before.Sequence);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var receipt = Assert.Single(retained.History().AssignmentReevaluations);
        Assert.Equal("assignment_removed", receipt.CauseCode);
        Assert.Equal(removed.StaffMemberId, receipt.StaffMemberId);
        Assert.Equal(before.Sequence + 2, retained.Sequence);
        Assert.False(Fixture.Eligible(retained, target.EngagementId, removed));
        Assert.Equal(!removeLead, Fixture.Eligible(retained, target.EngagementId, fixture.Lead));
        Assert.True(Fixture.Eligible(retained, before.Engagements[1].EngagementId, removed));
        Assert.Equal(before.ActualAssignmentHistory.Count, retained.ActualAssignmentHistory.Count);
        Assert.True(receipt.ProductionAcceptanceBlocked);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("mcp")]
    public async Task ShouldRevokeCurrentAssignmentAndRetainReevaluationGivenProductionTransport(string transport)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];
        var staff = fixture.Other;
        var request = new RevokeServiceEngagementActualStaff(fixture.Tenant, target.EngagementId,
            staff.StaffMemberId, before.Sequence, "Synthetic attributed actual-assignment revocation");
        var path = $"/api/v1/tenants/{fixture.Tenant}/service-engagements/{target.EngagementId}/actual-assignments/{staff.StaffMemberId}/revocations";
        RequestInvocation invocation = transport == "http"
            ? new HttpInvocation("POST", path, path, "bdgrz.service-engagement.actual-staff.revoke")
            : new McpInvocation("bdgrz.service-engagement.actual-staff.revoke");
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.User), invocation);

        // Act
        var result = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var retry = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var conflictingRetry = await fixture.Bus.DispatchAsync(request with { Reason = "Changed attribution" }, context,
            CancellationToken.None);
        var retained = await fixture.ReadAsync();
        var history = await fixture.Bus.SendAsync(new GetClientIndependenceHistory(fixture.Tenant),
            ProgramManagementServices.Actor(fixture.User));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.False(conflictingRetry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, conflictingRetry.Error?.Kind);
        Assert.True(history.IsSuccess, history.Error?.Message);
        Assert.Equal("active", result.Value!.Status);
        Assert.Equal(result.Value.Revision, retry.Value!.Revision);
        Assert.Contains(result.Value.Assignments, assignment => assignment.StaffMemberId == staff.StaffMemberId && !assignment.IsCurrent);
        var receipt = Assert.Single(history.Value!.AssignmentReevaluations);
        Assert.Equal("assignment_removed", receipt.CauseCode);
        Assert.Equal(staff.StaffMemberId, receipt.StaffMemberId);
        Assert.Equal(result.Value.Revision, receipt.ResultingAcceptance.Revision);
        Assert.Equal(before.Sequence + 1, receipt.SourceSequence);
        Assert.Equal(before.Sequence + 2, retained.Sequence);
        Assert.False(Fixture.Eligible(retained, target.EngagementId, staff));
        Assert.True(Fixture.Eligible(retained, target.EngagementId, fixture.Lead));
    }

    [Fact]
    public async Task ShouldDenyActualAssignmentRevocationGivenCurrentMemberWithoutClientManagerPermission()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];
        var request = new RevokeServiceEngagementActualStaff(fixture.Tenant, target.EngagementId,
            fixture.Other.StaffMemberId, before.Sequence, "Synthetic unauthorized removal attempt");

        // Act
        var result = await fixture.Bus.DispatchAsync(request, new RequestDispatchContext(
            ProgramManagementServices.Actor(fixture.NonManager), new McpInvocation("bdgrz.service-engagement.actual-staff.revoke")),
            CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(before.Sequence, retained.Sequence);
        Assert.Empty(retained.History().AssignmentReevaluations);
        Assert.True(Fixture.Eligible(retained, target.EngagementId, fixture.Other));
    }

    [Fact]
    public async Task ShouldCloseAcceptedEngagementGivenProductionLeadAssignmentRevocation()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];
        var path = $"/api/v1/tenants/{fixture.Tenant}/service-engagements/{target.EngagementId}/actual-assignments/{fixture.Lead.StaffMemberId}/revocations";
        var request = new RevokeServiceEngagementActualStaff(fixture.Tenant, target.EngagementId,
            fixture.Lead.StaffMemberId, before.Sequence, "Synthetic lead assignment revocation");
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.User),
            new HttpInvocation("POST", path, path, "bdgrz.service-engagement.actual-staff.revoke"));

        // Act
        var result = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var retained = await fixture.ReadAsync();
        var history = await fixture.Bus.SendAsync(new GetClientIndependenceHistory(fixture.Tenant),
            ProgramManagementServices.Actor(fixture.User));

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(history.IsSuccess, history.Error?.Message);
        Assert.Equal("closed", result.Value!.Status);
        Assert.Equal("closed", retained.Engagement(target.EngagementId)!.Status);
        var receipt = Assert.Single(history.Value!.AssignmentReevaluations);
        Assert.Equal("assignment_removed", receipt.CauseCode);
        Assert.Equal(fixture.Lead.StaffMemberId, receipt.StaffMemberId);
        Assert.Equal(before.Sequence + 2, retained.Sequence);
        Assert.False(Fixture.Eligible(retained, target.EngagementId, fixture.Lead));
        Assert.False(Fixture.Eligible(retained, target.EngagementId, fixture.Other));
    }

    [Fact]
    public async Task ShouldCommitOneCausalRemovalGivenConcurrentProductionAssignmentRevocations()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];
        var requests = new[] { fixture.Lead, fixture.Other }.Select(staff =>
            new RevokeServiceEngagementActualStaff(fixture.Tenant, target.EngagementId,
                staff.StaffMemberId, before.Sequence, "Synthetic competing actual-assignment removal")).ToArray();
        fixture.Gate.Enabled = true;

        // Act
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            try
            {
                return (await fixture.Bus.SendAsync(request, ProgramManagementServices.Actor(fixture.User))).IsSuccess;
            }
            catch (EventStreamConcurrencyException)
            {
                return false;
            }
        }));
        fixture.Gate.Enabled = false;
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Single(results, success => success);
        Assert.Single(results, success => !success);
        Assert.Equal(before.CommittedStreamPosition + 2, retained.CommittedStreamPosition);
        Assert.Equal(before.Sequence + 2, retained.Sequence);
        var receipt = Assert.Single(retained.History().AssignmentReevaluations);
        Assert.Equal("assignment_removed", receipt.CauseCode);
        Assert.Equal(before.Sequence + 1, receipt.SourceSequence);
    }

    [Fact]
    public async Task ShouldRefuseEntireLifecycleMutationGivenCompleteSnapshotReceiptExceedsBound()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(staffCount: 40);
        var before = await fixture.ReadAsync();
        var target = before.Engagements[0];

        // Act
        var result = await fixture.Bus.SendAsync(new CloseServiceEngagement(fixture.Tenant, target.EngagementId,
            before.Sequence, "Attributed bounded closure"), ProgramManagementServices.Actor(fixture.User));
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Contains("complete assignment cause and receipt", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal("active", retained.Acceptance(target.EngagementId)!.Status);
        Assert.Empty(retained.History().AssignmentReevaluations);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("cause")]
    [InlineData("source_sequence")]
    [InlineData("previous")]
    [InlineData("resulting")]
    [InlineData("history")]
    [InlineData("actor")]
    [InlineData("authority")]
    [InlineData("time")]
    [InlineData("intent")]
    public async Task ShouldRefuseReceiptReplayGivenChangedLifecycleProvenance(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        Assert.True((await fixture.Bus.SendAsync(new CloseServiceEngagement(fixture.Tenant, before.Engagements[0].EngagementId,
            before.Sequence, "Attributed closure"), ProgramManagementServices.Actor(fixture.User))).IsSuccess);
        var events = await fixture.EventsAsync();
        var ev = Assert.IsType<AssignmentIndependenceReevaluated>(events[^1]);
        var receipt = ev.Reevaluation;
        receipt = change switch
        {
            "tenant" => receipt with { TenantId = Uuid.CreateVersion4() },
            "cause" => receipt with { CausalRequestId = Uuid.CreateVersion4() },
            "source_sequence" => receipt with { SourceSequence = receipt.SourceSequence + 1 },
            "previous" => receipt with { PreviousAcceptance = receipt.PreviousAcceptance with { Revision = 9 } },
            "resulting" => receipt with { ResultingAcceptance = receipt.ResultingAcceptance with { Status = "active" } },
            "history" => receipt with { CanonicalAssignmentHistory = [] },
            "actor" => receipt with { SourceActor = ActorReference.ForMember(Uuid.CreateVersion4(), "Wrong actor") },
            "authority" => receipt with { ProductionAcceptanceBlocked = false },
            "time" => receipt with { RecordedAt = receipt.RecordedAt.AddDays(-1) },
            "intent" => receipt with { SourceIntent = "different logical decision" },
            _ => throw new InvalidOperationException()
        };

        // Act
        Action replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant))
            .Given(events[..^1].Append(ev with { Reevaluation = receipt }).ToArray());

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldRetainExactImmutableHistoryGivenLifecycleReplayAndLogicalRetry()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.User));
        var request = new CloseServiceEngagement(fixture.Tenant, before.Engagements[0].EngagementId, before.Sequence, "Attributed closure");
        var first = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var once = await fixture.ReadAsync();

        // Act
        var repeated = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var changed = await fixture.Bus.DispatchAsync(request with { Reason = "Changed intent" }, context, CancellationToken.None);
        var changedActor = await fixture.Bus.DispatchAsync(request, new RequestDispatchContext(
            ProgramManagementServices.Actor(fixture.OtherUser), context.Invocation, context.Metadata), CancellationToken.None);
        var retained = await fixture.ReadAsync();
        var events = await fixture.EventsAsync();
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant)).Given(events).Aggregate;

        // Assert
        Assert.True(first.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.False(changedActor.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(once.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(JsonSerializer.Serialize(first.Value, ComplianceCoreJsonContext.Default.ServiceEngagementView),
            JsonSerializer.Serialize(repeated.Value, ComplianceCoreJsonContext.Default.ServiceEngagementView));
        Assert.Equal(JsonSerializer.Serialize(retained.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView),
            JsonSerializer.Serialize(replay.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView));
        var receipt = Assert.Single(replay.History().AssignmentReevaluations);
        Assert.Throws<NotSupportedException>(() => ((IList<AssignmentIndependenceReevaluationView>)replay.History().AssignmentReevaluations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<HistoricalEngagementAssignmentView>)receipt.CanonicalAssignmentHistory).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EngagementActualAssignmentView>)receipt.PreviousAcceptance.Assignments).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<IndependenceServiceRuleContent>)receipt.ResultingAcceptance.Rules.Content.ServiceRules).Clear());
    }

    [Fact]
    public async Task ShouldRetainNoPartialLifecycleMutationGivenProductionWriterFailureAndRetry()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var context = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.User));
        var request = new CloseServiceEngagement(fixture.Tenant, before.Engagements[0].EngagementId, before.Sequence, "Attributed closure");
        fixture.Faults.FailNextSave = true;

        // Act
        await Assert.ThrowsAsync<IOException>(async () => await fixture.Bus.DispatchAsync(request, context, CancellationToken.None));
        var failed = await fixture.ReadAsync();
        var retry = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(2, fixture.Faults.PendingAtFailure.Length);
        Assert.IsType<ServiceEngagementAssignmentRevoked>(fixture.Faults.PendingAtFailure[0]);
        Assert.IsType<AssignmentIndependenceReevaluated>(fixture.Faults.PendingAtFailure[1]);
        Assert.Equal(before.CommittedStreamPosition, failed.CommittedStreamPosition);
        Assert.Equal("active", failed.Acceptance(request.EngagementId)!.Status);
        Assert.Empty(failed.History().AssignmentReevaluations);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(before.CommittedStreamPosition + 2, retained.CommittedStreamPosition);
        Assert.Single(retained.History().AssignmentReevaluations);
    }

    [Fact]
    public async Task ShouldCommitOneCompleteLifecycleTransactionGivenConcurrentProductionClosures()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var requests = before.Engagements.Select(engagement => new CloseServiceEngagement(fixture.Tenant,
            engagement.EngagementId, before.Sequence, "Attributed competing closure")).ToArray();
        fixture.Gate.Enabled = true;

        // Act
        var results = await Task.WhenAll(requests.Select(async request =>
        {
            try
            {
                return (await fixture.Bus.SendAsync(request, ProgramManagementServices.Actor(fixture.User))).IsSuccess;
            }
            catch (EventStreamConcurrencyException)
            {
                return false;
            }
        }));
        fixture.Gate.Enabled = false;
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
        Assert.Equal(before.CommittedStreamPosition + 2, retained.CommittedStreamPosition);
        Assert.Single(retained.History().AssignmentReevaluations);
        Assert.Single(retained.Engagements, engagement => engagement.Status == "closed");
        Assert.Single(retained.Engagements, engagement => engagement.Status == "accepted");
    }

    [Fact]
    public async Task ShouldRefuseCompleteReceiptGivenLifecycleClockPrecedesCapturedCanonicalHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(laterSecond: true);
        var before = await fixture.ReadAsync();
        var earlier = before.Engagements.OrderBy(engagement => engagement.RecordedAt).First();
        fixture.Clock.Now = earlier.RecordedAt.AddHours(1);

        // Act
        var result = await fixture.Bus.SendAsync(new CloseServiceEngagement(fixture.Tenant, earlier.EngagementId,
            before.Sequence, "Attributed backdated closure"), ProgramManagementServices.Actor(fixture.User));
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("cannot precede retained client sources", result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal("active", retained.Acceptance(earlier.EngagementId)!.Status);
        Assert.Empty(retained.History().AssignmentReevaluations);
    }

    [Fact]
    public async Task ShouldRefuseForeignTenantLifecycleWriteGivenOrdinaryCurrentMembershipAuthority()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.Bus.SendAsync(new CloseServiceEngagement(Uuid.CreateVersion4(), before.Engagements[0].EngagementId,
            before.Sequence, "Foreign scope"), ProgramManagementServices.Actor(fixture.User));
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Empty(retained.History().AssignmentReevaluations);
    }

    [Fact]
    public void ShouldDefaultToEmptyAssignmentReceiptHistoryGivenHistoricalJson()
    {
        // Arrange
        var payload = "{\"tenant_id\":\"" + Uuid.CreateVersion4() + "\",\"sequence\":0,\"rule_versions\":[],\"services\":[],\"evaluations\":[]}";

        // Act
        var history = JsonSerializer.Deserialize(payload, ComplianceCoreJsonContext.Default.IndependenceHistoryView);

        // Assert
        Assert.NotNull(history);
        Assert.Empty(history.AssignmentReevaluations);
        Assert.Empty(history.SourceReevaluations);
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid OtherUser { get; } = Uuid.CreateVersion4();
        public Uuid NonManager { get; } = Uuid.CreateVersion4();
        public Faults Faults { get; } = new();
        public Gate Gate { get; } = new();
        public Clock Clock { get; } = new();
        public IRequestBus Bus { get; }
        public FirmStaffMemberView Lead { get; private set; } = null!;
        public FirmStaffMemberView Other { get; private set; } = null!;
        static readonly DateTimeOffset SourceTime = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

        Fixture()
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
            services.AddSingleton<ITenantActivity>(new Activity());
            services.AddSingleton<IPermissionAuthorizer>(new Permissions(RbacIds.Member(Tenant, NonManager)));
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(Tenant, User, OtherUser, NonManager));
            services.AddSingleton<TimeProvider>(Clock);
            var originalReader = services.Single(descriptor => descriptor.ServiceType == typeof(IAggregateReader));
            var readerFactory = originalReader.ImplementationFactory ?? throw new InvalidOperationException("An aggregate reader factory is required.");
            services.Remove(originalReader);
            services.AddScoped<IAggregateReader>(provider => new GatedReader((IAggregateReader)readerFactory(provider), Gate));
            var originalWriter = services.Single(descriptor => descriptor.ServiceType == typeof(IAggregateWriter));
            var writerFactory = originalWriter.ImplementationFactory ?? throw new InvalidOperationException("An aggregate writer factory is required.");
            services.Remove(originalWriter);
            services.AddScoped<IAggregateWriter>(provider => new FaultingWriter((IAggregateWriter)writerFactory(provider), Faults));
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            _scope = _provider.CreateAsyncScope();
            Bus = _scope.ServiceProvider.GetRequiredService<IRequestBus>();
        }

        public static async Task<Fixture> CreateAsync(int staffCount = 2, bool laterSecond = false)
        {
            var fixture = new Fixture();
            var client = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.User), "Synthetic client administrator");
            fixture.Lead = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "attest", "Synthetic lead", true,
                1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), SourceTime);
            fixture.Other = fixture.Lead with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() };
            var staff = new[] { fixture.Lead, fixture.Other }.Concat(Enumerable.Range(2, staffCount - 2)
                .Select(_ => fixture.Lead with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() })).ToArray();
            var partner = fixture.Lead with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() };
            await ProgramManagementServices.SeedAsync(fixture._provider, new IndependenceLedger(fixture.Tenant), ledger =>
            {
                for (var index = 0; index < 2; index++)
                {
                    var now = laterSecond ? SourceTime.AddHours(index * 2) : SourceTime;
                    var id = Uuid.CreateVersion4();
                    var acknowledgement = Uuid.CreateVersion4();
                    Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), id, ledger.Sequence,
                        new ServiceEngagementDraftContent("attest", "Synthetic scope", new DateOnly(2026, 1, 1),
                            new DateOnly(2026, 12, 31), fixture.Lead.StaffMemberId), fixture.Lead, client, now).IsSuccess);
                    foreach (var proposed in staff.Skip(1))
                        Assert.True(ledger.ProposeEngagementStaff(Uuid.CreateVersion4(), id, ledger.Sequence, proposed, client, now).IsSuccess);
                    var draft = ledger.Engagement(id)!;
                    Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
                        fixture.Tenant, id, acknowledgement, ledger.Sequence, draft.Revision, [], "I retain management responsibility"),
                        fixture.User, client, now).IsSuccess);
                    var proof = new VerifiedEngagementAcceptance(fixture.Tenant, id, draft.Revision, Uuid.CreateVersion4(),
                        partner.StaffMemberId, partner.UserId, "Synthetic verified authority ONLY", null,
                        acknowledgement, null, null, staff, partner, 1, now);
                    var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
                        [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
                        "Synthetic ratified test rules ONLY"), fixture.Lead.Actor, now, true);
                    var accepted = ledger.AcceptEngagement(Uuid.CreateVersion4(), ledger.Sequence, proof, rules, now);
                    Assert.True(accepted.IsSuccess, accepted.Error?.Message);
                }
                return Result.Success;
            });
            return fixture;
        }

        public static bool Eligible(IndependenceLedger ledger, Uuid engagementId, FirmStaffMemberView staff) =>
            ledger.IsEligibleForProfessionalAccess(engagementId, staff.StaffMemberId, staff.UserId, 1, 1, SourceTime.AddDays(1));
        public Task<IndependenceLedger> ReadAsync() => ProgramManagementServices.HydrateAsync(_provider, new IndependenceLedger(Tenant));
        public ValueTask<Result<ServiceEngagementAcceptanceView>> RemoveAsync(Uuid engagementId, Uuid staffId, long sequence) =>
            _scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(new IndependenceLedger(Tenant),
                ledger => AggregateOutcome.CommitOnSuccess(ledger.RemoveActualStaff(Uuid.CreateVersion4(), engagementId,
                    staffId, sequence, "Synthetic INTERNAL removal ONLY", ActorReference.ForMember(RbacIds.Member(Tenant, User),
                        "Synthetic client administrator"), SourceTime.AddDays(1))),
                new RequestDispatchContext(ProgramManagementServices.Actor(User)), CancellationToken.None);
        public async Task<DomainEvent[]> EventsAsync()
        {
            var events = new List<DomainEvent>();
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                new IndependenceLedger(Tenant).Stream, 0, CancellationToken.None))
                events.Add(record.Event);
            return events.ToArray();
        }
        public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    sealed class Faults
    {
        public bool FailNextSave { get; set; }
        public DomainEvent[] PendingAtFailure { get; set; } = [];
    }
    sealed class FaultingWriter(IAggregateWriter inner, Faults faults) : IAggregateWriter
    {
        public ValueTask SaveAsync<TAggregate>(TAggregate aggregate, IExecutionContext context, CancellationToken ct = default) where TAggregate : Aggregate
        {
            if (aggregate is IndependenceLedger ledger && faults.FailNextSave)
            {
                faults.FailNextSave = false;
                faults.PendingAtFailure = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
                throw new IOException("Synthetic lifecycle append failure");
            }
            return inner.SaveAsync(aggregate, context, ct);
        }
    }
    sealed class Gate
    {
        public bool Enabled { get; set; }
        public int Arrived;
        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    sealed class GatedReader(IAggregateReader inner, Gate gate) : IAggregateReader
    {
        public async ValueTask<T> HydrateAsync<T>(T aggregate, CancellationToken ct = default) where T : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (aggregate is not IndependenceLedger || !gate.Enabled)
                return hydrated;
            if (Interlocked.Increment(ref gate.Arrived) == 2)
                gate.Released.TrySetResult();
            await gate.Released.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
            return hydrated;
        }
    }

    sealed class Activity : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class Permissions(Uuid deniedMember) : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(memberId != deniedMember);
    }
    sealed class Memberships(Uuid tenant, Uuid user, Uuid other, Uuid nonManager) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() &&
                (userId == user || userId == other || userId == nonManager)
                ? new TenantMembershipView(userId, tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(tenantId == tenant.ToString() && (userId == user || userId == other || userId == nonManager));
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

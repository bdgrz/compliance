using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class DirectoryIndependenceReevaluationTests
{
    [Fact]
    public void ShouldExposeEmptyDirectoryReceiptsGivenHistoricalHistoryJson()
    {
        // Arrange
        var json = $"{{\"tenant_id\":\"{Uuid.CreateVersion4()}\",\"sequence\":0,\"rule_versions\":[],\"services\":[],\"evaluations\":[]}}";

        // Act
        var history = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.IndependenceHistoryView)!;
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(history, ComplianceCoreJsonContext.Default.IndependenceHistoryView));

        // Assert
        Assert.Equal(0, serialized.RootElement.GetProperty("directory_reevaluations").GetArrayLength());
    }

    [Fact]
    public async Task ShouldRetainDirectoryReceiptGivenRegisteredStatusReactionAndAcceptedAssignment()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RecordStatusAsync(fixture, false);
        var registration = Assert.Single(fixture.Services.GetServices<WorkloadRegistration>(),
            item => item.Name == "FirmStaffStatusReevaluationV1");
        Assert.Equal(WorkloadScope.Global, registration.Scope);
        var reactor = (Reactor)fixture.Services.GetRequiredService(registration.ComponentType);
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(reactor.Name, reactor.Pattern);
        var runner = new ReactorRunner(fixture.Services.GetRequiredService<IDomainEventReader>(),
            fixture.Services.GetRequiredService<IReactorPrincipalProvider>(), fixture.Services.GetRequiredService<TimeProvider>());
        var checkpoint = await checkpoints.LoadAsync(identity);

        // Act
        while (true)
        {
            ProjectionCheckpoint next;
            try
            {
                next = await runner.RunAsync(reactor, checkpoint);
            }
            catch (ReactionCommandFailedException error)
            {
                Assert.Fail(error.Error.Message);
                throw;
            }
            if (next == checkpoint)
                break;
            checkpoint = next;
        }
        var ledger = await fixture.HydrateLedgerAsync();
        using var history = JsonDocument.Parse(JsonSerializer.Serialize(ledger.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView));

        // Assert
        var receipts = history.RootElement.GetProperty("directory_reevaluations");
        Assert.Equal(1, receipts.GetArrayLength());
        Assert.Equal("review_required", receipts[0].GetProperty("state").GetString());
        Assert.True(receipts[0].GetProperty("production_acceptance_blocked").GetBoolean());
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
    }

    [Fact]
    public async Task ShouldRetainStatusCheckpointGivenAcceptanceReconciledBeforeStatusAndLocatorLag()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await RunAsync(fixture, "AcceptedStaffDirectoryReconciliationV1", tenantId: fixture.Tenant);
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var before = await checkpoints.LoadAsync(identity);
        await RecordStatusAsync(fixture, false);

        // Act
        var pending = await Assert.ThrowsAsync<ReactionCommandFailedException>(() =>
            RunAsync(fixture, "FirmStaffStatusReevaluationV1"));

        // Assert
        Assert.True(pending.Error.IsTransient);
        Assert.Equal(before, await checkpoints.LoadAsync(identity));
        Assert.Empty((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var receipt = Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
        Assert.Equal(fixture.Staff.StaffMemberId, receipt.Source.StaffMemberId);
        Assert.NotEqual(before, await checkpoints.LoadAsync(identity));
        await RunAsync(fixture, "AcceptedStaffDirectoryReconciliationV1");
        Assert.Equal(receipt.ReevaluationId,
            Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations).ReevaluationId);
    }

    [Fact]
    public async Task ShouldReplayCommittedReceiptAfterLifecycleChangeGivenPartialFanOutAndPausedClientRecovery()
    {
        // Arrange
        var clock = new Clock();
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: clock);
        var other = Uuid.CreateVersion4();
        await fixture.SeedOtherTenantAsync(other);
        await RegisterTenantAsync(fixture);
        await RegisterTenantAsync(fixture, other);
        await fixture.ProjectAsync();
        await fixture.ProjectOtherTenantAsync(other);
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var catalogue = new List<Uuid>();
        await foreach (var tenant in fixture.Services.GetRequiredService<ITenantDirectory>().GetActiveTenantsAsync())
            catalogue.Add(Uuid.Parse(tenant.Value, null));
        Assert.Equal(2, catalogue.Count);
        var completed = catalogue[0];
        var paused = catalogue[1];
        var operatorId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Tenant(paused), tenant =>
            tenant.Suspend(operatorId, "Synthetic suspended client", clock.Now));
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var before = await checkpoints.LoadAsync(identity);
        await RecordStatusAsync(fixture, false);

        // Act
        var pending = await Assert.ThrowsAsync<ReactionCommandFailedException>(() =>
            RunAsync(fixture, "FirmStaffStatusReevaluationV1"));
        Assert.Equal(before, await checkpoints.LoadAsync(identity));
        Assert.Empty((await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new IndependenceLedger(paused))).History().DirectoryReevaluations);
        var first = Assert.Single((await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new IndependenceLedger(completed))).History().DirectoryReevaluations);
        clock.Now = clock.Now.AddMinutes(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new IndependenceLedger(completed), ledger =>
            ledger.CloseEngagement(Uuid.CreateVersion4(), first.OriginalAcceptance.EngagementId, ledger.Sequence,
                "Explicit retained synthetic client lifecycle change",
                ActorReference.ForMember(RbacIds.Member(completed, Uuid.CreateVersion4()), "Synthetic client author"), clock.Now));
        clock.Now = clock.Now.AddMinutes(1);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Tenant(paused), tenant =>
            tenant.Reactivate(operatorId, "Synthetic client recovery", clock.Now));
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");

        // Assert
        Assert.True(pending.Error.IsTransient);
        Assert.NotEqual(before, await checkpoints.LoadAsync(identity));
        var committedLedger = await ProgramManagementServices.HydrateAsync(fixture.Provider, new IndependenceLedger(completed));
        var retried = Assert.Single(committedLedger.History().DirectoryReevaluations);
        Assert.Equal(first.ReevaluationId, retried.ReevaluationId);
        Assert.Equal(first.RecordedAt, retried.RecordedAt);
        Assert.Equal("active", retried.ObservedAcceptance.Status);
        Assert.Equal("closed", committedLedger.Acceptance(first.OriginalAcceptance.EngagementId)!.Status);
        Assert.Single((await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new IndependenceLedger(paused))).History().DirectoryReevaluations);
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("client")]
    [InlineData("generic_system")]
    [InlineData("wrong_process")]
    [InlineData("mcp")]
    [InlineData("http")]
    [InlineData("causation")]
    [InlineData("directory_offset")]
    [InlineData("directory_event")]
    [InlineData("directory_digest")]
    [InlineData("acceptance_event")]
    [InlineData("acceptance_request")]
    [InlineData("acceptance_digest")]
    [InlineData("foreign_tenant")]
    [InlineData("foreign_user")]
    public async Task ShouldRefuseSourceEffectWithoutAppendGivenUntrustedActorTransportOrSource(string change)
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await RecordStatusAsync(fixture, false);
        var command = await CommandAsync(fixture);
        var actor = change switch
        {
            "anonymous" => RequestActor.Anonymous,
            "client" => ProgramManagementServices.Actor(Uuid.CreateVersion4()),
            "generic_system" => RequestActor.System,
            "wrong_process" => RequestActor.CreateSystem("reactor:OtherProcess", "bdgrz.system"),
            _ => RequestActor.CreateSystem("reactor:FirmStaffStatusReevaluationV1", "bdgrz.system")
        };
        RequestInvocation invocation = change switch
        {
            "mcp" => new McpInvocation("synthetic-directory-effect"),
            "http" => new HttpInvocation("POST", "/synthetic", "/synthetic", "synthetic"),
            _ => new DirectInvocation()
        };
        var altered = change switch
        {
            "directory_offset" => command with { DirectoryResourceOffset = 0 },
            "directory_event" => command with { DirectoryEventId = Uuid.CreateVersion4() },
            "directory_digest" => command with { DirectoryPayloadSha256 = new string('0', 64) },
            "acceptance_event" => command with { AcceptanceEventId = Uuid.CreateVersion4() },
            "acceptance_request" => command with { AcceptanceRequestId = Uuid.CreateVersion4() },
            "acceptance_digest" => command with { AcceptancePayloadSha256 = new string('0', 64) },
            "foreign_tenant" => command with { TenantId = Uuid.CreateVersion4() },
            "foreign_user" => command with { UserId = Uuid.CreateVersion4() },
            _ => command
        };
        var context = new RequestDispatchContext(actor, invocation, new RequestMetadata(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), change == "causation" ? Uuid.CreateVersion4() : command.DirectoryEventId));
        var before = await fixture.HydrateLedgerAsync();

        // Act
        var result = await fixture.Services.GetRequiredService<IRequestBus>().DispatchAsync(altered, context);
        var after = await fixture.HydrateLedgerAsync();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Empty(after.History().DirectoryReevaluations);
        Assert.False(typeof(ICallable).IsAssignableFrom(typeof(RecordDirectoryIndependenceReevaluation)));
        Assert.False(typeof(IQueuable).IsAssignableFrom(typeof(RecordDirectoryIndependenceReevaluation)));
    }

    [Fact]
    public async Task ShouldRetainImmutableSafeHistoryAndExactReplayGivenRecordedDirectoryReceipt()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RecordStatusAsync(fixture, false);
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var ledger = await fixture.HydrateLedgerAsync();
        var receipt = Assert.Single(ledger.History().DirectoryReevaluations);
        var records = await fixture.SourceRecordsAsync();

        // Act
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant))
            .Given(records.Select(record => record.Event).ToArray()).Aggregate;
        var json = JsonSerializer.Serialize(replay.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView);

        // Assert
        Assert.Equal(JsonSerializer.Serialize(ledger.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView), json);
        Assert.DoesNotContain("operator status reason", json);
        Assert.DoesNotContain("Private synthetic status operator", json);
        Assert.Throws<NotSupportedException>(() => ((IList<DirectoryIndependenceReevaluationView>)ledger.History().DirectoryReevaluations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EngagementActualAssignmentView>)receipt.OriginalAcceptance.Assignments).Clear());
        var current = ledger.Acceptance(receipt.OriginalAcceptance.EngagementId)!;
        Assert.False(ledger.IsEligibleForProfessionalAccess(current.EngagementId, fixture.Staff.StaffMemberId,
            fixture.Staff.UserId, current.Rules.Version, fixture.Staff.Revision, receipt.RecordedAt));
    }

    [Fact]
    public async Task ShouldRejectCompleteReceiptWithoutAppendGivenOversizedAcceptedSnapshots()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock(), serviceCount: 15);
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var before = await fixture.HydrateLedgerAsync();
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var checkpoint = await checkpoints.LoadAsync(identity);
        await RecordStatusAsync(fixture, false);

        // Act
        var rejected = await Assert.ThrowsAsync<ReactionCommandFailedException>(() =>
            RunAsync(fixture, "FirmStaffStatusReevaluationV1"));
        var after = await fixture.HydrateLedgerAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, rejected.Error.Kind);
        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Empty(after.History().DirectoryReevaluations);
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
        Assert.Equal(15, after.History().Services.Count);
        Assert.Single(Assert.Single(after.AcceptanceHistory(Assert.Single(after.Engagements).EngagementId)).Assignments);
    }

    [Fact]
    public async Task ShouldPreserveUnaffectedAssigneeEligibilityGivenOneStaffDirectoryChange()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(staffCount: 2, clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RecordStatusAsync(fixture, false);

        // Act
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var ledger = await fixture.HydrateLedgerAsync();
        var receipt = Assert.Single(ledger.History().DirectoryReevaluations);
        var acceptance = ledger.Acceptance(receipt.OriginalAcceptance.EngagementId)!;
        var other = Assert.Single(acceptance.Assignments, staff => staff.StaffMemberId != fixture.Staff.StaffMemberId);

        // Assert
        Assert.False(ledger.IsEligibleForProfessionalAccess(acceptance.EngagementId, fixture.Staff.StaffMemberId,
            fixture.Staff.UserId, acceptance.Rules.Version, fixture.Staff.Revision, receipt.RecordedAt));
        Assert.True(ledger.IsEligibleForProfessionalAccess(acceptance.EngagementId, other.StaffMemberId,
            other.UserId, acceptance.Rules.Version, other.DirectoryStaffRevision, receipt.RecordedAt));
    }

    [Fact]
    public async Task ShouldReconcileEveryOlderStatusAndPreserveFreshClientGivenReactivationBeforeAcceptance()
    {
        // Arrange
        var clock = new Clock();
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: clock);
        await RegisterTenantAsync(fixture);
        await RecordStatusAsync(fixture, false);
        await RecordStatusAsync(fixture, true);
        var directory = await ProgramManagementServices.HydrateAsync(fixture.Provider, new FirmStaffDirectory());
        var fresh = directory.Get(fixture.Staff.StaffMemberId)!;
        Assert.Equal(3, fresh.Revision);
        var otherTenant = Uuid.CreateVersion4();
        await fixture.SeedOtherTenantAsync(otherTenant, fresh, clock.Now);
        await RegisterTenantAsync(fixture, otherTenant);

        // Act
        await RunAsync(fixture, "AcceptedStaffDirectoryReconciliationV1");
        await RunAsync(fixture, "AcceptedStaffDirectoryReconciliationV1", otherTenant);
        await fixture.ProjectAsync();
        await fixture.ProjectOtherTenantAsync(otherTenant);
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var oldLedger = await fixture.HydrateLedgerAsync();
        var freshLedger = await ProgramManagementServices.HydrateAsync(fixture.Provider, new IndependenceLedger(otherTenant));

        // Assert
        Assert.Equal(new long[] { 2, 3 }, oldLedger.History().DirectoryReevaluations.Select(receipt => receipt.Source.StaffRevision));
        Assert.Empty(freshLedger.History().DirectoryReevaluations);
        var oldAcceptance = Assert.Single(oldLedger.AcceptanceHistory(Assert.Single(oldLedger.Engagements).EngagementId));
        var freshAcceptance = Assert.Single(freshLedger.AcceptanceHistory(Assert.Single(freshLedger.Engagements).EngagementId));
        Assert.False(oldLedger.IsEligibleForProfessionalAccess(oldAcceptance.EngagementId, fixture.Staff.StaffMemberId,
            fixture.Staff.UserId, oldAcceptance.Rules.Version, 1, clock.Now));
        Assert.True(freshLedger.IsEligibleForProfessionalAccess(freshAcceptance.EngagementId, fresh.StaffMemberId,
            fresh.UserId, freshAcceptance.Rules.Version, fresh.Revision, clock.Now));
    }

    [Fact]
    public async Task ShouldCompleteStatusCursorGivenSuspendedRegisteredClientWithoutAssignments()
    {
        // Arrange
        var clock = new Clock();
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: clock);
        await RegisterTenantAsync(fixture);
        var emptyClient = Uuid.CreateVersion4();
        await RegisterTenantAsync(fixture, emptyClient);
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Tenant(emptyClient), tenant =>
            tenant.Suspend(Uuid.CreateVersion4(), "Synthetic empty client suspension", clock.Now));
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        await RecordStatusAsync(fixture, false);

        // Act
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");

        // Assert
        Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
        var empty = await ProgramManagementServices.HydrateAsync(fixture.Provider, new IndependenceLedger(emptyClient));
        Assert.Equal(0ul, empty.CommittedStreamPosition);
        Assert.Empty(empty.History().DirectoryReevaluations);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("sequence")]
    [InlineData("identity")]
    [InlineData("state")]
    [InlineData("authority")]
    [InlineData("source_event")]
    [InlineData("source_request")]
    [InlineData("source_staff")]
    [InlineData("source_user")]
    [InlineData("source_revision")]
    [InlineData("source_time")]
    [InlineData("source_status")]
    [InlineData("source_event_changed")]
    [InlineData("source_offset")]
    [InlineData("source_sequence")]
    [InlineData("source_digest")]
    [InlineData("original_hash")]
    [InlineData("original_snapshot")]
    [InlineData("observed_hash")]
    [InlineData("observed_snapshot")]
    [InlineData("clock")]
    [InlineData("actor")]
    [InlineData("actor_display")]
    public async Task ShouldRefuseReceiptReplayGivenChangedFrozenContract(string change)
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RecordStatusAsync(fixture, false);
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var events = (await fixture.SourceRecordsAsync()).Select(record => record.Event).ToArray();
        var ev = Assert.IsType<DirectoryIndependenceReevaluated>(events[^1]);
        var receipt = ev.Reevaluation;
        var altered = change switch
        {
            "identity" => receipt with { ReevaluationId = Uuid.CreateVersion4() },
            "state" => receipt with { State = "compatible" },
            "authority" => receipt with { ProductionAcceptanceBlocked = false },
            "source_event" => receipt with { Source = receipt.Source with { EventId = Uuid.Empty } },
            "source_request" => receipt with { Source = receipt.Source with { RequestId = Uuid.Empty } },
            "source_staff" => receipt with { Source = receipt.Source with { StaffMemberId = Uuid.CreateVersion4() } },
            "source_user" => receipt with { Source = receipt.Source with { UserId = Uuid.CreateVersion4() } },
            "source_revision" => receipt with { Source = receipt.Source with { StaffRevision = 1 } },
            "source_time" => receipt with { Source = receipt.Source with { RecordedAt = receipt.OriginalAcceptance.RecordedAt.AddHours(-1) } },
            "source_status" => receipt with { Source = receipt.Source with { IsActive = !receipt.Source.IsActive } },
            "source_event_changed" => receipt with { Source = receipt.Source with { EventId = Uuid.CreateVersion4() } },
            "source_offset" => receipt with { Source = receipt.Source with { ResourceOffset = receipt.Source.ResourceOffset + 1 } },
            "source_sequence" => receipt with { Source = receipt.Source with { SourceSequence = receipt.Source.SourceSequence + 1 } },
            "source_digest" => receipt with { OriginalSourceSha256 = new string('0', 64) },
            "original_hash" => receipt with { OriginalAcceptanceSha256 = new string('0', 64) },
            "original_snapshot" => receipt with { OriginalAcceptance = receipt.OriginalAcceptance with { AuthorityReference = "Different frozen proof" } },
            "observed_hash" => receipt with { ObservedAcceptanceSha256 = new string('0', 64) },
            "observed_snapshot" => receipt with { ObservedAcceptance = receipt.ObservedAcceptance with { Status = "closed" } },
            "clock" => receipt with { RecordedAt = receipt.Source.RecordedAt.AddTicks(-1) },
            "actor" => receipt with { Actor = ActorReference.ForSystemProcess("reactor:Unrelated", "Unrelated") },
            "actor_display" => receipt with { Actor = receipt.Actor with { Display = "Changed named attribution" } },
            _ => receipt
        };
        var changed = ev with
        {
            Reevaluation = altered,
            TenantId = change == "tenant" ? Uuid.CreateVersion4() : ev.TenantId,
            ExpectedSequence = change == "sequence" ? ev.ExpectedSequence + 1 : ev.ExpectedSequence
        };
        // Act
        Action replay = () => _ = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant))
            .Given(events[..^1].Append(changed).ToArray()).Aggregate;

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldTraverseAllRegisteredClientsGivenMoreThanTwoHundredAffectedTenants()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        var tenants = new List<Uuid> { fixture.Tenant };
        for (var index = 0; index < 200; index++)
        {
            var tenantId = Uuid.CreateVersion4();
            await fixture.SeedOtherTenantAsync(tenantId);
            await RegisterTenantAsync(fixture, tenantId);
            await fixture.ProjectOtherTenantAsync(tenantId);
            tenants.Add(tenantId);
        }
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        await RecordStatusAsync(fixture, false);

        // Act
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");

        // Assert
        foreach (var tenantId in tenants)
        {
            var ledger = await ProgramManagementServices.HydrateAsync(fixture.Provider, new IndependenceLedger(tenantId));
            var receipt = Assert.Single(ledger.History().DirectoryReevaluations);
            Assert.Equal(tenantId, receipt.TenantId);
            Assert.Equal(fixture.Staff.UserId, receipt.Source.UserId);
        }
        Assert.Equal(201, tenants.Count);
    }

    [Fact]
    public async Task ShouldRetainStatusCursorGivenAcceptanceCommittedDuringLocatorPagination()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var before = await checkpoints.LoadAsync(identity);
        await RecordStatusAsync(fixture, false);
        // Explicit synthetic internal acceptance completes a retained pre-status proof during discovery.
        fixture.Faults.OnNextLocatorScan = () => fixture.SeedOtherTenantAsync(fixture.Tenant);

        // Act
        var pending = await Assert.ThrowsAsync<ReactionCommandFailedException>(() =>
            RunAsync(fixture, "FirmStaffStatusReevaluationV1"));

        // Assert
        Assert.True(pending.Error.IsTransient);
        Assert.Equal(before, await checkpoints.LoadAsync(identity));
        Assert.Equal(2, (await fixture.AcceptanceRecordsAsync()).Count);
        var partial = Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var receipts = (await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations;
        Assert.Equal(2, receipts.Count);
        Assert.Contains(receipts, receipt => receipt.ReevaluationId == partial.ReevaluationId && receipt.RecordedAt == partial.RecordedAt);
        Assert.NotEqual(before, await checkpoints.LoadAsync(identity));
    }

    [Fact]
    public async Task ShouldKeepTargetAndSourceCursorUnchangedGivenReceiptWriterFailureThenRetry()
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var before = await fixture.HydrateLedgerAsync();
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var checkpoint = await checkpoints.LoadAsync(identity);
        await RecordStatusAsync(fixture, false);
        fixture.Faults.FailNextDirectoryReceiptWrite = true;

        // Act
        await Assert.ThrowsAsync<IOException>(() => RunAsync(fixture, "FirmStaffStatusReevaluationV1"));
        var failed = await fixture.HydrateLedgerAsync();

        // Assert
        Assert.Equal(before.Sequence, failed.Sequence);
        Assert.Equal(before.CommittedStreamPosition, failed.CommittedStreamPosition);
        Assert.Empty(failed.History().DirectoryReevaluations);
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
    }

    [Fact]
    public async Task ShouldUseStableCausalIdentityAndFreshTargetSequenceGivenActualAppendConflict()
    {
        // Arrange
        var clock = new Clock();
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: clock);
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        await RecordStatusAsync(fixture, false);
        var command = await CommandAsync(fixture);
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var checkpoint = await checkpoints.LoadAsync(identity);
        fixture.Faults.OnNextDirectoryReceiptWrite = () => ProgramManagementServices.SeedAsync(fixture.Provider,
            new IndependenceLedger(fixture.Tenant), ledger => ledger.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
                ledger.Sequence, new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2010, 1, 1),
                    new DateOnly(2010, 12, 31), [fixture.Staff.StaffMemberId], false, "Synthetic concurrent historical service"),
                ActorReference.ForMember(RbacIds.Member(fixture.Tenant, Uuid.CreateVersion4()), "Synthetic client source"), clock.Now));

        // Act
        await Assert.ThrowsAsync<EventStreamConcurrencyException>(() => RunAsync(fixture, "FirmStaffStatusReevaluationV1"));
        var conflicted = await fixture.HydrateLedgerAsync();

        // Assert
        Assert.Empty(conflicted.History().DirectoryReevaluations);
        Assert.Single(conflicted.History().Services);
        Assert.Single(conflicted.History().SourceReevaluations);
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var retained = await fixture.HydrateLedgerAsync();
        var receipt = Assert.Single(retained.History().DirectoryReevaluations);
        var original = (ServiceEngagementAcceptanceRecorded)Assert.Single(await fixture.AcceptanceRecordsAsync()).Event;
        var cause = receipt.Source.RequestId;
        Assert.Equal(Uuid.CreateVersion5(cause,
            $"directory_reevaluation_v1:{fixture.Tenant}:{original.RequestId}:{original.Acceptance.EngagementId}:{fixture.Staff.StaffMemberId}:{fixture.Staff.UserId}"), receipt.ReevaluationId);
        Assert.Equal(command.AcceptanceRequestId, receipt.OriginalAcceptanceRequestId);
        Assert.Equal(conflicted.Sequence + 1, retained.Sequence);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public async Task ShouldKeepStatusCheckpointGivenIncompleteOrDuplicateRowsDespiteCoveredLocatorSource(string failure)
    {
        // Arrange
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: new Clock());
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var checkpoint = await checkpoints.LoadAsync(identity);
        await RecordStatusAsync(fixture, false);
        fixture.Faults.LocatorPageFault = failure;

        // Act
        await Assert.ThrowsAsync<ReactionCommandFailedException>(() => RunAsync(fixture, "FirmStaffStatusReevaluationV1"));

        // Assert
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
        fixture.Faults.LocatorPageFault = null;
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
        Assert.NotEqual(checkpoint, await checkpoints.LoadAsync(identity));
    }

    [Fact]
    public async Task ShouldPreserveExactRetryAndKeepNextCausePendingGivenOneThousandRetainedReceipts()
    {
        // Arrange
        var clock = new Clock();
        await using var fixture = await ActualStaffEngagementLocatorTests.Fixture.CreateAsync(clock: clock);
        await RegisterTenantAsync(fixture);
        await fixture.ProjectAsync();
        await RecordStatusAsync(fixture, false);
        await RunAsync(fixture, "FirmStaffStatusReevaluationV1");
        var command = await CommandAsync(fixture);
        var first = Assert.Single((await fixture.HydrateLedgerAsync()).History().DirectoryReevaluations);
        var original = (ServiceEngagementAcceptanceRecorded)Assert.Single(await fixture.AcceptanceRecordsAsync()).Event;
        // Domain-only quota fixtures represent distinct verified safe descriptors. They establish no external authority.
        await ProgramManagementServices.SeedAsync(fixture.Provider, new IndependenceLedger(fixture.Tenant), ledger =>
        {
            for (var index = 1; index < 1000; index++)
            {
                var descriptor = first.Source with
                {
                    RequestId = Uuid.CreateVersion4(),
                    EventId = Uuid.CreateVersion4(),
                    SourceSequence = first.Source.SourceSequence + index,
                    StaffRevision = first.Source.StaffRevision + index
                };
                var result = ledger.RecordDirectoryReevaluation(descriptor, original, first.Actor, clock.Now);
                Assert.True(result.IsSuccess);
            }
            return Result.Success;
        });
        var before = await fixture.HydrateLedgerAsync();
        var actor = RequestActor.CreateSystem("reactor:FirmStaffStatusReevaluationV1", "bdgrz.system");
        var context = new RequestDispatchContext(actor, metadata: new RequestMetadata(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), command.DirectoryEventId));
        var global = Resolve(fixture, "FirmStaffStatusReevaluationV1");
        var checkpoints = fixture.Services.GetRequiredService<IProjectionCheckpointStore>();
        var identity = new CheckpointIdentity(global.Name, global.Pattern);
        var checkpoint = await checkpoints.LoadAsync(identity);

        // Act
        var retry = await fixture.Services.GetRequiredService<IRequestBus>().DispatchAsync(command, context);
        await RecordStatusAsync(fixture, true);
        var exhausted = await Assert.ThrowsAsync<ReactionCommandFailedException>(() =>
            RunAsync(fixture, "FirmStaffStatusReevaluationV1"));
        var after = await fixture.HydrateLedgerAsync();

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, exhausted.Error.Kind);
        Assert.Equal(1000, after.History().DirectoryReevaluations.Count);
        Assert.Equal(before.Sequence, after.Sequence);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Equal(first.RecordedAt, after.History().DirectoryReevaluations[0].RecordedAt);
        Assert.Equal(first.ReevaluationId, after.History().DirectoryReevaluations[0].ReevaluationId);
        Assert.Equal(checkpoint, await checkpoints.LoadAsync(identity));
    }

    static async Task<RecordDirectoryIndependenceReevaluation> CommandAsync(ActualStaffEngagementLocatorTests.Fixture fixture)
    {
        var accepted = Assert.Single(await fixture.AcceptanceRecordsAsync());
        var statuses = new List<DomainEventRecord>();
        await foreach (var source in fixture.Services.GetRequiredService<IDomainEventReader>().ReadAsync(new FirmStaffDirectory().Stream, 0))
            if (source.Event is FirmStaffChangeRecorded { Operation: "status" })
                statuses.Add(source);
        var status = statuses.Last();
        var change = (FirmStaffChangeRecorded)status.Event;
        var original = (ServiceEngagementAcceptanceRecorded)accepted.Event;
        return new(fixture.Tenant, status.ResourceOffset, status.Event.Metadata.EventId,
            IndependenceSourceDigest.DirectoryEvent(change), accepted.ResourceOffset, accepted.Event.Metadata.EventId,
            original.RequestId, IndependenceSourceDigest.AcceptanceEvent(original), fixture.Staff.StaffMemberId, fixture.Staff.UserId);
    }

    static Reactor Resolve(ActualStaffEngagementLocatorTests.Fixture fixture, string name)
    {
        var registration = Assert.Single(fixture.Services.GetServices<WorkloadRegistration>(), item => item.Name == name);
        Assert.Equal(int.MaxValue, registration.FailureAttemptLimit);
        Assert.Equal(TimeSpan.FromSeconds(2), registration.MaximumFailureDelay);
        return (Reactor)fixture.Services.GetRequiredService(registration.ComponentType);
    }

    static async Task RunAsync(ActualStaffEngagementLocatorTests.Fixture fixture, string name, Uuid? tenantId = null)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registration = Assert.Single(services.GetServices<WorkloadRegistration>(), item => item.Name == name);
        var reactor = (Reactor)services.GetRequiredService(registration.ComponentType);
        if (registration.Scope == WorkloadScope.PerTenant)
            await new ReactorScenario(new TenantId((tenantId ?? fixture.Tenant).ToString())).RunAsync(reactor);
        var checkpoints = services.GetRequiredService<IProjectionCheckpointStore>();
        var checkpoint = await checkpoints.LoadAsync(new CheckpointIdentity(reactor.Name, reactor.Pattern));
        var runner = new ReactorRunner(services.GetRequiredService<IDomainEventReader>(),
            services.GetRequiredService<IReactorPrincipalProvider>(), services.GetRequiredService<TimeProvider>());
        while (true)
        {
            var next = await runner.RunAsync(reactor, checkpoint);
            if (next == checkpoint)
                return;
            checkpoint = next;
        }
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 13, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static async Task RegisterTenantAsync(ActualStaffEngagementLocatorTests.Fixture fixture, Uuid? tenantId = null)
    {
        var id = tenantId ?? fixture.Tenant;
        await ProgramManagementServices.SeedAsync(fixture.Provider, new Tenant(id), tenant =>
        {
            var slug = $"directory-{id.ToString()[..8]}";
            Assert.True(tenant.Register(Uuid.CreateVersion4(), "Synthetic registered client", slug).IsSuccess);
            return tenant.ConfirmSlug(slug);
        });
    }

    static Task RecordStatusAsync(ActualStaffEngagementLocatorTests.Fixture fixture, bool active) =>
        ProgramManagementServices.SeedAsync(fixture.Provider, new FirmStaffDirectory(), directory =>
            directory.SetStatus(Uuid.CreateVersion4(), fixture.Staff.StaffMemberId, active,
                "Synthetic operator status reason must stay out of client receipts", directory.Sequence,
                ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Private synthetic status operator"),
                new DateTimeOffset(2026, 10, 8, 12, 1, 0, TimeSpan.Zero)));
}

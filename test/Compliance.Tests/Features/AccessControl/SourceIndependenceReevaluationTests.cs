using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class SourceIndependenceReevaluationTests
{
    [Theory]
    [InlineData(false, false, "compatible")]
    [InlineData(true, false, "review_required")]
    [InlineData(true, true, "impaired")]
    public async Task ShouldRetainDenialOnlyCausalReceiptGivenProductionServiceChange(bool recent, bool management, string state)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var accepted = Assert.Single(before.Engagements);
        var acceptance = before.Acceptance(accepted.EngagementId)!;
        var assignment = Assert.Single(acceptance.Assignments);
        Assert.True(before.IsEligibleForProfessionalAccess(accepted.EngagementId, assignment.StaffMemberId,
            assignment.UserId, acceptance.Rules.Version, assignment.DirectoryStaffRevision, DateTimeOffset.UtcNow));
        var serviceId = Uuid.CreateVersion4();
        var request = new RecordNonattestService(fixture.Tenant, serviceId, before.Sequence,
            new NonattestServiceContent(Uuid.CreateVersion4(), "readiness",
                recent ? new DateOnly(2025, 11, 1) : new DateOnly(2020, 1, 1), recent ? new DateOnly(2025, 12, 1) : new DateOnly(2020, 12, 1),
                [Uuid.CreateVersion4()], management, "Attributed service fact"));
        var dispatch = new RequestDispatchContext(ProgramManagementServices.Actor(fixture.User),
            new McpInvocation("bdgrz.independence.service.record"));

        // Act
        var result = await fixture.Bus.DispatchAsync(request, dispatch, CancellationToken.None);
        var history = await fixture.Bus.SendAsync(new GetClientIndependenceHistory(fixture.Tenant), ProgramManagementServices.Actor(fixture.User));
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(history.IsSuccess, history.Error?.Message);
        var receipt = Assert.Single(history.Value!.SourceReevaluations);
        Assert.Equal(state, receipt.State);
        Assert.True(receipt.ProductionAcceptanceBlocked);
        Assert.Equal(serviceId, receipt.ServiceRecordId);
        Assert.Equal(fixture.Tenant, receipt.TenantId);
        Assert.Equal(accepted.EngagementId, receipt.EngagementId);
        Assert.Equal(acceptance.Revision, receipt.AcceptanceRevision);
        Assert.Equal(acceptance.Rules.Version, receipt.AcceptedRules.Version);
        Assert.Equal(acceptance.Rules.Content.ServiceRules.ToArray(), receipt.AcceptedRules.Content.ServiceRules.ToArray());
        Assert.Equal(accepted.Content.PeriodStart, receipt.ExaminationPeriodStart);
        Assert.Equal(serviceId, Assert.Single(receipt.CompleteServiceHistory).ServiceRecordId);
        Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.User).ToString(), receipt.SourceActor.Id);
        Assert.Equal(before.Sequence + 1, receipt.SourceSequence);
        Assert.Equal(before.CommittedStreamPosition + 2, retained.CommittedStreamPosition);
        Assert.Equal(acceptance.Revision, retained.Acceptance(accepted.EngagementId)!.Revision);
        Assert.Equal(acceptance.ReviewTaskId, retained.Acceptance(accepted.EngagementId)!.ReviewTaskId);
        Assert.Empty(retained.Acceptance(accepted.EngagementId)!.CompleteServiceHistory);
        Assert.False(retained.IsEligibleForProfessionalAccess(accepted.EngagementId, assignment.StaffMemberId,
            assignment.UserId, acceptance.Rules.Version, assignment.DirectoryStaffRevision, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("intent")]
    [InlineData("actor")]
    public async Task ShouldPreserveOriginalServiceDecisionGivenCausalRequestRetry(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var request = fixture.Service(before.Sequence);
        var context = fixture.Context();
        Assert.True((await fixture.Bus.DispatchAsync(request, context, CancellationToken.None)).IsSuccess);
        var recorded = await fixture.ReadAsync();
        var original = Assert.Single(recorded.History().SourceReevaluations);
        var retried = change == "intent" ? request with { Content = request.Content with { SourceReference = "Changed intent" } } : request;
        var retryContext = change == "actor" ? new RequestDispatchContext(ProgramManagementServices.Actor(fixture.OtherUser),
            context.Invocation, context.Metadata) : context;

        // Act
        var result = await fixture.Bus.DispatchAsync(retried, retryContext, CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(change == "same", result.IsSuccess);
        if (change != "same")
        {
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.Contains("intent or actor", result.Error!.Message, StringComparison.Ordinal);
        }
        Assert.Equal(recorded.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Equal(context.RequestId, Assert.Single(retained.History().SourceReevaluations).CausalRequestId);
        Assert.Equal(original.ReevaluationId, Assert.Single(retained.History().SourceReevaluations).ReevaluationId);
    }

    [Fact]
    public async Task ShouldRetainExactReceiptGivenDurableEventReplayAndImmutableHistory()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        Assert.True((await fixture.Bus.DispatchAsync(fixture.Service(before.Sequence), fixture.Context(), CancellationToken.None)).IsSuccess);
        var retained = await fixture.ReadAsync();
        var events = await fixture.EventsAsync();

        // Act
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant)).Given(events).Aggregate;
        var history = replay.History();
        var receipt = Assert.Single(history.SourceReevaluations);

        // Assert
        Assert.Equal(JsonSerializer.Serialize(Assert.Single(retained.History().SourceReevaluations),
            ComplianceCoreJsonContext.Default.ServiceIndependenceReevaluationView),
            JsonSerializer.Serialize(receipt, ComplianceCoreJsonContext.Default.ServiceIndependenceReevaluationView));
        Assert.Throws<NotSupportedException>(() => ((IList<ServiceIndependenceReevaluationView>)history.SourceReevaluations).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<NonattestServiceView>)receipt.CompleteServiceHistory).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<Uuid>)receipt.CompleteServiceHistory[0].Content.FirmStaffMemberIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<IndependenceServiceRuleContent>)receipt.AcceptedRules.Content.ServiceRules).Clear());
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("cause")]
    [InlineData("source_sequence")]
    [InlineData("acceptance_hash")]
    [InlineData("acceptance_revision")]
    [InlineData("actor")]
    [InlineData("period")]
    [InlineData("history")]
    [InlineData("approval")]
    public async Task ShouldRefuseReceiptReplayGivenChangedAuthoritativeSource(string change)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        Assert.True((await fixture.Bus.DispatchAsync(fixture.Service(before.Sequence), fixture.Context(), CancellationToken.None)).IsSuccess);
        var events = await fixture.EventsAsync();
        var ev = Assert.IsType<ServiceIndependenceReevaluated>(events[^1]);
        var receipt = ev.Reevaluation;
        var corrupt = change switch
        {
            "tenant" => receipt with { TenantId = Uuid.CreateVersion4() },
            "cause" => receipt with { CausalRequestId = Uuid.CreateVersion4() },
            "source_sequence" => receipt with { SourceSequence = receipt.SourceSequence - 1 },
            "acceptance_hash" => receipt with { AcceptanceSha256 = new string('b', 64) },
            "acceptance_revision" => receipt with { AcceptanceRevision = receipt.AcceptanceRevision + 1 },
            "actor" => receipt with { SourceActor = ActorReference.ForMember(Uuid.CreateVersion4(), "Different source actor") },
            "period" => receipt with { ExaminationPeriodStart = receipt.ExaminationPeriodStart.AddDays(1) },
            "history" => receipt with { CompleteServiceHistory = [] },
            _ => receipt with { ProductionAcceptanceBlocked = false }
        };
        events[^1] = ev with { Reevaluation = corrupt };

        // Act
        var replay = () => new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant)).Given(events);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public async Task ShouldRetainNoCauseOrReceiptGivenCompleteTransactionPayloadExceedsBound()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var staff = Enumerable.Range(0, 100).Select(_ => Uuid.CreateVersion4()).ToArray();
        var denied = false;

        // Act
        for (var index = 0; index < 100; index++)
        {
            var before = await fixture.ReadAsync();
            var request = fixture.Service(before.Sequence);
            request = request with { Content = request.Content with { FirmStaffMemberIds = staff, SourceReference = new string('x', 2000) } };
            var result = await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None);
            if (result.IsSuccess)
                continue;
            var retained = await fixture.ReadAsync();

            // Assert
            Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
            Assert.Contains("transaction", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before.Sequence, retained.Sequence);
            Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
            Assert.Equal(before.History().Services.Count, retained.History().Services.Count);
            Assert.Equal(before.History().SourceReevaluations.Count, retained.History().SourceReevaluations.Count);
            denied = true;
            break;
        }
        Assert.True(denied, "The bounded complete transaction must reject without truncating history.");
    }

    [Fact]
    public async Task ShouldRetainNeitherCauseNorReceiptGivenProductionWriterFailureThenRecover()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var request = fixture.Service(before.Sequence);
        var context = fixture.Context();
        fixture.Faults.FailNextSave = true;

        // Act
        await Assert.ThrowsAsync<IOException>(async () => await fixture.Bus.DispatchAsync(request, context, CancellationToken.None));
        var failed = await fixture.ReadAsync();
        var retried = await fixture.Bus.DispatchAsync(request, context, CancellationToken.None);
        var recovered = await fixture.ReadAsync();

        // Assert
        Assert.IsType<NonattestServiceRecorded>(fixture.Faults.PendingAtFailure[0]);
        Assert.IsType<ServiceIndependenceReevaluated>(fixture.Faults.PendingAtFailure[1]);
        Assert.Equal(2, fixture.Faults.PendingAtFailure.Length);
        Assert.Equal(before.CommittedStreamPosition, failed.CommittedStreamPosition);
        Assert.Empty(failed.History().Services);
        Assert.Empty(failed.History().SourceReevaluations);
        Assert.True(retried.IsSuccess, retried.Error?.Message);
        Assert.Single(recovered.History().Services);
        Assert.Single(recovered.History().SourceReevaluations);
        Assert.Equal(before.CommittedStreamPosition + 2, recovered.CommittedStreamPosition);
    }

    [Fact]
    public void ShouldDefaultToEmptySourceReceiptCollectionGivenHistoricalHistoryJson()
    {
        // Arrange
        var payload = "{\"tenant_id\":\"" + Uuid.CreateVersion4() + "\",\"sequence\":0,\"rule_versions\":[],\"services\":[],\"evaluations\":[]}";

        // Act
        var history = JsonSerializer.Deserialize(payload, ComplianceCoreJsonContext.Default.IndependenceHistoryView);

        // Assert
        Assert.NotNull(history);
        Assert.Empty(history.SourceReevaluations);
    }

    [Fact]
    public async Task ShouldRequireNewReviewGivenChangedConditionalFactsAndOldPartnerReference()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(priorPartnerReference: true);
        var before = await fixture.ReadAsync();
        var accepted = Assert.Single(before.Engagements);
        Assert.NotNull(before.Acceptance(accepted.EngagementId)!.PartnerEvaluationReference);
        var request = fixture.Service(before.Sequence);
        request = request with { Content = request.Content with { StartedOn = new DateOnly(2025, 11, 1), EndedOn = new DateOnly(2025, 12, 1) } };

        // Act
        var result = await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var receipt = Assert.Single(retained.History().SourceReevaluations);
        Assert.Equal("partner_evaluation_required", receipt.DecisionCode);
        Assert.Equal("review_required", receipt.State);
        Assert.True(receipt.ProductionAcceptanceBlocked);
        Assert.Equal(before.Acceptance(accepted.EngagementId)!.PartnerEvaluationReference,
            retained.Acceptance(accepted.EngagementId)!.PartnerEvaluationReference);
        Assert.Equal(before.Acceptance(accepted.EngagementId)!.Revision, retained.Acceptance(accepted.EngagementId)!.Revision);
    }

    [Theory]
    [InlineData("advisory", false)]
    [InlineData("attest", true)]
    public async Task ShouldRecordOnlySourceFactGivenNoActiveAcceptedAttestTarget(string practice, bool closed)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(practice: practice, closed: closed);
        var before = await fixture.ReadAsync();

        // Act
        var result = await fixture.Bus.DispatchAsync(fixture.Service(before.Sequence), fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Single(retained.History().Services);
        Assert.Empty(retained.History().SourceReevaluations);
        Assert.Equal(before.CommittedStreamPosition + 1, retained.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldDenyWholeSourceChangeGivenRecordedTimeBeforeAcceptedSource()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        fixture.Clock.Now = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        // Act
        var result = await fixture.Bus.DispatchAsync(fixture.Service(before.Sequence), fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Empty(retained.History().Services);
        Assert.Empty(retained.History().SourceReevaluations);
    }

    [Fact]
    public async Task ShouldDenyWholeSourceChangeGivenRecordedTimeBeforeLaterServiceFact()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Clock.Now = DateTimeOffset.UtcNow.AddHours(1);
        var initial = await fixture.ReadAsync();
        Assert.True((await fixture.Bus.DispatchAsync(fixture.Service(initial.Sequence), fixture.Context(), CancellationToken.None)).IsSuccess);
        var before = await fixture.ReadAsync();
        fixture.Clock.Now = DateTimeOffset.UtcNow;

        // Act
        var result = await fixture.Bus.DispatchAsync(fixture.Service(before.Sequence), fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Single(retained.History().Services);
        Assert.Single(retained.History().SourceReevaluations);
    }

    [Fact]
    public async Task ShouldCommitExactlyOneCompleteCausalTransactionGivenConcurrentSourceChanges()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var requests = new[] { fixture.Service(before.Sequence), fixture.Service(before.Sequence) };
        fixture.Gate.Enabled = true;

        // Act
        var outcomes = await Task.WhenAll(requests.Select(async request =>
        {
            try
            {
                return (await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None)).IsSuccess;
            }
            catch (EventStreamConcurrencyException)
            {
                return false;
            }
        }));
        fixture.Gate.Enabled = false;
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Single(outcomes, success => success);
        Assert.Single(outcomes, success => !success);
        var service = Assert.Single(retained.History().Services);
        var receipt = Assert.Single(retained.History().SourceReevaluations);
        Assert.Equal(service.ServiceRecordId, receipt.ServiceRecordId);
        Assert.Equal(before.CommittedStreamPosition + 2, retained.CommittedStreamPosition);
        Assert.Equal(before.Sequence + 2, retained.Sequence);
    }

    [Fact]
    public async Task ShouldPreserveOwningSourceGivenForeignTenantMutationAndHistoryRead()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var foreign = Uuid.CreateVersion4();
        var request = fixture.Service(before.Sequence) with { TenantId = foreign };

        // Act
        var write = await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None);
        var read = await fixture.Bus.SendAsync(new GetClientIndependenceHistory(foreign), ProgramManagementServices.Actor(fixture.User));
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, write.Error?.Kind);
        Assert.Equal(RequestErrorKind.NotFound, read.Error?.Kind);
        Assert.Equal(before.CommittedStreamPosition, retained.CommittedStreamPosition);
        Assert.Empty(retained.History().Services);
        Assert.Empty(retained.History().SourceReevaluations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireFreshReviewGivenNewServiceBeginsDuringAcceptedPeriod(bool management)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(priorPartnerReference: true);
        var before = await fixture.ReadAsync();
        var request = fixture.Service(before.Sequence);
        request = request with { Content = request.Content with { StartedOn = new DateOnly(2026, 9, 1), EndedOn = null, InvolvedManagementFunctions = management } };

        // Act
        var result = await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var receipt = Assert.Single(retained.History().SourceReevaluations);
        Assert.Equal("review_required", receipt.State);
        Assert.True(receipt.ProductionAcceptanceBlocked);
        Assert.Equal(new DateOnly(2026, 1, 1), receipt.ExaminationPeriodStart);
        Assert.Equal(receipt.ExaminationPeriodStart, receipt.PolicyReferenceDate);
        Assert.Equal("allowed", receipt.PeriodStartLookbackDecisionCode);
        Assert.Equal("during_period_service_requires_review", receipt.DecisionCode);
        Assert.True(receipt.RequiresDuringPeriodReview);
    }

    [Theory]
    [InlineData(false, 2026, true)]
    [InlineData(true, 2027, true)]
    [InlineData(false, 2027, false)]
    public async Task ShouldPreserveOriginalPeriodBoundsGivenNewEndedOrFutureService(bool openEnded, int year, bool requiresReview)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(openEnded: openEnded);
        var before = await fixture.ReadAsync();
        var request = fixture.Service(before.Sequence);
        request = request with { Content = request.Content with { StartedOn = new DateOnly(year, 9, 1), EndedOn = new DateOnly(year, 9, 20) } };

        // Act
        var result = await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var receipt = Assert.Single(retained.History().SourceReevaluations);
        Assert.Equal(openEnded ? null : new DateOnly(2026, 12, 31), receipt.ExaminationPeriodEnd);
        Assert.Equal(new DateOnly(2026, 1, 1), receipt.PolicyReferenceDate);
        Assert.Equal("allowed", receipt.PeriodStartLookbackDecisionCode);
        Assert.Equal(requiresReview, receipt.RequiresDuringPeriodReview);
        Assert.Equal(requiresReview ? "review_required" : "compatible", receipt.State);
        Assert.True(receipt.ProductionAcceptanceBlocked);
    }

    [Fact]
    public async Task ShouldRetainPendingDuringPeriodReviewGivenSubsequentOutsidePeriodCause()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var request = fixture.Service(before.Sequence);
        request = request with { Content = request.Content with { StartedOn = new DateOnly(2026, 9, 1), EndedOn = null } };
        Assert.True((await fixture.Bus.DispatchAsync(request, fixture.Context(), CancellationToken.None)).IsSuccess);
        var changed = await fixture.ReadAsync();

        // Act
        var result = await fixture.Bus.DispatchAsync(fixture.Service(changed.Sequence), fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, retained.History().SourceReevaluations.Count);
        Assert.All(retained.History().SourceReevaluations, receipt =>
        {
            Assert.Equal("review_required", receipt.State);
            Assert.True(receipt.RequiresDuringPeriodReview);
            Assert.True(receipt.ProductionAcceptanceBlocked);
        });
    }

    [Fact]
    public async Task ShouldRetainEveryImpactedAcceptedSnapshotGivenConsecutiveProductionServices()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(acceptedCount: 2);
        var before = await fixture.ReadAsync();
        var firstRequest = fixture.Service(before.Sequence);
        var firstContext = fixture.Context();

        // Act
        var first = await fixture.Bus.DispatchAsync(firstRequest, firstContext, CancellationToken.None);
        var firstHistory = await fixture.Bus.SendAsync(new GetClientIndependenceHistory(fixture.Tenant), ProgramManagementServices.Actor(fixture.User));
        var secondRequest = fixture.Service(firstHistory.Value!.Sequence);
        var second = await fixture.Bus.DispatchAsync(secondRequest, fixture.Context(), CancellationToken.None);
        var retry = await fixture.Bus.DispatchAsync(firstRequest, firstContext, CancellationToken.None);
        var retained = await fixture.ReadAsync();
        var events = await fixture.EventsAsync();

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        Assert.Equal(JsonSerializer.Serialize(first.Value, ComplianceCoreJsonContext.Default.NonattestServiceView),
            JsonSerializer.Serialize(retry.Value, ComplianceCoreJsonContext.Default.NonattestServiceView));
        Assert.Equal(before.Sequence + 3, firstHistory.Value.Sequence);
        Assert.Equal(before.Sequence + 6, retained.Sequence);
        Assert.Equal(2, retained.History().Services.Count);
        Assert.Equal(4, retained.History().SourceReevaluations.Count);
        foreach (var group in retained.History().SourceReevaluations.GroupBy(receipt => receipt.ServiceRecordId))
        {
            Assert.Equal(before.Engagements.Select(engagement => engagement.EngagementId).OrderBy(id => id.ToString(), StringComparer.Ordinal),
                group.Select(receipt => receipt.EngagementId));
            Assert.Single(group.Select(receipt => receipt.SourceSequence).Distinct());
            Assert.All(group, receipt => Assert.True(receipt.ProductionAcceptanceBlocked));
        }
        var replay = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(fixture.Tenant)).Given(events).Aggregate;
        Assert.Equal(JsonSerializer.Serialize(retained.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView),
            JsonSerializer.Serialize(replay.History(), ComplianceCoreJsonContext.Default.IndependenceHistoryView));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveKnownImpairmentGivenMixedClassifiedLookbackHistory(bool unknownFirst)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ReadAsync();
        var impaired = fixture.Service(before.Sequence);
        impaired = impaired with
        {
            Content = impaired.Content with
            {
                StartedOn = new DateOnly(2025, 11, 1),
                EndedOn = new DateOnly(2025, 12, 1),
                InvolvedManagementFunctions = true
            }
        };
        if (unknownFirst)
            impaired = impaired with { Content = impaired.Content with { ServiceType = "unclassified", InvolvedManagementFunctions = false } };
        Assert.True((await fixture.Bus.DispatchAsync(impaired, fixture.Context(), CancellationToken.None)).IsSuccess);
        var changed = await fixture.ReadAsync();
        var unknown = fixture.Service(changed.Sequence);
        unknown = unknown with
        {
            Content = unknown.Content with
            {
                ServiceType = unknownFirst ? "readiness" : "unclassified",
                InvolvedManagementFunctions = unknownFirst,
                StartedOn = new DateOnly(2025, 10, 1),
                EndedOn = new DateOnly(2025, 12, 1)
            }
        };

        // Act
        var result = await fixture.Bus.DispatchAsync(unknown, fixture.Context(), CancellationToken.None);
        var retained = await fixture.ReadAsync();

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, retained.History().SourceReevaluations.Count);
        Assert.Equal(unknownFirst ? "review_required" : "impaired", retained.History().SourceReevaluations[0].State);
        var receipt = retained.History().SourceReevaluations[1];
        Assert.Equal("impaired", receipt.State);
        Assert.Equal("recent_impairing_service", receipt.DecisionCode);
        Assert.Equal("service_not_classified", receipt.PeriodStartLookbackDecisionCode);
        Assert.All(retained.History().SourceReevaluations, item => Assert.True(item.ProductionAcceptanceBlocked));
    }

    sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider _provider;
        readonly AsyncServiceScope _scope;
        public Uuid Tenant { get; } = Uuid.CreateVersion4();
        public Uuid User { get; } = Uuid.CreateVersion4();
        public Uuid OtherUser { get; } = Uuid.CreateVersion4();
        public Faults Faults { get; } = new();
        public Clock Clock { get; } = new();
        public Gate Gate { get; } = new();
        public IRequestBus Bus { get; }
        readonly IAggregateReader _reader;

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
            services.AddSingleton<IPermissionAuthorizer>(new Permissions());
            services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(Tenant, User, OtherUser));
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
            _reader = _scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        }

        public static async Task<Fixture> CreateAsync(bool priorPartnerReference = false, string practice = "attest", bool closed = false, bool openEnded = false, int acceptedCount = 1)
        {
            var fixture = new Fixture();
            var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
            var client = ActorReference.ForMember(RbacIds.Member(fixture.Tenant, fixture.User), "Synthetic client administrator");
            var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), practice, "Synthetic assignee", true,
                1, ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), now);
            var partner = staff with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() };
            await ProgramManagementServices.SeedAsync(fixture._provider, new IndependenceLedger(fixture.Tenant), ledger =>
            {
                for (var index = 0; index < acceptedCount; index++)
                {
                    var engagement = Uuid.CreateVersion4();
                    var acknowledgement = Uuid.CreateVersion4();
                    Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, ledger.Sequence,
                        new ServiceEngagementDraftContent(practice, "Synthetic scope", new DateOnly(2026, 1, 1),
                            openEnded ? null : new DateOnly(2026, 12, 31), staff.StaffMemberId), staff, client, now).IsSuccess);
                    Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(), new AcknowledgeEngagementManagement(
                        fixture.Tenant, engagement, acknowledgement, ledger.Sequence, 1, [], "I retain management responsibility"),
                        fixture.User, client, now).IsSuccess);
                    var proof = new VerifiedEngagementAcceptance(fixture.Tenant, engagement, 1, Uuid.CreateVersion4(),
                        partner.StaffMemberId, partner.UserId, "Synthetic verified authority ONLY",
                        priorPartnerReference ? "Synthetic historical partner reference ONLY" : null,
                        acknowledgement, null, null, [staff], partner, 1, now);
                    var rules = new IndependenceRuleVersionView(1, new IndependenceRuleContent(12,
                        [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
                        "Synthetic ratified test rules ONLY"), staff.Actor, now, true);
                    Assert.True(ledger.AcceptEngagement(Uuid.CreateVersion4(), ledger.Sequence, proof, rules, now).IsSuccess);
                    if (closed)
                        Assert.True(ledger.CloseEngagement(Uuid.CreateVersion4(), engagement, ledger.Sequence, "Synthetic client closure", client, now).IsSuccess);
                }
                return Result.Success;
            });
            return fixture;
        }

        public RecordNonattestService Service(long sequence) => new(Tenant, Uuid.CreateVersion4(), sequence,
            new NonattestServiceContent(Uuid.CreateVersion4(), "readiness", new DateOnly(2020, 1, 1),
                new DateOnly(2020, 12, 1), [Uuid.CreateVersion4()], false, "Attributed service source"));
        public RequestDispatchContext Context() => new(ProgramManagementServices.Actor(User), new McpInvocation("bdgrz.independence.service.record"));
        public async Task<DomainEvent[]> EventsAsync()
        {
            var events = new List<DomainEvent>();
            await foreach (var record in _provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                new IndependenceLedger(Tenant).Stream, 0, CancellationToken.None))
                events.Add(record.Event);
            return events.ToArray();
        }
        public ValueTask<IndependenceLedger> ReadAsync() => _reader.HydrateAsync(new IndependenceLedger(Tenant));
        public async ValueTask DisposeAsync() { await _scope.DisposeAsync(); await _provider.DisposeAsync(); }
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

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class Faults
    {
        public bool FailNextSave { get; set; }
        public DomainEvent[] PendingAtFailure { get; set; } = [];
    }
    sealed class FaultingWriter(IAggregateWriter inner, Faults faults) : IAggregateWriter
    {
        public ValueTask SaveAsync<T>(T aggregate, IExecutionContext context, CancellationToken ct = default) where T : Aggregate
        {
            if (aggregate is IndependenceLedger ledger && faults.FailNextSave)
            {
                faults.FailNextSave = false;
                faults.PendingAtFailure = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
                throw new IOException("Synthetic source append failure");
            }
            return inner.SaveAsync(aggregate, context, ct);
        }
    }

    sealed class Activity : ITenantActivity
    {
        public ValueTask<bool> IsActiveAsync(Uuid tenantId, CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class Permissions : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => ValueTask.FromResult(true);
    }
    sealed class Memberships(Uuid tenant, Uuid user, Uuid other) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(tenantId == tenant.ToString() && (userId == user || userId == other)
                ? new TenantMembershipView(userId, tenant, "client_personnel") : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) => ValueTask.FromResult(tenantId == tenant.ToString() && (userId == user || userId == other));
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor, CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

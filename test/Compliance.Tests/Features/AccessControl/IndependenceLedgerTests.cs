using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceLedgerTests
{
    static readonly Uuid Client = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Synthetic administrator");
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    static readonly IndependenceRuleContent Rules = new(12,
        [new("design", "impairing", "impairing"), new("readiness", "conditionally_compatible", "impairing")],
        "Synthetic draft source");

    [Fact]
    public void ShouldRetainFullHistoryAndRuleVersionGivenServiceOutsideWindowAndSuccessorRules()
    {
        // Arrange
        var ledger = new IndependenceLedger(Client);
        var catalog = Catalog();
        var rules = catalog.Current!;
        var oldId = Uuid.CreateVersion4();
        Assert.True(ledger.RecordService(Uuid.CreateVersion4(), oldId, 0,
            Service("design", new DateOnly(2023, 1, 1), new DateOnly(2023, 2, 1)), Actor, Now).IsSuccess);

        // Act
        var evaluated = ledger.Evaluate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, rules,
            new DateOnly(2026, 10, 7), Actor, Now);
        Assert.True(catalog.ReviseRules(Uuid.CreateVersion4(), 1, Rules with { LookBackMonths = 48 },
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now).IsSuccess);
        var history = ledger.History();

        // Assert
        Assert.True(evaluated.IsSuccess);
        Assert.Equal("allowed", evaluated.Value!.DecisionCode);
        Assert.Equal("compatible", evaluated.Value.Outcome);
        Assert.Equal(oldId, Assert.Single(evaluated.Value.CompleteServiceHistory).ServiceRecordId);
        Assert.Empty(evaluated.Value.ConsideredServiceRecordIds);
        Assert.True(evaluated.Value.ProductionAcceptanceBlocked);
        Assert.All(history.RuleVersions, version => Assert.False(version.IsRatified));
        Assert.Equal(1, Assert.Single(history.Evaluations).RuleSetVersion);
        Assert.Equal(2, ledger.Sequence);
    }

    [Theory]
    [InlineData("design", "recent_impairing_service", "impaired")]
    [InlineData("readiness", "partner_evaluation_required", "conditionally_compatible")]
    [InlineData("unknown", "service_not_classified", null)]
    public void ShouldRetainDenialGivenRecentService(string kind, string code, string? outcome)
    {
        // Arrange
        var ledger = new IndependenceLedger(Client);
        var catalog = Catalog();
        var rules = catalog.Current!;
        Assert.True(ledger.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Service(kind, new DateOnly(2026, 1, 1), null), Actor, Now).IsSuccess);

        // Act
        var result = ledger.Evaluate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, rules,
            new DateOnly(2026, 10, 7), Actor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(code, result.Value!.DecisionCode);
        Assert.Equal(outcome, result.Value.Outcome);
        Assert.Single(result.Value.ConsideredServiceRecordIds);
        Assert.True(result.Value.ProductionAcceptanceBlocked);
    }

    [Fact]
    public void ShouldProtectRetainedRuleSnapshotGivenMutableTrustedInput()
    {
        // Arrange
        var entries = Rules.ServiceRules.ToList();
        var version = Catalog().Current! with { Content = Rules with { ServiceRules = entries } };
        var ledger = new IndependenceLedger(Client);
        Assert.True(ledger.Evaluate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, version,
            new DateOnly(2026, 10, 7), Actor, Now).IsSuccess);

        // Act
        entries.Clear();

        // Assert
        Assert.Equal(2, Assert.Single(ledger.History().Evaluations).EvaluatedRules.Content.ServiceRules.Count);
    }

    [Fact]
    public void ShouldProtectHydratedServiceHistoryGivenMutableDeserializedEvent()
    {
        // Arrange
        var staff = new List<Uuid> { Uuid.CreateVersion4() };
        var content = Service("design", new DateOnly(2026, 1, 1), null) with { FirmStaffMemberIds = staff };
        var ev = new NonattestServiceRecorded(Client, Uuid.CreateVersion4(), 0,
            new NonattestServiceView(Client, Uuid.CreateVersion4(), content, Actor, Now));
        var ledger = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Client)).Given(ev).Aggregate;

        // Act
        staff.Clear();

        // Assert
        Assert.Single(Assert.Single(ledger.History().Services).Content.FirmStaffMemberIds);
    }

    [Fact]
    public void ShouldRetainOriginalResponsesGivenReplayAndIdempotentRetriesAfterLaterChanges()
    {
        // Arrange
        var ledger = new IndependenceLedger(Client);
        var requestId = Uuid.CreateVersion4();
        var serviceId = Uuid.CreateVersion4();
        var content = Service("design", new DateOnly(2026, 1, 1), null);
        Assert.True(ledger.RecordService(requestId, serviceId, 0, content, Actor, Now).IsSuccess);
        Assert.True(ledger.Evaluate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, Catalog().Current!,
            new DateOnly(2026, 10, 7), Actor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<IndependenceLedger>(new IndependenceLedger(Client)).Given(events).Aggregate;

        // Act
        var retry = hydrated.RecordService(requestId, serviceId, 0,
            content with { FirmStaffMemberIds = content.FirmStaffMemberIds.ToArray() }, Actor, Now.AddDays(1));
        var changed = hydrated.RecordService(requestId, serviceId, 0,
            content with { SourceReference = "Different intent" }, Actor, Now);
        var differentActor = hydrated.RecordService(requestId, serviceId, 0, content,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Other actor"), Now);
        var stale = hydrated.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0, content, Actor, Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.Equal(Now, retry.Value!.RecordedAt);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, differentActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.Equal(2, hydrated.Sequence);
        Assert.Single(hydrated.History().Services);
        Assert.Single(hydrated.History().Evaluations);
        Assert.Empty(new AggregateScenario<IndependenceLedger>(hydrated).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCrossTenantReplayGivenServiceEventFromAnotherClient()
    {
        // Arrange
        var source = new IndependenceLedger(Client);
        Assert.True(source.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 0,
            Service("design", new DateOnly(2026, 1, 1), null), Actor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceLedger>(source).PendingEvents.ToArray();
        var other = new IndependenceLedger(Uuid.CreateVersion4());

        // Act
        var hydrate = () => new AggregateScenario<IndependenceLedger>(other).Given(events);

        // Assert
        Assert.Throws<InvalidOperationException>(hydrate);
    }

    [Fact]
    public void ShouldRejectIncompleteEvaluationGivenCompleteHistoryExceedsEventBudget()
    {
        // Arrange
        var ledger = new IndependenceLedger(Client);
        for (var index = 0; index < 30; index++)
            Assert.True(ledger.RecordService(Uuid.CreateVersion4(), Uuid.CreateVersion4(), index,
                Service("design", new DateOnly(2023, 1, 1), new DateOnly(2023, 2, 1)) with
                { SourceReference = new string('s', 2000) }, Actor, Now).IsSuccess);
        var before = new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.Count;

        // Act
        var result = ledger.Evaluate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 30, Catalog().Current!,
            new DateOnly(2026, 10, 7), Actor, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(before, new AggregateScenario<IndependenceLedger>(ledger).PendingEvents.Count);
        Assert.Empty(ledger.History().Evaluations);
        Assert.Equal(30, ledger.History().Services.Count);
    }

    [Theory]
    [InlineData(11, "impairing")]
    [InlineData(1201, "impairing")]
    [InlineData(12, "compatible")]
    public void ShouldRejectRuleWeakeningGivenLookBackOrManagementFunctionWallInvalid(int lookBack,
        string managementClassification)
    {
        // Arrange
        var catalog = new IndependenceRuleCatalog();
        var rules = Rules with
        {
            LookBackMonths = lookBack,
            ServiceRules = [new("design", "impairing", managementClassification)]
        };

        // Act
        var result = catalog.ReviseRules(Uuid.CreateVersion4(), 0, rules,
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Empty(catalog.Versions);
        Assert.Empty(new AggregateScenario<IndependenceRuleCatalog>(catalog).PendingEvents);
    }

    [Fact]
    public void ShouldRetainUnratifiedVersionsGivenCatalogReplayAndRetry()
    {
        // Arrange
        var operatorActor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        var requestId = Uuid.CreateVersion4();
        var source = new IndependenceRuleCatalog();
        Assert.True(source.ReviseRules(requestId, 0, Rules, operatorActor, Now).IsSuccess);
        Assert.True(source.ReviseRules(Uuid.CreateVersion4(), 1, Rules with { LookBackMonths = 24 }, operatorActor, Now).IsSuccess);
        var events = new AggregateScenario<IndependenceRuleCatalog>(source).PendingEvents.ToArray();
        var hydrated = new AggregateScenario<IndependenceRuleCatalog>(new IndependenceRuleCatalog()).Given(events).Aggregate;

        // Act
        var result = hydrated.ReviseRules(requestId, 0, Rules, operatorActor, Now.AddDays(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Version);
        Assert.Equal(Now, result.Value.RecordedAt);
        Assert.False(result.Value.IsRatified);
        Assert.Equal(2, hydrated.Sequence);
        Assert.All(hydrated.Versions, version => Assert.False(version.IsRatified));
        Assert.Empty(new AggregateScenario<IndependenceRuleCatalog>(hydrated).PendingEvents);
    }

    static IndependenceRuleCatalog Catalog()
    {
        var catalog = new IndependenceRuleCatalog();
        Assert.True(catalog.ReviseRules(Uuid.CreateVersion4(), 0, Rules,
            ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator"), Now).IsSuccess);
        return catalog;
    }

    static NonattestServiceContent Service(string kind, DateOnly start, DateOnly? end) =>
        new(Uuid.CreateVersion4(), kind, start, end, [Uuid.CreateVersion4()], false, "Synthetic source");
}

using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportSourceClaimTests
{
    [Fact]
    public void ShouldReuseCommittedTargetGivenRepeatedSourceRecord()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = Stage(tenant, "app-1", "Observed again");

        // Act
        var result = ledger.PrepareAcceptancePlan(batch, 1);

        // Assert
        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value);
        Assert.Equal(target.Id, row.ApplicationId);
        Assert.Equal("link_existing", row.Decision);
        Assert.Equal(1, target.Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldRejectRebindingGivenCommittedSourceIdentity(bool createNew)
    {
        // Arrange
        var (tenant, ledger, _, _) = Committed();
        var batch = Stage(tenant, "app-1", "Another observation");
        var other = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(other.Declare("Other", "Purpose", null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var request = new CorrelateApplicationImportRow(tenant, batch.Id, Assert.Single(batch.GetRows()).RowId,
            1, createNew ? "create_new" : "link_existing", createNew ? null : other.Id,
            createNew ? null : 1, "Replace identity");

        // Act
        var result = ledger.Correlate(batch, request, createNew ? null : other,
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    [Theory]
    [InlineData("partial", 0)]
    [InlineData("declared_complete", 1)]
    public void ShouldProposeMissingClaimsGivenSourceCoverage(string coverage, int count)
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        var batch = Stage(tenant, "app-2", "Another source record", coverage);

        // Act
        var result = ledger.GetMissingSourceClaims(batch);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(count, result.Value.Count);
        Assert.False(target.IsRetired);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        if (count > 0)
        {
            Assert.Equal("app-1", result.Value[0].Observation.SourceRecordId);
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
                Assert.Single(batch.GetRows()).RowId, 1, "create_new", null, null, "New record"),
                null, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
            Assert.False(ledger.PrepareAcceptancePlan(batch, 2).IsSuccess);
        }
    }

    [Theory]
    [InlineData("Observed name", false)]
    [InlineData(" Observed name ", false)]
    [InlineData("Changed observation", true)]
    public void ShouldCompareSourceFieldsGivenCommittedObservation(string name, bool changed)
    {
        // Arrange
        var (tenant, ledger, target, batch) = Committed();
        var claim = Assert.Single(ledger.GetSourceClaims());
        var later = Stage(tenant, "app-1", name);

        // Act
        var fields = claim.ChangedFields(Assert.Single(later.GetRows()));

        // Assert
        Assert.Equal(changed, fields.Contains("name"));
        Assert.DoesNotContain("purpose", fields);
        Assert.DoesNotContain("owner_reference", fields);
        Assert.Equal(batch.Id, claim.Plan.BatchId);
        Assert.Equal(tenant, claim.Plan.TenantId);
        Assert.Equal(target.Id, claim.Observation.ApplicationId);
        Assert.Equal(1, target.Revision);
        Assert.True(target.Declare("Governed name", "Governed purpose", null,
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("source")]
    [InlineData("namespace")]
    [InlineData("case")]
    public void ShouldKeepClaimsIsolatedGivenDifferentSourceIdentity(string difference)
    {
        // Arrange
        var (tenant, ledger, _, _) = Committed();
        var request = new StageApplicationImport(difference == "tenant" ? Uuid.CreateVersion4() : tenant,
            Uuid.CreateVersion4(), difference == "source" ? "other" : "manual",
            difference == "namespace" ? "other" : "applications", "partial",
            [new(difference == "case" ? "APP-1" : "app-1", "Observed name", "Observed purpose", "Observed owner")]);
        var batch = new ImportBatch(request.TenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);

        // Act
        var result = ledger.PrepareAcceptancePlan(batch, 1);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(ledger.GetSourceClaim("APP-1"));
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    [Fact]
    public void ShouldKeepPreviousObservationGivenCanceledLaterBatch()
    {
        // Arrange
        var (tenant, ledger, _, first) = Committed();
        var later = Stage(tenant, "app-1", "Changed observation");

        // Act
        Assert.Null(ledger.Cancel(later, 1, "Cancel", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));

        // Assert
        Assert.Equal(first.Id, Assert.Single(ledger.GetSourceClaims()).Plan.BatchId);
        Assert.False(ledger.PrepareAcceptancePlan(later, 2).IsSuccess);
    }

    [Fact]
    public void ShouldHideClaimsGivenUndurableCommit()
    {
        // Arrange
        var (tenant, ledger, _, _) = Committed(persistCommit: false);
        var later = Stage(tenant, "app-1", "Another observation");

        // Act
        var result = ledger.PrepareAcceptancePlan(later, 1);

        // Assert
        Assert.Empty(ledger.GetSourceClaims());
        Assert.False(result.IsSuccess);
        Assert.True(Assert.IsType<RequestError>(result.Error).IsTransient);
        Assert.False(ledger.GetMissingSourceClaims(later).IsSuccess);
    }

    [Fact]
    public void ShouldRequireFreshTargetRevisionGivenLaterGovernance()
    {
        // Arrange
        var (tenant, ledger, target, _) = Committed();
        target.ResolveImportVisibility(ledger);
        Assert.Null(target.Revise(1, "Revised governed name", "New purpose", null,
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var batch = Stage(tenant, "app-1", "Another observation");
        var targets = new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target };

        // Act
        var stale = ledger.BeginAcceptance(batch, 1, targets, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);

        // Assert
        Assert.False(stale.IsSuccess);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id,
            Assert.Single(batch.GetRows()).RowId, 1, "link_existing", target.Id, 2, "Reviewed current target"),
            target, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        Assert.True(ledger.BeginAcceptance(batch, 2, targets, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        Assert.Equal(2, target.Revision);
    }

    [Fact]
    public void ShouldUseStreamOrderGivenLaterCommittedObservationWithEarlierTimestamp()
    {
        // Arrange
        var ledgerHistory = new List<DomainEvent>();
        var targetHistory = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: ledgerHistory, targetHistory: targetHistory);
        var batch = Stage(tenant, "app-1", "Latest observation");
        var now = DateTimeOffset.UtcNow.AddDays(-1);
        Assert.True(ledger.BeginAcceptance(batch, 1, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Other lead", now).IsSuccess);
        ledgerHistory.AddRange(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        var accepting = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(accepting).Given(ledgerHistory.ToArray());
        Assert.True(target.RecordPendingImportEffect(accepting, batch, Assert.Single(batch.GetRows()).RowId, now).IsSuccess);
        targetHistory.AddRange(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
        var savedTarget = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(savedTarget).Given(targetHistory.ToArray());
        Assert.True(accepting.Commit(batch, 4, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = savedTarget }, now).IsSuccess);
        Assert.Equal("Observed name", Assert.Single(accepting.GetSourceClaims()).Observation.Name);
        ledgerHistory.AddRange(new AggregateScenario<ApplicationImportLedger>(accepting).PendingEvents);
        var replay = new ApplicationImportLedger(tenant, "manual", "applications");

        // Act
        _ = new AggregateScenario<ApplicationImportLedger>(replay).Given(ledgerHistory.ToArray());

        // Assert
        var claim = Assert.Single(replay.GetSourceClaims());
        Assert.Equal(batch.Id, claim.Plan.BatchId);
        Assert.Equal("Latest observation", claim.Observation.Name);
        Assert.Equal(target.Id, claim.Observation.ApplicationId);
        Assert.Equal(now, claim.CommittedAt);
        Assert.Equal("Other lead", claim.Plan.ApproverDisplay);
        Assert.Empty(claim.ChangedFields(Assert.Single(batch.GetRows())));
    }

    [Fact]
    public void ShouldRejectFrozenRebindingGivenCorruptedReplay()
    {
        // Arrange
        var history = new List<DomainEvent>();
        var (tenant, ledger, target, _) = Committed(ledgerHistory: history);
        var batch = Stage(tenant, "app-1", "Another observation");
        Assert.True(ledger.BeginAcceptance(batch, 1, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target },
            Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var frozen = Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.OfType<ApplicationImportPlanRowFrozen>());
        var corrupt = frozen with { Row = frozen.Row with { ApplicationId = Uuid.CreateVersion4() } };
        var replay = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(replay).Given(history.ToArray());
        var start = Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.OfType<ApplicationImportPlanStarted>());
        _ = new AggregateScenario<ApplicationImportLedger>(replay).Given(start);

        // Act
        var act = () => new AggregateScenario<ApplicationImportLedger>(replay).Given(corrupt);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    static ImportBatch Stage(Uuid tenant, string sourceRecordId, string name, string coverage = "partial")
    {
        var request = new StageApplicationImport(tenant, Uuid.CreateVersion4(), "manual", "applications", coverage,
            [new(sourceRecordId, name, "Observed purpose", "Observed owner")]);
        var batch = new ImportBatch(tenant, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        var saved = new ImportBatch(tenant, batch.Id);
        _ = new AggregateScenario<ImportBatch>(saved).Given(new AggregateScenario<ImportBatch>(batch).PendingEvents.ToArray());
        return saved;
    }

    static (Uuid Tenant, ApplicationImportLedger Ledger, DeclaredApplication Target, ImportBatch Batch) Committed(
        bool persistCommit = true, List<DomainEvent>? ledgerHistory = null, List<DomainEvent>? targetHistory = null)
    {
        var tenant = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var batch = Stage(tenant, "app-1", "Observed name");
        var target = new DeclaredApplication(tenant, Uuid.CreateVersion4());
        Assert.True(target.Declare("Governed name", "Governed purpose", null, actor, "Lead", now).IsSuccess);
        var plan = new ApplicationImportLedger(tenant, "manual", "applications");
        var row = Assert.Single(batch.GetRows());
        Assert.Null(plan.Correlate(batch, new CorrelateApplicationImportRow(tenant, batch.Id, row.RowId,
            1, "link_existing", target.Id, 1, "Reviewed identity"), target, actor, "Lead", now));
        var targets = new Dictionary<Uuid, DeclaredApplication> { [target.Id] = target };
        Assert.True(plan.BeginAcceptance(batch, 2, targets, actor, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenant, "manual", "applications");
        var planEvents = new AggregateScenario<ApplicationImportLedger>(plan).PendingEvents.ToArray();
        _ = new AggregateScenario<ApplicationImportLedger>(ledger).Given(planEvents);
        Assert.True(target.RecordPendingImportEffect(ledger, batch, row.RowId, now).IsSuccess);
        targetHistory?.AddRange(new AggregateScenario<DeclaredApplication>(target).PendingEvents);
        var savedTarget = new DeclaredApplication(tenant, target.Id);
        _ = new AggregateScenario<DeclaredApplication>(savedTarget).Given(
            new AggregateScenario<DeclaredApplication>(target).PendingEvents.ToArray());
        Assert.True(ledger.Commit(batch, 5, new Dictionary<Uuid, DeclaredApplication> { [target.Id] = savedTarget }, now).IsSuccess);
        if (!persistCommit)
            return (tenant, ledger, savedTarget, batch);
        ledgerHistory?.AddRange([.. planEvents, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        var savedLedger = new ApplicationImportLedger(tenant, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(savedLedger).Given(
            [.. planEvents, .. new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents]);
        return (tenant, savedLedger, savedTarget, batch);
    }
}

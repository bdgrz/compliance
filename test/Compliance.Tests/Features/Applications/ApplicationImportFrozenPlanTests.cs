using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportFrozenPlanTests
{
    [Fact]
    public void ShouldReplayOriginalChoiceGivenFrozenPlan()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var batch = Resolved(tenantId, actor, now, ledger);
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var replay = ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id,
            batch.GetRows()[0].RowId, 1, "create_new", null, null, "Reviewed identity"), null, actor, "Lead", now);

        // Assert
        Assert.Null(replay);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(2L)]
    [InlineData(99L)]
    public void ShouldRejectHeaderGivenNonconsecutiveBatchRevision(long revision)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var batch = Resolved(tenantId, actor, now, ledger);
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Take(2).ToArray();
        events[1] = Assert.IsType<ApplicationImportPlanStarted>(events[1]) with { Revision = revision };
        var replay = new ApplicationImportLedger(tenantId, "manual", "applications");

        // Act
        var exception = Record.Exception(() => new AggregateScenario<ApplicationImportLedger>(replay).Given(events));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(2, replay.GetRevision(batch));
    }

    [Theory]
    [InlineData("empty_approver")]
    [InlineData("blank_display")]
    public void ShouldRejectUnattributedPlanGivenInvalidApprover(string problem)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var batch = Resolved(tenantId, actor, now, ledger);
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var result = ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(),
            problem == "empty_approver" ? Uuid.Empty : actor, problem == "blank_display" ? " " : "Lead", now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData("missing_row")]
    [InlineData("altered_hash")]
    [InlineData("foreign_source")]
    public void ShouldRejectCorruptReplayGivenIncompleteOrAlteredPlan(string problem)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var batch = Resolved(tenantId, actor, now, ledger);
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToArray();
        if (problem == "missing_row")
            events = events.Where(ev => ev is not ApplicationImportPlanRowFrozen).ToArray();
        if (problem == "altered_hash")
            events[^1] = Assert.IsType<ApplicationImportPlanSealed>(events[^1]) with { PlanSha256 = "altered" };
        if (problem == "foreign_source")
        {
            var index = Array.FindIndex(events, ev => ev is ApplicationImportPlanStarted);
            events[index] = Assert.IsType<ApplicationImportPlanStarted>(events[index]) with { SourceKey = "another" };
        }
        var replay = new ApplicationImportLedger(tenantId, "manual", "applications");

        // Act
        var exception = Record.Exception(() => new AggregateScenario<ApplicationImportLedger>(replay).Given(events));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Null(replay.GetFrozenPlan(batch.Id));
    }

    [Fact]
    public void ShouldKeepEventsBoundedGivenTwoHundredStagedRows()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            Enumerable.Range(1, 200).Select(index => new ApplicationImportInputRow($"app-{index}", "Payroll", "Pay staff", null)).ToArray());
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actor, "Contributor", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        foreach (var row in batch.GetRows())
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id,
                row.RowId, ledger.GetRevision(batch), "create_new", null, null, "Reviewed identity"), null, actor, "Lead", now));

        // Act
        var result = ledger.BeginAcceptance(batch, 201, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(200, ledger.GetFrozenPlan(batch.Id)?.Rows.Count);
        Assert.Equal(403, ledger.GetRevision(batch));
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.OfType<ApplicationImportPlanRowFrozen>();
        Assert.Equal(200, events.Count());
        Assert.All(events, ev => Assert.True(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ev,
            ComplianceCoreJsonContext.Default.ApplicationImportPlanRowFrozen).Length <= ImportBatch.MaximumStagedPayloadBytes));
    }

    [Fact]
    public void ShouldReleaseSourceGivenCancellationOfAcceptingBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var first = Resolved(tenantId, actor, now, ledger);
        var second = Resolved(tenantId, actor, now, ledger);
        Assert.True(ledger.BeginAcceptance(first, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);

        // Act
        var competing = ledger.BeginAcceptance(second, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now);
        var canceled = ledger.Cancel(first, 5, "Canceled before effects", actor, "Lead", now);
        var next = ledger.BeginAcceptance(second, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(competing.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(competing.Error).IsTransient);
        Assert.Null(canceled);
        Assert.True(next.IsSuccess);
        Assert.NotNull(ledger.GetFrozenPlan(first.Id));
        Assert.NotNull(ledger.GetFrozenPlan(second.Id));
        Assert.Equal(6, ledger.GetCanceledRevision(first.Id));
    }

    [Fact]
    public void ShouldPreservePlanGivenRetryAndCorrelationEdit()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var batch = Resolved(tenantId, actor, now, ledger);
        Assert.True(ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), actor, "Lead", now).IsSuccess);
        var original = ledger.GetFrozenPlan(batch.Id);
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var retry = ledger.BeginAcceptance(batch, 2, new Dictionary<Uuid, DeclaredApplication>(), Uuid.CreateVersion4(), "Other lead", now.AddDays(1));
        var edit = ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id,
            batch.GetRows()[0].RowId, 5, "create_new", null, null, "Changed reasoning"), null, actor, "Lead", now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.NotNull(edit);
        Assert.Same(original, ledger.GetFrozenPlan(batch.Id));
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData("missing", RequestErrorKind.NotFound)]
    [InlineData("tenant", RequestErrorKind.NotFound)]
    [InlineData("revised", RequestErrorKind.Conflict)]
    [InlineData("retired", RequestErrorKind.Conflict)]
    public void ShouldRecheckExistingTargetGivenAcceptanceAfterCorrelation(string change, RequestErrorKind expected)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        var application = new DeclaredApplication(tenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Manual", "Governed", null, actor, "Lead", now).IsSuccess);
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actor, "Lead", now).IsSuccess);
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id,
            batch.GetRows()[0].RowId, 1, "link_existing", application.Id, 1, "Reviewed identity"), application, actor, "Lead", now));
        if (change == "revised")
            Assert.Null(application.Revise(1, "Revised", "Governed", null, actor, "Lead", now));
        if (change == "retired")
            Assert.Null(application.Retire(1, now, "Retired", null, actor, "Lead", now));
        var targets = new Dictionary<Uuid, DeclaredApplication>();
        if (change != "missing")
            targets[application.Id] = change == "tenant" ? new DeclaredApplication(Uuid.CreateVersion4(), application.Id) : application;
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var result = ledger.BeginAcceptance(batch, 2, targets, actor, "Lead", now);

        // Assert
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    static ImportBatch Resolved(Uuid tenantId, Uuid actor, DateTimeOffset now, ApplicationImportLedger ledger)
    {
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actor, "Contributor", now).IsSuccess);
        Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id,
            batch.GetRows()[0].RowId, 1, "create_new", null, null, "Reviewed identity"), null, actor, "Lead", now));
        return batch;
    }

    [Fact]
    public void ShouldPersistCompletePlanGivenResolvedBatchAndReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var submitter = Uuid.CreateVersion4();
        var approver = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null), new("app-2", "CRM", "Manage customers", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, submitter, "Contributor", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        foreach (var row in batch.GetRows())
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id,
                row.RowId, ledger.GetRevision(batch), "create_new", null, null, "Reviewed identity"),
                null, approver, "Lead", now));

        // Act
        var result = ledger.BeginAcceptance(batch, 3, new Dictionary<Uuid, DeclaredApplication>(), approver, "Lead", now);
        var events = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.ToArray();
        var replay = new ApplicationImportLedger(tenantId, "manual", "applications");
        _ = new AggregateScenario<ApplicationImportLedger>(replay).Given(events);
        var frozen = replay.GetFrozenPlan(batch.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(frozen);
        Assert.Equal(2, frozen.Rows.Count);
        Assert.Equal(submitter, frozen.Start.SubmitterMemberId);
        Assert.Equal(approver, frozen.Start.ApproverMemberId);
        Assert.Equal("partial", frozen.Start.Coverage);
        Assert.Equal(7, frozen.Revision);
        Assert.Equal(7, replay.GetRevision(batch));
        Assert.Equal(1, batch.Revision);
        Assert.Single(events.OfType<ApplicationImportPlanStarted>());
        Assert.Equal(2, events.OfType<ApplicationImportPlanRowFrozen>().Count());
        Assert.Single(events.OfType<ApplicationImportPlanSealed>());
    }
}

using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportCorrelationTests
{
    [Fact]
    public void ShouldReplayRecordedLinkGivenTargetChangedAfterDecision()
    {
        // Arrange
        var fixture = Staged();
        var application = new DeclaredApplication(fixture.TenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Manual", "Governed", null, fixture.ActorId, "Lead", fixture.Now).IsSuccess);
        var request = fixture.Request with
        {
            Decision = "link_existing",
            ApplicationId = application.Id,
            ExpectedApplicationRevision = 1,
        };
        var ledger = new ApplicationImportLedger(fixture.TenantId, "manual", "applications");
        Assert.Null(ledger.Correlate(fixture.Batch, request, application, fixture.ActorId, "Lead", fixture.Now));
        var original = ledger.GetCorrelation(fixture.Batch.Id, fixture.Request.RowId);
        Assert.Null(application.Revise(1, "Revised manually", "Governed", null, fixture.ActorId, "Lead", fixture.Now));

        // Act
        var replay = ledger.Correlate(fixture.Batch, request, application, fixture.ActorId, "Lead", fixture.Now.AddDays(1));

        // Assert
        Assert.Null(replay);
        Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
        Assert.Equal(original, ledger.GetCorrelation(fixture.Batch.Id, fixture.Request.RowId));
        Assert.Equal(2, ledger.GetRevision(fixture.Batch));
    }

    [Fact]
    public void ShouldPreserveAuditEventGivenIdenticalCorrelationReplay()
    {
        // Arrange
        var fixture = Staged();
        var original = new ApplicationImportLedger(fixture.TenantId, "manual", "applications");
        Assert.Null(original.Correlate(fixture.Batch, fixture.Request, null, fixture.ActorId, "Lead", fixture.Now));
        var recorded = Assert.IsType<ApplicationImportRowCorrelated>(
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(original).PendingEvents));
        var replayed = new ApplicationImportLedger(fixture.TenantId, "manual", "applications");
        var scenario = new AggregateScenario<ApplicationImportLedger>(replayed).Given(recorded);

        // Act
        var failure = replayed.Correlate(fixture.Batch, fixture.Request, null,
            Uuid.CreateVersion4(), "Another lead", fixture.Now.AddDays(1));

        // Assert
        Assert.Null(failure);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(recorded, replayed.GetCorrelation(fixture.Batch.Id, fixture.Request.RowId));
        Assert.Equal(2, replayed.GetRevision(fixture.Batch));
    }

    [Fact]
    public void ShouldAdvanceRevisionGivenReplacementChoiceAndCancellation()
    {
        // Arrange
        var fixture = Staged();
        var ledger = new ApplicationImportLedger(fixture.TenantId, "manual", "applications");
        Assert.Null(ledger.Correlate(fixture.Batch, fixture.Request, null, fixture.ActorId, "Lead", fixture.Now));
        var application = new DeclaredApplication(fixture.TenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Manual", "Governed", null, fixture.ActorId, "Lead", fixture.Now).IsSuccess);
        var replacement = fixture.Request with
        {
            Decision = "link_existing",
            ApplicationId = application.Id,
            ExpectedApplicationRevision = 1,
            ExpectedBatchRevision = 2,
        };

        // Act
        var stale = ledger.Correlate(fixture.Batch, replacement with { ExpectedBatchRevision = 1 }, application,
            fixture.ActorId, "Lead", fixture.Now);
        var changed = ledger.Correlate(fixture.Batch, replacement, application, fixture.ActorId, "Lead", fixture.Now);
        var canceled = ledger.Cancel(fixture.Batch, 3, "Canceled after review", fixture.ActorId, "Lead", fixture.Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, stale?.Code);
        Assert.Null(changed);
        Assert.Null(canceled);
        Assert.Equal(application.Id, ledger.GetCorrelation(fixture.Batch.Id, fixture.Request.RowId)?.ApplicationId);
        Assert.Equal(4, ledger.GetCanceledRevision(fixture.Batch.Id));
        Assert.Equal(4, ledger.GetRevision(fixture.Batch));
        Assert.Equal(3, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("key")]
    [InlineData("namespace")]
    public void ShouldRejectReplayGivenCorrelationFromAnotherSource(string mismatch)
    {
        // Arrange
        var fixture = Staged();
        var original = new ApplicationImportLedger(fixture.TenantId, "manual", "applications");
        Assert.Null(original.Correlate(fixture.Batch, fixture.Request, null, fixture.ActorId, "Lead", fixture.Now));
        var recorded = Assert.IsType<ApplicationImportRowCorrelated>(
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(original).PendingEvents));
        var ledger = new ApplicationImportLedger(mismatch == "tenant" ? Uuid.CreateVersion4() : fixture.TenantId,
            mismatch == "key" ? "other" : "manual", mismatch == "namespace" ? "other" : "applications");

        // Act
        var exception = Record.Exception(() => new AggregateScenario<ApplicationImportLedger>(ledger).Given(recorded));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Theory]
    [InlineData("tenant", CommandFailureCode.MissingRecord)]
    [InlineData("source", CommandFailureCode.MissingRecord)]
    [InlineData("batch", CommandFailureCode.MissingRecord)]
    [InlineData("row", CommandFailureCode.MissingRecord)]
    [InlineData("invalid", CommandFailureCode.InvalidContent)]
    [InlineData("duplicate", CommandFailureCode.InvalidContent)]
    [InlineData("canceled", CommandFailureCode.StateConflict)]
    [InlineData("legacy_canceled", CommandFailureCode.StateConflict)]
    [InlineData("decision", CommandFailureCode.InvalidContent)]
    [InlineData("reason", CommandFailureCode.InvalidContent)]
    [InlineData("new_target", CommandFailureCode.InvalidContent)]
    [InlineData("new_revision", CommandFailureCode.InvalidContent)]
    public void ShouldRejectChoiceGivenInvalidBatchOrDecision(string problem, CommandFailureCode expected)
    {
        // Arrange
        var fixture = Staged(problem);
        var request = fixture.Request;
        var ledger = new ApplicationImportLedger(fixture.TenantId,
            problem == "source" ? "other" : "manual", "applications");
        if (problem == "canceled")
            Assert.Null(ledger.Cancel(fixture.Batch, 1, "Canceled", fixture.ActorId, "Lead", fixture.Now));
        if (problem == "legacy_canceled")
            Assert.Null(fixture.Batch.Cancel(1, "Canceled", fixture.ActorId, "Lead", fixture.Now));
        request = problem switch
        {
            "tenant" => request with { TenantId = Uuid.CreateVersion4() },
            "batch" => request with { BatchId = Uuid.CreateVersion4() },
            "row" => request with { RowId = Uuid.CreateVersion4() },
            "decision" => request with { Decision = "guess_by_name" },
            "reason" => request with { Reason = " " },
            "new_target" => request with { ApplicationId = Uuid.CreateVersion4() },
            "new_revision" => request with { ExpectedApplicationRevision = 1 },
            _ => request,
        };
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var failure = ledger.Correlate(fixture.Batch, request, null, fixture.ActorId, "Lead", fixture.Now);

        // Assert
        Assert.Equal(expected, failure?.Code);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Theory]
    [InlineData("missing", CommandFailureCode.MissingRecord)]
    [InlineData("tenant", CommandFailureCode.MissingRecord)]
    [InlineData("id", CommandFailureCode.MissingRecord)]
    [InlineData("retired", CommandFailureCode.StateConflict)]
    [InlineData("revision", CommandFailureCode.VersionConflict)]
    [InlineData("batch_revision", CommandFailureCode.VersionConflict)]
    [InlineData("no_revision", CommandFailureCode.InvalidContent)]
    public void ShouldRejectLinkGivenUnavailableOrChangedTarget(string problem, CommandFailureCode expected)
    {
        // Arrange
        var fixture = Staged();
        var application = new DeclaredApplication(problem == "tenant" ? Uuid.CreateVersion4() : fixture.TenantId,
            Uuid.CreateVersion4());
        if (problem != "missing")
            Assert.True(application.Declare("Manual", "Governed", null, fixture.ActorId, "Lead", fixture.Now).IsSuccess);
        if (problem == "retired")
            Assert.Null(application.Retire(1, fixture.Now, "Retired", null, fixture.ActorId, "Lead", fixture.Now));
        var request = fixture.Request with
        {
            Decision = "link_existing",
            ApplicationId = problem == "id" ? Uuid.CreateVersion4() : application.Id,
            ExpectedApplicationRevision = problem == "revision" ? 2 : problem == "no_revision" ? null : 1,
            ExpectedBatchRevision = problem == "batch_revision" ? 2 : 1,
        };
        var ledger = new ApplicationImportLedger(fixture.TenantId, "manual", "applications");

        // Act
        var failure = ledger.Correlate(fixture.Batch, request, application, fixture.ActorId, "Lead", fixture.Now);

        // Assert
        Assert.Equal(expected, failure?.Code);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    static (Uuid TenantId, Uuid ActorId, DateTimeOffset Now, ImportBatch Batch,
        CorrelateApplicationImportRow Request) Staged(string problem = "")
    {
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        ApplicationImportInputRow[] rows = problem == "duplicate"
            ? [new("app-1", "Payroll", "Pay staff", null), new("app-1", "Payroll", "Pay staff", null)]
            : [new("app-1", problem == "invalid" ? " " : "Payroll", "Pay staff", null)];
        var stage = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial", rows);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(stage));
        Assert.True(batch.Stage(stage, actorId, "Lead", now).IsSuccess);
        var rowId = Assert.IsType<ApplicationImportStaged>(
            Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents)).Rows[0].RowId;
        return (tenantId, actorId, now, batch, new CorrelateApplicationImportRow(tenantId,
            batch.Id, rowId, 1, "create_new", null, null, "Reviewed source identity"));
    }

    [Theory]
    [InlineData("create_new")]
    [InlineData("link_existing")]
    public void ShouldRecordAttributedChoiceGivenValidStagedRow(string decision)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var stage = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual",
            "applications", "partial", [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(stage));
        Assert.True(batch.Stage(stage, actorId, "Lead", now).IsSuccess);
        var row = Assert.Single(Assert.IsType<ApplicationImportStaged>(
            Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents)).Rows);
        var application = new DeclaredApplication(tenantId, Uuid.CreateVersion4());
        Assert.True(application.Declare("Governed name", "Governed purpose", null, actorId, "Lead", now).IsSuccess);
        var target = decision == "link_existing" ? application.Id : (Uuid?)null;
        var expected = decision == "link_existing" ? 1L : (long?)null;
        var request = new CorrelateApplicationImportRow(tenantId, batch.Id, row.RowId, 1,
            decision, target, expected, "Reviewed source identity");
        var ledger = new ApplicationImportLedger(tenantId, stage.SourceKey, stage.SourceNamespace);

        // Act
        var failure = ledger.Correlate(batch, request, decision == "link_existing" ? application : null,
            actorId, "Lead", now);

        // Assert
        Assert.Null(failure);
        var recorded = Assert.IsType<ApplicationImportRowCorrelated>(
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents));
        Assert.Equal(tenantId, recorded.TenantId);
        Assert.Equal(batch.Id, recorded.BatchId);
        Assert.Equal(row.RowId, recorded.RowId);
        Assert.Equal("app-1", recorded.SourceRecordId);
        Assert.Equal(2, recorded.Revision);
        Assert.Equal(decision, recorded.Decision);
        Assert.Equal(target ?? Uuid.CreateVersion5(batch.Id, $"application_import_target:{row.RowId}"), recorded.ApplicationId);
        Assert.Equal(expected, recorded.ExpectedApplicationRevision);
        Assert.Equal(actorId, recorded.ActorMemberId);
        Assert.Equal("Lead", recorded.ActorDisplay);
        Assert.Equal(now, recorded.RecordedAt);
        Assert.Equal("Reviewed source identity", recorded.Reason);
        Assert.Equal(1, batch.Revision);
        Assert.Single(new AggregateScenario<DeclaredApplication>(application).PendingEvents);
    }
}

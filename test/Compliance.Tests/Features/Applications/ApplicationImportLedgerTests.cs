using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportLedgerTests
{
    [Theory]
    [InlineData("tenant")]
    [InlineData("key")]
    [InlineData("namespace")]
    [InlineData("missing")]
    public void ShouldHideBatchGivenDifferentLedgerSource(string mismatch)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batch = mismatch == "missing" ? new ImportBatch(tenantId, Uuid.CreateVersion4()) :
            Staged(mismatch == "tenant" ? Uuid.CreateVersion4() : tenantId,
                mismatch == "key" ? "other" : "manual",
                mismatch == "namespace" ? "other" : "applications");
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");

        // Act
        var failure = ledger.Cancel(batch, 1, "Superseded", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord, failure?.Code);
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveTerminalStateGivenHistoricalBatchCancellation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batch = Staged(tenantId);
        Assert.Null(batch.Cancel(1, "Legacy cancellation", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");

        // Act
        var failure = ledger.Cancel(batch, 1, "Replay", Uuid.CreateVersion4(), "Other lead", DateTimeOffset.UtcNow);

        // Assert
        Assert.Null(failure);
        Assert.Equal(2, batch.Revision);
        Assert.True(batch.IsCanceled);
        Assert.Null(ledger.GetCanceledRevision(batch.Id));
        Assert.Empty(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents);
    }

    [Fact]
    public void ShouldAvoidDuplicateCancellationGivenLedgerReplay()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batch = Staged(tenantId);
        var original = new ApplicationImportLedger(tenantId, "manual", "applications");
        Assert.Null(original.Cancel(batch, 1, "Original", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow));
        var canceled = Assert.IsType<ApplicationImportCanceled>(
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(original).PendingEvents));
        var replayed = new ApplicationImportLedger(tenantId, "manual", "applications");
        var scenario = new AggregateScenario<ApplicationImportLedger>(replayed).Given(canceled);

        // Act
        var failure = replayed.Cancel(batch, 1, "Retry", Uuid.CreateVersion4(), "Other lead", DateTimeOffset.UtcNow);

        // Assert
        Assert.Null(failure);
        Assert.Equal(2, replayed.GetCanceledRevision(batch.Id));
        Assert.Empty(scenario.PendingEvents);
    }

    [Fact]
    public void ShouldKeepRevisionsPerBatchGivenTwoBatchesFromSameSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var first = Staged(tenantId);
        var second = Staged(tenantId);
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");

        // Act
        var firstFailure = ledger.Cancel(first, 1, "First", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);
        var secondFailure = ledger.Cancel(second, 1, "Second", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);

        // Assert
        Assert.Null(firstFailure);
        Assert.Null(secondFailure);
        Assert.Equal(2, ledger.GetCanceledRevision(first.Id));
        Assert.Equal(2, ledger.GetCanceledRevision(second.Id));
        Assert.Equal(2, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    static ImportBatch Staged(Uuid tenantId, string sourceKey = "manual", string sourceNamespace = "applications")
    {
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), sourceKey,
            sourceNamespace, "partial", [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow).IsSuccess);
        return batch;
    }

    [Fact]
    public void ShouldRejectReplayGivenForeignTenantCancellation()
    {
        // Arrange
        var ledger = new ApplicationImportLedger(Uuid.CreateVersion4(), "manual", "applications");
        var foreign = new ApplicationImportCanceled(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            2, "Wrong tenant", Uuid.CreateVersion4(), "Lead", DateTimeOffset.UtcNow);
        var scenario = new AggregateScenario<ApplicationImportLedger>(ledger);

        // Act
        var exception = Record.Exception(() => scenario.Given(foreign));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void ShouldCancelOnSourceLedgerGivenStagedImport()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual",
            "applications", "partial", [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actorId, "Lead", now).IsSuccess);
        var ledger = new ApplicationImportLedger(tenantId, request.SourceKey, request.SourceNamespace);

        // Act
        var failure = ledger.Cancel(batch, 1, "Superseded source", actorId, "Lead", now);

        // Assert
        Assert.Null(failure);
        Assert.NotEqual(batch.Stream, ledger.Stream);
        Assert.False(batch.IsCanceled);
        Assert.Equal(1, batch.Revision);
        var canceled = Assert.IsType<ApplicationImportCanceled>(
            Assert.Single(new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents));
        Assert.Equal(tenantId, canceled.TenantId);
        Assert.Equal(batch.Id, canceled.BatchId);
        Assert.Equal(2, canceled.Revision);
        Assert.Equal("Superseded source", canceled.Reason);
        Assert.Equal(actorId, canceled.ActorMemberId);
        Assert.Equal("Lead", canceled.ActorDisplay);
        Assert.Equal(now, canceled.CanceledAt);
    }
}

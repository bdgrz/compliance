using System.Globalization;
using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ImportBatchTests
{
    [Fact]
    public void ShouldKeepOneObservationGivenIdenticalReplayAndRejectChangedContent()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var submissionId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, submissionId, "source", "primary",
            "partial", [new ApplicationImportInputRow("app-1", " Payroll ", " Pay staff ", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;

        // Act
        var first = batch.Stage(request, actorId, "Manager", now);
        var replay = batch.Stage(request with
        {
            Rows = [new ApplicationImportInputRow("app-1", "Payroll", "Pay staff", null)]
        }, actorId, "Other", now.AddMinutes(1));
        var changed = batch.Stage(request with
        {
            Rows = [new ApplicationImportInputRow("app-1", "Different", "Pay staff", null)]
        }, actorId, "Manager", now.AddMinutes(2));

        // Assert
        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents);
        Assert.Equal(1, batch.Revision);
    }

    [Fact]
    public void ShouldReportInvalidAndDuplicateRowsGivenBoundedStage()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "source", "primary",
            "partial", [
                new ApplicationImportInputRow("one", "A", "Purpose", null),
                new ApplicationImportInputRow("one", "", "Purpose", null),
                new ApplicationImportInputRow(" ", "C", "Purpose", null)
            ]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));

        // Act
        var result = batch.Stage(request, Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Collection(new AggregateScenario<ImportBatch>(batch).PendingEvents,
            ev =>
            {
                Assert.Contains("duplicate_source_record_id",
                    Assert.IsType<ApplicationImportStaged>(ev).Rows[0].ValidationFindings);
                Assert.Contains("name_required",
                    Assert.IsType<ApplicationImportStaged>(ev).Rows[1].ValidationFindings);
                Assert.Contains("source_record_id_required",
                    Assert.IsType<ApplicationImportStaged>(ev).Rows[2].ValidationFindings);
                Assert.Equal(3, Assert.IsType<ApplicationImportStaged>(ev).Rows.Count);
            });
    }

    [Fact]
    public void ShouldRejectWithoutWritingAnEventGivenOversizedRawRows()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "source", "primary",
            "partial", [new ApplicationImportInputRow("one", new string('x', 201), "Purpose", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));

        // Act
        var result = batch.Stage(request, Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ImportBatch>(batch).PendingEvents);
    }

    [Fact]
    public void ShouldKeepBatchIdentityAndDigestGivenCultureChange()
    {
        // Arrange
        var request = new StageApplicationImport(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            "source", "primary", "partial",
            [new ApplicationImportInputRow("record", "Payroll", "Purpose", null)]);
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Act
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var batchId = ImportBatch.BatchIdFor(request);
            var batch = new ImportBatch(request.TenantId, batchId);
            var first = batch.Stage(request, Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-EG");
            var alternateBatchId = ImportBatch.BatchIdFor(request);
            var replay = batch.Stage(request, Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);

            // Assert
            Assert.Equal(batchId, alternateBatchId);
            Assert.True(first.IsSuccess);
            Assert.Equal(first.Value.ContentSha256, replay.Value.ContentSha256);
            Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ShouldCancelOnceAndRejectStaleCancellationGivenProjectedBatch()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "source",
            "primary", "partial", [new ApplicationImportInputRow("record", "Payroll",
                "Purpose", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow).IsSuccess);
        var actorId = Uuid.CreateVersion4();

        // Act
        var canceled = batch.Cancel(1, "The source was superseded.", actorId, "Manager",
            DateTimeOffset.UtcNow);
        var replay = batch.Cancel(1, "The source was superseded.", actorId, "Manager",
            DateTimeOffset.UtcNow.AddMinutes(1));
        var future = batch.Cancel(3, "A stale cancellation.", actorId, "Manager",
            DateTimeOffset.UtcNow.AddMinutes(2));

        // Assert
        Assert.True(canceled.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(future.Error).Kind);
        Assert.True(batch.IsCanceled);
        Assert.Equal(2, batch.Revision);
        Assert.Collection(new AggregateScenario<ImportBatch>(batch).PendingEvents,
            ev => Assert.IsType<ApplicationImportStaged>(ev),
            ev =>
            {
                Assert.Equal("ApplicationImportCanceled", ev.GetType().Name);
                Assert.Equal("The source was superseded.",
                    ev.GetType().GetProperty("Reason")?.GetValue(ev));
            });
    }

    [Fact]
    public void ShouldRejectWithoutWritingAnEventGivenRowsExceedStreamFrame()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(),
            "source", "primary", "partial", Enumerable.Range(1, 200)
                .Select(number => new ApplicationImportInputRow($"record-{number}",
                    "Application", new string('p', 2000), null)).ToArray());
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));

        // Act
        var result = batch.Stage(request, Uuid.CreateVersion4(), "Manager", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<ImportBatch>(batch).PendingEvents);
    }
}

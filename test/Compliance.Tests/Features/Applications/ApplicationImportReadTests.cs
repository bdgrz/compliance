using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportReadTests
{
    [Theory]
    [InlineData(1, "projection")]
    [InlineData(2, "source")]
    public async Task ShouldReportLagGivenStagedSourceWithoutProjectedRows(
        long minimumRevision, string laggingLayer)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(),
            "source", "primary", "partial",
            [new ApplicationImportInputRow("record", "Payroll", "Purpose", null)]);
        var batchId = ImportBatch.BatchIdFor(request);
        var source = new ImportBatch(tenantId, batchId);
        Assert.True(source.Stage(request, Uuid.CreateVersion4(), "Manager",
            DateTimeOffset.UtcNow).IsSuccess);
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var consistency = new ApplicationImportReadConsistency(directory, new SourceReader(source));

        // Act
        var batch = await consistency.GetFreshAsync(tenantId, batchId, minimumRevision,
            CancellationToken.None);
        var rows = await new ListApplicationImportRowsHandler(directory, consistency)
            .HandleAsync(new RequestContext<ListApplicationImportRows>(
                new ListApplicationImportRows(tenantId, batchId,
                    MinimumRevision: minimumRevision), new()), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(batch.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(batch.Error).IsTransient);
        Assert.Contains(laggingLayer, Assert.IsType<RequestError>(batch.Error).Message,
            StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(rows.Error).Kind);
        Assert.True(Assert.IsType<RequestError>(rows.Error).IsTransient);
    }

    [Fact]
    public async Task ShouldHideUnknownBatchGivenUncreatedSource()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var batchId = Uuid.CreateVersion4();
        var directory = new FitzApplicationImportDirectory(new InMemoryKvClient());
        var consistency = new ApplicationImportReadConsistency(directory,
            new SourceReader(new ImportBatch(tenantId, batchId)));

        // Act
        var result = await consistency.GetFreshAsync(tenantId, batchId, null,
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(result.Error).Kind);
    }

    sealed class SourceReader(Aggregate source) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            ValueTask.FromResult((TAggregate)source);
    }
}

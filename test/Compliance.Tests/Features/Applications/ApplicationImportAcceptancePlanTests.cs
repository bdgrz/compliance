using Bdgrz.Compliance.Features.Applications;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportAcceptancePlanTests
{
    [Theory]
    [InlineData("unresolved", RequestErrorKind.Conflict)]
    [InlineData("invalid", RequestErrorKind.Conflict)]
    [InlineData("duplicate", RequestErrorKind.Conflict)]
    [InlineData("stale", RequestErrorKind.Conflict)]
    [InlineData("canceled", RequestErrorKind.Conflict)]
    [InlineData("tenant", RequestErrorKind.NotFound)]
    public void ShouldRejectWholePlanGivenUnreadyBatch(string problem, RequestErrorKind expected)
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null),
                new(problem == "duplicate" ? "app-1" : "app-2", problem == "invalid" ? " " : "CRM", "Manage customers", null)]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actorId, "Lead", now).IsSuccess);
        var staged = Assert.IsType<ApplicationImportStaged>(Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents));
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        foreach (var row in staged.Rows.Where(row => row.ValidationFindings.Count == 0))
        {
            if (problem == "unresolved" && row.RowNumber == 2)
                continue;
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id, row.RowId,
                ledger.GetRevision(batch), "create_new", null, null, "Reviewed identity"), null, actorId, "Lead", now));
        }
        if (problem == "canceled")
            Assert.Null(ledger.Cancel(batch, ledger.GetRevision(batch), "Canceled", actorId, "Lead", now));
        var expectedRevision = problem == "stale" ? 1 : ledger.GetRevision(batch);
        var reader = problem == "tenant" ? new ApplicationImportLedger(Uuid.CreateVersion4(), "manual", "applications") : ledger;
        var before = new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count;

        // Act
        var result = reader.PrepareAcceptancePlan(batch, expectedRevision);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Equal(before, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }

    [Fact]
    public void ShouldFreezeEveryRowGivenExplicitNewTargetChoices()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new StageApplicationImport(tenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("app-1", "Payroll", "Pay staff", null), new("app-2", "CRM", "Manage customers", "Sales")]);
        var batch = new ImportBatch(tenantId, ImportBatch.BatchIdFor(request));
        Assert.True(batch.Stage(request, actorId, "Lead", now).IsSuccess);
        var staged = Assert.IsType<ApplicationImportStaged>(Assert.Single(new AggregateScenario<ImportBatch>(batch).PendingEvents));
        var ledger = new ApplicationImportLedger(tenantId, "manual", "applications");
        foreach (var row in staged.Rows)
            Assert.Null(ledger.Correlate(batch, new CorrelateApplicationImportRow(tenantId, batch.Id, row.RowId,
                ledger.GetRevision(batch), "create_new", null, null, "Reviewed identity"), null, actorId, "Lead", now));

        // Act
        var result = ledger.PrepareAcceptancePlan(batch, 3);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(staged.Rows.Select(row => row.RowId), result.Value.Select(row => row.RowId));
        Assert.Equal(["Payroll", "CRM"], result.Value.Select(row => row.Name));
        Assert.All(result.Value, row => Assert.Equal("create_new", row.Decision));
        Assert.All(result.Value, row => Assert.Equal(Uuid.CreateVersion5(batch.Id,
            $"application_import_target:{row.RowId}"), row.ApplicationId));
        Assert.Equal(3, ledger.GetRevision(batch));
        Assert.Equal(2, new AggregateScenario<ApplicationImportLedger>(ledger).PendingEvents.Count);
    }
}

using Bdgrz.Compliance.Features.Applications;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class FitzApplicationDirectoryTests
{
    [Fact]
    public async Task ShouldPreserveTenantBoundaryAndUnresolvedFactsGivenManualDeclarations()
    {
        // Arrange
        var client = new InMemoryKvClient();
        var directory = new FitzApplicationDirectory(client);
        var tenantId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var instanceId = Uuid.CreateVersion4();
        var missingSourceInstanceId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
        var identity = new CheckpointIdentity("ApplicationDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                instanceId, 2, "Production", "production", null, "payroll-prod",
                actorId, "Manager", now));
            await directory.ApplyAsync(new SystemInstanceDeclared(tenantId, applicationId,
                missingSourceInstanceId, 3, "Staging", "staging", null, null,
                actorId, "Manager", now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var application = await directory.GetAsync(tenantId, applicationId);
        var instance = await directory.GetInstanceAsync(tenantId, instanceId);
        var page = await directory.ListInstancesAsync(tenantId, applicationId, 10, null);

        // Assert
        Assert.Equal(3, application?.Revision);
        Assert.Contains("owner_missing", application!.Unresolved);
        Assert.Contains("classification_unresolved", application.Unresolved);
        Assert.Contains("access_boundary_review_pending", application.Unresolved);
        Assert.Equal("manual", application.SourceKind);
        Assert.Equal("manual", instance?.SourceKind);
        Assert.Equal("payroll-prod", instance?.SourceIdentifier);
        Assert.Contains("access_boundary_missing", instance!.Unresolved);
        Assert.Contains("source_identifier_unverified", instance.Unresolved);
        Assert.Equal(2, page.Items.Count);
        var missingSource = await directory.GetInstanceAsync(tenantId, missingSourceInstanceId);
        Assert.Contains("source_identifier_missing", missingSource!.Unresolved);
        Assert.Null(await directory.GetAsync(otherTenantId, applicationId));
        Assert.Null(await directory.GetInstanceAsync(otherTenantId, instanceId));
        Assert.Empty((await directory.ListAsync(otherTenantId, 10, null)).Items);
    }

    [Fact]
    public async Task ShouldRollbackAndRetryGivenInterruptedProjection()
    {
        // Arrange
        var directory = new FitzApplicationDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var actorId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var identity = new CheckpointIdentity("ApplicationDirectory",
            EventStreamPattern.ForPattern(tenantId.ToString()));

        // Act
        await using (var batch = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await directory.ApplyAsync(new ApplicationRevised(tenantId, Uuid.CreateVersion4(),
                    2, "Wrong", "Wrong", null, actorId, "Manager", now)));
        }

        // Assert
        Assert.Null(await directory.GetAsync(tenantId, applicationId));
        await using (var retry = await directory.BeginAsync(
                         new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new ApplicationDeclared(tenantId, applicationId,
                "Payroll", "Run payroll", null, actorId, "Manager", now));
            await retry.CommitAsync(ProjectionCheckpoint.Start);
        }
        Assert.NotNull(await directory.GetAsync(tenantId, applicationId));
    }
}

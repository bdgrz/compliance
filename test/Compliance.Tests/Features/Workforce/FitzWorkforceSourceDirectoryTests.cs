using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class FitzWorkforceSourceDirectoryTests
{
    [Fact]
    public async Task ShouldRetainAcceptedSourceProvenanceAndIsolateTenantGivenProjectedDecision()
    {
        // Arrange
        var directory = new FitzWorkforceSourceDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var targetId = Uuid.CreateVersion4();
        var observationId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
        var now = DateTimeOffset.UtcNow;
        var source = new WorkforceSourceIdentity("hris", "system", "100", "v1");
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null));
        var decision = new WorkforceSourceDecision("accepted", "Matches HRIS", 2, actor, now);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("WorkforceSourcesV1",
                             EventStreamPattern.ForPattern(tenantId.ToString(), "workforce-source-observations")),
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new WorkforceSourceObserved(tenantId, observationId, source,
                "person", targetId, 1, facts, now, actor, now));
            await directory.ApplyAsync(new WorkforceSourceReconciled(tenantId, observationId, 2, decision));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var found = await directory.GetAsync(tenantId, observationId);
        var page = await directory.ListAsync(tenantId, "person", targetId, 50, null);
        var alien = await directory.GetAsync(Uuid.CreateVersion4(), observationId);

        // Assert
        Assert.Equal(source, found!.Source);
        Assert.Equal(facts, found.Facts);
        Assert.Equal(decision, found.Decision);
        Assert.Equal(2, found.Revision);
        Assert.Equal(found, Assert.Single(page.Items));
        Assert.Null(alien);
    }
}

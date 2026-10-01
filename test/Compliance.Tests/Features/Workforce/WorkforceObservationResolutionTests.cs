using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class WorkforceObservationResolutionTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");

    [Fact]
    public void ShouldResolveWithAttributionAndReplayGivenOpenObservation()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var resolution = new WorkforceObservationResolution(tenantId, Uuid.CreateVersion4());

        // Act
        var resolved = resolution.Resolve("resolved", " Access removed in IdP ", Author, Now);
        var replay = resolution.Resolve("resolved", "Access removed in IdP", Author, Now);
        var changed = resolution.Resolve("dismissed", "Not applicable", Author, Now);

        // Assert
        Assert.Null(resolved);
        Assert.Null(replay);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        var ev = Assert.IsType<WorkforceObservationResolved>(Assert.Single(
            new AggregateScenario<WorkforceObservationResolution>(resolution).PendingEvents));
        Assert.Equal("Access removed in IdP", ev.Note);
        Assert.Equal(Author, ev.Actor);
        Assert.Equal(resolution.Id, ev.ObservationId);
    }

    [Theory]
    [InlineData("reopened", "note")]
    [InlineData("resolved", " ")]
    public void ShouldRejectResolutionGivenInvalidOutcomeOrNote(string outcome, string note)
    {
        // Arrange
        var resolution = new WorkforceObservationResolution(Uuid.CreateVersion4(),
            Uuid.CreateVersion4());

        // Act
        var result = resolution.Resolve(outcome, note, Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(result).Code);
    }

    [Fact]
    public async Task ShouldProjectResolutionsPerTenantGivenResolvedObservations()
    {
        // Arrange
        var directory = new FitzWorkforceObservationResolutionDirectory(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var observationId = Uuid.CreateVersion4();
        var open = Uuid.CreateVersion4();
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity("WorkforceObservationResolutionsV1",
                             EventStreamPattern.ForPattern(tenantId.ToString(),
                                 "workforce-observation-resolutions")),
                         ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(new WorkforceObservationResolved(tenantId, observationId,
                "dismissed", "Contractor tracked outside HR", Author, Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }

        // Act
        var found = await directory.GetManyAsync(tenantId, [observationId, open]);
        var alien = await directory.GetManyAsync(Uuid.CreateVersion4(), [observationId]);

        // Assert
        var resolution = Assert.Single(found).Value;
        Assert.Equal("dismissed", resolution.Resolution);
        Assert.Equal(Author, resolution.ResolvedBy);
        Assert.Empty(alien);
    }

    [Fact]
    public void ShouldOverlayStatusGivenResolution()
    {
        // Arrange
        var resolution = new WorkforceObservationResolutionView(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), "dismissed", "Accepted", Author, Now);

        // Act
        var open = WorkforceObservationStatus.Of(null);
        var dismissed = WorkforceObservationStatus.Of(resolution);

        // Assert
        Assert.Equal("open", open);
        Assert.Equal("dismissed", dismissed);
    }
}

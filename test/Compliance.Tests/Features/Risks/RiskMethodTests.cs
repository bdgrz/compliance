using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskMethodTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldPublishSequentialImmutableVersionsGivenExpectedVersion()
    {
        // Arrange
        var method = new RiskMethod(TenantId, ProgramId);

        // Act
        var first = method.Publish(ProgramId, 0, Scale(), Scale(), null, Actor, Now);
        var second = method.Publish(ProgramId, 1, Scale(), Scale(), 12, Actor, Now.AddDays(1));
        var stale = method.Publish(ProgramId, 1, Scale(), Scale(), 10, Actor, Now);

        // Assert
        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(2, method.PublishedVersion);
        var current = Assert.IsType<RiskMethodVersionView>(method.Current);
        Assert.Equal(12, current.AppetiteThreshold);
        Assert.Equal("qualitative_5x5", current.Kind);
        Assert.Equal("P1Y", current.ReassessmentInterval);
        Assert.Null(method.GetVersion(1)!.AppetiteThreshold);
        Assert.Equal(method.GetVersion(1)!.MethodVersionId, method.Find(
            method.GetVersion(1)!.MethodVersionId)!.MethodVersionId);
        Assert.Equal(2, new AggregateScenario<RiskMethod>(method).PendingEvents.Count);
    }

    [Theory]
    [InlineData(4, 5, null)]
    [InlineData(5, 6, null)]
    [InlineData(5, 5, 0)]
    [InlineData(5, 5, 26)]
    public void ShouldRejectMethodGivenInvalidScalesOrAppetite(int likelihoodCount,
        int impactCount, int? appetite)
    {
        // Arrange
        var method = new RiskMethod(TenantId, ProgramId);

        // Act
        var failure = method.Publish(ProgramId, 0, Scale(likelihoodCount), Scale(impactCount),
            appetite, Actor, Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, method.PublishedVersion);
    }

    [Fact]
    public void ShouldKeepMethodIdentityPerProgramGivenTenantAndProgram()
    {
        // Arrange
        var otherProgram = Uuid.CreateVersion4();

        // Act
        var id = RiskMethod.IdFor(TenantId, ProgramId);

        // Assert
        Assert.Equal(id, RiskMethod.IdFor(TenantId, ProgramId));
        Assert.NotEqual(id, RiskMethod.IdFor(TenantId, otherProgram));
        Assert.NotEqual(id, RiskMethod.IdFor(Uuid.CreateVersion4(), ProgramId));
    }

    internal static IReadOnlyList<string> Scale(int count = 5) =>
        [.. Enumerable.Range(1, count).Select(level => $"Level {level}")];
}

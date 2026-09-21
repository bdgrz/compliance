using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ActorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldKeepIdentifierUniqueWithinProgramGivenNormalizedCase()
    {
        // Arrange
        var otherProgram = Uuid.CreateVersion4();
        var otherTenant = Uuid.CreateVersion4();

        // Act
        var id = RiskDraft.IdFor(TenantId, ProgramId, "r-01");

        // Assert
        Assert.Equal(id, RiskDraft.IdFor(TenantId, ProgramId, " R-01 "));
        Assert.NotEqual(id, RiskDraft.IdFor(TenantId, otherProgram, "R-01"));
        Assert.NotEqual(id, RiskDraft.IdFor(otherTenant, ProgramId, "R-01"));
    }

    [Fact]
    public void ShouldReplayOnlyOriginalCreateAndRejectStaleRevisionGivenDraft()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var riskId = RiskDraft.IdFor(TenantId, ProgramId, "R-01");
        var risk = new RiskDraft(TenantId, riskId);
        Assert.True(risk.Create(ProgramId, requestId, "r-01", Content(), ActorId,
            "Author", Now).IsSuccess);
        Assert.True(risk.Revise(ProgramId, 1, Content() with { Scenario = "Changed" },
            ActorId, "Editor", Now.AddMinutes(1)).IsSuccess);

        // Act
        var replay = risk.Create(ProgramId, requestId, "R-01", Content(), ActorId,
            "Author", Now);
        var duplicate = risk.Create(ProgramId, Uuid.CreateVersion4(), "R-01", Content(),
            ActorId, "Other", Now);
        var stale = risk.Revise(ProgramId, 1, Content(), ActorId, "Other", Now);
        var wrongProgram = risk.Revise(Uuid.CreateVersion4(), 2, Content(),
            ActorId, "Other", Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(riskId, replay.Value.RiskId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(duplicate.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(wrongProgram.Error).Kind);
        Assert.Equal(2, risk.Revision);
        Assert.Collection(new AggregateScenario<RiskDraft>(risk).PendingEvents,
            ev => Assert.Equal(ActorId, Assert.IsType<RiskDraftCreated>(ev).ActorMemberId),
            ev => Assert.Equal("Editor", Assert.IsType<RiskDraftRevised>(ev).ActorDisplay));
    }

    [Fact]
    public void ShouldRejectInvalidAndOversizeDraftBeforeAppendGivenNewRisk()
    {
        // Arrange
        var risk = new RiskDraft(TenantId, Uuid.CreateVersion4());
        var requestId = Uuid.CreateVersion4();

        // Act
        var identifier = risk.Create(ProgramId, requestId, "R 01", Content(), ActorId,
            "Author", Now);
        var blank = risk.Create(ProgramId, requestId, "R-01",
            Content() with { Scenario = " " }, ActorId, "Author", Now);
        var huge = risk.Create(ProgramId, requestId, "R-01",
            Content() with { PotentialEffect = new string('e', 8001) },
            ActorId, "Author", Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(identifier.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(blank.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(huge.Error).Kind);
        Assert.Empty(new AggregateScenario<RiskDraft>(risk).PendingEvents);
    }

    static RiskDraftContent Content() => new("Provider outage",
        "An upstream provider is unavailable", "The service cannot process requests",
        "Management observation");
}

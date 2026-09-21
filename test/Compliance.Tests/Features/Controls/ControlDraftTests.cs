using Bdgrz.Compliance.Features.Controls;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlDraftTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldUseSameIdentityGivenNormalizedIdentifierWithinProgram()
    {
        // Arrange
        var otherProgramId = Uuid.CreateVersion4();
        var otherTenantId = Uuid.CreateVersion4();

        // Act
        var first = ControlDraft.IdFor(TenantId, ProgramId, "ac-01");
        var normalized = ControlDraft.IdFor(TenantId, ProgramId, " AC-01 ");

        // Assert
        Assert.Equal(first, normalized);
        Assert.NotEqual(first, ControlDraft.IdFor(TenantId, otherProgramId, "AC-01"));
        Assert.NotEqual(first, ControlDraft.IdFor(otherTenantId, ProgramId, "AC-01"));
    }

    [Fact]
    public void ShouldReplayOnlyOriginalCreateGivenDuplicateIdentifier()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var control = new ControlDraft(TenantId, controlId);
        var original = Content();
        Assert.True(control.Create(ProgramId, requestId, "ac-01", original,
            AuthorId, "First author", Now).IsSuccess);
        Assert.True(control.Revise(ProgramId, 1, original with { Title = "Updated title" },
            AuthorId, "Second author", Now.AddMinutes(1)).IsSuccess);

        // Act
        var replay = control.Create(ProgramId, requestId, " AC-01 ", Content(),
            AuthorId, "First author", Now.AddMinutes(2));
        var duplicate = control.Create(ProgramId, Uuid.CreateVersion4(), "AC-01",
            Content(), AuthorId, "Another author", Now.AddMinutes(2));
        var changedReplay = control.Create(ProgramId, requestId, "AC-01",
            original with { Title = "Different title" }, AuthorId, "First author", Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(controlId, replay.Value.ControlId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(duplicate.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedReplay.Error).Kind);
        Assert.Equal(2, new AggregateScenario<ControlDraft>(control).PendingEvents.Count);
    }

    [Fact]
    public void ShouldPreserveAttributionAndRejectStaleOrWrongProgramRevisionGivenRevisedDraft()
    {
        // Arrange
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-02");
        var control = new ControlDraft(TenantId, controlId);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-02", Content(),
            AuthorId, "Author One", Now).IsSuccess);
        var nextAuthorId = Uuid.CreateVersion4();
        Assert.True(control.Revise(ProgramId, 1, Content() with { Title = "Revised" },
            nextAuthorId, "Author Two", Now.AddMinutes(1)).IsSuccess);

        // Act
        var stale = control.Revise(ProgramId, 1, Content(), AuthorId, "Author One", Now);
        var wrongProgram = control.Revise(Uuid.CreateVersion4(), 2, Content(),
            AuthorId, "Author One", Now);

        // Assert
        Assert.Equal(2, control.Revision);
        Assert.Equal(ProgramId, control.ProgramId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Contains("2", Assert.IsType<RequestError>(stale.Error).Message,
            StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(wrongProgram.Error).Kind);
        Assert.Collection(new AggregateScenario<ControlDraft>(control).PendingEvents,
            ev =>
            {
                Assert.Equal(typeof(ControlDraftCreated), ev.GetType());
                Assert.Equal(AuthorId, ev.GetType().GetProperty("ActorMemberId")?.GetValue(ev));
                Assert.Equal("Author One", ev.GetType().GetProperty("ActorDisplay")?.GetValue(ev));
                Assert.Equal("AC-02", ev.GetType().GetProperty("Identifier")?.GetValue(ev));
            },
            ev =>
            {
                Assert.Equal(typeof(ControlDraftRevised), ev.GetType());
                Assert.Equal(nextAuthorId, ev.GetType().GetProperty("ActorMemberId")?.GetValue(ev));
                Assert.Equal("Author Two", ev.GetType().GetProperty("ActorDisplay")?.GetValue(ev));
                Assert.Equal(2L, ev.GetType().GetProperty("Revision")?.GetValue(ev));
            });
    }

    [Fact]
    public void ShouldRejectInvalidIdentifierAndContentWithoutEventsGivenNewDraft()
    {
        // Arrange
        var control = new ControlDraft(TenantId, Uuid.CreateVersion4());
        var requestId = Uuid.CreateVersion4();

        // Act
        var identifier = control.Create(ProgramId, requestId, "AC 01", Content(),
            AuthorId, "Author", Now);
        var evidence = control.Create(ProgramId, requestId, "AC-01",
            Content() with { ExpectedEvidenceDescriptions = [" "] },
            AuthorId, "Author", Now);
        var narrative = control.Create(ProgramId, requestId, "AC-01",
            Content() with { ImplementationNarrative = new string('x', 12001) },
            AuthorId, "Author", Now);
        var payload = control.Create(ProgramId, requestId, "AC-01",
            Content() with
            {
                ImplementationNarrative = new string('n', 12000),
                ExpectedEvidenceDescriptions = Enumerable.Repeat(new string('e', 2000), 20)
                    .ToArray(),
            }, AuthorId, "Author", Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(identifier.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(evidence.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(narrative.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(payload.Error).Kind);
        Assert.Empty(new AggregateScenario<ControlDraft>(control).PendingEvents);
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews the access list quarterly.",
        ["Dated review record", "Approved access list"]);
}

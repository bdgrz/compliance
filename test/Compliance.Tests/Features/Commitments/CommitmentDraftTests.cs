using Bdgrz.Compliance.Features.Commitments;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Commitments;

public sealed class CommitmentDraftTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ServiceId = Uuid.CreateVersion4();
    static readonly Uuid AuthorId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("service_commitment")]
    [InlineData("system_requirement")]
    [InlineData("user_entity_responsibility")]
    [InlineData("subservice_responsibility")]
    public void ShouldCreateDistinctDraftKindGivenSupportedKind(string kind)
    {
        // Arrange
        var id = CommitmentDraft.IdFor(TenantId, ProgramId, kind, "SRC-01");
        var draft = new CommitmentDraft(TenantId, id);

        // Act
        var result = draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId, kind,
            "src-01", "The authored statement", "Draft context", "Contract section 4",
            AuthorId, "Author", Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(kind, result.Value.Kind);
        Assert.Equal("SRC-01", result.Value.Identifier);
        Assert.Equal(id, result.Value.DraftId);
        Assert.Equal(1, draft.Revision);
        Assert.Single(new AggregateScenario<CommitmentDraft>(draft).PendingEvents);
    }

    [Fact]
    public void ShouldPreserveKindAndSourceHistoryGivenRevisionAndReplay()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var id = CommitmentDraft.IdFor(TenantId, ProgramId, "service_commitment", "SC-01");
        var draft = new CommitmentDraft(TenantId, id);
        Assert.True(draft.Create(ProgramId, requestId, ServiceId, "service_commitment",
            "SC-01", "First statement", "Context", "Source A", AuthorId,
            "First author", Now).IsSuccess);

        // Act
        var revised = draft.Revise(ProgramId, 1, "Second statement", "Updated context",
            "Source B", AuthorId, "Second author", Now.AddMinutes(1));
        var replay = draft.Create(ProgramId, requestId, ServiceId, "service_commitment",
            "sc-01", "First statement", "Context", "Source A", AuthorId,
            "First author", Now);
        var duplicate = draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "service_commitment", "SC-01", "First statement", "Context", "Source A",
            AuthorId, "Another author", Now);
        var stale = draft.Revise(ProgramId, 1, "Third statement", "Context", "Source C",
            AuthorId, "Author", Now);
        var wrongProgram = draft.Revise(Uuid.CreateVersion4(), 2, "Third statement",
            "Context", "Source C", AuthorId, "Author", Now);

        // Assert
        Assert.True(revised.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(duplicate.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(wrongProgram.Error).Kind);
        Assert.Collection(new AggregateScenario<CommitmentDraft>(draft).PendingEvents,
            created =>
            {
                Assert.Equal(typeof(CommitmentDraftCreated), created.GetType());
                Assert.Equal("Source A", created.GetType()
                    .GetProperty("SourceReference")?.GetValue(created));
                Assert.Equal("First author", created.GetType()
                    .GetProperty("ActorDisplay")?.GetValue(created));
            },
            changed =>
            {
                Assert.Equal(typeof(CommitmentDraftRevised), changed.GetType());
                Assert.Equal("Source B", changed.GetType()
                    .GetProperty("SourceReference")?.GetValue(changed));
                Assert.Equal("Second author", changed.GetType()
                    .GetProperty("ActorDisplay")?.GetValue(changed));
                Assert.Equal(2L, changed.GetType()
                    .GetProperty("Revision")?.GetValue(changed));
            });
    }

    [Fact]
    public void ShouldRejectUnsupportedOrUnboundedDraftWithoutEventGivenNewDraft()
    {
        // Arrange
        var draft = new CommitmentDraft(TenantId, Uuid.CreateVersion4());

        // Act
        var unsupported = draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "control", "SC-01", "Statement", "", "Source", AuthorId, "Author", Now);
        var invalidIdentifier = draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "service_commitment", "SC 01", "Statement", "", "Source", AuthorId,
            "Author", Now);
        var unsourced = draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "service_commitment", "SC-01", "Statement", "", " ", AuthorId,
            "Author", Now);
        var oversized = draft.Create(ProgramId, Uuid.CreateVersion4(), ServiceId,
            "service_commitment", "SC-01", new string('界', 16000),
            new string('界', 16000), "Source", AuthorId, "Author", Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(unsupported.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(invalidIdentifier.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(unsourced.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(oversized.Error).Kind);
        Assert.Empty(new AggregateScenario<CommitmentDraft>(draft).PendingEvents);
    }
}

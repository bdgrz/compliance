using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class PersonTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReplayIdenticalRecordAndRejectChangedContentGivenRecordedPerson()
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record(" Ada Lovelace ", " ada@example.com ", Author, Now).IsSuccess);

        // Act
        var replay = person.Record("Ada Lovelace", "ada@example.com", Author, Now);
        var changed = person.Record("Ada King", "ada@example.com", Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(person.Id, replay.Value.PersonId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        var recorded = Assert.IsType<PersonRecorded>(
            Assert.Single(new AggregateScenario<Person>(person).PendingEvents));
        Assert.Equal("Ada Lovelace", recorded.DisplayName);
        Assert.Equal("ada@example.com", recorded.WorkEmail);
        Assert.Equal(Author, recorded.Actor);
    }

    [Fact]
    public void ShouldAdvanceRevisionAndRejectStaleOrMissingGivenRevise()
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());
        var missing = new Person(TenantId, Uuid.CreateVersion4());
        var editor = ActorReference.ForMember(Uuid.CreateVersion4(), "Editor");
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);

        // Act
        var revised = person.Revise(1, "Ada King", "ada@example.com", editor, Now.AddMinutes(1));
        var stale = person.Revise(1, "Stale", null, editor, Now.AddMinutes(2));
        var absent = missing.Revise(1, "Nobody", null, editor, Now);

        // Assert
        Assert.True(revised.IsSuccess);
        Assert.Equal(2, person.Revision);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(absent.Error).Kind);
        var ev = Assert.IsType<PersonRevised>(
            new AggregateScenario<Person>(person).PendingEvents[1]);
        Assert.Equal(2, ev.Revision);
        Assert.Equal(editor, ev.Actor);
    }

    [Theory]
    [InlineData(" ", null)]
    [InlineData("Ada", "not-an-email")]
    [InlineData("Ada", "@example.com")]
    [InlineData("Ada", "ada@")]
    [InlineData("Ada", "ada lovelace@example.com")]
    public void ShouldRejectBeforeAppendGivenInvalidPerson(string displayName, string? workEmail)
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());

        // Act
        var result = person.Record(displayName, workEmail, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.False(person.IsCreated);
        Assert.Empty(new AggregateScenario<Person>(person).PendingEvents);
    }

    [Fact]
    public void ShouldRejectGivenOversizeDisplayName()
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());

        // Act
        var result = person.Record(new string('a', 201), null, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }
}

using Bdgrz.Compliance.Features.Versioning;
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
    public void ShouldRecordRestrictedPersonalContactGivenValidPerson()
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());
        var contact = new PersonalContactDetails("ada.personal@example.com", "+1 202 555 0147");

        // Act
        var result = person.Record("Ada Lovelace", "ada@example.com", contact, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
        var recorded = Assert.IsType<PersonRecorded>(
            Assert.Single(new AggregateScenario<Person>(person).PendingEvents));
        Assert.Equal(contact, recorded.PersonalContact);
        Assert.Equal(Author, recorded.Actor);
        var changedReplay = person.Record("Ada Lovelace", "ada@example.com",
            new PersonalContactDetails("different@example.com", contact.PersonalPhone), Author, Now);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(changedReplay.Error).Kind);
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
        Assert.Null(revised);
        Assert.Equal(2, person.Revision);
        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(absent).Code);
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

    [Fact]
    public void ShouldCorrelateAndClearMembershipGivenCurrentRevision()
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);
        var userId = Uuid.CreateVersion4();

        // Act
        var linked = person.CorrelateMembership(1, userId, Author, Now);
        var unchanged = person.CorrelateMembership(2, userId, Author, Now);
        var cleared = person.CorrelateMembership(2, null, Author, Now);

        // Assert
        Assert.Null(linked);
        Assert.Null(unchanged);
        Assert.Null(cleared);
        Assert.Equal(3, person.Revision);
        Assert.Null(person.CorrelatedUserId);
        var events = new AggregateScenario<Person>(person).PendingEvents
            .OfType<PersonMembershipCorrelated>().ToList();
        Assert.Equal([userId, (Uuid?)null], events.Select(ev => ev.UserId));
        Assert.Equal([2L, 3L], events.Select(ev => ev.Revision));
        Assert.All(events, ev => Assert.Equal(Author, ev.Actor));
    }

    [Fact]
    public void ShouldRejectCorrelationGivenStaleRevisionOrMissingPerson()
    {
        // Arrange
        var person = new Person(TenantId, Uuid.CreateVersion4());
        Assert.True(person.Record("Ada Lovelace", null, Author, Now).IsSuccess);
        var absent = new Person(TenantId, Uuid.CreateVersion4());

        // Act
        var stale = person.CorrelateMembership(5, Uuid.CreateVersion4(), Author, Now);
        var missing = absent.CorrelateMembership(1, Uuid.CreateVersion4(), Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(missing).Code);
        Assert.Equal(1, person.Revision);
    }
}

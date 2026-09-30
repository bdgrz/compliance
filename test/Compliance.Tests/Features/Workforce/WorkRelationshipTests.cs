using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class WorkRelationshipTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid PersonId = Uuid.CreateVersion4();
    static readonly Uuid ManagerId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Start = new(2025, 1, 6);

    static WorkRelationshipTerms Employee(string status = "active", DateOnly? end = null) =>
        new("employee", status, Start, end, "Engineering", ManagerId, null);

    static WorkRelationship New(string workerId = "E-100") =>
        new(TenantId, WorkRelationship.IdFor(TenantId, workerId));

    [Fact]
    public void ShouldKeyRelationshipBySourceWorkerIdGivenCaseAndSpacing()
    {
        // Arrange
        var otherTenant = Uuid.CreateVersion4();

        // Act
        var spaced = WorkRelationship.IdFor(TenantId, " E-100 ");
        var lower = WorkRelationship.IdFor(TenantId, "e-100");
        var foreign = WorkRelationship.IdFor(otherTenant, "E-100");

        // Assert
        Assert.Equal(lower, spaced);
        Assert.NotEqual(lower, foreign);
    }

    [Fact]
    public void ShouldReplayIdenticalRecordAndRejectAnotherPersonGivenSameWorkerId()
    {
        // Arrange
        var relationship = New();
        Assert.True(relationship.Record(PersonId, " e-100 ", Employee(), Author, Now).IsSuccess);

        // Act
        var replay = relationship.Record(PersonId, "E-100", Employee(), Author, Now);
        var otherPerson = relationship.Record(Uuid.CreateVersion4(), "E-100", Employee(), Author, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(otherPerson.Error).Kind);
        var recorded = Assert.IsType<WorkRelationshipRecorded>(
            Assert.Single(new AggregateScenario<WorkRelationship>(relationship).PendingEvents));
        Assert.Equal("E-100", recorded.SourceWorkerId);
        Assert.Equal(PersonId, recorded.PersonId);
        Assert.Equal(Author, recorded.Actor);
    }

    [Theory]
    [InlineData("volunteer", "active", null, "employee")]
    [InlineData("employee", "retired", null, "employee")]
    [InlineData("employee", "ended", null, "employee")]
    [InlineData("employee", "active", "2024-12-31", "employee")]
    [InlineData("external_collaborator", "active", null, "external_collaborator")]
    public void ShouldRejectRecordGivenInvalidTerms(string workerType, string status, string? end,
        string _)
    {
        // Arrange
        var relationship = New();
        var terms = new WorkRelationshipTerms(workerType, status, Start,
            end is null ? null : DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), null, null, null);

        // Act
        var result = relationship.Record(PersonId, "E-100", terms, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
    }

    [Fact]
    public void ShouldRejectRecordGivenSelfManagerOrBlankWorkerId()
    {
        // Arrange
        var relationship = New();

        // Act
        var self = relationship.Record(PersonId, "E-100",
            Employee() with { ManagerPersonId = PersonId }, Author, Now);
        var blank = relationship.Record(PersonId, "  ", Employee(), Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(self.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(blank.Error).Kind);
    }

    [Fact]
    public void ShouldAcceptExternalCollaboratorGivenInternalSponsor()
    {
        // Arrange
        var relationship = New("X-7");
        var terms = new WorkRelationshipTerms("external_collaborator", "active", Start, null, null, null,
            Uuid.CreateVersion4());

        // Act
        var result = relationship.Record(PersonId, "X-7", terms, Author, Now);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ShouldRecordLeaverAndRejectStaleGivenRevise()
    {
        // Arrange
        var relationship = New();
        var missing = New("E-404");
        Assert.True(relationship.Record(PersonId, "E-100", Employee(), Author, Now).IsSuccess);

        // Act
        var ended = relationship.Revise(1, Employee("ended", new DateOnly(2026, 9, 1)), Author, Now.AddMinutes(1));
        var stale = relationship.Revise(1, Employee(), Author, Now.AddMinutes(2));
        var absent = missing.Revise(1, Employee(), Author, Now);

        // Assert
        Assert.Null(ended);
        Assert.Equal(2, relationship.Revision);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.MissingRecord, Assert.IsType<CommandFailure>(absent).Code);
        var revised = Assert.IsType<WorkRelationshipRevised>(
            new AggregateScenario<WorkRelationship>(relationship).PendingEvents[^1]);
        Assert.Equal("ended", revised.Terms.LifecycleStatus);
        Assert.Equal(new DateOnly(2026, 9, 1), revised.Terms.EndDate);
    }
}

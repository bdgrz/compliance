using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Workforce;

public sealed class WorkforceSourceObservationTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid PersonId = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainImmutableSourceIdentityAndConflictOnChangedRetryGivenRecordedObservation()
    {
        // Arrange
        var identity = new WorkforceSourceIdentity("hris", "people-system", "worker-100", "v1");
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", "ada@example.com"));
        var observation = new WorkforceSourceObservation(TenantId,
            WorkforceSourceObservation.IdFor(TenantId, identity));

        // Act
        var recorded = observation.Record(identity, "person", PersonId, 2, facts, Now, Author, Now);
        var retry = observation.Record(identity, "person", PersonId, 2, facts, Now, Author, Now);
        var changed = observation.Record(identity, "person", PersonId, 2,
            facts with { Person = new WorkforcePersonSourceFacts("Grace", null) }, Now, Author, Now);

        // Assert
        Assert.True(recorded.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changed.Error).Kind);
        var ev = Assert.IsType<WorkforceSourceObserved>(Assert.Single(
            new AggregateScenario<WorkforceSourceObservation>(observation).PendingEvents));
        Assert.Equal(identity, ev.Source);
        Assert.Equal(PersonId, ev.TargetId);
        Assert.Equal(2, ev.ObservedTargetRevision);
        Assert.Equal(Author, ev.Actor);
    }

    [Theory]
    [InlineData("manual", "person", false)]
    [InlineData("provider", "person", false)]
    [InlineData("hris", "service_identity", false)]
    [InlineData("idp", "person", true)]
    public void ShouldRequireCompatibleSourceAndExactlyOneFactPayloadGivenSourceObservation(
        string sourceKind, string targetKind, bool mixedFacts)
    {
        // Arrange
        var source = new WorkforceSourceIdentity(sourceKind, "system", "100", "v1");
        var observation = new WorkforceSourceObservation(TenantId,
            WorkforceSourceObservation.IdFor(TenantId, source));
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null),
            WorkRelationship: mixedFacts ? new WorkRelationshipTerms("employee", "active",
                new DateOnly(2025, 1, 1), null, null, null, null) : null);

        // Act
        var result = observation.Record(source, targetKind, PersonId, 1, facts, Now, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<WorkforceSourceObservation>(observation).PendingEvents);
    }

    [Fact]
    public void ShouldNormalizeSourceIdentityWithoutConflatingCaseOrTenantGivenStableSourceKeys()
    {
        // Arrange
        var source = new WorkforceSourceIdentity("hris", "system", "worker", "v1");
        var padded = source with { SourceRecordId = " worker " };

        // Act
        var id = WorkforceSourceObservation.IdFor(TenantId, source);
        var same = WorkforceSourceObservation.IdFor(TenantId, padded);
        var otherCase = WorkforceSourceObservation.IdFor(TenantId, source with { SourceRecordId = "WORKER" });
        var otherTenant = WorkforceSourceObservation.IdFor(Uuid.CreateVersion4(), source);

        // Assert
        Assert.Equal(id, same);
        Assert.NotEqual(id, otherCase);
        Assert.NotEqual(id, otherTenant);
    }

    [Fact]
    public void ShouldPreviewAuthorityAndConflictsWithoutRestrictedValuesGivenSourceDisagreement()
    {
        // Arrange
        var terms = new WorkRelationshipTerms("employee", "active", new DateOnly(2025, 1, 1),
            null, "Engineering", Uuid.CreateVersion4(), null, "private reason");
        var observed = new WorkforceSourceObserved(TenantId, Uuid.CreateVersion4(),
            new WorkforceSourceIdentity("hris", "system", "100", "v1"), "work_relationship",
            Uuid.CreateVersion4(), 1, new WorkforceSourceFacts(WorkRelationship: terms), Now, Author, Now);
        var current = new WorkforceSourceFacts(WorkRelationship: terms with
        {
            Department = "Finance",
            ManagerPersonId = null,
            EmploymentStatusReason = null,
        });

        // Act
        var preview = WorkforceSourceReconciliation.Preview(observed, 1, 2, current, null);

        // Assert
        Assert.Equal("authoritative", preview.SourceAuthority);
        Assert.Equal(["department"], preview.ConflictingFields);
        Assert.True(preview.RestrictedFieldsConflict);
        Assert.False(preview.CanAccept);
        Assert.Equal(1, preview.ObservedTargetRevision);
        Assert.Equal(2, preview.CurrentTargetRevision);
    }

    [Fact]
    public void ShouldRequireMatchingCanonicalFactsAndRevisionGivenSourceAcceptance()
    {
        // Arrange
        var source = new WorkforceSourceIdentity("idp", "system", "100", "v1");
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null));
        var observation = new WorkforceSourceObservation(TenantId, WorkforceSourceObservation.IdFor(TenantId, source));
        Assert.True(observation.Record(source, "person", PersonId, 1, facts, Now, Author, Now).IsSuccess);

        // Act
        var different = observation.Reconcile(1, 1, 1,
            facts with { Person = new WorkforcePersonSourceFacts("Grace", null) },
            "accepted", "Checked HRIS against roster", Author, Now);
        var stale = observation.Reconcile(1, 1, 2, facts, "accepted", "Checked", Author, Now);
        var staleObservation = observation.Reconcile(2, 2, 2, facts, "accepted", "Checked", Author, Now);
        var accepted = observation.Reconcile(1, 2, 2, facts, "accepted", "Checked", Author, Now);
        var retry = observation.Reconcile(1, 2, 3, facts, "accepted", "Checked", Author, Now);
        var changed = observation.Reconcile(1, 2, 2, facts, "dismissed", "Changed", Author, Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(different).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(staleObservation).Code);
        Assert.Null(accepted);
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(2, observation.Revision);
        Assert.Equal(2, observation.Decision!.TargetRevision);
        Assert.Equal(Author, observation.Decision.Actor);
        Assert.False(WorkforceSourceReconciliation.Preview(observation.Observation!, 2, 3, facts,
            observation.Decision).AcceptedForCurrentRevision);
    }

    [Fact]
    public void ShouldKeepCanonicalWithAttributedReasonGivenDismissedSourceConflict()
    {
        // Arrange
        var source = new WorkforceSourceIdentity("idp", "system", "100", "v1");
        var facts = new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts("Ada", null));
        var observation = new WorkforceSourceObservation(TenantId, WorkforceSourceObservation.IdFor(TenantId, source));
        Assert.True(observation.Record(source, "person", PersonId, 1, facts, Now, Author, Now).IsSuccess);

        // Act
        var dismissed = observation.Reconcile(1, 1, 1,
            facts with { Person = new WorkforcePersonSourceFacts("Grace", null) },
            "dismissed", "HRIS authoritative name retained", Author, Now);

        // Assert
        Assert.Null(dismissed);
        Assert.Equal("dismissed", observation.Decision!.Outcome);
        Assert.Equal("HRIS authoritative name retained", observation.Decision.Note);
        Assert.Equal(Author, observation.Decision.Actor);
    }

    [Theory]
    [InlineData("person")]
    [InlineData("work_relationship")]
    [InlineData("service_identity")]
    public void ShouldRejectUnboundedSourceFactsGivenCanonicalFieldLimits(string targetKind)
    {
        // Arrange
        var source = new WorkforceSourceIdentity(targetKind == "service_identity" ? "provider" : "hris",
            "system", "100", "v1");
        var facts = targetKind switch
        {
            "person" => new WorkforceSourceFacts(Person: new WorkforcePersonSourceFacts(new string('a', 201), null)),
            "work_relationship" => new WorkforceSourceFacts(WorkRelationship: new WorkRelationshipTerms("employee",
                "active", new DateOnly(2025, 1, 1), null, null, null, null, new string('a', 1001))),
            _ => new WorkforceSourceFacts(ServiceIdentity: new WorkforceServiceSourceFacts("Bot", "bot",
                new string('a', 101), "active", null)),
        };
        var observation = new WorkforceSourceObservation(TenantId, WorkforceSourceObservation.IdFor(TenantId, source));

        // Act
        var result = observation.Record(source, targetKind, PersonId, 1, facts, Now, Author, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(new AggregateScenario<WorkforceSourceObservation>(observation).PendingEvents);
    }
}

using System.Text.Json;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.AccessControl;
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
    public void ShouldKeepNewDeclarationsUnresolvedGivenLegacyDraftEventContent()
    {
        // Arrange
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-LEGACY");
        var createRequestId = Uuid.CreateVersion4();
        var json = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","control_id":"{{controlId}}","create_request_id":"{{createRequestId}}","identifier":"AC-LEGACY","content":{"title":"Access review","objective":"Review access","description":"Management reviews access","implementation_narrative":"The owner reviews the access list quarterly.","expected_evidence_descriptions":["Dated review record"]},"actor_member_id":"{{AuthorId}}","actor_display":"Author","changed_at":"2026-09-22T12:00:00+00:00"}
            """;

        // Act
        var created = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.ControlDraftCreated);

        // Assert
        Assert.NotNull(created);
        Assert.Null(created.Content.OwnerReference);
        Assert.Null(created.Content.Applicability);
        // The fixture is deliberately a pre-snapshot (legacy-shape) event with no "actor".
        Assert.Null(created.StoredActor);
        Assert.Equal(ActorReference.ForMember(AuthorId, "Author"), created.Actor);
    }

    [Fact]
    public void ShouldReplayOnlyOriginalCreateGivenDuplicateIdentifier()
    {
        // Arrange
        var requestId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-01");
        var original = Content();
        AggregateScenario<ControlDraft> Revised()
        {
            var scenario = Scenario("AC-01");
            Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Create(
                ProgramId, requestId, "ac-01", original, AuthorId, "First author", Now))).IsSuccess);
            Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Revise(
                ProgramId, 1, original with { Title = "Updated title" }, AuthorId,
                "Second author", Now.AddMinutes(1)))).IsSuccess);
            return scenario;
        }
        AggregateScenario<ControlDraft>[] scenarios = [Revised(), Revised(), Revised()];

        // Act
        var replay = scenarios[0].When(control => AggregateOutcome.CommitOnSuccess(control.Create(
            ProgramId, requestId, " AC-01 ", Content(), AuthorId, "First author",
            Now.AddMinutes(2))));
        var duplicate = scenarios[1].When(control => AggregateOutcome.CommitOnSuccess(
            control.Create(ProgramId, Uuid.CreateVersion4(), "AC-01", Content(), AuthorId,
                "Another author", Now.AddMinutes(2))));
        var changedReplay = scenarios[2].When(control => AggregateOutcome.CommitOnSuccess(
            control.Create(ProgramId, requestId, "AC-01",
                original with { Title = "Different title" }, AuthorId, "First author", Now)));

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(new ControlRegistration(controlId, "AC-01", 1), replay.Value);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(duplicate.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(changedReplay.Error).Kind);
        Assert.All(scenarios, scenario =>
        {
            Assert.Empty(scenario.PendingEvents);
            Assert.Equal(2UL, scenario.CommittedEventCount);
        });
    }

    [Fact]
    public void ShouldPreserveAttributionAndRejectStaleOrWrongProgramRevisionGivenRevisedDraft()
    {
        // Arrange
        var nextAuthorId = Uuid.CreateVersion4();
        var stale = Scenario("AC-02");
        var wrongProgram = Scenario("AC-02");
        Assert.All([stale, wrongProgram], scenario => Assert.True(scenario.When(control =>
            AggregateOutcome.CommitOnSuccess(control.Create(ProgramId, Uuid.CreateVersion4(),
                "AC-02", Content(), AuthorId, "Author One", Now))).IsSuccess));
        var created = Assert.IsType<ControlDraftCreated>(Assert.Single(stale.PendingEvents));
        Assert.All([stale, wrongProgram], scenario => Assert.True(scenario.When(control =>
            AggregateOutcome.CommitOnSuccess(control.Revise(ProgramId, 1,
                Content() with { Title = "Revised" }, nextAuthorId, "Author Two",
                Now.AddMinutes(1)))).IsSuccess));
        var revised = Assert.IsType<ControlDraftRevised>(Assert.Single(stale.PendingEvents));

        // Act
        var staleResult = stale.When(control => AggregateOutcome.CommitOnSuccess(control.Revise(
            ProgramId, 1, Content(), AuthorId, "Author One", Now)));
        var wrongProgramResult = wrongProgram.When(control => AggregateOutcome.CommitOnSuccess(
            control.Revise(Uuid.CreateVersion4(), 2, Content(), AuthorId, "Author One", Now)));

        // Assert
        Assert.Equal(2, stale.Aggregate.Revision);
        Assert.Equal(ProgramId, stale.Aggregate.ProgramId);
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(staleResult.Error).Kind);
        Assert.Contains("2", Assert.IsType<RequestError>(staleResult.Error).Message,
            StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(wrongProgramResult.Error).Kind);
        Assert.Empty(stale.PendingEvents);
        Assert.Empty(wrongProgram.PendingEvents);
        Assert.Equal((AuthorId, "Author One", "AC-02"),
            (created.ActorMemberId, created.ActorDisplay, created.Identifier));
        Assert.Equal((nextAuthorId, "Author Two", 2L),
            (revised.ActorMemberId, revised.ActorDisplay, revised.Revision));
    }

    [Fact]
    public void ShouldPreserveDeclaredOwnerAndExplicitUnresolvedApplicabilityGivenDraft()
    {
        // Arrange
        var scenario = Scenario("AC-03");
        var applicationEntryId = Uuid.CreateVersion4();
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceEntryId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var riskEntryId = Uuid.CreateVersion4();
        var content = Content() with
        {
            OwnerReference = " Security lead ",
            Applicability =
            [
                new ControlApplicabilityReference(applicationEntryId, "application",
                    " GitHub organization ", applicationId,
                    "Privileged access is administered there.", false),
                new ControlApplicabilityReference(systemInstanceEntryId, "system_instance",
                    " GitHub production ", systemInstanceId,
                    "This is the reviewed instance.", false),
                new ControlApplicabilityReference(riskEntryId, "risk",
                    " Privileged access ", null,
                    "Treatment is pending.", true),
            ],
        };
        Result<ControlRegistration> CreateNew(string identifier, ControlDraftContent draft) =>
            Scenario(identifier).When(control => AggregateOutcome.CommitOnSuccess(control.Create(
                ProgramId, Uuid.CreateVersion4(), identifier, draft, AuthorId, "Author", Now)));

        // Act
        var created = scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Create(
            ProgramId, Uuid.CreateVersion4(), "AC-03", content, AuthorId, "Author", Now)));
        var missingGovernedId = CreateNew("AC-04", content with
        {
            Applicability = [content.Applicability[2] with { Unresolved = false }],
        });
        var duplicateReference = CreateNew("AC-05", content with
        {
            Applicability = [content.Applicability[0], content.Applicability[0]],
        });
        var unresolvedApplication = CreateNew("AC-08", content with
        {
            Applicability = [content.Applicability[0] with
            {
                GovernedRecordId = null,
                Unresolved = true,
            }],
        });

        // Assert
        Assert.True(created.IsSuccess);
        var draft = Assert.IsType<ControlDraftCreated>(Assert.Single(scenario.PendingEvents));
        Assert.Equal("Security lead", draft.Content.OwnerReference);
        Assert.NotNull(draft.Content.Applicability);
        Assert.Equal(3, draft.Content.Applicability!.Count);
        var application = draft.Content.Applicability[0];
        Assert.Equal(applicationEntryId, application.EntryId);
        Assert.Equal(applicationId, application.GovernedRecordId);
        Assert.Equal("GitHub organization", application.Subject);
        Assert.False(application.Unresolved);
        var systemInstance = draft.Content.Applicability[1];
        Assert.Equal(systemInstanceEntryId, systemInstance.EntryId);
        Assert.Equal(systemInstanceId, systemInstance.GovernedRecordId);
        Assert.False(systemInstance.Unresolved);
        Assert.Equal(riskEntryId, draft.Content.Applicability[2].EntryId);
        Assert.True(draft.Content.Applicability[2].Unresolved);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(missingGovernedId.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(duplicateReference.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(unresolvedApplication.Error).Kind);
    }

    [Fact]
    public void ShouldDiscardUnpublishedDraftAndRejectFurtherEditsGivenExistingDraft()
    {
        // Arrange
        var controlId = ControlDraft.IdFor(TenantId, ProgramId, "AC-06");
        AggregateScenario<ControlDraft> Created()
        {
            var scenario = Scenario("AC-06");
            Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Create(
                ProgramId, Uuid.CreateVersion4(), "AC-06", Content(), AuthorId, "Author",
                Now))).IsSuccess);
            return scenario;
        }
        AggregateScenario<ControlDraft> Discarded()
        {
            var scenario = Created();
            Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Discard(
                ProgramId, 1, "Not needed in this program.", AuthorId, "Author",
                Now.AddMinutes(1)))).IsSuccess);
            return scenario;
        }
        var discardedScenario = Created();
        AggregateScenario<ControlDraft>[] afterDiscard = [Discarded(), Discarded(), Discarded()];

        // Act
        var stale = Created().When(control => AggregateOutcome.CommitOnSuccess(control.Discard(
            ProgramId, 0, "Not needed in this program.", AuthorId, "Author", Now)));
        var blank = Created().When(control => AggregateOutcome.CommitOnSuccess(control.Discard(
            ProgramId, 1, " ", AuthorId, "Author", Now)));
        var discarded = discardedScenario.When(control => AggregateOutcome.CommitOnSuccess(
            control.Discard(ProgramId, 1, "Not needed in this program.", AuthorId, "Author",
                Now.AddMinutes(1))));
        var revised = afterDiscard[0].When(control => AggregateOutcome.CommitOnSuccess(
            control.Revise(ProgramId, 1, Content() with { Title = "Changed" }, AuthorId, "Author",
                Now.AddMinutes(2))));
        var recreated = afterDiscard[1].When(control => AggregateOutcome.CommitOnSuccess(
            control.Create(ProgramId, Uuid.CreateVersion4(), "AC-06", Content(), AuthorId,
                "Author", Now.AddMinutes(3))));
        var discardedAgain = afterDiscard[2].When(control => AggregateOutcome.CommitOnSuccess(
            control.Discard(ProgramId, 1, "Still not needed.", AuthorId, "Author",
                Now.AddMinutes(4))));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(blank.Error).Kind);
        Assert.True(discarded.IsSuccess);
        Assert.False(discardedScenario.Aggregate.IsVisible);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(revised.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(recreated.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(discardedAgain.Error).Kind);
        Assert.Equal(1UL, discardedScenario.CommittedEventCount);
        Assert.Equal([new ControlDraftDiscarded(TenantId, ProgramId, controlId, 1, AuthorId,
                "Author", "Not needed in this program.", Now.AddMinutes(1))
            {
                StoredActor = ActorReference.ForMember(AuthorId, "Author"),
            }], discardedScenario.PendingEvents);
        Assert.All(afterDiscard, scenario => Assert.Empty(scenario.PendingEvents));
    }

    [Fact]
    public void ShouldRejectDiscardGivenCurrentApplicabilityRelationship()
    {
        // Arrange
        var scenario = Scenario("AC-07");
        Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Create(
            ProgramId, Uuid.CreateVersion4(), "AC-07", Content(), AuthorId, "Author",
            Now))).IsSuccess);
        Assert.True(scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Revise(
            ProgramId, 1, Content() with
            {
                Applicability =
                [
                    new ControlApplicabilityReference(Uuid.CreateVersion4(), "risk",
                        "Privileged access", null, "Treatment is pending.", true),
                ],
            }, AuthorId, "Author", Now.AddMinutes(1)))).IsSuccess);

        // Act
        var discarded = scenario.When(control => AggregateOutcome.CommitOnSuccess(
            control.Discard(ProgramId, 2, "No longer needed.", AuthorId, "Author",
                Now.AddMinutes(2))));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(discarded.Error).Kind);
        Assert.True(scenario.Aggregate.IsVisible);
        Assert.Equal(2UL, scenario.CommittedEventCount);
        Assert.Empty(scenario.PendingEvents);
    }

    [Theory]
    [MemberData(nameof(InvalidCreates))]
    public void ShouldRejectInvalidIdentifierAndContentWithoutEventsGivenNewDraft(
        string identifier, ControlDraftContent content)
    {
        // Arrange
        var scenario = new AggregateScenario<ControlDraft>(
            new ControlDraft(TenantId, Uuid.CreateVersion4()));

        // Act
        var result = scenario.When(control => AggregateOutcome.CommitOnSuccess(control.Create(
            ProgramId, Uuid.CreateVersion4(), identifier, content, AuthorId, "Author", Now)));

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(result.Error).Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(0UL, scenario.CommittedEventCount);
    }

    public static TheoryData<string, ControlDraftContent> InvalidCreates() => new()
    {
        { "AC 01", Content() },
        { "AC-01", Content() with { ExpectedEvidenceDescriptions = [" "] } },
        { "AC-01", Content() with { ImplementationNarrative = new string('x', 12001) } },
        {
            "AC-01", Content() with
            {
                ImplementationNarrative = new string('n', 12000),
                ExpectedEvidenceDescriptions = Enumerable.Repeat(new string('e', 2000), 20)
                    .ToArray(),
            }
        },
        {
            "AC-01", Content() with
            {
                Applicability =
                [
                    new ControlApplicabilityReference(Uuid.CreateVersion4(), "application",
                        "GitHub", Uuid.Empty, "Access is administered there.", false),
                ],
            }
        },
    };

    static AggregateScenario<ControlDraft> Scenario(string identifier) => new(
        new ControlDraft(TenantId, ControlDraft.IdFor(TenantId, ProgramId, identifier)));

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews the access list quarterly.",
        ["Dated review record", "Approved access list"]);
}

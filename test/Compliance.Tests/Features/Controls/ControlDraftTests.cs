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
    public void ShouldPreserveDeclaredOwnerAndExplicitUnresolvedApplicabilityGivenDraft()
    {
        // Arrange
        var control = new ControlDraft(TenantId,
            ControlDraft.IdFor(TenantId, ProgramId, "AC-03"));
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

        // Act
        var created = control.Create(ProgramId, Uuid.CreateVersion4(), "AC-03", content,
            AuthorId, "Author", Now);
        var missingGovernedId = new ControlDraft(TenantId, Uuid.CreateVersion4()).Create(
            ProgramId, Uuid.CreateVersion4(), "AC-04", content with
            {
                Applicability = [content.Applicability[2] with { Unresolved = false }],
            }, AuthorId, "Author", Now);
        var duplicateReference = new ControlDraft(TenantId, Uuid.CreateVersion4()).Create(
            ProgramId, Uuid.CreateVersion4(), "AC-05", content with
            {
                Applicability = [content.Applicability[0], content.Applicability[0]],
            }, AuthorId, "Author", Now);
        var unresolvedApplication = new ControlDraft(TenantId, Uuid.CreateVersion4()).Create(
            ProgramId, Uuid.CreateVersion4(), "AC-08", content with
            {
                Applicability = [content.Applicability[0] with
                {
                    GovernedRecordId = null,
                    Unresolved = true,
                }],
            }, AuthorId, "Author", Now);

        // Assert
        Assert.True(created.IsSuccess);
        var draft = Assert.IsType<ControlDraftCreated>(
            new AggregateScenario<ControlDraft>(control).PendingEvents.Single());
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
        var control = new ControlDraft(TenantId, controlId);
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-06", Content(),
            AuthorId, "Author", Now).IsSuccess);

        // Act
        var stale = control.Discard(ProgramId, 0, "Not needed in this program.",
            AuthorId, "Author", Now);
        var blank = control.Discard(ProgramId, 1, " ", AuthorId, "Author", Now);
        var discarded = control.Discard(ProgramId, 1, "Not needed in this program.",
            AuthorId, "Author", Now.AddMinutes(1));
        var revised = control.Revise(ProgramId, 1, Content() with { Title = "Changed" },
            AuthorId, "Author", Now.AddMinutes(2));
        var recreated = control.Create(ProgramId, Uuid.CreateVersion4(), "AC-06", Content(),
            AuthorId, "Author", Now.AddMinutes(3));
        var discardedAgain = control.Discard(ProgramId, 1, "Still not needed.",
            AuthorId, "Author", Now.AddMinutes(4));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, Assert.IsType<RequestError>(stale.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(blank.Error).Kind);
        Assert.True(discarded.IsSuccess);
        Assert.False(control.IsVisible);
        Assert.Equal(RequestErrorKind.NotFound, Assert.IsType<RequestError>(revised.Error).Kind);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(recreated.Error).Kind);
        Assert.Equal(RequestErrorKind.NotFound,
            Assert.IsType<RequestError>(discardedAgain.Error).Kind);
        Assert.Collection(new AggregateScenario<ControlDraft>(control).PendingEvents,
            ev => Assert.IsType<ControlDraftCreated>(ev),
            ev =>
            {
                var discardedEvent = Assert.IsType<ControlDraftDiscarded>(ev);
                Assert.Equal(1, discardedEvent.Revision);
                Assert.Equal("Not needed in this program.", discardedEvent.Rationale);
            });
    }

    [Fact]
    public void ShouldRejectDiscardGivenCurrentApplicabilityRelationship()
    {
        // Arrange
        var control = new ControlDraft(TenantId,
            ControlDraft.IdFor(TenantId, ProgramId, "AC-07"));
        Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-07", Content(),
            AuthorId, "Author", Now).IsSuccess);
        Assert.True(control.Revise(ProgramId, 1, Content() with
        {
            Applicability =
            [
                new ControlApplicabilityReference(Uuid.CreateVersion4(), "risk",
                    "Privileged access", null, "Treatment is pending.", true),
            ],
        }, AuthorId, "Author", Now.AddMinutes(1)).IsSuccess);

        // Act
        var discarded = control.Discard(ProgramId, 2, "No longer needed.",
            AuthorId, "Author", Now.AddMinutes(2));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(discarded.Error).Kind);
        Assert.True(control.IsVisible);
        Assert.Equal(2, new AggregateScenario<ControlDraft>(control).PendingEvents.Count);
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
        var emptyGovernedRecord = control.Create(ProgramId, requestId, "AC-01", Content() with
        {
            Applicability =
            [
                new ControlApplicabilityReference(Uuid.CreateVersion4(), "application",
                    "GitHub", Uuid.Empty, "Access is administered there.", false),
            ],
        }, AuthorId, "Author", Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(identifier.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(evidence.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(narrative.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation, Assert.IsType<RequestError>(payload.Error).Kind);
        Assert.Equal(RequestErrorKind.Validation,
            Assert.IsType<RequestError>(emptyGovernedRecord.Error).Kind);
        Assert.Empty(new AggregateScenario<ControlDraft>(control).PendingEvents);
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access", "The owner reviews the access list quarterly.",
        ["Dated review record", "Approved access list"]);
}

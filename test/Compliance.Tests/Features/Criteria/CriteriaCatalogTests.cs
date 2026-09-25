using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Criteria;

public sealed class CriteriaCatalogTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRejectInvalidEntriesGivenDuplicateIdOrOrphanFocus()
    {
        // Arrange
        var edition = CriteriaCatalog.Platform.Edition;
        var criterion = new Criterion(edition.EditionId, "CC6.1", "CC6.1", "security",
            "criterion", null, "Protect access to system information.");
        var orphan = new Criterion(edition.EditionId, "bdgrz:orphan", null, "security",
            "point_of_focus", "CC9.9", "Review an absent parent.");

        // Act
        // Assert
        Assert.Throws<ArgumentException>(() => new CriteriaCatalog(edition, [criterion, criterion]));
        Assert.Throws<ArgumentException>(() => new CriteriaCatalog(edition, [criterion, orphan]));
    }

    [Fact]
    public void ShouldKeepIdentifierUniquenessGivenTwoCompleteEditions()
    {
        // Arrange
        var first = CriteriaCatalog.Platform.Edition with
        {
            EditionId = Uuid.CreateVersion4(),
            EditionLabel = "test_complete_first",
            IsComplete = true,
        };
        var second = first with
        {
            EditionId = Uuid.CreateVersion4(),
            EditionLabel = "test_complete_second",
        };
        var firstEntry = new Criterion(first.EditionId, "CC6.1", "CC6.1", "security",
            "criterion", null, "Protect logical access in this test edition.");
        var secondEntry = firstEntry with
        {
            EditionId = second.EditionId,
            Summary = "Keep the second test edition separate.",
        };

        // Act
        var catalog = new CriteriaCatalog([first, second], [firstEntry, secondEntry]);

        // Assert
        Assert.Equal(2, catalog.Editions.Count);
        Assert.True(catalog.GetEdition(first.EditionId)?.IsComplete);
        Assert.True(catalog.GetEdition(second.EditionId)?.IsComplete);
        Assert.Equal(firstEntry, catalog.GetEntry(first.EditionId, "CC6.1"));
        Assert.Equal(secondEntry, catalog.GetEntry(second.EditionId, "CC6.1"));
        Assert.Equal(firstEntry, Assert.Single(catalog.ListEntries(first.EditionId,
            "security", "criterion", null)));
    }

    [Fact]
    public void ShouldKeepExactEditionSelectionGivenExplicitRemap()
    {
        // Arrange
        var foundation = CriteriaCatalog.Platform.Edition;
        var firstEdition = foundation with
        {
            EditionId = Uuid.CreateVersion4(),
            EditionLabel = "test_complete_first",
            IsComplete = true,
        };
        var secondEdition = firstEdition with
        {
            EditionId = Uuid.CreateVersion4(),
            EditionLabel = "test_complete_second",
        };
        var catalog = new CriteriaCatalog([firstEdition, secondEdition],
        [
            new(firstEdition.EditionId, "CC6.1", "CC6.1", "security", "criterion", null,
                "Test first edition."),
            new(secondEdition.EditionId, "CC6.1", "CC6.1", "security", "criterion", null,
                "Test second edition."),
        ]);
        var first = catalog.Editions[0].EditionId;
        var second = catalog.Editions[1].EditionId;
        var program = new ComplianceProgram(TenantId, ProgramId);
        var plan = new ProgramPlan(null, null, null, null, null, null);
        Assert.Null(program.Create("SOC 2", plan, MemberId, "Lead", Now));

        // Act
        var selected = program.SelectCriteriaEdition(1, first, MemberId, "Lead", Now.AddMinutes(1));
        var stale = program.SelectCriteriaEdition(1, second, MemberId, "Lead", Now.AddMinutes(2));
        var remapped = program.SelectCriteriaEdition(2, second, MemberId, "Lead", Now.AddMinutes(3));
        var events = new AggregateScenario<ComplianceProgram>(program).PendingEvents;

        // Assert
        Assert.Null(selected);
        Assert.Equal(CommandFailureCode.VersionConflict, stale?.Code);
        Assert.Null(remapped);
        Assert.Equal(second, program.CriteriaEditionId);
        Assert.Collection(events,
            ev => Assert.IsType<ProgramCreated>(ev),
            ev => Assert.Equal(first, Assert.IsType<ProgramCriteriaEditionSelected>(ev).EditionId),
            ev => Assert.Equal(second, Assert.IsType<ProgramCriteriaEditionSelected>(ev).EditionId));
    }

    [Fact]
    public async Task ShouldRejectSelectionGivenEditionIsIncomplete()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var incomplete = CriteriaCatalog.Platform.Edition with
        {
            EditionId = Uuid.CreateVersion4(),
            EditionLabel = "test_incomplete",
            IsComplete = false,
        };
        var catalog = new CriteriaCatalog(incomplete,
        [
            new(incomplete.EditionId, "CC6.1", "CC6.1", "security", "criterion", null,
                "Test incomplete edition."),
        ]);
        var handler = new SelectProgramCriteriaEditionHandler(fixture.Repository,
            catalog, TimeProvider.System);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var request = new SelectProgramCriteriaEdition(TenantId, ProgramId, 1,
            incomplete.EditionId);

        // Act
        var result = await handler.HandleAsync(new RequestContext<SelectProgramCriteriaEdition>(
            request, actor),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error.Kind);
        Assert.Equal("The criteria edition is incomplete and cannot be selected.",
            result.Error.Message);
        await foreach (var _ in fixture.Store.ReadAsync(
                           new ComplianceProgram(TenantId, ProgramId).Stream, 0,
                           CancellationToken.None))
            Assert.Fail("An incomplete edition must not append a Program event.");
    }

    [Fact]
    public void ShouldIgnoreRetryGivenSameEditionAlreadySelected()
    {
        // Arrange
        var editionId = CriteriaCatalog.Platform.Edition.EditionId;
        var program = new ComplianceProgram(TenantId, ProgramId);
        Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            MemberId, "Lead", Now));
        Assert.Null(program.SelectCriteriaEdition(1, editionId, MemberId, "Lead", Now));

        // Act
        var retry = program.SelectCriteriaEdition(1, editionId, MemberId, "Lead", Now.AddMinutes(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(2, program.Revision);
        Assert.Equal(2, new AggregateScenario<ComplianceProgram>(program).PendingEvents.Count);
    }

    [Fact]
    public void ShouldRejectStaleRetryGivenEditionReselectedAfterInterveningChange()
    {
        // Arrange
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var program = new ComplianceProgram(TenantId, ProgramId);
        Assert.Null(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null, null),
            MemberId, "Lead", Now));
        Assert.Null(program.SelectCriteriaEdition(1, first, MemberId, "Lead", Now));
        Assert.Null(program.SelectCriteriaEdition(2, second, MemberId, "Lead", Now));
        Assert.Null(program.SelectCriteriaEdition(3, first, MemberId, "Lead", Now));

        // Act
        var staleRetry = program.SelectCriteriaEdition(1, first, MemberId, "Lead", Now);
        var arbitrary = program.SelectCriteriaEdition(99, first, MemberId, "Lead", Now);
        var exactRetry = program.SelectCriteriaEdition(3, first, MemberId, "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, staleRetry?.Code);
        Assert.Equal(CommandFailureCode.VersionConflict, arbitrary?.Code);
        Assert.Null(exactRetry);
        Assert.Equal(4, program.Revision);
        Assert.Equal(4, new AggregateScenario<ComplianceProgram>(program).PendingEvents.Count);
    }

    [Fact]
    public async Task ShouldPersistExactEditionAndReplayRevisionGivenStoredSelections()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var first = CriteriaCatalog.Platform.Edition;
        var second = first with { EditionId = Uuid.CreateVersion4(), EditionLabel = "test_second" };
        var catalog = new CriteriaCatalog([first, second],
        [
            .. CriteriaCatalog.Platform.Entries,
            new(second.EditionId, "CC6.1", "CC6.1", "security", "criterion", null,
                "Test second edition."),
        ]);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var create = new CreateProgramHandler(fixture.Repository, TimeProvider.System);
        var creation = new RequestContext<CreateProgram>(new CreateProgram(TenantId, "SOC 2",
            new ProgramPlan(null, null, null, null, null, null)), actor);
        Assert.True((await create.HandleAsync(creation, CancellationToken.None)).IsSuccess);
        var programId = creation.RequestId;
        var select = new SelectProgramCriteriaEditionHandler(fixture.Repository, catalog,
            TimeProvider.System);
        Task<Result> SelectAsync(long revision, Uuid editionId) => select.HandleAsync(
            new RequestContext<SelectProgramCriteriaEdition>(
                new SelectProgramCriteriaEdition(TenantId, programId, revision, editionId),
                actor), CancellationToken.None).AsTask();

        // Act
        var selected = await SelectAsync(1, first.EditionId);
        var retried = await SelectAsync(1, first.EditionId);
        var staleRemap = await SelectAsync(1, second.EditionId);
        var remapped = await SelectAsync(2, second.EditionId);
        var unknown = await SelectAsync(3, Uuid.CreateVersion4());
        var stored = new List<ProgramCriteriaEditionSelected>();
        await foreach (var record in fixture.Store.ReadAsync(
                           new ComplianceProgram(TenantId, programId).Stream, 0,
                           CancellationToken.None))
            if (record.Event is ProgramCriteriaEditionSelected selection)
                stored.Add(selection);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(stored[0],
            ComplianceCoreJsonContext.Default.ProgramCriteriaEditionSelected));

        // Assert
        Assert.True(selected.IsSuccess);
        Assert.True(retried.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, staleRemap.Error?.Kind);
        Assert.True(remapped.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, unknown.Error?.Kind);
        Assert.Collection(stored,
            ev => Assert.Equal((2L, first.EditionId), (ev.Revision, ev.EditionId)),
            ev => Assert.Equal((3L, second.EditionId), (ev.Revision, ev.EditionId)));
        Assert.Equal(first.EditionId.ToString(),
            wire.RootElement.GetProperty("edition_id").GetString());
        Assert.Equal("member", wire.RootElement.GetProperty("actor").GetProperty("kind")
            .GetString());
    }
}

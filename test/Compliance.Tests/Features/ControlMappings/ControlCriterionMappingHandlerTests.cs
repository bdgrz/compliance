using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Criteria;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Tests.Features.Criteria;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

public sealed class ControlCriterionMappingHandlerTests
{
    static readonly Uuid CurrentEdition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-current");
    static readonly Uuid NextEdition = Uuid.CreateVersion5(Uuid.Empty, "mapping-test-next");
    const string FocusIdentifier = "bdgrz:focus:cc6.1-access";

    [Fact]
    public async Task ShouldCountOnlyAcceptedMappingsGivenPendingProposalThenIndependentReview()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var registration = await fixture.ProposeAsync("CC6.1");
        var pending = await fixture.CoverageAsync("CC6.1");

        // Act
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(registration.MappingId, registration.Revision, "accept"))
            .ExpectSuccess();

        // Assert
        Assert.Equal("unmapped", pending.CoverageState);
        Assert.Empty(pending.MappedControls);
        Assert.Equal(1, pending.PendingProposalCount);
        var mapped = await fixture.CoverageAsync("CC6.1");
        Assert.Equal("mapped", mapped.CoverageState);
        var reference = Assert.Single(mapped.MappedControls);
        Assert.Equal(fixture.ControlId, reference.ControlId);
        Assert.Equal(fixture.ControlVersionId, reference.ControlVersionId);
        Assert.Equal(0, mapped.PendingProposalCount);
        var unmapped = await fixture.QueryAsync<ListCriteriaCoverage, Page<CriterionCoverageView>>(
            new ListCriteriaCoverage(fixture.TenantId, fixture.ProgramId,
                CoverageState: "unmapped"));
        Assert.Equal(["CC6.2", FocusIdentifier], unmapped.Items.Select(item => item.Identifier));
    }

    [Fact]
    public async Task ShouldRejectReviewGivenProposerReviewsOwnMapping()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var registration = await fixture.ProposeAsync("CC6.1");

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(fixture.Review(registration.MappingId, registration.Revision, "accept"))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Forbidden);
        var mapping = await fixture.GetAsync(registration.MappingId);
        Assert.Equal("pending", mapping.Status);
        Assert.Equal("unmapped", (await fixture.CoverageAsync("CC6.1")).CoverageState);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenEditionNotSelectedByProgram()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(fixture.Propose("CC6.1", NextEdition))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AuthorUserId)
            .When(fixture.Propose("CC9.9"))
            .ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenControlVersionIsNotApproved()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(fixture.Propose("CC6.1") with { ControlVersionId = Uuid.CreateVersion4() })
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AuthorUserId)
            .When(fixture.Propose("CC6.1") with { ControlId = Uuid.CreateVersion4() })
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldRejectDuplicateGivenAcceptedMappingWithSameContent()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var first = await fixture.ProposeAsync("CC6.1");
        var retry = await fixture.ProposeAsync("CC6.1");
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(first.MappingId, first.Revision, "accept"))
            .ExpectSuccess();

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(fixture.Propose("CC6.1") with { ExpectedRevision = 2 })
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Equal(first, retry);
        await fixture.Scenario(fixture.AuthorUserId)
            .When(fixture.Propose("CC6.1") with { Rationale = "Changed", ExpectedRevision = 1 })
            .ExpectFailure(RequestErrorKind.Conflict);
        var coverage = await fixture.CoverageAsync("CC6.1");
        Assert.Single(coverage.MappedControls);
    }

    [Fact]
    public async Task ShouldPreserveVersionHistoryGivenRemappingAndRetirement()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var first = await fixture.ProposeAsync("CC6.1");
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(first.MappingId, 1, "accept")).ExpectSuccess();
        var second = await fixture.ProposeAsync("CC6.1", 2, "Narrowed rationale");
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(first.MappingId, second.Revision, "accept")).ExpectSuccess();

        await fixture.Scenario(fixture.ReviewerUserId)
            // Act
            .When(new RetireControlCriterionMapping(fixture.TenantId, fixture.ProgramId,
                first.MappingId, 4, "The control no longer addresses access provisioning."))
            // Assert
            .ExpectSuccess();
        var mapping = await fixture.GetAsync(first.MappingId);
        Assert.Equal("retired", mapping.Status);
        Assert.Equal(5, mapping.Revision);
        Assert.Equal(["superseded", "retired"], mapping.Versions.Select(v => v.Status));
        Assert.Equal("Narrowed rationale", mapping.Versions[1].Rationale);
        Assert.NotNull(mapping.Versions[0].ReviewedBy);
        Assert.Equal("unmapped", (await fixture.CoverageAsync("CC6.1")).CoverageState);
    }

    [Fact]
    public async Task ShouldKeepHistoricalEditionCoverageGivenProgramSelectsLaterEdition()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var first = await fixture.ProposeAsync(FocusIdentifier);
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(first.MappingId, 1, "accept")).ExpectSuccess();

        // Act
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                Command(program.SelectCriteriaEdition(2, NextEdition, Uuid.CreateVersion4(),
                    "Admin", DateTimeOffset.UtcNow)));

        // Assert
        var historical = await fixture.QueryAsync<ListCriteriaCoverage,
            Page<CriterionCoverageView>>(new ListCriteriaCoverage(fixture.TenantId,
            fixture.ProgramId, CurrentEdition, Kind: "point_of_focus"));
        Assert.Equal("mapped", Assert.Single(historical.Items).CoverageState);
        var current = await fixture.QueryAsync<ListCriteriaCoverage, Page<CriterionCoverageView>>(
            new ListCriteriaCoverage(fixture.TenantId, fixture.ProgramId));
        Assert.All(current.Items, item => Assert.Equal("unmapped", item.CoverageState));
        Assert.All(current.Items, item => Assert.Equal(NextEdition, item.EditionId));
        var mappings = await fixture.QueryAsync<ListControlCriterionMappings,
            Page<ControlCriterionMappingView>>(new ListControlCriterionMappings(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId));
        Assert.Equal(CurrentEdition, Assert.Single(mappings.Items).EditionId);
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenMappingFromAnotherProgram()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var registration = await fixture.ProposeAsync("CC6.1");

        await fixture.Scenario(fixture.ReviewerUserId)
            // Act
            .When(new GetControlCriterionMapping(fixture.TenantId, Uuid.CreateVersion4(),
                registration.MappingId))
            // Assert
            .ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(new GetControlCriterionMapping(Uuid.CreateVersion4(), fixture.ProgramId,
                registration.MappingId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldReportNotApplicableGivenIndependentlyAcceptedDecision()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var proposed = await fixture.Scenario(fixture.AuthorUserId)
            .When(new ProposeCriterionNotApplicable(fixture.TenantId, fixture.ProgramId,
                CurrentEdition, "CC6.2", 0, "No user accounts are approved by the service."))
            .ExpectSuccess();
        var registration = proposed.Value;
        await fixture.Scenario(fixture.AuthorUserId)
            .When(fixture.ReviewApplicability(registration.DecisionId, 1, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var pending = await fixture.CoverageAsync("CC6.2");

        // Act
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.ReviewApplicability(registration.DecisionId, 1, "accept"))
            .ExpectSuccess();

        // Assert
        Assert.Equal("unmapped", pending.CoverageState);
        var excluded = await fixture.CoverageAsync("CC6.2");
        Assert.Equal("not_applicable", excluded.CoverageState);
        Assert.Equal(registration.DecisionId, excluded.NotApplicableDecisionId);
        var filtered = await fixture.QueryAsync<ListCriteriaCoverage,
            Page<CriterionCoverageView>>(new ListCriteriaCoverage(fixture.TenantId,
            fixture.ProgramId, CoverageState: "not_applicable"));
        Assert.Equal(["CC6.2"], filtered.Items.Select(item => item.Identifier));
        var decisions = await fixture.QueryAsync<ListCriterionApplicability,
            Page<CriterionApplicabilityView>>(new ListCriterionApplicability(fixture.TenantId,
            fixture.ProgramId, Status: "not_applicable"));
        var decision = Assert.Single(decisions.Items);
        Assert.Equal("accepted", decision.Versions[0].Status);
        await fixture.Scenario(fixture.AuthorUserId)
            .When(new WithdrawCriterionNotApplicable(fixture.TenantId, fixture.ProgramId,
                registration.DecisionId, 2, "The service now approves accounts."))
            .ExpectSuccess();
        Assert.Equal("unmapped", (await fixture.CoverageAsync("CC6.2")).CoverageState);
        var withdrawn = await fixture.QueryAsync<GetCriterionApplicability,
            CriterionApplicabilityView>(new GetCriterionApplicability(fixture.TenantId,
            fixture.ProgramId, registration.DecisionId));
        Assert.Equal("withdrawn", withdrawn.Status);
    }

    [Fact]
    public async Task ShouldRejectNotApplicableGivenPointOfFocusOrUnselectedEdition()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(new ProposeCriterionNotApplicable(fixture.TenantId, fixture.ProgramId,
                CurrentEdition, FocusIdentifier, 0, "Not applicable."))
            // Assert
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.AuthorUserId)
            .When(new ProposeCriterionNotApplicable(fixture.TenantId, fixture.ProgramId,
                NextEdition, "CC6.2", 0, "Not applicable."))
            .ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldFlagRemapRequiredGivenApprovedSuccessorControlVersion()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        var registration = await fixture.ProposeAsync("CC6.1");
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(fixture.Review(registration.MappingId, registration.Revision, "accept"))
            .ExpectSuccess();
        var before = Assert.Single((await fixture.CoverageAsync("CC6.1")).MappedControls);

        // Act
        await fixture.ApproveSuccessorAsync();

        // Assert
        Assert.False(before.RemapRequired);
        var after = Assert.Single((await fixture.CoverageAsync("CC6.1")).MappedControls);
        Assert.True(after.RemapRequired);
        Assert.Equal(fixture.ControlVersionId, after.ControlVersionId);
    }

    [Fact]
    public async Task ShouldReturnTransientConflictGivenCoverageProjectionBehindSource()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync();
        await fixture.ProposeAsync("CC6.1");

        await fixture.Scenario(fixture.ReviewerUserId)
            // Act
            .When(new ListCriteriaCoverage(fixture.TenantId, fixture.ProgramId))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.ReviewerUserId)
            .When(new ListControlCriterionMappings(fixture.TenantId, fixture.ProgramId))
            .ExpectFailure(RequestErrorKind.Conflict);
        Assert.Single((await fixture.QueryAsync<ListControlCriterionMappings,
            Page<ControlCriterionMappingView>>(new ListControlCriterionMappings(
            fixture.TenantId, fixture.ProgramId))).Items);
    }

    static Result Command(CommandFailure? failure) => failure is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

    internal static CriteriaCatalog TestCatalog()
    {
        CriteriaCatalogEdition Edition(Uuid id, string label) => new(id, "tsc", label,
            DateTimeOffset.UnixEpoch, true, "Test coverage.", "https://example.com",
            "identifiers_and_original_summaries", []);
        IEnumerable<Criterion> Entries(Uuid id) =>
        [
            new(id, "CC6.1", "CC6.1", "security", "criterion", null, "Access is protected."),
            new(id, "CC6.2", "CC6.2", "security", "criterion", null, "Accounts are approved."),
            new(id, FocusIdentifier, null, "security", "point_of_focus", "CC6.1",
                "Access software restricts access."),
        ];
        return new CriteriaCatalog([Edition(CurrentEdition, "current"), Edition(NextEdition, "next")],
            Entries(CurrentEdition).Concat(Entries(NextEdition)).ToArray());
    }

    sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "AC-MAP");
        public Uuid ControlVersionId => ControlVersionIds.Initial(ControlId);

        public static async Task<Fixture> CreateAsync()
        {
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed: true),
                portia => portia.AddRequestHandler<ProposeControlCriterionMappingHandler>()
                    .AddRequestHandler<ReviewControlCriterionMappingHandler>()
                    .AddRequestHandler<RetireControlCriterionMappingHandler>()
                    .AddRequestHandler<GetControlCriterionMappingHandler>()
                    .AddRequestHandler<ListControlCriterionMappingsHandler>()
                    .AddRequestHandler<ListCriteriaCoverageHandler>()
                    .AddRequestHandler<ProposeCriterionNotApplicableHandler>()
                    .AddRequestHandler<ReviewCriterionApplicabilityHandler>()
                    .AddRequestHandler<WithdrawCriterionNotApplicableHandler>()
                    .AddRequestHandler<GetCriterionApplicabilityHandler>()
                    .AddRequestHandler<ListCriterionApplicabilityHandler>(),
                services => services.AddSingleton<ICriteriaCatalog>(TestCatalog())
                    .AddScoped<ICriteriaTextOverlayReader, EmptyCriteriaTextOverlayReader>()
                    .AddScoped<ControlActivationSource>()
                    .AddCoverageProjections());
            var fixture = new Fixture { Provider = provider };
            var now = DateTimeOffset.UtcNow;
            var admin = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(provider,
                new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                    Command(program.Create("SOC 2", new ProgramPlan(null, null, null, null, null,
                        null), admin, "Admin", now)));
            await ProgramManagementServices.SeedAsync(provider,
                new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                    Command(program.SelectCriteriaEdition(1, CurrentEdition, admin, "Admin", now)));
            var owner = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control =>
                {
                    var created = control.Create(fixture.ProgramId, Uuid.CreateVersion4(),
                        "AC-MAP", new ControlDraftContent("Access review", "Review access",
                            "Management reviews access", "Quarterly review.", ["Record"]),
                        admin, "Admin", now);
                    Assert.True(created.IsSuccess);
                    Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control",
                            fixture.ControlId, fixture.ControlVersionId, 1), Uuid.CreateVersion4(),
                        owner, ResponsibilityType.ControlOwner, admin, "Admin", now,
                        now.AddMinutes(-1), null, []));
                    Assert.Null(control.Review(fixture.ProgramId, 1, reviewId, "accept", "Ok",
                        Uuid.CreateVersion4(), "Reviewer", now));
                    return Command(control.Approve(fixture.ProgramId, 1, Uuid.CreateVersion4(),
                        reviewId, new DateOnly(2026, 10, 1), "Ready", new HashSet<Uuid> { owner },
                        Uuid.CreateVersion4(), "Approver", now));
                });
            return fixture;
        }

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public ProposeControlCriterionMapping Propose(string identifier, Uuid? edition = null,
            long expectedRevision = 0, string rationale = "Access reviews restrict access.") =>
            new(TenantId, ProgramId, ControlId, ControlVersionId, edition ?? CurrentEdition,
                identifier, expectedRevision, rationale,
                "Applies to every production system in scope.");

        public async Task<ControlCriterionMappingRegistration> ProposeAsync(string identifier,
            long expectedRevision = 0, string rationale = "Access reviews restrict access.")
        {
            var result = await Scenario(AuthorUserId)
                .When(Propose(identifier, null, expectedRevision, rationale)).ExpectSuccess();
            return result.Value;
        }

        public ReviewCriterionApplicability ReviewApplicability(Uuid decisionId, long revision,
            string outcome) => new(TenantId, ProgramId, decisionId, revision, outcome,
            "The rationale is supported.");

        public Task ApproveSuccessorAsync()
        {
            var now = DateTimeOffset.UtcNow;
            var author = Uuid.CreateVersion4();
            var owner = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            return ProgramManagementServices.SeedAsync(Provider,
                new ControlDraft(TenantId, ControlId), control =>
                {
                    Assert.Null(control.ProposeSuccessor(ProgramId, ControlVersionId,
                        new ControlDraftContent("Access review v2", "Review access",
                            "Management reviews access", "Monthly review.", ["Record"]),
                        author, "Author", now));
                    Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control",
                            ControlId, control.DraftVersionId, control.Revision),
                        Uuid.CreateVersion4(), owner, ResponsibilityType.ControlOwner, author,
                        "Author", now, now.AddMinutes(-1), null, []));
                    Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept",
                        "Ok", Uuid.CreateVersion4(), "Reviewer", now));
                    return Command(control.Approve(ProgramId, control.Revision,
                        Uuid.CreateVersion4(), reviewId, new DateOnly(2026, 11, 1), "Ready",
                        new HashSet<Uuid> { owner }, Uuid.CreateVersion4(), "Approver", now,
                        impactDigest: "DIGEST"));
                });
        }

        public ReviewControlCriterionMapping Review(Uuid mappingId, long revision,
            string outcome) => new(TenantId, ProgramId, mappingId, revision, outcome,
            "The rationale matches the criterion.");

        public Task<ControlCriterionMappingView> GetAsync(Uuid mappingId) =>
            QueryAsync<GetControlCriterionMapping, ControlCriterionMappingView>(
                new GetControlCriterionMapping(TenantId, ProgramId, mappingId));

        public async Task<CriterionCoverageView> CoverageAsync(string identifier)
        {
            var page = await QueryAsync<ListCriteriaCoverage, Page<CriterionCoverageView>>(
                new ListCriteriaCoverage(TenantId, ProgramId));
            return Assert.Single(page.Items, item => item.Identifier == identifier);
        }

        public async Task<TOut> QueryAsync<TRequest, TOut>(TRequest request)
            where TRequest : IRequest<TOut>
        {
            await CoverageProjections.CatchUpAsync(Provider, TenantId);
            var result = await Scenario(ReviewerUserId).When(request).ExpectSuccess();
            return result.Value;
        }
    }
}

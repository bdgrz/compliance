using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskGovernanceHandlerTests
{
    [Fact]
    public async Task ShouldRecordResidualUnderMitigateGivenIndependentlyAcceptedControlTreatment()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate");
        await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.Residual(2))
            .ExpectFailure(RequestErrorKind.Conflict);
        var proposed = await fixture.Scenario(fixture.AssessorUserId)
            .When(new ProposeRiskControlTreatment(fixture.TenantId, fixture.ProgramId,
                fixture.RiskId, 0, fixture.ControlId, fixture.ControlVersionId,
                "Quarterly access review lowers likelihood."))
            .ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.ReviewTreatment(proposed.Value.TreatmentId, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Act
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.ReviewTreatment(proposed.Value.TreatmentId, "accept"))
            .ExpectSuccess();
        var residual = await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.Residual(2))
            .ExpectSuccess();

        // Assert
        Assert.Equal(4, residual.Value.Score);
        var governance = await fixture.GovernanceAsync();
        var treatment = Assert.Single(governance.ControlTreatments);
        Assert.Equal("accepted", treatment.Status);
        Assert.Equal(fixture.ControlVersionId, treatment.ControlVersionId);
        Assert.Equal(2, governance.Revision);
    }

    [Fact]
    public async Task ShouldRejectTreatmentGivenControlVersionNotCurrentOrRiskNotMitigated()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("accept");

        await fixture.Scenario(fixture.AssessorUserId)
            // Act
            .When(new ProposeRiskControlTreatment(fixture.TenantId, fixture.ProgramId,
                fixture.RiskId, 0, fixture.ControlId, Uuid.CreateVersion4(), "Wrong version."))
            // Assert
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(new ProposeRiskControlTreatment(fixture.TenantId, fixture.ProgramId,
                fixture.RiskId, 0, fixture.ControlId, fixture.ControlVersionId, "Not mitigate."))
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(new ProposeRiskControlTreatment(fixture.TenantId, fixture.ProgramId,
                Uuid.CreateVersion4(), 0, fixture.ControlId, fixture.ControlVersionId, "None."))
            .ExpectFailure(RequestErrorKind.NotFound);
        Assert.Empty((await fixture.GovernanceAsync()).ControlTreatments);
    }

    [Fact]
    public async Task ShouldDenyOwnerAcceptanceGivenCorrelatedPersonOwnerWithoutWaiver()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("accept");
        var personId = await fixture.SeedPersonAsync(fixture.ApproverUserId);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(new AssignRiskOwner(fixture.TenantId, fixture.ProgramId, fixture.RiskId, 0,
                personId, "Owns the provider relationship."))
            .ExpectSuccess();
        var residual = await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.Residual(2))
            .ExpectSuccess();
        var waiverId = await fixture.SeedWaiverAsync(residual.Value.AssessmentId, 3);

        // Act
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.Accept(residual.Value.AssessmentId, null))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var accepted = await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.Accept(residual.Value.AssessmentId, waiverId))
            .ExpectSuccess();

        // Assert
        Assert.Equal(waiverId, accepted.Value.SeparationOfDutiesWaiverId);
        var owner = (await fixture.GovernanceAsync()).Owner!;
        Assert.Equal(personId, owner.PersonId);
        Assert.Equal(RbacIds.Member(fixture.TenantId, fixture.ApproverUserId),
            owner.CorrelatedMemberId);
    }

    [Fact]
    public async Task ShouldRaiseTriggersOnlyForAffectedRisksGivenSystemReaction()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("accept");
        var source = Uuid.CreateVersion4().ToString();
        var raise = new RaiseRiskReassessmentTriggers(fixture.TenantId, fixture.ProgramId,
            "boundary_changed", source, DateTimeOffset.UtcNow, null);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(raise)
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(raise).ExpectSuccess();
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(raise).ExpectSuccess();
        await RequestScenario.For(fixture.Provider).GivenActor(RequestActor.System)
            .When(new RaiseRiskReassessmentTriggers(fixture.TenantId, fixture.ProgramId,
                "method_changed", fixture.MethodVersionId.ToString(), DateTimeOffset.UtcNow,
                fixture.MethodVersionId))
            .ExpectSuccess();

        // Assert
        var trigger = Assert.Single((await fixture.GovernanceAsync()).ReassessmentTriggers);
        Assert.Equal("boundary_changed", trigger.TriggerKind);
        Assert.Equal(source, trigger.SourceReference);
        Assert.Equal("open", trigger.Status);
    }

    [Fact]
    public async Task ShouldCompleteActionOnlyAfterIndependentReviewGivenFulfilledEvidence()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate");
        var worker = await fixture.SeedMemberAsync();
        var requestId = await fixture.SeedEvidenceAsync(fulfilled: false, worker);
        var added = await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.AddAction(worker, [requestId], 0)).ExpectSuccess();
        var early = fixture.Submit(added.Value.ActionId, 1, [requestId]);
        await fixture.Scenario(fixture.AssessorUserId).When(early)
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.FulfilEvidenceAsync(requestId);

        // Act
        var submitted = await fixture.Scenario(fixture.AssessorUserId).When(early).ExpectSuccess();
        var pending = await fixture.GovernanceAsync();
        await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.ReviewAction(added.Value.ActionId, 2, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(worker.UserId)
            .When(fixture.ReviewAction(added.Value.ActionId, 2, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var unassignedReviewer = Uuid.CreateVersion4();
        await fixture.Scenario(unassignedReviewer)
            .When(fixture.ReviewAction(added.Value.ActionId, 2, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.ReviewAction(added.Value.ActionId, 2, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var reviewWorkItemId = WorkCandidate.IdFor(submitted.Value.SubmissionId,
            "risk_treatment_action_review");
        var assessor = RbacIds.Member(fixture.TenantId, fixture.AssessorUserId);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new WorkAssignmentLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AssignTo(reviewWorkItemId, 0, WorkAssignmentLedger.Assign,
                    RbacIds.Member(fixture.TenantId, fixture.ApproverUserId), null,
                    "Independent completion review.",
                    ActorReference.ForMember(assessor, "Assessor"), DateTimeOffset.UtcNow));
                return Result.Success;
            });
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.ReviewAction(added.Value.ActionId, 2, "accept")).ExpectSuccess();
        var done = await fixture.GovernanceAsync();

        // Assert
        Assert.Equal("in_progress", pending.TreatmentActionStatus);
        Assert.Equal("completion_submitted", Assert.Single(pending.TreatmentActions).Status);
        Assert.Equal(submitted.Value.SubmissionId,
            Assert.Single(Assert.Single(done.TreatmentActions).Completions).SubmissionId);
        Assert.Equal("completed", done.TreatmentActionStatus);
        Assert.Equal(3, done.Revision);
    }

    [Fact]
    public async Task ShouldReviseOpenActionGivenActiveAccountableMemberAndProgramEvidence()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate");
        var originalOwner = await fixture.SeedMemberAsync();
        var revisedOwner = await fixture.SeedMemberAsync();
        var evidenceId = await fixture.SeedEvidenceAsync(fulfilled: false, revisedOwner);
        var added = await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.AddAction(originalOwner, [], 0)).ExpectSuccess();
        var revise = new ReviseRiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, added.Value.ActionId, 1, "Enforce phishing-resistant MFA",
            "Every administrator uses a hardware key.",
            "Identity provider policy export.", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
            revisedOwner.MemberId, [evidenceId]);

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(revise).ExpectSuccess();
        var governance = await fixture.GovernanceAsync();

        // Assert
        var action = Assert.Single(governance.TreatmentActions);
        Assert.Equal("Enforce phishing-resistant MFA", action.Title);
        Assert.Equal("Every administrator uses a hardware key.", action.TargetState);
        Assert.Equal("Identity provider policy export.", action.ExpectedEvidence);
        Assert.Equal(revisedOwner.MemberId, action.AccountableMemberId);
        Assert.Equal([evidenceId], action.EvidenceRequestIds);
        Assert.Equal("open", action.Status);
        Assert.Equal(2, governance.Revision);
    }

    [Fact]
    public async Task ShouldRejectTreatmentActionEditGivenForeignEvidenceOrInactiveAccountableMember()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate");
        var other = await Fixture.CreateAsync("mitigate");
        var owner = await fixture.SeedMemberAsync();
        var otherOwner = await other.SeedMemberAsync();
        var foreignEvidenceId = await other.SeedEvidenceAsync(fulfilled: false, otherOwner);
        var added = await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.AddAction(owner, [], 0)).ExpectSuccess();
        var foreignEvidenceEdit = new ReviseRiskTreatmentAction(fixture.TenantId,
            fixture.ProgramId, fixture.RiskId, added.Value.ActionId, 1, "Updated action",
            "Updated state", "Updated evidence", DateOnly.FromDateTime(DateTime.UtcNow)
                .AddDays(30), owner.MemberId, [foreignEvidenceId]);
        var inactiveMemberEdit = new ReviseRiskTreatmentAction(fixture.TenantId,
            fixture.ProgramId, fixture.RiskId, added.Value.ActionId, 1, "Updated action",
            "Updated state", "Updated evidence", DateOnly.FromDateTime(DateTime.UtcNow)
                .AddDays(30), Uuid.CreateVersion4());

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(foreignEvidenceEdit)
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.AssessorUserId).When(inactiveMemberEdit)
            .ExpectFailure(RequestErrorKind.Validation);
        var governance = await fixture.GovernanceAsync();

        // Assert
        var action = Assert.Single(governance.TreatmentActions);
        Assert.Equal("Enforce MFA", action.Title);
        Assert.Equal(owner.MemberId, action.AccountableMemberId);
        Assert.Equal(1, governance.Revision);
    }

    [Fact]
    public async Task ShouldForbidTreatmentActionWritesGivenActorWithoutProgramManage()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate", allowed: false);
        var worker = await fixture.SeedMemberAsync();

        // Act
        await fixture.Scenario(fixture.AssessorUserId).When(fixture.AddAction(worker, [], 0))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.Submit(Uuid.CreateVersion4(), 1, [Uuid.CreateVersion4()]))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(new ReviseRiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
                fixture.RiskId, Uuid.CreateVersion4(), 0, "Updated action", "Updated state",
                "Updated evidence", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),
                Uuid.CreateVersion4()))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.ReviewAction(Uuid.CreateVersion4(), 1, "accept"))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        var ledger = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId));
        Assert.Equal(0, ledger.RevisionOf(fixture.RiskId));
    }

    [Fact]
    public async Task ShouldRejectActionGivenOtherTenantEvidenceInactiveMemberOrUnchosenTreatment()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate");
        var other = await Fixture.CreateAsync("mitigate");
        var worker = await fixture.SeedMemberAsync();
        var foreignEvidence = await other.SeedEvidenceAsync(fulfilled: true, worker);
        var accepted = await Fixture.CreateAsync("accept");
        var acceptedWorker = await accepted.SeedMemberAsync();

        // Act
        await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.AddAction(worker, [foreignEvidence], 0))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.Scenario(fixture.AssessorUserId)
            .When(fixture.AddAction(new Worker(Uuid.CreateVersion4(), Uuid.CreateVersion4()), [],
                0)).ExpectFailure(RequestErrorKind.Validation);
        await accepted.Scenario(accepted.AssessorUserId)
            .When(accepted.AddAction(acceptedWorker, [], 0))
            .ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Empty((await fixture.GovernanceAsync()).TreatmentActions);
        Assert.Empty((await accepted.GovernanceAsync()).TreatmentActions);
    }

    [Fact]
    public async Task ShouldReplayActionAndSubmissionGivenSameRequestIds()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync("mitigate");
        var worker = await fixture.SeedMemberAsync();
        var evidenceId = await fixture.SeedEvidenceAsync(fulfilled: true, worker);
        var addId = Uuid.CreateVersion4();
        var add = fixture.AddAction(worker, [evidenceId], 0);

        // Act
        var first = await fixture.ReplayScenario(fixture.AssessorUserId, add, addId)
            .ExpectSuccess();
        var second = await fixture.ReplayScenario(fixture.AssessorUserId, add, addId)
            .ExpectSuccess();
        var submitId = Uuid.CreateVersion4();
        var submit = fixture.Submit(addId, 1, [evidenceId]);
        await fixture.ReplayScenario(fixture.AssessorUserId, submit, submitId).ExpectSuccess();
        await fixture.ReplayScenario(fixture.AssessorUserId, submit, submitId).ExpectSuccess();
        await fixture.ReplayScenario(fixture.AssessorUserId, add with { Title = "Changed" },
            addId).ExpectFailure(RequestErrorKind.Conflict);

        // Assert
        Assert.Equal(first.Value, second.Value);
        var governance = await fixture.GovernanceAsync();
        Assert.Equal(2, governance.Revision);
        Assert.Single(Assert.Single(governance.TreatmentActions).Completions);
    }

    sealed record Worker(Uuid UserId, Uuid MemberId);

    sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid AssessorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public Uuid RiskId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "AC-RISK");
        public Uuid ControlVersionId => ControlVersionIds.Initial(ControlId);
        public Uuid MethodVersionId { get; private set; }

        public static async Task<Fixture> CreateAsync(string treatment, bool allowed = true)
        {
            var directory = new SingleRiskDirectory();
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed),
                portia => portia.AddRequestHandler<RecordRiskAssessmentHandler>()
                    .AddRequestHandler<AcceptRiskHandler>()
                    .AddRequestHandler<AssignRiskOwnerHandler>()
                    .AddRequestHandler<ProposeRiskControlTreatmentHandler>()
                    .AddRequestHandler<ReviewRiskControlTreatmentHandler>()
                    .AddRequestHandler<GetRiskGovernanceHandler>()
                    .AddRequestHandler<AddRiskTreatmentActionHandler>()
                    .AddRequestHandler<ReviseRiskTreatmentActionHandler>()
                    .AddRequestHandler<SubmitRiskTreatmentActionCompletionHandler>()
                    .AddRequestHandler<ReviewRiskTreatmentActionCompletionHandler>()
                    .AddRequestHandler<RaiseRiskReassessmentTriggersHandler>()
                    .AddRequestAuthorizer<RiskReassessmentReactionAuthorizer>(),
                services => services.AddScoped<ControlActivationSource>()
                    .AddScoped<RiskDraftListReadConsistency>()
                    .AddSingleton<IDomainEventReader>(services =>
                        (IDomainEventReader)services.GetRequiredService<IEventStore>())
                    .AddSingleton<IRiskDraftDirectoryReader>(directory));
            var fixture = new Fixture { Provider = provider };
            directory.Fixture = fixture;
            var now = DateTimeOffset.UtcNow;
            var assessor = RbacIds.Member(fixture.TenantId, fixture.AssessorUserId);
            await ProgramManagementServices.SeedAsync(provider,
                new RiskDraft(fixture.TenantId, fixture.RiskId), risk => risk.Create(
                    fixture.ProgramId, Uuid.CreateVersion4(), "R-01", new RiskDraftContent(
                        "Provider outage", "The provider is unavailable", "Requests fail",
                        null), assessor, "Assessor", now));
            var method = new RiskMethod(fixture.TenantId, fixture.ProgramId);
            await ProgramManagementServices.SeedAsync(provider, method, item => Command(
                item.Publish(fixture.ProgramId, 0, RiskMethodTests.Scale(),
                    RiskMethodTests.Scale(), 12, ActorReference.ForMember(assessor, "Lead"),
                    now)));
            var methodVersion = (await ProgramManagementServices.HydrateAsync(provider,
                new RiskMethod(fixture.TenantId, fixture.ProgramId))).Current!;
            fixture.MethodVersionId = methodVersion.MethodVersionId;
            await ProgramManagementServices.SeedAsync(provider,
                new RiskEvaluation(fixture.TenantId, fixture.RiskId), evaluation =>
                {
                    Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 0,
                        Uuid.CreateVersion4(), methodVersion, "inherent", 4, 4, "Severe",
                        assessor, "Assessor", now));
                    return Command(evaluation.ChooseTreatment(fixture.ProgramId, 1, treatment,
                        "Chosen.", assessor, "Assessor", now));
                });
            var owner = Uuid.CreateVersion4();
            var reviewId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control =>
                {
                    Assert.True(control.Create(fixture.ProgramId, Uuid.CreateVersion4(),
                        "AC-RISK", new ControlDraftContent("Access review", "Review access",
                            "Management reviews access", "Quarterly review.", ["Record"]),
                        assessor, "Assessor", now).IsSuccess);
                    Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control",
                            fixture.ControlId, fixture.ControlVersionId, 1),
                        Uuid.CreateVersion4(), owner, ResponsibilityType.ControlOwner, assessor,
                        "Assessor", now, now.AddMinutes(-1), null, []));
                    Assert.Null(control.Review(fixture.ProgramId, 1, reviewId, "accept", "Ok",
                        Uuid.CreateVersion4(), "Reviewer", now));
                    return Command(control.Approve(fixture.ProgramId, 1, Uuid.CreateVersion4(),
                        reviewId, new DateOnly(2026, 10, 1), "Ready",
                        new HashSet<Uuid> { owner }, Uuid.CreateVersion4(), "Approver", now));
                });
            return fixture;
        }

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public RequestExpectations<TOut> ReplayScenario<TOut>(Uuid userId, IRequest<TOut> request,
            Uuid requestId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId))
            .GivenMetadata(new RequestMetadata(requestId, requestId, null))
            .When(request).ExpectAuthorized().ExpectHandled();

        public AddRiskTreatmentAction AddAction(Worker worker, IReadOnlyList<Uuid> evidence,
            long revision) => new(TenantId, ProgramId, RiskId, revision, "Enforce MFA",
            "MFA is required for every administrator.", "Identity provider policy export.",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), worker.MemberId, evidence);

        public SubmitRiskTreatmentActionCompletion Submit(Uuid actionId, long revision,
            IReadOnlyList<Uuid> evidence) => new(TenantId, ProgramId, RiskId, actionId, revision,
            "MFA enforced on all administrators.", evidence);

        public ReviewRiskTreatmentActionCompletion ReviewAction(Uuid actionId, long revision,
            string outcome) => new(TenantId, ProgramId, RiskId, actionId, revision, outcome,
            "Evidence matches the target state.");

        public async Task<Worker> SeedMemberAsync()
        {
            var userId = Uuid.CreateVersion4();
            await ProgramManagementServices.SeedAsync(Provider, new Member(TenantId, userId),
                member => member.Register());
            return new Worker(userId, RbacIds.Member(TenantId, userId));
        }

        public async Task<Uuid> SeedEvidenceAsync(bool fulfilled, Worker owner)
        {
            var requestId = Uuid.CreateVersion4();
            var admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Admin");
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(Provider,
                new EvidenceRequestLedger(TenantId, ProgramId), ledger => Command(
                    ledger.OpenRequest(requestId, "MFA export", "Export the policy.",
                        owner.MemberId, DateOnly.FromDateTime(now.UtcDateTime).AddDays(7), null,
                        admin, now)));
            if (fulfilled)
                await FulfilEvidenceAsync(requestId);
            return requestId;
        }

        public Task FulfilEvidenceAsync(Uuid requestId) => ProgramManagementServices.SeedAsync(
            Provider, new EvidenceRequestLedger(TenantId, ProgramId), ledger => Command(
                ledger.Fulfil(requestId, 1, Uuid.CreateVersion4(),
                    ActorReference.ForMember(Uuid.CreateVersion4(), "Owner"),
                    DateTimeOffset.UtcNow)));

        public RecordRiskAssessment Residual(long revision) => new(TenantId, ProgramId, RiskId,
            revision, 1, "residual", 2, 2, "After treatment.");

        public ReviewRiskControlTreatment ReviewTreatment(Uuid treatmentId, string outcome) =>
            new(TenantId, ProgramId, RiskId, treatmentId, 1, outcome, "Independently reviewed.");

        public AcceptRisk Accept(Uuid residualId, Uuid? waiverId) => new(TenantId, ProgramId,
            RiskId, 3, residualId, "executive", DateTimeOffset.UtcNow.AddMonths(6),
            "Accepted within tolerance.", waiverId);

        public async Task<RiskGovernanceView> GovernanceAsync()
        {
            var result = await Scenario(AssessorUserId)
                .When(new GetRiskGovernance(TenantId, ProgramId, RiskId)).ExpectSuccess();
            return result.Value;
        }

        public async Task<Uuid> SeedPersonAsync(Uuid correlatedUserId)
        {
            var personId = Uuid.CreateVersion4();
            var admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Admin");
            await ProgramManagementServices.SeedAsync(Provider, new Person(TenantId, personId),
                person => person.Record("Provider owner", null, admin, DateTimeOffset.UtcNow));
            await ProgramManagementServices.SeedAsync(Provider, new Person(TenantId, personId),
                person => Command(person.CorrelateMembership(1, correlatedUserId, admin,
                    DateTimeOffset.UtcNow)));
            return personId;
        }

        public async Task<Uuid> SeedWaiverAsync(Uuid residualId, long revision)
        {
            var waiverId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(Provider,
                new SeparationOfDutiesWaiver(TenantId, waiverId), waiver =>
                {
                    Assert.Null(waiver.Record(new SeparationOfDutiesWaiverScope("risk", RiskId,
                            residualId, revision, "approve"),
                        RbacIds.Member(TenantId, ApproverUserId), Uuid.CreateVersion4(),
                        "Admin", "Sole executive owns the provider relationship.",
                        now.AddMinutes(-5), now.AddDays(1)));
                    return Command(waiver.Approve(Uuid.CreateVersion4(), "Other admin",
                        now.AddMinutes(-1)));
                });
            return waiverId;
        }
    }

    static Result Command(CommandFailure? failure) => failure is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

    sealed class SingleRiskDirectory : IRiskDraftDirectoryReader
    {
        public Fixture? Fixture { get; set; }

        public async ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default)
        {
            var cursor = EventCursor.Start;
            await foreach (var record in Fixture!.Provider.GetRequiredService<IDomainEventReader>()
                               .ReadAsync(EventStreamPattern.ForPattern(tenantId.ToString(),
                                   "risks"), EventCursor.Start, ct))
                cursor = record.NextCursor;
            return new ProjectionCheckpoint(cursor);
        }

        public ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId,
            CancellationToken ct = default) => ValueTask.FromResult<RiskDraftView?>(null);

        public ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<RiskDraftView>(
            [
                new RiskDraftView(tenantId, programId, Fixture!.RiskId, "R-01", 1,
                    "draft_unassessed", "unresolved",
                    new RiskDraftContent("Provider outage", "Unavailable", "Fails", null),
                    Uuid.CreateVersion4(), "Assessor", DateTimeOffset.UtcNow),
            ], null));

        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
            long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<RiskDraftRevisionView?>(null);
    }
}

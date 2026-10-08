using Bdgrz.Compliance.Tests.Features.Policies;
using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class ReviewerWorkAuthorityTests
{
    [Theory]
    [InlineData("commitment", false)]
    [InlineData("commitment", true)]
    [InlineData("control_assigned", false)]
    [InlineData("control_assigned", true)]
    [InlineData("risk_control_treatment", false)]
    [InlineData("risk_control_treatment", true)]
    [InlineData("policy", false)]
    [InlineData("policy", true)]
    [InlineData("control", false)]
    [InlineData("control", true)]
    [InlineData("finding_closure", false)]
    [InlineData("finding_closure", true)]
    [InlineData("evaluation", false)]
    [InlineData("evaluation", true)]
    [InlineData("operating_plan", false)]
    [InlineData("operating_plan", true)]
    [InlineData("mapping", false)]
    [InlineData("mapping", true)]
    [InlineData("applicability", false)]
    [InlineData("applicability", true)]
    public async Task ShouldPermitIndependentAssignmentGivenGuestManagerAcceptedByActualSourceCommand(
        string kind, bool projected)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        var source = await fixture.SeedAsync(kind);

        // Act
        await fixture.ReviewAsync(kind, source);
        var next = await fixture.SeedAsync(kind, source.Revision + 1);
        var queue = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, kind == "control_assigned" ? "all" : "unassigned")).ExpectSuccess();
        var item = Assert.Single(queue.Value.Items, candidate => candidate.SourceId == next.Id);
        var assignedItem = item;
        if (kind != "control_assigned")
        {
            var assigned = await fixture.Scenario().When(new AssignWorkItem(fixture.Operations.TenantId,
                fixture.Operations.ProgramId, item.WorkItemId, 0, fixture.GuestMemberId)).ExpectSuccess();
            assignedItem = assigned.Value.Item;
        }

        var detail = await fixture.Scenario().When(new GetWorkItem(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, assignedItem.WorkItemId)).ExpectSuccess();
        await fixture.ReviewAsync(kind, next);
        var after = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "mine")).ExpectSuccess();

        // Assert
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
        Assert.Equal(fixture.GuestMemberId, assignedItem.AssigneeMemberId);
        Assert.Equal(assignedItem, detail.Value.Item);
        Assert.Equal(kind == "control_assigned" ? 2 : 1, queue.Value.Counts.Total);
    }

    [Theory]
    [InlineData(false, "accept")]
    [InlineData(true, "accept")]
    [InlineData(false, "reject")]
    [InlineData(true, "reject")]
    public async Task ShouldPermitRiskCompletionReviewGivenAuthorizedGuestAndActualQueueAssignment(bool projected,
        string outcome)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        var pending = await fixture.SeedAsync("risk_completion");
        var operations = fixture.Operations;
        var command = new ReviewRiskTreatmentActionCompletion(operations.TenantId, operations.ProgramId,
            pending.ResourceId!.Value, pending.ActionId!.Value, pending.Revision, outcome, "Reviewed independently.");
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var authorizer = new ProgramManagementAuthorizer(services.GetRequiredService<ITenantMembershipDirectoryReader>(),
            services.GetRequiredService<ITenantActivity>(), services.GetRequiredService<IAccessGrantPermissionAuthorizer>(),
            ProgramManagementServices.ResourceScopes(operations.ProgramId));
        var authorized = await authorizer.AuthorizeAsync(new RequestContext<IProgramManagementRequest>(command,
            ProgramManagementServices.Actor(fixture.GuestUserId)), CancellationToken.None);
        Assert.True(authorized.IsSuccess);
        var queue = await fixture.Scenario(operations.ApproverUserId).When(new ListWork(operations.TenantId,
            operations.ProgramId, "all")).ExpectSuccess();
        var item = Assert.Single(queue.Value.Items, candidate => candidate.SourceId == pending.Id);

        // Act
        await fixture.Scenario(operations.ApproverUserId).When(new AssignWorkItem(operations.TenantId,
            operations.ProgramId, item.WorkItemId, 0, fixture.GuestMemberId)).ExpectSuccess();
        await fixture.Scenario().When(command).ExpectSuccess();
        await fixture.CatchUpAsync();
        var after = await fixture.Scenario().When(new ListWork(operations.TenantId, operations.ProgramId,
            "mine")).ExpectSuccess();

        // Assert
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
    }

    [Theory]
    [InlineData(false, "owner")]
    [InlineData(true, "owner")]
    [InlineData(false, "submitter")]
    [InlineData(true, "submitter")]
    public async Task ShouldDenyRiskReviewAssignmentGivenGuestManagerOwnsOrSubmittedAction(bool projected, string role)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected);
        fixture.RiskConflictRole = role;
        var pending = await fixture.SeedAsync("risk_completion");
        var operations = fixture.Operations;
        var oversight = await fixture.Scenario(operations.ApproverUserId).When(new ListWork(operations.TenantId,
            operations.ProgramId, "all")).ExpectSuccess();
        var item = Assert.Single(oversight.Value.Items, candidate => candidate.SourceId == pending.Id);

        // Act
        await fixture.Scenario(operations.ApproverUserId).When(new AssignWorkItem(operations.TenantId,
            operations.ProgramId, item.WorkItemId, 0, fixture.GuestMemberId))
            .ExpectFailure(RequestErrorKind.Validation);
        await fixture.ReviewAsync("risk_completion", pending, RequestErrorKind.Forbidden);
        var mine = await fixture.Scenario().When(new ListWork(operations.TenantId, operations.ProgramId,
            "mine")).ExpectSuccess();

        // Assert
        Assert.Null(item.AssigneeMemberId);
        Assert.Empty(mine.Value.Items);
        Assert.Equal(0, mine.Value.Counts.Total);
    }

    [Theory]
    [InlineData("risk_completion")]
    [InlineData("commitment")]
    [InlineData("policy")]
    [InlineData("control")]
    [InlineData("risk_control_treatment")]
    [InlineData("finding_closure")]
    [InlineData("evaluation")]
    [InlineData("operating_plan")]
    [InlineData("mapping")]
    [InlineData("applicability")]
    public async Task ShouldInvalidateDecisionAssignmentGivenGuestLosesSourceManagement(string kind)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected: true);
        var pending = await fixture.SeedAsync(kind);
        var before = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        await fixture.Scenario().When(new AssignWorkItem(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, item.WorkItemId, 0, fixture.GuestMemberId)).ExpectSuccess();
        fixture.Operations.Permissions.Managers.Remove(fixture.GuestMemberId);

        // Act
        await fixture.ReviewAsync(kind, pending, RequestErrorKind.Forbidden);
        var guest = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "unassigned")).ExpectSuccess();
        await fixture.Scenario().When(new GetWorkItem(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, item.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);
        var manager = await fixture.Scenario(fixture.Operations.ApproverUserId).When(
            new ListWork(fixture.Operations.TenantId, fixture.Operations.ProgramId,
                "unassigned")).ExpectSuccess();
        await fixture.Scenario(fixture.Operations.ApproverUserId).When(new AssignWorkItem(
            fixture.Operations.TenantId, fixture.Operations.ProgramId, item.WorkItemId, 1,
            fixture.GuestMemberId)).ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Empty(guest.Value.Items);
        Assert.Equal(0, guest.Value.Counts.Total);
        Assert.Null(Assert.Single(manager.Value.Items).AssigneeMemberId);
        Assert.Equal(1, manager.Value.Counts.Total);
    }

    [Fact]
    public async Task ShouldKeepClientReviewerRequirementGivenExplicitGuestManagementGrant()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var authority = scope.ServiceProvider.GetRequiredService<OperatingAuthority>();

        // Act
        var manager = await authority.HoldsAsync(fixture.Operations.TenantId,
            new OperatingHolder(OperatingAuthority.ProgramManagerHolder, fixture.Operations.ProgramId),
            fixture.GuestMemberId, CancellationToken.None);
        var reviewer = await authority.HoldsAsync(fixture.Operations.TenantId,
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, fixture.Operations.ProgramId),
            fixture.GuestMemberId, CancellationToken.None);

        // Assert
        Assert.True(manager);
        Assert.False(reviewer);
    }

    [Theory]
    [InlineData("finding_owner")]
    [InlineData("action_owner")]
    [InlineData("completer")]
    public async Task ShouldDenyClosureAndHideWorkGivenGuestManagerInvolvedInRemediation(string role)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(projected: true);
        var operations = fixture.Operations;
        var finding = await WorkTestData.AddActionsAsync(operations,
            role == "action_owner" ? fixture.GuestMemberId : operations.OwnerMemberId,
            operations.Today);
        finding = await operations.AsAsync(role == "completer" ? fixture.GuestUserId :
                role == "action_owner" ? operations.ApproverUserId : operations.OwnerUserId,
            new CompleteCorrectiveAction(operations.TenantId, operations.ProgramId,
                finding.FindingId, finding.Revision, Assert.Single(finding.CorrectiveActions).ActionId,
                "Corrected access.", OperationsFixture.FullSupport));
        if (role == "finding_owner")
            finding = await operations.AsAsync(operations.LeadUserId, new ReviseFinding(
                operations.TenantId, operations.ProgramId, finding.FindingId, finding.Revision,
                "high", fixture.GuestMemberId, finding.DueOn, "VPN", null, "Owner changed."));
        await fixture.CatchUpAsync();
        var candidate = FindingClosureWork.Candidate(finding)!;

        // Act
        await fixture.ReviewAsync("finding_closure", new Pending(finding.FindingId, finding.Revision),
            RequestErrorKind.Forbidden);
        var guest = await fixture.Scenario().When(new ListWork(operations.TenantId,
            operations.ProgramId, "all")).ExpectSuccess();
        await fixture.Scenario().When(new GetWorkItem(operations.TenantId, operations.ProgramId,
            candidate.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);
        await fixture.Scenario(operations.BackupUserId).When(new AssignWorkItem(operations.TenantId,
            operations.ProgramId, candidate.WorkItemId, 0, fixture.GuestMemberId))
            .ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.Empty(guest.Value.Items);
        Assert.Equal(0, guest.Value.Counts.Total);
    }

    sealed record Pending(Uuid Id, long Revision, Uuid? ResourceId = null, Uuid? ActionId = null);

    sealed class Fixture : IAsyncDisposable
    {
        public required OperationsFixture Operations { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required Uuid GuestUserId { get; init; }
        public Uuid GuestMemberId => Operations.Member(GuestUserId);
        public string? RiskConflictRole { get; set; }

        public static async Task<Fixture> CreateAsync(bool projected = false)
        {
            var operations = await OperationsFixture.CreateAsync();
            var guestUserId = Uuid.CreateVersion4();
            var guestMemberId = operations.Member(guestUserId);
            operations.Permissions.Managers.Add(guestMemberId);
            operations.Permissions.Managers.Add(operations.BackupMemberId);
            DomainEvent registered = new MemberRegistered(operations.TenantId, guestMemberId,
                guestUserId, "guest", Uuid.CreateVersion4());
            registered.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), guestMemberId,
                1, DateTimeOffset.UtcNow));
            var events = operations.Provider.GetRequiredService<IEventStore>();
            await events.AppendAsync(new EventStreamAddress(operations.TenantId.ToString(),
                "rbac-members", guestMemberId.ToString()), 0, [registered]);
            var client = new InMemoryKvClient();
            var provider = ProgramManagementServices.Build(operations.Permissions,
                portia => portia.AddRequestHandler<ReviewControlEvaluationHandler>()
                    .AddRequestHandler<CloseFindingHandler>()
                    .AddRequestHandler<ReviewPolicyDraftHandler>()
                    .AddRequestHandler<ReviewControlHandler>()
                    .AddRequestHandler<ReviewRiskControlTreatmentHandler>()
                    .AddRequestHandler<ReviewRiskTreatmentActionCompletionHandler>()
                    .AddRequestHandler<ReviewCommitmentDraftHandler>()
                    .AddRequestHandler<ApproveControlOperatingPlanHandler>()
                    .AddRequestHandler<ReviewControlCriterionMappingHandler>()
                    .AddRequestHandler<ReviewCriterionApplicabilityHandler>()
                    .AddRequestHandler<ListWorkHandler>().AddRequestHandler<GetWorkItemHandler>()
                    .AddRequestHandler<AssignWorkItemHandler>(),
                services =>
                {
                    services.AddSingleton(events);
                    services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
                    services.AddScoped<ITenantMembershipDirectoryReader, MembershipDirectory>();
                    services.AddScoped<OperatingAuthority>();
                    services.AddSingleton(new ControlActivationReleaseGate(true));
                    services.AddSingleton(new ControlLifecycleReleaseGate(true));
                    services.AddSingleton<IPolicyDirectoryReader>(operations.Policies);
                    services.AddSingleton<ICommitmentDraftDirectoryReader>(operations.Commitments);
                    services.AddSingleton(operations.Provider.GetRequiredService<IControlDraftDirectoryReader>());
                    services.AddScoped<WorkQueueReader>();
                    services.AddScoped<WorkQueueReadConsistency>();
                    if (projected)
                    {
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider => new FitzRiskGovernanceWorkItemDirectory(client));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzCommitmentDecisionWorkItemDirectory(client, provider.GetRequiredService<IAggregateReader>()));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzPolicyDecisionWorkItemDirectory(client, provider.GetRequiredService<IAggregateReader>(), TimeProvider.System));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzControlDecisionWorkItemDirectory(client, provider.GetRequiredService<IAggregateReader>(),
                                provider.GetRequiredService<OperatingAuthority>(), new(true), new(true)));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzFindingClosureWorkItemDirectory(client));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzControlEvaluationWorkItemDirectory(client));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzControlOperatingPlanWorkItemDirectory(client,
                                provider.GetRequiredService<IAggregateReader>()));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzControlMappingWorkItemDirectory(client));
                        services.AddScoped<IAccountableWorkItemDirectoryReader>(provider =>
                            new FitzCriterionApplicabilityWorkItemDirectory(client));
                    }
                });
            return new Fixture { Operations = operations, Provider = provider, GuestUserId = guestUserId };
        }

        public RequestScenario Scenario(Uuid? userId = null) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId ?? GuestUserId));

        public async Task<Pending> SeedAsync(string kind, long revision = 0)
        {
            var pending = await SeedCoreAsync(kind, revision);
            await CatchUpAsync();
            return pending;
        }

        async Task<Pending> SeedCoreAsync(string kind, long revision)
        {
            if (kind == "risk_completion")
            {
                var riskId = Uuid.CreateVersion4();
                var actionId = Uuid.CreateVersion4();
                var submissionId = Uuid.CreateVersion4();
                var evidenceId = Uuid.CreateVersion4();
                var lead = ActorReference.ForMember(Operations.LeadMemberId, "Lead");
                var completionAt = DateTimeOffset.UtcNow;
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new RiskDraft(Operations.TenantId, riskId), risk => risk.Create(Operations.ProgramId,
                        Uuid.CreateVersion4(), "R-GUEST", new RiskDraftContent("Provider outage",
                            "Provider unavailable", "Requests fail", null), Operations.LeadMemberId,
                        "Lead", completionAt));
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new EvidenceRequestLedger(Operations.TenantId, Operations.ProgramId), ledger =>
                    {
                        Assert.Null(ledger.OpenRequest(evidenceId, "MFA export", "Upload policy", Operations.OwnerMemberId,
                            Operations.Today.AddDays(5), null, lead, completionAt));
                        Assert.Null(ledger.Fulfil(evidenceId, 1, Uuid.CreateVersion4(), lead, completionAt));
                        return Result.Success;
                    });
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new RiskGovernanceLedger(Operations.TenantId, Operations.ProgramId), ledger =>
                    {
                        Assert.Null(ledger.AddTreatmentAction(riskId, 0, actionId, "mitigate", "Enforce MFA",
                            "MFA enforced", "Policy export", Operations.Today.AddDays(9), RiskConflictRole == "owner" ? GuestMemberId : Operations.OwnerMemberId,
                            [evidenceId], lead, completionAt));
                        Assert.Null(ledger.SubmitActionCompletion(riskId, actionId, 1, submissionId, "MFA enforced",
                            [evidenceId], new HashSet<Uuid> { evidenceId }, RiskConflictRole == "submitter" ? GuestMemberId : Operations.LeadMemberId,
                            RiskConflictRole == "submitter" ? ActorReference.ForMember(GuestMemberId, "Guest") : lead, completionAt));
                        return Result.Success;
                    });
                return new Pending(submissionId, 2, riskId, actionId);
            }
            if (kind == "commitment")
            {
                var draftId = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new CommitmentDraft(Operations.TenantId, draftId), draft => draft.Create(Operations.ProgramId,
                        Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", "SC-GUEST",
                        "Protect customer data", "Security commitment", "MSA 4.1", Operations.LeadMemberId,
                        "Lead", DateTimeOffset.UtcNow));
                await RefreshCommitmentAsync(draftId);
                return new Pending(draftId, 1);
            }
            if (kind == "risk_control_treatment")
            {
                var riskId = Uuid.CreateVersion4();
                var treatmentId = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new RiskDraft(Operations.TenantId, riskId), risk => risk.Create(Operations.ProgramId,
                        Uuid.CreateVersion4(), "R-GUEST", new RiskDraftContent("Provider outage",
                            "Provider unavailable", "Requests fail", null), Operations.LeadMemberId,
                        "Lead", DateTimeOffset.UtcNow));
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new RiskGovernanceLedger(Operations.TenantId, Operations.ProgramId), ledger =>
                    {
                        Assert.Null(ledger.ProposeControlTreatment(riskId, 0, treatmentId, "mitigate",
                            Operations.ControlId, Operations.ControlVersionId, "Control treats the risk.",
                            Operations.LeadMemberId, ActorReference.ForMember(Operations.LeadMemberId, "Lead"),
                            DateTimeOffset.UtcNow));
                        return Result.Success;
                    });
                return new Pending(treatmentId, 1, riskId);
            }
            if (kind == "policy")
            {
                var policyId = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new Policy(Operations.TenantId, policyId), policy =>
                    {
                        Assert.True(policy.Create(Operations.ProgramId, Uuid.CreateVersion4(), "POL-GUEST",
                            new PolicyContent("Guest review", "Access", PolicyAudience.CoreSecurity, null, 12,
                                "Body", null, "Security owner", []),
                            ActorReference.ForMember(Operations.LeadMemberId, "Lead"), Operations.LeadMemberId,
                            DateTimeOffset.UtcNow).IsSuccess);
                        return Result.Success;
                    });
                var policy = await ProgramManagementServices.HydrateAsync(Operations.Provider,
                    new Policy(Operations.TenantId, policyId));
                var view = policy.ToView(Operations.Today)!;
                Operations.Policies.Add(new PolicySummaryView(view.TenantId, view.ProgramId, view.PolicyId,
                    view.Identifier, "Guest review", view.Status, view.PendingStatus, view.Revision,
                    view.CurrentVersion, view.CurrentEffectiveFrom, view.NextReviewDueOn, view.ReviewOverdue,
                    view.LastChangedAt));
                return new Pending(policyId, policy.Revision);
            }
            if (kind is "control" or "control_assigned")
            {
                var controlId = Uuid.CreateVersion4();
                var versionId = Uuid.CreateVersion4();
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new ControlDraft(Operations.TenantId, controlId), control =>
                    {
                        Assert.True(control.Create(Operations.ProgramId, versionId, "AC-GUEST",
                            new ControlDraftContent("Guest review", "Access", "Review access", "Monthly review", ["Signed review"]),
                            Operations.LeadMemberId, "Lead", DateTimeOffset.UtcNow).IsSuccess);
                        if (kind == "control_assigned")
                            Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control", controlId,
                                    control.DraftVersionId, control.Revision), Uuid.CreateVersion4(), GuestMemberId,
                                ResponsibilityType.AssignedReviewer, Operations.LeadMemberId, "Lead", DateTimeOffset.UtcNow,
                                DateTimeOffset.UtcNow.AddMinutes(-1), null, []));
                        return Result.Success;
                    });
                Operations.IncludeControlInDirectory(controlId);
                return new Pending(ControlVersionIds.Initial(controlId), 1, controlId);
            }
            if (kind == "finding_closure")
            {
                var finding = await WorkTestData.AddActionsAsync(Operations, Operations.OwnerMemberId,
                    Operations.Today);
                finding = await Operations.AsAsync(Operations.OwnerUserId, new CompleteCorrectiveAction(
                    Operations.TenantId, Operations.ProgramId, finding.FindingId, finding.Revision,
                    Assert.Single(finding.CorrectiveActions).ActionId, "Corrected access.",
                    OperationsFixture.FullSupport));
                return new Pending(finding.FindingId, finding.Revision);
            }
            if (kind == "operating_plan")
            {
                var plan = await Operations.ProposeAsync(revision,
                    cadence: revision == 0 ? null : new ControlCadence("recurring", "monthly",
                        Operations.ControlEffectiveFrom.AddDays(1), 5),
                    effectiveFrom: revision == 0 ? null : Operations.ControlEffectiveFrom.AddDays(1));
                return new Pending(plan.PlanVersionId, plan.Revision);
            }
            if (kind == "evaluation")
            {
                var plan = await Operations.GetOrDefineEvaluationPlanAsync();
                var evaluation = await Operations.AsAsync(Operations.OwnerUserId,
                    new StartControlEvaluation(Operations.TenantId, Operations.ProgramId,
                        Operations.ControlId, plan.PlanVersionId));
                foreach (var step in evaluation.Steps)
                    evaluation = await Operations.AsAsync(Operations.OwnerUserId,
                        new RecordControlEvaluationStep(Operations.TenantId, Operations.ProgramId,
                            Operations.ControlId, evaluation.EvaluationId, step.StepId,
                            evaluation.Revision, "met", "Inspected exact items.", step.InspectedItems));
                evaluation = await Operations.AsAsync(Operations.OwnerUserId,
                    new SubmitControlEvaluation(Operations.TenantId, Operations.ProgramId,
                        Operations.ControlId, evaluation.EvaluationId, evaluation.Revision,
                        [new("design", "effective", "Design meets objective."),
                            new("implementation", "effective", "Operates as designed."),
                            new("evidence_sufficiency", "effective", "Evidence is sufficient.")]));
                return new Pending(evaluation.EvaluationId, evaluation.Revision);
            }
            var edition = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            if (kind == "mapping")
            {
                await ProgramManagementServices.SeedAsync(Operations.Provider,
                    new ControlCriterionMappingLedger(Operations.TenantId, Operations.ProgramId), ledger =>
                    {
                        Assert.Null(ledger.Propose(Operations.ControlId, Operations.ControlVersionId,
                            edition, "CC6.1", "criterion", 0, "Addresses access review.",
                            "Applies to production.", Operations.LeadMemberId, "Lead", now, out _));
                        return Result.Success;
                    });
                return new Pending(ControlCriterionMappingLedger.MappingIdFor(Operations.ProgramId,
                    Operations.ControlId, edition, "CC6.1"), 1);
            }
            await ProgramManagementServices.SeedAsync(Operations.Provider,
                new CriterionApplicabilityLedger(Operations.TenantId, Operations.ProgramId), ledger =>
                {
                    Assert.Null(ledger.Propose(edition, "CC6.2", 0, "Does not apply to organization.",
                        Operations.LeadMemberId, ActorReference.ForMember(Operations.LeadMemberId, "Lead"),
                        now, out _));
                    return Result.Success;
                });
            return new Pending(CriterionApplicabilityLedger.DecisionIdFor(Operations.ProgramId,
                edition, "CC6.2"), 1);
        }

        public async Task ReviewAsync(string kind, Pending pending, RequestErrorKind? error = null)
        {
            if (kind == "risk_completion")
            {
                var request = Scenario().When(new ReviewRiskTreatmentActionCompletion(Operations.TenantId,
                    Operations.ProgramId, pending.ResourceId!.Value, pending.ActionId!.Value,
                    pending.Revision, "accept", "Reviewed independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
            }
            else if (kind == "commitment")
            {
                var request = Scenario().When(new ReviewCommitmentDraft(Operations.TenantId, Operations.ProgramId,
                    pending.Id, pending.Revision, "accept", "Reviewed independently.", "Security lead", "applicable",
                    "supported", SourceVerifiedReference: "MSA 4.1", SourceEvidence: "Signed MSA section 4.1"));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
                await RefreshCommitmentAsync(pending.Id);
            }
            else if (kind == "risk_control_treatment")
            {
                var request = Scenario().When(new ReviewRiskControlTreatment(Operations.TenantId,
                    Operations.ProgramId, pending.ResourceId!.Value, pending.Id, pending.Revision,
                    "accept", "Reviewed independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
            }
            else if (kind == "policy")
            {
                await PersonalPolicyDecisionTransportTests.SendHttpAsync(Provider, GuestUserId,
                    new ReviewPolicyDraft(Operations.TenantId, Operations.ProgramId,
                        pending.Id, pending.Revision, "accept", "Reviewed independently."), error);
            }
            else if (kind is "control" or "control_assigned")
            {
                var request = Scenario().When(new ReviewControl(Operations.TenantId, Operations.ProgramId,
                    pending.ResourceId!.Value, pending.Revision, "accept", "Reviewed independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
            }
            else if (kind == "finding_closure")
            {
                await PersonalReadinessClosureTransportTests.CloseHttpAsync(Provider, GuestUserId,
                    new CloseFinding(Operations.TenantId, Operations.ProgramId, pending.Id, pending.Revision,
                        "Independently verified correction.", OperationsFixture.FullSupport, "Closure accepted."), error);
            }
            else if (kind == "evaluation")
            {
                var request = Scenario().When(new ReviewControlEvaluation(Operations.TenantId, Operations.ProgramId,
                    Operations.ControlId, pending.Id, pending.Revision, "accepted", "Reviewed independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
            }
            else if (kind == "operating_plan")
            {
                await PersonalOperatingPlanApprovalTransportTests.HttpAsync(Provider, GuestUserId,
                    new ApproveControlOperatingPlan(Operations.TenantId, Operations.ProgramId,
                        Operations.ControlId, pending.Revision, pending.Id, "Approved independently."), error);
            }
            else if (kind == "mapping")
            {
                var request = Scenario().When(new ReviewControlCriterionMapping(Operations.TenantId,
                    Operations.ProgramId, pending.Id, pending.Revision, "reject", "Reviewed independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
            }
            else
            {
                var request = Scenario().When(new ReviewCriterionApplicability(Operations.TenantId,
                    Operations.ProgramId, pending.Id, pending.Revision, "reject", "Reviewed independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
            }
            if (kind == "policy")
            {
                var policy = await ProgramManagementServices.HydrateAsync(Operations.Provider,
                    new Policy(Operations.TenantId, pending.Id));
                var view = policy.ToView(Operations.Today)!;
                Operations.Policies.Add(new PolicySummaryView(view.TenantId, view.ProgramId, view.PolicyId,
                    view.Identifier, "Guest review", view.Status, view.PendingStatus, view.Revision,
                    view.CurrentVersion, view.CurrentEffectiveFrom, view.NextReviewDueOn, view.ReviewOverdue,
                    view.LastChangedAt));
            }
            await CatchUpAsync();
        }

        async Task RefreshCommitmentAsync(Uuid draftId)
        {
            var draft = await ProgramManagementServices.HydrateAsync(Operations.Provider,
                new CommitmentDraft(Operations.TenantId, draftId));
            Operations.Commitments.Add(new CommitmentDraftView(Operations.TenantId, draft.ProgramId,
                draft.Id, draft.ServiceId, draft.Kind!, draft.Identifier!, draft.Revision,
                draft.AcceptedReviewDecisionId is null ? "draft" : "reviewed", "verified", "verified",
                "applicable", "Statement", "Context", "MSA 4.1", Operations.LeadMemberId, "Lead", DateTimeOffset.UtcNow));
        }

        public async Task CatchUpAsync()
        {
            await using var scope = Provider.CreateAsyncScope();
            var events = scope.ServiceProvider.GetRequiredService<IDomainEventReader>();
            foreach (var directory in scope.ServiceProvider.GetServices<IAccountableWorkItemDirectoryReader>())
            {
                var checkpoint = await directory.LoadCheckpointAsync(Operations.TenantId);
                var pattern = directory.SourcePattern(Operations.TenantId);
                await using var batch = await ((IProjectionStore)directory).BeginAsync(
                    new ProjectionBatchContext(new CheckpointIdentity(directory.ProjectorName, pattern), checkpoint));
                var cursor = checkpoint.Cursor;
                await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
                {
                    await (directory switch
                    {
                        FitzFindingClosureWorkItemDirectory finding => finding.ApplyAsync(record.Event),
                        FitzPolicyDecisionWorkItemDirectory policy => policy.ApplyAsync(record.Event),
                        FitzRiskGovernanceWorkItemDirectory risk => risk.ApplyAsync(record.Event),
                        FitzCommitmentDecisionWorkItemDirectory commitment => commitment.ApplyAsync(record.Event),
                        FitzControlDecisionWorkItemDirectory control => control.ApplyAsync(record.Event),
                        FitzControlEvaluationWorkItemDirectory evaluation => evaluation.ApplyAsync(record.Event),
                        FitzControlOperatingPlanWorkItemDirectory plan => plan.ApplyAsync(record.Event),
                        FitzControlMappingWorkItemDirectory mapping => mapping.ApplyAsync(record.Event),
                        FitzCriterionApplicabilityWorkItemDirectory applicability => applicability.ApplyAsync(record.Event),
                        _ => throw new InvalidOperationException("Unexpected source directory."),
                    });
                    cursor = record.NextCursor;
                }
                await batch.CommitAsync(new ProjectionCheckpoint(cursor));
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await Operations.Provider.DisposeAsync();
        }
    }

    sealed class MembershipDirectory(IAggregateReader reader) : ITenantMembershipDirectoryReader
    {
        public async ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId,
            CancellationToken ct = default)
        {
            var id = Uuid.Parse(tenantId, System.Globalization.CultureInfo.InvariantCulture);
            var member = await reader.HydrateAsync(new Member(id, userId), ct);
            return member.IsRegistered || member.IsDeprovisioned
                ? new TenantMembershipView(userId, id, member.Affiliation!, member.IsSuspended,
                    IsDeprovisioned: member.IsDeprovisioned)
                : null;
        }

        public async ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId,
            CancellationToken ct = default) => await GetAsync(tenantId, userId, ct) is
            { IsSuspended: false, IsDeprovisioned: false };

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

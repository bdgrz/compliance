using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Remediation;
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
            fixture.Operations.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(queue.Value.Items, candidate => candidate.SourceId == next.Id);
        var assigned = await fixture.Scenario().When(new AssignWorkItem(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, item.WorkItemId, 0, fixture.GuestMemberId)).ExpectSuccess();

        await fixture.ReviewAsync(kind, next);
        var after = await fixture.Scenario().When(new ListWork(fixture.Operations.TenantId,
            fixture.Operations.ProgramId, "mine")).ExpectSuccess();

        // Assert
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
        Assert.Equal(fixture.GuestMemberId, assigned.Value.Item.AssigneeMemberId);
        Assert.Equal(1, queue.Value.Counts.Total);
    }

    [Theory]
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

    sealed record Pending(Uuid Id, long Revision);

    sealed class Fixture : IAsyncDisposable
    {
        public required OperationsFixture Operations { get; init; }
        public required ServiceProvider Provider { get; init; }
        public required Uuid GuestUserId { get; init; }
        public Uuid GuestMemberId => Operations.Member(GuestUserId);

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
                    services.AddScoped<WorkQueueReader>();
                    services.AddScoped<WorkQueueReadConsistency>();
                    if (projected)
                    {
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
            if (kind == "finding_closure")
            {
                var request = Scenario().When(new CloseFinding(Operations.TenantId, Operations.ProgramId,
                    pending.Id, pending.Revision, "Independently verified correction.",
                    OperationsFixture.FullSupport, "Closure accepted."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
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
                var request = Scenario().When(new ApproveControlOperatingPlan(Operations.TenantId,
                    Operations.ProgramId, Operations.ControlId, pending.Revision, pending.Id,
                    "Approved independently."));
                if (error is { } expected)
                    await request.ExpectFailure(expected);
                else
                    await request.ExpectSuccess();
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
            await CatchUpAsync();
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

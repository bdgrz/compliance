using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Operations;

/// <summary>
///     One program with an approved control, two program managers (lead and approver), and
///     contributor members who hold only read access until a plan assigns them operating work.
/// </summary>
sealed class OperationsFixture
{
    public required ServiceProvider Provider { get; init; }
    public required ManagerPermissions Permissions { get; init; }
    public required ProgramBoundaries Boundaries { get; init; }
    public required ProgramPolicies Policies { get; init; }
    public required ProgramCommitments Commitments { get; init; }
    public required ProgramRisks Risks { get; init; }
    public Uuid TenantId { get; } = Uuid.CreateVersion4();
    public Uuid ProgramId { get; } = Uuid.CreateVersion4();
    public Uuid LeadUserId { get; } = Uuid.CreateVersion4();
    public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
    public Uuid OwnerUserId { get; } = Uuid.CreateVersion4();
    public Uuid BackupUserId { get; } = Uuid.CreateVersion4();
    public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
    public Uuid OutsiderUserId { get; } = Uuid.CreateVersion4();
    public Uuid PersonId { get; } = Uuid.CreateVersion4();
    public Uuid TeamId { get; } = Uuid.CreateVersion4();
    public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly ControlEffectiveFrom => Today.AddDays(-60);
    public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "AC-OPS");
    public Uuid ControlVersionId => ControlVersionIds.Initial(ControlId);
    public Uuid OwnerMemberId => Member(OwnerUserId);
    public Uuid BackupMemberId => Member(BackupUserId);
    public Uuid ReviewerMemberId => Member(ReviewerUserId);
    public Uuid LeadMemberId => Member(LeadUserId);
    public Uuid ApproverMemberId => Member(ApproverUserId);
    public OperatingHolder OwnerHolder => new("member", OwnerMemberId);
    public ControlCadence Monthly => new("recurring", "monthly", ControlEffectiveFrom, 5);

    public Uuid Member(Uuid userId) => RbacIds.Member(TenantId, userId);

    public static async Task<OperationsFixture> CreateAsync()
    {
        var permissions = new ManagerPermissions();
        var boundaries = new ProgramBoundaries();
        var provider = ProgramManagementServices.Build(
            permissions,
            portia => portia.AddRequestHandler<ProposeControlOperatingPlanHandler>()
                .AddRequestHandler<PreviewControlOperatingPlanHandler>()
                .AddRequestHandler<ApproveControlOperatingPlanHandler>()
                .AddRequestHandler<GetControlOperatingPlanHandler>()
                .AddRequestHandler<ListControlOperatingBlockersHandler>()
                .AddRequestHandler<ListMyControlWorkHandler>()
                .AddRequestHandler<OpenControlOccurrenceHandler>()
                .AddRequestHandler<AttestControlOccurrenceHandler>()
                .AddRequestHandler<CorrectControlAttestationHandler>()
                .AddRequestHandler<ReviewControlOccurrenceHandler>()
                .AddRequestHandler<GetControlOccurrenceHandler>()
                .AddRequestHandler<ListControlOccurrencesHandler>()
                .AddRequestHandler<RaiseFindingHandler>()
                .AddRequestHandler<RaiseOccurrenceFindingHandler>()
                .AddRequestHandler<ReviseFindingHandler>()
                .AddRequestHandler<AddCorrectiveActionHandler>()
                .AddRequestHandler<CompleteCorrectiveActionHandler>()
                .AddRequestHandler<LinkFindingAcceptanceHandler>()
                .AddRequestHandler<CloseFindingHandler>()
                .AddRequestHandler<ReopenFindingHandler>()
                .AddRequestHandler<GetFindingHandler>()
                .AddRequestHandler<ListFindingsHandler>()
                .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.OpenEvidenceRequestHandler>()
                .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.FulfilEvidenceRequestHandler>()
                .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.CancelEvidenceRequestHandler>()
                .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.GetEvidenceRequestHandler>()
                .AddRequestHandler<Bdgrz.Compliance.Features.Evidence.ListEvidenceRequestsHandler>()
                .AddRequestHandler<ListWorkHandler>()
                .AddRequestHandler<GetWorkItemHandler>()
                .AddRequestHandler<ClaimWorkItemHandler>()
                .AddRequestHandler<AssignWorkItemHandler>()
                .AddRequestHandler<DelegateWorkItemHandler>()
                .AddRequestHandler<EscalateWorkItemHandler>()
                .AddRequestHandler<ListWorkRemindersHandler>()
                .AddRequestHandler<GetWorkDigestHandler>()
                .AddRequestHandler<SetWorkDigestPreferenceHandler>()
                .AddRequestHandler<GetWorkDigestPreferenceHandler>()
                .AddRequestAuthorizer<TenantAccessAuthorizer>()
                .AddRequestHandler<DefineControlEvaluationPlanHandler>()
                .AddRequestHandler<GetControlEvaluationPlanVersionHandler>()
                .AddRequestHandler<ListControlEvaluationPlanVersionsHandler>()
                .AddRequestHandler<StartControlEvaluationHandler>()
                .AddRequestHandler<RecordControlEvaluationStepHandler>()
                .AddRequestHandler<DisposeControlEvaluationDeviationHandler>()
                .AddRequestHandler<SubmitControlEvaluationHandler>()
                .AddRequestHandler<ReviewControlEvaluationHandler>()
                .AddRequestHandler<GetControlEvaluationHandler>()
                .AddRequestHandler<ListControlEvaluationsHandler>()
                .AddRequestHandler<RaiseEvaluationDeviationFindingHandler>()
                .AddRequestAuthorizer<OperationsReactionAuthorizer>(),
            services =>
            {
                services.AddScoped<OperatingAuthority>();
                services.AddScoped<WorkQueueReader>();
                services.AddSingleton(new ControlActivationReleaseGate(true));
                services.AddSingleton(new ControlLifecycleReleaseGate(true));
                services.AddSingleton<IControlDraftDirectoryReader, ProgramControls>();
                services.AddSingleton<IBoundaryDirectoryReader>(boundaries);
                services.AddSingleton<ProgramPolicies>();
                services.AddSingleton<IPolicyDirectoryReader>(provider =>
                    provider.GetRequiredService<ProgramPolicies>());
                services.AddSingleton<ProgramCommitments>();
                services.AddSingleton<ICommitmentDraftDirectoryReader>(provider =>
                    provider.GetRequiredService<ProgramCommitments>());
                services.AddSingleton<ProgramRisks>();
                services.AddSingleton<IRiskDraftDirectoryReader>(provider =>
                    provider.GetRequiredService<ProgramRisks>());
            });
        var fixture = new OperationsFixture
        {
            Provider = provider,
            Permissions = permissions,
            Boundaries = boundaries,
            Policies = provider.GetRequiredService<ProgramPolicies>(),
            Commitments = provider.GetRequiredService<ProgramCommitments>(),
            Risks = provider.GetRequiredService<ProgramRisks>(),
        };
        permissions.Managers.Add(fixture.LeadMemberId);
        permissions.Managers.Add(fixture.ApproverMemberId);
        ((ProgramControls)provider.GetRequiredService<IControlDraftDirectoryReader>()).Add(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId);
        foreach (var user in new[] { fixture.LeadUserId, fixture.ApproverUserId, fixture.OwnerUserId,
                     fixture.BackupUserId, fixture.ReviewerUserId, fixture.OutsiderUserId })
            await ProgramManagementServices.SeedAsync(provider,
                new Member(fixture.TenantId, user), member => member.Register());
        await ProgramManagementServices.SeedAsync<Person, PersonRegistration>(provider,
            new Person(fixture.TenantId, fixture.PersonId), person => person.Record("Pat Offline",
                null, ActorReference.ForMember(fixture.LeadMemberId, "Lead"),
                DateTimeOffset.UtcNow));
        await ProgramManagementServices.SeedAsync(provider, new Team(fixture.TenantId,
            fixture.TeamId), team => team.Define("Infrastructure"));
        await fixture.SeedControlAsync();
        return fixture;
    }

    async Task SeedControlAsync()
    {
        var admin = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddDays(-61);
        var reviewId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(Provider, new ControlDraft(TenantId, ControlId),
            control =>
            {
                Assert.True(control.Create(ProgramId, Uuid.CreateVersion4(), "AC-OPS",
                    new ControlDraftContent("Access review", "Review access",
                        "Management reviews user access", "Monthly review.",
                        ["Signed review record", "Ticket for each removal"]),
                    admin, "Admin", now).IsSuccess);
                Assert.Null(control.AssignResponsibility(new ResponsibilityScope("control",
                        ControlId, ControlVersionId, 1), Uuid.CreateVersion4(), OwnerMemberId,
                    ResponsibilityType.ControlOwner, admin, "Admin", now, now.AddMinutes(-1), null,
                    []));
                Assert.Null(control.Review(ProgramId, 1, reviewId, "accept", "Ok",
                    Uuid.CreateVersion4(), "Reviewer", now));
                return Command(control.Approve(ProgramId, 1, Uuid.CreateVersion4(), reviewId,
                    ControlEffectiveFrom, "Ready", new HashSet<Uuid> { OwnerMemberId },
                    Uuid.CreateVersion4(), "Approver", now));
            });
    }

    public Task RetireControlAsync(DateOnly effectiveUntil)
    {
        var reviewId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        return ProgramManagementServices.SeedAsync(Provider, new ControlDraft(TenantId, ControlId),
            control =>
            {
                Assert.Null(control.ProposeRetirement(ProgramId, ControlVersionId, effectiveUntil,
                    "Replaced.", Uuid.CreateVersion4(), "Author", now));
                Assert.Null(control.Review(ProgramId, control.Revision, reviewId, "accept", "Ok",
                    Uuid.CreateVersion4(), "Reviewer", now));
                return Command(control.Retire(ProgramId, control.Revision, Uuid.CreateVersion4(),
                    reviewId, "digest", "Retire it.", Uuid.CreateVersion4(), "Approver", now));
            });
    }

    public Task SuspendAsync(Uuid userId) => ProgramManagementServices.SeedAsync(Provider,
        new Member(TenantId, userId), member => member.Suspend(LeadMemberId, "Lead",
            DateTimeOffset.UtcNow, "Left the company."));

    public void IncludeControlInDirectory(Uuid controlId) =>
        ((ProgramControls)Provider.GetRequiredService<IControlDraftDirectoryReader>())
        .Add(TenantId, ProgramId, controlId);

    public async Task AddToTeamAsync(Uuid userId)
    {
        await using var scope = Provider.CreateAsyncScope();
        var member = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new Member(TenantId, userId));
        await ProgramManagementServices.SeedAsync(Provider,
            new TeamMember(TenantId, TeamId, Member(userId)),
            assignment => assignment.Assign(member.MembershipEpisodeId));
    }

    public Task RemoveFromTeamAsync(Uuid userId) => ProgramManagementServices.SeedAsync(Provider,
        new TeamMember(TenantId, TeamId, Member(userId)), membership => membership.Remove());

    public async Task<Uuid> ApprovedWaiverAsync(SeparationOfDutiesWaiverScope scope,
        Uuid beneficiaryMemberId)
    {
        var waiverId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        await ProgramManagementServices.SeedAsync(Provider, new SeparationOfDutiesWaiver(TenantId,
            waiverId), waiver =>
        {
            Assert.Null(waiver.Record(scope, beneficiaryMemberId, LeadMemberId, "Lead",
                "Small team.", now, now.AddDays(30)));
            return Command(waiver.Approve(ApproverMemberId, "Approver", now));
        });
        return waiverId;
    }

    public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
        .GivenActor(ProgramManagementServices.Actor(userId));

    public async Task<TOut> AsAsync<TOut>(Uuid userId, IRequest<TOut> request)
    {
        var result = await Scenario(userId).When(request).ExpectSuccess();
        return result.Value;
    }

    public async Task<ControlEvaluationPlanVersionView> GetOrDefineEvaluationPlanAsync(
        IReadOnlyList<EvaluationProcedureStep>? steps = null,
        bool testerIndependenceRequired = false)
    {
        await using var scope = Provider.CreateAsyncScope();
        var plan = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlEvaluationPlan(TenantId, ProgramId, ControlId));
        if (plan.CurrentVersion is { } current)
            return current;
        var defaultSteps = steps ??
        [
            new EvaluationProcedureStep("design", "inspection",
                [new EvaluationInspectedItem("policy", "policy/access-review", "v3")],
                "Policy requires a monthly signed review."),
            new EvaluationProcedureStep("implementation", "reperformance",
                [new EvaluationInspectedItem("artifact", "exports/q3.csv", "sha256:ab12")],
                "Every leaver was removed."),
            new EvaluationProcedureStep("evidence_sufficiency", "inspection",
                [new EvaluationInspectedItem("artifact", "exports/q3.csv", "sha256:ab12")],
                "Export is complete and dated."),
        ];
        return await DefineEvaluationPlanAsync(0, "Evaluate control design and implementation.",
            defaultSteps, testerIndependenceRequired);
    }

    public Task<ControlEvaluationPlanVersionView> DefineEvaluationPlanAsync(long expectedVersion,
        string objective, IReadOnlyList<EvaluationProcedureStep> steps,
        bool testerIndependenceRequired = false, Uuid? userId = null) =>
        AsAsync(userId ?? LeadUserId, new DefineControlEvaluationPlan(TenantId, ProgramId,
            ControlId, ControlVersionId, expectedVersion, objective, steps,
            testerIndependenceRequired));

    public Task<ControlOperatingPlanView> ProposeAsync(long expectedRevision,
        OperatingHolder? owner = null, OperatingHolder? backup = null, Uuid? reviewer = null,
        ControlCadence? cadence = null, DateOnly? effectiveFrom = null, Uuid? waiverId = null) =>
        AsAsync(LeadUserId, Propose(expectedRevision, owner, backup, reviewer, cadence,
            effectiveFrom, waiverId));

    public ProposeControlOperatingPlan Propose(long expectedRevision, OperatingHolder? owner = null,
        OperatingHolder? backup = null, Uuid? reviewer = null, ControlCadence? cadence = null,
        DateOnly? effectiveFrom = null, Uuid? waiverId = null) => new(TenantId, ProgramId,
        ControlId, expectedRevision, ControlVersionId, owner ?? OwnerHolder, backup,
        reviewer ?? ReviewerMemberId, cadence ?? Monthly, effectiveFrom ?? ControlEffectiveFrom,
        "Owner runs the review monthly.", waiverId);

    public async Task<ControlOperatingPlanView> PlanAsync(OperatingHolder? owner = null,
        OperatingHolder? backup = null, ControlCadence? cadence = null,
        DateOnly? effectiveFrom = null)
    {
        var revision = (await GetPlansAsync()).Revision;
        var proposed = await ProposeAsync(revision, owner, backup, cadence: cadence,
            effectiveFrom: effectiveFrom);
        return await AsAsync(ApproverUserId, new ApproveControlOperatingPlan(TenantId, ProgramId,
            ControlId, proposed.Revision, proposed.PlanVersionId, "Independent approval."));
    }

    public Task<ControlOperatingPlanSetView> GetPlansAsync() => AsAsync(OutsiderUserId,
        new GetControlOperatingPlan(TenantId, ProgramId, ControlId));

    public async Task<IReadOnlyList<ControlOccurrenceView>> OccurrencesAsync(string? state = null) =>
        (await AsAsync(OutsiderUserId, new ListControlOccurrences(TenantId, ProgramId, ControlId,
            state, 200))).Items;

    public Task<ControlOccurrenceView> GetOccurrenceAsync(Uuid occurrenceId) =>
        AsAsync(OutsiderUserId, new GetControlOccurrence(TenantId, ProgramId, ControlId,
            occurrenceId));

    public async Task<IReadOnlyList<ControlOperatingBlockerView>> BlockersAsync() =>
        (await AsAsync(OutsiderUserId, new ListControlOperatingBlockers(TenantId, ProgramId,
            200))).Items;

    public static IReadOnlyList<EvidenceReference> FullSupport =>
    [
        new(0, "external", "https://tickets.example/REV-1", "Signed review"),
        new(1, "record", "TICKET-42"),
    ];

    public AttestControlOccurrence Attest(ControlOccurrenceView occurrence, string result = "complete",
        IReadOnlyList<EvidenceReference>? evidence = null, string? rationale = null,
        Uuid? personId = null) => new(TenantId, ProgramId, ControlId, occurrence.OccurrenceId,
        occurrence.Revision, result, DateTimeOffset.UtcNow.AddMinutes(-10), null, null,
        "Reviewed all accounts.", rationale, evidence ?? FullSupport, personId);

    public ReviewControlOccurrence Review(ControlOccurrenceView occurrence, string outcome,
        IReadOnlyList<string>? actions = null, Uuid? waiverId = null) => new(TenantId, ProgramId,
        ControlId, occurrence.OccurrenceId, occurrence.Revision,
        occurrence.Attestations[^1].AttestationId, outcome, "Checked independently.", actions,
        waiverId);

    public RaiseFinding Raise(Uuid ownerMemberId, DateOnly? dueOn = null) => new(TenantId,
        ProgramId, new FindingSource("manual", null, null, "Terminated users kept VPN access."),
        "Stale VPN access", "Two leavers retained VPN access.", "high", "VPN", ownerMemberId,
        dueOn ?? Today.AddDays(14), [new FindingLink("control", ControlId.ToString())]);

    public Task<FindingView> GetFindingAsync(Uuid findingId) => AsAsync(OutsiderUserId,
        new GetFinding(TenantId, ProgramId, findingId));

    static Result Command(CommandFailure? failure) => failure is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));

    /// <summary>Grants tenant access to everyone and program management to listed members only.</summary>
    public sealed class ManagerPermissions : IPermissionAuthorizer
    {
        public HashSet<Uuid> Managers { get; } = [];
        public HashSet<(Uuid MemberId, string Permission)> RiskApprovers { get; } = [];

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(
            permission == IProgramReadRequest.ReadPermission || Managers.Contains(memberId) ||
            RiskApprovers.Contains((memberId, permission)));
    }

    /// <summary>A program control directory that lists the seeded controls with no lag.</summary>
    sealed class ProgramControls : IControlDraftDirectoryReader
    {
        readonly List<(Uuid TenantId, Uuid ProgramId, Uuid ControlId)> _controls = [];

        public void Add(Uuid tenantId, Uuid programId, Uuid controlId) =>
            _controls.Add((tenantId, programId, controlId));

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<ControlDraftView?> GetAsync(Uuid tenantId, Uuid controlId,
            CancellationToken ct = default) => throw new NotSupportedException();

        public ValueTask<Page<ControlDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<ControlDraftView>(_controls
                .Where(control => control.TenantId == tenantId && control.ProgramId == programId)
                .Select(control => new ControlDraftView(tenantId, programId, control.ControlId,
                    "AC-OPS", 1, "approved", "verified_member", "resolved",
                    new ControlDraftContent("t", "o", "d", "n", ["e"]), Uuid.Empty, "Admin",
                    DateTimeOffset.UtcNow))
                .ToArray(), null));

        public ValueTask<ControlDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid controlId, long revision, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    public sealed class ProgramBoundaries : IBoundaryDirectoryReader
    {
        readonly List<BoundaryView> _boundaries = [];

        public void Add(BoundaryView boundary)
        {
            var index = _boundaries.FindIndex(existing => existing.BoundaryId == boundary.BoundaryId);
            if (index < 0)
                _boundaries.Add(boundary);
            else
                _boundaries[index] = boundary;
        }

        public ValueTask<BoundaryView?> GetAsync(Uuid tenantId, Uuid boundaryId,
            CancellationToken ct = default) => ValueTask.FromResult(_boundaries.FirstOrDefault(
            boundary => boundary.TenantId == tenantId && boundary.BoundaryId == boundaryId));

        public ValueTask<Page<BoundaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<BoundaryView>(_boundaries
                .Where(boundary => boundary.TenantId == tenantId && boundary.ProgramId == programId)
                .Take(limit).ToArray(), null));

        public ValueTask<BoundaryVersionView?> GetVersionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid versionId, CancellationToken ct = default) => ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<Page<BoundaryVersionView>?> ListVersionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryVersionView>?>(new Page<BoundaryVersionView>([], null));

        public ValueTask<BoundaryVersionView?> GetEffectiveVersionAsync(Uuid tenantId,
            Uuid boundaryId, DateOnly effectiveOn, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryVersionView?>(null);

        public ValueTask<BoundaryDecisionView?> GetDecisionAsync(Uuid tenantId, Uuid boundaryId,
            Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<BoundaryDecisionView?>(null);

        public ValueTask<Page<BoundaryDecisionView>?> ListDecisionsAsync(Uuid tenantId,
            Uuid boundaryId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult<Page<BoundaryDecisionView>?>(new Page<BoundaryDecisionView>([], null));

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    public sealed class ProgramPolicies : IPolicyDirectoryReader
    {
        readonly List<PolicySummaryView> _policies = [];

        public void Add(PolicySummaryView summary)
        {
            var index = _policies.FindIndex(existing => existing.PolicyId == summary.PolicyId);
            if (index < 0)
                _policies.Add(summary);
            else
                _policies[index] = summary;
        }

        public PolicySummaryView? Find(Uuid policyId) =>
            _policies.FirstOrDefault(policy => policy.PolicyId == policyId);

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<Page<PolicySummaryView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var offset = cursor is null ? 0 : int.Parse(cursor,
                System.Globalization.CultureInfo.InvariantCulture);
            var matching = _policies.Where(policy => policy.TenantId == tenantId &&
                    policy.ProgramId == programId)
                .OrderBy(static policy => policy.Identifier, StringComparer.Ordinal)
                .ToArray();
            var page = matching.Skip(offset).Take(limit).ToArray();
            var next = offset + page.Length;
            return ValueTask.FromResult(new Page<PolicySummaryView>(page,
                next < matching.Length
                    ? next.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null));
        }
    }

    public sealed class ProgramCommitments : ICommitmentDraftDirectoryReader
    {
        readonly List<CommitmentDraftView> _drafts = [];

        public ProjectionCheckpoint Checkpoint { get; set; } = ProjectionCheckpoint.Start;
        public ProjectionCheckpoint? CheckpointAfterNextList { get; set; }

        public void Add(CommitmentDraftView draft)
        {
            var index = _drafts.FindIndex(existing => existing.DraftId == draft.DraftId);
            if (index < 0)
                _drafts.Add(draft);
            else
                _drafts[index] = draft;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public ValueTask<CommitmentDraftView?> GetAsync(Uuid tenantId, Uuid draftId,
            CancellationToken ct = default) => ValueTask.FromResult(_drafts.FirstOrDefault(draft =>
            draft.TenantId == tenantId && draft.DraftId == draftId));

        public ValueTask<Page<CommitmentDraftView>> ListProgramAsync(Uuid tenantId,
            Uuid programId, int limit, string? cursor, CancellationToken ct = default)
        {
            var offset = cursor is null ? 0 : int.Parse(cursor,
                System.Globalization.CultureInfo.InvariantCulture);
            var matching = _drafts.Where(draft => draft.TenantId == tenantId &&
                    draft.ProgramId == programId)
                .OrderBy(static draft => draft.Identifier, StringComparer.Ordinal)
                .ToArray();
            var page = matching.Skip(offset).Take(limit).ToArray();
            var next = offset + page.Length;
            if (CheckpointAfterNextList is { } checkpoint)
            {
                Checkpoint = checkpoint;
                CheckpointAfterNextList = null;
            }
            return ValueTask.FromResult(new Page<CommitmentDraftView>(page,
                next < matching.Length
                    ? next.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : null));
        }

        public ValueTask<CommitmentDraftRevisionView?> GetRevisionAsync(Uuid tenantId,
            Uuid draftId, long revision, CancellationToken ct = default) =>
            ValueTask.FromResult<CommitmentDraftRevisionView?>(null);

        public ValueTask<CommitmentVersionView?> GetVersionAsync(Uuid tenantId, Uuid draftId,
            long version, CancellationToken ct = default) =>
            ValueTask.FromResult<CommitmentVersionView?>(null);

        public ValueTask<Page<CommitmentVersionView>> ListVersionsAsync(Uuid tenantId,
            Uuid draftId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<CommitmentVersionView>([], null));

        public ValueTask<Page<CommitmentDecisionView>> ListDecisionsAsync(Uuid tenantId,
            Uuid draftId, int limit, string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<CommitmentDecisionView>([], null));

        public ValueTask<CommitmentDecisionView?> GetDecisionAsync(Uuid tenantId,
            Uuid decisionId, CancellationToken ct = default) =>
            ValueTask.FromResult<CommitmentDecisionView?>(null);
    }

    public sealed class ProgramRisks : IRiskDraftDirectoryReader
    {
        readonly List<RiskDraftView> _risks = [];

        public void Add(RiskDraftView risk)
        {
            var index = _risks.FindIndex(existing => existing.RiskId == risk.RiskId);
            if (index < 0)
                _risks.Add(risk);
            else
                _risks[index] = risk;
        }

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(ProjectionCheckpoint.Start);

        public ValueTask<RiskDraftView?> GetAsync(Uuid tenantId, Uuid riskId,
            CancellationToken ct = default) => ValueTask.FromResult(_risks.FirstOrDefault(risk =>
            risk.TenantId == tenantId && risk.RiskId == riskId));

        public ValueTask<Page<RiskDraftView>> ListProgramAsync(Uuid tenantId, Uuid programId,
            int limit, string? cursor, CancellationToken ct = default)
        {
            var matching = _risks.Where(risk => risk.TenantId == tenantId && risk.ProgramId == programId)
                .OrderBy(static risk => risk.Identifier, StringComparer.Ordinal).ToArray();
            return ValueTask.FromResult(new Page<RiskDraftView>(matching.Take(limit).ToArray(), null));
        }

        public ValueTask<RiskDraftRevisionView?> GetRevisionAsync(Uuid tenantId, Uuid riskId,
            long revision, CancellationToken ct = default) => ValueTask.FromResult<RiskDraftRevisionView?>(null);
    }
}

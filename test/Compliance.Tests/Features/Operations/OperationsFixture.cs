using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
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
                .AddRequestAuthorizer<OperationsReactionAuthorizer>(),
            services =>
            {
                services.AddScoped<OperatingAuthority>();
                services.AddSingleton<IControlDraftDirectoryReader, ProgramControls>();
            });
        var fixture = new OperationsFixture { Provider = provider, Permissions = permissions };
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

    public Task AddToTeamAsync(Uuid userId) => ProgramManagementServices.SeedAsync(Provider,
        new TeamMember(TenantId, TeamId, Member(userId)), membership => membership.Assign());

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

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) => ValueTask.FromResult(
            permission == IProgramReadRequest.ReadPermission || Managers.Contains(memberId));
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
}

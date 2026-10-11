using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlOwnerPersonReadTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldReadExactDesignationGivenRehydratedOpenDraft()
    {
        // Arrange
        await using var provider = Build();
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var controlId = ControlDraft.IdFor(tenantId, programId, "OWNER-01");
        var userId = Uuid.CreateVersion4();
        var actorId = RbacIds.Member(tenantId, userId);
        var actor = ActorReference.ForMember(actorId, "Recorded author");
        var designationId = Uuid.CreateVersion4();
        var personId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(provider, new Person(tenantId, personId),
            person => person.Record("Workforce owner", "owner@example.com", actor, Now));
        await ProgramManagementServices.SeedAsync(provider, new ControlDraft(tenantId, controlId),
            control => control.Create(programId, Uuid.CreateVersion4(), "OWNER-01", Content(),
                actorId, actor.Display, Now));
        await ProgramManagementServices.SeedAsync(provider, new ControlDraft(tenantId, controlId),
            control =>
            {
                Assert.Null(control.DesignatePersonOwner(programId, 1, designationId, personId,
                    null, "  Recorded rationale.  ", actorId, actor, Now.AddMinutes(1)));
                return Result.Success;
            });
        var before = await ProgramManagementServices.HydrateAsync(provider,
            new ControlDraft(tenantId, controlId));

        // Act
        var result = await RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(userId))
            .When(new GetControlDraftOwnerPerson(tenantId, programId, controlId))
            .ExpectSuccess();

        // Assert
        var view = result.Value;
        Assert.Equal(tenantId, view.TenantId);
        Assert.Equal(programId, view.ProgramId);
        Assert.Equal(controlId, view.ControlId);
        Assert.Equal(1, view.Revision);
        Assert.Equal(ControlVersionIds.Initial(controlId), view.DraftVersionId);
        Assert.Equal(new ControlPersonOwnerDesignationView(personId, designationId,
            "Recorded rationale.", actor, Now.AddMinutes(1)), view.Designation);
        var person = await ProgramManagementServices.HydrateAsync(provider, new Person(tenantId, personId));
        Assert.True(person.IsCreated);
        Assert.Null(person.CorrelatedUserId);
        var after = await ProgramManagementServices.HydrateAsync(provider,
            new ControlDraft(tenantId, controlId));
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldReadReplacementMetadataGivenUnchangedContentRevision()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var original = await fixture.ReadAsync();
        var replacement = fixture.Designated(Uuid.CreateVersion4()) with
        {
            Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Replacement recorder"),
            DesignatedAt = Now.AddHours(1),
            Rationale = "Replacement rationale.",
        };
        await fixture.SeedAsync(replacement);
        var before = await fixture.PositionsAsync();

        // Act
        var current = await fixture.ReadAsync();

        // Assert
        Assert.Equal(original.DraftVersionId, current.DraftVersionId);
        Assert.Equal(original.Revision, current.Revision);
        Assert.Equal(View(replacement), current.Designation);
        Assert.NotEqual(original.Designation, current.Designation);
        Assert.Equal(before, await fixture.PositionsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReturnNullDesignationGivenUnassignedOrClearedDraft(bool cleared)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(designated: cleared);
        if (cleared)
            await fixture.SeedAsync(fixture.Designated(null));
        var before = await fixture.PositionsAsync();

        // Act
        var current = await fixture.ReadAsync();

        // Assert
        Assert.Equal(fixture.InitialVersionId, current.DraftVersionId);
        Assert.Equal(1, current.Revision);
        Assert.Null(current.Designation);
        Assert.Equal(before, await fixture.PositionsAsync());
    }

    [Fact]
    public async Task ShouldRequireExactRevisionDesignationGivenRevisedDraft()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(fixture.Revised(2));
        var before = await fixture.PositionsAsync();

        // Act
        var unassigned = await fixture.ReadAsync();
        var designation = fixture.Designated(Uuid.CreateVersion4(), revision: 2);
        await fixture.SeedAsync(designation);
        var current = await fixture.ReadAsync();

        // Assert
        Assert.Equal(2, unassigned.Revision);
        Assert.Null(unassigned.Designation);
        Assert.Equal(View(designation), current.Designation);
        Assert.Equal(before[0] + 1, (await fixture.PositionsAsync())[0]);
    }

    [Fact]
    public async Task ShouldRequireNewVersionDesignationGivenSuccessorDraft()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var successorId = Uuid.CreateVersion4();
        await fixture.SeedAsync(fixture.Approved(), fixture.Successor(successorId), fixture.Revised(2));

        // Act
        var unassigned = await fixture.ReadAsync();
        var designation = fixture.Designated(Uuid.CreateVersion4(), successorId, 2);
        await fixture.SeedAsync(designation);
        var current = await fixture.ReadAsync();

        // Assert
        Assert.Equal(successorId, unassigned.DraftVersionId);
        Assert.Equal(2, unassigned.Revision);
        Assert.Null(unassigned.Designation);
        Assert.Equal(View(designation), current.Designation);
    }

    [Theory]
    [InlineData("approved", 1)]
    [InlineData("withdrawn", 3)]
    [InlineData("retired", 1)]
    public async Task ShouldReturnNullDraftAndDesignationGivenClosedLine(string state, long revision)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(fixture.Approved());
        if (state == "withdrawn")
        {
            var successorId = Uuid.CreateVersion4();
            await fixture.SeedAsync(fixture.Successor(successorId), fixture.Revised(2),
                fixture.Designated(Uuid.CreateVersion4(), successorId, 2),
                new ControlProposalWithdrawn(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                    Uuid.CreateVersion4(), "successor", successorId, fixture.InitialVersionId, 2,
                    "Withdrawn.", fixture.MemberId, fixture.Actor, Now), fixture.Revised(3));
        }
        if (state == "retired")
            await fixture.SeedAsync(new ControlRetired(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, Uuid.CreateVersion4(), fixture.InitialVersionId, 1,
                Uuid.CreateVersion4(), Uuid.CreateVersion4(), new DateOnly(2027, 1, 1), "digest",
                fixture.MemberId, fixture.Actor.Display, "Retired.", Now));
        var before = await fixture.PositionsAsync();

        // Act
        var current = await fixture.ReadAsync();

        // Assert
        Assert.Equal(fixture.TenantId, current.TenantId);
        Assert.Equal(fixture.ProgramId, current.ProgramId);
        Assert.Equal(fixture.ControlId, current.ControlId);
        Assert.Equal(revision, current.Revision);
        Assert.Null(current.DraftVersionId);
        Assert.Null(current.Designation);
        Assert.Equal(before, await fixture.PositionsAsync());
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("discarded")]
    [InlineData("wrong_program")]
    public async Task ShouldHideOwnerGivenInvisibleOrWrongProgramControl(string condition)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        if (condition == "discarded")
            await fixture.SeedAsync(new ControlDraftDiscarded(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, 1, fixture.MemberId, fixture.Actor.Display, "Discarded.", Now));
        var before = await fixture.PositionsAsync();
        var controlId = condition == "unknown" ? Uuid.CreateVersion4() : fixture.ControlId;
        var programId = condition == "wrong_program" ? Uuid.CreateVersion4() : fixture.ProgramId;

        // Act
        var result = await RequestScenario.For(fixture.Provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.UserId))
            .When(new GetControlDraftOwnerPerson(fixture.TenantId, programId, controlId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(before, await fixture.PositionsAsync());
    }

    [Theory]
    [InlineData("anonymous", RequestErrorKind.Unauthorized)]
    [InlineData("program_denied", RequestErrorKind.Forbidden)]
    [InlineData("foreign_tenant", RequestErrorKind.NotFound)]
    public async Task ShouldDenyOwnerReadWithoutDisclosureOrAppendGivenUnauthorizedScope(
        string condition, RequestErrorKind error)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(allowed: condition != "program_denied");
        var before = await fixture.PositionsAsync();
        var tenantId = condition == "foreign_tenant" ? Uuid.CreateVersion4() : fixture.TenantId;
        var actor = condition == "anonymous" ? new ClaimsPrincipal()
            : ProgramManagementServices.Actor(fixture.UserId);

        // Act
        var denied = await RequestScenario.For(fixture.Provider).GivenActor(actor)
            .When(new GetControlDraftOwnerPerson(tenantId, fixture.ProgramId, fixture.ControlId))
            .ExpectDenied(error).ExpectNotHandled();

        // Assert
        Assert.False(denied.IsSuccess);
        Assert.Equal(before, await fixture.PositionsAsync());
        Assert.Equal(condition == "program_denied" ? [RbacPermissions.ProgramManage] : [],
            fixture.Permissions.Permissions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldReadRetainedDesignationWithoutEnablingWritesGivenLifecycleFlag(bool enabled)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(lifecycleEnabled: enabled);
        var before = await fixture.PositionsAsync();

        // Act
        var current = await fixture.ReadAsync();
        if (!enabled)
            await RequestScenario.For(fixture.Provider)
                .GivenActor(ProgramManagementServices.Actor(fixture.UserId))
                .When(new DesignateControlOwnerPerson(fixture.TenantId, fixture.ProgramId,
                    fixture.ControlId, 1, null, "Clear owner."))
                .ExpectAuthorized().ExpectHandled().ExpectFailure(RequestErrorKind.Conflict);
        var after = await fixture.ReadAsync();

        // Assert
        Assert.NotNull(current.Designation);
        Assert.Equal(current, after);
        Assert.Equal(before, await fixture.PositionsAsync());
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlDraft(fixture.TenantId, fixture.ControlId));
        Assert.Equal(current.Designation!.PersonId, source.CurrentPersonOwner!.PersonId);
        Assert.Equal(current.Designation.DesignationId, source.CurrentPersonOwner.DesignationId);
        Assert.Equal(fixture.MemberId, source.CurrentPersonOwner.CorrelatedMemberId);
        Assert.All(fixture.Permissions.Permissions,
            permission => Assert.Equal(RbacPermissions.ProgramManage, permission));
    }

    static ControlPersonOwnerDesignationView View(ControlOwnerPersonDesignated designation) =>
        new(designation.PersonId!.Value, designation.DesignationId, designation.Rationale,
            designation.Actor, designation.DesignatedAt);

    static ServiceProvider Build() => ProgramManagementServices.Build(
        new RecordingPermissionAuthorizer(allowed: true),
        portia => portia.AddRequestHandler<GetControlDraftOwnerPersonHandler>(),
        services => services.AddScoped<ControlActivationSource>());

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access.", "The owner reviews access quarterly.", ["Review record"]);

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        Uuid GrantId { get; } = Uuid.CreateVersion4();
        public Uuid MemberId => RbacIds.Member(TenantId, UserId);
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "OWNER-REPLAY");
        public Uuid InitialVersionId => ControlVersionIds.Initial(ControlId);
        public ActorReference Actor => ActorReference.ForMember(MemberId, "Saved recorder");
        public ServiceProvider Provider { get; private set; } = null!;
        public RecordingPermissionAuthorizer Permissions { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(bool designated = true, bool allowed = true,
            bool lifecycleEnabled = false)
        {
            var fixture = new Fixture { Permissions = new RecordingPermissionAuthorizer(allowed) };
            fixture.Provider = ProgramManagementServices.Build(fixture.Permissions,
                portia => portia.AddRequestHandler<GetControlDraftOwnerPersonHandler>()
                    .AddRequestHandler<DesignateControlOwnerPersonHandler>(),
                services => services.AddScoped<ControlActivationSource>()
                    .AddSingleton(new ControlLifecycleReleaseGate(lifecycleEnabled))
                    .AddSingleton<ITenantMembershipDirectoryReader>(new TenantMembership(fixture.TenantId)));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new Member(fixture.TenantId, fixture.UserId), member => member.Register());
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new AccessGrant(fixture.TenantId, fixture.GrantId), grant => grant.Issue(
                    new AccessGrantTerms(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                            fixture.MemberId), Uuid.CreateVersion4(),
                        new AccessGrantScope(AccessGrantScopeKind.Program, fixture.ProgramId),
                        new AccessGrantSource("manual", "owner-read-test"), fixture.Actor, Now, null)));
            await fixture.SeedAsync(new ControlDraftCreated(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, Uuid.CreateVersion4(), "OWNER-REPLAY", Content(), fixture.MemberId,
                fixture.Actor.Display, Now));
            if (designated)
                await fixture.SeedAsync(fixture.Designated(Uuid.CreateVersion4()));
            return fixture;
        }

        public ControlOwnerPersonDesignated Designated(Uuid? personId, Uuid? versionId = null,
            long revision = 1) => new(TenantId, ProgramId, ControlId, versionId ?? InitialVersionId,
            revision, Uuid.CreateVersion4(), personId, personId is null ? null : MemberId,
            "Saved rationale.", MemberId, Actor, Now);

        public ControlDraftRevised Revised(long revision) => new(TenantId, ProgramId, ControlId,
            revision, Content() with { Title = $"Revision {revision}" }, MemberId, Actor.Display, Now);

        public ControlApproved Approved() => new(TenantId, ProgramId, ControlId, InitialVersionId,
            1, Uuid.CreateVersion4(), Uuid.CreateVersion4(), Content(), Uuid.CreateVersion4(), MemberId,
            Uuid.CreateVersion4(), "Approver", "Approved.", new DateOnly(2026, 10, 12), Now);

        public ControlSuccessorProposed Successor(Uuid versionId) => new(TenantId, ProgramId,
            ControlId, versionId, InitialVersionId, 2, MemberId, Actor.Display, Now);

        public async Task SeedAsync(params DomainEvent[] events)
        {
            var control = await ProgramManagementServices.HydrateAsync(Provider,
                new ControlDraft(TenantId, ControlId));
            var seeded = events.Select((ev, index) => DomainEventSeed.Attach(
                (DomainEvent)JsonSerializer.Deserialize(JsonSerializer.Serialize(ev, ev.GetType(),
                    ComplianceCoreJsonContext.Default), ev.GetType(), ComplianceCoreJsonContext.Default)!,
                ControlId, control.CommittedStreamPosition + (ulong)index + 1)).ToArray();
            await Provider.GetRequiredService<IEventStore>().AppendAsync(
                new EventStreamAddress(TenantId.ToString(), "controls", ControlId.ToString()),
                control.CommittedStreamPosition, seeded, CancellationToken.None);
        }

        public async Task<ControlDraftOwnerPersonView> ReadAsync() =>
            (await RequestScenario.For(Provider).GivenActor(ProgramManagementServices.Actor(UserId))
                .When(new GetControlDraftOwnerPerson(TenantId, ProgramId, ControlId))
                .ExpectSuccess()).Value;

        public async Task<ulong[]> PositionsAsync() =>
        [
            (await ProgramManagementServices.HydrateAsync(Provider,
                new ControlDraft(TenantId, ControlId))).CommittedStreamPosition,
            (await ProgramManagementServices.HydrateAsync(Provider,
                new Member(TenantId, UserId))).CommittedStreamPosition,
            (await ProgramManagementServices.HydrateAsync(Provider,
                new AccessGrant(TenantId, GrantId))).CommittedStreamPosition,
        ];

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class TenantMembership(Uuid tenantId) : ITenantMembershipDirectoryReader
    {
        readonly FixedMembershipDirectory _member = new(true);

        public ValueTask<TenantMembershipView?> GetAsync(string requestedTenantId, Uuid userId,
            CancellationToken ct = default) => requestedTenantId == tenantId.ToString()
            ? _member.GetAsync(requestedTenantId, userId, ct)
            : ValueTask.FromResult<TenantMembershipView?>(null);

        public ValueTask<bool> IsMemberAsync(string requestedTenantId, Uuid userId,
            CancellationToken ct = default) => ValueTask.FromResult(requestedTenantId == tenantId.ToString());

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid requestedTenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

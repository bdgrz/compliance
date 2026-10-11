using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Workforce;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlOwnerPersonWriteTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldDesignateAccountableOwnerWithoutGrantingAccessGivenPersonWithoutAccount()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ControlAsync();
        var personBefore = await fixture.PersonAsync(fixture.TenantId, fixture.CandidatePersonId);
        Assert.True(personBefore.IsCreated);
        Assert.Null(personBefore.CorrelatedUserId);
        var localEvents = await fixture.EventsAsync(fixture.TenantId);
        var foreignEvents = await fixture.EventsAsync(fixture.ForeignTenantId);
        var requestId = Uuid.CreateVersion4();
        var correlationId = Uuid.CreateVersion4();

        // Act
        await RequestScenario.For(fixture.Provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.UserId))
            .GivenMetadata(new RequestMetadata(requestId, correlationId, null))
            .When(fixture.Request(fixture.CandidatePersonId))
            .ExpectAuthorized().ExpectHandled().ExpectSuccess();

        // Assert
        var afterEvents = await fixture.EventsAsync(fixture.TenantId);
        Assert.Equal(localEvents, afterEvents.Take(localEvents.Length));
        var appended = Assert.Single(afterEvents.Skip(localEvents.Length));
        Assert.Equal(new EventStreamAddress(fixture.TenantId.ToString(), "controls",
            fixture.ControlId.ToString()), appended.Stream);
        Assert.Equal(before.CommittedStreamPosition, appended.ResourceOffset);
        var designation = Assert.IsType<ControlOwnerPersonDesignated>(appended.Event);
        var expectedActor = ActorReference.ForMember(fixture.MemberId, "author@example.com");
        Assert.Equal(new ControlOwnerPersonDesignated(fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, before.DraftVersionId, before.Revision, requestId,
            fixture.CandidatePersonId, null, "The workforce owner is accountable.",
            fixture.MemberId, expectedActor, Now), designation);
        Assert.Equal(requestId, designation.Metadata.CausationId);
        Assert.Equal(correlationId, designation.Metadata.CorrelationId);
        Assert.Equal(before.CommittedStreamPosition + 1, designation.Metadata.AggregateVersion);
        Assert.Equal(foreignEvents, await fixture.EventsAsync(fixture.ForeignTenantId));
        var after = await fixture.ControlAsync();
        Assert.Equal(before.CommittedStreamPosition + 1, after.CommittedStreamPosition);
        Assert.Equal(before.DraftVersionId, after.DraftVersionId);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(before.CurrentContent, after.CurrentContent);
        Assert.Equal(new ControlPersonOwner(fixture.CandidatePersonId, null, requestId),
            after.CurrentPersonOwner);
        Assert.Equal(new ControlPersonOwnerDesignationView(fixture.CandidatePersonId, requestId,
            designation.Rationale, expectedActor, Now), after.CurrentPersonOwnerDesignation);
        var personAfter = await fixture.PersonAsync(fixture.TenantId, fixture.CandidatePersonId);
        Assert.Null(personAfter.CorrelatedUserId);
        Assert.Equal(personBefore.Revision, personAfter.Revision);
        Assert.Equal(personBefore.CommittedStreamPosition, personAfter.CommittedStreamPosition);
        Assert.Equal([RbacPermissions.ProgramManage], fixture.Permissions.Permissions);
        Assert.Equal([fixture.MemberId], fixture.Permissions.MemberIds);
    }

    [Theory]
    [InlineData("anonymous", RequestErrorKind.Unauthorized)]
    [InlineData("program_denied", RequestErrorKind.Forbidden)]
    [InlineData("foreign_tenant", RequestErrorKind.NotFound)]
    public async Task ShouldPreserveOwnerAndStreamsGivenUnauthorizedOwnerWrite(
        string condition, RequestErrorKind error)
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync(allowed: condition != "program_denied");
        var before = await fixture.ControlAsync();
        var localEvents = await fixture.EventsAsync(fixture.TenantId);
        var foreignEvents = await fixture.EventsAsync(fixture.ForeignTenantId);
        var actor = condition == "anonymous" ? new ClaimsPrincipal()
            : ProgramManagementServices.Actor(fixture.UserId);
        var request = fixture.Request(fixture.CandidatePersonId) with
        {
            TenantId = condition == "foreign_tenant" ? fixture.ForeignTenantId : fixture.TenantId,
        };

        // Act
        await RequestScenario.For(fixture.Provider).GivenActor(actor).When(request)
            .ExpectDenied(error).ExpectNotHandled();

        // Assert
        await fixture.AssertOwnerUnchangedAsync(before);
        Assert.Equal(localEvents, await fixture.EventsAsync(fixture.TenantId));
        Assert.Equal(foreignEvents, await fixture.EventsAsync(fixture.ForeignTenantId));
        Assert.Equal(condition == "program_denied" ? [RbacPermissions.ProgramManage] : [],
            fixture.Permissions.Permissions);
        Assert.Equal(condition == "program_denied" ? [fixture.MemberId] : [],
            fixture.Permissions.MemberIds);
    }

    [Fact]
    public async Task ShouldPreserveOwnerAndBothOrganizationsGivenForeignOrganizationPerson()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var before = await fixture.ControlAsync();
        var foreignPerson = await fixture.PersonAsync(fixture.ForeignTenantId, fixture.ForeignPersonId);
        Assert.True(foreignPerson.IsCreated);
        Assert.False((await fixture.PersonAsync(fixture.TenantId, fixture.ForeignPersonId)).IsCreated);
        var localEvents = await fixture.EventsAsync(fixture.TenantId);
        var foreignEvents = await fixture.EventsAsync(fixture.ForeignTenantId);

        // Act
        await RequestScenario.For(fixture.Provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.UserId))
            .When(fixture.Request(fixture.ForeignPersonId))
            .ExpectAuthorized().ExpectHandled().ExpectFailure(RequestErrorKind.Validation);

        // Assert
        await fixture.AssertOwnerUnchangedAsync(before);
        Assert.Equal(localEvents, await fixture.EventsAsync(fixture.TenantId));
        Assert.Equal(foreignEvents, await fixture.EventsAsync(fixture.ForeignTenantId));
        Assert.Equal([RbacPermissions.ProgramManage], fixture.Permissions.Permissions);
        Assert.Equal([fixture.MemberId], fixture.Permissions.MemberIds);
    }

    static ControlDraftContent Content() => new("Access review", "Review access",
        "Management reviews access.", "The owner reviews access quarterly.", ["Review record"]);

    sealed class Fixture : IAsyncDisposable
    {
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ForeignTenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid UserId { get; } = Uuid.CreateVersion4();
        Uuid OriginalPersonId { get; } = Uuid.CreateVersion4();
        public Uuid CandidatePersonId { get; } = Uuid.CreateVersion4();
        public Uuid ForeignPersonId { get; } = Uuid.CreateVersion4();
        Uuid OriginalDesignationId { get; } = Uuid.CreateVersion4();
        public Uuid MemberId => RbacIds.Member(TenantId, UserId);
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "OWNER-WRITE");
        ActorReference Actor => ActorReference.ForMember(MemberId, "Saved recorder");
        public ServiceProvider Provider { get; private set; } = null!;
        public RecordingPermissionAuthorizer Permissions { get; private set; } = null!;

        public static async Task<Fixture> CreateAsync(bool allowed = true)
        {
            var fixture = new Fixture { Permissions = new RecordingPermissionAuthorizer(allowed) };
            fixture.Provider = ProgramManagementServices.Build(fixture.Permissions,
                portia => portia.AddRequestHandler<DesignateControlOwnerPersonHandler>(),
                services => services.AddSingleton(new ControlLifecycleReleaseGate(true))
                    .AddSingleton<TimeProvider>(new FixedClock())
                    .AddSingleton<ITenantMembershipDirectoryReader>(
                        new TenantMembership(fixture.TenantId, fixture.UserId))
                    .AddSingleton<IDomainEventReader>(provider =>
                        (IDomainEventReader)provider.GetRequiredService<IEventStore>()));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new Member(fixture.TenantId, fixture.UserId), member => member.Register());
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new AccessGrant(fixture.TenantId, Uuid.CreateVersion4()), grant => grant.Issue(
                    new AccessGrantTerms(new AccessGrantPrincipal(AccessGrantPrincipalKind.Member,
                            fixture.MemberId), Uuid.CreateVersion4(),
                        new AccessGrantScope(AccessGrantScopeKind.Program, fixture.ProgramId),
                        new AccessGrantSource("manual", "owner-write-test"), fixture.Actor,
                        Now.AddDays(-1), null)));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new Person(fixture.TenantId, fixture.OriginalPersonId), person =>
                    person.Record("Original owner", "original@example.com", fixture.Actor, Now.AddDays(-1)));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new Person(fixture.TenantId, fixture.CandidatePersonId), person =>
                    person.Record("Workforce owner", "owner@example.com", fixture.Actor, Now.AddDays(-1)));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new Person(fixture.ForeignTenantId, fixture.ForeignPersonId), person =>
                    person.Record("Foreign owner", "foreign@example.com",
                        ActorReference.ForMember(RbacIds.Member(fixture.ForeignTenantId,
                            Uuid.CreateVersion4()), "Foreign recorder"), Now.AddDays(-1)));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control =>
                    control.Create(fixture.ProgramId, Uuid.CreateVersion4(), "OWNER-WRITE", Content(),
                        fixture.MemberId, fixture.Actor.Display, Now.AddDays(-1)));
            await ProgramManagementServices.SeedAsync(fixture.Provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control =>
                {
                    Assert.Null(control.DesignatePersonOwner(fixture.ProgramId, 1,
                        fixture.OriginalDesignationId, fixture.OriginalPersonId, null,
                        "Original owner rationale.", fixture.MemberId, fixture.Actor, Now.AddDays(-1)));
                    return Result.Success;
                });
            return fixture;
        }

        public DesignateControlOwnerPerson Request(Uuid personId) => new(TenantId, ProgramId,
            ControlId, 1, personId, "  The workforce owner is accountable.  ");

        public Task<ControlDraft> ControlAsync() => ProgramManagementServices.HydrateAsync(Provider,
            new ControlDraft(TenantId, ControlId));

        public Task<Person> PersonAsync(Uuid tenantId, Uuid personId) =>
            ProgramManagementServices.HydrateAsync(Provider, new Person(tenantId, personId));

        public async Task<DomainEventRecord[]> EventsAsync(Uuid tenantId)
        {
            var events = new List<DomainEventRecord>();
            await foreach (var record in Provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                EventStreamPattern.ForPattern(tenantId.ToString()), EventCursor.Start, CancellationToken.None))
                events.Add(record);
            return events.ToArray();
        }

        public async Task AssertOwnerUnchangedAsync(ControlDraft before)
        {
            Assert.Equal(new ControlPersonOwner(OriginalPersonId, null, OriginalDesignationId),
                before.CurrentPersonOwner);
            var after = await ControlAsync();
            Assert.Equal(before.CurrentPersonOwner, after.CurrentPersonOwner);
            Assert.Equal(before.CurrentPersonOwnerDesignation, after.CurrentPersonOwnerDesignation);
            Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
            Assert.Equal(before.DraftVersionId, after.DraftVersionId);
            Assert.Equal(before.Revision, after.Revision);
            Assert.Equal(before.CurrentContent, after.CurrentContent);
        }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class TenantMembership(Uuid tenantId, Uuid userId) : ITenantMembershipDirectoryReader
    {
        readonly FixedMembershipDirectory _member = new(true);

        public ValueTask<TenantMembershipView?> GetAsync(string requestedTenantId, Uuid requestedUserId,
            CancellationToken ct = default) => requestedTenantId == tenantId.ToString() && requestedUserId == userId
            ? _member.GetAsync(requestedTenantId, requestedUserId, ct)
            : ValueTask.FromResult<TenantMembershipView?>(null);

        public ValueTask<bool> IsMemberAsync(string requestedTenantId, Uuid requestedUserId,
            CancellationToken ct = default) => ValueTask.FromResult(
                requestedTenantId == tenantId.ToString() && requestedUserId == userId);

        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid requestedTenantId, int limit,
            string? cursor, CancellationToken ct = default) =>
            ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

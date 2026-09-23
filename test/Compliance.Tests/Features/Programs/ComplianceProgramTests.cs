using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Programs;

public sealed class ComplianceProgramTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldReturnDomainFailureGivenProgramCreateAndRevisionPrecedence()
    {
        // Arrange
        var program = new ComplianceProgram(TenantId, ProgramId);
        var valid = new ProgramPlan(null, null, null, null, null, null);
        var invalid = valid with
        {
            TargetTypeIIStartDate = new DateOnly(2028, 4, 1),
            TargetTypeIIEndDate = new DateOnly(2028, 3, 1),
        };

        // Act
        var missing = program.Revise(1, "", invalid, MemberId, "Lead", Now);
        var invalidCreate = program.Create("", invalid, MemberId, "Lead", Now);
        var created = program.Create("SOC 2", valid, MemberId, "Lead", Now);
        var replay = program.Create(" SOC 2 ", valid, MemberId, "Lead", Now);
        var changedCreate = program.Create("", invalid, MemberId, "Lead", Now);
        var stale = program.Revise(0, "", invalid, MemberId, "Lead", Now);
        var invalidCurrent = program.Revise(1, "SOC 2", invalid, MemberId, "Lead", Now);

        // Assert
        Assert.Equal(CommandFailureCode.MissingRecord,
            Assert.IsType<CommandFailure>(missing).Code);
        Assert.Equal("A program requires a name.",
            Assert.IsType<CommandFailure>(invalidCreate).Message);
        Assert.Null(created);
        Assert.Null(replay);
        var alreadyExists = Assert.IsType<CommandFailure>(changedCreate);
        Assert.Equal(CommandFailureCode.StateConflict, alreadyExists.Code);
        Assert.Equal("The program already exists with different content.", alreadyExists.Message);
        var version = Assert.IsType<CommandFailure>(stale);
        Assert.Equal(CommandFailureCode.VersionConflict, version.Code);
        Assert.Equal(1, version.Version?.CurrentRevision);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(invalidCurrent).Code);
        Assert.Single(new AggregateScenario<ComplianceProgram>(program).PendingEvents);
    }

    [Fact]
    public async Task ShouldAttributeProgramToSessionGivenProviderClaimsAppearFirst()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var userId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[]
            {
                new Claim("iss", "https://issuer.example/"),
                new Claim("sub", "external-subject"),
                new Claim("email", "provider@example.com"),
            }, "oidc"),
            new ClaimsIdentity(new[]
            {
                new Claim("iss", "bdgrz"),
                new Claim("sub", userId.ToString()),
                new Claim("email", "session@example.com"),
            }, "BdgrzSession"),
        });
        var context = new RequestContext<CreateProgram>(
            new CreateProgram(TenantId, "SOC 2", new ProgramPlan(null, null, null, null,
                null, null)), actor);
        var handler = new CreateProgramHandler(fixture.Repository, TimeProvider.System);

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        DomainEvent? created = null;
        await foreach (var record in fixture.Store.ReadAsync(
                           new ComplianceProgram(TenantId, context.RequestId).Stream, 0,
                           CancellationToken.None))
            created = Assert.IsType<ProgramCreated>(record.Event);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(created);
        Assert.Equal(RbacIds.Member(TenantId, userId),
            Assert.IsType<ProgramCreated>(created).ActorMemberId);
        Assert.Equal("session@example.com", Assert.IsType<ProgramCreated>(created).ActorDisplay);
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationGivenIdenticalCreateRetry()
    {
        // Arrange
        await using var fixture = new StoreFixture();
        var requestId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", Uuid.CreateVersion4().ToString())],
            "BdgrzSession"));
        var plan = new ProgramPlan(null, null, null, null, null, null);
        var handler = new CreateProgramHandler(fixture.Repository, TimeProvider.System);
        var original = new RequestContext<CreateProgram>(
            new CreateProgram(TenantId, "SOC 2", plan), actor, requestId);
        var identical = new RequestContext<CreateProgram>(
            new CreateProgram(TenantId, " SOC 2 ", plan), actor, requestId);
        var changed = new RequestContext<CreateProgram>(
            new CreateProgram(TenantId, "Different", plan), actor, requestId);

        // Act
        var first = await handler.HandleAsync(original, CancellationToken.None);
        var retry = await handler.HandleAsync(identical, CancellationToken.None);
        var rejected = await handler.HandleAsync(changed, CancellationToken.None);
        var eventCount = 0;
        await foreach (var record in fixture.Store.ReadAsync(
                           new ComplianceProgram(TenantId, requestId).Stream, 0,
                           CancellationToken.None))
        {
            Assert.IsType<ProgramCreated>(record.Event);
            eventCount++;
        }

        // Assert
        Assert.Equal(requestId, Assert.IsType<ProgramRegistration>(first.Value).ProgramId);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(RequestErrorKind.Conflict,
            Assert.IsType<RequestError>(rejected.Error).Kind);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void ShouldUseSessionSubjectGivenUntrustedEmailAndNoSessionEmail()
    {
        // Arrange
        var userId = Uuid.CreateVersion4();
        var actor = new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(new[] { new Claim("iss", "bdgrz"),
                new Claim("email", "unauthenticated@example.com") }),
            new ClaimsIdentity(new[] { new Claim("iss", "https://issuer.example/"),
                new Claim("email", "provider@example.com") }, "oidc"),
            new ClaimsIdentity(new[] { new Claim("iss", "bdgrz"),
                new Claim("sub", userId.ToString()) }, "BdgrzSession"),
        });

        // Act
        var found = UserIdentityClaims.TryGetBdgrzSubject(actor, out var subject);
        var display = UserIdentityClaims.BdgrzDisplay(actor, subject);

        // Assert
        Assert.True(found);
        Assert.Equal(userId, subject);
        Assert.Equal(userId.ToString(), display);
    }

    [Fact]
    public void ShouldPreserveEventsAndActorSnapshotGivenProgramRevision()
    {
        // Arrange
        var program = new ComplianceProgram(TenantId, ProgramId);
        var original = new ProgramPlan(null, new DateOnly(2027, 3, 1),
            new DateOnly(2027, 4, 1), new DateOnly(2028, 3, 31), "Advisor A", null);
        var revised = original with { ReadinessAdvisor = "Advisor B" };

        var created = program.Create("SOC 2", original, MemberId, "Lead A", Now);
        var changed = program.Revise(1, "SOC 2 program", revised, MemberId, "Lead B", Now.AddDays(1));

        // Act
        var events = new AggregateScenario<ComplianceProgram>(program).PendingEvents;

        // Assert
        Assert.Null(created);
        Assert.Null(changed);
        Assert.Collection(events,
            ev => Assert.Equal("Lead A", Assert.IsType<ProgramCreated>(ev).ActorDisplay),
            ev =>
            {
                Assert.Equal("ProgramRevised", ev.GetType().Name);
                Assert.Equal(2L, ev.GetType().GetProperty("Revision")?.GetValue(ev));
                Assert.Equal("Lead B", ev.GetType().GetProperty("ActorDisplay")?.GetValue(ev));
            });
    }

    [Fact]
    public void ShouldPreserveLegacyActorGivenDifferentReviserAndReplay()
    {
        // Arrange
        var originalMemberId = Uuid.CreateVersion4();
        var replacementMemberId = Uuid.CreateVersion4();
        var program = new ComplianceProgram(TenantId, ProgramId);
        var plan = new ProgramPlan(null, null, null, null, null, null);
        Assert.Null(program.Create("SOC 2", plan, originalMemberId, "Original name", Now));
        Assert.Null(program.Revise(1, "SOC 2 revised", plan, replacementMemberId,
            "Replacement name", Now.AddDays(1)));
        var events = new AggregateScenario<ComplianceProgram>(program).PendingEvents;
        var original = Assert.IsType<ProgramCreated>(events[0]);
        var replacement = Assert.IsType<ProgramRevised>(events[1]);
        var legacyJson = $$"""
            {"tenant_id":"{{TenantId}}","program_id":"{{ProgramId}}","name":"SOC 2",
             "plan":{},"actor_member_id":"{{originalMemberId}}",
             "actor_display":"Original name","changed_at":"{{Now:O}}"}
            """;
        var legacyRevisionJson = $$"""
            {"program_id":"{{ProgramId}}","revision":1,"name":"SOC 2",
             "plan":{},"actor_member_id":"{{originalMemberId}}",
             "actor_display":"Original name","changed_at":"{{Now:O}}"}
            """;

        // Act
        var replayed = JsonSerializer.Deserialize(legacyJson,
            ComplianceCoreJsonContext.Default.ProgramCreated);
        var projected = JsonSerializer.Deserialize(legacyRevisionJson,
            ComplianceCoreJsonContext.Default.ProgramRevisionView);
        var currentJson = JsonSerializer.Serialize(original,
            ComplianceCoreJsonContext.Default.ProgramCreated);
        using var currentDocument = JsonDocument.Parse(currentJson);

        // Assert
        Assert.Equal(ActorReference.ForMember(originalMemberId, "Original name"), original.Actor);
        Assert.Equal(original.Actor, original.StoredActor);
        Assert.Equal(ActorReference.ForMember(originalMemberId, "Original name"), replayed?.Actor);
        Assert.Null(replayed?.StoredActor);
        Assert.Equal(ActorReference.ForMember(originalMemberId, "Original name"), projected?.Actor);
        Assert.Equal(ActorReference.ForMember(replacementMemberId, "Replacement name"),
            replacement.Actor);
        Assert.Equal(replacement.Actor, replacement.StoredActor);
        Assert.NotEqual(replacement.Actor.Id, replayed?.Actor.Id);
        Assert.Equal("Original name", currentDocument.RootElement.GetProperty("actor")
            .GetProperty("display").GetString());
    }

    [Fact]
    public void ShouldRejectChangedCreateGivenExistingProgramAndPreserveReplay()
    {
        // Arrange
        var program = new ComplianceProgram(TenantId, ProgramId);
        var original = new ProgramPlan(null, null, null, null, "Advisor A", null);
        Assert.Null(program.Create("SOC 2", original, MemberId, "Lead", Now));
        Assert.Null(program.Revise(1, "SOC 2 revised", original with { ReadinessAdvisor = "Advisor B" },
            MemberId, "Lead", Now.AddMinutes(1)));

        // Act
        var replay = program.Create(" SOC 2 ", original, MemberId, "Lead", Now.AddMinutes(2));
        var changed = program.Create("Different", original, MemberId, "Lead", Now.AddMinutes(2));

        // Assert
        Assert.Null(replay);
        var conflict = Assert.IsType<CommandFailure>(changed);
        Assert.Equal(CommandFailureCode.StateConflict, conflict.Code);
        Assert.Equal("The program already exists with different content.", conflict.Message);
        Assert.Equal(2, new AggregateScenario<ComplianceProgram>(program).PendingEvents.Count);
    }

    [Fact]
    public void ShouldAvoidEventsGivenStaleRevisionOrInvalidPeriod()
    {
        // Arrange
        var program = new ComplianceProgram(TenantId, ProgramId);

        // Act
        var valid = new ProgramPlan(null, null, null, null, null, null);

        // Assert
        Assert.Null(program.Create("SOC 2", valid, MemberId, "Lead", Now));
        var invalid = valid with
        {
            TargetTypeIIStartDate = new DateOnly(2028, 4, 1),
            TargetTypeIIEndDate = new DateOnly(2028, 3, 1),
        };
        var reversedJourney = valid with
        {
            TargetReadinessDate = new DateOnly(2028, 4, 1),
            TargetTypeIAsOfDate = new DateOnly(2028, 3, 1),
        };

        var stale = program.Revise(0, "Stale", valid, MemberId, "Lead", Now);
        var reversed = program.Revise(1, "Invalid", invalid, MemberId, "Lead", Now);
        var reversedStages = program.Revise(1, "Invalid", reversedJourney, MemberId, "Lead", Now);

        Assert.Equal(CommandFailureCode.VersionConflict,
            Assert.IsType<CommandFailure>(stale).Code);
        Assert.Equal(1, stale.Version?.CurrentRevision);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(reversed).Code);
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(reversedStages).Code);
        Assert.Single(new AggregateScenario<ComplianceProgram>(program).PendingEvents);
    }

    sealed class RequestContext<TRequest>(TRequest request, ClaimsPrincipal actor,
        Uuid? requestId = null)
        : IRequestContext<TRequest>
    {
        public TRequest Request { get; } = request;
        public ClaimsPrincipal Actor => actor;
        public Uuid ExecutionId { get; } = Uuid.CreateVersion4();
        public Uuid RequestId { get; } = requestId ?? Uuid.CreateVersion4();
        public Uuid CorrelationId { get; } = Uuid.CreateVersion4();
        public Uuid? CausationId => null;
        public Uuid CauseId => RequestId;
        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
        public RequestInvocation Invocation { get; } = new DirectInvocation();
    }
}

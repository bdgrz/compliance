using System.Security.Claims;
using System.Text.Json;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        await using var provider = Services();
        var userId = Uuid.CreateVersion4();
        var requestId = Uuid.CreateVersion4();
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

        // Act
        await Create(provider, actor, requestId, "SOC 2")
            .ExpectSuccess(new ProgramRegistration(requestId));
        var events = await ProgramEvents(provider, requestId);

        // Assert
        var created = Assert.IsType<ProgramCreated>(Assert.Single(events));
        Assert.Equal(RbacIds.Member(TenantId, userId), created.ActorMemberId);
        Assert.Equal("session@example.com", created.ActorDisplay);
    }

    [Fact]
    public async Task ShouldReturnOriginalRegistrationGivenIdenticalCreateRetry()
    {
        // Arrange
        await using var provider = Services();
        var requestId = Uuid.CreateVersion4();
        var actor = ProgramManagementServices.Actor(Uuid.CreateVersion4());
        var registration = new ProgramRegistration(requestId);

        // Act
        await Create(provider, actor, requestId, "SOC 2").ExpectSuccess(registration);
        await Create(provider, actor, requestId, " SOC 2 ").ExpectSuccess(registration);
        await Create(provider, actor, requestId, "Different")
            .ExpectFailure(RequestErrorKind.Conflict);
        var events = await ProgramEvents(provider, requestId);

        // Assert
        Assert.IsType<ProgramCreated>(Assert.Single(events));
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

    static ServiceProvider Services() => ProgramManagementServices.Build(
        new RecordingPermissionAuthorizer(allowed: true),
        portia => portia.AddRequestHandler<CreateProgramHandler>());

    static RequestExpectations<ProgramRegistration> Create(IServiceProvider provider,
        ClaimsPrincipal actor, Uuid requestId, string name) => RequestScenario.For(provider)
        .GivenActor(actor)
        .GivenMetadata(new RequestMetadata(requestId, requestId, null))
        .When(new CreateProgram(TenantId, name,
            new ProgramPlan(null, null, null, null, null, null)))
        .ExpectAuthorized()
        .ExpectHandled();

    static async Task<List<DomainEvent>> ProgramEvents(IServiceProvider provider, Uuid programId)
    {
        var events = new List<DomainEvent>();
        await foreach (var record in provider.GetRequiredService<IEventStore>().ReadAsync(
                           new ComplianceProgram(TenantId, programId).Stream, 0,
                           CancellationToken.None))
            events.Add(record.Event);
        return events;
    }
}

using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ProfessionalDutyGovernanceCompositionTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.UtcNow.AddMinutes(-1);
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Staff = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldDenyProfessionalDutyMutationGivenOnlyPlatformOperatorMetadataAuthority()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var http = new RequestDispatchContext(Actor,
            new HttpInvocation("PUT", "/synthetic/duty", "/synthetic/duty", "synthetic"));
        var staffRegistered = await bus.SendAsync(new RegisterFirmStaff(Staff, User, "attest",
            "Synthetic staff source", 0), Actor);
        Assert.True(staffRegistered.IsSuccess, staffRegistered.Error?.Message);
        var noDesignation = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(),
            1, 1, 0, new string('a', 64), "Synthetic ratification source"), http, CancellationToken.None);

        // Act
        var designated = await bus.DispatchAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), "synthetic invalid assertion", 0), http, CancellationToken.None);
        var deniedTransport = await bus.SendAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), "synthetic invalid assertion", 0), Actor);
        var rules = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic authored rules");
        var draft = await bus.SendAsync(new ReviseIndependenceRules(0, rules), Actor);
        var ratification = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(),
            1, 1, 0, IndependenceSourceDigest.RuleContent(rules), "Synthetic ratification evidence"),
            http, CancellationToken.None);
        var versions = await bus.SendAsync(new GetIndependenceRuleVersions(), Actor);
        var duties = await bus.DispatchAsync(new GetFirmProfessionalDutyCatalog(), http, CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, noDesignation.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, designated.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, deniedTransport.Error?.Kind);
        Assert.True(draft.IsSuccess, draft.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, ratification.Error?.Kind);
        var version = Assert.Single(versions.Value!);
        Assert.False(version.IsRatified);
        Assert.Empty(duties.Value!.Designations);
    }

    [Fact]
    public async Task ShouldRetainAndRevokeCurrentDutyGivenSignedIssuerAssertions()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var duties = scope.ServiceProvider.GetRequiredService<ProfessionalDutyAuthorityReader>();
        var http = new RequestDispatchContext(Actor,
            new HttpInvocation("PUT", "/synthetic/duty", "/synthetic/duty", "synthetic"));
        var staffRegistered = await bus.SendAsync(new RegisterFirmStaff(Staff, User, "attest",
            "Synthetic staff source", 0), Actor);
        Assert.True(staffRegistered.IsSuccess, staffRegistered.Error?.Message);
        var designationId = Uuid.CreateVersion4();
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).AddMinutes(-1);
        var active = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, issuedAt, issuedAt.AddMinutes(30));
        var directDenied = await bus.SendAsync(new RecordFirmProfessionalDutyDesignation(designationId,
            active.Token, 0), Actor);
        var mcpDenied = await bus.DispatchAsync(new RecordFirmProfessionalDutyDesignation(designationId,
            active.Token, 0), new RequestDispatchContext(Actor, new McpInvocation("synthetic.duty.record")),
            CancellationToken.None);

        // Act
        var recorded = await bus.DispatchAsync(new RecordFirmProfessionalDutyDesignation(designationId,
            active.Token, 0), http, CancellationToken.None);
        var catalogAfterRecord = await bus.DispatchAsync(new GetFirmProfessionalDutyCatalog(), http,
            CancellationToken.None);
        var currentBefore = await duties.ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null,
            CancellationToken.None);
        var revokedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var revokedAssertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "revoked", active.Evidence.AssertionId,
            revokedAt, revokedAt.AddMinutes(30));
        var revoked = await bus.DispatchAsync(new RevokeFirmProfessionalDutyDesignation(designationId,
            revokedAssertion.Token, 1), new RequestDispatchContext(Actor,
                new HttpInvocation("POST", "/synthetic/duty/revocation", "/synthetic/duty/revocation", "synthetic")),
            CancellationToken.None);
        var currentAfter = await duties.ReadCurrentAsync(User, FirmProfessionalDuty.RuleRatifier, null,
            CancellationToken.None);
        var catalogAfterRevoke = await bus.DispatchAsync(new GetFirmProfessionalDutyCatalog(), http,
            CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, directDenied.Error?.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, mcpDenied.Error?.Kind);
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        Assert.Equal(active.Evidence.AssertionId, Assert.Single(catalogAfterRecord.Value!.Designations).AssertionId);
        Assert.NotNull(currentBefore);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Null(currentAfter);
        var retained = Assert.Single(catalogAfterRevoke.Value!.Designations);
        Assert.False(retained.IsActive);
        Assert.Equal(revokedAssertion.Evidence.AssertionId, retained.AssertionId);
        Assert.Equal("revoked", retained.AuthorityStatus);
    }

    [Fact]
    public async Task ShouldNotExposeRatifiedRulesGivenExactRatifierDesignationIsRevoked()
    {
        // Arrange
        await using var provider = Compose();
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var operatorActor = ActorReference.ForPlatformOperator(User, "Synthetic operator");
        var seededStaff = await executor.ExecuteAsync(new FirmStaffDirectory(), directory =>
            AggregateOutcome.CommitOnSuccess(directory.Register(Uuid.CreateVersion4(), Staff, User, "attest",
                "Synthetic staff source", directory.Sequence, operatorActor, Now)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededStaff.IsSuccess, seededStaff.Error?.Message);
        var staff = (await reader.HydrateAsync(new FirmStaffDirectory(), CancellationToken.None)).Get(Staff)!;
        var designationId = Uuid.CreateVersion4();
        var assertion = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 1, "active", null, Now, Now.AddHours(1));
        var seededDuty = await executor.ExecuteAsync(new FirmProfessionalDutyCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Designate(Uuid.CreateVersion4(), assertion.Evidence,
                assertion.Token, staff, catalog.Sequence, operatorActor, Now)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededDuty.IsSuccess, seededDuty.Error?.Message);
        var content = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic rules");
        var seededRules = await executor.ExecuteAsync(new IndependenceRuleCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.ReviseRules(Uuid.CreateVersion4(), catalog.Sequence,
                content, operatorActor, Now)), new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededRules.IsSuccess, seededRules.Error?.Message);
        var seededRatification = await executor.ExecuteAsync(new IndependenceRuleRatificationCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Ratify(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1, 1,
                catalog.Sequence, IndependenceSourceDigest.RuleContent(content), "Synthetic ratification evidence",
                staff, seededDuty.Value!, ActorReference.ForFirmStaff(User, "Synthetic ratifier"), Now)),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);
        Assert.True(seededRatification.IsSuccess, seededRatification.Error?.Message);
        var rulesReader = scope.ServiceProvider.GetRequiredService<CurrentRatifiedIndependenceRulesReader>();
        Assert.NotNull(await rulesReader.ReadActiveAsync(CancellationToken.None));

        // Act
        var revocation = ProfessionalDutyAssertionTestIssuer.Issue(designationId, User,
            FirmProfessionalDuty.RuleRatifier, null, 2, "revoked", assertion.Evidence.AssertionId,
            Now.AddMinutes(1), Now.AddHours(1));
        var revoked = await executor.ExecuteAsync(new FirmProfessionalDutyCatalog(), catalog =>
            AggregateOutcome.CommitOnSuccess(catalog.Revoke(Uuid.CreateVersion4(), revocation.Evidence,
                revocation.Token, catalog.Sequence, operatorActor, Now.AddMinutes(1))),
            new RequestDispatchContext(RequestActor.System), CancellationToken.None);

        // Assert
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.Null(await rulesReader.ReadActiveAsync(CancellationToken.None));
    }

    static ServiceProvider Compose()
    {
        var services = new ServiceCollection();
        var configurationValues = new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        };
        ProfessionalDutyAssertionTestIssuer.AddConfiguration(configurationValues);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPlatformOperatorAccess>(new OperatorAccess(true));
        services.AddSingleton<IPlatformUserDirectoryReader>(new UserDirectory());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class OperatorAccess(bool allowed) : IPlatformOperatorAccess
    {
        public ValueTask<bool> IsOperatorAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(allowed);
    }

    sealed class UserDirectory : IPlatformUserDirectoryReader
    {
        public ValueTask<bool> ExistsAsync(Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(userId == User);
    }
}

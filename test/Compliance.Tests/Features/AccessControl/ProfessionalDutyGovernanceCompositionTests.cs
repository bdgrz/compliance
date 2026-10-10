using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class ProfessionalDutyGovernanceCompositionTests
{
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Staff = Uuid.CreateVersion4();
    static readonly ClaimsPrincipal Actor = new(new ClaimsIdentity([
        new Claim("iss", "bdgrz"), new Claim("sub", User.ToString())], "BdgrzSession"));

    [Fact]
    public async Task ShouldKeepDraftAuthorshipSeparateFromPersonalCurrentRuleRatificationGivenDutySource()
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
            Uuid.CreateVersion4(), Staff, FirmProfessionalDuty.RuleRatifier, null,
            "Synthetic duty evidence", 0), http, CancellationToken.None);
        var deniedTransport = await bus.SendAsync(new RecordFirmProfessionalDutyDesignation(
            Uuid.CreateVersion4(), Staff, FirmProfessionalDuty.RuleRatifier, null,
            "Synthetic duty evidence", 0), Actor);
        var rules = new IndependenceRuleContent(12,
            [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
            "Synthetic authored rules");
        var draft = await bus.SendAsync(new ReviseIndependenceRules(0, rules), Actor);
        var ratification = await bus.DispatchAsync(new RatifyIndependenceRuleVersion(Uuid.CreateVersion4(),
            1, 1, 0, IndependenceSourceDigest.RuleContent(rules), "Synthetic ratification evidence"),
            http, CancellationToken.None);
        var versions = await bus.SendAsync(new GetIndependenceRuleVersions(), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, noDesignation.Error?.Kind);
        Assert.True(designated.IsSuccess, designated.Error?.Message);
        Assert.Equal(RequestErrorKind.Forbidden, deniedTransport.Error?.Kind);
        Assert.True(draft.IsSuccess, draft.Error?.Message);
        Assert.True(ratification.IsSuccess, ratification.Error?.Message);
        var version = Assert.Single(versions.Value!);
        Assert.True(version.IsRatified);
        Assert.Equal(User.ToString(), ratification.Value!.Actor.Id);
        Assert.Equal(designated.Value.DesignationId, ratification.Value.DutyDesignationId);
    }

    static ServiceProvider Compose()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build();
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

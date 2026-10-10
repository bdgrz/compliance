using System.Security.Claims;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class IndependenceAssignmentBoundaryTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid ClientUser = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference ClientActor = ActorReference.ForMember(
        RbacIds.Member(Tenant, ClientUser), "Synthetic client administrator");

    [Theory]
    [InlineData("http")]
    [InlineData("mcp")]
    public async Task ShouldMapPersonPracticeConflictToPortiaConflictGivenAssignmentBoundary(string transport)
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build();
        services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IPermissionAuthorizer>(new RecordingPermissionAuthorizer(true));
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new FixedMembershipDirectory(true));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var actor = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("iss", "bdgrz"), new Claim("sub", ClientUser.ToString())], "BdgrzSession"));
        var executor = scope.ServiceProvider.GetRequiredService<IAggregateExecutor>();
        FirmStaffMemberView? conflictingStaff = null;
        Uuid? priorEngagementId = null;
        var accepted = await executor.ExecuteAsync(new IndependenceLedger(Tenant), ledger =>
        {
            var (_, proof, rules) = AcceptanceFixture(ledger, "attest");
            conflictingStaff = proof.CurrentStaff.Single();
            priorEngagementId = proof.EngagementId;
            return AggregateOutcome.CommitOnSuccess(ledger.AcceptEngagement(Uuid.CreateVersion4(), 2,
                proof, rules, Now));
        }, new RequestDispatchContext(actor), CancellationToken.None);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);

        var operatorActor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        // The retained synthetic acceptance and current directory intentionally disagree on practice.
        // This exercises the defensive history wall without asserting any firm rule-set values.
        var newLead = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), "advisory",
            "Synthetic current directory source", true, 1, operatorActor, Now);
        var directory = await executor.ExecuteAsync(new FirmStaffDirectory(), current =>
        {
            var conflictEntry = current.Register(Uuid.CreateVersion4(), conflictingStaff!.StaffMemberId,
                conflictingStaff.UserId, "advisory", "Synthetic current directory source", current.Sequence,
                operatorActor, Now);
            if (!conflictEntry.IsSuccess)
                return AggregateOutcome.CommitOnSuccess(conflictEntry);
            return AggregateOutcome.CommitOnSuccess(current.Register(Uuid.CreateVersion4(), newLead.StaffMemberId,
                newLead.UserId, "advisory", "Synthetic current directory source", current.Sequence,
                operatorActor, Now));
        }, new RequestDispatchContext(actor), CancellationToken.None);
        Assert.True(directory.IsSuccess, directory.Error?.Message);

        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var engagementId = Uuid.CreateVersion4();
        var engagementPath = $"/api/v1/tenants/{Tenant}/service-engagements/{engagementId}";
        var created = await bus.DispatchAsync(new CreateServiceEngagement(Tenant, engagementId, 3,
            new ServiceEngagementDraftContent("advisory", "Synthetic scope", new DateOnly(2026, 1, 1), null,
                newLead.StaffMemberId)), new RequestDispatchContext(actor,
            new HttpInvocation("PUT", engagementPath, engagementPath, "synthetic")), CancellationToken.None);
        Assert.True(created.IsSuccess, created.Error?.Message);

        RequestInvocation invocation = transport == "http"
            ? new HttpInvocation("PUT", $"{engagementPath}/staff-proposals/{conflictingStaff!.StaffMemberId}",
                $"{engagementPath}/staff-proposals/{conflictingStaff.StaffMemberId}", "synthetic")
            : new McpInvocation("bdgrz.service-engagement.staff.propose");

        // Act
        var result = await bus.DispatchAsync(new ProposeServiceEngagementStaff(Tenant, engagementId,
            conflictingStaff!.StaffMemberId, 4), new RequestDispatchContext(actor, invocation),
            CancellationToken.None);
        var retained = await scope.ServiceProvider.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new IndependenceLedger(Tenant), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Contains(IndependenceCompartments.PersonExclusivityRuleId, result.Error!.Message,
            StringComparison.Ordinal);
        Assert.Contains(priorEngagementId!.Value.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(conflictingStaff.StaffMemberId.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Contains(conflictingStaff.UserId.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(4, retained.Sequence);
        Assert.Equal("draft", retained.Engagement(engagementId)!.Status);
        Assert.Equal(newLead.StaffMemberId, Assert.Single(retained.Engagement(engagementId)!.Staff).StaffMemberId);
    }

    static (IndependenceLedger Ledger, VerifiedEngagementAcceptance Proof,
        IndependenceRuleVersionView Rules) AcceptanceFixture(IndependenceLedger ledger, string practice)
    {
        var operatorActor = ActorReference.ForPlatformOperator(Uuid.CreateVersion4(), "Synthetic operator");
        var staff = new FirmStaffMemberView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), practice,
            "Synthetic historical directory source", true, 1, operatorActor, Now);
        var engagement = Uuid.CreateVersion4();
        Assert.True(ledger.CreateEngagement(Uuid.CreateVersion4(), engagement, 0,
            new ServiceEngagementDraftContent(practice, "Synthetic scope", new DateOnly(2026, 1, 1), null,
                staff.StaffMemberId), staff, ClientActor, Now).IsSuccess);
        var acknowledgementId = Uuid.CreateVersion4();
        Assert.True(ledger.AcknowledgeManagement(Uuid.CreateVersion4(),
            new AcknowledgeEngagementManagement(Tenant, engagement, acknowledgementId, 1, 1, [],
                "Synthetic responsibility acknowledgement"), ClientUser, ClientActor, Now).IsSuccess);
        var partner = staff with { StaffMemberId = Uuid.CreateVersion4(), UserId = Uuid.CreateVersion4() };
        var proof = new VerifiedEngagementAcceptance(Tenant, engagement, 1, Uuid.CreateVersion4(),
            partner.StaffMemberId, partner.UserId, "Synthetic fixture only", null, acknowledgementId,
            null, null, [staff], partner, 1, Now);
        var rules = new IndependenceRuleVersionView(1,
            new IndependenceRuleContent(12,
                [new IndependenceServiceRuleContent("readiness", "conditionally_compatible", "impairing")],
                "Synthetic fixture only"), operatorActor, Now, true);
        return (ledger, proof, rules);
    }
}

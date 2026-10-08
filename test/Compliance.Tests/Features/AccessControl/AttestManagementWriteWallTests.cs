using System.Security.Claims;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AttestManagementWriteWallTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid User = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    static readonly ClaimsPrincipal Actor = ProgramManagementServices.Actor(User);

    [Theory]
    [InlineData("client_personnel", "control", false)]
    [InlineData("guest", "control", false)]
    [InlineData("client_personnel", "policy", false)]
    [InlineData("client_personnel", "evidence", false)]
    [InlineData("client_personnel", "attest", false)]
    [InlineData("client_personnel", "review", false)]
    [InlineData("client_personnel", "control", true)]
    public async Task ShouldDenyManagementWriteGivenCanonicalActualAttestHistoryAndCurrentClientGrant(
        string affiliation, string operation, bool revoked)
    {
        // Arrange
        await using var provider = Compose(affiliation);
        await SeedProgramAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, revoked);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        var control = Uuid.CreateVersion4();
        var occurrence = Uuid.CreateVersion4();

        // Act
        var error = operation switch
        {
            "control" => (await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1",
                Content), Actor)).Error,
            "policy" => (await bus.SendAsync(new CreatePolicyDraft(Tenant, Program, "POL-1",
                new PolicyContent("Policy", "Purpose", "core_security", null, 12, "Body", null, null, null)), Actor)).Error,
            "evidence" => (await bus.SendAsync(new OpenEvidenceRequest(Tenant, Program, "Evidence", "Instructions",
                RbacIds.Member(Tenant, User), new DateOnly(2027, 1, 1)), Actor)).Error,
            "attest" => (await bus.SendAsync(new AttestControlOccurrence(Tenant, Program, control, occurrence,
                1, "performed", Now, null, null, null, null, []), Actor)).Error,
            "review" => (await bus.SendAsync(new ReviewControlOccurrence(Tenant, Program, control, occurrence,
                1, Uuid.CreateVersion4(), "accept", "Review rationale"), Actor)).Error,
            _ => throw new InvalidOperationException()
        };

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, error?.Kind);
        Assert.Contains("Attest", error!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("client_personnel")]
    [InlineData("guest")]
    public async Task ShouldAllowSharedControlReadGivenHistoricalAttestIdentityAndOrdinaryGrant(string affiliation)
    {
        // Arrange
        await using var provider = Compose(affiliation);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();
        await SeedProgramAsync(provider);
        var control = await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);
        Assert.True(control.IsSuccess, control.Error?.Message);
        var projection = scope.ServiceProvider.GetRequiredService<IControlDraftDirectoryProjection>();
        await using (var batch = await projection.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ControlDraftDirectoryV2", EventStreamPattern.ForPattern(Tenant.ToString(),
                "controls")), ProjectionCheckpoint.Start)))
        {
            await projection.ApplyAsync(new ControlDraftCreated(Tenant, Program, control.Value!.ControlId,
                Uuid.CreateVersion4(), "AC-1", Content, RbacIds.Member(Tenant, User), "Client author", Now));
            await batch.CommitAsync(ProjectionCheckpoint.Start);
        }
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User, true);

        // Act
        var read = await bus.SendAsync(new GetControlDraft(Tenant, Program, control.Value!.ControlId), Actor);

        // Assert
        Assert.True(read.IsSuccess, read.Error?.Message);
        Assert.Equal("Access control", read.Value!.Content.Title);
    }

    [Theory]
    [InlineData(false, "attest")]
    [InlineData(true, "advisory")]
    public async Task ShouldAllowManagementWriteGivenNoActualAttestHistoryForThisClient(bool sameTenant,
        string practice)
    {
        // Arrange
        await using var provider = Compose("client_personnel");
        await AttestAssignmentHistoryFixture.SeedAsync(provider, sameTenant ? Tenant : Uuid.CreateVersion4(),
            User, true, practice);
        await using var scope = provider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IRequestBus>();

        // Act
        await SeedProgramAsync(provider);
        var control = await bus.SendAsync(new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.True(control.IsSuccess, control.Error?.Message);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task ShouldHideHistoricalAttestFactsGivenNoCurrentOwningMembership(bool member,
        bool suspended, bool deprovisioned)
    {
        // Arrange
        await using var provider = Compose("client_personnel", member: member, suspended: suspended,
            deprovisioned: deprovisioned);
        await SeedProgramAsync(provider);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, Tenant, User);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error?.Kind);
        Assert.DoesNotContain("Attest", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldStillDenyMutationGivenNoOrdinaryProgramGrantAndNoAttestHistory()
    {
        // Arrange
        await using var provider = Compose("client_personnel", permitted: false);
        await SeedProgramAsync(provider);
        await using var scope = provider.CreateAsyncScope();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().SendAsync(
            new CreateControlDraft(Tenant, Program, "AC-1", Content), Actor);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.DoesNotContain("Attest", result.Error!.Message, StringComparison.Ordinal);
    }

    static Task SeedProgramAsync(ServiceProvider provider) => ProgramManagementServices.SeedAsync(provider,
        new ComplianceProgram(Tenant, Program), program =>
        {
            Assert.Null(program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                RbacIds.Member(Tenant, User), "Client author", Now));
            return Result.Success;
        });

    static ControlDraftContent Content => new("Access control", "Protect systems", "Review access",
        "Owners review access", ["Access review record"]);

    static ServiceProvider Compose(string affiliation, bool member = true, bool suspended = false,
        bool deprovisioned = false, bool permitted = true)
    {
        var services = new ServiceCollection();
        services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance",
        }).Build(), developerAuthentication: true);
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton<ITenantActivity>(new ActiveTenant());
        services.AddSingleton<ITenantMembershipDirectoryReader>(new Memberships(affiliation, member, suspended, deprovisioned));
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(new PermissionBackedAccessGrantPermissionAuthorizer(
            new RecordingPermissionAuthorizer(permitted)));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class Memberships(string affiliation, bool member, bool suspended, bool deprovisioned) : ITenantMembershipDirectoryReader
    {
        public ValueTask<TenantMembershipView?> GetAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult<TenantMembershipView?>(member && tenantId == Tenant.ToString() && userId == User
                ? new TenantMembershipView(userId, Tenant, affiliation, IsSuspended: suspended,
                    IsDeprovisioned: deprovisioned) : null);
        public ValueTask<bool> IsMemberAsync(string tenantId, Uuid userId, CancellationToken ct = default) =>
            ValueTask.FromResult(member && tenantId == Tenant.ToString() && userId == User);
        public ValueTask<Page<TenantMembershipView>> ListAsync(Uuid tenantId, int limit, string? cursor,
            CancellationToken ct = default) => ValueTask.FromResult(new Page<TenantMembershipView>([], null));
    }
}

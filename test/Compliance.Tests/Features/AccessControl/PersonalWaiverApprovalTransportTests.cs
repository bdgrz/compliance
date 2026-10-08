using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class PersonalWaiverApprovalTransportTests
{
    [Theory]
    [InlineData("direct")]
    [InlineData("mcp")]
    [InlineData("http")]
    public async Task ShouldRequirePersonalApprovalGivenValidIndependentWaiver(string transport)
    {
        // Arrange
        await using var fixture = await WaiverManagementIndependenceTests.Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var before = await fixture.WaiverEventsAsync();
        await using var scope = fixture.Provider.CreateAsyncScope();
        RequestInvocation invocation = transport == "http" ? new HttpInvocation("POST", "/synthetic/waiver/approve", "/synthetic/waiver/approve", "synthetic") :
            transport == "mcp" ? new McpInvocation("synthetic.waiver.approve") : new DirectInvocation();

        // Act
        var result = await scope.ServiceProvider.GetRequiredService<IRequestBus>().DispatchAsync(
            new ApproveSeparationOfDutiesWaiver(fixture.Tenant, recorded.Value.WaiverId),
            new RequestDispatchContext(ProgramManagementServices.Actor(fixture.Approver), invocation), CancellationToken.None);

        // Assert
        if (transport == "http")
        {
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.True(result.Value.Active);
            Assert.Equal(RbacIds.Member(fixture.Tenant, fixture.Approver).ToString(), result.Value.Approver!.Id);
            Assert.Equal(fixture.Scope, result.Value.Scope);
            Assert.Equal(before + 1, await fixture.WaiverEventsAsync());
        }
        else
        {
            Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
            Assert.Contains("personal HTTP", result.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(before, await fixture.WaiverEventsAsync());
        }
    }
    [Theory]
    [InlineData("requester", RequestErrorKind.Forbidden, "A waiver must be approved by a different Org Admin who is not its beneficiary.")]
    [InlineData("beneficiary", RequestErrorKind.Forbidden, "A waiver must be approved by a different Org Admin who is not its beneficiary.")]
    [InlineData("grant", RequestErrorKind.Forbidden, "The actor may not manage this tenant's waivers.")]
    [InlineData("missing", RequestErrorKind.NotFound, "The separation-of-duties waiver was not found.")]
    [InlineData("expired", RequestErrorKind.Conflict, "The separation-of-duties waiver is outside its approval window.")]
    public async Task ShouldPreserveApprovalSourceRefusalGivenNativeHttp(string fault, RequestErrorKind kind, string message)
    {
        // Arrange
        await using var fixture = await WaiverManagementIndependenceTests.Fixture.CreateAsync();
        var recorded = await fixture.RecordAsync();
        Assert.True(recorded.IsSuccess, recorded.Error?.Message);
        var waiverId = recorded.Value.WaiverId;
        if (fault == "missing")
            waiverId = Uuid.CreateVersion4();
        if (fault == "expired")
        {
            waiverId = Uuid.CreateVersion4();
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(fixture.Provider, new SeparationOfDutiesWaiver(fixture.Tenant, waiverId), waiver =>
            {
                Assert.Null(waiver.Record(fixture.Scope, RbacIds.Member(fixture.Tenant, fixture.Beneficiary),
                    RbacIds.Member(fixture.Tenant, fixture.Requester), "Requester", "Historical exception", now.AddDays(-2), now.AddDays(-1)));
                return Result.Success;
            });
        }
        if (fault == "grant")
            fixture.Permissions.Allowed = false;
        var before = await fixture.WaiverEventsAsync();
        var user = fault == "requester" ? fixture.Requester : fault == "beneficiary" ? fixture.Beneficiary : fixture.Approver;

        // Act
        var result = await fixture.SendAsync(new ApproveSeparationOfDutiesWaiver(fixture.Tenant, waiverId), user);

        // Assert
        Assert.Equal(kind, result.Error?.Kind);
        Assert.Equal(message, result.Error?.Message);
        Assert.Equal(before, await fixture.WaiverEventsAsync());
    }

}

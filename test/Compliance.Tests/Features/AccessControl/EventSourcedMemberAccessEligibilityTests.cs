using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class EventSourcedMemberAccessEligibilityTests
{
    [Fact]
    public async Task ShouldDenyGivenSuspensionSourceEventBeforeMembershipProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scope.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var source = new EventSourcedMemberAccessEligibility(reader);
        var projected = new FixedMembershipDirectory(true);
        var member = new Member(tenantId, userId);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        Assert.True(await source.IsEligibleAsync(tenantId, userId));

        // Act
        member = await reader.HydrateAsync(new Member(tenantId, userId));
        Assert.True(member.Suspend(RbacIds.Member(tenantId, Uuid.CreateVersion4()),
            "Administrator", DateTimeOffset.UtcNow, "Access review.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));

        // Assert
        Assert.False((await projected.GetAsync(tenantId.ToString(), userId))!.IsSuspended);
        Assert.False(await source.IsEligibleAsync(tenantId, userId));
    }
}

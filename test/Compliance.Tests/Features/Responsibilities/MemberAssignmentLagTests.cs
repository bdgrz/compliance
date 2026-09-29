using System.Security.Claims;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class MemberAssignmentLagTests
{
    [Fact]
    public async Task ShouldRejectNewAssignmentGivenSuspensionHasNotReachedMembershipProjection()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var administratorId = Uuid.CreateVersion4();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scopeServices = provider.CreateAsyncScope();
        var reader = scopeServices.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scopeServices.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(tenantId, userId);
        Assert.True(member.Register().IsSuccess);
        Assert.True(member.Suspend(RbacIds.Member(tenantId, administratorId),
            "Administrator", now, "Access review failed.").IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var boundary = new SystemBoundary(tenantId, boundaryId);
        Assert.True(boundary.Create(Uuid.CreateVersion4(), versionId,
            new BoundaryContent("Member work", "readiness", ["security"], []),
            RbacIds.Member(tenantId, administratorId), "Administrator", now).IsSuccess);
        await writer.SaveAsync(boundary, new RequestDispatchContext(RequestActor.System));
        var membershipProjection = new FixedMembershipDirectory(true);
        Assert.True((await reader.HydrateAsync(new Member(tenantId, userId))).IsSuspended);
        Assert.False((await membershipProjection.GetAsync(tenantId.ToString(), userId))!.IsSuspended);
        var handler = new AssignResponsibilityHandler(
            scopeServices.ServiceProvider.GetRequiredService<IAggregateExecutor>(), reader,
            membershipProjection, new AllowScope(), TimeProvider.System);
        var request = new AssignResponsibility(tenantId, userId,
            ResponsibilityType.ControlOwner, "boundary", boundaryId, versionId, 1,
            now, null, []);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", administratorId.ToString())],
            "BdgrzSession"));

        // Act
        var result = await handler.HandleAsync(
            new RequestContext<AssignResponsibility>(request, actor), CancellationToken.None);
        var previewRequest = new PreviewResponsibilityConflicts(tenantId, userId,
            "control_owner", "boundary", boundaryId, versionId, 1, now, null);
        var preview = new PreviewResponsibilityConflictsHandler(new EmptyResponsibilitySets(),
            membershipProjection, new AllowScope(), reader);
        var previewResult = await preview.HandleAsync(
            new RequestContext<PreviewResponsibilityConflicts>(previewRequest, actor),
            CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        Assert.False(previewResult.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, previewResult.Error.Kind);
        var unchanged = await reader.HydrateAsync(new SystemBoundary(tenantId, boundaryId));
        Assert.Empty(unchanged.GetResponsibilitySet(request.Scope).ReadAssignments());
    }

    [Fact]
    public async Task ShouldRejectAssignmentGivenSuspensionCommitsDuringWaiverHydration()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var administratorId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var administratorMemberId = RbacIds.Member(tenantId, administratorId);
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var waiverId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var responsibilityScope = new ResponsibilityScope("boundary", boundaryId, versionId, 1);
        var services = new ServiceCollection();
        services.AddSingleton<IEventStore>(new InMemoryEventStore());
        services.AddPortia();
        await using var provider = services.BuildServiceProvider();
        await using var scopeServices = provider.CreateAsyncScope();
        var source = scopeServices.ServiceProvider.GetRequiredService<IAggregateReader>();
        var writer = scopeServices.ServiceProvider.GetRequiredService<IAggregateWriter>();
        var member = new Member(tenantId, userId);
        Assert.True(member.Register().IsSuccess);
        await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System));
        var boundary = new SystemBoundary(tenantId, boundaryId);
        Assert.True(boundary.Create(Uuid.CreateVersion4(), versionId,
            new BoundaryContent("Member work", "readiness", ["security"], []),
            administratorMemberId, "Administrator", now).IsSuccess);
        Assert.Null(boundary.AssignResponsibility(responsibilityScope, Uuid.CreateVersion4(),
            memberId, ResponsibilityType.ControlOwner, administratorMemberId,
            "Administrator", now.AddMinutes(-2), now.AddMinutes(-2), null, []));
        await writer.SaveAsync(boundary, new RequestDispatchContext(RequestActor.System));
        var waiver = new SeparationOfDutiesWaiver(tenantId, waiverId);
        var waiverScope = new SeparationOfDutiesWaiverScope("boundary", boundaryId,
            versionId, 1, SeparationOfDutiesActions.Review);
        Assert.Null(waiver.Record(waiverScope, memberId, administratorMemberId,
            "Administrator", "Small team exception", now.AddDays(-2), now.AddDays(2)));
        Assert.Null(waiver.Approve(RbacIds.Member(tenantId, Uuid.CreateVersion4()),
            "Second administrator", now.AddDays(-1)));
        await writer.SaveAsync(waiver, new RequestDispatchContext(RequestActor.System));
        var interleavingReader = new SuspendingWaiverReader(source, writer, tenantId, userId,
            administratorMemberId, now);
        var handler = new AssignResponsibilityHandler(
            scopeServices.ServiceProvider.GetRequiredService<IAggregateExecutor>(),
            interleavingReader, new FixedMembershipDirectory(true), new AllowScope(),
            TimeProvider.System);
        var request = new AssignResponsibility(tenantId, userId,
            ResponsibilityType.AssignedReviewer, "boundary", boundaryId, versionId, 1,
            now, null, [waiverId]);
        var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", "bdgrz"), new Claim("sub", administratorId.ToString())],
            "BdgrzSession"));

        // Act
        var result = await handler.HandleAsync(
            new RequestContext<AssignResponsibility>(request, actor), CancellationToken.None);

        // Assert
        Assert.True(interleavingReader.SuspensionCommitted);
        Assert.True((await source.HydrateAsync(new Member(tenantId, userId))).IsSuspended);
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error.Kind);
        var unchanged = await source.HydrateAsync(new SystemBoundary(tenantId, boundaryId));
        Assert.Single(unchanged.GetResponsibilitySet(responsibilityScope).ReadAssignments());
    }

    sealed class SuspendingWaiverReader(IAggregateReader inner, IAggregateWriter writer,
        Uuid tenantId, Uuid userId, Uuid actorMemberId, DateTimeOffset now) : IAggregateReader
    {
        public bool SuspensionCommitted { get; private set; }

        public async ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate
        {
            var hydrated = await inner.HydrateAsync(aggregate, ct);
            if (hydrated is SeparationOfDutiesWaiver && !SuspensionCommitted)
            {
                var member = await inner.HydrateAsync(new Member(tenantId, userId), ct);
                Assert.True(member.Suspend(actorMemberId, "Administrator", now,
                    "Access review failed.").IsSuccess);
                await writer.SaveAsync(member, new RequestDispatchContext(RequestActor.System), ct);
                SuspensionCommitted = true;
            }
            return hydrated;
        }
    }

    sealed class AllowScope : IResponsibilityScopeValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ResponsibilityScope scope,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);
    }

    sealed class EmptyResponsibilitySets : IResponsibilitySetDirectory
    {
        public ValueTask<ResponsibilitySetView?> GetAsync(Uuid tenantId, ResponsibilityScope scope,
            CancellationToken ct = default) => ValueTask.FromResult<ResponsibilitySetView?>(null);
    }
}

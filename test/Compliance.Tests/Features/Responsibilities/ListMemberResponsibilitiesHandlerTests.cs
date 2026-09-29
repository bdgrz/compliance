using System.Globalization;
using System.Security.Claims;
using Bdgrz.Compliance.Features.Responsibilities;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Responsibilities;

public sealed class ListMemberResponsibilitiesHandlerTests
{
    [Fact]
    public async Task ShouldReturnCurrentOpenAssignmentsGivenPastFutureAndRevokedHistory()
    {
        // Arrange
        var tenantId = Uuid.CreateVersion4();
        var userId = Uuid.CreateVersion4();
        var memberId = RbacIds.Member(tenantId, userId);
        var now = DateTimeOffset.Parse("2026-09-29T12:00:00Z", CultureInfo.InvariantCulture);
        var assignments = new[]
        {
            CreateAssignment(tenantId, memberId, now.AddDays(-1), null, null),
            CreateAssignment(tenantId, memberId, now.AddDays(-2), now.AddHours(-1), null),
            CreateAssignment(tenantId, memberId, now.AddHours(1), null, null),
            CreateAssignment(tenantId, memberId, now.AddDays(-1), null, now.AddMinutes(-1)),
        };
        var index = new FixedMemberResponsibilityIndex(assignments);
        var handler = new ListMemberResponsibilitiesHandler(index, new FixedTimeProvider(now));
        var request = new ListMemberResponsibilities(tenantId, userId);
        var context = new RequestContext<ListMemberResponsibilities>(request, new ClaimsPrincipal());

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var current = Assert.Single(result.Value);
        Assert.Equal(assignments[0].AssignmentId, current.AssignmentId);
        Assert.Equal(tenantId, index.LastTenantId);
        Assert.Equal(memberId, index.LastMemberId);
    }

    static ResponsibilityAssignmentView CreateAssignment(Uuid tenantId, Uuid memberId,
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil, DateTimeOffset? revokedAt) =>
        new(tenantId, Uuid.CreateVersion4(), memberId, ResponsibilityType.ControlOwner,
            new ResponsibilityScope("boundary", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1),
            effectiveFrom, Uuid.CreateVersion4(), effectiveFrom, effectiveUntil, revokedAt,
            revokedAt is null ? Uuid.Empty : Uuid.CreateVersion4(), [], "Admin");

    sealed class FixedMemberResponsibilityIndex(IReadOnlyList<ResponsibilityAssignmentView> assignments)
        : IMemberResponsibilityIndex
    {
        public Uuid LastTenantId { get; private set; }
        public Uuid LastMemberId { get; private set; }

        public ValueTask<IReadOnlyList<ResponsibilityAssignmentView>> GetAsync(Uuid tenantId,
            Uuid memberId, CancellationToken ct = default)
        {
            LastTenantId = tenantId;
            LastMemberId = memberId;
            return ValueTask.FromResult(assignments);
        }
    }

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

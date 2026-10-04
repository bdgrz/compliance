using System.Globalization;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessControl;

public sealed class AccessGrantProjectionTests
{
    static readonly Uuid TenantId = Uuid.Parse("11f455d2-fb10-4f28-a157-23e18e706e70", CultureInfo.InvariantCulture);
    static readonly Uuid GrantId = Uuid.Parse("c1b1b99d-2c52-46c6-ad58-55fe90bf53b3", CultureInfo.InvariantCulture);
    static readonly Uuid MemberId = Uuid.Parse("0862062f-97e9-45de-a312-0f884c48180d", CultureInfo.InvariantCulture);
    static readonly Uuid RoleId = Uuid.Parse("5b66f817-415a-44e8-b809-503cf44b1537", CultureInfo.InvariantCulture);
    static readonly DateTimeOffset EffectiveFrom = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRetainIssuedGrantAndAdvanceRevisionGivenNewFact()
    {
        // Arrange
        var state = new AccessGrantProjectionState();
        var issued = new AccessGrantIssued(TenantId, GrantId, Terms());

        // Act
        state.Apply(issued);

        // Assert
        var view = state.ToView(TenantId);
        Assert.Equal(1, view.Revision);
        var grant = Assert.Single(view.Grants);
        Assert.Equal(GrantId, grant.GrantId);
        Assert.Equal(Terms(), grant.Terms);
        Assert.Null(grant.RevokedAt);
    }

    [Fact]
    public void ShouldRetainGrantHistoryAndAdvanceRevisionGivenRevocation()
    {
        // Arrange
        var state = new AccessGrantProjectionState();
        var issued = new AccessGrantIssued(TenantId, GrantId, Terms());
        var revokedAt = EffectiveFrom.AddDays(1);
        state.Apply(issued);

        // Act
        state.Apply(new AccessGrantRevoked(TenantId, GrantId,
            ActorReference.ForMember(MemberId, "Organization Admin"), revokedAt));

        // Assert
        var view = state.ToView(TenantId);
        Assert.Equal(2, view.Revision);
        var grant = Assert.Single(view.Grants);
        Assert.Equal(Terms(), grant.Terms);
        Assert.Equal(ActorReference.ForMember(MemberId, "Organization Admin"), grant.RevokedBy);
        Assert.Equal(revokedAt, grant.RevokedAt);
    }

    [Fact]
    public void ShouldNotAdvanceRevisionGivenExactEventReplay()
    {
        // Arrange
        var state = new AccessGrantProjectionState();
        var issued = new AccessGrantIssued(TenantId, GrantId, Terms());
        state.Apply(issued);

        // Act
        state.Apply(issued);

        // Assert
        Assert.Equal(1, state.ToView(TenantId).Revision);
    }

    [Fact]
    public void ShouldNotReopenGrantGivenIssuedFactReplayedAfterRevocation()
    {
        // Arrange
        var state = new AccessGrantProjectionState();
        var issued = new AccessGrantIssued(TenantId, GrantId, Terms());
        state.Apply(issued);
        state.Apply(new AccessGrantRevoked(TenantId, GrantId,
            ActorReference.ForMember(MemberId, "Organization Admin"), EffectiveFrom.AddDays(1)));

        // Act
        state.Apply(issued);

        // Assert
        var view = state.ToView(TenantId);
        Assert.Equal(2, view.Revision);
        var grant = Assert.Single(view.Grants);
        Assert.NotNull(grant.RevokedAt);
    }

    [Fact]
    public void ShouldRejectAnotherTenantGivenExistingProjectionState()
    {
        // Arrange
        var state = new AccessGrantProjectionState();
        state.Apply(new AccessGrantIssued(TenantId, GrantId, Terms()));

        // Act
        var error = Record.Exception(() => state.Apply(new AccessGrantIssued(
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Terms())));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(1, state.ToView(TenantId).Revision);
        Assert.Single(state.ToView(TenantId).Grants);
    }

    [Fact]
    public void ShouldRoundTripProjectionStateGivenGeneratedJsonContext()
    {
        // Arrange
        var state = new AccessGrantProjectionState();
        var episodeId = Uuid.CreateVersion4();
        state.Apply(new AccessGrantIssued(TenantId, GrantId, Terms(), episodeId));
        state.Apply(new AccessGrantRevoked(TenantId, GrantId,
            ActorReference.ForMember(MemberId, "Organization Admin"), EffectiveFrom.AddDays(1)));
        var json = JsonSerializer.SerializeToUtf8Bytes(state,
            ComplianceCoreJsonContext.Default.AccessGrantProjectionState);

        // Act
        var restored = JsonSerializer.Deserialize(json,
            ComplianceCoreJsonContext.Default.AccessGrantProjectionState);

        // Assert
        Assert.NotNull(restored);
        var view = restored.ToView(TenantId);
        Assert.Equal(2, view.Revision);
        var grant = Assert.Single(view.Grants);
        Assert.Equal(Terms(), grant.Terms);
        Assert.Equal(EffectiveFrom.AddDays(1), grant.RevokedAt);
        Assert.Equal(episodeId, restored.GetMembershipEpisodeId(GrantId));
    }

    [Fact]
    public void ShouldRejectRevocationBeforeIssuanceGivenProjectionReplay()
    {
        // Arrange
        var state = new AccessGrantProjectionState();

        // Act
        var error = Record.Exception(() => state.Apply(
            new AccessGrantRevoked(TenantId, GrantId,
                ActorReference.ForMember(MemberId, "Organization Admin"), EffectiveFrom)));

        // Assert
        Assert.IsType<InvalidOperationException>(error);
        Assert.Equal(0, state.ToView(TenantId).Revision);
        Assert.Empty(state.ToView(TenantId).Grants);
    }

    static AccessGrantTerms Terms() => new(
        new AccessGrantPrincipal(AccessGrantPrincipalKind.Member, MemberId),
        RoleId,
        new AccessGrantScope(AccessGrantScopeKind.Program, Uuid.Parse(
            "e9a858f1-25e1-4594-87d2-814a73fc87ec", CultureInfo.InvariantCulture)),
        new AccessGrantSource("manual", "request-123"),
        ActorReference.ForMember(MemberId, "Organization Admin"),
        EffectiveFrom,
        null);
}

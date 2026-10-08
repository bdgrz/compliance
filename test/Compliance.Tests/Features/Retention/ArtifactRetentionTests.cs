using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Retention;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Retention;

public sealed class ArtifactRetentionTests
{
    [Fact]
    public void ShouldRecordSevenYearsFromExplicitPeriodGivenSourceBoundImportDecision()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "application_import", id);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "application_import", id, new string('a', 64)), 1);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");

        // Act
        var result = retention.RecordBasis(source, 0, new DateOnly(2019, 1, 1), new DateOnly(2019, 12, 31),
            "Period verified against original source", actor, new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 12, 31), retention.Basis!.RetainsThrough);
        Assert.Equal(actor, retention.Basis.RecordedBy);
        Assert.Equal(source.Source, retention.Source);
        Assert.Equal(2, retention.Revision);
        Assert.Equal(2, new AggregateScenario<ArtifactRetention>(retention).PendingEvents.Count);
    }
    [Fact]
    public void ShouldKeepIndefiniteLegalHoldHistoryGivenAttributedPersonalRelease()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "application_import", id);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "application_import", id, new string('a', 64)), 1);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
        var holdId = Uuid.CreateVersion4();
        var now = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        // Act
        var placed = retention.PlaceLegalHold(source, 0, holdId, "Preserve for legal matter", actor, now);
        var active = retention.GetLegalHolds();
        var released = retention.ReleaseLegalHold(source, 2, holdId, "Authorized release", actor, now.AddYears(10));

        // Assert
        Assert.True(placed.IsSuccess);
        Assert.True(released.IsSuccess);
        Assert.Null(Assert.Single(active).ReleasedAt);
        var historical = Assert.Single(retention.GetLegalHolds());
        Assert.Equal(actor, historical.PlacedBy);
        Assert.Equal(actor, historical.ReleasedBy);
        Assert.Equal(now.AddYears(10), historical.ReleasedAt);
        Assert.Null(retention.Basis);
        Assert.Equal(3, retention.Revision);
    }
    [Fact]
    public void ShouldRejectRegressingSourcePositionGivenAlreadyBoundAuthority()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "application_import", id);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "application_import", id, new string('a', 64)), 10);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
        var now = DateTimeOffset.UtcNow;
        Assert.True(retention.PlaceLegalHold(source, 0, Uuid.CreateVersion4(), "Preserve", actor, now).IsSuccess);

        // Act
        var result = retention.PlaceLegalHold(source with { Position = 9 }, 2, Uuid.CreateVersion4(), "Other hold", actor, now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, retention.Revision);
    }
    [Theory]
    [InlineData("digest")]
    [InlineData("tenant")]
    [InlineData("period")]
    [InlineData("revision")]
    [InlineData("reason")]
    public void ShouldRejectChangedIntentGivenRecordedEvidenceBasis(string change)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "evidence_artifact", id);
        var first = new DateOnly(2019, 1, 1);
        var last = new DateOnly(2019, 12, 31);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "evidence_artifact", id, new string('a', 64)), 10, first, last);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
        var now = DateTimeOffset.UtcNow;
        Assert.True(retention.RecordBasis(source, 0, null, null, "Verified", actor, now).IsSuccess);
        var altered = change switch
        {
            "digest" => source with { Source = source.Source with { ContentSha256 = new string('b', 64) } },
            "tenant" => source with { Source = source.Source with { TenantId = Uuid.CreateVersion4() } },
            "period" => source with { PeriodEnd = last.AddDays(1) },
            _ => source,
        };

        // Act
        var result = retention.RecordBasis(altered, change == "revision" ? 3 : 0,
            null, null, change == "reason" ? "Changed" : "Verified", actor, now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, retention.Revision);
        Assert.Equal(source.Source, retention.Source);
    }

    [Fact]
    public void ShouldRemainBlockedGivenElapsedPeriodAndReleasedHold()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "application_import", id);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "application_import", id, new string('a', 64)), 10);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin");
        var now = DateTimeOffset.UtcNow;
        var hold = Uuid.CreateVersion4();
        Assert.True(retention.RecordBasis(source, 0, new(2019, 1, 1), new(2019, 12, 31), "Verified", actor, now).IsSuccess);
        Assert.True(retention.PlaceLegalHold(source, 2, hold, "Legal matter", actor, now).IsSuccess);
        Assert.True(retention.ReleaseLegalHold(source, 3, hold, "Matter closed", actor, now).IsSuccess);

        // Act
        var anniversary = retention.Assess(source.Source, new(2026, 12, 31));
        var elapsed = retention.Assess(source.Source, new(2027, 1, 1));
        var replay = retention.PlaceLegalHold(source, 2, hold, "Legal matter", actor, now);
        var changed = retention.ReleaseLegalHold(source, 3, hold, "Different reason", actor, now);

        // Assert
        Assert.False(anniversary.RetentionElapsed);
        Assert.True(elapsed.RetentionElapsed);
        Assert.False(elapsed.DispositionAllowed);
        Assert.Equal(0, elapsed.ActiveLegalHoldCount);
        Assert.Equal(1, elapsed.LegalHoldHistoryCount);
        Assert.Contains("engagement_hold_authority_unavailable", elapsed.Blockers);
        Assert.Contains("historical_reliance_unavailable", elapsed.Blockers);
        Assert.Contains("inline_disposition_unsupported", elapsed.Blockers);
        Assert.True(replay.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(4, retention.Revision);
    }

    [Fact]
    public void ShouldRequireExplicitSupportedPeriodGivenRawImportRows()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "application_import", id);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "application_import", id, new string('a', 64)), 1);

        // Act
        var result = retention.RecordBasis(source, 0, null, null, "Submitted today",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Org Admin"), DateTimeOffset.UtcNow);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(0, retention.Revision);
        Assert.Empty(new AggregateScenario<ArtifactRetention>(retention).PendingEvents);
    }
    [Theory]
    [InlineData("basis")]
    [InlineData("place")]
    [InlineData("release")]
    public void ShouldRejectChangedActorGivenAttributedHistoricalDecisionRetry(string operation)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var retention = new ArtifactRetention(tenant, "application_import", id);
        var source = new ArtifactRetentionSourceSnapshot(new(tenant, "application_import", id, new string('a', 64)), 1);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "First Org Admin");
        var other = ActorReference.ForMember(Uuid.CreateVersion4(), "Another Org Admin");
        var now = DateTimeOffset.UtcNow;
        var hold = Uuid.CreateVersion4();
        Assert.True(retention.RecordBasis(source, 0, new(2019, 1, 1), new(2019, 12, 31), "Verified", actor, now).IsSuccess);
        Assert.True(retention.PlaceLegalHold(source, 2, hold, "Preserve", actor, now).IsSuccess);
        Assert.True(retention.ReleaseLegalHold(source, 3, hold, "Released", actor, now).IsSuccess);

        // Act
        var result = operation switch
        {
            "basis" => retention.RecordBasis(source, 0, new(2019, 1, 1), new(2019, 12, 31), "Verified", other, now),
            "place" => retention.PlaceLegalHold(source, 2, hold, "Preserve", other, now),
            _ => retention.ReleaseLegalHold(source, 3, hold, "Released", other, now),
        };

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(4, retention.Revision);
    }
}

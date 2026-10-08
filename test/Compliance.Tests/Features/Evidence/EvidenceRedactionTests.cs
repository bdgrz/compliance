using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceRedactionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Contributor = ActorReference.ForMember(Uuid.CreateVersion4(), "Contributor");

    [Fact]
    public void ShouldRetainExactOriginalAndDistinctDerivativeGivenAttributedPreparation()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var original = Identity('a');
        var derived = Identity('b');
        var request = Uuid.CreateVersion4();

        // Act
        var result = redaction.Prepare(request, 0, original, derived,
            "Manually prepared replacement", "Remove private employee fields", Contributor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(original, result.Value.Original);
        Assert.Equal(derived, result.Value.Derived);
        Assert.Equal(request, result.Value.PreparationId);
        Assert.Equal(Contributor, result.Value.PreparedBy);
        Assert.IsType<EvidenceRedactionPrepared>(Assert.Single(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents));
    }

    [Fact]
    public void ShouldRetainNativeRegistrationIdentityGivenVerifiedNonIntakeArtifact()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var native = Identity('a') with { ArtifactId = Uuid.CreateVersion4() };
        native = native with { IdentitySha256 = EvidenceRedaction.IdentityDigest(native) };

        // Act
        var result = redaction.Prepare(Uuid.CreateVersion4(), 0, native, Identity('b'),
            "Manual", "Private fields", Contributor, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(native, result.Value.Original);
    }

    [Theory]
    [InlineData("same")]
    [InlineData("foreign_tenant")]
    [InlineData("empty_id")]
    [InlineData("digest")]
    [InlineData("event")]
    [InlineData("length")]
    [InlineData("chronology")]
    public void ShouldRejectBeforeAppendGivenInvalidSourceIdentity(string scenario)
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var original = Identity('a');
        var derived = scenario switch
        {
            "same" => original,
            "foreign_tenant" => Identity('b') with { TenantId = Uuid.CreateVersion4() },
            "empty_id" => Identity('b') with { ArtifactId = Uuid.Empty },
            "digest" => Identity('b') with { RegistrationSha256 = "invalid" },
            "event" => Identity('b') with { RegistrationEventId = Uuid.Empty },
            "length" => Identity('b') with { ContentLength = 0 },
            "chronology" => Identity('b') with { RegisteredAt = Now.AddMinutes(1) },
            _ => throw new InvalidOperationException()
        };

        // Act
        var result = redaction.Prepare(Uuid.CreateVersion4(), 0, original, derived,
            "Manual preparation", "Remove personal fields", Contributor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Empty(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents);
    }

    [Fact]
    public void ShouldReturnOriginalAttributedPreparationGivenExactRetryAndDenyChangedActor()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var original = Identity('a');
        var derived = Identity('b');
        var request = Uuid.CreateVersion4();
        var first = redaction.Prepare(request, 0, original, derived, "Manual", "Private fields", Contributor, Now);
        Assert.True(first.IsSuccess);

        // Act
        var retry = redaction.Prepare(request, 0, original, derived, "Manual", "Private fields", Contributor, Now.AddMinutes(1));
        var changed = redaction.Prepare(request, 0, original, derived, "Manual", "Private fields",
            ActorReference.ForMember(Uuid.CreateVersion4(), "Other contributor"), Now.AddMinutes(1));

        // Assert
        Assert.Equal(first.Value, retry.Value);
        Assert.False(changed.IsSuccess);
        Assert.Single(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents);
    }

    [Fact]
    public void ShouldReplayFrozenLineageAndRetainPriorPreparationGivenNewAttributedRevision()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var redaction = new EvidenceRedaction(Tenant, id);
        var original = Identity('a');
        var derived = Identity('b');
        Assert.True(redaction.Prepare(Uuid.CreateVersion4(), 0, original, derived, "Manual", "First reason", Contributor, Now).IsSuccess);
        Assert.True(redaction.Prepare(Uuid.CreateVersion4(), 1, original, derived, "Manual", "Revised reason", Contributor, Now.AddMinutes(1)).IsSuccess);
        var events = new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<EvidenceRedaction>(new EvidenceRedaction(Tenant, id)).Given(events).Aggregate;

        // Assert
        Assert.Equal(2, replay.Revision);
        Assert.Equal("First reason", replay.Preparations[0].Reason);
        Assert.Equal("Revised reason", replay.CurrentPreparation!.Reason);
        Assert.Throws<NotSupportedException>(() => ((IList<EvidenceRedactionPreparationFact>)replay.Preparations).Clear());
    }

    [Fact]
    public void ShouldRetainExactPreparedApprovalGivenIndependentLeadDecision()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var preparationId = Uuid.CreateVersion4();
        Assert.True(redaction.Prepare(preparationId, 0, Identity('a'), Identity('b'),
            "Manual", "Private fields", Contributor, Now).IsSuccess);
        var lead = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var decisionId = Uuid.CreateVersion4();

        // Act
        var result = redaction.Approve(decisionId, 1, preparationId, 1, 2,
            lead, Now.AddMinutes(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(preparationId, result.Value.PreparationId);
        Assert.Equal(1, result.Value.PreparedRevision);
        Assert.Equal(lead, result.Value.ApprovedBy);
        Assert.IsType<EvidenceRedactionApproved>(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents[^1]);
    }

    [Fact]
    public void ShouldReturnOriginalPresentationGivenSameCanonicalPreparerAfterDisplayChange()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var original = Identity('a');
        var derived = Identity('b');
        var request = Uuid.CreateVersion4();
        var first = redaction.Prepare(request, 0, original, derived, "Manual", "Private fields", Contributor, Now);
        Assert.True(first.IsSuccess);

        // Act
        var renamed = redaction.Prepare(request, 0, original, derived, "Manual", "Private fields",
            Contributor with { Display = "Renamed contributor" }, Now.AddMinutes(1));

        // Assert
        Assert.True(renamed.IsSuccess);
        Assert.Equal(first.Value, renamed.Value);
        Assert.Equal(Contributor, renamed.Value.PreparedBy);
        Assert.Single(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents);
    }

    [Theory]
    [InlineData("registration_event")]
    [InlineData("registration_payload")]
    [InlineData("content_length")]
    [InlineData("registration_time")]
    public void ShouldRejectChangedSafeSourceTupleGivenRetainedPreparationReplay(string scenario)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var redaction = new EvidenceRedaction(Tenant, id);
        Assert.True(redaction.Prepare(Uuid.CreateVersion4(), 0, Identity('a'), Identity('b'),
            "Manual", "Private fields", Contributor, Now).IsSuccess);
        var ev = Assert.IsType<EvidenceRedactionPrepared>(Assert.Single(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents));
        var original = scenario switch
        {
            "registration_event" => ev.Preparation.Original with { RegistrationEventId = Uuid.CreateVersion4() },
            "registration_payload" => ev.Preparation.Original with { RegistrationSha256 = new string('d', 64) },
            "content_length" => ev.Preparation.Original with { ContentLength = 124 },
            "registration_time" => ev.Preparation.Original with { RegisteredAt = Now.AddDays(-2) },
            _ => throw new InvalidOperationException()
        };
        var malformed = ev with { Preparation = ev.Preparation with { Original = original } };

        // Act
        Action replay = () => new AggregateScenario<EvidenceRedaction>(new EvidenceRedaction(Tenant, id)).Given(malformed);

        // Assert
        Assert.Throws<InvalidOperationException>(replay);
    }

    [Fact]
    public void ShouldPreserveApprovalPresentationAndDenyAnotherCanonicalActorGivenExactRetry()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var preparationId = Uuid.CreateVersion4();
        Assert.True(redaction.Prepare(preparationId, 0, Identity('a'), Identity('b'), "Manual", "Private fields", Contributor, Now).IsSuccess);
        var actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Lead");
        var request = Uuid.CreateVersion4();
        var first = redaction.Approve(request, 1, preparationId, 1, 2, actor, Now.AddMinutes(1));
        Assert.True(first.IsSuccess);

        // Act
        var renamed = redaction.Approve(request, 1, preparationId, 1, 3,
            actor with { Display = "Renamed lead" }, Now.AddMinutes(2));
        var other = redaction.Approve(request, 1, preparationId, 1, 3,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Other lead"), Now.AddMinutes(2));

        // Assert
        Assert.Equal(first.Value, renamed.Value);
        Assert.False(other.IsSuccess);
        Assert.Equal(2, new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents.Count);
    }

    [Fact]
    public void ShouldDenySelfApprovalWithoutAppendGivenPreparerAsLead()
    {
        // Arrange
        var redaction = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var preparationId = Uuid.CreateVersion4();
        Assert.True(redaction.Prepare(preparationId, 0, Identity('a'), Identity('b'), "Manual", "Private fields", Contributor, Now).IsSuccess);

        // Act
        var result = redaction.Approve(Uuid.CreateVersion4(), 1, preparationId, 1, 2, Contributor, Now.AddMinutes(1));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Single(new AggregateScenario<EvidenceRedaction>(redaction).PendingEvents);
    }

    static EvidenceRedactionSourceCapsule Identity(char hash)
    {
        var digest = new string(hash, 64);
        var identity = new EvidenceRedactionSourceCapsule(Tenant, EvidenceIntake.IdFor(Tenant, digest), Uuid.CreateVersion4(),
            new string('c', 64), digest, 123, Now.AddDays(-1), "");
        return identity with { IdentitySha256 = EvidenceRedaction.IdentityDigest(identity) };
    }
}

using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceArtifactTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly ActorReference Collector = ActorReference.ForMember(Uuid.CreateVersion4(), "Collector");
    static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    const string Sha256 = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public void ShouldRegisterPendingInspectionGivenValidMetadata()
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());

        // Act
        var result = artifact.Register(Content(), Sha256, 4, Collector, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(EvidenceArtifactStates.PendingInspection, artifact.State);
        var registered = Assert.IsType<EvidenceArtifactRegistered>(Assert.Single(
            new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents));
        Assert.Equal("Quarterly access review export", registered.Content.Title);
        Assert.Equal(Sha256, registered.ContentSha256);
        Assert.Equal(Collector, registered.Collector);
    }

    [Theory]
    [InlineData(" ", "confidential", 0, 4, Sha256)]
    [InlineData("Export", "secret", 0, 4, Sha256)]
    [InlineData("Export", "internal", -1, 4, Sha256)]
    [InlineData("Export", "internal", 0, 0, Sha256)]
    [InlineData("Export", "internal", 0, 4, "ABC")]
    public void ShouldRejectBeforeAppendGivenInvalidMetadata(string title, string handlingClass,
        int periodEndOffsetDays, long length, string sha256)
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
        var content = Content() with
        {
            Title = title,
            HandlingClass = handlingClass,
            PeriodEnd = Content().PeriodStart.AddDays(periodEndOffsetDays),
        };

        // Act
        var result = artifact.Register(content, sha256, length, Collector, Now);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents);
    }

    [Fact]
    public void ShouldReplayIdenticalAndRejectChangedRegistrationGivenRegisteredArtifact()
    {
        // Arrange
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
        Assert.True(artifact.Register(Content(), Sha256, 4, Collector, Now).IsSuccess);

        // Act
        var replay = artifact.Register(Content(), Sha256, 4, Collector, Now);
        var changed = artifact.Register(Content() with { Title = "Other" }, Sha256, 4, Collector, Now);

        // Assert
        Assert.True(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Single(new AggregateScenario<EvidenceArtifact>(artifact).PendingEvents);
    }

    [Theory]
    [InlineData(EvidenceInspectionOutcome.Clean, "available", null)]
    [InlineData(EvidenceInspectionOutcome.Malware, "quarantined", "malware")]
    [InlineData(EvidenceInspectionOutcome.SecretDetected, "rejected", "secret_detected")]
    [InlineData(EvidenceInspectionOutcome.Invalid, "rejected", "invalid")]
    public void ShouldMoveToInspectedStateGivenInspectionOutcome(EvidenceInspectionOutcome outcome,
        string state, string? reason)
    {
        // Arrange
        var artifact = Registered();

        // Act
        var failure = artifact.RecordInspection(outcome, Now.AddMinutes(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal(state, artifact.State);
        Assert.Equal(reason, artifact.StateReason);
    }

    [Fact]
    public void ShouldRefuseSecondInspectionGivenAlreadyInspected()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Clean, Now));

        // Act
        var failure = artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("available", artifact.State);
    }

    [Fact]
    public void ShouldReleaseMalwareFalsePositiveGivenQuarantinedArtifact()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));
        var admin = ActorReference.ForMember(Uuid.CreateVersion4(), "Admin");

        // Act
        var failure = artifact.ReleaseQuarantine("Rescan clean; vendor false positive", admin, Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal("available", artifact.State);
        Assert.Null(artifact.StateReason);
    }

    [Fact]
    public void ShouldNeverReleaseGivenSecretDetected()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.SecretDetected, Now));

        // Act
        var failure = artifact.ReleaseQuarantine("Looks fine", Collector, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("rejected", artifact.State);
    }

    [Fact]
    public void ShouldRequireRationaleGivenQuarantineRelease()
    {
        // Arrange
        var artifact = Registered();
        Assert.Null(artifact.RecordInspection(EvidenceInspectionOutcome.Malware, Now));

        // Act
        var failure = artifact.ReleaseQuarantine(" ", Collector, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("quarantined", artifact.State);
    }

    static EvidenceArtifact Registered()
    {
        var artifact = new EvidenceArtifact(TenantId, Uuid.CreateVersion4());
        Assert.True(artifact.Register(Content(), Sha256, 4, Collector, Now).IsSuccess);
        return artifact;
    }

    static EvidenceArtifactContent Content() =>
        new("Quarterly access review export", null, "system_export", "Okta admin console",
            Now.AddHours(-2), new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), "confidential");
}

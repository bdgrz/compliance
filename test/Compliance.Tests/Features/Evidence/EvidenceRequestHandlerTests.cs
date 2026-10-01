using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed class EvidenceRequestHandlerTests
{
    const string Sha256 = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    [Fact]
    public async Task ShouldOpenAndLetTheOwnerFulfilGivenAnAvailableArtifact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);

        // Act
        var fulfilled = await fixture.AsAsync(fixture.OwnerUserId, new FulfilEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        Assert.Equal(EvidenceRequestLedger.Open, opened.Status);
        Assert.Equal(fixture.ControlId, opened.ControlId);
        Assert.Equal(EvidenceRequestLedger.Fulfilled, fulfilled.Status);
        Assert.Equal(artifactId, fulfilled.ArtifactId);
        var listed = await fixture.AsAsync(fixture.LeadUserId, new ListEvidenceRequests(fixture.TenantId,
            fixture.ProgramId, EvidenceRequestLedger.Fulfilled));
        Assert.Equal(opened.EvidenceRequestId, Assert.Single(listed.Items).EvidenceRequestId);
    }

    [Fact]
    public async Task ShouldForbidFulfilmentGivenNeitherOwnerNorManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.Clean);

        // Act
        var result = fixture.Scenario(fixture.ReviewerUserId).When(new FulfilEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldRefuseFulfilmentGivenARejectedArtifact()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);
        var artifactId = await ArtifactAsync(fixture, EvidenceInspectionOutcome.SecretDetected);

        // Act
        var result = fixture.Scenario(fixture.OwnerUserId).When(new FulfilEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, artifactId));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Conflict);
    }

    [Fact]
    public async Task ShouldRejectOpeningGivenAnUnknownOwner()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        var result = fixture.Scenario(fixture.LeadUserId).When(new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Export", "Upload it.", Uuid.CreateVersion4(), fixture.Today.AddDays(7)));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Validation);
    }

    [Fact]
    public async Task ShouldForbidOpeningGivenANonManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();

        // Act
        var result = fixture.Scenario(fixture.OwnerUserId).When(new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Export", "Upload it.", fixture.OwnerMemberId, fixture.Today.AddDays(7)));

        // Assert
        await result.ExpectFailure(RequestErrorKind.Forbidden);
    }

    [Fact]
    public async Task ShouldCancelWithRationaleGivenAManager()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var opened = await OpenAsync(fixture);

        // Act
        var cancelled = await fixture.AsAsync(fixture.LeadUserId, new CancelEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, opened.EvidenceRequestId, opened.Revision, "Superseded by the Q4 request"));

        // Assert
        Assert.Equal(EvidenceRequestLedger.Cancelled, cancelled.Status);
        Assert.Equal("Superseded by the Q4 request", cancelled.CancellationRationale);
    }

    static Task<EvidenceRequestView> OpenAsync(OperationsFixture fixture) =>
        fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId, fixture.ProgramId,
            "Q3 access review export", "Upload the signed review record.", fixture.OwnerMemberId,
            fixture.Today.AddDays(14), fixture.ControlId));

    static async Task<Uuid> ArtifactAsync(OperationsFixture fixture, EvidenceInspectionOutcome outcome)
    {
        var artifactId = Uuid.CreateVersion4();
        var collector = ActorReference.ForMember(fixture.OwnerMemberId, "Owner");
        await ProgramManagementServices.SeedAsync<EvidenceArtifact, EvidenceArtifactRegistration>(fixture.Provider,
            new EvidenceArtifact(fixture.TenantId, artifactId), artifact => artifact.Register(
                new EvidenceArtifactContent("Signed review", null, "document", "Okta", DateTimeOffset.UtcNow,
                    fixture.Today, fixture.Today, "internal"), Sha256, 4, collector, DateTimeOffset.UtcNow));
        await ProgramManagementServices.SeedAsync(fixture.Provider, new EvidenceArtifact(fixture.TenantId, artifactId),
            artifact => artifact.RecordInspection(outcome, DateTimeOffset.UtcNow) is null
                ? Result.Success
                : Result.Failure(new RequestError(RequestErrorKind.Conflict, "inspection")));
        return artifactId;
    }
}

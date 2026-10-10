using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.AccessReviews;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class FitzAccessReviewCampaignWorkItemDirectoryTests
{
    [Fact]
    public async Task ShouldProjectReviewAndRemediationAsSeparateStableWorkGivenSourceChanges()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var initialProjectionRevision = await directory.LoadRevisionAsync(fixture.TenantId);
        await ProjectAsync(directory, events, fixture.TenantId);
        Assert.Equal(initialProjectionRevision, await directory.LoadRevisionAsync(fixture.TenantId));
        var repeatedLaunchProjection = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var review = Assert.Single(repeatedLaunchProjection.Value,
            candidate => candidate.SourceId == itemId);
        var selfReviewItemId = ItemId(campaign, "rae", "admin");
        var selfReview = Assert.Single(repeatedLaunchProjection.Value, candidate =>
            candidate.SourceId == selfReviewItemId && candidate.Kind == WorkSource.AccessReviewReview);
        await fixture.SendAsync(fixture.ReviewerUserId, new RecordAccessDecision(fixture.TenantId,
            launched.CampaignId, itemId, 1, "revoke", "No longer required."));
        await ProjectAsync(directory, events, fixture.TenantId);
        var afterDecision = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var remediation = Assert.Single(afterDecision.Value,
            candidate => candidate.SourceId == itemId);
        await fixture.SendAsync(fixture.ManagerUserId,
            new RecordAccessRemediationChange(fixture.TenantId, launched.CampaignId, itemId, 2,
                "ticket-42", "Disable the stale grant.", DateTimeOffset.UtcNow));
        await ProjectAsync(directory, events, fixture.TenantId);
        var afterProviderChange = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var providerChanged = Assert.Single(afterProviderChange.Value,
            candidate => candidate.SourceId == itemId);
        var revisionAfterProviderChange = await directory.LoadRevisionAsync(fixture.TenantId);
        await ProjectAsync(directory, events, fixture.TenantId);
        var replayed = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var otherProgram = await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var otherTenant = await directory.LoadProgramAsync(Uuid.CreateVersion4(),
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal(WorkSource.AccessReviewReview, review.Kind);
        Assert.Equal("record_decision", review.NextAction);
        Assert.Equal(fixture.ReviewerMemberId, review.Responsible.Id);
        Assert.Equal(fixture.ProgramId, review.ProgramId);
        Assert.Equal(fixture.InstanceId, review.RestrictedSystemInstanceId);
        Assert.True(selfReview.RequiresSeparationOfDutiesWaiver);
        Assert.Contains(fixture.ReviewerMemberId, selfReview.Excluded);
        Assert.Equal(WorkSource.AccessReviewRemediation, remediation.Kind);
        Assert.Equal("record_remediation_change", remediation.NextAction);
        Assert.Equal(fixture.RemediationOwnerMemberId, remediation.Responsible.Id);
        Assert.NotEqual(review.WorkItemId, remediation.WorkItemId);
        Assert.Equal(remediation.WorkItemId, providerChanged.WorkItemId);
        Assert.Contains("verification", providerChanged.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("verify_remediation", providerChanged.NextAction);
        Assert.EndsWith("/remediation-verifications", providerChanged.ActionPath,
            StringComparison.Ordinal);
        Assert.Equal(revisionAfterProviderChange, await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Single(replayed.Value, candidate => candidate.WorkItemId == remediation.WorkItemId);
        Assert.Empty(otherProgram.Value);
        Assert.Empty(otherTenant.Value);
    }

    [Fact]
    public async Task ShouldRestoreRemediationWorkGivenExceptionExpiresWithoutNewEvent()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        var decision = await fixture.SendAsync(fixture.ReviewerUserId,
            new RecordAccessDecision(fixture.TenantId, launched.CampaignId, itemId, 1,
                "revoke", "No longer required."));
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await fixture.SendAsync(fixture.ManagerUserId,
            new ExemptAccessRemediation(fixture.TenantId, launched.CampaignId, itemId, 2,
                "Short-lived exception while access is disabled.", expiresAt));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var covered = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            expiresAt.AddTicks(-1), DateOnly.MaxValue, null);
        var expired = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            expiresAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal("revoke", decision.Decision);
        Assert.DoesNotContain(covered.Value, candidate => candidate.SourceId == itemId);
        Assert.Equal(WorkSource.AccessReviewRemediation,
            Assert.Single(expired.Value, candidate => candidate.SourceId == itemId).Kind);
    }

    [Fact]
    public async Task ShouldRemoveRemediationWorkGivenIndependentPopulationVerification()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId);
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        var decision = await fixture.SendAsync(fixture.ReviewerUserId,
            new RecordAccessDecision(fixture.TenantId, launched.CampaignId, itemId, 1,
                "revoke", "No longer required."));
        var currentFacts = AccessReviewFixture.StandardFacts();
        var laterFacts = new AccessPopulationFacts(currentFacts.Principals,
            currentFacts.Entitlements,
            currentFacts.GroupMembers.Where(static membership =>
                membership.GroupProviderSubjectId != "deployer" ||
                membership.MemberProviderSubjectId != "ada").ToArray(),
            currentFacts.Assignments);
        await Task.Delay(20);
        var (laterPopulationId, _) = await fixture.AcceptAsync(laterFacts,
            DateTimeOffset.UtcNow);
        var verification = await fixture.SendAsync(fixture.ManagerUserId,
            new VerifyAccessRemediation(fixture.TenantId, launched.CampaignId, itemId, 2,
                laterPopulationId));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var candidates = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal("removed", verification.Outcome);
        Assert.DoesNotContain(candidates.Value, candidate => candidate.SourceId == itemId);
    }

    [Fact]
    public async Task ShouldReassignCurrentReviewerWithoutChangingWorkIdentityGivenEligibilityLoss()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var launched = await fixture.LaunchAsync(populationId, fixture.ApproverMemberId,
            "Reviewer delegation.");
        var campaign = await fixture.CampaignAsync(launched.CampaignId);
        var itemId = ItemId(campaign, "ada", "deploy");
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await ProjectAsync(directory, events, fixture.TenantId);
        var before = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var beforeCandidate = Assert.Single(before.Value, candidate => candidate.SourceId == itemId);
        fixture.Permissions.Deny(fixture.ApproverUserId,
            Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission);
        await fixture.SendAsync(fixture.ManagerUserId, new ReassignAccessReviewResponsibility(
            fixture.TenantId, launched.CampaignId, itemId, AccessReviewResponsibilityKind.Reviewer,
            1, fixture.ReviewerMemberId, "Original reviewer lost Program queue access.",
            "Appointed the access owner as the review delegate."));

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var after = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var afterCandidate = Assert.Single(after.Value, candidate => candidate.SourceId == itemId);

        // Assert
        Assert.Equal(beforeCandidate.WorkItemId, afterCandidate.WorkItemId);
        Assert.Equal(fixture.ApproverMemberId, beforeCandidate.Responsible.Id);
        Assert.Equal(fixture.ReviewerMemberId, afterCandidate.Responsible.Id);
        Assert.Equal(2, await directory.LoadRevisionAsync(fixture.TenantId));
    }

    [Fact]
    public async Task ShouldSeparateCampaignReadVisibilityFromRemediationAuthorityGivenManagerRead()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync(applicationRestricted: true);
        fixture.AllowManagerRestrictedRead();
        fixture.Permissions.Deny(fixture.ManagerUserId,
            Bdgrz.Compliance.Features.AccessControl.RbacPermissions.AccessReviewManage);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var services = sourceScope.ServiceProvider;
        var eligibility = new AccessReviewQueueEligibility(
            services.GetRequiredService<IAggregateReader>(),
            new ProgramScopedQueueRead(fixture.ProgramId), fixture.Permissions,
            services.GetRequiredService<Bdgrz.Compliance.Features.Applications.RestrictedApplicationVisibility>());

        // Act
        var canRead = await eligibility.CanReadAsync(fixture.TenantId, fixture.ProgramId,
            fixture.ManagerMemberId, fixture.InstanceId, CancellationToken.None);
        var canReview = await eligibility.CanReviewAsync(fixture.TenantId, fixture.ProgramId,
            fixture.ManagerMemberId, fixture.InstanceId, CancellationToken.None);
        var canRemediate = await eligibility.CanRemediateAsync(fixture.TenantId, fixture.ProgramId,
            fixture.ManagerMemberId, fixture.InstanceId, CancellationToken.None);
        var crossProgramRead = await eligibility.CanReadAsync(fixture.TenantId,
            Uuid.CreateVersion4(), fixture.ManagerMemberId, fixture.InstanceId,
            CancellationToken.None);

        // Assert
        Assert.True(canRead);
        Assert.True(canReview);
        Assert.False(canRemediate);
        Assert.False(crossProgramRead);
    }

    [Fact]
    public async Task ShouldExcludeUnroutedLegacyCampaignGivenTenantDirectLaunch()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var routed = await fixture.LaunchAsync(populationId);
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await ProjectAsync(directory, events, fixture.TenantId);
        var routedCandidates = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, DateTimeOffset.UtcNow, DateOnly.MaxValue, null);
        var (legacyCampaignId, _) = await fixture.SeedLegacyCampaignAsync(routed.CampaignId);

        // Act
        await ProjectAsync(directory, events, fixture.TenantId);
        var candidates = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            DateTimeOffset.UtcNow, DateOnly.MaxValue, null);

        // Assert
        Assert.NotEqual(routed.CampaignId, legacyCampaignId);
        Assert.Equal(routedCandidates.Value.Select(static candidate => candidate.WorkItemId)
                .Order().ToArray(),
            candidates.Value.Select(static candidate => candidate.WorkItemId).Order().ToArray());
    }

    [Fact]
    public async Task ShouldRejectPartialFrozenRouteWithoutAdvancingCheckpointGivenMalformedCampaign()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        var routed = await fixture.LaunchAsync(populationId);
        var source = await fixture.CampaignAsync(routed.CampaignId);
        var malformedCampaignId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync<AccessReviewCampaign,
            AccessReviewCampaignRegistration>(fixture.Provider,
            new AccessReviewCampaign(fixture.TenantId, malformedCampaignId), campaign =>
                campaign.Launch(source.Name, source.Instructions, source.Deadline, source.SnapshotId,
                    source.ContentSha256, source.Reviewers,
                    source.Items.Select(static item => item.Item).ToArray(), source.LaunchedBy,
                    source.LaunchedAt, fixture.ProgramId));
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = CreateDirectory(sourceScope.ServiceProvider);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var before = await directory.LoadCheckpointAsync(fixture.TenantId);

        // Act
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ProjectAsync(directory, events, fixture.TenantId));
        var after = await directory.LoadCheckpointAsync(fixture.TenantId);

        // Assert
        Assert.Contains("both its Program and remediation owner", error.Message,
            StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    sealed class ProgramScopedQueueRead(Uuid permittedProgramId)
        : Bdgrz.Compliance.Features.AccessControl.IAccessGrantPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            Uuid programId, string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(programId == permittedProgramId &&
                permission == Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission);

        public ValueTask<Bdgrz.Compliance.Features.AccessControl.ProgramAccessVisibility>
            GetProgramVisibilityAsync(Uuid tenantId, Uuid userId, Uuid memberId,
                string permission, CancellationToken ct = default) =>
            ValueTask.FromResult(new Bdgrz.Compliance.Features.AccessControl.ProgramAccessVisibility(
                false, new HashSet<Uuid> { permittedProgramId }));
    }

    static FitzAccessReviewCampaignWorkItemDirectory CreateDirectory(IServiceProvider services) =>
        new(new InMemoryKvClient(), services.GetRequiredService<IAggregateReader>());

    static Uuid ItemId(AccessReviewCampaignView campaign, string subject, string entitlement) =>
        Assert.Single(campaign.Items, item => item.Item.ProviderSubjectId == subject &&
            item.Item.ProviderEntitlementId == entitlement).Item.ItemId;

    static async Task ProjectAsync(FitzAccessReviewCampaignWorkItemDirectory directory,
        IDomainEventReader events, Uuid tenantId)
    {
        var pattern = directory.SourcePattern(tenantId);
        var identity = new CheckpointIdentity(
            FitzAccessReviewCampaignWorkItemDirectory.ProjectorName, pattern);
        var current = await directory.LoadCheckpointAsync(tenantId);
        var cursor = current.Cursor;
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            current));
        await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
        {
            await directory.ApplyAsync(record.Event);
            cursor = record.NextCursor;
        }
        await batch.CommitAsync(new ProjectionCheckpoint(cursor));
    }
}

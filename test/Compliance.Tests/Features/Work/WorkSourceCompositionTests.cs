using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Features.AccessControl;
using Bdgrz.Compliance.Tests.Features.AccessReviews;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia.Testing;
using Cntryl.Fitz;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class WorkSourceCompositionTests
{
    [Fact]
    public async Task ShouldReconcileCountsDetailsAndSourceActionGivenAllProductionReaders()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today.AddDays(-7));
        var remediated = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        var verified = await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, remediated.FindingId, remediated.Revision,
            Assert.Single(remediated.CorrectiveActions).ActionId, "Removed.", OperationsFixture.FullSupport));
        var evidenceRequest = await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(
            fixture.TenantId, fixture.ProgramId, "Quarterly evidence", "Reviewed export", fixture.OwnerMemberId,
            fixture.Today));
        var (reviewCampaignId, reviewItemId, _) = await SeedReviewCampaignAsync(fixture);
        await using var provider = CreateProvider(fixture);
        var actor = ProgramManagementServices.Actor(fixture.ApproverUserId);
        RequestScenario Scenario() => RequestScenario.For(provider).GivenActor(actor);
        var list = new ListWork(fixture.TenantId, fixture.ProgramId, "all");

        // Act
        var lag = await Scenario().When(list with { Search = "no visible match" }).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var before = await Scenario().When(list).ExpectSuccess();
        foreach (var item in before.Value.Items)
        {
            var detail = await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
                .ExpectSuccess();
            Assert.Equal(item, detail.Value.Item);
            var searched = await Scenario().When(list with { Search = item.Kind }).ExpectSuccess();
            Assert.Equal(item, Assert.Single(searched.Value.Items));
            Assert.Equal(1, searched.Value.Counts.Total);
        }
        var outsider = await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(fixture.OutsiderUserId))
            .When(list with { Search = "Quarterly evidence" }).ExpectSuccess();
        Assert.Empty(outsider.Value.Items);
        Assert.Equal(0, outsider.Value.Counts.Total);
        var evidenceItem = Assert.Single(before.Value.Items, item => item.Kind == "evidence_request");
        var reviewItem = Assert.Single(before.Value.Items,
            item => item.Kind == WorkSource.AccessReviewReview);
        Assert.Equal(reviewItemId, reviewItem.SourceId);
        Assert.Equal("record_decision", reviewItem.NextAction);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/access-review-campaigns/" +
                     $"{reviewCampaignId}/items/{reviewItemId}/decisions", reviewItem.ActionPath);
        var reviewDetail = await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId,
            reviewItem.WorkItemId)).ExpectSuccess();
        Assert.Equal(reviewItem, reviewDetail.Value.Item);
        var reviewSearch = await Scenario().When(list with { Search = WorkSource.AccessReviewReview })
            .ExpectSuccess();
        Assert.Equal(reviewItem, Assert.Single(reviewSearch.Value.Items));
        Assert.Equal(1, reviewSearch.Value.Counts.Total);
        await RequestScenario.For(provider).GivenActor(ProgramManagementServices.Actor(fixture.OutsiderUserId))
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, evidenceItem.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);

        var decision = await PersonalAccessReviewTransportTests.SendHttpAsync(provider,
            fixture.LeadUserId, new RecordAccessDecision(fixture.TenantId, reviewCampaignId,
                reviewItemId, 1, "keep", "The access is still required."));
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var campaignChanged = await Scenario().When(list).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var afterCampaignDecision = await Scenario().When(list).ExpectSuccess();
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId,
            reviewItem.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);

        await Scenario().When(new CancelEvidenceRequest(fixture.TenantId, fixture.ProgramId,
            evidenceRequest.EvidenceRequestId, evidenceRequest.Revision, "No longer required")).ExpectSuccess();
        var changed = await Scenario().When(list).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var after = await Scenario().When(list).ExpectSuccess();

        var closureItem = Assert.Single(after.Value.Items, item => item.Kind == "finding_closure_review");
        await Scenario().When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId, closureItem.WorkItemId,
            0, fixture.ApproverMemberId)).ExpectSuccess();
        await PersonalReadinessClosureTransportTests.CloseHttpAsync(provider, fixture.ApproverUserId, new CloseFinding(fixture.TenantId, fixture.ProgramId, verified.FindingId,
            verified.Revision, "Independently verified correction", OperationsFixture.FullSupport,
            "Closure accepted"));
        await Scenario().When(list).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var final = await Scenario().When(list).ExpectSuccess();
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, closureItem.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.True(lag.Error!.IsTransient);
        Assert.True(campaignChanged.Error!.IsTransient);
        Assert.True(changed.Error!.IsTransient);
        Assert.Equal(new WorkCountsView(4, 1, 1, 1), before.Value.Counts);
        Assert.Equal(4, before.Value.Items.Select(item => item.WorkItemId).Distinct().Count());
        Assert.Equal(new WorkCountsView(3, 1, 1, 1), afterCampaignDecision.Value.Counts);
        Assert.DoesNotContain(afterCampaignDecision.Value.Items,
            item => item.WorkItemId == reviewItem.WorkItemId);
        Assert.Equal(new WorkCountsView(2, 1, 0, 1), after.Value.Counts);
        Assert.DoesNotContain(after.Value.Items, item => item.SourceId == evidenceRequest.EvidenceRequestId);
        Assert.Equal(new WorkCountsView(1, 1, 0, 1), final.Value.Counts);
        Assert.Equal("corrective_action", Assert.Single(final.Value.Items).Kind);
        Assert.Contains(after.Value.Items, item => item.Kind == "corrective_action" && item.NextAction == "complete");
        Assert.Contains(after.Value.Items, item => item.Kind == "finding_closure_review" && item.NextAction == "close");
    }

    [Fact]
    public async Task ShouldReconcileAccessReviewRemediationQueueGivenRevocationAndProviderChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var (campaignId, itemId, _) = await SeedReviewCampaignAsync(fixture);
        await using var provider = CreateProvider(fixture,
            accessReviewPermissions: fixture.Permissions);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid userId) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(userId));
        var request = new ListWork(fixture.TenantId, fixture.ProgramId, "all");

        var before = await Scenario(fixture.ApproverUserId).When(request).ExpectSuccess();
        var review = Assert.Single(before.Value.Items);

        // Act
        var decision = await PersonalAccessReviewTransportTests.SendHttpAsync(provider,
            fixture.LeadUserId, new RecordAccessDecision(fixture.TenantId, campaignId,
                itemId, 1, "revoke", "The access is no longer required."));
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        var decisionLag = await Scenario(fixture.ApproverUserId).When(request)
            .ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var pending = await Scenario(fixture.ApproverUserId).When(request).ExpectSuccess();
        var remediation = Assert.Single(pending.Value.Items);
        var remediationDetail = await Scenario(fixture.ApproverUserId).When(new GetWorkItem(
            fixture.TenantId, fixture.ProgramId, remediation.WorkItemId)).ExpectSuccess();
        var remediationSearch = await Scenario(fixture.ApproverUserId).When(request with
        {
            Search = WorkSource.AccessReviewRemediation,
        }).ExpectSuccess();

        var providerChange = await PersonalAccessReviewTransportTests.SendHttpAsync(provider,
            fixture.ApproverUserId, new RecordAccessRemediationChange(fixture.TenantId,
                campaignId, itemId, 2, "CASE-42", "Removed the provider-side grant.",
                DateTimeOffset.UtcNow));
        Assert.True(providerChange.IsSuccess, providerChange.Error?.Message);
        var providerChangeLag = await Scenario(fixture.ApproverUserId).When(request)
            .ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAsync(provider, fixture.TenantId);
        var afterProviderChange = await Scenario(fixture.ApproverUserId).When(request)
            .ExpectSuccess();
        var verified = Assert.Single(afterProviderChange.Value.Items);
        var verifiedDetail = await Scenario(fixture.ApproverUserId).When(new GetWorkItem(
            fixture.TenantId, fixture.ProgramId, verified.WorkItemId)).ExpectSuccess();
        var verifiedSearch = await Scenario(fixture.ApproverUserId).When(request with
        {
            Search = WorkSource.AccessReviewRemediation,
        }).ExpectSuccess();

        // Assert
        Assert.True(decisionLag.Error!.IsTransient);
        Assert.True(providerChangeLag.Error!.IsTransient);
        Assert.Equal(new WorkCountsView(1, 0, 0, 0), before.Value.Counts);
        Assert.Equal(WorkSource.AccessReviewReview, review.Kind);
        Assert.Equal("record_decision", review.NextAction);
        Assert.Equal(new WorkCountsView(1, 0, 0, 0), pending.Value.Counts);
        Assert.Equal(WorkSource.AccessReviewRemediation, remediation.Kind);
        Assert.Equal("record_remediation_change", remediation.NextAction);
        Assert.EndsWith("/remediation-changes", remediation.ActionPath, StringComparison.Ordinal);
        Assert.NotEqual(review.WorkItemId, remediation.WorkItemId);
        Assert.Equal(remediation, remediationDetail.Value.Item);
        Assert.Equal(remediation, Assert.Single(remediationSearch.Value.Items));
        Assert.Equal(new WorkCountsView(1, 0, 0, 0), remediationSearch.Value.Counts);
        Assert.Equal(remediation.WorkItemId, verified.WorkItemId);
        Assert.Equal(WorkSource.AccessReviewRemediation, verified.Kind);
        Assert.Equal("verify_remediation", verified.NextAction);
        Assert.EndsWith("/remediation-verifications", verified.ActionPath, StringComparison.Ordinal);
        Assert.Equal(new WorkCountsView(1, 0, 0, 0), afterProviderChange.Value.Counts);
        Assert.Equal(verified, verifiedDetail.Value.Item);
        Assert.Equal(verified, Assert.Single(verifiedSearch.Value.Items));
        Assert.Equal(new WorkCountsView(1, 0, 0, 0), verifiedSearch.Value.Counts);
    }

    [Fact]
    public async Task ShouldHideProductionProjectedRestrictedRemediationGivenNoCurrentSystemVisibility()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var (campaignId, itemId, systemInstanceId) = await SeedReviewCampaignAsync(fixture,
            applicationRestricted: true);
        var permissions = new RestrictedReadPermissions(fixture.Permissions, fixture.LeadUserId);
        await using var provider = CreateProvider(fixture, permissions, permissions);
        var decision = await PersonalAccessReviewTransportTests.SendHttpAsync(provider,
            fixture.LeadUserId, new RecordAccessDecision(fixture.TenantId, campaignId,
                itemId, 1, "revoke", "The access is no longer required."));
        Assert.True(decision.IsSuccess, decision.Error?.Message);
        await CatchUpAsync(provider, fixture.TenantId);

        await using var scope = provider.CreateAsyncScope();
        var source = scope.ServiceProvider.GetServices<IAccountableWorkItemDirectoryReader>()
            .Single(reader => reader.ProjectedKinds.Contains(WorkSource.AccessReviewRemediation));
        var projected = await source.LoadProgramAsync(fixture.TenantId, fixture.ProgramId);
        Assert.True(projected.IsSuccess, projected.Error?.Message);
        var candidate = Assert.Single(projected.Value);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.ApproverUserId));
        var list = new ListWork(fixture.TenantId, fixture.ProgramId, "all");

        // Act
        var all = await Scenario().When(list).ExpectSuccess();
        var search = await Scenario().When(list with
        {
            Search = WorkSource.AccessReviewRemediation,
        }).ExpectSuccess();
        var detail = await Scenario().When(new GetWorkItem(fixture.TenantId,
            fixture.ProgramId, candidate.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);
        var assignment = await Scenario().When(new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, candidate.WorkItemId, 0, fixture.LeadMemberId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Equal(WorkSource.AccessReviewRemediation, candidate.Kind);
        Assert.Equal(itemId, candidate.SourceId);
        Assert.Equal(fixture.ProgramId, candidate.ProgramId);
        Assert.Equal(systemInstanceId, candidate.RestrictedSystemInstanceId);
        Assert.Equal("record_remediation_change", candidate.NextAction);
        Assert.True(all.IsSuccess);
        Assert.Empty(all.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), all.Value.Counts);
        Assert.True(search.IsSuccess);
        Assert.Empty(search.Value.Items);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), search.Value.Counts);
        Assert.False(detail.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, detail.Error.Kind);
        Assert.False(assignment.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, assignment.Error.Kind);
    }

    static async Task<(Uuid CampaignId, Uuid ItemId, Uuid SystemInstanceId)>
        SeedReviewCampaignAsync(OperationsFixture fixture, bool applicationRestricted = false)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var applicationId = Uuid.CreateVersion4();
        var systemInstanceId = Uuid.CreateVersion4();
        var campaignId = Uuid.CreateVersion4();
        var populationId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var itemId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new DeclaredApplication(fixture.TenantId, applicationId), application =>
                application.Declare("Payroll", "Payroll administration", null,
                    fixture.LeadMemberId, "Lead", now, isRestricted: applicationRestricted));
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new DeclaredSystemInstance(fixture.TenantId, systemInstanceId), instance =>
                instance.Declare(applicationId, "Production", "aws_account", null, null,
                    Uuid.CreateVersion4(), "Seeder", now));
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ComplianceProgram(fixture.TenantId, fixture.ProgramId), program =>
                program.Create("Security", new ProgramPlan(null, null, null, null, null, null),
                    fixture.LeadMemberId, "Lead", now) is null
                    ? Result.Success
                    : Result.Failure(new RequestError(RequestErrorKind.Conflict, "program")));
        var item = new AccessReviewItemView(itemId, populationId, snapshotId, systemInstanceId,
            fixture.LeadMemberId, "ada", "user_account", "Ada Lovelace", "human", null, null,
            "deploy", "permission", "Deploy", false, "expected", [], null);
        var reviewer = new AccessReviewerView(populationId, systemInstanceId,
            fixture.LeadMemberId, false, null);
        await ProgramManagementServices.SeedAsync<AccessReviewCampaign,
            AccessReviewCampaignRegistration>(fixture.Provider,
            new AccessReviewCampaign(fixture.TenantId, campaignId), campaign => campaign.Launch(
                "Quarterly access review", "Review each frozen assignment.", now.AddDays(14),
                snapshotId, new string('a', 64), [reviewer], [item], actor, now,
                fixture.ProgramId, fixture.ApproverMemberId));
        return (campaignId, itemId, systemInstanceId);
    }

    [Fact]
    public async Task ShouldRemoveManagementWorkFromMineGivenRetainedAttestHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var permitted = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        var blocked = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await using var provider = CreateProvider(fixture);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));
        CompleteCorrectiveAction Complete(FindingView finding) => new(fixture.TenantId, fixture.ProgramId,
            finding.FindingId, finding.Revision, Assert.Single(finding.CorrectiveActions).ActionId,
            "Removed.", OperationsFixture.FullSupport);
        await Scenario().When(Complete(permitted)).ExpectSuccess();
        await CatchUpAsync(provider, fixture.TenantId);
        var before = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        Assert.Equal("corrective_action", item.Kind);
        Assert.Equal(1, before.Value.Counts.Total);

        // Act
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId,
            revoked: true);
        var denied = await Scenario().When(Complete(blocked)).ExpectFailure(RequestErrorKind.Forbidden);
        var after = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();

        // Assert
        Assert.Contains("Attest", denied.Error!.Message, StringComparison.Ordinal);
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);
    }

    [Fact]
    public async Task ShouldPreserveOversightButDenyManagementAssignmentGivenManagersAttestHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        await fixture.ProposeAsync(0);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid user) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(user));
        var before = await Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        Assert.Equal("control_operating_plan_approval", item.Kind);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.ApproverUserId);

        // Act
        var oversight = await Scenario(fixture.ApproverUserId)
            .When(new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId)).ExpectSuccess();
        await Scenario(fixture.ApproverUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0, fixture.OwnerMemberId)).ExpectFailure(RequestErrorKind.Forbidden);
        var assigned = await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, fixture.OwnerMemberId)).ExpectSuccess();

        // Assert
        Assert.Null(oversight.Value.Item.AssigneeMemberId);
        Assert.Equal(fixture.OwnerMemberId, assigned.Value.Item.AssigneeMemberId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldInvalidateRecordedAssigneeAndAllowReplacementGivenActualAttestHistory(bool revoked)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        var plan = await fixture.ProposeAsync(0);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid user) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(user));
        var before = await Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        var item = Assert.Single(before.Value.Items);
        await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0, fixture.ApproverMemberId)).ExpectSuccess();
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.ApproverUserId,
            revoked: revoked);

        // Act
        await PersonalOperatingPlanApprovalTransportTests.HttpAsync(provider, fixture.ApproverUserId,
            new ApproveControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                plan.Revision, plan.PlanVersionId, "Independent approval."), RequestErrorKind.Forbidden);
        var mine = await Scenario(fixture.ApproverUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine")).ExpectSuccess();
        var orphan = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        await Scenario(fixture.ApproverUserId).When(new DelegateWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 1, fixture.OwnerMemberId, "Replacement needed."))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario(fixture.ApproverUserId).When(new EscalateWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 1, "Replacement needed."))
            .ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, fixture.ApproverMemberId)).ExpectFailure(RequestErrorKind.Validation);
        await Scenario(fixture.LeadUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 1, fixture.OwnerMemberId)).ExpectSuccess();
        await PersonalOperatingPlanApprovalTransportTests.HttpAsync(provider, fixture.OwnerUserId,
            new ApproveControlOperatingPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                plan.Revision, plan.PlanVersionId, "Independent replacement approval."));
        await CatchUpAsync(provider, fixture.TenantId);
        var after = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine")).ExpectSuccess();

        // Assert
        Assert.Empty(mine.Value.Items);
        Assert.Equal(0, mine.Value.Counts.Total);
        Assert.Null(Assert.Single(orphan.Value.Items).AssigneeMemberId);
        Assert.Equal(1, orphan.Value.Counts.Total);
        Assert.DoesNotContain(after.Value.Items, candidate => candidate.WorkItemId == item.WorkItemId);
        Assert.All(after.Value.Items, candidate => Assert.Equal("control_occurrence", candidate.Kind));
    }

    [Theory]
    [InlineData("advisory", false)]
    [InlineData("attest", true)]
    public async Task ShouldPreserveManagementWorkGivenHistoryOutsideTheClientsAttestWall(string practice,
        bool otherTenant)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        await AttestAssignmentHistoryFixture.SeedAsync(provider,
            otherTenant ? Uuid.CreateVersion4() : fixture.TenantId, fixture.OwnerUserId, practice: practice);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        var mine = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();
        var completed = await Scenario().When(new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision,
            Assert.Single(finding.CorrectiveActions).ActionId, "Removed.", OperationsFixture.FullSupport))
            .ExpectSuccess();
        await CatchUpAsync(provider, fixture.TenantId);
        var after = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();

        // Assert
        Assert.Equal(1, mine.Value.Counts.Total);
        Assert.Equal(fixture.OwnerMemberId, Assert.Single(mine.Value.Items).AssigneeMemberId);
        Assert.Equal("completed", Assert.Single(completed.Value.CorrectiveActions).Status);
        Assert.Empty(after.Value.Items);
    }

    [Fact]
    public async Task ShouldPreserveTeamOversightButDenyClaimGivenActualAttestHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.OwnerMemberId);
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.AddToTeamAsync(fixture.OutsiderUserId);
        await fixture.PlanAsync(new OperatingHolder(OperatingAuthority.TeamHolder, fixture.TeamId));
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario(Uuid user) => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(user));
        var before = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "team")).ExpectSuccess();
        var item = before.Value.Items[0];
        Assert.Null(item.AssigneeMemberId);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId);

        // Act
        var team = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "team")).ExpectSuccess();
        var all = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "all")).ExpectSuccess();
        var unassigned = await Scenario(fixture.OwnerUserId)
            .When(new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned")).ExpectSuccess();
        await Scenario(fixture.OwnerUserId).When(new ClaimWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0)).ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario(fixture.OwnerUserId).When(new AssignWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, fixture.Member(fixture.OutsiderUserId)))
            .ExpectFailure(RequestErrorKind.Forbidden);
        var claimed = await Scenario(fixture.OutsiderUserId).When(new ClaimWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0)).ExpectSuccess();

        // Assert
        Assert.Equal(before.Value.Counts, team.Value.Counts);
        Assert.Equal(before.Value.Items, team.Value.Items);
        Assert.Equal(team.Value.Counts, all.Value.Counts);
        Assert.Empty(unassigned.Value.Items);
        Assert.Equal(0, unassigned.Value.Counts.Total);
        Assert.Equal(fixture.Member(fixture.OutsiderUserId), claimed.Value.Item.AssigneeMemberId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveReadAuthorizedTeamOversightGivenActionEligibilityLost(bool revoked)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.PlanAsync(new OperatingHolder(OperatingAuthority.TeamHolder, fixture.TeamId));
        Assert.DoesNotContain(fixture.OwnerMemberId, fixture.Permissions.Managers);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));
        var initial = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var performedItem = initial.Value.Items[0];
        var source = await Scenario().When(new GetControlOccurrence(fixture.TenantId,
            fixture.ProgramId, performedItem.ControlId!.Value, performedItem.SourceId)).ExpectSuccess();
        var performed = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, fixture.OwnerUserId, fixture.Attest(source.Value));
        Assert.Equal(ControlOperationsLedger.Submitted, performed.Value.State);
        await CatchUpAsync(provider, fixture.TenantId);
        var before = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var item = before.Value.Items[0];
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId,
            fixture.OwnerUserId, revoked);

        // Act
        var readable = await Scenario().When(new GetControlOccurrence(fixture.TenantId,
            fixture.ProgramId, item.ControlId!.Value, item.SourceId)).ExpectSuccess();
        var denied = await PersonalOccurrenceProofTransportTests.HttpAsync(provider, fixture.OwnerUserId, fixture.Attest(readable.Value), RequestErrorKind.Forbidden);
        var team = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var all = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "all")).ExpectSuccess();
        var detail = await Scenario().When(new GetWorkItem(fixture.TenantId,
            fixture.ProgramId, item.WorkItemId)).ExpectSuccess();
        foreach (var scope in new[] { "mine", "unassigned" })
        {
            var actionable = await Scenario().When(new ListWork(fixture.TenantId,
                fixture.ProgramId, scope)).ExpectSuccess();
            Assert.Empty(actionable.Value.Items);
            Assert.Equal(0, actionable.Value.Counts.Total);
        }
        await Scenario().When(new ClaimWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0)).ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario().When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0, fixture.OwnerMemberId)).ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario().When(new DelegateWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, 0, fixture.BackupMemberId, "No source action authority."))
            .ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Empty(detail.Value.History);
        Assert.Equal(item, detail.Value.Item);
        Assert.Contains("Attest", denied.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(before.Value.Items, team.Value.Items);
        Assert.Equal(before.Value.Counts, team.Value.Counts);
        Assert.Equal(team.Value.Items, all.Value.Items);
        Assert.Equal(team.Value.Counts, all.Value.Counts);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("suspended")]
    [InlineData("deprovisioned")]
    public async Task ShouldOmitTeamOversightGivenMembershipNoLongerCurrent(string membership)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.PlanAsync(new OperatingHolder(OperatingAuthority.TeamHolder, fixture.TeamId));
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));
        var before = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var item = before.Value.Items[0];
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId, true);
        if (membership == "removed")
            await fixture.RemoveFromTeamAsync(fixture.OwnerUserId);
        else if (membership == "suspended")
            await fixture.SuspendAsync(fixture.OwnerUserId);
        else
            await ProgramManagementServices.SeedAsync(provider, new Member(fixture.TenantId, fixture.OwnerUserId),
                member => member.Deprovision(fixture.LeadMemberId, "Lead", DateTimeOffset.UtcNow, "Left."));

        // Act
        var team = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var all = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "all")).ExpectSuccess();
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        Assert.Empty(team.Value.Items);
        Assert.Equal(0, team.Value.Counts.Total);
        Assert.Empty(all.Value.Items);
        Assert.Equal(0, all.Value.Counts.Total);
    }

    [Fact]
    public async Task ShouldRequireOrdinarySourceReadGrantGivenCurrentTeamOversight()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.PlanAsync(new OperatingHolder(OperatingAuthority.TeamHolder, fixture.TeamId));
        var permissions = new ReadTogglePermissions(fixture.Permissions);
        await using var provider = CreateProvider(fixture, permissions);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));
        var before = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var item = before.Value.Items[0];
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId, true);
        permissions.CanRead = false;

        // Act
        var source = await Scenario().When(new GetControlOccurrence(fixture.TenantId,
            fixture.ProgramId, item.ControlId!.Value, item.SourceId)).ExpectFailure(RequestErrorKind.Forbidden);
        var team = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectFailure(RequestErrorKind.Forbidden);
        await Scenario().When(new GetWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId)).ExpectFailure(RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(source.Error!.Message, team.Error!.Message);
    }

    [Fact]
    public async Task ShouldOmitTeamOversightGivenMalformedRetainedCanonicalMember()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await fixture.AddToTeamAsync(fixture.OwnerUserId);
        await fixture.PlanAsync(new OperatingHolder(OperatingAuthority.TeamHolder, fixture.TeamId));
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));
        var before = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();
        var item = before.Value.Items[0];
        var registrations = new List<DomainEvent>();
        await foreach (var record in provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                           EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "rbac-members",
                               fixture.OwnerMemberId.ToString()), ProjectionCheckpoint.Start.Cursor, CancellationToken.None))
            registrations.Add(record.Event);
        var registered = Assert.IsType<MemberRegistered>(Assert.Single(registrations));
        // Retain the current episode to prove the canonical actor fence, not an episode mismatch.
        DomainEvent malformed = new MemberRegistered(fixture.TenantId, fixture.OwnerMemberId,
            fixture.OutsiderUserId, "client_personnel", registered.MembershipEpisodeId);
        malformed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), fixture.OwnerMemberId,
            2, DateTimeOffset.UtcNow));
        await provider.GetRequiredService<IEventStore>().AppendAsync(new EventStreamAddress(
            fixture.TenantId.ToString(), "rbac-members", fixture.OwnerMemberId.ToString()),
            1, [malformed]);

        // Act
        await Scenario().When(new GetControlOccurrence(fixture.TenantId, fixture.ProgramId,
            item.ControlId!.Value, item.SourceId)).ExpectSuccess();
        var after = await Scenario().When(new ListWork(fixture.TenantId,
            fixture.ProgramId, "team")).ExpectSuccess();

        // Assert
        Assert.Empty(after.Value.Items);
        Assert.Equal(0, after.Value.Counts.Total);
    }

    [Fact]
    public async Task ShouldFailClosedGivenRetainedMemberUserDisagreesWithCanonicalMemberIdentity()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId, fixture.Today);
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        await AttestAssignmentHistoryFixture.SeedAsync(provider, fixture.TenantId, fixture.OwnerUserId);
        {
            var registrations = new List<DomainEvent>();
            await foreach (var record in provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                               EventStreamPattern.ForPattern(fixture.TenantId.ToString(), "rbac-members",
                                   fixture.OwnerMemberId.ToString()), ProjectionCheckpoint.Start.Cursor, CancellationToken.None))
                registrations.Add(record.Event);
            Assert.IsType<MemberRegistered>(Assert.Single(registrations));
            // Deliberately malformed retained provenance: public registration cannot produce this identity.
            DomainEvent malformed = new MemberRegistered(fixture.TenantId, fixture.OwnerMemberId,
                fixture.OutsiderUserId, "client_personnel", Uuid.CreateVersion4());
            malformed.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(), fixture.OwnerMemberId,
                2, DateTimeOffset.UtcNow));
            await provider.GetRequiredService<IEventStore>().AppendAsync(new EventStreamAddress(
                fixture.TenantId.ToString(), "rbac-members", fixture.OwnerMemberId.ToString()),
                1, [malformed]);
        }
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        await Scenario().When(new CompleteCorrectiveAction(fixture.TenantId, fixture.ProgramId,
            finding.FindingId, finding.Revision, Assert.Single(finding.CorrectiveActions).ActionId,
            "Removed.", OperationsFixture.FullSupport)).ExpectFailure(RequestErrorKind.Forbidden);
        var mine = await Scenario().When(new ListWork(fixture.TenantId, fixture.ProgramId, "mine"))
            .ExpectSuccess();

        // Assert
        Assert.Empty(mine.Value.Items);
        Assert.Equal(0, mine.Value.Counts.Total);
    }

    [Fact]
    public async Task ShouldCoverEveryProductionKindGivenSourceManagementRequestMapping()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await using var provider = CreateProvider(fixture);
        await using var scope = provider.CreateAsyncScope();
        var projectedKinds = scope.ServiceProvider.GetServices<IAccountableWorkItemDirectoryReader>()
            .SelectMany(reader => reader.ProjectedKinds).ToArray();
        var kinds = projectedKinds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

        // Act
        var mapped = WorkSourceManagement.SourceRequests.Keys.Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(projectedKinds.Length, projectedKinds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(kinds, mapped);
        Assert.All(WorkSourceManagement.SourceRequests.Values.SelectMany(types => types),
            type => Assert.True(typeof(IRequestBase).IsAssignableFrom(type)));
        Assert.Equal(typeof(AcknowledgePolicy), Assert.Single(
            WorkSourceManagement.SourceRequests[PolicyCampaignWork.Acknowledgement]));
        Assert.False(typeof(IClientManagementMutationRequest).IsAssignableFrom(typeof(AcknowledgePolicy)));
    }

    static ServiceProvider CreateProvider(OperationsFixture fixture,
        IPermissionAuthorizer? permissions = null,
        IPermissionAuthorizer? accessReviewPermissions = null)
    {
        var events = fixture.Provider.GetRequiredService<IEventStore>();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
            ["Fitz:ApplicationName"] = "compliance"
        }).Build();
        _ = services.AddCompliance(configuration, developerAuthentication: true);
        services.AddSingleton<IKvClient>(new InMemoryKvClient());
        services.AddSingleton(events);
        services.AddSingleton<IDomainEventReader>((IDomainEventReader)events);
        if (accessReviewPermissions is not null)
            services.AddSingleton(accessReviewPermissions);
        services.AddSingleton<IAccessGrantPermissionAuthorizer>(
            new PermissionBackedAccessGrantPermissionAuthorizer(permissions ?? fixture.Permissions));
        services.AddSingleton<IProgramResourceScopeResolver, TestProgramResourceScopeResolver>();
        services.AddSingleton<ITenantActivity, ActiveTenant>();
        services.AddSingleton<ITenantMembershipDirectoryReader, AlwaysMemberDirectory>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    sealed class ReadTogglePermissions(IPermissionAuthorizer inner) : IPermissionAuthorizer
    {
        public bool CanRead { get; set; } = true;

        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId, string permission,
            CancellationToken ct = default) => permission == Bdgrz.Compliance.Features.Programs.IProgramReadRequest.ReadPermission && !CanRead
            ? ValueTask.FromResult(false)
            : inner.IsAllowedAsync(tenantId, userId, memberId, permission, ct);
    }

    sealed class RestrictedReadPermissions(IPermissionAuthorizer inner, Uuid allowedUserId)
        : IPermissionAuthorizer
    {
        public ValueTask<bool> IsAllowedAsync(Uuid tenantId, Uuid userId, Uuid memberId,
            string permission, CancellationToken ct = default) =>
            permission == RbacPermissions.ApplicationRestrictedRead
                ? ValueTask.FromResult(userId == allowedUserId)
                : inner.IsAllowedAsync(tenantId, userId, memberId, permission, ct);
    }

    static async Task CatchUpAsync(IServiceProvider provider, Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var events = services.GetRequiredService<IDomainEventReader>();
        foreach (var reader in services.GetServices<IAccountableWorkItemDirectoryReader>())
        {
            var store = reader as FitzKvProjectionStore ?? services.GetRequiredService<FitzRiskEvaluationDirectory>();
            var method = store.GetType().GetMethod("ApplyAsync", [typeof(DomainEvent), typeof(CancellationToken)]);
            Assert.NotNull(method);
            await ApplyAsync(reader, store, method.CreateDelegate<Func<DomainEvent, CancellationToken, ValueTask>>(store));
        }

        async Task ApplyAsync(IAccountableWorkItemDirectoryReader reader, FitzKvProjectionStore store,
            Func<DomainEvent, CancellationToken, ValueTask> apply)
        {
            var pattern = reader.SourcePattern(tenantId);
            var checkpoint = await reader.LoadCheckpointAsync(tenantId);
            await using var batch = await store.BeginAsync(new ProjectionBatchContext(
                new CheckpointIdentity(reader.ProjectorName, pattern), checkpoint));
            var cursor = checkpoint.Cursor;
            await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await apply(record.Event, CancellationToken.None);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
    }
}

using Bdgrz.Compliance.Tests.Features.Readiness;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Boundaries;
using Bdgrz.Compliance.Features.Commitments;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Policies;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Responsibilities;
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
    public async Task ShouldRecoverPolicyCampaignWorkGivenProjectionLagAndCatchUp()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        await using var provider = CreateProvider(fixture);
        await CatchUpAsync(provider, fixture.TenantId);
        var campaignId = Uuid.CreateVersion4();
        var personId = fixture.PersonId;
        var launchedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var launchedOn = DateOnly.FromDateTime(launchedAt.UtcDateTime);
        var dueOn = launchedOn.AddDays(7);
        var subject = new CampaignSubject("policy", Uuid.CreateVersion4(), "POL-WORK",
            "Access Control Policy", 2, "policy-content-hash");
        var match = new RosterMatch(personId, "Pat Offline", "employee", "Engineering",
            launchedOn.AddYears(-1));
        var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [personId] = match,
        }, new HashSet<Uuid> { personId });
        await ProgramManagementServices.SeedAsync<PolicyDistributionCampaign,
            CampaignRegistration>(fixture.Provider,
            new PolicyDistributionCampaign(fixture.TenantId, campaignId), campaign =>
                campaign.Launch(fixture.ProgramId, subject, "core_security", [],
                    Uuid.CreateVersion4(), new string('a', 64), audience, dueOn,
                    "Read and acknowledge this policy.",
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"),
                    fixture.LeadMemberId, launchedAt));
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.ApproverUserId));
        var request = new ListWork(fixture.TenantId, fixture.ProgramId, "all");

        // Act
        var launchLag = await Scenario().When(request).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpPolicyCampaignProjectionAsync(provider, fixture.TenantId);
        var pending = await Scenario().When(request).ExpectSuccess();
        var campaignWork = Assert.Single(pending.Value.Items,
            item => item.Kind == PolicyCampaignWork.Acknowledgement);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new PolicyDistributionCampaign(fixture.TenantId, campaignId), campaign =>
            {
                Assert.Null(campaign.Acknowledge(fixture.ProgramId, Uuid.CreateVersion4(), personId,
                    subject.Version, subject.ContentSha256,
                    PolicyDistributionCampaign.DefaultAcknowledgementText,
                    new ActorReference("workforce_person", personId.ToString(), "Pat Offline"),
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), true,
                    DateTimeOffset.UtcNow));
                return Result.Success;
            });
        var acknowledgementLag = await Scenario().When(request)
            .ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpPolicyCampaignProjectionAsync(provider, fixture.TenantId);
        var reconciled = await Scenario().When(request).ExpectSuccess();

        // Assert
        Assert.True(launchLag.Error!.IsTransient);
        Assert.Equal(PolicyCampaignWork.Acknowledgement, campaignWork.Kind);
        Assert.Equal(Uuid.CreateVersion5(campaignId, "participant:" + personId),
            campaignWork.SourceId);
        Assert.True(acknowledgementLag.Error!.IsTransient);
        Assert.DoesNotContain(reconciled.Value.Items,
            item => item.WorkItemId == campaignWork.WorkItemId);
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

    static async Task<EvidenceRequestView> SeedOperationsWorkAsync(OperationsFixture fixture)
    {
        var cadence = new ControlCadence("ad_hoc", DueWithinDays: 10);
        await fixture.PlanAsync(cadence: cadence, effectiveFrom: fixture.Today.AddDays(-1));
        var first = await fixture.AsAsync(fixture.OwnerUserId, new OpenControlOccurrence(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "First independent check.",
            fixture.Today));
        _ = await fixture.AsAsync(fixture.OwnerUserId, new OpenControlOccurrence(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, "Second independent check.",
            fixture.Today));
        await fixture.AsAsync(fixture.OwnerUserId, fixture.Attest(first));
        var planSet = await fixture.GetPlansAsync();
        await fixture.ProposeAsync(planSet.Revision, cadence: cadence,
            effectiveFrom: fixture.Today);

        _ = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(7));
        var completed = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(7));
        var completedAction = Assert.Single(completed.CorrectiveActions);
        await fixture.AsAsync(fixture.OwnerUserId, new CompleteCorrectiveAction(fixture.TenantId,
            fixture.ProgramId, completed.FindingId, completed.Revision,
            completedAction.ActionId, "Removed the unsafe behavior.", OperationsFixture.FullSupport));

        return await fixture.AsAsync(fixture.LeadUserId, new OpenEvidenceRequest(fixture.TenantId,
            fixture.ProgramId, "Quarterly evidence", "Upload the approved export.",
            fixture.OwnerMemberId, fixture.Today.AddDays(7)));
    }

    static async Task SeedAccessReviewWorkAsync(OperationsFixture fixture,
        IServiceProvider provider)
    {
        var unresolved = await SeedReviewCampaignAsync(fixture);
        var remediation = await SeedReviewCampaignAsync(fixture);
        var result = await PersonalAccessReviewTransportTests.SendHttpAsync(provider,
            fixture.LeadUserId, new RecordAccessDecision(fixture.TenantId,
                remediation.CampaignId, remediation.ItemId, 1, "revoke",
                "The assignment is no longer required."));
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotEqual(unresolved.ItemId, remediation.ItemId);
    }

    static async Task SeedRiskGovernanceWorkAsync(OperationsFixture fixture)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var actionRiskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AddTreatmentAction(actionRiskId, 0, actionId, "mitigate",
                    "Enforce MFA", "MFA is required", "Identity provider export.",
                    fixture.Today.AddDays(10), fixture.OwnerMemberId, [],
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                return Result.Success;
            });

        var reviewRiskId = Uuid.CreateVersion4();
        var reviewActionId = Uuid.CreateVersion4();
        var submissionId = Uuid.CreateVersion4();
        var evidenceId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.AddTreatmentAction(reviewRiskId, 0, reviewActionId, "mitigate",
                    "Rotate credentials", "Credentials are rotated quarterly",
                    "The credential rotation record.", fixture.Today.AddDays(10),
                    fixture.OwnerMemberId, [], ActorReference.ForMember(fixture.LeadMemberId,
                        "Lead"), now));
                Assert.Null(ledger.SubmitActionCompletion(reviewRiskId, reviewActionId, 1,
                    submissionId, "Rotated all active credentials.", [evidenceId],
                    new HashSet<Uuid> { evidenceId }, fixture.OwnerMemberId,
                    ActorReference.ForMember(fixture.OwnerMemberId, "Owner"), now.AddMinutes(1)));
                return Result.Success;
            });

        var treatmentRiskId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.ProposeControlTreatment(treatmentRiskId, 0,
                    Uuid.CreateVersion4(), "mitigate", fixture.ControlId,
                    fixture.ControlVersionId, "This control treats the risk.",
                    fixture.LeadMemberId,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                return Result.Success;
            });
    }

    static async Task SeedRiskAcceptanceWorkAsync(OperationsFixture fixture)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        var riskId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskDraft(fixture.TenantId, riskId), draft =>
            {
                Assert.True(draft.Create(fixture.ProgramId, Uuid.CreateVersion4(), "R-MIXED",
                    new RiskDraftContent("Data exposure", "Customer records could be exposed.",
                        "Loss of customer trust.", null), fixture.LeadMemberId, "Lead", now)
                    .IsSuccess);
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskGovernanceLedger(fixture.TenantId, fixture.ProgramId), governance =>
            {
                Assert.Null(governance.AssignOwner(riskId, 0, fixture.PersonId,
                    fixture.OwnerMemberId, "Owns the service risk.",
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                return Result.Success;
            });
        RiskMethodVersionView? method = null;
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskMethod(fixture.TenantId, fixture.ProgramId), riskMethod =>
            {
                Assert.Null(riskMethod.Publish(fixture.ProgramId, 0,
                    ["rare", "unlikely", "possible", "likely", "almost certain"],
                    ["low", "minor", "moderate", "major", "severe"], 20,
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now));
                method = riskMethod.Current;
                return Result.Success;
            });
        var residualId = Uuid.CreateVersion4();
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new RiskEvaluation(fixture.TenantId, riskId), evaluation =>
            {
                Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 0,
                    Uuid.CreateVersion4(), method!, RiskEvaluation.Inherent, 2, 2,
                    "The baseline exposure is limited.", fixture.LeadMemberId, "Lead", now));
                Assert.Null(evaluation.ChooseTreatment(fixture.ProgramId, 1, "accept",
                    "The residual exposure is within appetite.", fixture.LeadMemberId, "Lead",
                    now.AddMinutes(1)));
                Assert.Null(evaluation.RecordAssessment(fixture.ProgramId, 2, residualId,
                    method!, RiskEvaluation.Residual, 3, 3,
                    "The residual exposure remains within appetite.", fixture.LeadMemberId,
                    "Lead", now.AddMinutes(2)));
                return Result.Success;
            });
    }

    static async Task SeedPolicyCampaignWorkAsync(OperationsFixture fixture, string kind)
    {
        var campaignId = Uuid.CreateVersion4();
        var launchedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var launchedOn = DateOnly.FromDateTime(launchedAt.UtcDateTime);
        var subject = new CampaignSubject(kind, Uuid.CreateVersion4(),
            kind == "training" ? "TR-MIXED" : "POL-MIXED",
            kind == "training" ? "Security awareness" : "Access control policy", 1,
            new string(kind == "training" ? 'b' : 'a', 64));
        var match = new RosterMatch(fixture.PersonId, "Pat Offline", "employee", "Engineering",
            launchedOn.AddYears(-1));
        var audience = new RosterAudience(new Dictionary<Uuid, RosterMatch>
        {
            [fixture.PersonId] = match,
        }, new HashSet<Uuid> { fixture.PersonId });
        await ProgramManagementServices.SeedAsync<PolicyDistributionCampaign,
            CampaignRegistration>(fixture.Provider,
            new PolicyDistributionCampaign(fixture.TenantId, campaignId), campaign =>
                campaign.Launch(fixture.ProgramId, subject, "core_security", [],
                    Uuid.CreateVersion4(), new string('c', 64), audience,
                    launchedOn.AddDays(10), "Read the material and record completion.",
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"),
                    fixture.LeadMemberId, launchedAt));
    }

    static async Task SeedControlDecisionWorkAsync(OperationsFixture fixture)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-3);
        await SeedControlAsync(fixture, "AC-MIXED-REVIEW", approve: false,
            retirement: false, acceptRetirementReview: false, now);
        await SeedControlAsync(fixture, "AC-MIXED-APPROVAL", approve: true,
            retirement: false, acceptRetirementReview: false, now.AddMinutes(1));
        await SeedControlAsync(fixture, "AC-MIXED-RETIRE-REVIEW", approve: true,
            retirement: true, acceptRetirementReview: false, now.AddMinutes(2));
        await SeedControlAsync(fixture, "AC-MIXED-RETIRE-APPROVAL", approve: true,
            retirement: true, acceptRetirementReview: true, now.AddMinutes(3));
    }

    static async Task SeedControlMappingWorkAsync(OperationsFixture fixture)
    {
        var editionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow.AddMinutes(-2);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlCriterionMappingLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Propose(fixture.ControlId, fixture.ControlVersionId,
                    editionId, "CC6.1", "criterion", 0,
                    "The control addresses access review.",
                    "Applies to production systems.", fixture.LeadMemberId, "Lead", now,
                    out _));
                return Result.Success;
            });
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CriterionApplicabilityLedger(fixture.TenantId, fixture.ProgramId), ledger =>
            {
                Assert.Null(ledger.Propose(editionId, "CC6.2", 0,
                    "The requirement does not apply to this organization.",
                    fixture.LeadMemberId, ActorReference.ForMember(fixture.LeadMemberId,
                        "Lead"), now, out _));
                return Result.Success;
            });
    }

    static async Task SeedControlEvaluationWorkAsync(OperationsFixture fixture)
    {
        EvaluationProcedureStep[] steps =
        [
            new("design", "inspection",
                [new EvaluationInspectedItem("record", "policy/access-review", "v3")],
                "Policy requires a monthly signed review."),
            new("implementation", "reperformance",
                [new EvaluationInspectedItem("artifact", "exports/q3.csv", "sha256:ab12")],
                "Every leaver was removed."),
            new("evidence_sufficiency", "inspection",
                [new EvaluationInspectedItem("artifact", "exports/q3.csv", "sha256:ab12")],
                "Export is complete and dated."),
        ];
        var plan = await fixture.GetOrDefineEvaluationPlanAsync(steps);
        var evaluation = await fixture.AsAsync(fixture.OwnerUserId,
            new StartControlEvaluation(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                plan.PlanVersionId));
        for (var index = 0; index < evaluation.Steps.Count; index++)
            evaluation = await fixture.AsAsync(fixture.OwnerUserId,
                new RecordControlEvaluationStep(fixture.TenantId, fixture.ProgramId,
                    fixture.ControlId, evaluation.EvaluationId, evaluation.Steps[index].StepId,
                    evaluation.Revision, "met", "Inspected the exact items.",
                    evaluation.Steps[index].InspectedItems));
        await fixture.AsAsync(fixture.OwnerUserId,
            new SubmitControlEvaluation(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                evaluation.EvaluationId, evaluation.Revision,
                [new("design", "effective", "Design meets the objective."),
                    new("implementation", "effective", "Operates as designed."),
                    new("evidence_sufficiency", "effective", "Evidence is sufficient.")]));
    }

    static async Task SeedControlAsync(OperationsFixture fixture, string identifier, bool approve,
        bool retirement, bool acceptRetirementReview, DateTimeOffset now)
    {
        var controlId = ControlDraft.IdFor(fixture.TenantId, fixture.ProgramId, identifier);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new ControlDraft(fixture.TenantId, controlId), control =>
            {
                var content = new ControlDraftContent("Mixed work control", "Protect access",
                    "Review access on a schedule.", "The owner records the review.",
                    ["Signed review record"]);
                Assert.True(control.Create(fixture.ProgramId, Uuid.CreateVersion4(), identifier,
                    content, fixture.LeadMemberId, "Lead", now).IsSuccess);
                var initialScope = new ResponsibilityScope("control", controlId,
                    ControlVersionIds.Initial(controlId), control.Revision);
                AssignControl(control, fixture, initialScope, fixture.ReviewerMemberId,
                    ResponsibilityType.AssignedReviewer, now);
                AssignControl(control, fixture, initialScope, fixture.ApproverMemberId,
                    ResponsibilityType.PolicyApprover, now);
                AssignControl(control, fixture, initialScope, fixture.OwnerMemberId,
                    ResponsibilityType.ControlOwner, now);
                if (!approve && !retirement)
                    return Result.Success;

                var reviewId = Uuid.CreateVersion4();
                Assert.Null(control.Review(fixture.ProgramId, control.Revision, reviewId,
                    "accept", "The draft is ready.", fixture.ReviewerMemberId, "Reviewer",
                    now.AddMinutes(1)));
                if (!retirement)
                    return Result.Success;

                Assert.Null(control.Approve(fixture.ProgramId, control.Revision,
                    Uuid.CreateVersion4(), reviewId, fixture.Today.AddDays(-30),
                    "Approved for operations.", new HashSet<Uuid> { fixture.OwnerMemberId },
                    fixture.ApproverMemberId, "Approver", now.AddMinutes(2)));
                Assert.Null(control.ProposeRetirement(fixture.ProgramId,
                    control.ApprovedVersion!.VersionId, fixture.Today.AddDays(30),
                    "The control is replaced.", fixture.LeadMemberId, "Lead",
                    now.AddMinutes(3)));
                var retirementScope = new ResponsibilityScope("control", controlId,
                    control.PendingTargetId!.Value, control.Revision);
                AssignControl(control, fixture, retirementScope, fixture.ReviewerMemberId,
                    ResponsibilityType.AssignedReviewer, now.AddMinutes(3));
                AssignControl(control, fixture, retirementScope, fixture.ApproverMemberId,
                    ResponsibilityType.PolicyApprover, now.AddMinutes(3));
                if (acceptRetirementReview)
                    Assert.Null(control.Review(fixture.ProgramId, control.Revision,
                        Uuid.CreateVersion4(), "accept", "Retirement is appropriate.",
                        fixture.ReviewerMemberId, "Reviewer", now.AddMinutes(4)));
                return Result.Success;
            });
    }

    static void AssignControl(ControlDraft control, OperationsFixture fixture,
        ResponsibilityScope scope, Uuid memberId, ResponsibilityType type, DateTimeOffset at) =>
        Assert.Null(control.AssignResponsibility(scope, Uuid.CreateVersion4(), memberId, type,
            fixture.LeadMemberId, "Lead", at, at.AddMinutes(-1), null, []));

    static async Task SeedPolicyDecisionWorkAsync(OperationsFixture fixture)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-4);
        await SeedPolicyAsync(fixture, "POL-MIXED-DRAFT-REVIEW", false, false, false, false,
            fixture.Today, now);
        await SeedPolicyAsync(fixture, "POL-MIXED-DRAFT-APPROVAL", true, false, false, false,
            fixture.Today, now.AddMinutes(1));
        await SeedPolicyAsync(fixture, "POL-MIXED-RETIRE-REVIEW", true, true, true, false,
            fixture.Today.AddDays(-30), now.AddMinutes(2));
        await SeedPolicyAsync(fixture, "POL-MIXED-RETIRE-APPROVAL", true, true, true, true,
            fixture.Today.AddDays(-30), now.AddMinutes(3));
        await SeedPolicyAsync(fixture, "POL-MIXED-PERIODIC", true, true, false, false,
            fixture.Today.AddMonths(-13), now.AddMonths(-13));
    }

    static Task SeedPolicyAsync(OperationsFixture fixture, string identifier,
        bool acceptInitialReview, bool publishVersion, bool retirement,
        bool acceptRetirementReview, DateOnly approvedOn, DateTimeOffset now)
    {
        var policyId = Policy.IdFor(fixture.TenantId, fixture.ProgramId, identifier);
        return ProgramManagementServices.SeedAsync(fixture.Provider,
            new Policy(fixture.TenantId, policyId), policy =>
            {
                Assert.True(policy.Create(fixture.ProgramId, Uuid.CreateVersion4(), identifier,
                    MixedPolicyContent(identifier),
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"),
                    fixture.LeadMemberId, now).IsSuccess);
                if (!acceptInitialReview)
                    return Result.Success;
                var reviewId = Uuid.CreateVersion4();
                Assert.Null(policy.Review(fixture.ProgramId, policy.Revision, reviewId, "accept",
                    "The draft is ready.", ActorReference.ForMember(fixture.ReviewerMemberId,
                        "Reviewer"), fixture.ReviewerMemberId, now.AddMinutes(1)));
                if (!publishVersion)
                    return Result.Success;
                Assert.Null(policy.Approve(fixture.ProgramId, policy.Revision,
                    Uuid.CreateVersion4(), reviewId, approvedOn, true,
                    "Approve the policy.", null,
                    ActorReference.ForMember(fixture.ApproverMemberId, "Approver"),
                    fixture.ApproverMemberId, now.AddMinutes(2)));
                if (!retirement)
                    return Result.Success;
                Assert.Null(policy.ProposeRetirement(fixture.ProgramId,
                    policy.CurrentVersion!.Version, fixture.Today.AddDays(30),
                    "The policy is replaced.",
                    ActorReference.ForMember(fixture.LeadMemberId, "Lead"),
                    fixture.LeadMemberId, now.AddMinutes(3)));
                if (acceptRetirementReview)
                    Assert.Null(policy.Review(fixture.ProgramId, policy.Revision,
                        Uuid.CreateVersion4(), "accept", "Retirement is appropriate.",
                        ActorReference.ForMember(fixture.ReviewerMemberId, "Reviewer"),
                        fixture.ReviewerMemberId, now.AddMinutes(4)));
                return Result.Success;
            });
    }

    static PolicyContent MixedPolicyContent(string title) => new(title, "Govern access",
        PolicyAudience.CoreSecurity, null, 12, "Body of the mixed-source policy.", null,
        "Security owner", []);

    static async Task SeedBoundaryDecisionWorkAsync(OperationsFixture fixture)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-4);
        await SeedBoundaryAsync(fixture, approveReview: false, now);
        await SeedBoundaryAsync(fixture, approveReview: true, now.AddMinutes(1));
    }

    static Task SeedBoundaryAsync(OperationsFixture fixture, bool approveReview,
        DateTimeOffset now)
    {
        var boundaryId = Uuid.CreateVersion4();
        var draftVersionId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("boundary", boundaryId, draftVersionId, 1);
        var content = new BoundaryContent("SOC 2 system boundary", "readiness",
            ["security"], []);
        return ProgramManagementServices.SeedAsync(fixture.Provider,
            new SystemBoundary(fixture.TenantId, boundaryId), boundary =>
            {
                Assert.True(boundary.Create(fixture.ProgramId, draftVersionId, content,
                    fixture.LeadMemberId, "Lead", now).IsSuccess);
                Assert.Null(boundary.AssignResponsibility(scope, Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now, now, null, []));
                Assert.Null(boundary.AssignResponsibility(scope, Uuid.CreateVersion4(),
                    fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                    fixture.LeadMemberId, "Lead", now, now, null, []));
                if (approveReview)
                    Assert.Null(boundary.Review(draftVersionId, 1, Uuid.CreateVersion4(),
                        "accept", "Reviewed independently.", fixture.ReviewerMemberId,
                        "Reviewer", now.AddMinutes(1)));
                return Result.Success;
            });
    }

    static async Task SeedCommitmentDecisionWorkAsync(OperationsFixture fixture)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-4);
        await SeedCommitmentAsync(fixture, acceptReview: false, now);
        await SeedCommitmentAsync(fixture, acceptReview: true, now.AddMinutes(1));
    }

    static Task SeedCommitmentAsync(OperationsFixture fixture, bool acceptReview,
        DateTimeOffset now)
    {
        var draftId = Uuid.CreateVersion4();
        var scope = new ResponsibilityScope("commitment", draftId, draftId, 1);
        const string sourceReference = "MSA 4.1";
        return ProgramManagementServices.SeedAsync(fixture.Provider,
            new CommitmentDraft(fixture.TenantId, draftId), draft =>
            {
                Assert.True(draft.Create(fixture.ProgramId, Uuid.CreateVersion4(),
                    Uuid.CreateVersion4(), "service_commitment", "SC-MIXED",
                    "The service protects customer data.", "Security commitment.",
                    sourceReference, fixture.LeadMemberId, "Lead", now).IsSuccess);
                Assert.Null(draft.AssignResponsibility(scope, Uuid.CreateVersion4(),
                    fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                    fixture.LeadMemberId, "Lead", now, now, null, []));
                Assert.Null(draft.AssignResponsibility(scope, Uuid.CreateVersion4(),
                    fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                    fixture.LeadMemberId, "Lead", now, now, null, []));
                if (acceptReview)
                {
                    var reviewId = Uuid.CreateVersion4();
                    Assert.Null(draft.Review(fixture.ProgramId, draft.Revision, reviewId,
                        "accept", "Security owner", "applicable", "supported", null,
                        "Verified against the source.", fixture.ReviewerMemberId, "Reviewer",
                        now.AddMinutes(1), null, sourceReference, "Signed MSA section 4.1"));
                }
                return Result.Success;
            });
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

    [Fact]
    public async Task ShouldReconcileEveryProductionKindGivenMixedSourceQueue()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await using var ownedSource = fixture.Provider;
        fixture.Permissions.Managers.Add(fixture.ReviewerMemberId);
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptComplianceLead));
        fixture.Permissions.RiskApprovers.Add((fixture.ApproverMemberId,
            RbacPermissions.RiskAcceptExecutive));
        await using var provider = CreateProvider(fixture,
            accessReviewPermissions: fixture.Permissions);
        var openEvidence = await SeedOperationsWorkAsync(fixture);
        await SeedAccessReviewWorkAsync(fixture, provider);
        await SeedControlMappingWorkAsync(fixture);
        await SeedControlDecisionWorkAsync(fixture);
        await SeedControlEvaluationWorkAsync(fixture);
        await SeedPolicyDecisionWorkAsync(fixture);
        await SeedBoundaryDecisionWorkAsync(fixture);
        await SeedCommitmentDecisionWorkAsync(fixture);
        await SeedRiskGovernanceWorkAsync(fixture);
        await SeedRiskAcceptanceWorkAsync(fixture);
        await SeedPolicyCampaignWorkAsync(fixture, "policy");
        await SeedPolicyCampaignWorkAsync(fixture, "training");
        await CatchUpRiskDraftProjectionAsync(provider, fixture.TenantId);
        await CatchUpPolicyCampaignProjectionAsync(provider, fixture.TenantId);
        await CatchUpAccountableProjectorsAsync(provider, fixture.TenantId);
        RequestScenario Scenario() => RequestScenario.For(provider)
            .GivenActor(ProgramManagementServices.Actor(fixture.ApproverUserId));
        var request = new ListWork(fixture.TenantId, fixture.ProgramId, "all");
        var expectedKinds = WorkSourceManagement.SourceRequests.Keys
            .Order(StringComparer.Ordinal).ToArray();

        // Act
        var initial = await Scenario().When(request).ExpectSuccess();
        var evidenceWorkItem = Assert.Single(initial.Value.Items,
            item => item.Kind == WorkSource.EvidenceRequest &&
                    item.SourceId == openEvidence.EvidenceRequestId);
        foreach (var item in initial.Value.Items)
        {
            var detail = await Scenario().When(new GetWorkItem(fixture.TenantId,
                fixture.ProgramId, item.WorkItemId)).ExpectSuccess();
            var search = await Scenario().When(request with { Search = item.Summary }).ExpectSuccess();
            Assert.Equal(item, detail.Value.Item);
            Assert.Contains(item, search.Value.Items);
            Assert.Equal(CountsFor(search.Value), search.Value.Counts);
        }
        await Scenario().When(new CancelEvidenceRequest(fixture.TenantId, fixture.ProgramId,
            openEvidence.EvidenceRequestId, openEvidence.Revision, "Evidence is no longer required."))
            .ExpectSuccess();
        var lag = await Scenario().When(request).ExpectFailure(RequestErrorKind.Conflict);
        await CatchUpAccountableProjectorsAsync(provider, fixture.TenantId);
        var reconciled = await Scenario().When(request).ExpectSuccess();
        var removedDetail = await Scenario().When(new GetWorkItem(fixture.TenantId,
            fixture.ProgramId, evidenceWorkItem.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        var actualKinds = initial.Value.Items.Select(static item => item.Kind)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.True(expectedKinds.SequenceEqual(actualKinds),
            $"Expected {string.Join(',', expectedKinds)}; actual {string.Join(',', actualKinds)}");
        Assert.Equal(expectedKinds.Length, initial.Value.Items.Count);
        Assert.Equal(initial.Value.Items.Count,
            initial.Value.Items.Select(static item => item.WorkItemId).Distinct().Count());
        Assert.Equal(CountsFor(initial.Value), initial.Value.Counts);
        Assert.All(initial.Value.Items, item =>
        {
            Assert.NotEqual(Uuid.Empty, item.SourceId);
            Assert.False(string.IsNullOrWhiteSpace(item.NextAction));
            Assert.False(string.IsNullOrWhiteSpace(item.ActionPath));
            Assert.StartsWith($"/api/v1/tenants/{fixture.TenantId}/", item.ActionPath,
                StringComparison.Ordinal);
        });
        Assert.True(lag.Error!.IsTransient);
        Assert.Equal(CountsFor(reconciled.Value), reconciled.Value.Counts);
        Assert.Equal(initial.Value.Items.Count - 1, reconciled.Value.Counts.Total);
        Assert.Equal(RequestErrorKind.NotFound, removedDetail.Error!.Kind);
        Assert.DoesNotContain(reconciled.Value.Items,
            item => item.Kind == WorkSource.EvidenceRequest);

        static WorkCountsView CountsFor(WorkQueueView view) => new(view.Items.Count,
            view.Items.Count(static item => item.Overdue),
            view.Items.Count(item => item.DueOn == view.AsOf),
            view.Items.Count(static item => item.Escalated));
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
            ["Fitz:ApplicationName"] = "compliance",
            ["Compliance:Controls:ActivationEnabled"] = "true",
            ["Compliance:Controls:LifecycleEnabled"] = "true",
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

    static async Task CatchUpPolicyCampaignProjectionAsync(IServiceProvider provider,
        Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = Assert.Single(services.GetServices<IAccountableWorkItemDirectoryReader>(),
            candidate => candidate.ProjectorName ==
                         FitzPolicyCampaignWorkItemDirectory.ProjectorName);
        var registration = Assert.Single(services.GetServices<WorkloadRegistration>(),
            candidate => candidate.Name == reader.ProjectorName);
        var projector = (Projector)services.GetRequiredService(registration.ComponentType);
        await new ProjectorScenario(new TenantId(tenantId.ToString())).RunAsync(projector);
        var checkpoint = await reader.LoadCheckpointAsync(tenantId);
        var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
        while (true)
        {
            var next = await runner.RunAsync(projector, checkpoint);
            if (next == checkpoint)
                break;
            checkpoint = next;
        }
    }

    static async Task CatchUpRiskDraftProjectionAsync(IServiceProvider provider,
        Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registration = Assert.Single(services.GetServices<WorkloadRegistration>(),
            candidate => candidate.Name == "RiskDraftDirectory");
        var projector = (Projector)services.GetRequiredService(registration.ComponentType);
        await new ProjectorScenario(new TenantId(tenantId.ToString())).RunAsync(projector);
        var reader = services.GetRequiredService<IRiskDraftDirectoryReader>();
        var checkpoint = await reader.LoadCheckpointAsync(tenantId);
        var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
        while (true)
        {
            var next = await runner.RunAsync(projector, checkpoint);
            if (next == checkpoint)
                break;
            checkpoint = next;
        }
    }

    static async Task CatchUpAccountableProjectorsAsync(IServiceProvider provider,
        Uuid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var registrations = services.GetServices<WorkloadRegistration>().ToArray();
        var runner = new ProjectorRunner(services.GetRequiredService<IDomainEventReader>());
        foreach (var reader in services.GetServices<IAccountableWorkItemDirectoryReader>())
        {
            var registration = Assert.Single(registrations,
                candidate => candidate.Name == reader.ProjectorName);
            var projector = (Projector)services.GetRequiredService(registration.ComponentType);
            await new ProjectorScenario(new TenantId(tenantId.ToString())).RunAsync(projector);
            var checkpoint = await reader.LoadCheckpointAsync(tenantId);
            while (true)
            {
                var next = await runner.RunAsync(projector, checkpoint);
                if (next == checkpoint)
                    break;
                checkpoint = next;
            }
        }
    }
}

using Bdgrz.Compliance.Features.AccessReviews;
using Bdgrz.Compliance.Features.Workforce;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AccessReviews;

public sealed class AccessClassificationAndVarianceTests
{
    [Fact]
    public async Task ShouldRetainGovernedServiceIdentityCorrelationGivenProviderPrincipalClassification()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        fixture.Sources.Identities[fixture.BotIdentityId] = new ServiceIdentityView(fixture.TenantId,
            fixture.BotIdentityId, 1, "Deploy bot", "bot", "Deploys", null, "active", "person",
            fixture.AdaPersonId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90)), "manual", false,
            [], ActorReference.ForMember(Uuid.CreateVersion4(), "Seeder"), DateTimeOffset.UtcNow);

        // Act
        await fixture.ClassifyAsync(populationId, "bot", AccessReviewVocabulary.Nhi,
            serviceIdentityId: fixture.BotIdentityId);
        var principals = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));

        // Assert
        var principal = Assert.Single(principals.Items, item => item.ProviderSubjectId == "bot");
        Assert.Equal("nhi", principal.Classification);
        Assert.Equal(fixture.BotIdentityId, principal.Current!.ServiceIdentityId);
        Assert.Equal(fixture.BotIdentityId, Assert.Single(principal.History).ServiceIdentityId);
    }

    [Fact]
    public async Task ShouldProposeWithoutDecidingAndKeepHistoryGivenReclassification()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        var before = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));

        // Act
        await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Unclassified);
        var changed = await fixture.ClassifyAsync(populationId, "ada", AccessReviewVocabulary.Human,
            fixture.AdaPersonId, count: 1);
        var after = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));

        // Assert
        var adaBefore = Assert.Single(before.Items, item => item.ProviderSubjectId == "ada");
        Assert.Equal("unclassified", adaBefore.Classification);
        Assert.Equal("human", adaBefore.Proposal!.Classification);
        Assert.Equal(fixture.AdaPersonId, adaBefore.Proposal.PersonId);
        Assert.Contains("unclassified", adaBefore.Gaps);
        Assert.Equal("nhi", Assert.Single(before.Items, item => item.ProviderSubjectId == "bot")
            .Proposal!.Classification);
        var engineering = Assert.Single(before.Items, item => item.ProviderSubjectId == "engineering");
        Assert.True(engineering.IsAccessStructure);
        Assert.Equal("group", engineering.Classification);
        Assert.Contains("nested_group", Assert.Single(before.Items, item =>
            item.ProviderSubjectId == "admins").Gaps);
        var adaAfter = Assert.Single(after.Items, item => item.ProviderSubjectId == "ada");
        Assert.Equal("human", adaAfter.Classification);
        Assert.Equal(2, adaAfter.History.Count);
        Assert.Equal("unclassified", adaAfter.History[0].Classification);
        Assert.Equal(2, changed.Sequence);
        Assert.Equal("human", changed.ProposedClassification);
        Assert.DoesNotContain("unclassified", adaAfter.Gaps);
    }

    [Fact]
    public async Task ShouldRejectClassificationGivenMissingOwnerStructureOrStaleCount()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());

        // Act
        var nhiWithoutIdentity = await fixture.FailAsync(fixture.ManagerUserId,
            new ClassifyAccessPrincipal(fixture.TenantId, populationId, "bot", 0, "nhi", "Bot."),
            RequestErrorKind.Validation);
        var sharedWithoutOwner = await fixture.FailAsync(fixture.ManagerUserId,
            new ClassifyAccessPrincipal(fixture.TenantId, populationId, "legacy", 0, "shared",
                "Shared.", SharedJustification: "Break glass."), RequestErrorKind.Validation);
        var group = await fixture.FailAsync(fixture.ManagerUserId,
            new ClassifyAccessPrincipal(fixture.TenantId, populationId, "engineering", 0,
                "unclassified", "Group."), RequestErrorKind.Validation);
        var unknownPerson = await fixture.FailAsync(fixture.ManagerUserId,
            new ClassifyAccessPrincipal(fixture.TenantId, populationId, "ada", 0, "human", "Ada.",
                Uuid.CreateVersion4()), RequestErrorKind.Validation);
        var stale = await fixture.FailAsync(fixture.ManagerUserId,
            new ClassifyAccessPrincipal(fixture.TenantId, populationId, "ada", 3, "human", "Ada.",
                fixture.AdaPersonId), RequestErrorKind.Conflict);

        // Assert
        Assert.All(new[] { nhiWithoutIdentity, sharedWithoutOwner, group, unknownPerson, stale },
            Assert.NotNull);
    }

    [Fact]
    public async Task ShouldSurfaceInactiveWorkerUnownedNhiAndSharedGapsGivenGovernedFacts()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyStandardAsync(populationId);
        fixture.Sources.Statuses[fixture.AdaPersonId] = ["ended"];
        fixture.Sources.Identities[fixture.BotIdentityId] = new ServiceIdentityView(fixture.TenantId,
            fixture.BotIdentityId, 1, "Deploy bot", "bot", "Deploys", null, "active", "person",
            fixture.AdaPersonId, DateOnly.FromDateTime(DateTime.UtcNow), "manual", true,
            ["owner_relationship_ended"], ActorReference.ForMember(Uuid.CreateVersion4(), "Seeder"),
            DateTimeOffset.UtcNow);

        // Act
        var principals = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId));
        var unowned = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessPrincipals(fixture.TenantId, populationId, "nhi_unowned"));

        // Assert
        Assert.Contains("inactive_worker", Gaps(principals, "ada"));
        Assert.Contains("shared_account", Gaps(principals, "legacy"));
        Assert.Equal("bot", Assert.Single(unowned.Items).ProviderSubjectId);
    }

    [Fact]
    public async Task ShouldExplainVarianceWithoutDecidingGivenApprovedExpectationsAndException()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var noIamUsers = await fixture.ApprovedExpectationAsync(0,
            AccessReviewVocabulary.RequiresExpiry, new AccessExpectationParameters(
                PrincipalKind: "user_account", ProviderEntitlementId: "read"));
        await fixture.ApprovedExpectationAsync(2, AccessReviewVocabulary.PrivilegedEntitlement,
            new AccessExpectationParameters(EntitlementKind: "admin_privilege"));
        await fixture.ApprovedExpectationAsync(4, AccessReviewVocabulary.RequiredAccess,
            new AccessExpectationParameters(ProviderSubjectId: "bot", ProviderEntitlementId: "deploy"));
        await fixture.ApprovedExpectationAsync(6, AccessReviewVocabulary.RequiredAccess,
            new AccessExpectationParameters(ProviderSubjectId: "ci", ProviderEntitlementId: "deploy"));
        await fixture.ApprovedExpectationAsync(8, AccessReviewVocabulary.ForbiddenPrincipalKind,
            new AccessExpectationParameters(PrincipalKind: "access_token"));
        var (populationId, _) = await fixture.AcceptAsync(AccessReviewFixture.StandardFacts());
        await fixture.ClassifyAsync(populationId, "bot", AccessReviewVocabulary.Nhi,
            serviceIdentityId: fixture.BotIdentityId);
        await fixture.ClassifyAsync(populationId, "legacy", AccessReviewVocabulary.Human, fixture.AdaPersonId);
        await fixture.ClassifyAsync(populationId, "rae", AccessReviewVocabulary.Human, fixture.ReviewerPersonId);

        // Act
        var variance = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessVariance(fixture.TenantId, populationId));
        await fixture.SendAsync(fixture.ApproverUserId, new ExemptAccessExpectation(fixture.TenantId,
            fixture.InstanceId, noIamUsers.ExpectationId, 10, "legacy", "Migration in progress.",
            DateTimeOffset.UtcNow.AddDays(30), "read"));
        var excepted = await fixture.SendAsync(fixture.ManagerUserId,
            new GetAccessVariance(fixture.TenantId, populationId));
        var expectations = await fixture.SendAsync(fixture.ManagerUserId,
            new ListAccessExpectations(fixture.TenantId, fixture.InstanceId));

        // Assert
        Assert.Equal("prohibited", Item(variance, "legacy", "read").Category);
        Assert.Equal("expected", Item(variance, "bot", "deploy").Category);
        Assert.Equal("missing", Item(variance, "ci", "deploy").Category);
        Assert.Equal("unresolved", Item(variance, "ada", "deploy").Category);
        var raeAdmin = Item(variance, "rae", "admin");
        Assert.True(raeAdmin.Privileged);
        Assert.Equal("unexpected", raeAdmin.Category);
        Assert.Equal("excepted", Item(excepted, "legacy", "read").Category);
        Assert.NotNull(Item(excepted, "legacy", "read").Findings.Single(f => f.ExceptionId is not null));
        Assert.Equal(5, expectations.Expectations.Count);
        Assert.All(expectations.Expectations, expectation => Assert.Equal("approved", expectation.Status));
        Assert.Single(expectations.Exceptions);
    }

    [Fact]
    public async Task ShouldForbidProposerApprovalGivenNoWaiver()
    {
        // Arrange
        await using var fixture = await AccessReviewFixture.CreateAsync();
        var proposed = await fixture.SendAsync(fixture.ManagerUserId, new ProposeAccessExpectation(
            fixture.TenantId, fixture.ApplicationId, fixture.InstanceId, 0,
            AccessReviewVocabulary.ForbiddenPrincipalKind,
            new AccessExpectationParameters(PrincipalKind: "access_token"), "No tokens.",
            DateTimeOffset.UtcNow));

        // Act
        var self = await fixture.FailAsync(fixture.ManagerUserId, new ApproveAccessExpectation(
            fixture.TenantId, fixture.InstanceId, proposed.ExpectationId, 1), RequestErrorKind.Forbidden);
        var invalid = await fixture.FailAsync(fixture.ManagerUserId, new ProposeAccessExpectation(
            fixture.TenantId, fixture.ApplicationId, fixture.InstanceId, 1,
            AccessReviewVocabulary.RequiredAccess, new AccessExpectationParameters(PrincipalKind: "role"),
            "Bad.", DateTimeOffset.UtcNow), RequestErrorKind.Validation);

        // Assert
        Assert.Contains("proposed", self.Message, StringComparison.Ordinal);
        Assert.NotNull(invalid);
    }

    static IReadOnlyList<string> Gaps(Page<AccessPrincipalView> page, string subject) =>
        Assert.Single(page.Items, item => item.ProviderSubjectId == subject).Gaps;

    static AccessVarianceItemView Item(AccessVarianceView view, string subject, string entitlement) =>
        Assert.Single(view.Items, item => item.ProviderSubjectId == subject &&
                                          item.ProviderEntitlementId == entitlement);
}

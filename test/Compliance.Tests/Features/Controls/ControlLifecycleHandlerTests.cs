using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Responsibilities;
using Bdgrz.Compliance.Features.Versioning;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Controls;

public sealed class ControlLifecycleHandlerTests
{
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);
    static readonly DateOnly SuccessorFrom = new(2027, 1, 1);
    static readonly Uuid ApplicationId = Uuid.CreateVersion4();

    [Fact]
    public async Task ShouldApproveSuccessorGivenMatchingImpactDigestAndRejectStaleDigest()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var registration = await fixture.ProposeSuccessorAsync();
        await fixture.AssignOwnerAsync(registration.DraftVersionId, registration.Revision);
        var reviewId = await fixture.ReviewAsync(registration.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                registration.Revision));

        // Act
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.Approve(registration.Revision, reviewId, SuccessorFrom, "STALE"))
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.Approve(registration.Revision, reviewId, SuccessorFrom, preview.Digest))
            .ExpectSuccess();

        // Assert
        Assert.Equal("successor", preview.Kind);
        Assert.True(preview.Complete);
        Assert.Empty(preview.PendingContexts);
        Assert.Contains(preview.Changes, change => change.Field == "title");
        Assert.Contains(preview.Changes, change =>
            change.Field == "applicability" && change.ChangeType == "added");
        var applicability = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "applicability");
        Assert.Equal(ApplicationId, Assert.Single(applicability.Records).RecordId);
        var responsibilities = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "responsibilities");
        Assert.Contains(responsibilities.Records, record =>
            record.VersionId == fixture.InitialVersionId);
        var mappings = Assert.Single(preview.Contributions,
            contribution => contribution.Context == "mappings");
        Assert.Equal("unlinked", mappings.Status);
        Assert.Empty(mappings.Records);
        var versions = await fixture.QueryAsync<ListControlVersions, Page<ControlVersionView>>(
            new ListControlVersions(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal(["superseded", "approved"], versions.Items.Select(static v => v.Status));
        var before = await fixture.QueryAsync<GetEffectiveControlVersion, ControlVersionView>(
            new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, SuccessorFrom.AddDays(-1)));
        Assert.Equal(fixture.InitialVersionId, before.VersionId);
        var exact = await fixture.QueryAsync<GetControlVersion, ControlVersionView>(
            new GetControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                registration.DraftVersionId));
        Assert.Equal(fixture.InitialVersionId, exact.PredecessorVersionId);
    }

    [Fact]
    public async Task ShouldRetireGivenMatchingDigestAndKeepHistoricalReads()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var retireOn = new DateOnly(2027, 3, 1);
        var proposal = await fixture.Scenario(fixture.AuthorUserId)
            .When(new ProposeControlRetirement(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.InitialVersionId, retireOn, "Replaced."))
            .ExpectSuccess();
        var reviewId = await fixture.ReviewAsync(proposal.Value.Revision);
        var preview = await fixture.QueryAsync<PreviewControlImpact, ControlImpactPreview>(
            new PreviewControlImpact(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Value.Revision));

        // Act
        await fixture.Scenario(fixture.ApproverUserId)
            .When(new RetireControl(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                proposal.Value.Revision, reviewId, preview.Digest, "Retire at quarter end."))
            .ExpectSuccess();

        // Assert
        Assert.Equal("retirement", preview.Kind);
        Assert.Equal(proposal.Value.RetirementId, preview.TargetId);
        var current = await fixture.QueryAsync<GetCurrentControlVersion, ControlVersionView>(
            new GetCurrentControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId));
        Assert.Equal("retired", current.Status);
        Assert.Equal(retireOn, current.EffectiveUntil);
        var historical = await fixture.QueryAsync<GetEffectiveControlVersion, ControlVersionView>(
            new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, retireOn.AddDays(-1)));
        Assert.Equal(fixture.InitialVersionId, historical.VersionId);
        var after = await fixture.Scenario(fixture.ApproverUserId)
            .When(new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, retireOn))
            .ExpectFailure();
        Assert.Equal(RequestErrorKind.NotFound, after.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectSuccessorProposalGivenDisabledLifecycleGate()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: false);

        await fixture.Scenario(fixture.AuthorUserId)
            // Act
            .When(new ProposeControlSuccessor(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, fixture.InitialVersionId, Fixture.Content()))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Conflict);
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlDraft(fixture.TenantId, fixture.ControlId));
        Assert.False(source.HasOpenDraft);
    }

    [Fact]
    public async Task ShouldRejectPreviewGivenStaleRevision()
    {
        // Arrange
        var fixture = await Fixture.CreateApprovedAsync(lifecycleEnabled: true);
        var registration = await fixture.ProposeSuccessorAsync();

        // Act
        var result = await fixture.Scenario(fixture.ApproverUserId)
            .When(new PreviewControlImpact(fixture.TenantId, fixture.ProgramId,
                fixture.ControlId, registration.Revision - 1))
            .ExpectFailure();

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
    }

    sealed class Fixture
    {
        public required ServiceProvider Provider { get; init; }
        public Uuid TenantId { get; } = Uuid.CreateVersion4();
        public Uuid ProgramId { get; } = Uuid.CreateVersion4();
        public Uuid AuthorUserId { get; } = Uuid.CreateVersion4();
        public Uuid ReviewerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ApproverUserId { get; } = Uuid.CreateVersion4();
        public Uuid OwnerUserId { get; } = Uuid.CreateVersion4();
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "LC-HTTP");
        public Uuid InitialVersionId => ControlVersionIds.Initial(ControlId);
        Uuid OwnerMemberId => RbacIds.Member(TenantId, OwnerUserId);

        public static async Task<Fixture> CreateApprovedAsync(bool lifecycleEnabled)
        {
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed: true),
                portia => portia.AddRequestHandler<ReviewControlHandler>()
                    .AddRequestHandler<ApproveControlHandler>()
                    .AddRequestHandler<ProposeControlSuccessorHandler>()
                    .AddRequestHandler<ProposeControlRetirementHandler>()
                    .AddRequestHandler<PreviewControlImpactHandler>()
                    .AddRequestHandler<RetireControlHandler>()
                    .AddRequestHandler<GetControlVersionHandler>()
                    .AddRequestHandler<GetCurrentControlVersionHandler>()
                    .AddRequestHandler<GetEffectiveControlVersionHandler>()
                    .AddRequestHandler<ListControlVersionsHandler>(),
                services => services
                    .AddSingleton(new ControlActivationReleaseGate(true))
                    .AddSingleton(new ControlLifecycleReleaseGate(lifecycleEnabled))
                    .AddScoped<ControlActivationSource>()
                    .AddScoped<ControlImpactService>()
                    .AddScoped<IControlImpactContributor, ApplicabilityControlImpactContributor>()
                    .AddScoped<IControlImpactContributor, ResponsibilityControlImpactContributor>()
                    .AddSingleton<IControlApplicabilityReferenceValidator, AcceptingValidator>());
            var fixture = new Fixture { Provider = provider };
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control => control.Create(
                    fixture.ProgramId, Uuid.CreateVersion4(), "LC-HTTP", Content(),
                    RbacIds.Member(fixture.TenantId, fixture.AuthorUserId), "Author", now));
            await ProgramManagementServices.SeedAsync(provider,
                new Member(fixture.TenantId, fixture.OwnerUserId), member => member.Register());
            await fixture.AssignOwnerAsync(fixture.InitialVersionId, 1);
            var reviewId = await fixture.ReviewAsync(1);
            await fixture.Scenario(fixture.ApproverUserId)
                .When(new ApproveControl(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                    1, reviewId, EffectiveFrom, "Ready to operate."))
                .ExpectSuccess();
            return fixture;
        }

        public static ControlDraftContent Content() => new("Access review", "Review access",
            "Management reviews access", "The security lead reviews access quarterly.",
            ["Dated review record"]);

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public ApproveControl Approve(long revision, Uuid reviewId, DateOnly effectiveFrom,
            string digest) => new(TenantId, ProgramId, ControlId, revision, reviewId,
            effectiveFrom, "Successor ready.", ImpactDigest: digest);

        public async Task<ControlSuccessorRegistration> ProposeSuccessorAsync()
        {
            var result = await Scenario(AuthorUserId)
                .When(new ProposeControlSuccessor(TenantId, ProgramId, ControlId,
                    InitialVersionId, Content() with
                    {
                        Title = "Access review v2",
                        Applicability =
                        [
                            new ControlApplicabilityReference(Uuid.CreateVersion4(),
                                "application", "Billing", ApplicationId,
                                "Billing access is reviewed.", false),
                        ],
                    }))
                .ExpectSuccess();
            return result.Value;
        }

        public Task AssignOwnerAsync(Uuid versionId, long revision)
        {
            var now = DateTimeOffset.UtcNow;
            return ProgramManagementServices.SeedAsync(Provider,
                new ControlDraft(TenantId, ControlId), control =>
                    CommandResult(control.AssignResponsibility(new ResponsibilityScope("control",
                            ControlId, versionId, revision), Uuid.CreateVersion4(), OwnerMemberId,
                        ResponsibilityType.ControlOwner, RbacIds.Member(TenantId, AuthorUserId),
                        "Author", now, now.AddMinutes(-1), null, [])));
        }

        public async Task<Uuid> ReviewAsync(long revision)
        {
            var requestId = Uuid.CreateVersion4();
            await Scenario(ReviewerUserId)
                .GivenMetadata(new RequestMetadata(requestId, requestId, null))
                .When(new ReviewControl(TenantId, ProgramId, ControlId, revision, "accept",
                    "Reviewed"))
                .ExpectSuccess();
            return requestId;
        }

        public async Task<TOut> QueryAsync<TRequest, TOut>(TRequest request)
            where TRequest : IRequest<TOut>
        {
            var result = await Scenario(ApproverUserId).When(request).ExpectSuccess();
            return result.Value;
        }

        static Result CommandResult(CommandFailure? failure) => failure is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, failure.Message!));
    }

    sealed class AcceptingValidator : IControlApplicabilityReferenceValidator
    {
        public ValueTask<Result> ValidateAsync(Uuid tenantId, ControlDraftContent? content,
            CancellationToken ct = default) => ValueTask.FromResult(Result.Success);
    }
}

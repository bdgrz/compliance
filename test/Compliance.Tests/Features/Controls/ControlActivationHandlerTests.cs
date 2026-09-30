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

public sealed class ControlActivationHandlerTests
{
    static readonly DateOnly EffectiveFrom = new(2026, 10, 1);

    [Fact]
    public async Task ShouldRejectActivationGivenSuspendedOwnerThenActivateGivenReinstatedOwner()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: true);
        await fixture.SuspendOwnerAsync();
        var reviewId = await fixture.ReviewAsync();

        await fixture.Scenario(fixture.ApproverUserId)
            // Act
            .When(fixture.Approve(reviewId))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Conflict);
        await fixture.ReinstateOwnerAsync();
        await fixture.Scenario(fixture.ApproverUserId)
            .When(fixture.Approve(reviewId))
            .ExpectSuccess();
        var version = await fixture.QueryAsync<GetEffectiveControlVersion, ControlVersionView>(
            new GetEffectiveControlVersion(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                EffectiveFrom));
        Assert.Equal(fixture.OwnerMemberId, version.OwnerMemberId);
        var before = await fixture.QueryFailureAsync(new GetEffectiveControlVersion(
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, EffectiveFrom.AddDays(-1)));
        Assert.Equal(RequestErrorKind.NotFound, before);
        var decisions = await fixture.QueryAsync<ListControlDecisions, Page<ControlDecisionView>>(
            new ListControlDecisions(fixture.TenantId, fixture.ProgramId, fixture.ControlId, 1));
        Assert.Equal("review", Assert.Single(decisions.Items).Kind);
        Assert.NotNull(decisions.NextCursor);
        var next = await fixture.QueryAsync<ListControlDecisions, Page<ControlDecisionView>>(
            new ListControlDecisions(fixture.TenantId, fixture.ProgramId, fixture.ControlId, 1,
                decisions.NextCursor));
        Assert.Equal("approval", Assert.Single(next.Items).Kind);
        Assert.Null(next.NextCursor);
    }

    [Fact]
    public async Task ShouldRejectReviewGivenDisabledReleaseGate()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: false);

        await fixture.Scenario(fixture.ApproverUserId)
            // Act
            .When(new ReviewControl(fixture.TenantId, fixture.ProgramId, fixture.ControlId, 1,
                "accept", "Reviewed"))
            // Assert
            .ExpectAuthorized()
            .ExpectHandled()
            .ExpectFailure(RequestErrorKind.Conflict);
        var source = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlDraft(fixture.TenantId, fixture.ControlId));
        Assert.Empty(source.ReadDecisions());
    }

    [Fact]
    public async Task ShouldReturnNotFoundGivenControlFromAnotherProgram()
    {
        // Arrange
        var fixture = await Fixture.CreateAsync(activationEnabled: true);

        // Act
        var error = await fixture.QueryFailureAsync(new GetCurrentControlVersion(
            fixture.TenantId, Uuid.CreateVersion4(), fixture.ControlId));

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, error);
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
        public Uuid ControlId => ControlDraft.IdFor(TenantId, ProgramId, "AC-HTTP");
        public Uuid OwnerMemberId => RbacIds.Member(TenantId, OwnerUserId);

        public static async Task<Fixture> CreateAsync(bool activationEnabled)
        {
            var provider = ProgramManagementServices.Build(
                new RecordingPermissionAuthorizer(allowed: true),
                portia => portia.AddRequestHandler<ReviewControlHandler>()
                    .AddRequestHandler<ApproveControlHandler>()
                    .AddRequestHandler<GetCurrentControlVersionHandler>()
                    .AddRequestHandler<GetEffectiveControlVersionHandler>()
                    .AddRequestHandler<ListControlDecisionsHandler>(),
                services => services
                    .AddSingleton(new ControlActivationReleaseGate(activationEnabled))
                    .AddScoped<ControlActivationSource>()
                    .AddSingleton<IControlApplicabilityReferenceValidator, AcceptingValidator>());
            var fixture = new Fixture { Provider = provider };
            var now = DateTimeOffset.UtcNow;
            await ProgramManagementServices.SeedAsync(provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control => control.Create(
                    fixture.ProgramId, Uuid.CreateVersion4(), "AC-HTTP", new ControlDraftContent(
                        "Access review", "Review access", "Management reviews access",
                        "The security lead reviews access quarterly.", ["Dated review record"]),
                    RbacIds.Member(fixture.TenantId, fixture.AuthorUserId), "Author", now));
            await ProgramManagementServices.SeedAsync(provider,
                new Member(fixture.TenantId, fixture.OwnerUserId), member => member.Register());
            await ProgramManagementServices.SeedAsync(provider,
                new ControlDraft(fixture.TenantId, fixture.ControlId), control =>
                    CommandResult(control.AssignResponsibility(new ResponsibilityScope("control",
                            fixture.ControlId, ControlVersionIds.Initial(fixture.ControlId), 1),
                        Uuid.CreateVersion4(), fixture.OwnerMemberId,
                        ResponsibilityType.ControlOwner,
                        RbacIds.Member(fixture.TenantId, fixture.AuthorUserId), "Author", now,
                        now.AddMinutes(-1), null, [])));
            return fixture;
        }

        public RequestScenario Scenario(Uuid userId) => RequestScenario.For(Provider)
            .GivenActor(ProgramManagementServices.Actor(userId));

        public ApproveControl Approve(Uuid reviewId) => new(TenantId, ProgramId, ControlId, 1,
            reviewId, EffectiveFrom, "Ready to operate.");

        public async Task<Uuid> ReviewAsync()
        {
            var requestId = Uuid.CreateVersion4();
            await Scenario(ReviewerUserId)
                .GivenMetadata(new RequestMetadata(requestId, requestId, null))
                .When(new ReviewControl(TenantId, ProgramId, ControlId, 1, "accept", "Reviewed"))
                .ExpectSuccess();
            return requestId;
        }

        public Task SuspendOwnerAsync() => ProgramManagementServices.SeedAsync(Provider,
            new Member(TenantId, OwnerUserId), member => member.Suspend(
                RbacIds.Member(TenantId, AuthorUserId), "Admin", DateTimeOffset.UtcNow,
                "Leave of absence"));

        public Task ReinstateOwnerAsync() => ProgramManagementServices.SeedAsync(Provider,
            new Member(TenantId, OwnerUserId), member => member.Reinstate(
                RbacIds.Member(TenantId, AuthorUserId), "Admin", DateTimeOffset.UtcNow));

        public async Task<TOut> QueryAsync<TRequest, TOut>(TRequest request)
            where TRequest : IRequest<TOut>
        {
            var result = await Scenario(ApproverUserId).When(request).ExpectSuccess();
            return result.Value;
        }

        public async Task<RequestErrorKind> QueryFailureAsync<TRequest>(TRequest request)
            where TRequest : IRequest<ControlVersionView>
        {
            var result = await Scenario(ApproverUserId).When(request).ExpectFailure();
            return result.Error!.Kind;
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

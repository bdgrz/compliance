using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

sealed class ApplicationInventoryAuthorizer(ITenantMembershipDirectoryReader memberships,
    ITenantActivity tenants, IPermissionAuthorizer permissions)
    : IRequestAuthorizer<IApplicationInventoryRequest>
{
    // A narrow V1 grant-backfill checkpoint replays TenantRegistered without replaying broader
    // tenant bootstrap side effects. Record-level policy remains a later authority decision.
    public async ValueTask<Result> AuthorizeAsync(
        IRequestContext<IApplicationInventoryRequest> context, CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Application inventory requires a Bdgrz user identity."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct)
            .ConfigureAwait(false);
        if (membership is null)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The tenant was not found."));
        if (membership.Affiliation == "firm_staff")
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Firm staff require an accepted engagement to access client work."));
        if (!await tenants.IsActiveAsync(tenantId, ct).ConfigureAwait(false))
            return Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The tenant is not active."));
        return await permissions.IsAllowedAsync(tenantId,
                RbacIds.Member(tenantId, userId), RbacPermissions.ApplicationInventoryManage, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "The actor may not inspect or manage this inventory."));
    }
}

static class ApplicationActor
{
    public static (Uuid MemberId, string Display) From<T>(IRequestContext<T> context)
        where T : IRequestBase
    {
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("ApplicationInventoryAuthorizer must reject this actor.");
        var tenantId = ((IApplicationInventoryRequest)context.Request).TenantId;
        return (RbacIds.Member(tenantId, userId),
            UserIdentityClaims.BdgrzDisplay(context.Actor, userId));
    }
}

public sealed class DeclareApplicationHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<DeclareApplication, ApplicationRegistration>
{
    public ValueTask<Result<ApplicationRegistration>> HandleAsync(
        IRequestContext<DeclareApplication> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredApplication(request.TenantId, context.RequestId),
            app => AggregateOutcome.CommitOnSuccess(app.Declare(request.Name, request.Purpose,
                request.OwnerReference, memberId, display, clock.GetUtcNow(),
                request.Classification)), context, ct);
    }
}

public sealed class ReviseApplicationHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<ReviseApplication>
{
    public ValueTask<Result> HandleAsync(IRequestContext<ReviseApplication> context,
        CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredApplication(request.TenantId, request.ApplicationId),
            app => AggregateOutcome.CommitOnSuccess(app.Revise(request.ExpectedRevision,
                request.Name, request.Purpose, request.OwnerReference,
                memberId, display, clock.GetUtcNow(), request.Classification)), context, ct);
    }
}

public sealed class DeclareSystemInstanceHandler(IAggregateExecutor executor, TimeProvider clock)
    : IRequestHandler<DeclareSystemInstance, SystemInstanceRegistration>
{
    public ValueTask<Result<SystemInstanceRegistration>> HandleAsync(
        IRequestContext<DeclareSystemInstance> context, CancellationToken ct)
    {
        var (memberId, display) = ApplicationActor.From(context);
        var request = context.Request;
        return executor.ExecuteAsync(new DeclaredApplication(request.TenantId, request.ApplicationId),
            app => AggregateOutcome.CommitOnSuccess(app.DeclareInstance(
                request.ExpectedApplicationRevision, context.RequestId,
                request.Name, request.Kind, request.AccessBoundaryReference,
                request.SourceIdentifier,
                memberId, display, clock.GetUtcNow())), context, ct);
    }
}

public sealed class GetApplicationHandler(IApplicationDirectoryReader directory,
    IAggregateReader reader) : IRequestHandler<GetApplication, ApplicationView>
{
    public async ValueTask<Result<ApplicationView>> HandleAsync(
        IRequestContext<GetApplication> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.MinimumRevision is < 1)
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        var view = await directory.GetAsync(request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        if (view is not null && (view.TenantId != request.TenantId ||
                                 view.ApplicationId != request.ApplicationId))
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (request.MinimumRevision is { } minimum && (view is null || view.Revision < minimum))
        {
            var source = await reader.HydrateAsync(new DeclaredApplication(request.TenantId,
                request.ApplicationId), ct).ConfigureAwait(false);
            if (!source.IsCreated)
                return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                    "The application was not found."));
            return Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.Conflict,
                source.Revision < minimum
                    ? $"The application source has not reached revision {minimum}."
                    : $"The application projection has not reached revision {minimum}."));
        }
        return view is null
            ? Result<ApplicationView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."))
            : Result<ApplicationView>.Success(view);
    }
}

public sealed class ListApplicationsHandler(IApplicationDirectoryReader directory)
    : IRequestHandler<ListApplications, Page<ApplicationView>>
{
    public async ValueTask<Result<Page<ApplicationView>>> HandleAsync(
        IRequestContext<ListApplications> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The application list limit must be between 1 and 200."));
        Page<ApplicationView> page;
        try
        {
            page = await directory.ListAsync(request.TenantId, request.Limit ?? 50,
                request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.Validation,
                "The application cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId)
            ? Result<Page<ApplicationView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The applications were not found."))
            : Result<Page<ApplicationView>>.Success(page);
    }
}

public sealed class ApplicationHistoryReadConsistency(IApplicationDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid applicationId,
        long? minimumRevision, CancellationToken ct)
    {
        if (minimumRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        var view = await directory.GetAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (view is not null && view.TenantId == tenantId &&
            view.ApplicationId == applicationId &&
            (minimumRevision is null || view.Revision >= minimumRevision))
            return Result.Success;
        var source = await reader.HydrateAsync(new DeclaredApplication(tenantId,
            applicationId), ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        return Result.Failure(new RequestError(RequestErrorKind.Conflict,
            minimumRevision is { } minimum && source.Revision < minimum
                ? $"The application source has not reached revision {minimum}."
                : "The application projection has not reached the requested revision."));
    }
}

public sealed class GetApplicationRevisionHandler(IApplicationDirectoryReader directory,
    ApplicationHistoryReadConsistency consistency)
    : IRequestHandler<GetApplicationRevision, ApplicationRevisionView>
{
    public async ValueTask<Result<ApplicationRevisionView>> HandleAsync(
        IRequestContext<GetApplicationRevision> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId,
            request.ApplicationId, request.Revision, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<ApplicationRevisionView>.Failure(freshness.Error);
        var revision = await directory.GetRevisionAsync(request.TenantId,
            request.ApplicationId, request.Revision, ct).ConfigureAwait(false);
        return revision is not null && revision.TenantId == request.TenantId &&
               revision.ApplicationId == request.ApplicationId &&
               revision.Revision == request.Revision
            ? Result<ApplicationRevisionView>.Success(revision)
            : Result<ApplicationRevisionView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application revision projection is incomplete."));
    }
}

public sealed class ListApplicationRevisionsHandler(IApplicationDirectoryReader directory,
    ApplicationHistoryReadConsistency consistency)
    : IRequestHandler<ListApplicationRevisions, Page<ApplicationRevisionView>>
{
    public async ValueTask<Result<Page<ApplicationRevisionView>>> HandleAsync(
        IRequestContext<ListApplicationRevisions> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The application revision list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId,
            request.ApplicationId, request.MinimumApplicationRevision, ct)
            .ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<ApplicationRevisionView>>.Failure(freshness.Error);
        Page<ApplicationRevisionView>? page;
        try
        {
            page = await directory.ListRevisionsAsync(request.TenantId,
                request.ApplicationId, request.Limit ?? 50, request.Cursor, ct)
                .ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The application revision cursor is invalid."));
        }
        if (page is null || page.Items.Any(item => item.TenantId != request.TenantId ||
            item.ApplicationId != request.ApplicationId))
            return Result<Page<ApplicationRevisionView>>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application revision projection is incomplete."));
        return Result<Page<ApplicationRevisionView>>.Success(page);
    }
}

public sealed class SystemInstanceReadConsistency(IApplicationDirectoryReader directory,
    IAggregateReader reader)
{
    public async ValueTask<Result> EnsureAsync(Uuid tenantId, Uuid applicationId,
        long? minimumApplicationRevision, Uuid? systemInstanceId, CancellationToken ct)
    {
        if (minimumApplicationRevision is < 1)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "The minimum application revision must be positive."));
        var source = await reader.HydrateAsync(new DeclaredApplication(tenantId, applicationId),
            ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The application was not found."));
        if (systemInstanceId is { } instanceId && !source.HasInstance(instanceId))
            return Result.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instance was not found."));
        if (minimumApplicationRevision is { } minimum && source.Revision < minimum)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The application source has not reached revision {minimum}.",
                isTransient: true));
        var projection = await directory.GetAsync(tenantId, applicationId, ct)
            .ConfigureAwait(false);
        if (projection is null || projection.TenantId != tenantId ||
            projection.ApplicationId != applicationId || projection.Revision < source.Revision)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The application projection has not reached the source revision.",
                isTransient: true));
        return Result.Success;
    }
}

public sealed class GetSystemInstanceHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency)
    : IRequestHandler<GetSystemInstance, SystemInstanceView>
{
    public async ValueTask<Result<SystemInstanceView>> HandleAsync(
        IRequestContext<GetSystemInstance> context, CancellationToken ct)
    {
        var request = context.Request;
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            request.MinimumApplicationRevision, request.SystemInstanceId, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<SystemInstanceView>.Failure(freshness.Error);
        var view = await directory.GetInstanceAsync(request.TenantId, request.SystemInstanceId, ct)
            .ConfigureAwait(false);
        if (view is null)
            return Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.Conflict,
                "The system instance projection is incomplete.", isTransient: true));
        return view.TenantId != request.TenantId ||
               view.ApplicationId != request.ApplicationId ||
               view.SystemInstanceId != request.SystemInstanceId
            ? Result<SystemInstanceView>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instance was not found."))
            : Result<SystemInstanceView>.Success(view);
    }
}

public sealed class ListSystemInstancesHandler(IApplicationDirectoryReader directory,
    SystemInstanceReadConsistency consistency)
    : IRequestHandler<ListSystemInstances, Page<SystemInstanceView>>
{
    public async ValueTask<Result<Page<SystemInstanceView>>> HandleAsync(
        IRequestContext<ListSystemInstances> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.Limit is < 1 or > 200)
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation,
                "The system instance list limit must be between 1 and 200."));
        var freshness = await consistency.EnsureAsync(request.TenantId, request.ApplicationId,
            request.MinimumApplicationRevision, null, ct).ConfigureAwait(false);
        if (!freshness.IsSuccess)
            return Result<Page<SystemInstanceView>>.Failure(freshness.Error);
        Page<SystemInstanceView> page;
        try
        {
            page = await directory.ListInstancesAsync(request.TenantId, request.ApplicationId,
                request.Limit ?? 50, request.Cursor, ct).ConfigureAwait(false);
        }
        catch (KvDirectoryQueryException)
        {
            return Result<Page<SystemInstanceView>>.Failure(new RequestError(
                RequestErrorKind.Validation, "The system instance cursor is invalid."));
        }
        return page.Items.Any(item => item.TenantId != request.TenantId ||
                                      item.ApplicationId != request.ApplicationId)
            ? Result<Page<SystemInstanceView>>.Failure(new RequestError(RequestErrorKind.NotFound,
                "The system instances were not found."))
            : Result<Page<SystemInstanceView>>.Success(page);
    }
}

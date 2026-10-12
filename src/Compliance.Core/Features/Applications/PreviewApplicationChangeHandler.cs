using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reports the known boundary impact without authorizing an application change.</summary>
public sealed class PreviewApplicationChangeHandler(
    IAggregateReader aggregates, IApplicationDirectoryReader applications,
    IApplicationBoundaryReferenceDirectory references,
    ApplicationBoundaryReferenceReadConsistency boundaryConsistency,
    IApplicationControlDraftReferenceDirectory controls,
    ApplicationControlDraftReferenceReadConsistency controlConsistency,
    RestrictedApplicationVisibility visibility, ApplicationGovernedRevisionGate? gate = null)
    : IRequestHandler<PreviewApplicationChange, ApplicationChangePreview>
{
    static readonly string[] MissingContexts =
        ["application_relationships", "vendors", "approved_control_versions_and_lifecycle_impact",
            "system_instance_control_draft_references", "policies", "evidence_sources", "access_populations",
            "review_campaigns", "open_work", "readiness", "engagements"];

    public async ValueTask<Result<ApplicationChangePreview>> HandleAsync(
        IRequestContext<PreviewApplicationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var inputError = Validate(request);
        if (inputError is not null)
            return Result<ApplicationChangePreview>.Failure(inputError);

        var source = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        if (!await visibility.CanReadApplicationAsync(request.TenantId, userId,
                request.ApplicationId, ct).ConfigureAwait(false))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (source.IsRetired)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application is retired."));
        if (source.Revision != request.ExpectedApplicationRevision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                $"The application is at revision {source.Revision}; preview revision {request.ExpectedApplicationRevision} is stale."));

        ApplicationRevisionContent? content = null;
        if (request.ChangeKind == "revise")
        {
            var invalid = source.ResolveRevisionContent(request.ExpectedApplicationRevision,
                request.Name!, request.Purpose!, request.OwnerReference, request.Classification,
                request.SystemOwnerPersonId, request.AccessOwnerPersonId, null,
                request.ApplicationKind, request.Criticality, gate?.IsEnabled ?? false,
                request.ClassificationIntent, request.SystemOwnerIntent, request.AccessOwnerIntent,
                out var resolved);
            if (invalid is not null)
                return Result<ApplicationChangePreview>.Failure(invalid.ToRequestError());
            var owners = await ApplicationOwnerReferences.ValidateAsync(aggregates,
                request.TenantId, resolved.SystemOwnerPersonId, resolved.AccessOwnerPersonId, ct)
                .ConfigureAwait(false);
            if (owners is not null)
                return Result<ApplicationChangePreview>.Failure(owners);
            content = resolved;
        }

        var current = await applications.GetAsync(request.TenantId, request.ApplicationId, ct)
            .ConfigureAwait(false);
        if (current is null || current.Revision < source.Revision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application projection has not reached the source revision.",
                isTransient: true));
        if (current.TenantId != request.TenantId || current.ApplicationId != request.ApplicationId)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (current.Revision != source.Revision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application source and projection changed during preview.",
                isTransient: true));

        var relationshipSourceRevision = source.SourceRelationshipRevision;
        var relationships = await ApplicationRelationshipProjection.ReadAsync(source,
            request.TenantId, userId, visibility, ct).ConfigureAwait(false);

        var caughtUp = await boundaryConsistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                caughtUp.Error.Kind, caughtUp.Error.Message, isTransient: true));
        var controlsCaughtUp = await controlConsistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!controlsCaughtUp.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                controlsCaughtUp.Error.Kind, controlsCaughtUp.Error.Message, isTransient: true));
        var page = await references.ListAsync(request.TenantId, "application",
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.SubjectType != "application" ||
                                   item.GovernedRecordId != request.ApplicationId))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application boundary reference projection is inconsistent.",
                isTransient: true));
        var controlPage = await controls.ListAsync(request.TenantId, "application",
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (controlPage.Items.Any(item => item.TenantId != request.TenantId ||
                                          item.SubjectType != "application" ||
                                          item.GovernedRecordId != request.ApplicationId))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict,
                "The application control draft reference projection is inconsistent.",
                isTransient: true));

        var instancePage = await applications.ListInstancesAsync(request.TenantId,
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (instancePage.Items.Any(item => item.TenantId != request.TenantId ||
                                           item.ApplicationId != request.ApplicationId))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The system instance projection is inconsistent.",
                isTransient: true));

        var boundaryCaughtUpAfterRead = await boundaryConsistency.EnsureCaughtUpAsync(
            request.TenantId, ct).ConfigureAwait(false);
        if (!boundaryCaughtUpAfterRead.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                boundaryCaughtUpAfterRead.Error.Kind, boundaryCaughtUpAfterRead.Error.Message,
                isTransient: true));
        var controlsCaughtUpAfterRead = await controlConsistency.EnsureCaughtUpAsync(
            request.TenantId, ct).ConfigureAwait(false);
        if (!controlsCaughtUpAfterRead.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                controlsCaughtUpAfterRead.Error.Kind, controlsCaughtUpAfterRead.Error.Message,
                isTransient: true));

        // Source rechecks catch a pending relationship write that raced the preview reads.
        // This preview remains advisory; future approval must use a complete, exact-version impact gate.
        var latest = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (latest.Revision != source.Revision ||
            latest.SourceRelationshipRevision != relationshipSourceRevision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application or its relationships changed during preview.",
                isTransient: true));

        List<string> pending = [];
        if (page.NextCursor is not null)
            pending.Add("boundary_references_over_limit");
        if (controlPage.NextCursor is not null)
            pending.Add("control_draft_references_over_limit");
        if (instancePage.NextCursor is not null)
            pending.Add("system_instance_references_over_limit");
        pending.AddRange(MissingContexts);
        return Result<ApplicationChangePreview>.Success(new ApplicationChangePreview(
            request.TenantId, request.ApplicationId, source.Revision, request.ChangeKind,
            Changes(current, content), page.Items, pending, false)
        {
            ApplicationRelationships = relationships.ActiveItems,
            ControlDraftReferences = controlPage.Items,
            SystemInstanceReferences = instancePage.Items,
        });
    }

    static RequestError? Validate(PreviewApplicationChange request)
    {
        if (request.ExpectedApplicationRevision < 1)
            return new RequestError(RequestErrorKind.Validation,
                "The expected application revision must be positive.");
        if (request.ChangeKind == "retire")
            return request.Name is null && request.Purpose is null &&
                   request.OwnerReference is null && request.Classification is null &&
                   request.SystemOwnerPersonId is null && request.AccessOwnerPersonId is null &&
                   request.ApplicationKind is null && request.Criticality is null ? null :
                new RequestError(RequestErrorKind.Validation,
                    "A retirement preview cannot include revised application fields.");
        if (request.ChangeKind != "revise")
            return new RequestError(RequestErrorKind.Validation,
                "The change kind must be revise or retire.");
        return null;
    }

    static List<ApplicationFieldChange> Changes(ApplicationView current,
        ApplicationRevisionContent? content)
    {
        if (content is null)
            return [];
        var changes = new List<ApplicationFieldChange>();
        Add("name", current.Name, content.Name);
        Add("purpose", current.Purpose, content.Purpose);
        Add("owner_reference", current.OwnerReference, content.OwnerReference);
        Add("classification", current.Classification, content.Classification);
        Add("system_owner_person_id", current.SystemOwnerPersonId?.ToString(),
            content.SystemOwnerPersonId?.ToString());
        Add("access_owner_person_id", current.AccessOwnerPersonId?.ToString(),
            content.AccessOwnerPersonId?.ToString());
        Add("application_kind", current.ApplicationKind, content.ApplicationKind);
        Add("criticality", current.Criticality, content.Criticality);
        return changes;

        void Add(string field, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                changes.Add(new ApplicationFieldChange(field, before, after));
        }
    }
}

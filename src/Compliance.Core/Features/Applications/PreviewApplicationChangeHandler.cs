using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reports the known boundary impact without authorizing an application change.</summary>
public sealed class PreviewApplicationChangeHandler(
    IAggregateReader aggregates, IApplicationDirectoryReader applications,
    IApplicationBoundaryReferenceDirectory references,
    ApplicationBoundaryReferenceReadConsistency boundaryConsistency,
    IApplicationControlDraftReferenceDirectory controls,
    ApplicationControlDraftReferenceReadConsistency controlConsistency,
    RestrictedApplicationVisibility visibility,
    SystemInstanceReadConsistency instanceConsistency)
    : IRequestHandler<PreviewApplicationChange, ApplicationChangePreview>,
      IApplicationChangeImpactReader, IApplicationImportRetirementImpactReader
{
    static readonly string[] MissingContexts =
        ["application_relationships", "vendors", "approved_control_versions_and_lifecycle_impact",
            "system_instance_control_draft_references", "policies", "evidence_sources", "access_populations",
            "review_campaigns", "open_work", "readiness", "engagements",
            "cross_source_manual_reliance"];

    public async ValueTask<Result<ApplicationChangePreview>> HandleAsync(
        IRequestContext<PreviewApplicationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : Uuid.Empty;
        return await ReadAsync(request, userId, ct).ConfigureAwait(false);
    }

    public async ValueTask<Result<ApplicationChangePreview>> ReadAsync(
        PreviewApplicationChange request, Uuid userId, CancellationToken ct)
        => await ReadCoreAsync(request, userId, requireActorVisibility: true, ct).ConfigureAwait(false);

    public ValueTask<Result<ApplicationChangePreview>> ReadForImportWorkerAsync(
        PreviewApplicationChange request, CancellationToken ct) =>
        ReadCoreAsync(request, Uuid.Empty, requireActorVisibility: false, ct);

    async ValueTask<Result<ApplicationChangePreview>> ReadCoreAsync(
        PreviewApplicationChange request, Uuid userId, bool requireActorVisibility,
        CancellationToken ct)
    {
        var inputError = Validate(request);
        if (inputError is not null)
            return Result<ApplicationChangePreview>.Failure(inputError);

        var source = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
        if (requireActorVisibility && !await visibility.CanReadApplicationAsync(request.TenantId, userId,
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

        var boundaryFence = await boundaryConsistency.CaptureAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!boundaryFence.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(boundaryFence.Error);
        var controlFence = await controlConsistency.CaptureAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!controlFence.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(controlFence.Error);
        var instanceFence = await instanceConsistency.CaptureApplicationListAsync(
            request.TenantId, ct).ConfigureAwait(false);
        if (!instanceFence.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(instanceFence.Error);
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

        var boundaryConfirmed = await boundaryConsistency.ConfirmUnchangedAndCaughtUpAsync(
            request.TenantId, boundaryFence.Value, ct).ConfigureAwait(false);
        if (!boundaryConfirmed.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(boundaryConfirmed.Error);
        var controlsConfirmed = await controlConsistency.ConfirmUnchangedAndCaughtUpAsync(
            request.TenantId, controlFence.Value, ct).ConfigureAwait(false);
        if (!controlsConfirmed.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(controlsConfirmed.Error);
        var instancesConfirmed = await instanceConsistency
            .ConfirmApplicationListUnchangedAndCaughtUpAsync(request.TenantId,
                instanceFence.Value, ct).ConfigureAwait(false);
        if (!instancesConfirmed.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(instancesConfirmed.Error);

        // Source rechecks catch an application write that raced any of the bounded projection reads.
        var latest = await aggregates.HydrateApplicationAsync(request.TenantId, request.ApplicationId, ct).ConfigureAwait(false);
        if (latest.Revision != source.Revision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application changed during preview.",
                isTransient: true));

        List<string> pending = [];
        if (page.NextCursor is not null)
            pending.Add("boundary_references_over_limit");
        if (controlPage.NextCursor is not null)
            pending.Add("control_draft_references_over_limit");
        if (instancePage.NextCursor is not null)
            pending.Add("system_instance_references_over_limit");
        pending.AddRange(MissingContexts);
        var preview = new ApplicationChangePreview(
            request.TenantId, request.ApplicationId, source.Revision, request.ChangeKind,
            Changes(current, request), page.Items, pending, false)
        {
            ControlDraftReferences = controlPage.Items,
            SystemInstanceReferences = instancePage.Items,
        };
        return Result<ApplicationChangePreview>.Success(preview with
        {
            ImpactDigest = ApplicationChangeImpactDigest.Compute(preview),
        });
    }

    static RequestError? Validate(PreviewApplicationChange request)
    {
        if (request.ExpectedApplicationRevision < 1)
            return new RequestError(RequestErrorKind.Validation,
                "The expected application revision must be positive.");
        if (request.ChangeKind == "retire")
            return request.Name is null && request.Purpose is null &&
                   request.OwnerReference is null && request.Classification is null ? null :
                new RequestError(RequestErrorKind.Validation,
                    "A retirement preview cannot include revised application fields.");
        if (request.ChangeKind != "revise")
            return new RequestError(RequestErrorKind.Validation,
                "The change kind must be revise or retire.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Length > 2000 ||
            request.OwnerReference is { Length: > 2000 } ||
            request.Classification is { Length: > 200 })
            return new RequestError(RequestErrorKind.Validation,
                "A revision preview requires bounded name, purpose, owner, and classification fields.");
        return null;
    }

    static List<ApplicationFieldChange> Changes(ApplicationView current,
        PreviewApplicationChange request)
    {
        if (request.ChangeKind == "retire")
            return [];
        var changes = new List<ApplicationFieldChange>();
        Add("name", current.Name, request.Name!.Trim());
        Add("purpose", current.Purpose, request.Purpose!.Trim());
        Add("owner_reference", current.OwnerReference,
            string.IsNullOrWhiteSpace(request.OwnerReference) ? null :
                request.OwnerReference.Trim());
        Add("classification", current.Classification,
            string.IsNullOrWhiteSpace(request.Classification) ? null :
                request.Classification.Trim());
        return changes;

        void Add(string field, string? before, string? after)
        {
            if (!string.Equals(before, after, StringComparison.Ordinal))
                changes.Add(new ApplicationFieldChange(field, before, after));
        }
    }
}

using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications;

/// <summary>Reports the known boundary impact without authorizing an application change.</summary>
public sealed class PreviewApplicationChangeHandler(
    IAggregateReader aggregates, IApplicationDirectoryReader applications,
    IApplicationBoundaryReferenceDirectory references,
    ApplicationBoundaryReferenceReadConsistency boundaryConsistency)
    : IRequestHandler<PreviewApplicationChange, ApplicationChangePreview>
{
    static readonly string[] MissingContexts =
        ["vendors", "controls", "policies", "evidence_sources", "access_populations",
            "review_campaigns", "open_work", "readiness", "engagements"];

    public async ValueTask<Result<ApplicationChangePreview>> HandleAsync(
        IRequestContext<PreviewApplicationChange> context, CancellationToken ct)
    {
        var request = context.Request;
        var inputError = Validate(request);
        if (inputError is not null)
            return Result<ApplicationChangePreview>.Failure(inputError);

        var source = await aggregates.HydrateAsync(new DeclaredApplication(
            request.TenantId, request.ApplicationId), ct).ConfigureAwait(false);
        if (!source.IsCreated)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.NotFound, "The application was not found."));
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

        var caughtUp = await boundaryConsistency.EnsureCaughtUpAsync(request.TenantId, ct)
            .ConfigureAwait(false);
        if (!caughtUp.IsSuccess)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                caughtUp.Error.Kind, caughtUp.Error.Message, isTransient: true));
        var page = await references.ListAsync(request.TenantId, "application",
            request.ApplicationId, 200, null, ct).ConfigureAwait(false);
        if (page.Items.Any(item => item.TenantId != request.TenantId ||
                                   item.SubjectType != "application" ||
                                   item.GovernedRecordId != request.ApplicationId))
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application boundary reference projection is inconsistent.",
                isTransient: true));

        // A second source check detects an edit that raced the projection reads. This preview
        // remains advisory; future approval must use a complete, exact-version impact gate.
        var latest = await aggregates.HydrateAsync(new DeclaredApplication(
            request.TenantId, request.ApplicationId), ct).ConfigureAwait(false);
        if (latest.Revision != source.Revision)
            return Result<ApplicationChangePreview>.Failure(new RequestError(
                RequestErrorKind.Conflict, "The application changed during preview.",
                isTransient: true));

        IReadOnlyList<string> pending = page.NextCursor is null
            ? MissingContexts
            : ["boundary_references_over_limit", .. MissingContexts];
        return Result<ApplicationChangePreview>.Success(new ApplicationChangePreview(
            request.TenantId, request.ApplicationId, source.Revision, request.ChangeKind,
            Changes(current, request), page.Items, pending, false));
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

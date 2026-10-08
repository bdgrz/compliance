using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Readiness;

/// <summary>Protects existing advisor feedback while leaving shared management readiness reads under ordinary grants.</summary>
sealed class ReadinessAnnotationCompartmentAuthorizer(ITenantMembershipDirectoryReader memberships,
    ClientCompartmentIndependenceGuard independence) : IRequestAuthorizer<ListReadinessAnnotations>
{
    public async ValueTask<Result> AuthorizeAsync(IRequestContext<ListReadinessAnnotations> context,
        CancellationToken ct)
    {
        if (!UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var userId) || RequestActor.IsSystem(context.Actor))
            return Result.Failure(new RequestError(RequestErrorKind.Unauthorized,
                "Advisory feedback requires an attributable Bdgrz user."));
        var tenantId = context.Request.TenantId;
        var membership = await memberships.GetAsync(tenantId.ToString(), userId, ct).ConfigureAwait(false);
        if (membership is null || membership.TenantId != tenantId || membership.UserId != userId ||
            membership.IsSuspended || membership.IsDeprovisioned)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The tenant was not found."));
        return await independence.CanReadAsync(tenantId, userId, RecordCompartment.AdvisoryWorkingNotes, ct)
            .ConfigureAwait(false)
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Forbidden,
                "Actual Attest assignment history prevents reading this client's advisory working notes."));
    }
}

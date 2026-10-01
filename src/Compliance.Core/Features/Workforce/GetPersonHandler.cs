using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class GetPersonHandler(PersonReadConsistency consistency,
    IPermissionAuthorizer permissions)
    : IRequestHandler<GetPerson, PersonView>
{
    public async ValueTask<Result<PersonView>> HandleAsync(IRequestContext<GetPerson> context,
        CancellationToken ct) =>
        await GetAndRedactAsync(context, ct).ConfigureAwait(false);

    async ValueTask<Result<PersonView>> GetAndRedactAsync(IRequestContext<GetPerson> context,
        CancellationToken ct)
    {
        var request = context.Request;
        var view = await consistency.GetAsync(request.TenantId, request.PersonId,
            request.MinimumRevision, ct).ConfigureAwait(false);
        if (!view.IsSuccess)
            return view;
        var userId = UserIdentityClaims.TryGetBdgrzSubject(context.Actor, out var subject)
            ? subject
            : throw new InvalidOperationException("WorkforceAuthorizer must reject this actor.");
        var personalDetails = await FieldRestrictions.ForActorAsync(permissions, request.TenantId,
            userId, FieldClasses.WorkforcePersonalDetails, ct).ConfigureAwait(false);
        return personalDetails.CanRead
            ? view
            : Result<PersonView>.Success(view.Value with
            {
                PersonalContact = null,
                RestrictedFieldsRedacted = true,
            });
    }
}

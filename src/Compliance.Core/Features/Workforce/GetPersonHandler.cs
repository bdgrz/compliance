using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

public sealed class GetPersonHandler(PersonReadConsistency consistency)
    : IRequestHandler<GetPerson, PersonView>
{
    public ValueTask<Result<PersonView>> HandleAsync(IRequestContext<GetPerson> context,
        CancellationToken ct) =>
        consistency.GetAsync(context.Request.TenantId, context.Request.PersonId,
            context.Request.MinimumRevision, ct);
}

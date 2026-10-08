using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Programs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Controls;

/// <summary>
///     Designates a governed workforce person, who need not sign in, as owner of the exact
///     pending draft revision; a null <c>PersonId</c> clears the designation. Activation verifies
///     the person against the workforce roster.
/// </summary>
[Discriminator("bdgrz.control.owner_person.designate", 1)]
public sealed record DesignateControlOwnerPerson(Uuid TenantId, Uuid ProgramId,
    Uuid ControlId, long ExpectedRevision, Uuid? PersonId, string Rationale)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;

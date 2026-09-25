using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

public interface IEmailOwnershipRequest : IRequestBase
{
    Uuid UserId { get; }
}
